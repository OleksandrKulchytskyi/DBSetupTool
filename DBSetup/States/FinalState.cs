using DBSetup.Common;

namespace DBSetup.States
{
	internal class FinalState : IState
	{
		private readonly string _name = typeof(FinalState).Name;

		public string Name
		{
			get { return _name; }
		}

		public string HealthSystemName { get; set; }

		public string SiteName { get; set; }

		public string Password { get; set; }
	}
}