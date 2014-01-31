namespace DBSetup
{
	partial class WizardMain
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.mainControl1 = new DBSetup.MainControl();
			this.SuspendLayout();
			// 
			// mainControl1
			// 
			this.mainControl1.Location = new System.Drawing.Point(3, 3);
			this.mainControl1.Name = "mainControl1";
			this.mainControl1.Size = new System.Drawing.Size(673, 370);
			this.mainControl1.TabIndex = 0;
			// 
			// WizardMain
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(678, 386);
			this.Controls.Add(this.mainControl1);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.MaximizeBox = false;
			this.Name = "WizardMain";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "PowerScribe 360 Database Setup Wizard";
			this.Load += new System.EventHandler(this.WizardMain_Load);
			this.ResumeLayout(false);

		}

		#endregion

		private MainControl mainControl1;

	}
}