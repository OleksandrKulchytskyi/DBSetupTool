using DBSetup.Common;
using DBSetup.Helpers;
using DBSetup.States;
using System;
using System.Windows.Forms;

namespace DBSetup
{
	public partial class WizardControl2 : UserControl
	{
		private WizardMain mainForm;

		public WizardControl2()
		{
			InitializeComponent();
			this.Load += WizardControl2_Load;
		}

		private void WizardControl2_Load(object sender, EventArgs e)
		{
			if (TopLevelControl != null && TopLevelControl is WizardMain)
			{
				mainForm = (TopLevelControl as WizardMain);
				mainForm.AcceptButton = btnNext;
			}

			this.DisableCtrlPlusA();

			txtComm4exists.ReadOnly = true;
			txtComm4Version.ReadOnly = true;
			txtSQLVersion.ReadOnly = true;

			if (StateContainer.Instance[2] != null)
			{
				txtSQLVersion.Text = (StateContainer.Instance[2] as SqlServerReportState).SQLVersion;
				Log.Instance.Info(string.Format("SQL Server version: {0}", (StateContainer.Instance[2] as SqlServerReportState).SQLVersion));

				this.txtComm4exists.Text = (StateContainer.Instance[2] as SqlServerReportState).IsComm4Exists == true ? "Yes" : "No";
				Log.Instance.Info(string.Format("Comm4 exists: {0}", this.txtComm4exists.Text));

				txtComm4Version.Text = string.IsNullOrEmpty((StateContainer.Instance[2] as SqlServerReportState).Comm4Version) == true ? string.Empty :
										(StateContainer.Instance[2] as SqlServerReportState).Comm4Version;
				Log.Instance.Info(string.Format("Comm4 version is: {0}", txtComm4Version.Text));
			}

			if (!btnNext.Focused) btnNext.Focus();
		}

		private void btnPrevious_Click(object sender, EventArgs e)
		{
			if (this.mainForm != null)
			{
				this.RevertCtrlPlusA();
				UnsubscribeFromEvents();
				mainForm.Controls.Clear();
				mainForm.Controls.Add(new WizardControl1());

				mainForm = null;
			}
		}

		private void btnNext_Click(object sender, EventArgs e)
		{
			if (StateContainer.Instance[3] == null)
				StateContainer.Instance.AddState(3, new DbSetupState());

			this.RevertCtrlPlusA();
			UnsubscribeFromEvents();

			if (this.mainForm != null)
			{
				mainForm.Controls.Clear();
				mainForm.Controls.Add(new WizardControl3());
				mainForm = null;
				GC.Collect();
			}
		}

		private void UnsubscribeFromEvents()
		{
			this.Load -= WizardControl2_Load;
		}

		private void btnCancel_Click(object sender, EventArgs e)
		{
			if (ConfirmCancellation())
				Application.Exit();
		}

		private bool ConfirmCancellation()
		{
			if (MessageBox.Show(mainForm, StringsContainer.Instance.ExitConfirmation, StringsContainer.Instance.ExitConfirmationTitle,
				MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
			{
				this.RevertCtrlPlusA();
				Program.IsExitRequired = true;
				return true;
			}
			Program.IsExitRequired = false;
			return false;
		}
	}
}