using DBSetup.Common;
using DBSetup.Common.Helpers;
using DBSetup.Common.Native;
using DBSetup.Helpers;
using DBSetup.States;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DBSetup
{
	internal static class Program
	{
		private const string _msgEnd = "Application ends.";
		private const string _msgEndSuccess = "Application ends - Success.";
		private const string _msgEndFail = "Application ends - Fail.";
		internal static Nullable<bool> ISExitRequired;

		private static string[] parameters = null;

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		private static void Main()
		{
			parameters = Environment.GetCommandLineArgs();
			Application.ThreadException += Application_ThreadException;
			Application.ApplicationExit += Application_ApplicationExit;
			GUIConsoleWriter writer = null;

			if (parameters.Length > 1)
			{
				try
				{
					writer = new GUIConsoleWriter();
					writer.InitHandles();
				}
				catch (System.ComponentModel.Win32Exception ex)
				{
					Log.Instance.Warn("Fail to attach the console.{0}{1}".FormatWith(Environment.NewLine, ex));
				}
			}

			Log.Instance.Info("Application starts.");

			LogCmdParameters();

			if (parameters != null && parameters.Length >= 2)
			{
				Log.Instance.Info("Run with arguments: {0} {1}".FormatWith(parameters[1], parameters.Length == 3 ? parameters[2] : string.Empty));

				if (parameters.Contains("-f"))
				{
					CheckFilePresence();

					IExecutor executor = ServiceLocator.Instance.GetService<IExecutor>();
					if (executor != null)
					{
						string fpath = parameters[2];
						if (string.IsNullOrEmpty(fpath) || !System.IO.File.Exists(fpath))
							throw new ApplicationException("Specified command line arguments have incorrect format, see: ( -f [pathToFile])");

						try
						{
							writer.WriteLine("Begin set the automation parameters.");
							executor.SetParameters(System.IO.File.ReadAllText(fpath));

							writer.WriteLine("Begin setup the database.");
							writer.WriteLine("Wait until process has finished.{0}This can takes several minutes.".FormatWith(Environment.NewLine));
							writer.WriteLine("......");

							try
							{
								writer.Dispose();
								writer = null;
							}
							catch (System.ComponentModel.Win32Exception ex)
							{
								Log.Instance.Warn(ex.Message);
							}

							executor.Execute();

							try
							{
								writer = new GUIConsoleWriter();
								writer.InitHandles();
								writer.WriteLine("Database has been successfully installed.");
								writer.WriteLine(_msgEndSuccess);
							}
							catch (System.ComponentModel.Win32Exception ex)
							{
								Log.Instance.Warn("Fail to attach the console.{0}{1}".
											  FormatWith(Environment.NewLine, ex));
							}
							finally
							{
								try
								{
									writer.Dispose();
									writer = null;
								}
								catch (Exception ex) { Log.Instance.Warn(ex.Message); }
							}
						}
						catch (Exception ex)
						{
							try
							{
								string errMsg = "Exception has been occurred while installing the database.{0}{1}".
																		FormatWith(Environment.NewLine, ex.Message);
								if (writer != null && !writer.Disposed)
								{
									writer.WriteLine(errMsg);
									writer.WriteLine(_msgEndFail);
									writer = null;
								}
								else
								{
									using (writer = new GUIConsoleWriter())
									{
										writer.InitHandles();
										writer.WriteLine(errMsg);
										writer.WriteLine(_msgEndFail);
									}
									writer = null;
								}
							}
							catch { }
							Log.Instance.Error("Error occured while execiting NonUiWorkflow.", ex);
						}
						finally
						{
							try
							{
								executor = null;
								System.Windows.Forms.SendKeys.SendWait("{ENTER}");
							}
							catch (Exception ex) { Log.Instance.Error("finally", ex); }
							Application.Exit();// exit app
						}
					}
					return;
				}
				else
				{
					var msg = "Specified command line arguments has incorrect format, see: -f [pathToFile] ";
					Log.Instance.Error(msg);
					throw new ApplicationException(msg);
				}
			}

			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			StateContainer.Instance.AddState(1, new StateDBSettings());

			var sqlsTask = Task.Factory.StartNew<List<string>>(() => SqlServerHelper.GetSQLsServerInstances(true));
			sqlsTask.RegisterFaultedHandler(OnError);
			sqlsTask.RegisterSucceededHandler(OnRetrieved);

			System.Threading.Thread.Sleep(600);//Necessary delay for proper SQL Server population
			Application.Run(new WizardMain());
		}

		private static void OnRetrieved(List<string> result)
		{
			if (result != null && result.Count > 0)
			{
				var sb = new System.Text.StringBuilder();
				sb.AppendLine("Retrieving avaliable SQL instances:");
				foreach (var item in result)
				{
					sb.AppendLine(item);
					(StateContainer.Instance[1] as StateDBSettings).LocaSqlInstances.Add(item);
				}
				Log.Instance.Info(sb.ToString());
				sb.Clear();
			}
		}

		private static void OnError(Exception exc)
		{
			Log.Instance.Error("Fail to load SQL Server instances.", exc);
		}

		private static void LogCmdParameters()
		{
			if (parameters != null)
			{
				Log.Instance.Info("Input parameters:");
				foreach (string parameter in parameters)
				{
					Log.Instance.Info(parameter);
				}
			}
		}

		private static void CheckFilePresence()
		{
			if (parameters == null)
				throw new ArgumentNullException("parameters");

			if (!File.Exists(parameters[2]))
			{
				if (string.IsNullOrEmpty(Path.GetDirectoryName(parameters[2])))
				{
					string fullLocPath = Path.Combine(Path.GetDirectoryName(parameters[0]), Path.GetFileName(parameters[2]));
					if (!File.Exists(fullLocPath))
					{
						string msg = "Specified file doesn't exists.{0}{1}".FormatWith(Environment.NewLine, fullLocPath);
						Log.Instance.Error(msg);
						throw new ApplicationException(msg);
					}
				}
				else
				{
					string msg = "Specified file doesn't exists.";
					Log.Instance.Error(msg);
					throw new ApplicationException(msg);
				}
			}
		}

		private static void Application_ApplicationExit(object sender, EventArgs e)
		{
			try
			{
				if (parameters != null)
				{
					if (parameters.Length > 1)
						System.Windows.Forms.SendKeys.SendWait("{ENTER}");
					parameters = null;
				}
			}
			catch { }

			OnEnd();
		}

		private static void OnEnd()
		{
			if (Log.Instance != null)
			{
				Log.Instance.Info(_msgEnd);
				Log.Instance.Dispose();
			}
		}

		private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
		{
			if (e.Exception is ApplicationException)
			{
				Log.Instance.Fatal("Application_ThreadException", e.Exception);
				System.Windows.Forms.SendKeys.SendWait("{ENTER}");
				Application.Exit();
			}

			Log.Instance.Fatal("Application_ThreadException", e.Exception);
		}
	}
}