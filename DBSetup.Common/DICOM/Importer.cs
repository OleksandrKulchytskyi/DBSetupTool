using DBSetup.Common.DICOM.Configuration;
using DBSetup.Common.DICOM.Data;
using DBSetup.Common.Models;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace DBSetup.Common.DICOM
{
	public class Importer : IDisposable, ICancelable
	{
		private bool disposed = false;
		private volatile bool isCancelled = false;

		//DICOM DB context
		private PS360DICOMTablesDataContext _context;
		//internal logger component
		private ILog _logger;

		private Action<string, string, object> entryProcessing;

		/// <summary>
		/// Delay between Importer operations(in ms).
		/// </summary>
		public int DelayBetweenOpeartions { get; set; }

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="server">DB server name</param>
		/// <param name="dbName">Name of the database</param>
		/// <param name="username">SQL user name</param>
		/// <param name="password">SQL password</param>
		/// <param name="logger">Logger instance</param>
		/// <exception cref="System.ArgumentNullException"></exception>
		public Importer(string server, string dbName, string username, string password, ILog logger)
		{
			if (logger == null)
				throw new ArgumentNullException("Logger parameter cannot be a null.");
			_logger = logger;
			_context = new PS360DICOMTablesDataContext(String.Format("Server={0};Database={1};User Id={2};Password={3}", server, dbName, username, password));
		}

		public void Cancel()
		{
			isCancelled = true;
		}

		/// <summary>
		/// Processing all csv file records and submit it to the DB
		/// </summary>
		/// <param name="filename"></param>
		/// <param name="inactive"></param>
		/// <param name="active"></param>
		/// <param name="dicomMFgroupelement"></param>
		/// <exception cref="System.IO.IOException"></exception>
		/// /// <exception cref="System.ArgumentNullException"></exception>
		public void Process(string filename, bool inactive, bool active, DICOMMergeFieldElements dicomMFgroupelement)
		{
			if (string.IsNullOrEmpty(filename))
				throw new ArgumentNullException("Filename parameter cannot be a null.");
			if (dicomMFgroupelement == null)
				throw new ArgumentNullException("dicomMFgroupelement parameter cannot be a null.");

			ThrowIfDisposed();

			try
			{
				int deviceID = GetDeviceID();
				if (deviceID == 0)
				{
					_logger.Error("Creating the device record in the database.");
					return;
				}

				System.Threading.Thread.Sleep(DelayBetweenOpeartions);

				int srTemplateTypeID = GetTemplateTypeID(filename);
				if (srTemplateTypeID == 0)
				{
					_logger.Error("Creating the SR template type record in the database.");
					return;
				}
				System.Threading.Thread.Sleep(DelayBetweenOpeartions);

				int srTemplateID = GetTemplateID(filename, srTemplateTypeID, deviceID, dicomMFgroupelement);
				if (srTemplateTypeID == 0)
				{
					_logger.Error("Creating the SR Template record in the database.");
					return;
				}

				// loop through the filename, and import the data 
				bool hasError = false;
				using (var reader = new StreamReader(filename))
				{
					OnActionProcessing("Reading CSV content header.", filename, null);
					string line;
					int row = 0;
					int added = 0;
					int updated = 0;
					int time = DelayBetweenOpeartions - 25;
					while (!isCancelled && ((line = reader.ReadLine()) != null))
					{
						// tokenize
						row++;
						string[] tok = line.Split(',');
						if (tok.Length == 13)
						{
							// skip the header row
							if (row > 1)
							{
								OnActionProcessing("Processing CSV line.", filename, line.Substring(0, 40));
								System.Threading.Thread.Sleep(time < 0 ? 10 : time);

								int DICOMMergeFieldID = Int32.Parse(tok[0]);
								int MergeFieldID = Int32.Parse(tok[1]);
								int DICOMSRTemplateID = Int32.Parse(tok[2]);
								string Name = tok[3];
								string Description = tok[4];
								bool IsActive = tok[5].Equals("1");
								if (inactive)
								{
									IsActive = false;
								}
								else if (active)
								{
									IsActive = true;
								}
								string ValueXPath = tok[6];
								string UnitsXPath = tok[7];
								string FindingSite = tok[8];
								string Property = tok[9];
								string Modifier = tok[10];
								bool HasOBContext = tok[11].Equals("1");
								bool HasLateralityContext = tok[12].Equals("1");

								if (Name.Length <= 250 && Description.Length <= 2000)
								{
									var recs = _context.DICOMMergeFields.Where(x => x.MergeFieldID == MergeFieldID);
									if (recs == null || recs.Count() == 0)
									{
										MergeField mergeField = new MergeField();
										mergeField.MergeFieldID = MergeFieldID;
										mergeField.MergeFieldTypeID = 4; // this is a DICOM Merge Field in PS360
										mergeField.Name = Name;
										_context.MergeFields.InsertOnSubmit(mergeField);

										DICOMMergeField dicomMergeField = new DICOMMergeField();
										dicomMergeField.MergeFieldID = MergeFieldID;
										dicomMergeField.DICOMSRTemplateID = srTemplateID;
										dicomMergeField.Name = Name;
										dicomMergeField.Description = Description;
										dicomMergeField.IsActive = IsActive;
										dicomMergeField.ValueXPath = ValueXPath;
										dicomMergeField.UnitsXPath = UnitsXPath;
										dicomMergeField.FindingSite = FindingSite;
										dicomMergeField.Property = Property;
										dicomMergeField.Modifier = Modifier;
										dicomMergeField.HasOBContext = HasOBContext;
										dicomMergeField.HasLateralityContext = HasLateralityContext;
										_context.DICOMMergeFields.InsertOnSubmit(dicomMergeField);
										added++;
									}
									else
									{
										// update the name, description, or isactive flag only
										MergeField mf = _context.MergeFields.Where(x => x.MergeFieldID == MergeFieldID).First();
										mf.Name = Name;
										mf.Description = Description;

										DICOMMergeField dmf = recs.First();
										dmf.Name = Name;
										dmf.Description = Description;
										dmf.IsActive = IsActive;
										updated++;
									}
								}
								else
								{
									_logger.Warn("The Name or Description for the merge field was too long on row: " + row + ", it will be skipped");
									_logger.Warn("Name: " + Name);
								}
							}
						}
						else
						{
							_logger.Error("Row [" + row + "] is invalid");
							hasError = true;
							break;
						}
					}//end of while loop statement

					if (!hasError && !isCancelled)
					{
						_context.SubmitChanges();
						_logger.Info((row - 1) + " rows were processed from " + filename);
						_logger.Info(added + " DICOM Merge Fields were added");
						_logger.Info(updated + " DICOM Merge Fields were updated");
					}

					ThrowIfCancelled();
				}
			}
			catch (IOException ex)
			{
				ex.Data.Add("DICOM", filename);
				ex.Data.Add("ISACTIVE", active);
				throw;
			}
			catch (Exception ex)
			{
				ex.Data.Add("DICOM", filename);
				ex.Data.Add("ISACTIVE", active);
				throw;
			}
		}

		public void SetOnEntryProcessing(Action<string, string, object> onEntryProcessing)
		{
			if (onEntryProcessing != null)
				entryProcessing = onEntryProcessing;
		}

		protected int GetDeviceID()
		{
			ThrowIfCancelled();
			int result = 0;

			OnActionProcessing("Retrieving Device ID", null, null);

			var devices = _context.DICOMDevices.Where(x => x.Manufacturer.Equals(Utils.GetAppSetting("MANUFACTURER", "Philips Medical Systems")) &&
				x.Model.Equals(Utils.GetAppSetting("MODEL", "EPIQ 7C")) && x.Version.Equals(Utils.GetAppSetting("VERSION", "EPIQ 7G_1.0.0.2071")));

			if (devices == null || devices.Count() == 0)
			{
				DICOMDevice device = new DICOMDevice();
				device.Manufacturer = Utils.GetAppSetting("MANUFACTURER", "Philips Medical Systems");
				device.Model = Utils.GetAppSetting("MODEL", "EPIQ 7C");
				device.Version = Utils.GetAppSetting("VERSION", "EPIQ 7G_1.0.0.2071");
				_context.DICOMDevices.InsertOnSubmit(device);
				_context.SubmitChanges();
				result = device.DICOMDeviceID;

				_logger.Info("Successfully added device record for standard SR measurements.");
			}
			else
			{
				result = devices.First().DICOMDeviceID;
				_logger.Info("Found existing device record for standard SR measurements.");
			}

			return result;
		}

		protected int GetTemplateTypeID(string filename)
		{
			ThrowIfCancelled();
			int result = 0;
			OnActionProcessing("Retrieving TemplateTypeID", filename, null);
			// based on the file name, we will determine if the template is an OB, GYN, Adult Echo, or Vasc
			if (Path.GetFileName(filename).ToUpper().StartsWith("OB") || Path.GetFileName(filename).ToUpper().StartsWith("GYN"))
			{
				var templateTypes = _context.DICOMSRTemplateTypes.Where(x => x.Code.Equals(Utils.GetAppSetting("STD_OB_CODE", "125000")));
				if (templateTypes == null || templateTypes.Count() == 0)
				{
					DICOMSRTemplateType templateType = new DICOMSRTemplateType();
					templateType.Name = Utils.GetAppSetting("STD_OB_TEMP_NAME", "OB-GYN Ultrasound Procedure Report");
					templateType.Code = Utils.GetAppSetting("STD_OB_CODE", "125000");
					_context.DICOMSRTemplateTypes.InsertOnSubmit(templateType);
					_context.SubmitChanges();
					result = templateType.DICOMSRTemplateTypeID;
					_logger.Info("Successfully added " + templateType.Name + " SR template record.");
				}
				else
				{
					result = templateTypes.First().DICOMSRTemplateTypeID;
					_logger.Info("Found existing " + Utils.GetAppSetting("STD_OB_TEMP_NAME", "OB-GYN Ultrasound Procedure Report") + " SR template record.");
				}
			}
			else if (Path.GetFileName(filename).ToUpper().StartsWith("ADULTECHO"))
			{
				var templateTypes = _context.DICOMSRTemplateTypes.Where(x => x.Code.Equals(Utils.GetAppSetting("STD_ADULTECHO_CODE", "125200")));
				if (templateTypes == null || templateTypes.Count() == 0)
				{
					DICOMSRTemplateType templateType = new Data.DICOMSRTemplateType();
					templateType.Name = Utils.GetAppSetting("STD_ADULTECHO_TEMP_NAME", "Adult Echocardiography Procedure Report");
					templateType.Code = Utils.GetAppSetting("STD_ADULTECHO_CODE", "125200");
					_context.DICOMSRTemplateTypes.InsertOnSubmit(templateType);
					_context.SubmitChanges();
					result = templateType.DICOMSRTemplateTypeID;
					_logger.Info("Successfully added " + templateType.Name + " SR template record.");
				}
				else
				{
					result = templateTypes.First().DICOMSRTemplateTypeID;
					_logger.Info("Found existing " + Utils.GetAppSetting("STD_ADULTECHO_TEMP_NAME", "Adult Echocardiography Procedure Report") + " SR template record.");
				}

			}
			else if (Path.GetFileName(filename).ToUpper().StartsWith("VASC") || Path.GetFileName(filename).ToUpper().StartsWith("ABDO"))
			{
				var templateTypes = _context.DICOMSRTemplateTypes.Where(x => x.Code.Equals(Utils.GetAppSetting("STD_VASC_CODE", "125100")));
				if (templateTypes == null || templateTypes.Count() == 0)
				{
					DICOMSRTemplateType templateType = new DICOMSRTemplateType();
					templateType.Name = Utils.GetAppSetting("STD_VASC_TEMP_NAME", "Vascular Ultrasound Procedure Report");
					templateType.Code = Utils.GetAppSetting("STD_VASC_CODE", "125100");
					_context.DICOMSRTemplateTypes.InsertOnSubmit(templateType);
					_context.SubmitChanges();
					result = templateType.DICOMSRTemplateTypeID;
					_logger.Info("Successfully added " + templateType.Name + " SR template record.");
				}
				else
				{
					result = templateTypes.First().DICOMSRTemplateTypeID;
					_logger.Info("Found existing " + Utils.GetAppSetting("STD_VASC_TEMP_NAME", "Vascular Ultrasound Procedure Report") + " SR template record.");
				}
			}
			else
			{
				_logger.Error("Filename name is invalid. The filename must start with OB, GYN, AdultEcho, Vasc, or Abdo");
			}

			return result;
		}

		protected int GetTemplateID(string filename, int srTemplateID, int deviceID, DICOMMergeFieldElements dicomlist)
		{
			ThrowIfCancelled();

			int result = 0;
			string name = String.Empty;
			string description = String.Empty;

			// make the xml file loaded as a template
			DICOMSRTemplate templateSR = null;

			// based on the file name, we will determine if the template is an OB, GYN, Adult Echo, or Vasc
			XDocument xml = new XDocument();

			OnActionProcessing("Retrieving TemplateID", filename, null);

			if (Utils.GetAppSetting("DICOMMergeFieldGroup_Name_OB", "OB").Equals(dicomlist.Name, StringComparison.OrdinalIgnoreCase))
			{
				var templateSRs = _context.DICOMSRTemplates.Where(x => x.Name.Equals(Utils.GetAppSetting("STD_OB_NAME", "Standard SR for OB Measurements")));
				if (templateSRs == null || templateSRs.Count() == 0)
				{
					xml = XDocument.Load(Utils.GetAppSettingAndNormalizePath("STD_OB_FILE", ServiceLocator.Instance, "STD_OB.xml"));
					name = Utils.GetAppSetting("STD_OB_NAME", "Standard SR for OB Measurements");
					description = Utils.GetAppSetting("STD_OB_DESC", "Standard SR for OB Measurements");
				}
				else
				{
					templateSR = templateSRs.First();
				}
			}
			else if (Path.GetFileName(filename).IndexOf("GYN", StringComparison.OrdinalIgnoreCase) != -1)
			{
				var templateSRs = _context.DICOMSRTemplates.Where(x => x.Name.Equals(Utils.GetAppSetting("STD_GYN_NAME", "Standard SR for GYN Measurements")));
				if (templateSRs == null || templateSRs.Count() == 0)
				{
					xml = XDocument.Load(Utils.GetAppSettingAndNormalizePath("STD_GYN_FILE", ServiceLocator.Instance, "STD_GYN.xml"));
					name = Utils.GetAppSetting("STD_GYN_NAME", "Standard SR for GYN Measurements");
					description = Utils.GetAppSetting("STD_GYN_DESC", "Standard SR for GYN Measurements");
				}
				else
				{
					templateSR = templateSRs.First();
				}
			}
			else if (Path.GetFileName(filename).IndexOf("ADULTECHO", StringComparison.OrdinalIgnoreCase) != -1)
			{
				var templateSRs = _context.DICOMSRTemplates.Where(x => x.Name.Equals(Utils.GetAppSetting("STD_ADULTECHO_NAME", "Standard SR for Adult Echo Measurements")));
				if (templateSRs == null || templateSRs.Count() == 0)
				{
					xml = XDocument.Load(Utils.GetAppSettingAndNormalizePath("STD_ADULTECHO_FILE", ServiceLocator.Instance, "STD_ADULTECHO.xml"));
					name = Utils.GetAppSetting("STD_ADULTECHO_NAME", "Standard SR for Adult Echo Measurements");
					description = Utils.GetAppSetting("STD_ADULTECHO_DESC", "Standard SR for Adult Echo Measurements");
				}
				else
				{
					templateSR = templateSRs.First();
				}
			}
			else if (Path.GetFileName(filename).IndexOf("VASC", StringComparison.OrdinalIgnoreCase) != -1)
			{
				var templateSRs = _context.DICOMSRTemplates.Where(x => x.Name.Equals(Utils.GetAppSetting("STD_VASC_NAME", "Standard SR for Vascular Measurements")));
				if (templateSRs == null || templateSRs.Count() == 0)
				{
					xml = XDocument.Load(Utils.GetAppSettingAndNormalizePath("STD_VASC_FILE", ServiceLocator.Instance, "STD_VASC.xml"));
					name = Utils.GetAppSetting("STD_VACS_NAME", "Standard SR for Vascular Measurements");
					description = Utils.GetAppSetting("STD_VACS_DESC", "Standard SR for Vascular Measurements");
				}
				else
				{
					templateSR = templateSRs.First();
				}
			}
			else if (Path.GetFileName(filename).IndexOf("ABDO", StringComparison.OrdinalIgnoreCase) != -1)
			{
				var templateSRs = _context.DICOMSRTemplates.Where(x => x.Name.Equals(Utils.GetAppSetting("STD_ABDO_NAME", "Standard SR for Abdominal Measurements")));
				if (templateSRs == null || templateSRs.Count() == 0)
				{
					xml = XDocument.Load(Utils.GetAppSettingAndNormalizePath("STD_ABDO_FILE", ServiceLocator.Instance, "STD_Abdo.xml"));
					name = Utils.GetAppSetting("STD_ABDO_NAME", "Standard SR for Abdominal Measurements");
					description = Utils.GetAppSetting("STD_ABDO_DESC", "Standard SR for Abdominal Measurements");
				}
				else
				{
					templateSR = templateSRs.First();
				}
			}
			else
			{
				_logger.Error("Filename name is invalid. The filename must start with OB, GYN, AdultEcho, Vasc, or Abdo");
			}

			if (templateSR == null)
			{
				templateSR = new DICOMSRTemplate();
				templateSR.Name = name;
				templateSR.Description = description;
				templateSR.CreateDate = DateTime.Now;
				XElement sr = xml.Root;
				//if (sr == null)
				//	sr = new XElement("report");
				templateSR.SR = sr;
				templateSR.DICOMDeviceID = deviceID;
				templateSR.DICOMSRTemplateTypeID = srTemplateID;
				_context.DICOMSRTemplates.InsertOnSubmit(templateSR);
				_context.SubmitChanges(System.Data.Linq.ConflictMode.FailOnFirstConflict);
				_logger.Info("Successfully registered " + templateSR.Name + " for DICOM Merge Field management.");

			}
			result = templateSR.DICOMSRTemplateID;

			return result;
		}

		private void OnActionProcessing(string action, string file, object state)
		{
			if (this.entryProcessing != null)
			{
				entryProcessing(action, file, state);
			}
		}

		private void ThrowIfDisposed()
		{
			if (disposed)
				throw new ObjectDisposedException("Importer has benn disposed. Cannot access to the disposed object.");
		}

		private void ThrowIfCancelled()
		{
			if (isCancelled)
			{
				throw new DicomOperationCancelledException("Dicom workflow has been cancelled.");
			}
		}

		protected virtual void OnDisposose(bool disposing)
		{
			if (_context != null && disposing)
				_context.Dispose();
		}

		public void Dispose()
		{
			ThrowIfDisposed();

			OnDisposose(true);
			GC.SuppressFinalize(this);
		}
	}
}
