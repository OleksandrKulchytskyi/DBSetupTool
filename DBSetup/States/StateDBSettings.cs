using DBSetup.Common;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace DBSetup.States
{
	[Export(typeof(StateDBSettings))]
	internal class StateDBSettings : IState
	{
		public StateDBSettings()
		{
			LocaSqlInstances = new List<string>();
		}

		private const string _name = "Database connection settings";

		public string Name
		{
			get
			{
				return _name;
			}
		}

		public string ServerName { get; set; }

		public string UserName { get; set; }

		public string Password { get; set; }

		public List<string> LocaSqlInstances { get; set; }

		public bool IsConnectionSucceed { get; set; }

		public override string ToString()
		{
			return string.Format("Server: {0}, User: {1},Password: {2}", ServerName, UserName, Password);
		}
	}
}