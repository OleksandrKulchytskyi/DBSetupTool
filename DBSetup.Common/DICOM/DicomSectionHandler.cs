using DBSetup.Common.DICOM.Configuration;
using DBSetup.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common.DICOM
{
	public class DicomSectionHandler : ISectionHandler
	{
		public object Parameters
		{
			get;
			set;
		}

		public ILog Logger
		{
			get;
			set;
		}

		public void Handle(ISection entity)
		{
			if (entity == null)
				throw new ArgumentNullException("entity parameter cannot be a null.");

			if (entity is DICOMLink && Parameters != null && Parameters is ISqlConnectionSettings)
			{
				DICOMLink link = entity as DICOMLink;
				ISqlConnectionSettings settings = Parameters as ISqlConnectionSettings;

				List<DICOMMergeFieldElements> DICOMList = MergeFieldUtils.GetCollection();

				foreach (DICOMMergeFieldElements item in DICOMList)
				{
					NormalizePath(item);
				}

				Importer dicomImporter = null;
				foreach (var item in DICOMList)
				{
					try
					{
						dicomImporter = new Importer(settings.ServerName, settings.DatabaseName, settings.UserName, settings.Password);
						dicomImporter.Process(link.CSVFilePath, !link.IsActive, link.IsActive, item);
					}
					catch (Exception ex)
					{
						if (Logger != null)
							Logger.Error("Error occurred during DICOM import operation.", ex);
					}
					finally
					{
						if (dicomImporter != null)
							dicomImporter.Dispose();
					}
				}

				settings.Dispose();
			}
		}

		private void NormalizePath(DICOMMergeFieldElements item)
		{
			string rootFolder = DBSetup.Helpers.ServiceLocator.Instance.GetService<IGlobalState>().GetState<string>("rootPath");
			string csvPath = item.Csvfilename;
			string xmlPath = item.Xmlfilename;
			int ubnormalIndx = 0;
			
			csvPath = csvPath.StartsWith(".\\") ? System.IO.Path.Combine(rootFolder, csvPath) : csvPath;
			ubnormalIndx = csvPath.IndexOf(@"\.\", StringComparison.OrdinalIgnoreCase);
			csvPath = ubnormalIndx > 1 ? csvPath.Replace(@"\.\", @"\") : csvPath;
			item.Csvfilename = csvPath;

			xmlPath = xmlPath.StartsWith(".\\") ? System.IO.Path.Combine(rootFolder, xmlPath) : xmlPath;
			ubnormalIndx = xmlPath.IndexOf(@"\.\", StringComparison.OrdinalIgnoreCase);
			xmlPath = ubnormalIndx > 1 ? xmlPath.Replace(@"\.\", @"\") : xmlPath;
			item.Xmlfilename = xmlPath;
		}
	}
}
