using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DBSetup.Common;
using DBSetup.Common.ModelBuilder;

namespace DbSetup.UnitTest
{
	[TestClass]
	public class IniFileHelpersTest
	{
		[TestMethod]
		public void GetSingleSectionTestMethod()
		{
			var data = IniFileParser.GetSingleSection(@"D:\DbSetupPS360_2-13-2013\DB\SqlDbSetup2.ini", "Finalization");
			Assert.IsNotNull(data);
			Assert.IsTrue(data["Finalization"].Count > 0);
		}

		[TestMethod]
		public void GetSingleSectionAndBuildTreeTestMethod()
		{
			var data = IniFileParser.GetSingleSection(@"D:\DbSetupPS360_2-13-2013\DB\SqlDbSetup2.ini", "Finalization");
			Assert.IsNotNull(data);
			Assert.IsTrue(data["Finalization"].Count > 0);

			ObjectModelBuilder builder = new ObjectModelBuilder();
			builder.LoadSql = true; builder.LoadBLOB = true;
			var finalization = new DBSetup.Common.Models.SectionBase();
			finalization.Text = "Finalization";
			var result = builder.BuildByText(finalization, data);
			if (finalization.Children == null) finalization.Children = new System.Collections.Generic.List<DBSetup.Common.Models.SectionBase>();

			finalization.Children.AddRange(result);

			Assert.IsTrue(finalization.Children.Count == 1 && finalization.Children[0] is DBSetup.Common.Models.SqlLink);
		}
	}
}
