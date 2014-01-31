using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

using Microsoft.SqlServer.Management.Smo;

namespace DBSetup.Common.Helpers
{
	public static class SqlServerHelper
	{
		public static bool CanOpen(this SqlConnection connection)
		{
			try
			{
				if (connection == null) { return false; }

				connection.Open();
				var canOpen = connection.State == ConnectionState.Open;
				connection.Close();
				return canOpen;
			}
			catch
			{
				return false;
			}
		}

		public static bool CanOpen(this string connStr)
		{
			if (string.IsNullOrEmpty(connStr)) return false;

			try
			{
				bool canOpen = false;
				using (SqlConnection con = new SqlConnection(connStr))
				{
					if (con.State == ConnectionState.Closed)
						con.Open();

					canOpen = con.State == ConnectionState.Open;

					if (con.State == ConnectionState.Open)
						con.Close();
				}
				return canOpen;
			}
			catch (Exception ex)
			{
				Log.Instance.Error("CanOpen", ex);
				return false;
			}
		}

		public static void CanOpenWithException(this string connStr)
		{
			if (string.IsNullOrEmpty(connStr)) return;

			try
			{
				using (SqlConnection con = new SqlConnection(connStr))
				{
					if (con.State == ConnectionState.Closed)
						con.Open();

					if (con.State == ConnectionState.Open)
						con.Close();
				}
			}
			catch (Exception ex)
			{
				Log.Instance.Error("CanOpen", ex);
				throw;
			}
		}

		public static bool CheckDatabaseExists(SqlConnection tmpConnection, string databaseName)
		{
			string sqlCreateDBQuery;
			bool result = false;

			try
			{
				tmpConnection = new SqlConnection("server=(local)\\SQLEXPRESS;Trusted_Connection=yes");

				sqlCreateDBQuery = string.Format("SELECT database_id FROM sys.databases WHERE Name = '{0}'", databaseName);

				using (tmpConnection)
				{
					using (SqlCommand sqlCmd = new SqlCommand(sqlCreateDBQuery, tmpConnection))
					{
						if (tmpConnection.State != ConnectionState.Open)
							tmpConnection.Open();
						int databaseID = (int)sqlCmd.ExecuteScalar();

						result = (databaseID > 0);
					}
				}
			}
			catch (Exception ex)
			{
				result = false;
				Log.Instance.Error("CheckDatabaseExists", ex);
			}

			return result;
		}

		public static bool CheckDatabaseExists(string strConn, string databaseName)
		{
			string sqlCreateDBQuery;
			bool result = false;

			try
			{
				SqlConnection sqlCon = new SqlConnection(strConn);
				sqlCreateDBQuery = string.Format("SELECT database_id FROM sys.databases WHERE Name = '{0}'", databaseName);

				using (sqlCon)
				{
					using (SqlCommand sqlCmd = new SqlCommand(sqlCreateDBQuery, sqlCon))
					{
						if (sqlCon.State != ConnectionState.Open && sqlCon.State != ConnectionState.Connecting)
							sqlCon.Open();
						int databaseID = 0;
						object execResult = sqlCmd.ExecuteScalar();
						if (execResult != null)
							databaseID = (int)execResult;

						result = (databaseID > 0);
					}
				}
			}
			catch (Exception ex)
			{
				result = false;
				Log.Instance.Error("CheckDatabaseExists", ex);
			}

			return result;
		}

		public static string GetDatabaseVersion(string strConn, string databaseName)
		{
			string sqlCreateDBQuery;
			string result = null;

			try
			{
				SqlConnection sqlCon = new SqlConnection(strConn);
				sqlCreateDBQuery = "SELECT [version] FROM master.dbo.sysdatabases where name='{0}' ORDER BY dbid".FormatWith(databaseName);

				using (sqlCon)
				{
					using (SqlCommand sqlCmd = new SqlCommand(sqlCreateDBQuery, sqlCon))
					{
						if (sqlCon.State != ConnectionState.Open)
							sqlCon.Open();
						object version = null;
						using (var reader = sqlCmd.ExecuteReader(CommandBehavior.CloseConnection))
						{
							while (reader.Read())
							{
								version = reader[0];
								break;
							}
						}

						if (version != null)
							result = version.ToString();
					}
				}
			}
			catch (Exception ex)
			{
				Log.Instance.Warn(string.Format("Cannot get version of installed database.{0}{1}", Environment.NewLine, ex.Message));
			}

			return result;
		}

