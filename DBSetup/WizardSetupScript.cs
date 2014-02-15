using DBSetup.Common;
using DBSetup.Common.Helpers;
using DBSetup.Common.ModelBuilder;
using DBSetup.Helpers;
using DBSetup.States;
using System;
using System.Windows.Forms;

namespace DBSetup
{
	public partial class WizardSetupScript : UserControl
	{
		private WizardMain rootForm;
		private SetupScriptDocument _setupDocument;
		private ScriptConsequencyBuilder builder;

		public WizardSetupScript()
		{
			InitializeComponent();
			this.Load += WizardSetupScript_Load;
			txtSetupScript.Text = string.Empty;
		}

		private void WizardSetupScript_Load(object sender, EventArgs e)
		{
			rootForm = TopLevelControl as WizardMain;

			this.DisableCtrlPlusA();

			if (StateContainer.Instance[3] != null)
			{
				DisableNavigationButtons(true);
				builder = new ScriptConsequencyBuilder();
				_setupDocument = builder.GetDocumentResult();

				var task = builder.AsyncBuild((StateContainer.Instance[3] as DbSetupState).SelectedSetupType);
				task.RegisterSucceededHandler(OnSuccess, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
				task.RegisterFaultedHandler(OnFailed, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
			}
		}

		private void OnSuccess(SetupScriptDocument document)
		{
			if (builder != null)
				Log.Instance.Info(string.Format("Setup document has been successfully generated.{0}{1}",
								Environment.NewLine, builder.GetDocumentResult().GetDocumentText()));

			DisableNavigationButtons(false);

			if (builder.GetDocumentResult() != null)
			{
				if (StateContainer.Instance[4] == null)
					StateContainer.Instance.AddState(4, new States.SetupScriptState());
				txtSetupScript.Text = string.Empty;
				StateContainer.Instance.GetState<SetupScriptState>().DocumentText = builder.GetDocumentResult();

				using (var sr = new System.IO.StringReader(StateContainer.Instance.GetState<SetupScriptState>().DocumentText.GetDocumentText()))
				{
					string line = null;
					while ((line = sr.ReadLine()) != null)
					{
						System.Threading.Thread.Sleep(10);
						WinFormUtils.DoPaintEvents();
						txtSetupScript.AppendText((line + Environment.NewLine));
						line = null;
					}
				}
				WinFormUtils.DoPaintEvents();
			}
		}

		private void OnFailed(Exception ex)
		{
			this.ExecAction(() => DisableNavigationButtons(false));
			Exception castedExc = null;
			if (ex is AggregateException)
				castedExc = (ex as AggregateException).Flatten().InnerException;
			else
				castedExc = ex;
			Log.Instance.Error("Continuation task", castedExc);
			MessageBox.Show(rootForm, castedExc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}


		#region button handlers

		private void btnPrevious_Click(object sender, EventArgs e)
		{
			this.RevertCtrlPlusA();
			if (rootForm != null)
			{
				ClearDocText();
				txtSetupScript.Clear();
				rootForm.Controls.Clear();
				rootForm.Controls.Add(new WizardControl3());
				UnsubscribeFromEvents();
				rootForm = null;
			}

			ClearDocText();

			if (_setupDocument != null)
			{
				_setupDocument.Clear();
				_setupDocument = null;
			}
		}

		private void UnsubscribeFromEvents()
		{
			this.Load -= WizardSetupScript_Load;
		}

		private void btnNext_Click(object sender, EventArgs e)
		{
			this.RevertCtrlPlusA();

			if (this._setupDocument != null)
				this._setupDocument = null; //make reachable for GC

			if (rootForm != null)
			{
				if (StateContainer.Instance[5] == null)
					StateContainer.Instance.AddState(5, new RunScriptState());

				StateContainer.Instance.GetState<RunScriptState>().ComposeParts();

				ClearDocText();
				txtSetupScript.Clear();

				rootForm.Controls.Clear();
				rootForm.Controls.Add(new WizardRunScriptControl());
				UnsubscribeFromEvents();
				rootForm = null;

				GC.Collect();
			}
		}

		private void btnCancel_Click(object sender, EventArgs e)
		{
			if (ConfirmCancellation())
			{
				txtSetupScript.Clear();
				ClearDocText();
				UnsubscribeFromEvents();
				rootForm = null;

				Application.Exit();
			}
		}

		private bool ConfirmCancellation()
		{
			if (MessageBox.Show(StringsContainer.Instance.ExitConfirmation, StringsContainer.Instance.ExitConfirmationTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question)
				== System.Windows.Forms.DialogResult.Yes)
			{
				this.RevertCtrlPlusA();
				Program.IsExitRequired = true;
				return true;
			}
			Program.IsExitRequired = false;
			return false;
		}

		private void ClearDocText()
		{
			if (StateContainer.Instance.GetState<States.SetupScriptState>().DocumentText != null)
			{
				StateContainer.Instance.GetState<States.SetupScriptState>().DocumentText.Clear();
				StateContainer.Instance.GetState<States.SetupScriptState>().DocumentText = null;
			}
		}

		private void DisableNavigationButtons(bool isDisabled)
		{
			this.ExecAction(() =>
				{
					btnCancel.Enabled = !isDisabled;
					btnNext.Enabled = !isDisabled;
					btnPrevious.Enabled = !isDisabled;
				});
		}
		#endregion button handlers
	}
}