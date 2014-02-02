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

				foreach (var item in DICOMList)
				{
					NormalizePath(item.Csvfilename);
					NormalizePath(item.Xmlfilename);
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

		private void NormalizePath(string path)
		{
			string rootFolder = DBSetup.Helpers.ServiceLocator.Instance.GetService<IGlobalState>().GetState<string>("rootPath");
			path = path.StartsWith(".\\") ? System.IO.Path.Combine(rootFolder, path) : path;

			int ubnormalIndx = path.IndexOf(@"\.\", StringComparison.OrdinalIgnoreCase);
			path = ubnormalIndx > 1 ? path.Replace(@"\.\", @"\") : path;
		}
	}
}
