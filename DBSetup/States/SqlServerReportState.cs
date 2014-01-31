using DBSetup.Common;

namespace DBSetup.States
{
	internal class SqlServerReportState : IState
	{
		private const string _name = "SQL Server Report";

		public string Name
		{
			get { return _name; }
		}

		public string SQLVersion { get; set; }

		public bool IsComm4Exists { get; set; }

		public string Comm4Version { get; set; }
	}
}