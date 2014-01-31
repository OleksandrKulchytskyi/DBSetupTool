using DBSetup.Common.Models;
using System.Collections.Generic;

namespace DBSetup.Common.ModelBuilder
{
	public interface IBuilder
	{
		List<FaultSection> FaultSectionsList { get; set; }

		void Build();

		List<SectionBase> GetResult();

		bool LoadDICOM { get; set; }
	}
}
