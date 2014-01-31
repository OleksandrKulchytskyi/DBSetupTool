using DBSetup.Common.Models;
using DBSetup.Common.Statements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common
{
	public interface IDataStatementFactory
	{
		List<IDataStatement> Generate();

		List<IDataStatement> GenerateFor(SectionBase baseline);
	}
}