		public static string GetSQLServerVersion(string strCon)
		{
			try
			{
				if (string.IsNullOrEmpty(strCon))
					return null;
				string version = string.Empty;
				using (SqlConnection con = new SqlConnection(strCon))
				{
					con.Open();
					var canOpen = con.State == ConnectionState.Open;
					version = con.ServerVersion;

					if (con.State == ConnectionState.Open) con.Close();
				}
				return version;
			}
			catch (Exception ex)
			{
				Log.Instance.Error("GetSQLServerVersion", ex);
				return null;
			}
		}

		public static string GetSQLVersionByCommand(string conString)
		{
			try
			{
				if (string.IsNullOrEmpty(conString))
					return null;
				string version = string.Empty;

				using (SqlConnection con = new SqlConnection(conString))
				{
					if (con.State == ConnectionState.Closed)
						con.Open();

					using (SqlCommand command = con.CreateCommand())
					{
						command.CommandText = "SELECT @@version";

						object data = command.ExecuteScalar();
						if (data != null && data is string)
							version = data as string;
					}

					if (con.State == ConnectionState.Open)
						con.Close();
				}
				return version;
			}
			catch (Exception ex)
			{
				Log.Instance.Warn(string.Format("GetSQLServerVersion, {0}", ex.Message));
				return null;
			}
		}

		public static int GetDbVersionFromVesrsionTable(string conString, string dbName)
		{
			if (string.IsNullOrEmpty(conString))
				throw new ArgumentNullException("conString");

			if (string.IsNullOrEmpty(dbName))
				throw new ArgumentNullException("dbName");

			try
			{
				if (string.IsNullOrEmpty(conString))
					return -1;
				int version = -1;
				using (SqlConnection con = new SqlConnection(conString))
				{
					if (con.State == ConnectionState.Closed)
						con.Open();

					using (SqlCommand command = con.CreateCommand())
					{
						command.CommandText = "USE {0}; \n\r SELECT TOP 1 [Number] FROM [Version] ORDER BY [Number] DESC;".FormatWith(dbName);

						using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
						{
							while (reader.Read())
							{
								version = reader.GetInt32(0);
								break;
							}
						}
					}

					if (con.State == ConnectionState.Open) con.Close();
				}
				return version;
			}
			catch (Exception ex)
			{
				Log.Instance.Warn(string.Format("GetDbVersionFromVesrsionTable method, {0}", ex.Message));
				return -1;
			}
		}

		public static SQLServerVersion ParseSQLServerVersion(string strVersion)
		{
			if (!string.IsNullOrEmpty(strVersion))
				Log.Instance.Info(string.Format("ParseSQLServerVersion , incoming parameter for version is: {0}", strVersion));

			SQLServerVersion version = SQLServerVersion.Undefined;

			switch (strVersion)
			{
				case "11.00.3":
				case "11.00.30":
				case "11.00.3000":
					version = SQLServerVersion.SQLServer2012SP1;
					break;

				case "11.00.2218":
				case "11.00.2218.00":
				case "11.00.2100.6":
				case "11.00.2100.60":
					version = SQLServerVersion.SQLServer2012RTM;
					break;

				case "10.50.25":
				case "10.50.2500":
				case "10.50.2500.0":
					version = SQLServerVersion.SQLServer2008R2SP1;
					break;

				case "10.50.1600.1":
					version = SQLServerVersion.SQLServer2008R2;
					break;

				case "10.00.55":
				case "10.00.5500":
				case "10.00.5500.00":
					version = SQLServerVersion.SQLServer2008SP3;
					break;

				case "10.00.4":
				case "10.00.40":
				case "10.00.4000":
				case "10.00.4000.00":
					version = SQLServerVersion.SQLServer2008SP2;
					break;

				case "10.00.2531":
				case "10.00.2531.00":
					version = SQLServerVersion.SQLServer2008SP1;
					break;

				case "10.00.1600.22":
					version = SQLServerVersion.SQLServer2008;
					break;

				case "9.00.5":
				case "9.00.50":
				case "9.00.5000":
				case "9.00.5000.00":
					version = SQLServerVersion.SQLServer2005SP4;
					break;

				case "9.00.4035":
					version = SQLServerVersion.SQLServer2005SP3;
					break;

				case "9.00.3042":
					version = SQLServerVersion.SQLServer2005SP2;
					break;

				case "9.00.2047":
					version = SQLServerVersion.SQLServer2005SP1;
					break;

				case "9.00.1399":
					version = SQLServerVersion.SQLServer2005;
					break;

				default:
					break;
			}

			if (version == SQLServerVersion.Undefined && strVersion.StartsWithIgnoreSpaces("11.00"))
				version = SQLServerVersion.SQLServer2012;

			return version;
		}

