using DBSetup.Common.Models;
using DBSetup.Common.Statements;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace DBSetup.Common.ModelBuilder
{
	[Export(typeof(IDataStatementFactory))]
	public class DataStatementsFactory : IDataStatementFactory
	{
		private const int _retryCount = 3;

		public DataStatementsFactory()
		{
		}

		[Import(typeof(string))]
		public string RootFile { get; set; }

		[Import(typeof(SectionBase))]
		public SectionBase SelectedSetupConfig { get; set; }

		public List<IDataStatement> Generate()
		{
			ModelBuilder.FullModelBuilder builder = new FullModelBuilder();
			builder.SetBuildAll();
			builder.OpenFile(RootFile);
			builder.Build();

			var result = builder.GetResult();

			if (result == null)
				throw new InvalidOperationException("Fail to build full objects tree");

			List<IDataStatement> statements = new List<IDataStatement>();
			if (IsSectionExists(result, SelectedSetupConfig))
			{
				foreach (SectionBase section in SelectedSetupConfig.Children)
				{
					BuildSubTree(section, section.Children, statements);
				}
			}

			return statements;
		}

		public List<IDataStatement> GenerateFor(SectionBase baseline)
		{
			var result = new List<SectionBase>(1);
			result.Add(baseline);

			if (result == null)
				throw new InvalidOperationException("Fail to build full objects tree");

			List<IDataStatement> statements = new List<IDataStatement>();
			if (IsSectionExists(result, baseline))
			{
				foreach (SectionBase section in baseline.Children)
				{
					BuildSubTree(section, section.Children, statements);
				}
			}

			return statements;
		}

		private void BuildSubTree(SectionBase subSection, List<SectionBase> children, List<IDataStatement> list)
		{
			bool isError = false;
			int tryCount = 0;
			switch (subSection.GetType().Name)
			{
				case "SqlLink":
					do
					{
						try
						{
							list.Add(new SqlDataStatement(System.IO.File.ReadAllText((subSection as SqlLink).SqlFilePath),
																(subSection as SqlLink).SqlFilePath));
							isError = false;
						}
						catch (System.IO.IOException)
						{
							isError = true;
							tryCount++;
						}
						catch (System.UnauthorizedAccessException)
						{
							isError = true;
							tryCount++;
						}
					}
					while ((isError) && (tryCount != _retryCount));
					break;

				case "DICOMLink":
					list.Add(new DicomDataStatement((subSection as DICOMLink).CSVFilePath, (subSection as DICOMLink).IsActive));
					break;

				case "SectionBase":
					if (subSection.Children != null && subSection.Children.Count > 0)
					{
						foreach (var child in subSection.Children)
						{
							BuildSubTree(child, child.Children, list);
						}
					}
					break;

				default:
					break;
			}
		}

		#region check for section existing

		public bool IsSectionExists(List<SectionBase> objectTree, SectionBase searchItem)
		{
			bool IsFound = false;
			foreach (SectionBase sb in objectTree)
			{
				if (sb.Equals(searchItem))
				{
					IsFound = true;
					break;
				}
				else if (sb.Children != null)
				{
					FounInSubTrems(sb.Children, searchItem, out IsFound);
					if (IsFound)
						break;
				}
			}
			return IsFound;
		}

		private void FounInSubTrems(List<SectionBase> objectList, SectionBase searchItem, out bool found)
		{
			found = false;
			foreach (SectionBase sb in objectList)
			{
				if (sb.Equals(searchItem))
				{
					found = true;
					break;
				}
				else if (sb.Children != null)
				{
					FounInSubTrems(sb.Children, searchItem, out found);
					if (found)
						break;
				}
			}
		}
	}

		#endregion check for section existing
}