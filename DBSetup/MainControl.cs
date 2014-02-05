using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DBSetup.States;
using System.IO;
using DBSetup.Common;
using DBSetup.Helpers;

namespace DBSetup
{
	public partial class MainControl : UserControl
	{
		private WizardMain mainForm;
		private IServiceLocator ioc;

		public MainControl()
		{
			InitializeComponent();
			this.Load += MainControl_Load;
		}

		void MainControl_Load(object sender, EventArgs e)
		{
			if (this.TopLevelControl != null && TopLevelControl is WizardMain)
			{
				mainForm = (TopLevelControl as WizardMain);
				mainForm.AcceptButton = btnNext;
			}

			ioc = ServiceLocator.Instance;

			string hostPath = Path.GetDirectoryName(Application.ExecutablePath);
			if (!string.IsNullOrEmpty(hostPath))
			{
				var files = Directory.EnumerateFiles(hostPath, "*.ini", SearchOption.TopDirectoryOnly);
				txtFilePath.Text = files.FirstOrDefault() ?? string.Empty;
				if (txtFilePath.Text.Length == 0)
					btnNext.Enabled = false;
			}

			if (StateContainer.Instance[0] != null && (StateContainer.Instance[0] is ConfigFileState))
				txtFilePath.Text = (StateContainer.Instance[0] as ConfigFileState).FilePath;

			else
			{
				StateContainer.Instance.AddState(0, new ConfigFileState());
				if (!string.IsNullOrEmpty(txtFilePath.Text))
					(StateContainer.Instance[0] as ConfigFileState).FilePath = txtFilePath.Text;
			}

			//Add some hint, as far as txt control in active control it selects selected text (path to ini file) automatically
			//to workaround it some tips added below
			if (!string.IsNullOrEmpty(txtFilePath.Text) && !txtFilePath.Focused)
			{
				this.ActiveControl = txtFilePath;
				txtFilePath.Focus();
				txtFilePath.SelectAll();
				txtFilePath.Select(0, 0);
			}
		}

		private void btnNext_Click(object sender, EventArgs e)
		{
			if (!System.IO.File.Exists(txtFilePath.Text))
			{
				MessageBox.Show(mainForm, StringsContainer.Instance.FileIsNotExists);
				return;
			}

			(StateContainer.Instance[0] as ConfigFileState).FilePath = txtFilePath.Text;

			if (mainForm != null)
			{
				mainForm.MakeMainCtrlRootless();
				this.Load -= MainControl_Load;
				this.btnBrowse.Click -= new System.EventHandler(this.btnBrowse_Click);

				mainForm.Controls.Clear();
				mainForm.Controls.Add(new WizardControl1());
				mainForm = null;
			}
		}

		private void btnBrowse_Click(object sender, EventArgs e)
		{
			FileDialog openFD = new OpenFileDialog();
			openFD.InitialDirectory = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
			openFD.CheckFileExists = true;
			openFD.Filter = "INI files(*.ini)|*.ini";

			if (openFD.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				txtFilePath.Text = openFD.FileName;
				ioc.GetService<IGlobalState>().SetState<string>("rootPath", System.IO.Path.GetDirectoryName(txtFilePath.Text));
			}
		}

		private void btnCancel_Click(object sender, EventArgs e)
		{
			ConfirmCancellation();
		}

		private void ConfirmCancellation()
		{
			if (MessageBox.Show(mainForm, StringsContainer.Instance.ExitConfirmation, StringsContainer.Instance.ExitConfirmationTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question)
				== System.Windows.Forms.DialogResult.Yes)
			{
				Program.ISExitRequired = true;
				Application.Exit();
			}
		}

		private void txtFilePath_TextChanged(object sender, EventArgs e)
		{
			string text = (sender as TextBoxBase).Text;
			if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(text))
			{
				btnNext.Enabled = false;
				ioc.GetService<IGlobalState>().SetState<string>("rootPath", System.IO.Path.GetDirectoryName(text));
			}
			else
				btnNext.Enabled = true;
		}
	}
}
