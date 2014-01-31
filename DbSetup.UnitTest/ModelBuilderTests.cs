using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DBSetup.Common.ModelBuilder;
using DBSetup.Common.Models;

namespace DbSetup.UnitTest
{
	[TestClass]
	public class ModelBuilderTests
	{
		[TestMethod]
		public void CheckBuldingAbilities()
		{
			ModelBuilderContext context = new ModelBuilderContext();
			IBuilder builder = new FullModelBuilder();
			(builder as FullModelBuilder).OpenFile(@"D:\DbSetupPS360_2-13-2013\DB\SQLDBSetup.ini");
			(builder as FullModelBuilder).SetBuildAll();
			context.SetBuilder(builder);

			var sections = context.ExecuteBuild();
			if (sections == null || sections.Count == 0)
				Assert.Fail();
		}

		[TestMethod]
		public void BuildSetupTreeTest()
		{
			ModelBuilderContext context = new ModelBuilderContext();
			IBuilder builder = new FullModelBuilder();
			(builder as FullModelBuilder).OpenFile(@"D:\DbSetupPS360_2-13-2013\DB\SQLDBSetup.ini");
			(builder as FullModelBuilder).SetBuildAll();
			context.SetBuilder(builder);

			var sections = context.ExecuteBuild();
			if (sections == null || sections.Count == 0)
				Assert.Fail();

			ScriptConsequencyBuilder consequencyBuilder = new ScriptConsequencyBuilder();
			consequencyBuilder.Build(sections[1].Children[0]);

			if (consequencyBuilder.GetDocumentResult().DocumentLinesCount < 2)
				Assert.Fail();

			var text = consequencyBuilder.GetDocumentResult().GetDocumentText();
			Console.WriteLine(text);
		}


		[TestMethod]
		public void BuildSetupTreeAsyncTest()
		{
			ModelBuilderContext context = new ModelBuilderContext();
			IBuilder builder = new FullModelBuilder();
			(builder as FullModelBuilder).OpenFile(@"D:\DbSetupPS360_2-13-2013\DB\SQLDBSetup.ini");
			(builder as FullModelBuilder).SetBuildAll();
			context.SetBuilder(builder);

			var sections = context.ExecuteBuild();
			if (sections == null || sections.Count == 0)
				Assert.Fail();

			ScriptConsequencyBuilder consequencyBuilder = new ScriptConsequencyBuilder();
			consequencyBuilder.BuildAsync(sections[1].Children[0]);

			while (consequencyBuilder.TaskStarted && !consequencyBuilder.TaskCompleted)
				System.Threading.Thread.Sleep(10);

			if (consequencyBuilder.GetDocumentResult().DocumentLinesCount < 2)
				Assert.Fail();

			var text = consequencyBuilder.GetDocumentResult().GetDocumentText();
			Console.WriteLine(text);
		}

		[TestMethod]
		public void BuildSetupTreeTest2()
		{
			ModelBuilderContext context = new ModelBuilderContext();
			IBuilder builder = new FullModelBuilder();
			(builder as FullModelBuilder).OpenFile(@"D:\DbSetupPS360_2-13-2013\DB\SQLDBSetup.ini");
			(builder as FullModelBuilder).SetBuildAll();
			context.SetBuilder(builder);

			var sections = context.ExecuteBuild();
			if (sections == null || sections.Count == 0)
				Assert.Fail();

			ScriptConsequencyBuilder consequencyBuilder = new ScriptConsequencyBuilder();
			consequencyBuilder.Build(sections[0].Children[0]);

			if (consequencyBuilder.GetDocumentResult().DocumentLinesCount < 2)
				Assert.Fail();

			var text = consequencyBuilder.GetDocumentResult().GetDocumentText();
			Console.WriteLine(text);

		}
	}
}
