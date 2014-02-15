using DBSetup.Common;
using DBSetup.Common.Helpers;
using DBSetup.Common.Models;
using DBSetup.Common.Statements;
using DBSetup.Helpers;
using DBSetup.States;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DBSetup
{
	internal enum RunStatus : int
	{
		STOPPED = 0,
		RUNNING = 1,
		STEPSOURCE = 2,
		STEPSTATEMENT = 3,
		/// <summary>
		/// Errored state
		/// </summary>
		ERROR = 4,
		/// <summary>
		/// User terminated execution
		/// </summary>
		TERMINATED = 5,
		CONTINUE = 6,
		CANCELLED = 7,
		/// <summary>
		/// Execution completed
		/// </summary>
		FINISHED = 8
	}

	public partial class WizardRunScriptControl : UserControl
	{
		//wizard's constants
		private const string _runString = "Run";
		private const string _stopString = "Stop";
		private const string _dateTimeFormat = "dd-MM-yyyy hh:mm:ss";
		private const string _logLocation = @"Log\DbSetupLog.txt";

		//delay after script ends it's execution (ms)
		private const int scriptSleepTimeout = 60;

		//Time to wait before SQL command will be out of execution process (sec)
		//TTP:  TTP 4963 - SHOWSTOPPER- Database Upgrade fails for DB version 107,
		//forced to set this parameter to infinite timeout
		private const int sqlCommandTimeout = 0;

		private WizardMain rootForm;
		private IDataStatement _currentStatement;
		private bool _isHadlerFirstlyInvoked = false;

		private int _statementIndex = -1;

		private StateDBSettings _dbSettings;
		private SqlConnection _sqlConnection;

		private ISqlConnectionSettings _sqlSettings;
		private string _dbName;

		private bool _isFirstRun = true;
		private bool IsFirstRun
		{
			get
			{
				bool isLockTaken = false;
				try
				{
					Monitor.Enter(_lockObj, ref isLockTaken);
					return _isFirstRun;
				}
				finally
				{
					if (isLockTaken)
					{
						Monitor.Pulse(_lockObj);
						Monitor.Exit(_lockObj);
					}
				}
			}
			set
			{
				bool isLockTaken = false;
				try
				{
					Monitor.Enter(_lockObj, ref isLockTaken);
					_isFirstRun = value;
				}
				finally
				{
					if (isLockTaken)
					{
						Monitor.Pulse(_lockObj);
						Monitor.Exit(_lockObj);
					}
				}
			}
		}

		//Check if execution workflow is resides in state pending user input
		public bool IsEventPaused
		{
			get { return !_signalEvent.WaitOne(1); }
		}

		private RunStatus _curRunStatus;
		internal RunStatus CurrentRunStatus
		{
			get
			{
				bool isLockTaken = false;
				try
				{
					Monitor.Enter(_lockObj, ref isLockTaken);
					return _curRunStatus;
				}
				finally
				{
					if (isLockTaken)
					{
						Monitor.Pulse(_lockObj);
						Monitor.Exit(_lockObj);
					}
				}
			}
			set
			{
				bool isLockTaken = false;
				try
				{
					Monitor.Enter(_lockObj, ref isLockTaken);
					_curRunStatus = value;
				}
				finally
				{
					if (isLockTaken)
					{
						Monitor.Pulse(_lockObj);
						Monitor.Exit(_lockObj);
					}
				}
			}
		}

		private readonly object _lockObj;
		private readonly CancellationTokenSource _cts; // cancellation source
		private ManualResetEvent _signalEvent;
		private Thread _mainRunner; //main execution thread

		private ISectionHandler currentHandler;

		//ctor
		public WizardRunScriptControl()
		{
			_lockObj = new object();
			_signalEvent = new ManualResetEvent(false);

			_mainRunner = new Thread(new ThreadStart((ExecuteWorkflow)));
			_mainRunner.Priority = ThreadPriority.BelowNormal;
			_mainRunner.IsBackground = true;
			_mainRunner.Name = "MainRunTask";

			_cts = new CancellationTokenSource();

			CurrentRunStatus = DBSetup.RunStatus.STOPPED;

			InitializeComponent();
			this.Load += WizardRunScriptControl_Load;
		}

		private void WizardRunScriptControl_Load(object sender, EventArgs e)
		{
			if ((TopLevelControl as WizardMain) != null)
				rootForm = TopLevelControl as WizardMain;

			btnPrevious.Enabled = false;
			btnNext.Enabled = false;

			var mainTask = Task.Factory.StartNew<List<IDataStatement>>(GenerateStatements);

			this.DisableCtrlPlusA();

			mainTask.RegisterFaultedHandler(OnMainError, TaskScheduler.FromCurrentSynchronizationContext());
			mainTask.RegisterSucceededHandler(OnMainSucceed, TaskScheduler.FromCurrentSynchronizationContext());
		}

		private void WizardRunScriptControl_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Control && e.KeyCode == Keys.N)
			{
				ProceedNextStep();
			}
			else if (e.Control && e.KeyCode == Keys.T)
				ConfirmCancellation(false);
		}

		private void OnMainSucceed(List<IDataStatement> result)
		{
			groupBox1.Enabled = true;
			Log.Instance.Info(StringsContainer.Instance.LisOfSqlStatemenetsSuccess);
			StateContainer.Instance.GetState<RunScriptState>().DataStatements = result;

			int indx = StringsContainer.Instance.ConfigScriptAndUserDataMsg.IndexOf("(");
			string setupText = StringsContainer.Instance.ConfigScriptAndUserDataMsg.Substring(0, indx);
			SectionBase section = new SqlLink() { Text = setupText, FileName = setupText };
			section.Handler = new SQLSectionHandler();

			IDataStatement newSQl = new SqlDataStatement(PrepareNewSetupSQLScripts(), setupText);
			newSQl.ContentRoot = section;

			StateContainer.Instance.GetState<RunScriptState>().DataStatements.Insert(0, newSQl);

			_dbSettings = StateContainer.Instance.GetState<RunScriptState>().DbConSettings;
			_sqlSettings = new SqlConnectionSettings(_dbSettings.ServerName, _dbName, _dbSettings.UserName, _dbSettings.Password);
			_mainRunner.Start();
		}

		private void OnMainError(Exception exc)
		{
			groupBox1.Enabled = true;
			Log.Instance.Error(StringsContainer.Instance.FailToBuildSqlStatementList, exc);
			MessageBox.Show(rootForm, StringsContainer.Instance.FailToBuildSqlStatementList, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}

		#region Button handlers

		private void btnPrevious_Click(object sender, EventArgs e)
		{
		}

		private void btnNext_Click(object sender, EventArgs e)
		{
			if ((sender as Button).Text.IndexOf("Exit") != -1)
			{
				rootForm = null;
				this.Load -= WizardRunScriptControl_Load;
				StateContainer.Instance.GetState<RunScriptState>().DataStatements.Clear();

				if (_sqlSettings != null) _sqlSettings.Dispose();

				Program.IsExitRequired = true;
				Application.Exit();
			}
			else
				ProceedNextStep();
		}

		private void ProceedNextStep()
		{
			this.RevertCtrlPlusA();

			if (_sqlSettings != null)
				_sqlSettings.Dispose();

			if (rootForm != null)
			{
				if (StateContainer.Instance[6] == null)
					StateContainer.Instance.AddState(6, new FinalState());

				try
				{
					StateContainer.Instance.GetState<RunScriptState>().IsSuccessed = 1;

					if (StateContainer.Instance.GetState<RunScriptState>().DataStatements != null &&
						StateContainer.Instance.GetState<RunScriptState>().DataStatements.Count > 0)
						StateContainer.Instance.GetState<RunScriptState>().DataStatements.Clear();

					_signalEvent.Dispose();
					_signalEvent = null;
				}
				catch { }

				rootForm.Controls.Clear();
				rootForm.Controls.Add(new FinalizationControl());

				this.Load -= WizardRunScriptControl_Load;
				rootForm = null;
			}
		}

		private void btnCancel_Click(object sender, EventArgs e)
		{
			ConfirmCancellation();
		}

		private void ConfirmCancellation(bool withExit = true)
		{
			if (MessageBox.Show(rootForm, StringsContainer.Instance.ExitConfirmation, StringsContainer.Instance.ExitConfirmationTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question)
				== System.Windows.Forms.DialogResult.Yes)
			{
				StateContainer.Instance.GetState<RunScriptState>().IsSuccessed = -1;

				_cts.Cancel();
				CancelHandlerExecution();
				this.RevertCtrlPlusA();

				try
				{
					if (CurrentRunStatus == RunStatus.STEPSOURCE || CurrentRunStatus == RunStatus.STEPSTATEMENT)
					{
						if (IsEventPaused)
						{
							_signalEvent.Reset();
							_signalEvent.Set();
						}
					}
				}
				catch (Exception ex)
				{
					Log.Instance.Warn(ex.Message);
				}
				finally
				{
					CurrentRunStatus = RunStatus.CANCELLED;
				}

				if (withExit)
				{
					Program.IsExitRequired = true;
					Application.Exit();
				}
			}
		}

		private void commonBtnRunScriptHandler(object sender, EventArgs e)
		{
			var pressedBtn = sender as Button;
			if (pressedBtn != null)
			{
				if (IsFirstRun)
					IsFirstRun = false;

				switch (pressedBtn.Name)
				{
					case "btnRun":
						if (pressedBtn.Text.Equals(_runString, StringComparison.OrdinalIgnoreCase))
						{
							CurrentRunStatus = RunStatus.RUNNING;
							pressedBtn.Text = _stopString;
							DisableScriptEditing(true);
							DisableStepsButtons(true);
							_signalEvent.Set();
						}
						else if (pressedBtn.Text.Equals(_stopString, StringComparison.OrdinalIgnoreCase))
						{
							CurrentRunStatus = RunStatus.STOPPED;
							pressedBtn.Text = _runString;
							DisableScriptEditing(false);
							DisableStepsButtons(false);
							_signalEvent.Set();
						}
						break;

					case "btnStepOverSource":
						CurrentRunStatus = RunStatus.STEPSOURCE;
						DisableStepsAndChangeRun(true, false);

						//if (btnRun.Text.IndexOf(_runString, StringComparison.OrdinalIgnoreCase) >= 0)
						//	btnRun.Text = _stopString;
						_signalEvent.Set();

						break;

					case "btnRunOverSql":
						CurrentRunStatus = RunStatus.STEPSTATEMENT;
						_signalEvent.Set();

						break;

					default:
						break;
				}
			}
		}

		private void SetButtonsEnabled(bool b)
		{
			this.ExecAction(() =>
			{
				btnRun.Enabled = b;
				btnStepOverSource.Enabled = b;
				btnRunOverSql.Enabled = b;
			});
		}

		public void SimulateRunStopClick()
		{
			this.btnRun.ExecAction(() => btnRun.PerformClick());
		}

		private void DisableStepsAndChangeRun(bool isDisabled, bool isRun)
		{
			this.ExecAction(() =>
				{
					btnRunOverSql.Enabled = !isDisabled;
					btnStepOverSource.Enabled = !isDisabled;

					if (isRun)
						btnRun.Text = _runString;
					else
						btnRun.Text = _stopString;
				});
		}

		private void DisableStepsButtons(bool isDisabled)
		{
			this.ExecAction(() =>
			{
				btnRunOverSql.Enabled = !isDisabled;
				btnStepOverSource.Enabled = !isDisabled;
			});
		}

		private void DisableScriptEditing(bool isDisabled)
		{
			txtScriptToRun.ExecAction(() => txtScriptToRun.ReadOnly = isDisabled);
		}

		#endregion Button handlers

		#region workflow execution

		private void ExecuteWorkflow()
		{
			SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
			builder.DataSource = _dbSettings.ServerName;
			builder.UserID = _dbSettings.UserName;
			builder.Password = _dbSettings.Password;
			builder.MaxPoolSize = 20;
			builder.Pooling = true;

			using (_sqlConnection = new SqlConnection(builder.ToString()))
			{
				if (_sqlConnection.State != System.Data.ConnectionState.Open && _sqlConnection.State != System.Data.ConnectionState.Connecting)
					_sqlConnection.Open();

				#region perform initial log info

				txtExecutionLog.ExecAction(() =>
						{
							txtExecutionLog.Clear();

							txtExecutionLog.AppendText(string.Format("Log file: {0} {1} {1}",
								System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.ExecutablePath), _logLocation), Environment.NewLine));

							string compName = string.Format("Computer: {0} {1}", Environment.MachineName, Environment.NewLine);
							Log.Instance.Info(compName);
							txtExecutionLog.AppendText(compName);

							string startTime = string.Format("Start time: {0} {1}", DateTime.Now.ToString(_dateTimeFormat), Environment.NewLine);
							Log.Instance.Info(startTime);
							txtExecutionLog.AppendText(startTime);
						});

				#endregion perform initial log info

				int count = StateContainer.Instance.GetState<RunScriptState>().DataStatements.Count;
				for (int i = 0; i < count; i++)
				{
					if (CurrentRunStatus == RunStatus.CONTINUE)
						i--; //returns to the exceptional state and try to re-execute from the last saved point

					_statementIndex = i;
					_currentStatement = StateContainer.Instance.GetState<RunScriptState>().DataStatements[i];
					//CurrentRunStatus = CurrentRunStatus;

					if (_cts.IsCancellationRequested || CurrentRunStatus == RunStatus.TERMINATED)
						goto Cancel;

					#region new handling mechanism
					//handle DICOM sections
					if (!IsFirstRun && _currentStatement.Type == StatementType.Dicom)
					{
						currentHandler = _currentStatement.ContentRoot.Handler;
						if (currentHandler != null)
						{
							currentHandler.Logger = Log.Instance;
							currentHandler.Parameters = _sqlSettings;
							currentHandler.OnPreHandler(OnPreDicomHandler);
							currentHandler.OnErrorHandler(OnErrorHandler);
							currentHandler.OnStepHandler(OnDicomStepHandler);
							currentHandler.OnEntryProcessing(OnDicomEntryProcessing);

							if (currentHandler.Handle(_currentStatement.ContentRoot))
								txtExecutionLog.ExecAction(() =>
								{
									string scriptExecuted = string.Format("Executed: {0}{1}", _currentStatement.DataFile, Environment.NewLine);
									txtExecutionLog.AppendText(scriptExecuted);
									Log.Instance.Info(scriptExecuted);
								});
							else
								txtExecutionLog.ExecAction(() =>
								{
									string scriptExecuted = string.Format("Executed with error(s): {0}{1}", _currentStatement.DataFile, Environment.NewLine);
									txtExecutionLog.AppendText(scriptExecuted);
									Log.Instance.Info(scriptExecuted);
								});
						}
					}
					//handle SQL section
					else if (_currentStatement.Type == StatementType.Sql)
					{
						_isHadlerFirstlyInvoked = true;
						currentHandler = _currentStatement.ContentRoot.Handler;
						currentHandler.Logger = Log.Instance;
						currentHandler.Parameters = new Tuple<SqlConnection, IDataStatement>(_sqlConnection, _currentStatement);
						currentHandler.OnPreHandler(OnPreSqlStage);
						currentHandler.OnOutputReceived(OnSqlEngineOutput);
						currentHandler.OnStepHandler(OnSQLStep);
						currentHandler.OnErrorHandler(OnSqlError);

						if (currentHandler.Handle(_currentStatement.ContentRoot))
						{
							txtExecutionLog.ExecAction(() =>
							{
								string scriptExecuted = string.Format("Executed: {0}{1}", _currentStatement.DataFile, Environment.NewLine);
								txtExecutionLog.AppendText(scriptExecuted);
								Log.Instance.Info(scriptExecuted);
							});
						}
						else
						{
							txtExecutionLog.ExecAction(() =>
							{
								string scriptExecuted = string.Format("Executed with error(s): {0}{1}", _currentStatement.DataFile, Environment.NewLine);
								txtExecutionLog.AppendText(scriptExecuted);
								Log.Instance.Info(scriptExecuted);
							});
						}
						_signalEvent.Reset();
					}
					#endregion
				}// end for loop statement

				if (CurrentRunStatus != RunStatus.TERMINATED && CurrentRunStatus != RunStatus.CANCELLED)
					CurrentRunStatus = RunStatus.FINISHED;

			Cancel:

				txtExecutionLog.ExecAction(() =>
				{
					string msgEndTime = string.Format("End time: {0}{1}", DateTime.Now.ToString(_dateTimeFormat), Environment.NewLine);
					Log.Instance.Info(msgEndTime);
					txtExecutionLog.AppendText(msgEndTime);

					btnNext.Enabled = true;
					btnCancel.Enabled = false;

					txtCurrentStep.Clear();
					txtScriptToRun.Clear();
					txtCurrentStep.Enabled = false;
					txtScriptToRun.Enabled = false;

					if (CurrentRunStatus == RunStatus.CANCELLED)
					{
						string cancelledMessage = string.Format("Workflow has been cancelled {0}{1}", DateTime.Now.ToString(_dateTimeFormat), Environment.NewLine);
						Log.Instance.Info(cancelledMessage);
						txtExecutionLog.AppendText(cancelledMessage);
						btnNext.Text = "Exit";
					}
				});

				SetButtonsEnabled(false);

				try
				{
					if (_sqlConnection.State == System.Data.ConnectionState.Open && _sqlConnection.State != System.Data.ConnectionState.Closed)
						_sqlConnection.Close();

					var type = StateContainer.Instance.GetState<States.DbSetupState>().DatabaseSetupType;
					if (type == DbSetupType.Upgrade)
						this.ExecAction(() => btnNext.Text = "Exit");

				}
				catch (SqlException ex)
				{
					Log.Instance.Error("Error has been occurred while closing SQL connection.", ex);
				}
				finally
				{
					GC.Collect();
				}
			}//end using SqlConnection
		}

		#region SQL handler callbacks

		private void OnPreSqlStage(string arg1, object arg2)
		{
			txtScriptToRun.ExecAction(() => txtScriptToRun.Text = arg1);
		}

		private void OnSqlEngineOutput(string output)
		{
			if (output != null)
			{
				Log.Instance.Info(output);
				txtExecutionLog.ExecAction(() => txtExecutionLog.AppendText(output));
			}
		}

		private void OnSQLStep(string state)
		{
			string[] data = state.Split(',');
			txtCurrentStep.ExecAction(() =>
				txtCurrentStep.Text = string.Format(StringsContainer.Instance.SqlCurrentStepMsg, data[0], data[1], data[2]));

			if (_isHadlerFirstlyInvoked && CurrentRunStatus == RunStatus.STEPSOURCE)
			{
				WaitForUser();
				_isHadlerFirstlyInvoked = false;
			}
			else if (CurrentRunStatus == RunStatus.STEPSTATEMENT || CurrentRunStatus == RunStatus.STOPPED)
			{
				_signalEvent.Reset();
				WaitForUser();
			}

			CancelHandlerExecution();
		}

		private object OnSqlError(Exception exc, object state)
		{
			string edited = null;
			_signalEvent.Reset();
			WaitForUser(false);
			this.ExecAction(() =>
			{
				string failMsg = string.Format("Fail: {0} {1} Message: {2}{1}", _currentStatement.DataFile, Environment.NewLine, exc.Message);
				Log.Instance.Error(failMsg);
				txtExecutionLog.AppendText(failMsg);
				MessageBox.Show(rootForm, exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			});
			_signalEvent.WaitOne();

			txtScriptToRun.ExecAction(() => edited = txtScriptToRun.Text);
			return edited;
		}
		#endregion

		#region DICOM handler callbacks

		private void OnPreDicomHandler(string arg1, object arg2)
		{
			this.ExecAction(() =>
			{
				string beginMsg = string.Format("Begin to process DICOM : {0} {1}", arg1, Environment.NewLine);
				Log.Instance.Info(beginMsg);
				txtExecutionLog.AppendText(beginMsg);
				txtScriptToRun.Text = string.Empty;
			});

			if (CurrentRunStatus == RunStatus.STEPSOURCE)
			{
				_signalEvent.Reset();
				WaitForUser();
			}
		}

		private void OnDicomEntryProcessing(string action, string file, object state)
		{
			this.ExecAction(() =>
			{
				if (action.IndexOf("Processing", StringComparison.OrdinalIgnoreCase) != -1 && state != null)
					txtScriptToRun.Text = (state as string);
				else
					txtScriptToRun.Text = action;

			});

			if (CurrentRunStatus == RunStatus.STEPSTATEMENT || CurrentRunStatus == RunStatus.STOPPED)
			{
				_signalEvent.Reset();
				WaitForUser();
			}

			CancelHandlerExecution();
		}

		private void OnDicomStepHandler(string file)
		{
			this.ExecAction(() =>
			{
				txtCurrentStep.ExecAction(() => txtCurrentStep.Text = string.Format(StringsContainer.Instance.DICOMCurrentStepMsg,
																											_currentStatement.DataFile, (_currentStatement as DicomDataStatement).IsActive));
			});
		}

		private object OnErrorHandler(Exception ex, object state)
		{
			this.ExecAction(() =>
			{
				string failMsg = string.Format("Fail: {0} {1} Message: {2}{1}", _currentStatement.DataFile, Environment.NewLine, ex.Message);
				Log.Instance.Error(failMsg);
				txtExecutionLog.AppendText(failMsg);
				MessageBox.Show(rootForm, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

			});

			return null;
		}
		#endregion

		private void WaitForUser(bool wait = true)
		{
			this.ExecAction(() =>
			{
				if (btnRun.Text.IndexOf(_stopString, StringComparison.OrdinalIgnoreCase) >= 0)
					btnRun.Text = _runString;
			});
			DisableStepsAndChangeRun(false, true);
			DisableScriptEditing(false);
			if (wait)
				_signalEvent.WaitOne();
		}

		private void CancelHandlerExecution()
		{
			if (currentHandler != null && _cts.IsCancellationRequested)
				currentHandler.Cancel();
		}

		#endregion workflow execution

		#region Helpers

		private List<IDataStatement> GenerateStatements()
		{
			this.ExecAction(() => groupBox1.Enabled = false);
			return StateContainer.Instance.GetState<RunScriptState>().StatementFactory.Value.Generate();
		}

		private string PrepareNewSetupSQLScripts()
		{
			StringBuilder buffer = new StringBuilder();
			States.DbSetupType setupType = (States.StateContainer.Instance[3] as States.DbSetupState).DatabaseSetupType;

			buffer.Append("select @@version \n GO \n");
			buffer.Append("USE master \n GO \n");

			string DbName = string.Empty;
			string dbPath = string.Empty;
			string logPath = string.Empty;
			int dbSize = 0, dbGrowthSize = 0, logSize = 0, logGrowthSize = 0;

			var settingsDB = States.StateContainer.Instance.GetState<States.DbSetupState>();
			if (settingsDB != null)
			{
				DbName = System.IO.Path.GetFileNameWithoutExtension(settingsDB.DbFileName);
				_dbName = DbName;
				dbPath = settingsDB.DbFilePath;

				dbSize = settingsDB.DbInitialSize;
				dbGrowthSize = settingsDB.DbGrowth;

				logPath = settingsDB.LogFilePath;
				logSize = settingsDB.LogInitialSize;
				logGrowthSize = settingsDB.LogGrowth;
			}

			if (States.DbSetupType.New == setupType)
			{
				buffer.Append(string.Format("IF EXISTS (SELECT * FROM sysdatabases WHERE name = '{0}') \n", DbName) +
						"  BEGIN \n" +
						string.Format("    SELECT 'Dropping database: {0} ...' \n", DbName) +
						"  End \n GO \n" +
						string.Format("IF EXISTS (SELECT * FROM sysdatabases WHERE name = '{0}') \n", DbName) +
						"  BEGIN \n" +
						string.Format("	ALTER DATABASE {0} SET SINGLE_USER WITH ROLLBACK IMMEDIATE \n", DbName) +
						string.Format("    DROP DATABASE {0} \n", DbName) +
						"  End \n GO \n"
				);
			}

			if (setupType == States.DbSetupType.New)
			{
				buffer.Append(string.Format("SELECT 'Creating database: {0} ...' \n", DbName) +
								string.Format("GO \n USE master \n CREATE DATABASE {0} \n", DbName));
				buffer.Append("ON \n PRIMARY ( \n");

				if (DbName.Length > 0)
					buffer.Append(string.Format("    NAME={0},\n", DbName));
				if (dbPath.Length > 0)
					buffer.Append("    FILENAME='" + dbPath + "',\n");
				if (dbSize > 0)
					buffer.Append("    SIZE=" + dbSize + "MB,\n");

				buffer.Append("    FILEGROWTH=" + dbGrowthSize.ToString() + (settingsDB.DbGrowthType == States.GrowthType.MB ? "MB" : "%") + ")\n");

				buffer.Append("LOG ON ( \n");
				if (settingsDB.LogFileName.Length > 0)
					buffer.Append("    NAME=" + System.IO.Path.GetFileNameWithoutExtension(settingsDB.LogFileName) + ",\n");
				if (logPath.Length > 0)
					buffer.Append("    FILENAME='" + logPath.ToString() + "',\n");
				if (logSize > 0)
					buffer.Append("    SIZE=" + logSize.ToString() + "MB,\n");

				buffer.Append("    FILEGROWTH=" + logGrowthSize.ToString() + (settingsDB.LogGrowthType == States.GrowthType.MB ? "MB" : "%") + ")\n");

				//TODO: figure out how to set that flag!!
				//if (wizardDialog.bCreateForLoad)
				//{
				//	buffer.Append("FOR RESTORE \n");
				//}

				buffer.Append("GO \n");
			}
			else if (setupType == States.DbSetupType.LoadSetup)
			{
				/*If result is "LOAD", Get the DB Backup Database to restore.
				The assumption is that it is in the same directory as setup.ini file.
				Get the file name in .INI file.*/
				/*
					  System.out.println("RESTORE DATABASE " + DbName + " FROM DISK = '" +
							previousFileName.substring(0,previousFileName.lastIndexOf('\\')).trim() + "\\" + loadFile + "'\n" +
									"WITH MOVE '" + dataName.getText() + "' to '" + dataLocation.getText()+ "',\n" +
									"MOVE '" + logName.getText() + "' to '" + logLocation.getText() + "'\n" +
									"GO \n");

				buffer.Append("RESTORE DATABASE " + DbName + " FROM DISK = '" +
						previousFileName.substring(0, previousFileName.lastIndexOf('\\')).trim() + "\\" +
						(String)loadFile.get(setupConfigList.getSelectedIndex()) + "'\n" +
						"WITH MOVE '" + dataName.getText() + "' to '" + dataLocation.getText() + "',\n" +
						"MOVE '" + logName.getText() + "' to '" + logLocation.getText() + "'\n" +
						"GO \n"
				);*/
			}

			// for all
			if (((States.DbSetupType.LoadSetup == setupType) ||
				 (States.DbSetupType.New == setupType) ||
				 (States.DbSetupType.Upgrade == setupType))
				 &&
				 (settingsDB.DatabaseConfiguration != null && settingsDB.DatabaseConfiguration.Children != null)
				)
			{
				SettingsPair pair = new SettingsPair();

				#region in case when SQL Server version is less then SQL Server 2012

				if (StateContainer.Instance.GetState<SqlServerReportState>().SQLVersion.IndexOf("2012", StringComparison.Ordinal) == -1)
				{
					ChangePairValue(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
									x => x.Key.Equals("SelectIntoBulkCopy", StringComparison.OrdinalIgnoreCase)));
					buffer.Append("exec sp_dboption " + DbName + ", 'select into/bulkcopy', " + (pair == null ? "false" : pair.Value));
					buffer.Append("\nGO\n");

					ChangePairValue(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
												x => x.Key.Equals("ColumnsNullByDefault", StringComparison.OrdinalIgnoreCase)));
					buffer.Append("exec sp_dboption " + DbName + ", 'ANSI null default', " + (pair == null ? "false" : pair.Value));
					buffer.Append("\nGO\n");

					ChangePairValue(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
												x => x.Key.Equals("TruncateLogOnCheckpoint", StringComparison.OrdinalIgnoreCase)));
					buffer.Append("exec sp_dboption " + DbName + ", 'trunc. log on chkpt.', " + (pair == null ? "false" : pair.Value));
					buffer.Append("\nGO\n");

					ChangePairValue(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
																						x => x.Key.Equals("SingleUser", StringComparison.OrdinalIgnoreCase)));
					buffer.Append("exec sp_dboption " + DbName + ", 'single user', " + (pair == null ? "false" : pair.Value));
					buffer.Append("\nGO\n");

					ChangePairValue(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
																						x => x.Key.Equals("DBOUseOnly", StringComparison.OrdinalIgnoreCase)));
					buffer.Append("exec sp_dboption " + DbName + ", 'dbo use only', " + (pair == null ? "false" : pair.Value));
					buffer.Append("\nGO\n");

					ChangePairValue(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
																					x => x.Key.Equals("ReadOnly", StringComparison.OrdinalIgnoreCase)));
					buffer.Append("exec sp_dboption " + DbName + ", 'read only', " + (pair == null ? "false" : pair.Value));
					buffer.Append("\nGO\n");

					buffer.Append("USE " + DbName); buffer.Append("\nGO\n");

					ChangePairValueOnOff(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
													x => x.Key.Equals("Arithabort", StringComparison.OrdinalIgnoreCase)));

					buffer.AppendFormat("SET ARITHABORT {0} \n GO \n", pair == null ? "ON" : pair.Value);

					ChangePairValueOnOff(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
													x => x.Key.Equals("quoted_identifier", StringComparison.OrdinalIgnoreCase)));

					buffer.AppendFormat("SET QUOTED_IDENTIFIER {0}", pair == null ? "OFF" : pair.Value);
					buffer.Append("\nGO\n");

					ChangePairValueOnOff(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
													x => x.Key.Equals("ansi_nulls", StringComparison.OrdinalIgnoreCase)));

					buffer.AppendFormat("SET ANSI_NULLS {0}", pair == null ? "OFF" : pair.Value);
					buffer.Append("\nGO\n");
				}

				#endregion in case when SQL Server version is less then SQL Server 2012

				#region in case when SQL Server version is equals SQL Server 2012

				else
				{
					pair = settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(x => x.Key.Equals(
																											"SelectIntoBulkCopy", StringComparison.OrdinalIgnoreCase));
					buffer.Append(string.Format("ALTER DATABASE {0} SET RECOVERY {1} \nGO\n", DbName,
						(pair == null ? "SIMPLE" : pair.Value.IndexOf("yes", StringComparison.OrdinalIgnoreCase) == -1 ? "SIMPLE" : "FULL")));

					pair = settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
									x => x.Key.Equals("ColumnsNullByDefault", StringComparison.OrdinalIgnoreCase));
					buffer.Append(string.Format("ALTER DATABASE {0} SET ANSI_NULL_DEFAULT {1} \nGO\n", DbName,
						(pair == null ? "ON" : pair.Value.IndexOf("yes", StringComparison.OrdinalIgnoreCase) == -1 ? "OFF" : "ON")));

					pair = settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(x => x.Key.Equals(
																											"TruncateLogOnCheckpoint", StringComparison.OrdinalIgnoreCase));
					buffer.Append(string.Format("ALTER DATABASE {0} SET RECOVERY {1} \nGO\n", DbName,
						(pair == null ? "FULL" : pair.Value.IndexOf("yes", StringComparison.OrdinalIgnoreCase) == -1 ? "FULL" : "SIMPLE")));

					pair = settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(x => x.Key.Equals(
																											"SingleUser", StringComparison.OrdinalIgnoreCase));
					buffer.Append(string.Format("ALTER DATABASE {0} SET {1} \nGO\n", DbName,
						(pair == null ? "SIMPLE" : pair.Value.IndexOf("yes", StringComparison.OrdinalIgnoreCase) == -1 ? "SINGLE_USER" : "MULTI_USER")));

					pair = settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(x => x.Key.Equals(
																											"DBOUseOnly", StringComparison.OrdinalIgnoreCase));
					buffer.Append(string.Format("ALTER DATABASE {0} SET {1} \nGO\n", DbName,
						(pair == null ? "MULTI_USER" : pair.Value.IndexOf("yes", StringComparison.OrdinalIgnoreCase) == -1 ? "MULTI_USER" : "RESTRICTED_USER")));

					pair = settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(x => x.Key.Equals(
																					"ReadOnly", StringComparison.OrdinalIgnoreCase));
					buffer.Append(string.Format("ALTER DATABASE {0} SET {1} \nGO\n", DbName,
						(pair == null ? "READ_WRITE" : pair.Value.IndexOf("yes", StringComparison.OrdinalIgnoreCase) == -1 ? "READ_WRITE" : "READ_ONLY")));

					buffer.Append("USE " + DbName + "\nGO\n");

					ChangePairValueOnOff(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
													x => x.Key.Equals("Arithabort", StringComparison.OrdinalIgnoreCase)));

					buffer.AppendFormat("SET ARITHABORT {0} \n GO \n", pair == null ? "ON" : pair.Value);

					ChangePairValueOnOff(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
													x => x.Key.Equals("quoted_identifier", StringComparison.OrdinalIgnoreCase)));

					buffer.AppendFormat("SET QUOTED_IDENTIFIER {0}", pair == null ? "OFF" : pair.Value);
					buffer.Append("\nGO\n");

					ChangePairValueOnOff(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
													x => x.Key.Equals("ansi_nulls", StringComparison.OrdinalIgnoreCase)));

					buffer.AppendFormat("SET ANSI_NULLS {0}", pair == null ? "OFF" : pair.Value);
					buffer.Append("\nGO\n");
				}

				#endregion in case when SQL Server version is equals SQL Server 2012
			}//end if

			return buffer.ToString();
		}

		private void ChangePairValue(ref SettingsPair changedPair, SettingsPair pair)
		{
			if (pair == null)
			{
				changedPair = pair;
				return;
			}
			if (pair.Value.IndexOf("yes", StringComparison.OrdinalIgnoreCase) != -1 ||
				pair.Value.IndexOf("no", StringComparison.OrdinalIgnoreCase) != -1)
				pair.Value = (pair.Value.IndexOf("yes", StringComparison.OrdinalIgnoreCase) != -1 ? "true" : "false");

			changedPair = pair;
		}

		private void ChangePairValueOnOff(ref SettingsPair changedPair, SettingsPair pair)
		{
			if (pair == null)
			{
				changedPair = pair;
				return;
			}
			if (pair.Value.IndexOf("on", StringComparison.OrdinalIgnoreCase) != -1 ||
				pair.Value.IndexOf("off", StringComparison.OrdinalIgnoreCase) != -1)
				pair.Value = (pair.Value.IndexOf("on", StringComparison.OrdinalIgnoreCase) != -1 ? "on" : "off");

			changedPair = pair;
		}

		internal Nullable<bool> IsThreadRunning()
		{
			if (_mainRunner != null)
			{
				return (_mainRunner.IsAlive);
			}
			return null;
		}

		internal void TerminateThreadExec()
		{
			var val = IsThreadRunning();
			if (val.HasValue && val.Value)
			{
				try
				{
					_mainRunner.Abort();
				}
				catch { }
			}
		}

		#endregion Helpers
	}
}