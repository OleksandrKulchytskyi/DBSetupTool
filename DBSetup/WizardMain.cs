using System;
using System.Windows.Forms;

namespace DBSetup
{
	public partial class WizardMain : Form
	{
		public WizardMain()
		{
			InitializeComponent();
			this.FormClosing += WizardMain_FormClosing;
		}

		private void WizardMain_Load(object sender, EventArgs e)
		{
		}

		private void WizardMain_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (Program.ISExitRequired.HasValue && Program.ISExitRequired.Value)
			{
				e.Cancel = false;
				if (this.Controls.Count == 1 && this.Controls[0] is WizardRunScriptControl)
				{
					WizardRunScriptControl runScriptControl = (this.Controls[0] as WizardRunScriptControl);
					bool? val = runScriptControl.IsThreadRunning();
					if (val.HasValue && val.Value)
					{
						runScriptControl.TerminateThreadExec();
					}
					runScriptControl = null;
				}
				return;
			}

			else if (!ConfirmCancellation())
				e.Cancel = true;
		}

		internal void MakeMainCtrlRootless()
		{
			if (mainControl1 != null)
				mainControl1 = null;
		}

		private bool ConfirmCancellation()
		{
			bool result = false;
			if (MessageBox.Show(this, StringsContainer.Instance.ExitConfirmation, StringsContainer.Instance.ExitConfirmationTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question)
				== System.Windows.Forms.DialogResult.Yes)
			{
				result = true;
			}

			return result;
		}

	}
}
