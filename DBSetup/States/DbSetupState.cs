using DBSetup.Common;
using DBSetup.Common.Models;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace DBSetup.States
{
	internal class DbSetupState : IState
	{
		private readonly string _name = "DbSetupState";

		public string Name
		{
			get { return _name; }
		}

		public List<Language> Languages { get; set; }

		public Language SelectedLanguage { get; set; }

		public List<SectionBase> SetupTypes { get; set; }

		[Export(typeof(SectionBase))]
		public SectionBase SelectedSetupType { get; set; }

		public SectionBase DatabaseConfiguration { get; set; }

		public string DbFileName { get; set; }

		public string LogFileName { get; set; }

		public string DbFilePath { get; set; }

		public string LogFilePath { get; set; }

		public int DbInitialSize { get; set; }

		public int LogInitialSize { get; set; }

		public int DbGrowth { get; set; }

		public int LogGrowth { get; set; }

		public GrowthType DbGrowthType { get; set; }

		public GrowthType LogGrowthType { get; set; }

		public DbSetupType DatabaseSetupType { get; set; }
	}

	internal enum GrowthType : int
	{
		MB = 0,
		Percentage
	}

	internal enum DbSetupType : int
	{
		New = 0,
		Upgrade,
		LoadSetup,
		None
	}
}