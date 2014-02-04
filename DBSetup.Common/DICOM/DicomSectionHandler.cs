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

		private Action<string> onStep;
		private Action<Exception> onError;

		public bool Handle(ISection entity)
		{
			if (entity == null)
				throw new ArgumentNullException("entity parameter cannot be a null.");

			bool result = true;

			if (entity is DICOMLink && Parameters != null && Parameters is ISqlConnectionSettings)
			{
				try
				{
					DICOMLink dicomLink = entity as DICOMLink;
					if (onStep != null)
						onStep(dicomLink.CSVFilePath);

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
							dicomImporter = new Importer(settings.ServerName, settings.DatabaseName, settings.UserName, settings.Password, Logger);
							dicomImporter.Process(dicomLink.CSVFilePath, !dicomLink.IsActive, dicomLink.IsActive, item);
						}
						catch (Exception ex)
						{
							DispatchOnError(ex);
							result = false;
							if (Logger != null)
								Logger.Error("Error occurred during DICOM import operation.", ex);
						}
						finally
						{
							if (dicomImporter != null)
								dicomImporter.Dispose();
						}
						//TODO: Omit commnet here if we need to stop execusion in case of Exception.
						//if (!result) break;
					}
				}
				catch (Exception ex)
				{
					DispatchOnError(ex);
					result = false;
				}
			}
			return result;
		}

		private void DispatchOnError(Exception ex)
		{
			if (onError != null)
				onError(ex);
		}

		private void NormalizePath(DICOMMergeFieldElements item)
		{
			string rootFolder = DBSetup.Common.ServiceLocator.Instance.GetService<IGlobalState>().GetState<string>("rootPath");
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


		public void OnStepHandler(Action<string> onStep)
		{
			if (onStep != null)
				this.onStep = onStep;
		}

		public void OnErrorHandler(Action<Exception> onError)
		{
			if (onError != null)
				this.onError = onError;
		}
	}
}
