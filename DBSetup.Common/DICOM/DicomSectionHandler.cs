using DBSetup.Common.DICOM.Configuration;
using DBSetup.Common.Models;
using System;
using System.Collections.Generic;

namespace DBSetup.Common.DICOM
{
	public sealed class DicomSectionHandler : ISectionHandler, IDisposable
	{
		private Action<string, object> onPreHandle;
		private Action<string> onStep;
		private Func<Exception, object, object> onError;

		private Action<string, string, object> onEntryProcessed;
		private volatile bool isCancelled = false;

		private Importer curreantImporter = null;
		private bool disposed = false;

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
					if (this.onPreHandle != null)
						onPreHandle(dicomLink.CSVFilePath, settings);

					List<DICOMMergeFieldElements> DICOMList = MergeFieldUtils.GetCollection();

					foreach (DICOMMergeFieldElements item in DICOMList)
					{
						NormalizePath(item);
					}

					foreach (var item in DICOMList)
					{
						if (isCancelled)
							break;

						try
						{
							curreantImporter = new Importer(settings.ServerName, settings.DatabaseName, settings.UserName, settings.Password, Logger);
							curreantImporter.DelayBetweenOpeartions = 50;
							curreantImporter.SetOnEntryProcessing(onEntryProcessed);
							curreantImporter.Process(dicomLink.CSVFilePath, !dicomLink.IsActive, dicomLink.IsActive, item);
						}
						catch (DicomOperationCancelledException ex) { Logger.Warn(ex.Message); }
						catch (Exception ex)
						{
							object state = DispatchOnError(ex, null);
							if (state == null)
								result = false;
							if (Logger != null)
								Logger.Error("Error occurred during the DICOM import operation.", ex);
						}
						finally
						{
							if (curreantImporter != null)
							{
								curreantImporter.Dispose();
								curreantImporter = null;
							}
						}
						//TODO: Omit commnet here if we need to stop execusion in case of Exception.
						//if (!result) break;
					}
				}
				catch (Exception ex)
				{
					object state = DispatchOnError(ex, null);
					result = false;
				}
			}
			return result;
		}

		private object DispatchOnError(Exception ex, object state)
		{
			if (onError != null)
			{
				return onError(ex, state);
			}
			return null;
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

		public void OnPreHandler(Action<String, object> onPreHandle)
		{
			if (onPreHandle != null)
				this.onPreHandle = onPreHandle;
		}

		public void OnStepHandler(Action<string> onStep)
		{
			if (onStep != null)
				this.onStep = onStep;
		}

		public void OnErrorHandler(Func<Exception, object, object> onErrorHandler)
		{
			if (onErrorHandler != null)
				this.onError = onErrorHandler;
		}

		public void OnEntryProcessing(Action<string, string, object> onProcessed)
		{
			if (onProcessed != null)
				this.onEntryProcessed = onProcessed;
		}

		public void OnBunchHandled(Action<object> onBunch)
		{
			// no handling for dicom
		}

		public void OnOutputReceived(Action<string> onOutput)
		{
			// no handling for dicom
		}

		public void Cancel()
		{
			if (isCancelled) return;

			isCancelled = true;
			if (curreantImporter != null)
				curreantImporter.Cancel();
		}

		public void Dispose()
		{
			if (disposed)
				return;
			disposed = true;
			GC.SuppressFinalize(this);
			if (curreantImporter != null)
			{
				curreantImporter.Dispose();
				curreantImporter = null;
			}
		}
	}
}
