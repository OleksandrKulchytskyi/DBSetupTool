using DBSetup.Common;
using System.ComponentModel.Composition;

namespace DBSetup.States
{
	internal class ConfigFileState : IState
	{
		public ConfigFileState()
		{
			FilePath = string.Empty;
		}

		private readonly string _name = "ConfigFile state";

		public string Name
		{
			get { return _name; }
		}

		[Export(typeof(string))]
		public string FilePath { get; set; }
	}
}