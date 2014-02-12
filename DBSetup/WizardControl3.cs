using DBSetup.Common;
using DBSetup.Common.Helpers;
using DBSetup.Common.ModelBuilder;
using DBSetup.Common.Models;
using DBSetup.Helpers;
using DBSetup.States;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DBSetup
{
	public partial class WizardControl3 : UserControl
	{
		private const string _chckSettingsMsg = "Please, check the correctness of settings file.";
		private WizardMain rootControl;
		private GenericWeakReference<List<SectionBase>> _parsingResWeak;
		private IVersionService versionService;
		private int _latestComm4Version = -1;
		private FullModelBuilder _fullBuilder;
		private DbSetupType _setupType = DbSetupType.None;

		public WizardControl3()
		{
			InitializeComponent();

			cmbGrowthType.SelectedIndex = 0;
			cmbLogGrowthType.SelectedIndex = 0;

			cmbLanguage.DisplayMember = "LanguageName";
			cmbSetupType.DisplayMember = "Text";
		}

		private void WizardControl3_Load(object sender, EventArgs e)
		{
			if (this.TopLevelControl != null && TopLevelControl is WizardMain)
			{
				rootControl = (TopLevelControl as WizardMain);
				rootControl.AcceptButton = btnNext;
			}

			this.DisableCtrlPlusA();

			ModelBuilderContext context = new ModelBuilderContext();
			_fullBuilder = new FullModelBuilder();
			_fullBuilder.OpenFile((StateContainer.Instance[0] as ConfigFileState).FilePath);
			context.SetBuilder(_fullBuilder);

			var loadTask = Task.Factory.StartNew<List<SectionBase>>(() =>
			{
				versionService = ServiceLocator.Instance.GetService<Common.IVersionService>();
				string versionFile = System.Configuration.ConfigurationManager.AppSettings["versionControl"];
				//retrieve latest version
				versionService.SetSource(Path.Combine(Path.GetDirectoryName((StateContainer.Instance[0] as ConfigFileState).FilePath),
																			versionFile));
				_latestComm4Version = versionService.RetrieveVersion();
				versionService = null;//make rootless as far as this controll will never be garbage collected!!!
				return context.ExecuteBuild();
			});

			loadTask.RegisterFaultedHandler(OnError, TaskScheduler.FromCurrentSynchronizationContext());
			loadTask.RegisterSucceededHandler(OnSuccess, TaskScheduler.FromCurrentSynchronizationContext());
		}

		#region payload execution handlers
		private void OnSuccess(List<SectionBase> result)
		{
			_parsingResWeak = new GenericWeakReference<List<SectionBase>>(result);
			//read languages section from object tree
			var sectionLanguages = _parsingResWeak.Target.FirstOrDefault(x => x.Text.Equals("Languages", StringComparison.OrdinalIgnoreCase));
			if (sectionLanguages != null)
			{
				var languages = sectionLanguages.Children.OfType<Language>().ToList();
				if (languages != null)
				{
					cmbLanguage.Items.Clear();
					(StateContainer.Instance[3] as DbSetupState).Languages = languages;
					cmbLanguage.Items.AddRange(languages.ToArray());
				}
			}
			else
				Log.Instance.Error("Fail to find section [Languages]");

			var sectionsSetupConfig = _parsingResWeak.Target.Where(x => x.Text.Contains("Setup Configurations")).ToList();
			if (sectionsSetupConfig != null && sectionsSetupConfig.Count > 0)
				(StateContainer.Instance[3] as DbSetupState).SetupTypes = sectionsSetupConfig;

			var unusedSection = _parsingResWeak.Target.FirstOrDefault(x => x.Text.Equals("Unused Sections", StringComparison.OrdinalIgnoreCase));
			if (unusedSection != null)
			{	//read database configuration settings
				var databaseConfig = unusedSection.Children.FirstOrDefault(x => x.Text.Equals("Database Configurations", StringComparison.OrdinalIgnoreCase));
				if (databaseConfig != null)
				{
					(StateContainer.Instance[3] as DbSetupState).DatabaseConfiguration = databaseConfig;
					InitializaDatabaseSettings(databaseConfig);
				}
				else
					Log.Instance.Error("Fail to find section [Database Configurations]");
			}
			//according to the latest Andy feedback, if only 1 language is avaliable, select it by default
			if (cmbLanguage.Items.Count == 1)
				cmbLanguage.SelectedItem = cmbLanguage.Items[0];

			//if (cmbLanguage.DroppedDown)	cmbLanguage.DroppedDown = false;
		}

		private void OnError(Exception exc)
		{
			Log.Instance.Error("Error has occurred while building object model.", exc);

			Exception castedExc = null;
			if (exc is AggregateException)
				castedExc = (exc as AggregateException).Flatten().InnerException;
			else
				castedExc = exc;

			if (castedExc is FileNotFoundException)
				MessageBox.Show(rootControl, "Couldn't find file: {0}{1}{2}".FormatWith((castedExc as FileNotFoundException).FileName, Environment.NewLine,
																					_chckSettingsMsg));
			else if (castedExc is DirectoryNotFoundException)
				MessageBox.Show(rootControl, "{0}{1}{2}".FormatWith((castedExc as DirectoryNotFoundException).Message, Environment.NewLine,
															_chckSettingsMsg));
			else
				MessageBox.Show(rootControl, "Error has occured while parsing db setup file.");
		}
		#endregion

		private void cmbLanguage_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (cmbLanguage.SelectedItem != null && (StateContainer.Instance[3] != null) &&
				(StateContainer.Instance[3] as DbSetupState).Languages != null)
			{
				var selLanguage = (cmbLanguage.SelectedItem as Language);
				(StateContainer.Instance[3] as DbSetupState).SelectedLanguage = selLanguage;
				var changedSetupConfig = (StateContainer.Instance[3] as DbSetupState).SetupTypes.
										FirstOrDefault(x => x.Text.IndexOf(selLanguage.LanguageName, StringComparison.OrdinalIgnoreCase) > 0);

				if (changedSetupConfig != null && changedSetupConfig.Children != null)
				{
					cmbSetupType.Items.Clear();
					(StateContainer.Instance[3] as DbSetupState).SelectedSetupType = changedSetupConfig;
					foreach (var subItem in changedSetupConfig.Children)
					{
						cmbSetupType.Items.Add(subItem);
					}

					RetrieveUpgradeTypeIfNeed(changedSetupConfig);
				}
			}
		}

		private void RetrieveUpgradeTypeIfNeed(SectionBase changedSetupConfig)
		{
			if (changedSetupConfig == null) return;
			// Check if comm4 is exists in case of true check appropriate upgrade value
			try
			{
				int comm4Version;
				SectionBase upgradeType;
				// Retrieve comm4 version in int format
				if ((StateContainer.Instance[2] as SqlServerReportState).IsComm4Exists &&
					Int32.TryParse((StateContainer.Instance[2] as SqlServerReportState).Comm4Version, out comm4Version))
				{
					//get proper upgrade setup type based on comm4 version
					upgradeType = SectionBaseExtension.GetProperUpgrade(changedSetupConfig.Children, comm4Version);
					if (comm4Version == _latestComm4Version)
					{
						bool showPrompt = true;
						bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["promtInCaseOfLatestDbVersion"], out showPrompt);
						string textFromConfig = System.Configuration.ConfigurationManager.AppSettings["dbIsUpToDateText"];
						string messageText = string.IsNullOrEmpty(textFromConfig) ? StringsContainer.Instance.LatestVestionUpToDate : textFromConfig;
						if (showPrompt &&
							MessageBox.Show(rootControl, messageText, string.Empty, MessageBoxButtons.YesNo, MessageBoxIcon.Information) ==
							DialogResult.No)
						{
							this.RevertCtrlPlusA();
							Program.ISExitRequired = true;
							Application.Exit();
						}

						//this fix was added to eliminate ability of database upgrade which has the latest version
						//Since TTP 5000 was introduced we have changed a bit logic described above.
						var updateItems = new List<SectionBase>(cmbSetupType.Items.OfType<SectionBase>().Where(x => x.Text.Contains("Update")));
						foreach (var updItem in updateItems)
						{
							if (upgradeType != null && !updItem.Equals(upgradeType))
								cmbSetupType.Items.Remove(updItem);
						}
						updateItems.Clear();
						return;
					}

					if (upgradeType == null)
					{
						Log.Instance.Error(string.Format("Fail to get proper upgrade type for DB version {0}",
														(StateContainer.Instance[2] as SqlServerReportState).Comm4Version));

						MessageBox.Show(rootControl, string.Format(StringsContainer.Instance.FailToDetermineUpgradeType,
																	(StateContainer.Instance[2] as SqlServerReportState).Comm4Version),
										"Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					}
					else
					{
						Log.Instance.Info(string.Format("Appropriate upgrade type was successfully found.{0}{1}", Environment.NewLine, upgradeType.ToString()));
						cmbSetupType.SelectedItem = upgradeType;
						Log.Instance.Info(string.Format("select type was auto populated: {0}", upgradeType.ToString()));
						(StateContainer.Instance[3] as DbSetupState).SelectedSetupType = upgradeType;
						(StateContainer.Instance[3] as DbSetupState).DatabaseSetupType = DbSetupType.Upgrade;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Instance.Error("Fail to retrieve proper upgrade type.", ex);
				MessageBox.Show(rootControl, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		#region button handlers

		private void InitializaDatabaseSettings(SectionBase databaseConfig)
		{
			if (databaseConfig == null)
				throw new ArgumentNullException("databaseConfig");

			var dbName = databaseConfig.Children.OfType<SettingsPair>().FirstOrDefault(x => x.Key.Equals("Name", StringComparison.OrdinalIgnoreCase));
			txtDbFileName.Text = dbName == null ? string.Empty : dbName.Value;

			var dbSize = databaseConfig.Children.OfType<SettingsPair>().FirstOrDefault(x => x.Key.Equals("DataDeviceSize", StringComparison.OrdinalIgnoreCase));
			txtInitialSize.Text = dbSize == null ? string.Empty : dbSize.Value;

			int size;
			if (Int32.TryParse(txtInitialSize.Text, out size))
			{
				txtGrowth.Text = (size + 1).ToString();
			}

			var logSize = databaseConfig.Children.OfType<SettingsPair>().FirstOrDefault(x => x.Key.Equals("LogDeviceSize", StringComparison.OrdinalIgnoreCase));
			txtLogInitialSize.Text = logSize == null ? string.Empty : logSize.Value;

			if (Int32.TryParse(txtLogInitialSize.Text, out size))
			{
				if (size == 1)
					txtLogGrowth.Text = "1";
				else
					txtLogGrowth.Text = (size - 1).ToString();
			}

			var dataDevice = databaseConfig.Children.OfType<SettingsPair>().FirstOrDefault(x => x.Key.Equals("DataDevice", StringComparison.OrdinalIgnoreCase));
			txtDbLocation.Text = dataDevice == null ? string.Empty : System.IO.Path.ChangeExtension(dataDevice.Value, "mdf");

			var logDevice = databaseConfig.Children.OfType<SettingsPair>().FirstOrDefault(x => x.Key.Equals("LogDevice", StringComparison.OrdinalIgnoreCase));
			txtLogFileLocation.Text = logDevice == null ? string.Empty : System.IO.Path.ChangeExtension(logDevice.Value, "ldf");

			if (!string.IsNullOrEmpty(txtLogFileLocation.Text))
				txtDBLogName.Text = System.IO.Path.GetFileNameWithoutExtension(txtLogFileLocation.Text);
		}

		private void btnPrevious_Click(object sender, EventArgs e)
		{
			this.RevertCtrlPlusA();
			if (rootControl != null)
			{
				RootlessAcceptButton();
				rootControl.Controls.Clear();
				rootControl.Controls.Add(new WizardControl2());
				Unsubscribe();
				rootControl = null;
			}
			MakeBuilderRootless();
		}

		private void Unsubscribe()
		{
			this.Load -= WizardControl3_Load;
		}

		private void folderFailed(Exception exc)
		{
			Exception castedExc = null;
			if (exc is AggregateException)
				castedExc = (exc as AggregateException).Flatten().InnerException;
			else
				castedExc = exc;

			Log.Instance.Warn(string.Format("Fail to create directory.{0}{1}", Environment.NewLine, castedExc.Message));
			MessageBox.Show(rootControl, string.Format("Fail to create directory.{0}{1}", Environment.NewLine, castedExc.Message),
							"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}

		private void btnNext_Click(object sender, EventArgs e)
		{
			if (!string.IsNullOrEmpty(txtDbLocation.Text) &&
				!Directory.Exists(Path.GetDirectoryName(txtDbLocation.Text)))
			{
				var creationTask = Task.Factory.StartNew(() => Directory.CreateDirectory(Path.GetDirectoryName(txtDbLocation.Text)));
				creationTask.RegisterFaultedHandler(folderFailed, TaskScheduler.FromCurrentSynchronizationContext());
			}

			if (!string.IsNullOrEmpty(txtLogFileLocation.Text) &&
				!Directory.Exists(Path.GetDirectoryName(txtLogFileLocation.Text)))
			{
				var creationTask = Task.Factory.StartNew(() => Directory.CreateDirectory(Path.GetDirectoryName(txtLogFileLocation.Text)));
				creationTask.RegisterFaultedHandler(folderFailed, TaskScheduler.FromCurrentSynchronizationContext());
			}

			if (cmbLanguage.SelectedItem == null || cmbSetupType.SelectedItem == null)
			{
				MessageBox.Show(rootControl, StringsContainer.Instance.FieldsAreEmpty);
				return;
			}

			SaveState();

			Log.Instance.Info(string.Format("User has selected language: {0}{1}Selected setup type is: {2}",
											cmbLanguage.SelectedItem, Environment.NewLine, cmbSetupType.SelectedItem));

			bool needToContinue = false;

			if (StateContainer.Instance.GetConcreteInstance<DbSetupState>().DatabaseSetupType == DbSetupType.New)
			{
				needToContinue = true;

				if (StateContainer.Instance.GetConcreteInstance<SqlServerReportState>().IsComm4Exists)
				{
					if (MessageBox.Show(rootControl, StringsContainer.Instance.DbIsExistsMessage, "Overwrite?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
						 MessageBoxDefaultButton.Button2) == DialogResult.No)
						needToContinue = false;
					Helpers.UiHelper.CloseWindow("Overwrite?");
				}
			}
			else if (StateContainer.Instance.GetConcreteInstance<DbSetupState>().DatabaseSetupType == DbSetupType.Upgrade)
				needToContinue = true;

			if (!needToContinue)
				return;

			this.RevertCtrlPlusA();

			if (StateContainer.Instance[4] == null)
				StateContainer.Instance.AddState(4, new SetupScriptState());

			Unsubscribe();
			MakeBuilderRootless();
			if (rootControl != null)
			{
				RootlessAcceptButton();
				rootControl.Controls.Clear();
				rootControl.Controls.Add(new WizardSetupScript());
				rootControl = null;
				GC.Collect();
			}
		}

		private void btnCancel_Click(object sender, EventArgs e)
		{
			if (ConfirmCancellation())
				Application.Exit();
		}

		private void btnBrowse_Click(object sender, EventArgs e)
		{
			if (_setupType == DbSetupType.Upgrade || _setupType == DbSetupType.New)
			{
				string path;
				if (SelectFolder(out path))
				{
					txtDbLocation.Text = Path.ChangeExtension(Path.Combine(path, txtDbFileName.Text), "mdf");
					(StateContainer.Instance[3] as DbSetupState).DbFilePath = txtDbLocation.Text;
				}
				return;
			}

			using (var fd = new OpenFileDialog())
			{
				fd.InitialDirectory = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
				fd.Filter = "All files (*.*)|*.*|Setup files (*.ini)|*.ini";
				if (fd.ShowDialog() == DialogResult.OK)
				{
					txtDbLocation.Text = fd.FileName;
					(StateContainer.Instance[3] as DbSetupState).DbFilePath = fd.FileName;
				}
			}
		}

		private void btnBrowseLog_Click(object sender, EventArgs e)
		{
			if (_setupType == DbSetupType.Upgrade || _setupType == DbSetupType.New)
			{
				string path;
				if (SelectFolder(out path))
				{
					txtLogFileLocation.Text = Path.ChangeExtension(Path.Combine(path, txtDBLogName.Text), "ldf");
					(StateContainer.Instance[3] as DbSetupState).LogFilePath = txtLogFileLocation.Text;
				}
				return;
			}

			using (var fd = new OpenFileDialog())
			{
				fd.InitialDirectory = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
				fd.Filter = "All files (*.*)|*.*|Setup files (*.ini)|*.ini";
				if (fd.ShowDialog() == DialogResult.OK)
				{
					txtLogFileLocation.Text = fd.FileName;
					(StateContainer.Instance[3] as DbSetupState).LogFilePath = fd.FileName;
				}
			}
		}

		private bool SelectFolder(out string path)
		{
			path = string.Empty;
			using (FolderBrowserDialog fbd = new FolderBrowserDialog())
			{
				fbd.ShowNewFolderButton = true;
				fbd.Description = "Choose database folder location";
				if (fbd.ShowDialog() == DialogResult.OK)
				{
					path = fbd.SelectedPath;
					return true;
				}
				return false;
			}
		}

		private void MakeBuilderRootless()
		{
			if (_fullBuilder != null)
				_fullBuilder = null;//make rootless
		}

		private void RootlessAcceptButton()
		{
			if (rootControl.AcceptButton != null)
				rootControl.AcceptButton = null;//make rootless
		}

		#endregion button handlers

		private bool ConfirmCancellation()
		{
			if (MessageBox.Show(rootControl, StringsContainer.Instance.ExitConfirmation, StringsContainer.Instance.ExitConfirmationTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question)
				== System.Windows.Forms.DialogResult.Yes)
			{
				this.RevertCtrlPlusA();
				Program.ISExitRequired = true;
				return true;
			}
			Program.ISExitRequired = false;
			return false;
		}

		private void SaveState()
		{
			int size, growth;

			if (!string.IsNullOrEmpty(txtDbFileName.Text))
				(StateContainer.Instance[3] as DbSetupState).DbFileName = txtDbFileName.Text;

			if (!string.IsNullOrEmpty(txtDbLocation.Text))
				(StateContainer.Instance[3] as DbSetupState).DbFilePath = txtDbLocation.Text;

			if (!string.IsNullOrEmpty(txtInitialSize.Text))
			{
				if (int.TryParse(txtInitialSize.Text, out size))
					(StateContainer.Instance[3] as DbSetupState).DbGrowth = size;
			}

			if (!string.IsNullOrEmpty(txtGrowth.Text))
			{
				if (int.TryParse(txtGrowth.Text, out growth))
					(StateContainer.Instance[3] as DbSetupState).DbGrowth = growth;
			}

			if (!string.IsNullOrEmpty(txtLogFileLocation.Text))
				(StateContainer.Instance[3] as DbSetupState).LogFilePath = txtLogFileLocation.Text;

			if (!string.IsNullOrEmpty(txtDBLogName.Text))
				(StateContainer.Instance[3] as DbSetupState).LogFileName = txtDBLogName.Text;

			if (!string.IsNullOrEmpty(txtLogInitialSize.Text))
			{
				if (int.TryParse(txtLogInitialSize.Text, out size))
					(StateContainer.Instance[3] as DbSetupState).LogGrowth = size;
			}

			if (!string.IsNullOrEmpty(txtLogGrowth.Text))
			{
				if (int.TryParse(txtLogGrowth.Text, out growth))
					(StateContainer.Instance[3] as DbSetupState).DbGrowth = growth;
			}

			if (cmbLanguage.SelectedItem != null && cmbLanguage.SelectedItem is Language)
				(StateContainer.Instance[3] as DbSetupState).SelectedLanguage = (cmbLanguage.SelectedItem as Language);

			if (cmbSetupType.SelectedItem != null && cmbSetupType.SelectedItem is SectionBase)
			{
				(StateContainer.Instance[3] as DbSetupState).SelectedSetupType = (cmbSetupType.SelectedItem as SectionBase);
				ServiceLocator.Instance.GetService<IGlobalState>().SetState<SectionBase>("setupType", (cmbSetupType.SelectedItem as SectionBase));
			}

			if (cmbGrowthType.SelectedItem != null && (cmbGrowthType.SelectedItem as string).Equals("mb", StringComparison.OrdinalIgnoreCase))
				(StateContainer.Instance[3] as DbSetupState).DbGrowthType = GrowthType.MB;
			else
				(StateContainer.Instance[3] as DbSetupState).DbGrowthType = GrowthType.Percentage;

			if (cmbLogGrowthType.SelectedItem != null && (cmbLogGrowthType.SelectedItem as string).Equals("mb", StringComparison.OrdinalIgnoreCase))
				(StateContainer.Instance[3] as DbSetupState).LogGrowthType = GrowthType.MB;
			else
				(StateContainer.Instance[3] as DbSetupState).LogGrowthType = GrowthType.Percentage;
		}

		private void cmbSetupType_SelectedIndexChanged(object sender, EventArgs e)
		{
#if DEBUG
			if (cmbLanguage.SelectedItem != null && cmbLanguage.SelectedItem is Language)
				System.Diagnostics.Debug.WriteLine((cmbLanguage.SelectedItem as Language).LanguageName);
#endif
			if (cmbSetupType.SelectedItem != null && cmbSetupType.SelectedItem is SectionBase)
			{
#if DEBUG
				System.Diagnostics.Debug.WriteLine((cmbSetupType.SelectedItem as SectionBase).Text);
#endif
				if ((cmbSetupType.SelectedItem as SectionBase).Text.IndexOf("upgrade", StringComparison.OrdinalIgnoreCase) != -1
					|| (cmbSetupType.SelectedItem as SectionBase).Text.IndexOf("update", StringComparison.OrdinalIgnoreCase) != -1)
				{
					_setupType = DbSetupType.Upgrade;
					(States.StateContainer.Instance[3] as DbSetupState).DatabaseSetupType = DbSetupType.Upgrade;
					DisableFieldsOnUpgrade();
					return;
				}
				else if ((cmbSetupType.SelectedItem as SectionBase).Text.IndexOf("new ", StringComparison.OrdinalIgnoreCase) != -1)
				{
					_setupType = DbSetupType.New;
					(States.StateContainer.Instance[3] as DbSetupState).DatabaseSetupType = DbSetupType.New;
					EnableFieldsOnNew();
				}
				Application.DoEvents();
			}
		}

		private void DisableFieldsOnUpgrade()
		{
#if DEBUG
			System.Diagnostics.Debug.WriteLine("Inside DisableFieldsOnUpgrade method");
#endif
			foreach (Control c in groupBox1.Controls)
			{
#if DEBUG
				System.Diagnostics.Debug.WriteLine(c.Name);
#endif
				switch (c.GetType().Name)
				{
					case "ComboBox":
						if (c.Name.Equals("cmbLanguage", StringComparison.OrdinalIgnoreCase) || c.Name.Equals("cmbSetupType", StringComparison.OrdinalIgnoreCase))
							break;
						else
							c.Enabled = false;
						break;

					case "TextBox":
						c.Enabled = false;
						break;

					case "Button":
						if (c.Text.Equals("browse", StringComparison.OrdinalIgnoreCase))
							c.Enabled = false;
						break;

					default:
						break;
				}
			}
		}

		private void EnableFieldsOnNew()
		{
			foreach (Control c in this.groupBox1.Controls)
			{
				switch (c.GetType().Name)
				{
					case "ComboBox":
						if (c.Name.Equals("cmbLanguage", StringComparison.OrdinalIgnoreCase) || c.Name.Equals("cmbSetupType", StringComparison.OrdinalIgnoreCase))
							break;
						else
							c.Enabled = true;
						break;

					case "TextBox":
						c.Enabled = true;
						break;

					case "Button":
						if (c.Text.Equals("browse", StringComparison.OrdinalIgnoreCase))
							c.Enabled = true;
						break;

					default:
						break;
				}
			}
		}
	}
}