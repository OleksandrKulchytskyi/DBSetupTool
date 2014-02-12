using DBSetup.Common.Models;
using DBSetup.Common.Helpers;
using DBSetup.Common.Statements;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;

namespace DBSetup.Common
{
	public class SQLSectionHandler : ISectionHandler
	{
		private const int scriptSleepTimeout = 60;

		private Action<string, object> onPreHandle;
		private Action<string> onStepHandler;
		private Action<object> onBunchHandled;
		private Func<Exception, object, object> onErrorHandler;
		private Action<string, string, object> onEntryProcessed;

		private IGlobalState global;

		private string[] _sqlsToBeExecuted;
		private int _sqlProcessingIndex = 0;

		public SQLSectionHandler()
		{
			global = ServiceLocator.Instance.GetService<IGlobalState>();
		}

		public object Parameters { get; set; }

		public ILog Logger { get; set; }

		public bool Handle(ISection entity)
		{
			if (entity == null)
				throw new ArgumentNullException("entity parameter cannot be a null.");

			bool result = true;

			if (entity is SqlLink && Parameters != null && Parameters is Tuple<SqlConnection, IDataStatement>)
			{
				SqlLink sql = (entity as SqlLink);
				Tuple<SqlConnection, IDataStatement> parameter = (Tuple<SqlConnection, IDataStatement>)Parameters;
				SqlDataStatement statement = parameter.Item2 as SqlDataStatement;
				int count;
				_sqlsToBeExecuted = statement.SplitByGoStatementWithComments();
				count = _sqlsToBeExecuted.Length;
				Exception occurred = null;
				while (result && _sqlProcessingIndex < count)
				{
					try
					{
						string sqlContent = GetRidOfGoStatement(_sqlsToBeExecuted[_sqlProcessingIndex]);
						if (onPreHandle != null)
							onPreHandle(sqlContent, null);

						if (onStepHandler != null)
							onStepHandler(string.Format("{0},{1},{2}", sql.SqlFilePath, _sqlProcessingIndex + 1, count));

						ExecuteSql(sqlContent, parameter.Item1);
						_sqlProcessingIndex++;
					}
					catch (SqlException sqlExc)
					{
						occurred = sqlExc;
					}
					catch (Exception ex)
					{
						occurred = ex;
					}
					if (occurred != null && onErrorHandler != null)
					{
						object val = onErrorHandler(occurred, _sqlsToBeExecuted[_sqlProcessingIndex]);
						if (val != null)
						{
							_sqlsToBeExecuted[_sqlProcessingIndex] = (val as string);
							_sqlProcessingIndex--;
							occurred = null;
						}
						else
							result = false;
					}

				}
			}

			return result;
		}


		private void ExecuteSql(string sql, SqlConnection connection)
		{
			if (!string.IsNullOrEmpty(sql) && !string.IsNullOrWhiteSpace(sql))
			{
				using (var command = connection.CreateCommand())
				{
					if (sql.IndexOf("select", StringComparison.OrdinalIgnoreCase) != -1 &&
						sql.IndexOf("@@version", StringComparison.OrdinalIgnoreCase) != -1 &&
						global.GetState<SectionBase>("setupType").Text.StartsWithIgnoreSpaces("new"))
					{
						command.CommandText = sql;
						object data = command.ExecuteScalar();
						if (data != null && data is string)
						{
							//txtExecutionLog.ExecAction(() =>
							//{
							string msg = string.Format("{0} {1}", data as string, Environment.NewLine);
							//txtExecutionLog.AppendText(msg);
							//Log.Instance.Info(msg);
							//});
						}
						return;
					}

					string ctrlSql = null;
					//	txtScriptToRun.ExecAction(() => ctrlSql = txtScriptToRun.Text);
					if (!string.IsNullOrEmpty(ctrlSql) &&
						string.Compare(sql, ctrlSql, StringComparison.OrdinalIgnoreCase) != 0)
						command.CommandText = ctrlSql;
					else
						command.CommandText = sql;
					//add some trick , default command timeout was 30 secs.
					//command.CommandTimeout = sqlCommandTimeout;
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

		public void OnPreHandler(Action<String, object> onPreHandle)
		{
			if (onPreHandle != null)
				this.onPreHandle = onPreHandle;
		}

		public void OnStepHandler(Action<string> onStep)
		{
			if (onStep != null)
				this.onStepHandler = onStep;
		}

		public void OnErrorHandler(Func<Exception, object, object> onErrorHandler)
		{
			if (onErrorHandler != null)
				this.onErrorHandler = onErrorHandler;
		}

		public void OnEntryProcessing(Action<string, string, object> onProcessed)
		{
			if (onProcessed != null)
				this.onEntryProcessed = onProcessed;
		}

		public void OnBunchHandled(Action<object> onBunch)
		{
			if (onBunch != null)
				onBunchHandled = onBunch;
		}
	}
}
