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
		ERROR = 4,
		TERMINATED = 5,
		CONTINUE = 6,
		CANCELLED = 7,

		FINISHED = 8
	}

	public partial class WizardRunScriptControl : UserControl
	{
		private const string _runString = "Run";
		private const string _stopString = "Stop";
		private const string _dateTimeFormat = "dd-MM-yyyy hh:mm:ss";
		private const int _maxRetriesCount = 5;

		//delay after script ends it's execution (ms)
		private const int scriptSleepTimeout = 60;

		//Time to wait before SQL command will be out of execution process (sec)
		//TTP:  TTP 4963 - SHOWSTOPPER- Database Upgrade fails for DB version 107,
		//forced to set this parameter to infinite timeout
		private const int sqlCommandTimeout = 0;

		private WizardMain rootForm = null;
		private IDataStatement _currentStatement = null;
		private bool _requireUserInteruption = false;
		private bool _isExceptionalState = false;

		private int _statementIndex = -1;
		private int _SqlPartIndex = -1;
		private string[] _SQLToBeExecuted = null;

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

		private bool _isTriesExceed = false;
		private bool IsTriesExceed
		{
			get
			{
				bool isLockTaken = false;
				try
				{
					Monitor.Enter(_lockObj, ref isLockTaken);
					return _isTriesExceed;
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
					_isTriesExceed = value;
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
			get { return !_signalEvent.WaitOne(3); }
		}

		private int _exceptionOccurs = 0;
		private int ExceptionOccurs
		{
			get
			{
				bool isLockTaken = false;
				try
				{
					Monitor.Enter(_lockObj, ref isLockTaken);
					return _exceptionOccurs;
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
					_exceptionOccurs = value;
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
		private Thread _mainRunner = null; //main execution thread

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

			this.DisableCtrlPlusA();//.groupBox1.DisableCtrlPlusA();

			mainTask.RegisterFaultedHandler(OnMainError, TaskScheduler.FromCurrentSynchronizationContext());
			mainTask.RegisterSucceededHandler(OnMainSucceed, TaskScheduler.FromCurrentSynchronizationContext());
		}

		private void OnMainSucceed(List<IDataStatement> result)
		{
			groupBox1.Enabled = true;
			Log.Instance.Info(StringsContainer.Instance.LisOfSqlStatemenetsSuccess);
			StateContainer.Instance.GetConcreteInstance<RunScriptState>().DataStatements = result;
			StateContainer.Instance.GetConcreteInstance<RunScriptState>().DataStatements.Insert(0, new SqlDataStatement(GetSetupSQLScripts(),
					StringsContainer.Instance.ConfigScriptAndUserDataMsg.Substring(0, StringsContainer.Instance.ConfigScriptAndUserDataMsg.IndexOf("("))));

			_dbSettings = StateContainer.Instance.GetConcreteInstance<RunScriptState>().DbConSettings;
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
				this.Load -= WizardRunScriptControl_Load;
				rootForm = null;

				Program.ISExitRequired = true;
				Application.Exit();
			}
			else
				ProceedNextStep();
		}

		private void ProceedNextStep()
		{
			if (_SQLToBeExecuted != null && _SQLToBeExecuted.Length > 0)
			{
				Array.Resize(ref _SQLToBeExecuted, 1);
				_SQLToBeExecuted = null;
			}

			this.RevertCtrlPlusA();
			if (rootForm != null)
			{
				if (StateContainer.Instance[6] == null)
					StateContainer.Instance.AddState(6, new FinalState());

				try
				{
					StateContainer.Instance.GetConcreteInstance<RunScriptState>().IsSuccessed = 1;

					if (StateContainer.Instance.GetConcreteInstance<RunScriptState>().DataStatements != null &&
						StateContainer.Instance.GetConcreteInstance<RunScriptState>().DataStatements.Count > 0)
						StateContainer.Instance.GetConcreteInstance<RunScriptState>().DataStatements.Clear();

					_signalEvent.Dispose();
					_signalEvent = null;
				}
				catch { }

				rootForm.Controls.Clear();
				rootForm.Controls.Add(new FinalizationControl());

				this.Load -= WizardRunScriptControl_Load;
				rootForm = null;
				GC.Collect();
			}
		}

		private void btnCancel_Click(object sender, EventArgs e)
		{
			ConfirmCancellation();
		}

		private void ConfirmCancellation()
		{
			if (MessageBox.Show(rootForm, StringsContainer.Instance.ExitConfirmation, StringsContainer.Instance.ExitConfirmationTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question)
				== System.Windows.Forms.DialogResult.Yes)
			{
				StateContainer.Instance.GetConcreteInstance<RunScriptState>().IsSuccessed = -1;
				this.RevertCtrlPlusA();
				_cts.Cancel();
				CurrentRunStatus = RunStatus.STOPPED;

				try
				{
					_signalEvent.Dispose();
					_signalEvent = null;
				}
				catch (Exception)
				{
				}
				finally { GC.Collect(); }

				Program.ISExitRequired = true;
				Application.Exit();
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
							CurrentRunStatus = DBSetup.RunStatus.RUNNING;
							pressedBtn.Text = _stopString;
							DisableScriptEditing(true);
							DisableStepsButtons(true);
							_signalEvent.Set();
						}
						else if (pressedBtn.Text.Equals(_stopString, StringComparison.OrdinalIgnoreCase))
						{
							CurrentRunStatus = DBSetup.RunStatus.STOPPED;
							pressedBtn.Text = _runString;
							DisableScriptEditing(false);
							DisableStepsButtons(false);
							_signalEvent.Set();
						}
						break;

					case "btnStepOverSource":
						CurrentRunStatus = DBSetup.RunStatus.STEPSOURCE;
						DisableStepsAndChangeRun(true, false);

						//if (btnRun.Text.IndexOf(_runString, StringComparison.OrdinalIgnoreCase) >= 0)
						//	btnRun.Text = _stopString;
						_signalEvent.Set();

						break;

					case "btnRunOverSql":
						CurrentRunStatus = DBSetup.RunStatus.STEPSTATEMENT;
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

			IsTriesExceed = false;

			using (_sqlConnection = new SqlConnection(builder.ToString()))
			{
				if (_sqlConnection.State != System.Data.ConnectionState.Open)
					_sqlConnection.Open();

				#region perform initial log info

				txtExecutionLog.ExecAction(() =>
						{
							txtExecutionLog.Clear();

							txtExecutionLog.AppendText(string.Format("Log file: {0} {1} {1}", System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.ExecutablePath),
																									@"Log\DbSetupLog.txt"),
																									Environment.NewLine));

							string compName = string.Format("Computer: {0} {1}", Environment.MachineName, Environment.NewLine);
							Log.Instance.Info(compName);
							txtExecutionLog.AppendText(compName);

							string startTime = string.Format("Start time: {0} {1}", DateTime.Now.ToString(_dateTimeFormat), Environment.NewLine);
							Log.Instance.Info(startTime);
							txtExecutionLog.AppendText(startTime);
						});

				#endregion perform initial log info

				for (int i = 0; i < StateContainer.Instance.GetConcreteInstance<RunScriptState>().DataStatements.Count; i++)
				{
					if (CurrentRunStatus == RunStatus.CONTINUE)
						i--; //returns to the exceptional state and try to re-execute from last saved point

					_statementIndex = i;
					_currentStatement = StateContainer.Instance.GetConcreteInstance<RunScriptState>().DataStatements[i];
					CurrentRunStatus = CurrentRunStatus;

					if (_cts.IsCancellationRequested || CurrentRunStatus == RunStatus.TERMINATED)
						goto Cancel;

					//handle DICOM sections
					if (_currentStatement.Type == StatementType.Dicom)
					{
						ISectionHandler handler = _currentStatement.ContentRoot.Handler;
						if (handler != null)
						{
							handler.Logger = Log.Instance;
							handler.Parameters = _sqlSettings;
							handler.Handle(_currentStatement.ContentRoot);
						}
					}

					if (!IsFirstRun && _currentStatement != null && _statementIndex > 0)
					{
						//check if run status is not equals Continue (set after user have been pressed yes button)
						if (CurrentRunStatus != RunStatus.CONTINUE)
						{
							_SqlPartIndex = 0;
							_SQLToBeExecuted = (_currentStatement as SqlDataStatement).SplitByGoStatementWithComments();
						}
						else //clear flag back to exceptional state
							CurrentRunStatus = RunStatus.ERROR;

						this.ExecAction(() =>
							{
								if (i == 0)
									txtCurrentStep.ExecAction(() => txtCurrentStep.Text = string.Format(StringsContainer.Instance.ConfigScriptAndUserDataMsg,
																										_SqlPartIndex + 1, _SQLToBeExecuted.Length));
								else
									txtCurrentStep.ExecAction(() => txtCurrentStep.Text = string.Format(StringsContainer.Instance.SqlCurrentStepMsg,
																										_currentStatement.DataFile, _SqlPartIndex + 1, _SQLToBeExecuted.Length));
							});
					}//end if case, when it's not a first run and statement greater than 0

					//if user has stopped execution and it is not a first run or execution mode is on step over source just wait for user input
					if ((CurrentRunStatus == RunStatus.STOPPED && !IsFirstRun) ||
							(CurrentRunStatus != RunStatus.RUNNING && CurrentRunStatus != RunStatus.ERROR &&
							 CurrentRunStatus != RunStatus.STEPSTATEMENT && CurrentRunStatus == RunStatus.STEPSOURCE))
					{
						this.ExecAction(() =>
						{
							if (btnRun.Text.IndexOf(_stopString, StringComparison.OrdinalIgnoreCase) >= 0)
								btnRun.Text = _runString;
						});
						DisableStepsAndChangeRun(false, true);
						DisableScriptEditing(false);
						_signalEvent.WaitOne(); //waits for user input
					}

					if (_cts.IsCancellationRequested) break;

					do
					{
						if (_cts.IsCancellationRequested)
							break;
						try
						{
							while ((_requireUserInteruption && CurrentRunStatus == RunStatus.ERROR && !IsFirstRun))
							{
								Application.DoEvents();
								_signalEvent.WaitOne();
							}

							if (_cts.IsCancellationRequested)
								break;

							if (_SQLToBeExecuted != null && _SqlPartIndex != -1 &&
								CurrentRunStatus != RunStatus.ERROR && _isExceptionalState)
							{
								string newSql = string.Empty;
								txtScriptToRun.ExecAction(() => newSql = txtScriptToRun.Text);
								Thread.Sleep(scriptSleepTimeout);
								_SQLToBeExecuted[_SqlPartIndex] = newSql;
							}

							ProcessSqlStatements();

							ExceptionOccurs = 0;
							_isExceptionalState = false;
							_requireUserInteruption = false;
						}
						catch (SqlException ex)
						{
							DisableStepsAndChangeRun(false, true);
							DisableScriptEditing(false);
							Log.Instance.Warn(ex.Message);

							this.ExecAction(() =>
							{
								string failMsg = string.Format("Fail: {0} {1} Message: {2}{1}", _currentStatement.DataFile, Environment.NewLine, ex.Message);
								Log.Instance.Error(failMsg);
								txtExecutionLog.AppendText(failMsg);
								MessageBox.Show(rootForm, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
							});

							CurrentRunStatus = RunStatus.ERROR;
							_signalEvent.Reset();

							_isExceptionalState = true;
							_requireUserInteruption = true;
							ExceptionOccurs++;
						}
					}//end do parentheses
					while ((CurrentRunStatus == RunStatus.ERROR && !_cts.IsCancellationRequested) &&
						(ExceptionOccurs != _maxRetriesCount));

					if ((CurrentRunStatus == RunStatus.ERROR) && (ExceptionOccurs == _maxRetriesCount))
					{
						ExceptionOccurs = 0;
						_requireUserInteruption = true;
						IsTriesExceed = true;
						bool needExit = false;

						this.ExecAction(() =>
							{
								if (MessageBox.Show(rootForm, StringsContainer.Instance.MaxTryCountIsExceeded, string.Empty, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) !=
									 DialogResult.Yes)
								{
									needExit = true;
									string execTerminatedMsg = string.Format("Execution process was terminated! {0}", Environment.NewLine);
									Log.Instance.Warn(execTerminatedMsg);
									txtExecutionLog.AppendText(execTerminatedMsg);
								}
							});

						if (needExit)
						{
							CurrentRunStatus = RunStatus.TERMINATED;
							break;
						}
						else
							CurrentRunStatus = RunStatus.CONTINUE;
					}

					if (CurrentRunStatus != RunStatus.CONTINUE)
						txtExecutionLog.ExecAction(() =>
						{
							string scriptExecuted = string.Format("Executed: {0} {1}", _currentStatement.DataFile, Environment.NewLine);
							txtExecutionLog.AppendText(scriptExecuted);
							Log.Instance.Info(scriptExecuted);
						});

					GC.Collect();

					if (_cts.IsCancellationRequested)
						break;
				}// end for loop statement

				if (CurrentRunStatus != RunStatus.TERMINATED)
					CurrentRunStatus = RunStatus.FINISHED;

			Cancel:

				if (_currentStatement != null)
					_currentStatement = null;
				if (_SQLToBeExecuted != null)
				{
					_SQLToBeExecuted = null;
					_SqlPartIndex = -1;
				}
				txtExecutionLog.ExecAction(() =>
				{
					string msgEndTime = string.Format("End time: {0} {1}", DateTime.Now.ToString(_dateTimeFormat), Environment.NewLine);
					Log.Instance.Info(msgEndTime);
					txtExecutionLog.AppendText(msgEndTime);
					txtCurrentStep.Clear();
					txtScriptToRun.Clear();
					btnNext.Enabled = true;
					btnCancel.Enabled = false;
				});

				SetButtonsEnabled(false);

				btnNext.ExecAction(() => btnNext.Enabled = true);

				try
				{
					if (_sqlConnection.State == System.Data.ConnectionState.Open)
						_sqlConnection.Close();

					var type = StateContainer.Instance.GetConcreteInstance<States.DbSetupState>().DatabaseSetupType;
					if (type == DbSetupType.Upgrade)
						this.ExecAction(() => btnNext.Text = "Exit");

				}
				catch (SqlException ex)
				{
					Log.Instance.Error("Error has been occurred while closing SQL connection", ex);
				}
				finally
				{
					GC.Collect();
				}
			}//end using SqlConnection
		}

		private void ProcessSqlStatements()
		{
			if (_currentStatement != null)
			{
				//case when we have fixed some script and need to continue execution
				if (_SqlPartIndex != -1 && _SQLToBeExecuted != null && !_cts.IsCancellationRequested)
				{
					for (int i = _SqlPartIndex; i < _SQLToBeExecuted.Length; i++)
					{
						_SqlPartIndex = i;

						if (_cts.IsCancellationRequested)
							break;

						if (StateContainer.Instance.GetConcreteInstance<RunScriptState>().DataStatements.FindIndex(x => x.Equals(_currentStatement)) == 0)
							txtCurrentStep.ExecAction(() => txtCurrentStep.Text = string.Format(StringsContainer.Instance.ConfigScriptAndUserDataMsg, i + 1, _SQLToBeExecuted.Length));
						else
							txtCurrentStep.ExecAction(() => txtCurrentStep.Text = string.Format(StringsContainer.Instance.SqlCurrentStepMsg,
																								_currentStatement.DataFile, i + 1, _SQLToBeExecuted.Length));

						string sql = DisplayScriptsToBeExecuted(i);

						if (CurrentRunStatus == RunStatus.STOPPED ||
							(CurrentRunStatus != RunStatus.RUNNING &&
							CurrentRunStatus != RunStatus.STEPSOURCE &&
							CurrentRunStatus != RunStatus.ERROR &&
							CurrentRunStatus == RunStatus.STEPSTATEMENT && !_isExceptionalState))
						{
							DisableStepsAndChangeRun(false, true);
							_signalEvent.WaitOne();
						}

						if (CurrentRunStatus == RunStatus.STEPSTATEMENT)
						{
							DisableStepsButtons(true);
							DisableScriptEditing(true);
						}
						else if (CurrentRunStatus == RunStatus.STEPSOURCE)
							DisableScriptEditing(true);

						ExecuteSql(sql);
						sql = null;
						//cleaning-up exceptions counter
						if (ExceptionOccurs > 0) ExceptionOccurs = 0;

						if (CurrentRunStatus == RunStatus.STEPSTATEMENT)
						{
							DisableScriptEditing(false);
							DisableStepsAndChangeRun(false, true);
						}

						_isExceptionalState = false;
						_signalEvent.Reset();
						CurrentRunStatus = CurrentRunStatus;
					}//end for

					_SQLToBeExecuted = null;
					_SqlPartIndex = -1;
					return;
				}//end if (case when we have fixed some script and need to continue execution)

				_SQLToBeExecuted = (_currentStatement as SqlDataStatement).SplitByGoStatementWithComments();

				//case, when user do not fixed any scripts (clean workflow)
				for (int i = 0; i < _SQLToBeExecuted.Length; i++)
				{
					_SqlPartIndex = i;

					if (_cts.IsCancellationRequested)
						break;

					if (StateContainer.Instance.GetConcreteInstance<RunScriptState>().DataStatements.FindIndex(x => x.Equals(_currentStatement)) == 0)
						txtCurrentStep.ExecAction(() => txtCurrentStep.Text = string.Format(StringsContainer.Instance.ConfigScriptAndUserDataMsg, i + 1, _SQLToBeExecuted.Length));
					else
						txtCurrentStep.ExecAction(() => txtCurrentStep.Text = string.Format(StringsContainer.Instance.SqlCurrentStepMsg,
																							_currentStatement.DataFile, i + 1, _SQLToBeExecuted.Length));

					int retry = 0;
					try
					{
						string sql = DisplayScriptsToBeExecuted(i);

						if (CurrentRunStatus == RunStatus.STOPPED ||
							(CurrentRunStatus != RunStatus.RUNNING &&
							CurrentRunStatus != RunStatus.STEPSOURCE &&
							CurrentRunStatus != RunStatus.ERROR &&
							CurrentRunStatus == RunStatus.STEPSTATEMENT && !_isExceptionalState))
						{
							DisableStepsAndChangeRun(false, true);
							_signalEvent.WaitOne();
						}

						if (CurrentRunStatus == RunStatus.STEPSTATEMENT)
						{
							DisableStepsButtons(true);
							DisableScriptEditing(true);
						}
						else if (CurrentRunStatus == RunStatus.STEPSOURCE)
							DisableScriptEditing(true);

						ExecuteSql(sql);
						sql = null;

						//cleaning-up exceptions counter
						if (ExceptionOccurs > 0) ExceptionOccurs = 0;

						if (CurrentRunStatus == RunStatus.STEPSTATEMENT)
						{
							DisableScriptEditing(false);
							DisableStepsAndChangeRun(false, true);
						}

						_isExceptionalState = false;
						CurrentRunStatus = CurrentRunStatus;
						_signalEvent.Reset();
						IsFirstRun = false;

						_requireUserInteruption = false;
					}
					catch (SqlException)
					{
						CurrentRunStatus = RunStatus.ERROR;
						_isExceptionalState = true;
						IsFirstRun = false;
						_requireUserInteruption = true;
						retry++;
						throw;
					}
					catch (Exception)
					{
						CurrentRunStatus = RunStatus.ERROR;
						IsFirstRun = false;
						_requireUserInteruption = true;
						retry++;
						throw;
					}
				}//end for

				_SQLToBeExecuted = null;
				_SqlPartIndex = -1;
			}// end if _currentStatement != null
		}

		private string DisplayScriptsToBeExecuted(int i)
		{
			string sql = GetRidOfGoStatement(_SQLToBeExecuted[i]);
			txtScriptToRun.ExecAction(() =>
			{
				txtScriptToRun.Clear();
				txtScriptToRun.AppendText(sql);
			});

			return sql;
		}

		private void ExecuteSql(string sql)
		{
			if (!string.IsNullOrEmpty(sql) && !string.IsNullOrWhiteSpace(sql))
			{
				using (var command = _sqlConnection.CreateCommand())
				{
					if (sql.IndexOf("select", StringComparison.OrdinalIgnoreCase) != -1 &&
						sql.IndexOf("@@version", StringComparison.OrdinalIgnoreCase) != -1 &&
						StateContainer.Instance.GetConcreteInstance<DbSetupState>().DatabaseSetupType == DbSetupType.New)
					{
						command.CommandText = sql;
						object data = command.ExecuteScalar();
						if (data != null && data is string)
						{
							txtExecutionLog.ExecAction(() =>
							{
								string msg = string.Format("{0} {1}", data as string, Environment.NewLine);
								txtExecutionLog.AppendText(msg);
								Log.Instance.Info(msg);
							});
						}
						return;
					}

					string ctrlSql = null;
					txtScriptToRun.ExecAction(() => ctrlSql = txtScriptToRun.Text);
					if (!string.IsNullOrEmpty(ctrlSql) &&
						string.Compare(sql, ctrlSql, StringComparison.OrdinalIgnoreCase) != 0)
						command.CommandText = ctrlSql;
					else
						command.CommandText = sql;
					//add some trick , default command timeout was 30 secs.
					command.CommandTimeout = sqlCommandTimeout;
					int rowsAffected = command.ExecuteNonQuery();
				}
				Thread.Sleep(scriptSleepTimeout);
			}//end if
			else
				Log.Instance.Warn("Sql string is empty");
		}

		private string GetRidOfGoStatement(string sql)
		{
			StringBuilder sb = new StringBuilder();

			using (var sr = new System.IO.StringReader(sql))
			{
				string line = string.Empty;
				bool isGoFound = false;
				while ((line = sr.ReadLine()) != null)
				{
					if (line.ContainsOnly("GO"))
					{
						isGoFound = true;
						continue;
					}
					else if (isGoFound && line.StartsWithIgnoreSpaces(Environment.NewLine))
					{
						isGoFound = false;
						continue;
					}

					sb.AppendLine(line);
				}
				sql = sb.ToString();
				sb.Clear();
				sb = null;
			}
			return sql;
		}

		#endregion workflow execution

		#region Helpers

		private List<IDataStatement> GenerateStatements()
		{
			this.ExecAction(() => groupBox1.Enabled = false);
			return StateContainer.Instance.GetConcreteInstance<RunScriptState>().StatementFactory.Value.Generate();
		}

		private string GetSetupSQLScripts()
		{
			StringBuilder buffer = new StringBuilder();
			States.DbSetupType setupType = (States.StateContainer.Instance[3] as States.DbSetupState).DatabaseSetupType;

			buffer.Append("select @@version \n GO \n");
			buffer.Append("USE master \n GO \n");

			string DbName = string.Empty;
			string dbPath = string.Empty;
			string logPath = string.Empty;
			int dbSize = 0, dbGrowthSize = 0, logSize = 0, logGrowthSize = 0;

			var settingsDB = States.StateContainer.Instance.GetConcreteInstance<States.DbSetupState>();
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

				if (StateContainer.Instance.GetConcreteInstance<SqlServerReportState>().SQLVersion.IndexOf("2012", StringComparison.Ordinal) == -1)
				{
					ChangePairValue(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
									x => x.SettingKey.Equals("SelectIntoBulkCopy", StringComparison.OrdinalIgnoreCase)));
					buffer.Append("exec sp_dboption " + DbName + ", 'select into/bulkcopy', " + (pair == null ? "false" : pair.SettingValue));
					buffer.Append("\nGO\n");

					ChangePairValue(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
												x => x.SettingKey.Equals("ColumnsNullByDefault", StringComparison.OrdinalIgnoreCase)));
					buffer.Append("exec sp_dboption " + DbName + ", 'ANSI null default', " + (pair == null ? "false" : pair.SettingValue));
					buffer.Append("\nGO\n");

					ChangePairValue(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
												x => x.SettingKey.Equals("TruncateLogOnCheckpoint", StringComparison.OrdinalIgnoreCase)));
					buffer.Append("exec sp_dboption " + DbName + ", 'trunc. log on chkpt.', " + (pair == null ? "false" : pair.SettingValue));
					buffer.Append("\nGO\n");

					ChangePairValue(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
																						x => x.SettingKey.Equals("SingleUser", StringComparison.OrdinalIgnoreCase)));
					buffer.Append("exec sp_dboption " + DbName + ", 'single user', " + (pair == null ? "false" : pair.SettingValue));
					buffer.Append("\nGO\n");

					ChangePairValue(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
																						x => x.SettingKey.Equals("DBOUseOnly", StringComparison.OrdinalIgnoreCase)));
					buffer.Append("exec sp_dboption " + DbName + ", 'dbo use only', " + (pair == null ? "false" : pair.SettingValue));
					buffer.Append("\nGO\n");

					ChangePairValue(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
																					x => x.SettingKey.Equals("ReadOnly", StringComparison.OrdinalIgnoreCase)));
					buffer.Append("exec sp_dboption " + DbName + ", 'read only', " + (pair == null ? "false" : pair.SettingValue));
					buffer.Append("\nGO\n");

					buffer.Append("USE " + DbName); buffer.Append("\nGO\n");

					ChangePairValueOnOff(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
													x => x.SettingKey.Equals("Arithabort", StringComparison.OrdinalIgnoreCase)));

					buffer.AppendFormat("SET ARITHABORT {0} \n GO \n", pair == null ? "ON" : pair.SettingValue);

					ChangePairValueOnOff(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
													x => x.SettingKey.Equals("quoted_identifier", StringComparison.OrdinalIgnoreCase)));

					buffer.AppendFormat("SET QUOTED_IDENTIFIER {0}", pair == null ? "OFF" : pair.SettingValue);
					buffer.Append("\nGO\n");

					ChangePairValueOnOff(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
													x => x.SettingKey.Equals("ansi_nulls", StringComparison.OrdinalIgnoreCase)));

					buffer.AppendFormat("SET ANSI_NULLS {0}", pair == null ? "OFF" : pair.SettingValue);
					buffer.Append("\nGO\n");
				}

				#endregion in case when SQL Server version is less then SQL Server 2012

				#region in case when SQL Server version is equals SQL Server 2012

				else
				{
					pair = settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(x => x.SettingKey.Equals(
																											"SelectIntoBulkCopy", StringComparison.OrdinalIgnoreCase));
					buffer.Append(string.Format("ALTER DATABASE {0} SET RECOVERY {1} \nGO\n", DbName,
						(pair == null ? "SIMPLE" : pair.SettingValue.IndexOf("yes", StringComparison.OrdinalIgnoreCase) == -1 ? "SIMPLE" : "FULL")));

					pair = settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
									x => x.SettingKey.Equals("ColumnsNullByDefault", StringComparison.OrdinalIgnoreCase));
					buffer.Append(string.Format("ALTER DATABASE {0} SET ANSI_NULL_DEFAULT {1} \nGO\n", DbName,
						(pair == null ? "ON" : pair.SettingValue.IndexOf("yes", StringComparison.OrdinalIgnoreCase) == -1 ? "OFF" : "ON")));

					pair = settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(x => x.SettingKey.Equals(
																											"TruncateLogOnCheckpoint", StringComparison.OrdinalIgnoreCase));
					buffer.Append(string.Format("ALTER DATABASE {0} SET RECOVERY {1} \nGO\n", DbName,
						(pair == null ? "FULL" : pair.SettingValue.IndexOf("yes", StringComparison.OrdinalIgnoreCase) == -1 ? "FULL" : "SIMPLE")));

					pair = settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(x => x.SettingKey.Equals(
																											"SingleUser", StringComparison.OrdinalIgnoreCase));
					buffer.Append(string.Format("ALTER DATABASE {0} SET {1} \nGO\n", DbName,
						(pair == null ? "SIMPLE" : pair.SettingValue.IndexOf("yes", StringComparison.OrdinalIgnoreCase) == -1 ? "SINGLE_USER" : "MULTI_USER")));

					pair = settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(x => x.SettingKey.Equals(
																											"DBOUseOnly", StringComparison.OrdinalIgnoreCase));
					buffer.Append(string.Format("ALTER DATABASE {0} SET {1} \nGO\n", DbName,
						(pair == null ? "MULTI_USER" : pair.SettingValue.IndexOf("yes", StringComparison.OrdinalIgnoreCase) == -1 ? "MULTI_USER" : "RESTRICTED_USER")));

					pair = settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(x => x.SettingKey.Equals(
																					"ReadOnly", StringComparison.OrdinalIgnoreCase));
					buffer.Append(string.Format("ALTER DATABASE {0} SET {1} \nGO\n", DbName,
						(pair == null ? "READ_WRITE" : pair.SettingValue.IndexOf("yes", StringComparison.OrdinalIgnoreCase) == -1 ? "READ_WRITE" : "READ_ONLY")));

					buffer.Append("USE " + DbName + "\nGO\n");

					ChangePairValueOnOff(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
													x => x.SettingKey.Equals("Arithabort", StringComparison.OrdinalIgnoreCase)));

					buffer.AppendFormat("SET ARITHABORT {0} \n GO \n", pair == null ? "ON" : pair.SettingValue);

					ChangePairValueOnOff(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
													x => x.SettingKey.Equals("quoted_identifier", StringComparison.OrdinalIgnoreCase)));

					buffer.AppendFormat("SET QUOTED_IDENTIFIER {0}", pair == null ? "OFF" : pair.SettingValue);
					buffer.Append("\nGO\n");

					ChangePairValueOnOff(ref pair, settingsDB.DatabaseConfiguration.Children.OfType<SettingsPair>().FirstOrDefault(
													x => x.SettingKey.Equals("ansi_nulls", StringComparison.OrdinalIgnoreCase)));

					buffer.AppendFormat("SET ANSI_NULLS {0}", pair == null ? "OFF" : pair.SettingValue);
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
			if (pair.SettingValue.IndexOf("yes", StringComparison.OrdinalIgnoreCase) != -1 ||
				pair.SettingValue.IndexOf("no", StringComparison.OrdinalIgnoreCase) != -1)
				pair.SettingValue = (pair.SettingValue.IndexOf("yes", StringComparison.OrdinalIgnoreCase) != -1 ? "true" : "false");

			changedPair = pair;
		}

		private void ChangePairValueOnOff(ref SettingsPair changedPair, SettingsPair pair)
		{
			if (pair == null)
			{
				changedPair = pair;
				return;
			}
			if (pair.SettingValue.IndexOf("on", StringComparison.OrdinalIgnoreCase) != -1 ||
				pair.SettingValue.IndexOf("off", StringComparison.OrdinalIgnoreCase) != -1)
				pair.SettingValue = (pair.SettingValue.IndexOf("on", StringComparison.OrdinalIgnoreCase) != -1 ? "on" : "off");

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
				catch (Exception ex) { if (ex != null) { } }
			}
		}

		#endregion Helpers

		private void WizardRunScriptControl_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Control && e.KeyCode == Keys.N)
			{
				ProceedNextStep();
			}
		}
	}
}