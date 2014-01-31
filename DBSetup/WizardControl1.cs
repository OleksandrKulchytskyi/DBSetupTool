using DBSetup.Common;
using DBSetup.Common.Helpers;
using DBSetup.States;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DBSetup
{
	public partial class WizardControl1 : UserControl
	{
		private WizardMain mainForm = null;

		public WizardControl1()
		{
			InitializeComponent();
			this.Load += WizardControl1_Load;
		}

		void WizardControl1_Load(object sender, EventArgs e)
		{
			if (StateContainer.Instance[1] == null)
			{
				var dbState = new StateDBSettings();
				dbState.UserName = txtUserName.Text.Trim();
				dbState.ServerName = string.Empty;
				dbState.Password = txtPassword.Text.Trim();

				StateContainer.Instance.AddState(1, dbState);
			}

			if (this.TopLevelControl != null && TopLevelControl is WizardMain)
			{
				mainForm = (TopLevelControl as WizardMain);
				mainForm.AcceptButton = btnNext;
			}

			if (!this.txtUserName.Focused)
				this.txtUserName.Focus();

			EnableButtonIfAllFildsCompleted();

			//check whether we have already loaded SQL instances
			if (StateContainer.Instance[1] != null && (StateContainer.Instance[1] as StateDBSettings).LocaSqlInstances.Count == 0)
			{
				var loadingTask = Task.Factory.StartNew<List<string>>(() =>
						 SqlServerHelper.GetSQLsServerInstances(true));

				loadingTask.RegisterFaultedHandler(OnLoadinFail);

				loadingTask.RegisterSucceededHandler(OnLoadingSucceeded, TaskScheduler.FromCurrentSynchronizationContext());
			}
			else if (StateContainer.Instance[1] != null && StateContainer.Instance.GetConcreteInstance<StateDBSettings>().LocaSqlInstances.Count > 0)
			{
				if (cmbSLQInstances.Items.Count > 0)
					cmbSLQInstances.Items.Clear();

				foreach (string Item in StateContainer.Instance.GetConcreteInstance<StateDBSettings>().LocaSqlInstances)
				{
					cmbSLQInstances.Items.Add(Item);
				}
				if (cmbSLQInstances.Items.Count > 0)
				{
					cmbSLQInstances.SelectedIndex = 0;
					cmbSLQInstances.SelectedItem = cmbSLQInstances.Items[0];
				}
			}

			if (StateContainer.Instance[1] != null)
			{
				if ((StateContainer.Instance[1] as States.StateDBSettings).LocaSqlInstances.Count > 0)
				{
					if (cmbSLQInstances.Items.Count > 0)
						cmbSLQInstances.Items.Clear();
					foreach (string item in (StateContainer.Instance[1] as States.StateDBSettings).LocaSqlInstances)
						cmbSLQInstances.Items.Add(item);
				}

				if (cmbSLQInstances.Items.Count > 0)
				{
					cmbSLQInstances.SelectedIndex = 0;
					cmbSLQInstances.SelectedText = (StateContainer.Instance[1] as States.StateDBSettings).ServerName;
					cmbSLQInstances.SelectedItem = cmbSLQInstances.Items[0];
				}
				txtUserName.Text = (StateContainer.Instance[1] as States.StateDBSettings).UserName;
				txtPassword.Text = (StateContainer.Instance[1] as States.StateDBSettings).Password;
			}
		}

		private void OnLoadingSucceeded(List<string> result)
		{
			if (result != null && result.Count > 0)
			{
				if (cmbSLQInstances.Items.Count > 0)
					cmbSLQInstances.Items.Clear();

				foreach (string Item in result)
				{
					cmbSLQInstances.Items.Add(Item);
					(StateContainer.Instance[1] as StateDBSettings).LocaSqlInstances.Add(Item);
				}
				cmbSLQInstances.SelectedIndex = 0;
			}
		}

		private void OnLoadinFail(Exception exc)
		{
			Log.Instance.Error("Fail to load SQL Server instances", exc);
		}

		private void btnPrevious_Click(object sender, EventArgs e)
		{
			mainForm.Controls.Clear();
			mainForm.Controls.Add(new MainControl());
			UnsubscribeFromEvents();
			mainForm = null;//make rootless
		}

		private void btnNext_Click(object sender, EventArgs e)
		{
		Begin:
			string dataSource = string.Empty;

			if (cmbSLQInstances.SelectedItem == null)
			{
				if (string.IsNullOrEmpty(cmbSLQInstances.Text)) return;

				dataSource = cmbSLQInstances.Text;
			}

			StateContainer.Instance.GetConcreteInstance<StateDBSettings>().UserName = txtUserName.Text.Trim();
			StateContainer.Instance.GetConcreteInstance<StateDBSettings>().ServerName = cmbSLQInstances.SelectedItem != null ?
																						(cmbSLQInstances.SelectedItem as string).Trim() : dataSource.Trim();
			StateContainer.Instance.GetConcreteInstance<StateDBSettings>().Password = txtPassword.Text.Trim();
			dataSource = string.IsNullOrEmpty(dataSource) ? (cmbSLQInstances.SelectedItem as string).Trim() : dataSource.Trim();

			var strBuilder = new System.Data.SqlClient.SqlConnectionStringBuilder();
			strBuilder.DataSource = dataSource;
			strBuilder.UserID = txtUserName.Text.Trim();
			strBuilder.Password = txtPassword.Text.Trim();

			bool canOpen = false;
			try
			{
				SqlServerHelper.CanOpenWithException(strBuilder.ToString());
				canOpen = true;
			}
			catch (System.Data.SqlClient.SqlException ex)
			{
				MessageBox.Show(mainForm, string.Format(@"Fail to connect to SQL Server: {0} {1}{1}{2}", dataSource, Environment.NewLine, ex.Message));
				(StateContainer.Instance[1] as StateDBSettings).IsConnectionSucceed = false;
				return;
			}
			if (canOpen)
				(StateContainer.Instance[1] as StateDBSettings).IsConnectionSucceed = true;

			var version = SqlServerHelper.GetSQLServerVersion(strBuilder.ToString());
			var versionStr = SqlServerHelper.GetSQLVersionByCommand(strBuilder.ToString());
			if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(versionStr))
			{
				Log.Instance.Warn("Fail to retreive SQL Server version");
				return;
			}

			var sqlServerReportState = new SqlServerReportState();

			sqlServerReportState.SQLVersion = versionStr.Substring(0, versionStr.IndexOf('\n') + 1);// SqlServerHelper.ParseSQLServerVersion(version).ToString();
			//getting DB version number from version table !!!
			sqlServerReportState.IsComm4Exists = SqlServerHelper.CheckDatabaseExists(strBuilder.ToString(), StringsContainer.Instance.DatabaseName);
			if (sqlServerReportState.IsComm4Exists)
			{
				int dbVer = SqlServerHelper.GetDbVersionFromVesrsionTable(strBuilder.ToString(), StringsContainer.Instance.DatabaseName);
				sqlServerReportState.Comm4Version = (dbVer == -1 ? "Undefined" : dbVer.ToString());
			}

			if (StateContainer.Instance[2] == null)
				StateContainer.Instance.AddState(2, sqlServerReportState);
			else
			{
				StateContainer.Instance.GetConcreteInstance<SqlServerReportState>().Comm4Version = sqlServerReportState.Comm4Version;
				StateContainer.Instance.GetConcreteInstance<SqlServerReportState>().SQLVersion = sqlServerReportState.SQLVersion;
				StateContainer.Instance.GetConcreteInstance<SqlServerReportState>().IsComm4Exists = sqlServerReportState.IsComm4Exists;
			}

			if (CheckForComm1())
				goto Begin;

			if (this.TopLevelControl is WizardMain)
			{
				var form = (this.TopLevelControl as WizardMain);
				form.Controls.Clear();
				form.Controls.Add(new WizardControl2());
				GC.Collect();
			}
			UnsubscribeFromEvents();
		}

		private bool CheckForComm1()
		{
			if (!(StateContainer.Instance[2] as SqlServerReportState).IsComm4Exists)
			{
				var strBuilder = new System.Data.SqlClient.SqlConnectionStringBuilder();
				strBuilder.DataSource = StateContainer.Instance.GetConcreteInstance<StateDBSettings>().ServerName;
				strBuilder.UserID = StateContainer.Instance.GetConcreteInstance<StateDBSettings>().UserName;
				strBuilder.Password = StateContainer.Instance.GetConcreteInstance<StateDBSettings>().Password;
				if (SqlServerHelper.CheckDatabaseExists(strBuilder.ToString(), "Comm1"))
				{
					if (MessageBox.Show(mainForm, "Would you like to rename the Comm1 database to the Comm4 ?", "Rename", MessageBoxButtons.YesNo, MessageBoxIcon.Information) ==
						 DialogResult.Yes)
					{
						string fpath = (StateContainer.Instance[0] as States.ConfigFileState).FilePath;
						string addendum = ConfigurationManager.AppSettings["renamingModule"];
						string concatenated = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(fpath), addendum);
						if (!System.IO.File.Exists(concatenated))
						{
							MessageBox.Show(mainForm, "File doesn't exist by path: " + Environment.NewLine + concatenated,
											"File not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
							return false;
						}

						try
						{
							ExecSqlScript(strBuilder, concatenated);
							MessageBox.Show(mainForm, "Database has been successfully renamed", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
							return true;
						}
						catch (Exception ex)
						{
							Log.Instance.Error(ex.Message, ex);
							MessageBox.Show(mainForm, ex.Message);
						}
					}
					else//otherwise, exit from app
						ForceExit();
				}
				strBuilder.Clear();
			}
			return false;
		}

		private void ExecSqlScript(SqlConnectionStringBuilder strBuilder, string filePath)
		{
			using (SqlConnection con = new SqlConnection(strBuilder.ToString()))
			{
				using (SqlCommand cmd = con.CreateCommand())
				{
					cmd.CommandType = System.Data.CommandType.Text;
					cmd.CommandTimeout = 90;
					var regex = new Regex(Environment.NewLine + "go", RegexOptions.IgnoreCase);
					List<Common.Statements.SqlDataStatement> sqlStatements = new List<Common.Statements.SqlDataStatement>(){ 
					new Common.Statements.SqlDataStatement(System.IO.File.ReadAllText(filePath), filePath)};
					foreach (var statement in sqlStatements)
					{
						//remove go statement from script because it is not tsql statement.
						string content = regex.Replace(statement.SqlStatements, string.Empty);
						cmd.CommandText = content;
						try
						{
							if (con.State == System.Data.ConnectionState.Closed) con.Open();
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
		}

		private void ForceExit()
		{
			//this.RevertCtrlPlusA();
			UnsubscribeFromEvents();
			Program.ISExitRequired = true;
			Application.Exit();
		}

		private void UnsubscribeFromEvents()
		{
			this.Load -= WizardControl1_Load;
		}

		private void btnCancel_Click(object sender, EventArgs e)
		{
			ConfirmCancellation();
		}

		private void ConfirmCancellation()
		{
			if (MessageBox.Show(mainForm, StringsContainer.Instance.ExitConfirmation, StringsContainer.Instance.ExitConfirmationTitle,
				MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
			{
				UnsubscribeFromEvents();
				mainForm = null;
				Program.ISExitRequired = true;
				Application.Exit();
			}
		}

		private void cmbSLQInstances_SelectedIndexChanged(object sender, EventArgs e)
		{
			EnableButtonIfAllFildsCompleted();
		}

		private void txtServerName_TextChanged(object sender, EventArgs e)
		{
			EnableButtonIfAllFildsCompleted();
		}

		private void txtUserName_TextChanged(object sender, EventArgs e)
		{
			EnableButtonIfAllFildsCompleted();
		}

		private void txtPassword_TextChanged(object sender, EventArgs e)
		{
			EnableButtonIfAllFildsCompleted();
		}

		private void EnableButtonIfAllFildsCompleted()
		{
			if (string.IsNullOrEmpty(txtPassword.Text) || string.IsNullOrEmpty(txtUserName.Text) ||
				(string.IsNullOrEmpty(cmbSLQInstances.SelectedItem as string) && string.IsNullOrEmpty(cmbSLQInstances.Text)))
				btnNext.Enabled = false;
			else
				btnNext.Enabled = true;
		}
	}
}
