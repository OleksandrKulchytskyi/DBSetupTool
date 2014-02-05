using DBSetup.Common;
using DBSetup.Common.Helpers;
using DBSetup.Common.ModelBuilder;
using DBSetup.Common.Statements;
using DBSetup.Helpers;
using DBSetup.States;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DBSetup
{
	public partial class FinalizationControl : UserControl
	{
		private const string _exitBtnText = "Exit";
		private const string _healthGBText = "healthGBText";
		private const string _userGBText = "userGBText";
		private const string _ps360UserPwd = "pPS360UserPwd";
		private readonly string _FinalSection = ConfigurationManager.AppSettings["finalSection"];

		private WizardMain rootControl;
		private SqlConnection _sqlConn;
		private StateDBSettings _dbSettings;
		private volatile uint _initialized = 0;

		private volatile uint _isSiteSuccess = 0;
		private volatile uint _isUserSuccess = 0;

		public FinalizationControl()
		{
			InitializeComponent();
			this.Load += FinalizationControl_Load;
		}

		private void FinalizationControl_Load(object sender, EventArgs e)
		{
			if (this.TopLevelControl != null && TopLevelControl is WizardMain)
			{
				rootControl = (TopLevelControl as WizardMain);
				rootControl.AcceptButton = btnFinish;
			}

			this.DisableCtrlPlusA();
			txtPassword.PasswordChar = '*';
			btnPrevious.Enabled = false;

			this.groupBox2.Text = ConfigurationManager.AppSettings[_healthGBText];
			this.groupBox3.Text = ConfigurationManager.AppSettings[_userGBText];
			this.txtPassword.Text = ConfigurationManager.AppSettings[_ps360UserPwd];

			if (!txtHealthSystem.Focused)
				txtHealthSystem.Focus();
		}

		private void checkBox1_CheckedChanged(object sender, EventArgs e)
		{
			checkBox1.Text = checkBox1.Checked ? "Hide" : "Show";
			txtPassword.UseSystemPasswordChar = !checkBox1.Checked;
			txtPassword.PasswordChar = checkBox1.Checked ? '\0' : '*';
		}

		private void btnFinish_Click(object sender, EventArgs e)
		{
			if ((sender as Button).Text.Equals(_exitBtnText, StringComparison.OrdinalIgnoreCase))
			{
				UnsubscribeFromLoadEvent();
				Program.ISExitRequired = true;
				Application.Exit();
				return;
			}

			if (string.IsNullOrEmpty(txtHealthSystem.Text) ||
				string.IsNullOrEmpty(txtSiteName.Text) ||
				string.IsNullOrEmpty(txtPassword.Text))
			{
				MessageBox.Show(rootControl, StringsContainer.Instance.FieldsAreEmpty, "Value is missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			InitializeModules();

			EnableButtons(false);
			EnableFields(false);
			var mainTask = Task.Factory.StartNew(PerformWorkflow);
			mainTask.RegisterSucceededHandler(OnSuccess, TaskScheduler.FromCurrentSynchronizationContext());
			mainTask.RegisterFaultedHandler(OnError, TaskScheduler.FromCurrentSynchronizationContext());
		}

		private void OnSuccess()
		{
			if (_isSiteSuccess == 1 && _isUserSuccess == 1)
				this.ExecAction(() => btnFinish.Text = _exitBtnText);

			EnableButtons(true); //EnableFields(true);
			btnCancel.Enabled = false; btnPrevious.Enabled = false;
			MessageBox.Show(rootControl, StringsContainer.Instance.FinalMessage, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

			DestroySqlConnection();
		}

		private void OnError(Exception ex)
		{
			if (ex != null)
			{
				var castedExc = (ex is AggregateException) == true ? (ex as AggregateException).Flatten().InnerException : ex;
				Log.Instance.Error("PerformWorkflow", castedExc);

				EnableFields(_isSiteSuccess == 0, _isUserSuccess == 1);
				EnableButtons(true);
				btnCancel.Enabled = false; btnPrevious.Enabled = false;
				MessageBox.Show(rootControl, castedExc.Message, "Error occurred", MessageBoxButtons.OK, MessageBoxIcon.Error);

				DestroySqlConnection();
			}
		}

		private void btnPrevious_Click(object sender, EventArgs e)
		{
			DestroySqlConnection();
			UnsubscribeFromLoadEvent();
			if (rootControl != null)
			{
				rootControl = null;
			}
		}

		private void btnCancel_Click(object sender, EventArgs e)
		{
			if (ConfirmCancellation())
				Application.Exit();
		}

		private bool ConfirmCancellation()
		{
			if (MessageBox.Show(rootControl, StringsContainer.Instance.ExitConfirmation, StringsContainer.Instance.ExitConfirmationTitle,
								MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
			{
				UnsubscribeFromLoadEvent();
				this.RevertCtrlPlusA();
				Program.ISExitRequired = true;
				return true;
			}
			Program.ISExitRequired = false;
			return false;
		}

		private void UnsubscribeFromLoadEvent()
		{
			this.Load -= FinalizationControl_Load;
		}

		private void InitializeModules()
		{
			if (_initialized == 1) return;

			_initialized = 1;
			var finalState = StateContainer.Instance.GetConcreteInstance<FinalState>();
			if (finalState != null)
			{
				finalState.HealthSystemName = txtHealthSystem.Text;
				finalState.SiteName = txtSiteName.Text;
				finalState.Password = txtPassword.Text;
			}

			_sqlConn = new SqlConnection();
			_dbSettings = StateContainer.Instance.GetConcreteInstance<StateDBSettings>();
			SqlConnectionStringBuilder sqlBuilder = new SqlConnectionStringBuilder();
			sqlBuilder.DataSource = _dbSettings.ServerName;
			sqlBuilder.UserID = _dbSettings.UserName;
			sqlBuilder.Password = _dbSettings.Password;

			sqlBuilder.InitialCatalog = Path.GetFileNameWithoutExtension(StateContainer.Instance.GetConcreteInstance<DbSetupState>().DbFileName);
			sqlBuilder.MaxPoolSize = 20;
			sqlBuilder.Pooling = true;

			_sqlConn.ConnectionString = sqlBuilder.ConnectionString;
			sqlBuilder = null;
			if (_sqlConn.State == System.Data.ConnectionState.Closed)
				_sqlConn.Open();
		}

		private void PerformWorkflow()
		{
			SqlCommand command = null;
			if (_isSiteSuccess == 0)
			{
				using (command = _sqlConn.CreateCommand())
				{
					command.CommandType = System.Data.CommandType.StoredProcedure;
					command.CommandText = ConfigurationManager.AppSettings["spHealthSystem"];
					var @p1 = new SqlParameter("@healthsystem", System.Data.SqlDbType.VarChar);
					@p1.Value = txtHealthSystem.Text;
					command.Parameters.Add(@p1);
					var @p2 = new SqlParameter("@sitename", System.Data.SqlDbType.VarChar);
					@p2.Value = txtSiteName.Text;
					command.Parameters.Add(@p2);

					var @p3 = new SqlParameter("@workflow", System.Data.SqlDbType.VarChar);
					@p3.Value = ConfigurationManager.AppSettings["pWorkflow"];
					command.Parameters.Add(@p3);

					int i = command.ExecuteNonQuery();
					_isSiteSuccess = 1;
					this.ExecAction(() =>
						{
							txtHealthSystem.Enabled = false;
							txtSiteName.Enabled = false;
						});
				}
			}

			if (_isUserSuccess == 0)
			{
				using (command = _sqlConn.CreateCommand())
				{
					command.CommandType = System.Data.CommandType.StoredProcedure;
					command.CommandText = ConfigurationManager.AppSettings["spUser"];
					var @p1 = new SqlParameter("@psuser", System.Data.SqlDbType.VarChar);
					@p1.Value = ConfigurationManager.AppSettings["pPS360User"];
					command.Parameters.Add(@p1);
					var @p2 = new SqlParameter("@pspswd", System.Data.SqlDbType.VarChar);
					@p2.Value = txtPassword.Text;
					command.Parameters.Add(@p2);

					int i = command.ExecuteNonQuery();
					_isUserSuccess = 1;
					this.ExecAction(() => txtPassword.Enabled = false);
				}
			}
			DoFinalization();
		}

		private void DoFinalization()
		{
			Log.Instance.Info("Performing final step from INI file");
			var sectionData = IniFileParser.GetSingleSection((StateContainer.Instance[0] as ConfigFileState).FilePath, _FinalSection);

			ObjectModelBuilder builder = new ObjectModelBuilder();
			builder.LoadSql = true; builder.LoadBLOB = true;
			var finalization = new DBSetup.Common.Models.SectionBase();
			finalization.Text = _FinalSection;
			finalization.FileName = (StateContainer.Instance[0] as ConfigFileState).FilePath;
			var result = builder.BuildByText(finalization, sectionData);
			sectionData = null;//rootless for GC help
			if (finalization.Children == null)
				finalization.Children = new List<DBSetup.Common.Models.SectionBase>();
			finalization.Children.AddRange(result);

			NormalizePath(finalization);

			ScriptConsequencyBuilder scriptBuilder = new ScriptConsequencyBuilder();
			scriptBuilder.Build(finalization);

			Log.Instance.Info("Scripts to be executed:{0}{1}".FormatWith(Environment.NewLine, scriptBuilder.GetDocumentResult().GetDocumentText()));
			scriptBuilder = null;

			IDataStatementFactory factory = null;
			var runScriptState = StateContainer.Instance.GetConcreteInstance<RunScriptState>();
			if (runScriptState != null && runScriptState.StatementFactory != null && runScriptState.StatementFactory.Value != null)
				factory = runScriptState.StatementFactory.Value;
			else
				factory = new DataStatementsFactory();

			var sqlStatements = factory.GenerateFor(finalization);
			factory = null;

			InvokeRemainsSQLs(sqlStatements);
			sqlStatements = null;
			finalization = null;
		}

		private void InvokeRemainsSQLs(List<IDataStatement> sqlStatements)
		{
			using (SqlCommand cmd = _sqlConn.CreateCommand())
			{
				cmd.CommandType = System.Data.CommandType.Text;
				cmd.CommandTimeout = 0;//unlimited command wait time
				var regex = new Regex(Environment.NewLine + "go", RegexOptions.IgnoreCase);
				foreach (var statement in sqlStatements.Where(x => x.Type == StatementType.Sql))
				{
					//remove go statement from script because it is not tsql statement.
					string content = regex.Replace((statement as SqlDataStatement).SqlStatements, string.Empty);
					cmd.CommandText = content;
					try
					{
						if (_sqlConn.State == System.Data.ConnectionState.Closed)
							_sqlConn.Open();
						cmd.ExecuteNonQuery();
					}
					catch (SqlException ex)
					{
						Log.Instance.Error("Failt to execute {0}".FormatWith(statement.DataFile), ex);
						throw;
					}
				}
			}
		}

		private void NormalizePath(Common.Models.SectionBase finalization)
		{
			if (finalization == null || finalization.Children == null)
				return;

			foreach (var item in finalization.Children)
			{
				if (item is DBSetup.Common.Models.SqlLink)
				{
					if ((item as DBSetup.Common.Models.SqlLink).SqlFilePath.StartsWithIgnoreSpaces(@".\"))
						(item as DBSetup.Common.Models.SqlLink).SqlFilePath = Path.Combine(Path.GetDirectoryName(finalization.FileName),
																						(item as DBSetup.Common.Models.SqlLink).SqlFilePath);
				}
			}
		}

		private void DestroySqlConnection()
		{
			try
			{
				if (_sqlConn != null && (_sqlConn.State == System.Data.ConnectionState.Open || _sqlConn.State == System.Data.ConnectionState.Connecting))
				{
					_sqlConn.Close();
					_sqlConn.Dispose();
					_initialized = 0;
				}
			}
			catch (SqlException ex)
			{
				Log.Instance.Error("DestroySqlConnection", ex);
			}
		}

		private void EnableButtons(bool enable)
		{
			btnCancel.Enabled = enable;
			btnPrevious.Enabled = enable;
			btnFinish.Enabled = enable;
		}

		private void EnableFields(bool enable)
		{
			EnableFields(enable, !enable);
		}

		private void EnableFields(bool enable, bool passSucceed)
		{
			txtHealthSystem.Enabled = enable;
			txtSiteName.Enabled = enable;
			txtPassword.Enabled = !passSucceed;
		}
	}
}