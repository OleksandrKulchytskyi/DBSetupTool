using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DBSetup.Common.Statements;
using DBSetup.Common.Helpers;
using System.Collections.Generic;
using DBSetup.Common.Models;

namespace DbSetup.UnitTest
{
	[TestClass]
	public class SqlStatementTests
	{
		[TestMethod]
		public void SplitBySectionsWithCommentsTest()
		{
			string file = @"D:\DBSetup_Build\SQLScripts\SP\PsReport\rep_InsertReportVersion.sql";

			SqlDataStatement statement = new SqlDataStatement(System.IO.File.ReadAllText(file),file);

			int lines;
			var data = statement.SplitByGoStatementWithoutComments(out lines);
			if (data == null || data.Length == 0)
				Assert.Fail();
		}

		[TestMethod]
		public void SplitBySectionsTest()
		{
			SqlDataStatement statement = new SqlDataStatement(System.IO.File.ReadAllText(@"D:\DbSetup_PS360\PS360DBSetup\Comm4_schema2.SQL"),
				@"D:\DbSetup_PS360\PS360DBSetup\Comm4_schema2.SQL");
			var data = statement.SplitByGoStatementWithComments();
			if (data == null || data.Length == 0)
				Assert.Fail();
		}

		[TestMethod]
		public void SplitBySectionsTest2()
		{
			string text = string.Format("  /* hello my name is dfdsdfsd */ {0} /*you are line breaker  */ {0} " +
				"/* hello world {0}  what do you think {0} use master {0} gfdgfd */  ", Environment.NewLine);

			SqlDataStatement statement = new SqlDataStatement(text, @"D:\DbSetup_PS360\PS360DBSetup\Comm4_schema2.SQL");
			int lines;
			var data = statement.SplitByGoStatementWithoutComments(out lines);
			if (data == null || data.Length > 0)
				Assert.Fail();

			if (lines != 6)
				Assert.Fail();
		}

		[TestMethod]
		public void SplitBySectionsTest3()
		{
			var file = @"D:\DBSetup_Build\SQLScripts\V\v_PatientHistoryReport.sql";
			
			SqlDataStatement statement = new SqlDataStatement(System.IO.File.ReadAllText(file),file);

			int lines;
			var data = statement.SplitByGoStatementWithoutComments(out lines);
			if (data == null || data.Length > 0)
				Assert.Fail();

			if (lines <2)
				Assert.Fail();
		}

		[TestMethod]
		public void WithouCommentsTest()
		{
			string file = @"D:\1.txt";

			SqlDataStatement statement = new SqlDataStatement(System.IO.File.ReadAllText(file), file);
			int lines;
			var data = statement.SplitByGoStatementWithoutComments(out lines);
			if (data == null || data.Length == 0)
				Assert.Fail();
		}

		[TestMethod]
		public void GetProperUpgradeTest()
		{
			List<SectionBase> data = new List<SectionBase>() { new SectionBase() { Text = "Setup=Patch Update version 121 or higher" }, 
				new SectionBase() { Text = "Setup=Patch Update version 12 to 26" } };

			var actual =SectionBaseExtension.GetProperUpgrade(data, 20);
			Assert.AreNotEqual(null, actual);
		}
	}
}
