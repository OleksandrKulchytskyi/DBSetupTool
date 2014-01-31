namespace DBSetup
{
	partial class WizardControl2
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

		#region Component Designer generated code

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.btnCancel = new System.Windows.Forms.Button();
			this.btnNext = new System.Windows.Forms.Button();
			this.btnPrevious = new System.Windows.Forms.Button();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.txtComm4Version = new System.Windows.Forms.TextBox();
			this.txtComm4exists = new System.Windows.Forms.TextBox();
			this.txtSQLVersion = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.groupBox1.SuspendLayout();
			this.SuspendLayout();
			// 
			// btnCancel
			// 
			this.btnCancel.Location = new System.Drawing.Point(576, 342);
			this.btnCancel.Name = "btnCancel";
			this.btnCancel.Size = new System.Drawing.Size(75, 23);
			this.btnCancel.TabIndex = 19;
			this.btnCancel.Text = "Cancel";
			this.btnCancel.UseVisualStyleBackColor = true;
			this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
			// 
			// btnNext
			// 
			this.btnNext.Location = new System.Drawing.Point(495, 342);
			this.btnNext.Name = "btnNext";
			this.btnNext.Size = new System.Drawing.Size(75, 23);
			this.btnNext.TabIndex = 18;
			this.btnNext.Text = "Next";
			this.btnNext.UseVisualStyleBackColor = true;
			this.btnNext.Click += new System.EventHandler(this.btnNext_Click);
			// 
			// btnPrevious
			// 
			this.btnPrevious.Location = new System.Drawing.Point(414, 342);
			this.btnPrevious.Name = "btnPrevious";
			this.btnPrevious.Size = new System.Drawing.Size(75, 23);
			this.btnPrevious.TabIndex = 17;
			this.btnPrevious.Text = "Previous";
			this.btnPrevious.UseVisualStyleBackColor = true;
			this.btnPrevious.Click += new System.EventHandler(this.btnPrevious_Click);
			// 
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this.txtComm4Version);
			this.groupBox1.Controls.Add(this.txtComm4exists);
			this.groupBox1.Controls.Add(this.txtSQLVersion);
			this.groupBox1.Controls.Add(this.label4);
			this.groupBox1.Controls.Add(this.label3);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Location = new System.Drawing.Point(3, 3);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(668, 331);
			this.groupBox1.TabIndex = 20;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "SQL Server Report";
			// 
			// txtComm4Version
			// 
			this.txtComm4Version.Location = new System.Drawing.Point(155, 131);
			this.txtComm4Version.Name = "txtComm4Version";
			this.txtComm4Version.Size = new System.Drawing.Size(291, 20);
			this.txtComm4Version.TabIndex = 22;
			// 
			// txtComm4exists
			// 
			this.txtComm4exists.Location = new System.Drawing.Point(155, 85);
			this.txtComm4exists.Name = "txtComm4exists";
			this.txtComm4exists.Size = new System.Drawing.Size(291, 20);
			this.txtComm4exists.TabIndex = 21;
			// 
			// txtSQLVersion
			// 
			this.txtSQLVersion.Location = new System.Drawing.Point(155, 39);
			this.txtSQLVersion.Name = "txtSQLVersion";
			this.txtSQLVersion.Size = new System.Drawing.Size(291, 20);
			this.txtSQLVersion.TabIndex = 20;
			// 
			// label4
			// 
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(16, 131);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(129, 13);
			this.label4.TabIndex = 19;
			this.label4.Text = "Comm4 database version:";
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(16, 85);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(121, 13);
			this.label3.TabIndex = 18;
			this.label3.Text = "Comm4 database exists:";
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(16, 39);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(103, 13);
			this.label2.TabIndex = 17;
			this.label2.Text = "SQL Server Version:";
			// 
			// WizardControl2
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this.groupBox1);
			this.Controls.Add(this.btnCancel);
			this.Controls.Add(this.btnNext);
			this.Controls.Add(this.btnPrevious);
			this.Name = "WizardControl2";
			this.Size = new System.Drawing.Size(674, 378);
			//this.Load += new System.EventHandler(this.WizardControl2_Load);
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Button btnCancel;
		private System.Windows.Forms.Button btnNext;
		private System.Windows.Forms.Button btnPrevious;
		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.TextBox txtComm4Version;
		private System.Windows.Forms.TextBox txtComm4exists;
		private System.Windows.Forms.TextBox txtSQLVersion;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Label label2;
	}
}