		public static List<string> GetLocalSQLsServerInstances()
		{
			DataTable dt = SmoApplication.EnumAvailableSqlServers(true);
			string[] szSQLInstanceNames = new string[dt.Rows.Count];
			StringBuilder szSQLData = new StringBuilder();

			if (dt.Rows.Count > 0)
			{
				int i = 0;
				foreach (DataRow dr in dt.Rows)
				{
					try
					{
						szSQLInstanceNames[i] = dr["Name"].ToString();
						Server oServer = null;
						oServer = new Server(szSQLInstanceNames[i]);

						string information = "{0} Vesrion: {1} Servcice Pack: {2} Edition: {3}  Collation".FormatWith(szSQLInstanceNames[i],
								oServer.Information.Version.Major, oServer.Information.ProductLevel, oServer.Information.Collation);
						szSQLData.AppendLine(information);
						//szSQLData.AppendLine(szSQLInstanceNames[i] + "  Version: " + oServer.Information.Version.Major + "  Service Pack: " + oServer.Information.ProductLevel + 
						//					"  Edition: " + oServer.Information.Edition + "  Collation: " + oServer.Information.Collation);
					}
					catch (Exception Ex)
					{
						szSQLData.AppendLine("Exception occured while connecting to " + szSQLInstanceNames[i] + " " + Ex.Message);
					}

					i++;
				}
			}
			Log.Instance.Info(szSQLData.ToString());
			if ((dt as IDisposable) != null)
				(dt as IDisposable).Dispose();
			if (dt != null)
			{
				dt.Clear(); dt = null;
			}

			return szSQLInstanceNames.ToList();
		}

		public static List<string> GetSQLsServerInstances(bool local = true)
		{
			DataTable dt = SmoApplication.EnumAvailableSqlServers(local);
			if (dt == null || dt.Rows.Count == 0)
				return new List<string>();
			List<string> szSQLInstanceNames = new List<string>(dt.Rows.Count == 0 ? 1 : dt.Rows.Count);

			int i = 0;
			foreach (DataRow dr in dt.Rows)
			{
				try
				{
					szSQLInstanceNames.Add(dr["Name"].ToString());
					//szSQLInstanceNames[i] = dr["Name"].ToString();
				}
				catch (Exception ex)
				{
					Log.Instance.Error("GetSQLsServerInstances", ex);
					throw;
				}
				i++;
			}

			if ((dt as IDisposable) != null)
				(dt as IDisposable).Dispose();
			if (dt != null)
			{
				dt.Clear(); dt = null;
			}

			return szSQLInstanceNames;
		}
	}

	public enum SQLServerVersion
	{
		Undefined = 0,

		SQLServer2005 = 1,
		SQLServer2005SP1 = 2,
		SQLServer2005SP2 = 3,
		SQLServer2005SP3 = 4,
		SQLServer2005SP4 = 5,

		SQLServer2008 = 6,
		SQLServer2008SP1 = 7,
		SQLServer2008SP2 = 8,
		SQLServer2008SP3 = 9,

		SQLServer2008R2 = 10,
		SQLServer2008R2SP1 = 11,

		SQLServer2012RTM = 12,
		SQLServer2012SP1 = 13,
		SQLServer2012 = 14,
	}
}
