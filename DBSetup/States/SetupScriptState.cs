using DBSetup.Common;
using DBSetup.Common.ModelBuilder;

namespace DBSetup.States
{
	internal class SetupScriptState : IState
	{
		private readonly string _name = "SetupScriptState";

		public string Name
		{
			get { return _name; }
		}

		public SetupScriptDocument DocumentText { get; set; }
	}
}