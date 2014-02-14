using DBSetup.Common.Helpers;
using DBSetup.Common.ModelBuilder;
using DBSetup.Common.Models;
using DBSetup.Common.Statements;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DBSetup.Common.Services
{
	public sealed class NoUIExecutor : IExecutor
	{
		internal enum DbSetupType : int
		{
			New = 0,
			Upgrade,
			LoadSetup
		}

		private const string _dateTimeFormat = "dd-MM-yyyy hh:mm:ss";
		private const string _FinalSection = "Finalization";
		private const string msg1 = "configuration script and user entered data ({0} of {1})";
		private const int scriptSleepTimeout = 35; //delay after script ends it's execution (ms)

		private CompositionContainer _container;
		private IGlobalState _global;
		private ISqlConnectionSettings _sqlSettings;

		[Export(typeof(string))]
		internal string _pathToINI { get; private set; }

		#region user settings
		private string _sqlServer;
		private string _sqlUser;
		private string _sqlPassword;
		private string _sqlServerVesrion;
		private string _sqlDbName;
		private string _DbVersion;
		private bool _isComm4Exists = false;

		private string _setupLanguage;
		private string _setupType;

		//stored procedures names
		private string _spHealthSystem;
		private string _spUser;
		//final step variables
		private string _healthSystem;
		private string _siteName;
		private string _workflowName;
		private string _populateUser;
		private string _populatePassword;
		#endregion

		private List<SectionBase> _parsingResult;
		private Language _currentLanguage;

		private SectionBase _dbSettings;

		[Export(typeof(SectionBase))]
		internal SectionBase _currentSetup { get; private set; }

		private DbSetupType _currentSetupType;

		private IVersionService versionService;
		private SetupScriptDocument _scriptDocument;

		[Import(typeof(IDataStatementFactory))]
		public Lazy<IDataStatementFactory> StatementFactory { get; set; }

		private List<IDataStatement> _dataStatements;

		private int _latestComm4Version = -1;
		private volatile uint _dbIsUpToDate = 0;

		#region database settings
		private bool _overwriteDB = false;

		private string _dbFileName;
		private string _initialSize;
		private string _growth;
		private string _logInitialSize;
		private string _logGrowth;
		private string _dbLocation, _logLocation;
		private string _logName;
		#endregion

		public NoUIExecutor()
		{
			_cts = new CancellationTokenSource();
		}

		public void SetParameters(string config)
		{
			if (string.IsNullOrEmpty(config))
				throw new ArgumentException("config");

			XDocument xmlDoc = XDocument.Parse(config, LoadOptions.None);

			_pathToINI = xmlDoc.Root.Element("INI").Attribute("path").Value;

			_sqlDbName = xmlDoc.Root.Element("SqlSever").Element("Database").Value;
			_sqlServer = xmlDoc.Root.Element("SqlSever").Element("Server").Value;
			_sqlUser = xmlDoc.Root.Element("SqlSever").Element("User").Value;
			_sqlPassword = xmlDoc.Root.Element("SqlSever").Element("Password").Value;

			_sqlSettings = new SqlConnectionSettings(_sqlServer, _sqlDbName, _sqlUser, _sqlPassword);
			_global = ServiceLocator.Instance.GetService<IGlobalState>();
			_global.SetState<string>("rootPath", Path.GetDirectoryName(_pathToINI));

			_setupLanguage = xmlDoc.Root.Element("Setup").Element("Language").Value;
			_setupType = xmlDoc.Root.Element("Setup").Element("Type").Value;
			_overwriteDB = xmlDoc.Root.Element("Setup").Element("Overwrite") == null ? false :
				xmlDoc.Root.Element("Setup").Element("Overwrite").Value.IndexOf("y", StringComparison.OrdinalIgnoreCase) != -1 ? true : false;

			_spHealthSystem = xmlDoc.Root.Element("Procedures").Element("spHealthSystem").Value;
			_spUser = xmlDoc.Root.Element("Procedures").Element("spUser").Value;

			_healthSystem = xmlDoc.Root.Element("Final").Element("HealthName").Value;
			_siteName = xmlDoc.Root.Element("Final").Element("SiteName").Value;
			_workflowName = xmlDoc.Root.Element("Final").Element("Workflow").Value;

			_populateUser = xmlDoc.Root.Element("Final").Element("User").Value;
			_populatePassword = xmlDoc.Root.Element("Final").Element("Password").Value;

			xmlDoc.RemoveNodes();
			xmlDoc = null;
		}

		public void Execute()
		{
			var strBuilder = new SqlConnectionStringBuilder();
			strBuilder.DataSource = _sqlServer;
			strBuilder.UserID = _sqlUser;
			strBuilder.Password = _sqlPassword;

			if (!CheckServerConnection(strBuilder))
				throw new ApplicationException("Fail to establish connection with SQL server: {0}{1}User name: {2}, password: {3}".FormatWith(
													_sqlServer, Environment.NewLine, _sqlUser, _sqlPassword));

			RetreiveVersionAndDbVersion(strBuilder);

			BuildObjectsTree();

			if (_dbIsUpToDate == 1 && !_overwriteDB)
			{
				Log.Instance.Warn("Your database is up to date.{0}Version is {1}".FormatWith(Environment.NewLine, _DbVersion));
				return;
			}
			else if (_dbIsUpToDate == 1 && _overwriteDB)
				Log.Instance.Warn("Begin overwriting the database.");

			PrepareFolders();

			if (_currentSetup != null)
			{
				BuildExecutionTree();

				RunScript(strBuilder);
			}
		}

		#region internal implementation
		private bool CheckServerConnection(SqlConnectionStringBuilder conBuilder)
		{
			return SqlServerHelper.CanOpen(conBuilder.ToString());
		}

		private void RetreiveVersionAndDbVersion(SqlConnectionStringBuilder strBuilder)
		{
			var version = SqlServerHelper.GetSQLServerVersion(strBuilder.ToString());
			var versionStr = SqlServerHelper.GetSQLVersionByCommand(strBuilder.ToString());
			if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(versionStr))
			{
				Log.Instance.Warn("Fail to retreive SQL Server version"); return;
			}


			_sqlServerVesrion = versionStr.Substring(0, versionStr.IndexOf('\n') + 1);
			Log.Instance.Info(string.Format("SQL Server version: {0}", _sqlServerVesrion));

			_isComm4Exists = SqlServerHelper.CheckDatabaseExists(strBuilder.ToString(), _sqlDbName);//getting db version number from version table.
			if (_isComm4Exists)
			{
				Log.Instance.Info("Comm4 exists: Yes");
				int dbVer = SqlServerHelper.GetDbVersionFromVesrsionTable(strBuilder.ToString(), _sqlDbName);
				_DbVersion = (dbVer == -1 ? "Undefined" : dbVer.ToString());
				Log.Instance.Info(string.Format("Comm4 version is: {0}", _DbVersion));
			}
		}

		private void BuildObjectsTree()
		{
			ModelBuilderContext context = new ModelBuilderContext();
			FullModelBuilder builder = new FullModelBuilder();
			builder.OpenFile(_pathToINI);
			context.SetBuilder(builder);

			var loadTask = Task.Factory.StartNew(() =>
			{
				versionService = ServiceLocator.Instance.GetService<Common.IVersionService>();
				string versionFile = System.Configuration.ConfigurationManager.AppSettings["versionControl"];
				//retrieve latest version
				versionService.SetSource(Path.Combine(Path.GetDirectoryName(_pathToINI), versionFile));
				_latestComm4Version = versionService.RetrieveVersion();

				_parsingResult = context.ExecuteBuild();

				//read languages section from object tree
				var sectionLanguages = _parsingResult.FirstOrDefault(x => x.Text.Equals("Languages", StringComparison.OrdinalIgnoreCase));
				if (sectionLanguages != null)
				{
					var languages = sectionLanguages.Children.OfType<Language>().ToList();
					if (languages != null && languages.Count == 1)
						_currentLanguage = languages[0];

					else if (!string.IsNullOrEmpty(_setupLanguage) && languages != null && languages.Count > 1)
					{
						_currentLanguage = languages.Where(x => x.LanguageName.Equals(_setupLanguage, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
						if (_currentLanguage == null)
							throw new InvalidOperationException("Fail to retreive appropriate language from settings file.");
					}
					else
					{
						string msg = "Fail to resolve issue with languages";
						Log.Instance.Error(msg);
						throw new InvalidOperationException(msg);
					}
				}
				else
				{
					string msg = "Fail to find section [Languages]";
					Log.Instance.Error(msg);
					throw new InvalidOperationException(msg);
				}

				var sectionsSetupConfig = _parsingResult.Where(x => x.Text.IndexOf("Setup Configurations", StringComparison.OrdinalIgnoreCase) != -1).ToList();
				if (sectionsSetupConfig != null && sectionsSetupConfig.Count > 0)
				{
					var setupForLang = sectionsSetupConfig.FirstOrDefault(x => x.Text.IndexOf(_currentLanguage.LanguageName, StringComparison.OrdinalIgnoreCase) != -1);
					if (setupForLang == null)
					{
						string msg = "Fail to retrieve setup configuration for language {0}".FormatWith(_currentLanguage.LanguageName);
						Log.Instance.Error(msg);
						throw new InvalidOperationException(msg);
					}

					bool isUpToDate;
					RetrieveUpgradeTypeIfNeed(setupForLang, out isUpToDate);
					if (isUpToDate)
						_dbIsUpToDate = 1;
				}
				else
				{
					string msg = "Fail to retrieve setup configuration from config file {0}".FormatWith(_pathToINI);
					Log.Instance.Error(msg);
					throw new InvalidOperationException(msg);
				}

				var unusedSection = _parsingResult.FirstOrDefault(x => x.Text.Equals("Unused Sections", StringComparison.OrdinalIgnoreCase));
				if (unusedSection != null)
				{
					//read database configuration settings
					_dbSettings = unusedSection.Children.FirstOrDefault(x => x.Text.Equals("Database Configurations", StringComparison.OrdinalIgnoreCase));
					if (_dbSettings != null)
						InitializaDatabaseSettings(_dbSettings);
					else
					{
						string msg = "Fail to found section [Database Configurations]";
						Log.Instance.Error(msg);
						throw new InvalidOperationException(msg);
					}
				}
			});

			try
			{
				loadTask.Wait();
			}
			catch (Exception ex)
			{
				if (ex is AggregateException)
					Log.Instance.Error("Error has been occurred while building object model", (ex as AggregateException).Flatten().InnerException);
				else
					Log.Instance.Error("Error has been occurred while building object model", ex);
				throw;
			}
		}

		private void InitializaDatabaseSettings(SectionBase databaseConfig)
		{
			if (databaseConfig == null)
				throw new ArgumentNullException("databaseConfig");

			var dbName = databaseConfig.Children.OfType<SettingsPair>().FirstOrDefault(x => x.Key.Equals("Name", StringComparison.OrdinalIgnoreCase));
			_dbFileName = dbName == null ? string.Empty : dbName.Value;

			var dbSize = databaseConfig.Children.OfType<SettingsPair>().FirstOrDefault(x => x.Key.Equals("DataDeviceSize", StringComparison.OrdinalIgnoreCase));
			_initialSize = dbSize == null ? string.Empty : dbSize.Value;

			int size;
			if (Int32.TryParse(_initialSize, out size))
			{
				_growth = (size + 1).ToString();
			}

			var logSize = databaseConfig.Children.OfType<SettingsPair>().FirstOrDefault(x => x.Key.Equals("LogDeviceSize", StringComparison.OrdinalIgnoreCase));
			_logInitialSize = logSize == null ? string.Empty : logSize.Value;

			if (Int32.TryParse(_logInitialSize, out size))
			{
				if (size == 1)
					_logGrowth = "1";
				else
					_logGrowth = (size - 1).ToString();
			}

			var dataDevice = databaseConfig.Children.OfType<SettingsPair>().FirstOrDefault(x => x.Key.Equals("DataDevice", StringComparison.OrdinalIgnoreCase));
			_dbLocation = dataDevice == null ? string.Empty : System.IO.Path.ChangeExtension(dataDevice.Value, "mdf");

			var logDevice = databaseConfig.Children.OfType<SettingsPair>().FirstOrDefault(x => x.Key.Equals("LogDevice", StringComparison.OrdinalIgnoreCase));
			_logLocation = logDevice == null ? string.Empty : System.IO.Path.ChangeExtension(logDevice.Value, "ldf");

			if (!string.IsNullOrEmpty(_logLocation))
				_logName = System.IO.Path.GetFileNameWithoutExtension(_logLocation);
		}

		private void PrepareFolders()
		{
			if (!string.IsNullOrEmpty(_dbLocation) && !Directory.Exists(Path.GetDirectoryName(_dbLocation)))
			{
				var creationTask = Task.Factory.StartNew(() => Directory.CreateDirectory(Path.GetDirectoryName(_dbLocation)));
				creationTask.ContinueWith(prevTask =>
				{
					Log.Instance.Warn(string.Format("Fail to to create directory: {0} {1} {2}", Path.GetDirectoryName(_dbLocation), Environment.NewLine
											, prevTask.Exception));

					prevTask.Dispose();
				}, TaskContinuationOptions.NotOnRanToCompletion);

				creationTask.ContinueWith(prevTask =>
				{
					Log.Instance.Info(string.Format("Directory has been successfully created: {0}", Path.GetDirectoryName(_dbLocation)));
					prevTask.Dispose();
				}, TaskContinuationOptions.OnlyOnRanToCompletion);
			}

			if (!string.IsNullOrEmpty(_logLocation) && !Directory.Exists(Path.GetDirectoryName(_logLocation)))
			{
				var creationTask = Task.Factory.StartNew(() => Directory.CreateDirectory(Path.GetDirectoryName(_logLocation)));
				creationTask.ContinueWith(prevTask =>
				{
					Log.Instance.Warn(string.Format("Fail to to create directory: {0} {1} {2}", Path.GetDirectoryName(_logLocation), Environment.NewLine
											, prevTask.Exception));

					prevTask.Dispose();
				}, TaskContinuationOptions.NotOnRanToCompletion);

				creationTask.ContinueWith(prevTask =>
				{
					Log.Instance.Info(string.Format("Directory has been successfully created: {0}", Path.GetDirectoryName(_logLocation)));
					prevTask.Dispose();
				}, TaskContinuationOptions.OnlyOnRanToCompletion);
			}
		}

		private void RetrieveUpgradeTypeIfNeed(SectionBase changedSetupConfig, out bool upToDate)
		{
			upToDate = false;
			if (changedSetupConfig == null) return;

			// Check if comm4 is exists in case of true check appropriate upgrade value
			try
			{
				int comm4Version;
				// Retrieve comm4 version in int format
				if (_isComm4Exists && Int32.TryParse(_DbVersion, out comm4Version))
				{
					if (comm4Version == _latestComm4Version)
					{
						upToDate = true;
						if (_overwriteDB)
						{
							SectionBase newSetup = Common.Helpers.SectionBaseExtension.RetreiveNewSetup(changedSetupConfig.Children);
							if (newSetup == null)
							{
								string msg = "Fail to get proper setup type forom config file.";
								Log.Instance.Error(msg);
								throw new InvalidOperationException(msg);
							}
							else
							{
								Log.Instance.Info("Database is set to be overwritten.");
								_currentSetup = newSetup;
								_currentSetupType = DbSetupType.New;
								_global.SetState<SectionBase>("setupType", newSetup);
							}
						}
						return;
					}

					//get proper upgrade setup type based on comm4 version
					var upgradeType = Common.Helpers.SectionBaseExtension.GetProperUpgrade(changedSetupConfig.Children, comm4Version);
					if (upgradeType == null)
					{
						string msg = string.Format("Fail to get proper upgrade type for DB version {0}", _DbVersion);
						Log.Instance.Error(msg);
						throw new InvalidOperationException(msg);
					}
					else
					{
						Log.Instance.Info("Proper upgrade type has been successfully found.");
						_currentSetup = upgradeType;
						_global.SetState<SectionBase>("setupType", upgradeType);
						_currentSetupType = DbSetupType.Upgrade;
					}
				}
				else if (!_isComm4Exists)
				{
					var newSetup = Common.Helpers.SectionBaseExtension.RetreiveNewSetup(changedSetupConfig.Children);
					if (newSetup == null)
					{
						string msg = "Fail to get proper new setup type forom config file.";
						Log.Instance.Error(msg);
						throw new InvalidOperationException(msg);
					}
					else
					{
						Log.Instance.Info("New setup has been successfully found.");
						_currentSetup = newSetup;
						_global.SetState<SectionBase>("setupType", newSetup);
						_currentSetupType = DbSetupType.New;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Instance.Error("Fail to get proper upgrade type", ex);
				throw;
			}
		}

		private void BuildExecutionTree()
		{
			ScriptConsequencyBuilder builder = new ScriptConsequencyBuilder();
			var task = builder.AsyncBuild(_currentSetup);

			try
			{
				task.Wait();
			}
			catch (Exception ex)
			{
				Log.Instance.Error("AsyncBuild", ex);
				throw;
			}
			finally
			{
				task.Dispose();
			}

			_scriptDocument = task.Result;
			Log.Instance.Info(_scriptDocument.GetDocumentText());
		}
		#endregion

		#region Scripts execution logic
		private readonly CancellationTokenSource _cts; // cancellation source
		private Task _scriptsExecutor; //main execution thread

		private void RunScript(SqlConnectionStringBuilder strBuilder)
		{
			ComposeParts();
			var mainTask = Task.Factory.StartNew<List<IDataStatement>>(() =>
			{
				if (StatementFactory.Value != null)
					return StatementFactory.Value.Generate();

				return new List<IDataStatement>();
			});

			try
			{
				mainTask.Wait();
				_dataStatements = mainTask.Result;
				Log.Instance.Info("Sql statements have been successfully generated.");
			}
			catch (Exception ex)
			{
				if (ex is AggregateException)
					Log.Instance.Error("RunScript", (ex as AggregateException).Flatten().InnerException);
				else
					Log.Instance.Error("RunScript", ex);
				throw;
			}
			finally
			{
				_container.Dispose();
				_container = null;
			}

			int indx = msg1.IndexOf("(");
			string setupText = msg1.Substring(0, indx);
			SectionBase section = new SqlLink() { Text = setupText, FileName = setupText };
			section.Handler = new SQLSectionHandler();
			IDataStatement newSQl = new SqlDataStatement(GetSetupSQLScripts(), setupText);
			newSQl.ContentRoot = section;
			_dataStatements.Insert(0, newSQl);

			_scriptsExecutor = new Task(ExecuteWorkflow, strBuilder, TaskCreationOptions.LongRunning);
			_scriptsExecutor.Start();

			try
			{
				_scriptDocument.Clear();
				_scriptDocument = null;
				_scriptsExecutor.Wait();

				ProcessFinalSteps(strBuilder);

				Log.Instance.Info("RunScript has been completed.");
				//GC.RegisterForFullGCNotification(10, 10); GC.CancelFullGCNotification();
			}
			catch (Exception ex)
			{
				if (ex is AggregateException)
					Log.Instance.Error("ExecuteWorkflow", (ex as AggregateException).Flatten().InnerException);
				else
					Log.Instance.Error("ExecuteWorkflow", ex);
				throw;
			}
			finally
			{
				_scriptsExecutor.Dispose();
				_scriptsExecutor = null;
			}
		}

		private void ComposeParts()
		{
			try
			{
				using (AggregateCatalog agCatalog = new AggregateCatalog())
				{
					agCatalog.Catalogs.Add(new AssemblyCatalog(typeof(Common.IDataStatementFactory).Assembly));
					_container = new CompositionContainer(agCatalog);
					_container.ComposeParts(this);
				}
			}
			catch (Exception ex)
			{
				Log.Instance.Fatal("Fail to compose parts", ex);
				throw;
			}
		}

		private string GetSetupSQLScripts()
		{
			StringBuilder buffer = new StringBuilder();
			buffer.Append("select @@version \n GO \n");
			buffer.Append("USE master \n GO \n");

			string DbName = string.Empty;
			string dbPath = string.Empty;
			string logPath = string.Empty;
			int dbSize = 0, dbGrowthSize = 0, logSize = 0, logGrowthSize = 0;


			DbName = System.IO.Path.GetFileNameWithoutExtension(_dbFileName);
			dbPath = _dbLocation;

			int.TryParse(_initialSize, out  dbSize);
			int.TryParse(_growth, out dbGrowthSize);

			logPath = _logLocation;
			int.TryParse(_logInitialSize, out logSize);
			int.TryParse(_logGrowth, out logGrowthSize);

			if (DbSetupType.New == _currentSetupType)
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

			if (_currentSetupType == DbSetupType.New)
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

				buffer.Append("    FILEGROWTH=" + dbGrowthSize.ToString() + /*(settingsDB.DbGrowthType == States.GrowthType.MB ? "MB" : "%")*/ "MB" + ")\n");

				buffer.Append("LOG ON ( \n");
				if (_logName.Length > 0)
					buffer.Append("    NAME=" + System.IO.Path.GetFileNameWithoutExtension(_logName) + ",\n");
				if (logPath.Length > 0)
					buffer.Append("    FILENAME='" + logPath.ToString() + "',\n");
				if (logSize > 0)
					buffer.Append("    SIZE=" + logSize.ToString() + "MB,\n");

				buffer.Append("    FILEGROWTH=" + logGrowthSize.ToString() + "MB"  /*(settingsDB.LogGrowthType == States.GrowthType.MB ? "MB" : "%")*/ + ")\n");
				buffer.Append("GO \n");
			}
			else if (_currentSetupType == DbSetupType.LoadSetup)
			{

			}

			// for all
			if (((DbSetupType.LoadSetup == _currentSetupType) ||
				 (DbSetupType.New == _currentSetupType) ||
				 (DbSetupType.Upgrade == _currentSetupType))
				 &&
				 (_dbSettings != null && _dbSettings.Children != null))
			{
				SettingsPair pair = new SettingsPair();

				#region in case when SQL Server version is less then SQL Server 2012

				if (_sqlServerVesrion.IndexOf("2012", StringComparison.Ordinal) == -1)
				{
					ChangePairValue(ref pair, _dbSettings.Children.OfType<SettingsPair>().FirstOrDefault(
									x => x.Key.Equals("SelectIntoBulkCopy", StringComparison.OrdinalIgnoreCase)));
					buffer.Append("exec sp_dboption " + DbName + ", 'select into/bulkcopy', " + (pair == null ? "false" : pair.Value));
					buffer.Append("\nGO\n");

					ChangePairValue(ref pair, _dbSettings.Children.OfType<SettingsPair>().FirstOrDefault(
												x => x.Key.Equals("ColumnsNullByDefault", StringComparison.OrdinalIgnoreCase)));
					buffer.Append("exec sp_dboption " + DbName + ", 'ANSI null default', " + (pair == null ? "false" : pair.Value));
					buffer.Append("\nGO\n");

					ChangePairValue(ref pair, _dbSettings.Children.OfType<SettingsPair>().FirstOrDefault(
												x => x.Key.Equals("TruncateLogOnCheckpoint", StringComparison.OrdinalIgnoreCase)));
					buffer.Append("exec sp_dboption " + DbName + ", 'trunc. log on chkpt.', " + (pair == null ? "false" : pair.Value));
					buffer.Append("\nGO\n");

					ChangePairValue(ref pair, _dbSettings.Children.OfType<SettingsPair>().FirstOrDefault(
																						x => x.Key.Equals("SingleUser", StringComparison.OrdinalIgnoreCase)));
					buffer.Append("exec sp_dboption " + DbName + ", 'single user', " + (pair == null ? "false" : pair.Value));
					buffer.Append("\nGO\n");

					ChangePairValue(ref pair, _dbSettings.Children.OfType<SettingsPair>().FirstOrDefault(
																						x => x.Key.Equals("DBOUseOnly", StringComparison.OrdinalIgnoreCase)));
					buffer.Append("exec sp_dboption " + DbName + ", 'dbo use only', " + (pair == null ? "false" : pair.Value));
					buffer.Append("\nGO\n");

					ChangePairValue(ref pair, _dbSettings.Children.OfType<SettingsPair>().FirstOrDefault(
																					x => x.Key.Equals("ReadOnly", StringComparison.OrdinalIgnoreCase)));
					buffer.Append("exec sp_dboption " + DbName + ", 'read only', " + (pair == null ? "false" : pair.Value));
					buffer.Append("\nGO\n");

					buffer.Append("USE " + DbName); buffer.Append("\nGO\n");

					ChangePairValueOnOff(ref pair, _dbSettings.Children.OfType<SettingsPair>().FirstOrDefault(
													x => x.Key.Equals("Arithabort", StringComparison.OrdinalIgnoreCase)));

					buffer.AppendFormat("SET ARITHABORT {0} \n GO \n", pair == null ? "ON" : pair.Value);

					ChangePairValueOnOff(ref pair, _dbSettings.Children.OfType<SettingsPair>().FirstOrDefault(
													x => x.Key.Equals("quoted_identifier", StringComparison.OrdinalIgnoreCase)));

					buffer.AppendFormat("SET QUOTED_IDENTIFIER {0}", pair == null ? "OFF" : pair.Value);
					buffer.Append("\nGO\n");

					ChangePairValueOnOff(ref pair, _dbSettings.Children.OfType<SettingsPair>().FirstOrDefault(
													x => x.Key.Equals("ansi_nulls", StringComparison.OrdinalIgnoreCase)));

					buffer.AppendFormat("SET ANSI_NULLS {0}", pair == null ? "OFF" : pair.Value);
					buffer.Append("\nGO\n");
				}

				#endregion in case when SQL Server version is less then SQL Server 2012

				#region in case when SQL Server version is equals SQL Server 2012

				else
				{
					pair = _dbSettings.Children.OfType<SettingsPair>().FirstOrDefault(x => x.Key.Equals(
																											"SelectIntoBulkCopy", StringComparison.OrdinalIgnoreCase));
					buffer.Append(string.Format("ALTER DATABASE {0} SET RECOVERY {1} \nGO\n", DbName,
						(pair == null ? "SIMPLE" : pair.Value.IndexOf("yes", StringComparison.OrdinalIgnoreCase) == -1 ? "SIMPLE" : "FULL")));

					pair = _dbSettings.Children.OfType<SettingsPair>().FirstOrDefault(
									x => x.Key.Equals("ColumnsNullByDefault", StringComparison.OrdinalIgnoreCase));
					buffer.Append(string.Format("ALTER DATABASE {0} SET ANSI_NULL_DEFAULT {1} \nGO\n", DbName,
						(pair == null ? "ON" : pair.Value.IndexOf("yes", StringComparison.OrdinalIgnoreCase) == -1 ? "OFF" : "ON")));

					pair = _dbSettings.Children.OfType<SettingsPair>().FirstOrDefault(x => x.Key.Equals(
																											"TruncateLogOnCheckpoint", StringComparison.OrdinalIgnoreCase));
					buffer.Append(string.Format("ALTER DATABASE {0} SET RECOVERY {1} \nGO\n", DbName,
						(pair == null ? "FULL" : pair.Value.IndexOf("yes", StringComparison.OrdinalIgnoreCase) == -1 ? "FULL" : "SIMPLE")));

					pair = _dbSettings.Children.OfType<SettingsPair>().FirstOrDefault(x => x.Key.Equals(
																											"SingleUser", StringComparison.OrdinalIgnoreCase));
					buffer.Append(string.Format("ALTER DATABASE {0} SET {1} \nGO\n", DbName,
						(pair == null ? "SIMPLE" : pair.Value.IndexOf("yes", StringComparison.OrdinalIgnoreCase) == -1 ? "SINGLE_USER" : "MULTI_USER")));

					pair = _dbSettings.Children.OfType<SettingsPair>().FirstOrDefault(x => x.Key.Equals(
																											"DBOUseOnly", StringComparison.OrdinalIgnoreCase));
					buffer.Append(string.Format("ALTER DATABASE {0} SET {1} \nGO\n", DbName,
						(pair == null ? "MULTI_USER" : pair.Value.IndexOf("yes", StringComparison.OrdinalIgnoreCase) == -1 ? "MULTI_USER" : "RESTRICTED_USER")));

					pair = _dbSettings.Children.OfType<SettingsPair>().FirstOrDefault(x => x.Key.Equals(
																					"ReadOnly", StringComparison.OrdinalIgnoreCase));
					buffer.Append(string.Format("ALTER DATABASE {0} SET {1} \nGO\n", DbName,
						(pair == null ? "READ_WRITE" : pair.Value.IndexOf("yes", StringComparison.OrdinalIgnoreCase) == -1 ? "READ_WRITE" : "READ_ONLY")));

					buffer.Append("USE " + DbName + "\nGO\n");

					ChangePairValueOnOff(ref pair, _dbSettings.Children.OfType<SettingsPair>().FirstOrDefault(
													x => x.Key.Equals("Arithabort", StringComparison.OrdinalIgnoreCase)));

					buffer.AppendFormat("SET ARITHABORT {0} \n GO \n", pair == null ? "ON" : pair.Value);

					ChangePairValueOnOff(ref pair, _dbSettings.Children.OfType<SettingsPair>().FirstOrDefault(
													x => x.Key.Equals("quoted_identifier", StringComparison.OrdinalIgnoreCase)));

					buffer.AppendFormat("SET QUOTED_IDENTIFIER {0}", pair == null ? "OFF" : pair.Value);
					buffer.Append("\nGO\n");

					ChangePairValueOnOff(ref pair, _dbSettings.Children.OfType<SettingsPair>().FirstOrDefault(
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

		private SqlConnection _sqlConnection;
		private int _statementIndex = -1;
		private IDataStatement _currentStatement;
		private ISectionHandler currentHandler;

		private void ExecuteWorkflow(object state)
		{
			SqlConnectionStringBuilder builder = (state as SqlConnectionStringBuilder);
			builder.MaxPoolSize = 20;
			builder.Pooling = true;

			using (_sqlConnection = new SqlConnection(builder.ToString()))
			{
				if (_sqlConnection.State != System.Data.ConnectionState.Open && _sqlConnection.State != System.Data.ConnectionState.Connecting)
					_sqlConnection.Open();

				#region perform initial log info
				Log.Instance.Info(string.Format("Log file: {0} {1} {1}",
						System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), _logLocation), Environment.NewLine));

				string compName = string.Format("Computer: {0} {1}", Environment.MachineName, Environment.NewLine);
				Log.Instance.Info(compName);

				string startTime = string.Format("Start time: {0} {1}", DateTime.Now.ToString(_dateTimeFormat), Environment.NewLine);
				Log.Instance.Info(startTime);

				#endregion perform initial log info

				for (int i = 0; i < _dataStatements.Count; i++)
				{
					_statementIndex = i;
					_currentStatement = _dataStatements[i];
					Log.Instance.Info("Begin to execute {0}".FormatWith(_currentStatement.DataFile));

					#region new handling mechanism
					//handle DICOM sections
					if (_currentStatement.Type == StatementType.Dicom)
					{
						currentHandler = _currentStatement.ContentRoot.Handler;
						if (currentHandler != null)
						{
							currentHandler.Logger = Log.Instance;
							currentHandler.Parameters = _sqlSettings;
							currentHandler.OnErrorHandler(OnErrorHandler);

							if (currentHandler.Handle(_currentStatement.ContentRoot))
							{
								string scriptExecuted = string.Format("Executed: {0} {1}", _currentStatement.DataFile, Environment.NewLine);
								Log.Instance.Info(scriptExecuted);
							}
							else
							{
								string scriptExecuted = string.Format("Executed with error(s): {0} {1}", _currentStatement.DataFile, Environment.NewLine);
								Log.Instance.Info(scriptExecuted);
							}
						}
					}
					//handle SQL section
					else if (_currentStatement.Type == StatementType.Sql)
					{
						currentHandler = _currentStatement.ContentRoot.Handler;
						currentHandler.Logger = Log.Instance;
						currentHandler.Parameters = new Tuple<SqlConnection, IDataStatement>(_sqlConnection, _currentStatement);
						currentHandler.OnOutputReceived(OnSqlEngineOutput);
						currentHandler.OnErrorHandler(OnSqlError);

						if (currentHandler.Handle(_currentStatement.ContentRoot))
						{
							string scriptExecuted = string.Format("Executed: {0} {1}", _currentStatement.DataFile, Environment.NewLine);
							Log.Instance.Info(scriptExecuted);
						}
						else
						{
							string scriptExecuted = string.Format("Executed with error(s): {0} {1}", _currentStatement.DataFile, Environment.NewLine);
							Log.Instance.Info(scriptExecuted);
						}
					}
					#endregion

					Log.Instance.Info("End execute {0}".FormatWith(_currentStatement.DataFile));
				}// end for loop statement

				string msgEndTime = string.Format("End time: {0} {1}", DateTime.Now.ToString(_dateTimeFormat), Environment.NewLine);
				Log.Instance.Info(msgEndTime);

				try
				{
					if (_sqlConnection.State == System.Data.ConnectionState.Open &&
						_sqlConnection.State != System.Data.ConnectionState.Closed &&
								_sqlConnection.State != System.Data.ConnectionState.Broken)
						_sqlConnection.Close();
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

		private void OnSqlEngineOutput(string output)
		{
			if (output != null)
			{
				Log.Instance.Info(output);
			}
		}

		private object OnSqlError(Exception exc, object state)
		{
			string failMsg = string.Format("Fail: {0} {1} Message: {2}{1}", _currentStatement.DataFile, Environment.NewLine, exc.Message);
			Log.Instance.Error(failMsg);
			return null;
		}

		private object OnErrorHandler(Exception ex, object state)
		{
			string failMsg = string.Format("Fail: {0} {1} Message: {2}{1}", _currentStatement.DataFile, Environment.NewLine, ex.Message);
			Log.Instance.Error(failMsg);

			return null;
		}

		private void ProcessFinalSteps(SqlConnectionStringBuilder strBuilder)
		{
			strBuilder.InitialCatalog = System.IO.Path.GetFileNameWithoutExtension(_dbFileName);
			Log.Instance.Info("Processing final steps.");
			using (SqlConnection con = new SqlConnection(strBuilder.ToString()))
			{
				if (con.State == System.Data.ConnectionState.Closed)
					con.Open();

				Log.Instance.Info("Begin to invoke {0}".FormatWith(_spHealthSystem));
				SqlCommand command = null;
				using (command = con.CreateCommand())
				{
					command.CommandType = System.Data.CommandType.StoredProcedure;
					command.CommandText = _spHealthSystem;
					var @p1 = new SqlParameter("@healthsystem", System.Data.SqlDbType.VarChar);
					@p1.Value = _healthSystem;
					command.Parameters.Add(@p1);
					var @p2 = new SqlParameter("@sitename", System.Data.SqlDbType.VarChar);
					@p2.Value = _siteName;
					command.Parameters.Add(@p2);

					var @p3 = new SqlParameter("@workflow", System.Data.SqlDbType.VarChar);
					@p3.Value = _workflowName;
					command.Parameters.Add(@p3);

					int i = command.ExecuteNonQuery();
				}
				Log.Instance.Info("{0} has been successfully executed.".FormatWith(_spHealthSystem));

				Log.Instance.Info("Begin to invoke {0}".FormatWith(_spUser));
				using (command = con.CreateCommand())
				{
					command.CommandType = System.Data.CommandType.StoredProcedure;
					command.CommandText = _spUser;
					var @p1 = new SqlParameter("@psuser", System.Data.SqlDbType.VarChar);
					@p1.Value = _populateUser;
					command.Parameters.Add(@p1);
					var @p2 = new SqlParameter("@pspswd", System.Data.SqlDbType.VarChar);
					@p2.Value = _populatePassword;
					command.Parameters.Add(@p2);

					int i = command.ExecuteNonQuery();
				}
				Log.Instance.Info("{0} has been successfully executed.".FormatWith(_spUser));

				DoFinalization(con);
			}
		}

		private void DoFinalization(SqlConnection sqlCon)
		{
			Log.Instance.Info("Performing final step from INI file");
			var sectionData = IniFileParser.GetSingleSection(_pathToINI, _FinalSection);

			ObjectModelBuilder builder = new ObjectModelBuilder();
			builder.LoadSql = true; builder.LoadBLOB = true;
			var finalization = new DBSetup.Common.Models.SectionBase();
			finalization.Text = _FinalSection;
			finalization.FileName = _pathToINI;
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

			IDataStatementFactory factory = new DataStatementsFactory();
			var sqlStatements = factory.GenerateFor(finalization);
			factory = null;

			InvokeRemainsSQLs(sqlStatements, sqlCon);
			sqlStatements = null;
			finalization = null;
		}

		private void InvokeRemainsSQLs(List<Common.Statements.IDataStatement> sqlStatements, SqlConnection sqlCon)
		{
			using (SqlCommand cmd = sqlCon.CreateCommand())
			{
				cmd.CommandType = System.Data.CommandType.Text;
				cmd.CommandTimeout = 0;//unlimited command wait time
				var regex = new Regex(Environment.NewLine + "go", RegexOptions.IgnoreCase);
				foreach (var statement in sqlStatements)
				{
					//remove go statement from script because it is not tsql statement.
					string content = regex.Replace((statement as SqlDataStatement).SqlStatements, string.Empty);
					cmd.CommandText = content;
					try
					{
						if (sqlCon.State == System.Data.ConnectionState.Closed)
							sqlCon.Open();
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
		#endregion
	}
}
