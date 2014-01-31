namespace DBSetup
{
	partial class WizardRunScriptControl
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

			if (disposing)
			{
				if (_signalEvent != null)
					_signalEvent.Dispose();

				if (_cts != null)
					_cts.Dispose();
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
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.txtExecutionLog = new System.Windows.Forms.TextBox();
			this.txtScriptToRun = new System.Windows.Forms.TextBox();
			this.txtCurrentStep = new System.Windows.Forms.TextBox();
			this.btnRunOverSql = new System.Windows.Forms.Button();
			this.btnStepOverSource = new System.Windows.Forms.Button();
			this.btnRun = new System.Windows.Forms.Button();
			this.btnCancel = new System.Windows.Forms.Button();
			this.btnNext = new System.Windows.Forms.Button();
			this.btnPrevious = new System.Windows.Forms.Button();
			this.groupBox1.SuspendLayout();
			this.SuspendLayout();
			// 
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this.txtExecutionLog);
			this.groupBox1.Controls.Add(this.txtScriptToRun);
			this.groupBox1.Controls.Add(this.txtCurrentStep);
			this.groupBox1.Controls.Add(this.btnRunOverSql);
			this.groupBox1.Controls.Add(this.btnStepOverSource);
			this.groupBox1.Controls.Add(this.btnRun);
			this.groupBox1.Location = new System.Drawing.Point(4, 4);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(660, 348);
			this.groupBox1.TabIndex = 0;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Script Run";
			// 
			// txtExecutionLog
			// 
			this.txtExecutionLog.Location = new System.Drawing.Point(7, 240);
			this.txtExecutionLog.Multiline = true;
			this.txtExecutionLog.Name = "txtExecutionLog";
			this.txtExecutionLog.ReadOnly = true;
			this.txtExecutionLog.ScrollBars = System.Windows.Forms.ScrollBars.Both;
			this.txtExecutionLog.Size = new System.Drawing.Size(647, 100);
			this.txtExecutionLog.TabIndex = 5;
			// 
			// txtScriptToRun
			// 
			this.txtScriptToRun.Location = new System.Drawing.Point(7, 77);
			this.txtScriptToRun.Multiline = true;
			this.txtScriptToRun.Name = "txtScriptToRun";
			this.txtScriptToRun.ScrollBars = System.Windows.Forms.ScrollBars.Both;
			this.txtScriptToRun.Size = new System.Drawing.Size(647, 157);
			this.txtScriptToRun.TabIndex = 4;
			// 
			// txtCurrentStep
			// 
			this.txtCurrentStep.Location = new System.Drawing.Point(7, 50);
			this.txtCurrentStep.Name = "txtCurrentStep";
			this.txtCurrentStep.Size = new System.Drawing.Size(647, 20);
			this.txtCurrentStep.TabIndex = 3;
			// 
			// btnRunOverSql
			// 
			this.btnRunOverSql.Location = new System.Drawing.Point(449, 19);
			this.btnRunOverSql.Name = "btnRunOverSql";
			this.btnRunOverSql.Size = new System.Drawing.Size(207, 23);
			this.btnRunOverSql.TabIndex = 2;
			this.btnRunOverSql.Text = "Step over SQL Statement";
			this.btnRunOverSql.UseVisualStyleBackColor = true;
			this.btnRunOverSql.Click += new System.EventHandler(this.commonBtnRunScriptHandler);
			// 
			// btnStepOverSource
			// 
			this.btnStepOverSource.Location = new System.Drawing.Point(229, 20);
			this.btnStepOverSource.Name = "btnStepOverSource";
			this.btnStepOverSource.Size = new System.Drawing.Size(207, 23);
			this.btnStepOverSource.TabIndex = 1;
			this.btnStepOverSource.Text = "Step over Source";
			this.btnStepOverSource.UseVisualStyleBackColor = true;
			this.btnStepOverSource.Click += new System.EventHandler(this.commonBtnRunScriptHandler);
			// 
			// btnRun
			// 
			this.btnRun.Location = new System.Drawing.Point(7, 20);
			this.btnRun.Name = "btnRun";
			this.btnRun.Size = new System.Drawing.Size(207, 23);
			this.btnRun.TabIndex = 0;
			this.btnRun.Text = "Run";
			this.btnRun.UseVisualStyleBackColor = true;
			this.btnRun.Click += new System.EventHandler(this.commonBtnRunScriptHandler);
			// 
			// btnCancel
			// 
			this.btnCancel.Location = new System.Drawing.Point(585, 357);
			this.btnCancel.Name = "btnCancel";
			this.btnCancel.Size = new System.Drawing.Size(75, 21);
			this.btnCancel.TabIndex = 11;
			this.btnCancel.Text = "Cancel";
			this.btnCancel.UseVisualStyleBackColor = true;
			this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
			// 
			// btnNext
			// 
			this.btnNext.Location = new System.Drawing.Point(504, 356);
			this.btnNext.Name = "btnNext";
			this.btnNext.Size = new System.Drawing.Size(75, 23);
			this.btnNext.TabIndex = 10;
			this.btnNext.Text = "Next";
			this.btnNext.UseVisualStyleBackColor = true;
			this.btnNext.Click += new System.EventHandler(this.btnNext_Click);
			// 
			// btnPrevious
			// 
			this.btnPrevious.Location = new System.Drawing.Point(423, 356);
			this.btnPrevious.Name = "btnPrevious";
			this.btnPrevious.Size = new System.Drawing.Size(75, 23);
			this.btnPrevious.TabIndex = 9;
			this.btnPrevious.Text = "Previous";
			this.btnPrevious.UseVisualStyleBackColor = true;
			this.btnPrevious.Click += new System.EventHandler(this.btnPrevious_Click);
			// 
			// WizardRunScriptControl
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this.btnCancel);
			this.Controls.Add(this.btnNext);
			this.Controls.Add(this.btnPrevious);
			this.Controls.Add(this.groupBox1);
			this.Name = "WizardRunScriptControl";
			this.Size = new System.Drawing.Size(667, 385);
			this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.WizardRunScriptControl_KeyDown);
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.Button btnRunOverSql;
		private System.Windows.Forms.Button btnStepOverSource;
		private System.Windows.Forms.Button btnRun;
		private System.Windows.Forms.TextBox txtExecutionLog;
		private System.Windows.Forms.TextBox txtScriptToRun;
		private System.Windows.Forms.TextBox txtCurrentStep;
		private System.Windows.Forms.Button btnCancel;
		private System.Windows.Forms.Button btnNext;
		private System.Windows.Forms.Button btnPrevious;
	}
}
