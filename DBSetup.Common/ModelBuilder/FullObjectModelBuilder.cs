using DBSetup.Common.Helpers;
using DBSetup.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common.ModelBuilder
{
	/// <summary>
	/// this class represent full build module, which insted of Ini objects bild real sections from other ini sections
	/// </summary>
	public class FullModelBuilder : IBuilder
	{
		private const string c_Languages = "Languages";
		private const string c_SetupConfigurations = "Setup Configurations"; //The name of root section for almost all active setup branches
		private readonly string[] ReadOnlySections = new string[] { "Languages", "Setup Configuration", "SQL Server Configurations", "SetupConfig", "Database Configuration" };
		string m_currentLanguage = string.Empty; //Language of current branch of tree

		private ISectionHandlerFactory handlersFactory;

		private List<SectionBase> _rootSections; //The list of tree view objects - the source for tree view
		private List<SectionBase> _unusedSections;//List of tree view objects,that wasn't used in object tree
		private List<string> _filesToOpen;
		private Dictionary<string, List<string>> m_unhandledsections;
		private string _rootFolder;
		private string _rootOpenedFile;
		private Dictionary<string, Dictionary<string, List<string>>> m_filesSectionsData;

		#region Loading properties
		/// <summary>
		/// Flag that indicates, if there are such need to load sql section in the objects tree
		/// </summary>
		public bool LoadSql { get; set; }

		/// <summary>
		/// Flag that indicates, if there are such need to load Java section in the objects tree
		/// </summary>
		public bool LoadJava { get; set; }

		/// <summary>
		/// Flag that indicates, if there are such need to load BLOB links in the objects tree
		/// </summary>
		public bool LoadBLOB { get; set; }

		/// <summary>
		/// Flag that indicates, if there are such need to load DICOM links in the objects tree
		/// </summary>
		public bool LoadDICOM { get; set; }

		/// <summary>
		/// Flag that indicates, if there are such need to load comment lines in the objects tree
		/// </summary>
		public bool LoadComments { get; set; }
		#endregion

		public FullModelBuilder()
		{
			m_filesSectionsData = new Dictionary<string, Dictionary<string, List<string>>>();
			_filesToOpen = new List<string>();
			if (FaultSectionsList == null)
				FaultSectionsList = new List<FaultSection>();

			SetBuildAll();
			LoadComments = false;
			handlersFactory = DBSetup.Common.ServiceLocator.Instance.GetService<ISectionHandlerFactory>();
		}

		public void SetBuildAll()
		{
			LoadBLOB = true;
			LoadJava = true;
			LoadSql = true;
			LoadDICOM = true;
		}

		public void OpenFile(string filePath)
		{
			if (string.IsNullOrEmpty(filePath))
				throw new ArgumentNullException("fileName");

			if (!System.IO.File.Exists(filePath))
				throw new System.IO.FileNotFoundException("This file is not found on the machine", filePath);

			m_filesSectionsData[filePath] = IniFileParser.GetSectionsDictionary(filePath);
			_rootOpenedFile = filePath;

			_rootFolder = System.IO.Path.GetDirectoryName(filePath);
			m_unhandledsections = new Dictionary<string, List<string>>(m_filesSectionsData[filePath]);
			_filesToOpen.Add(filePath);
		}

		public List<FaultSection> FaultSectionsList
		{
			get;
			set;
		}

		public void Build()
		{
			//Initialize sections list object
			if (_rootSections != null && _rootSections.Count > 0)
			{ _rootSections.Clear(); _rootSections = null; }

			_rootSections = new List<SectionBase>();

			if (_unusedSections != null && _unusedSections.Count > 0)
			{ _unusedSections.Clear(); _unusedSections = null; }

			_unusedSections = new List<SectionBase>();

			foreach (string secName in m_filesSectionsData[_rootOpenedFile].Keys)
			{
				//Do not build tree for section that was already added as a child
				if (!m_unhandledsections.ContainsKey(secName))
					continue;

				//Current language should be empty until it is defined
				m_currentLanguage = string.Empty;

				//Cut language name from "setup configuration" section
				if (secName.Contains(c_SetupConfigurations) && (c_SetupConfigurations.Length < secName.Length))
				{
					int ind = secName.IndexOf(c_SetupConfigurations);
					m_currentLanguage = secName.Substring(ind + c_SetupConfigurations.Length + 1);
				}
				//Initialize section
				SectionBase section = new SectionBase() { Text = secName, Children = new List<SectionBase>(), Parent = null };
				section.FileName = _rootOpenedFile;

				//check for section which must be ReadOnly
				if (ReadOnlySections.Any(x => secName.Contains(x)))
					section.ReadOnly = true;
				if (m_filesSectionsData[_rootOpenedFile][secName].Any(x => x.Contains("ActiveSection=")))
					section.ReadOnly = true;

				//Build section object tree
				BuildChildren(_rootOpenedFile, section.Text, section);
				//Remove all the childs of sections, that could be added earlier to collection
				foreach (SectionBase sb in section.Children)
					if (_rootSections.Contains(sb)) _rootSections.Remove(sb);

				//Add only root sections to final collection, otherwise add section to unused section collection
				if (section.Text.Contains(c_Languages) || section.Text.Contains(c_SetupConfigurations))
					_rootSections.Add(section); //Add section to final sections collection
				else
				{
					section.ReadOnly = false;
					_unusedSections.Add(section);
				}
			}

			//If there are one or more unused section - add them to final sections collections as unused
			if (_unusedSections.Count > 0)
			{
				SectionBase sb = new SectionBase()
				{
					Parent = null,
					Content = "All unused sections",
					Text = "Unused Sections",
					FileName = _rootOpenedFile,
					Children = _unusedSections
				};
				_rootSections.Add(sb);
			}
		}

		/// <summary>
		/// This function uses the recursion call of itself to build the complete tree view object model
		/// </summary>
		/// <param name="secName">Section name to process</param>
		/// <param name="sectionObject">Section object in current state</param>
		private void BuildChildren(string fileName, string secName, SectionBase sectionObject)
		{
			if (!m_filesSectionsData.ContainsKey(fileName))
			{
				if (!_filesToOpen.Contains(fileName))
					_filesToOpen.Add(fileName);

				m_filesSectionsData[fileName] = IniFileParser.GetSectionsDictionary(fileName);
			}

			if (m_filesSectionsData[fileName].ContainsKey(secName))
			{
				m_unhandledsections.Remove(secName); //Remove this section from unused sections collection
				sectionObject.Children = sectionObject.Children ?? new List<SectionBase>();
				//All subsections of current section should be added as its childs list
				int ubnormalIndx = -1;
				foreach (string subName in m_filesSectionsData[fileName][secName])
				{
					if (string.IsNullOrEmpty(subName))
						continue;

					//the next scoe of code defines the type of child object which will be added to object tree
					//Fot sectionLink object we should recursively call BuildChildren() until last child will be found
					LineType typeOfLine = LineParser.getLineType(subName);
					switch (typeOfLine)
					{
						case LineType.Language:
							Language language = new Language();
							language.LanguageName = LineParser.GetKeyValueFromString(subName).Value;
							language.Parent = sectionObject;
							language.FileName = fileName;
							sectionObject.Children.Add(language);
							break;

						case LineType.Comment:
							if (!LoadComments)
								break;
							Comment comment = new Comment();
							comment.CommentLine = subName;
							comment.Parent = sectionObject;
							comment.FileName = fileName;
							sectionObject.Children.Add(comment);
							break;

						case LineType.SectionLink:
							string sectionName = LineParser.GetKeyValueFromString(subName, false).Value;
							if (m_filesSectionsData[fileName].ContainsKey(sectionName) ||
								m_filesSectionsData[fileName].ContainsKey(string.Format("{0} {1}", sectionName, m_currentLanguage)))
							{
								SectionBase subSection = new SectionBase();
								subSection.Parent = sectionObject;
								subSection.FileName = fileName;
								subSection.Text = m_filesSectionsData[fileName].ContainsKey(sectionName) ? sectionName : string.Format("{0} {1}", sectionName, m_currentLanguage);

								if (m_filesSectionsData[fileName][subSection.Text].Any(x => x.Contains("ActiveSection=")))
									subSection.ReadOnly = true;

								//If section has circle link to parent - make it type Faultsection
								//This should prevent stack overflow problem
								if (!sectionObject.IsParentsHaveText(sectionName))
								{
									sectionObject.Children.Add(subSection);
									BuildChildren(fileName, subSection.Text, subSection);
								}
								else
								{
									FaultSection fs = new FaultSection()
									{
										Parent = subSection.Parent,
										Text = subSection.Text,
										FileName = fileName
									};
									FaultSectionsList.Add(fs);
									sectionObject.Children.Add(fs);
								}
							}
							//Check if we are already in Languages sections and curent section name is Language 
							//we add language to Languages section
							else if (secName.Contains("Language"))
							{
								SectionBase languageSec = new SectionBase();
								languageSec.Content = string.Empty;
								languageSec.FileName = fileName;
								languageSec.Parent = sectionObject;
								languageSec.Children = null; languageSec.Text = sectionName;
								sectionObject.Children.Add(languageSec);
							}
							break;
						case LineType.SqlLink:
							if (!LoadSql) break;

							SqlLink sql = new SqlLink();
							string sqlPath = LineParser.GetKeyValueFromString(subName).Value;
							NormalizePath(ref ubnormalIndx, ref sqlPath);

							sql.SqlFilePath = sqlPath;
							sql.Parent = sectionObject;
							sql.FileName = fileName;
							sql.Handler = handlersFactory.CreateByType(typeOfLine, null, null);
							sectionObject.Children.Add(sql);
							break;

						case LineType.JavaLink:
							if (!LoadJava) break;

							JavaLink java = new JavaLink();
							java.JavaFilePath = LineParser.GetKeyValueFromString(subName).Value;
							java.Parent = sectionObject;
							java.FileName = fileName;
							sectionObject.Children.Add(java);
							break;

						case LineType.BlobLink:
							if (!LoadBLOB) break;

							BlobLink blob = new BlobLink();
							var BlobPair = LineParser.ParseBlobString(LineParser.GetKeyValueFromString(subName).Value);
							blob.BlobFilePath = BlobPair.Key;
							blob.BlobContent = BlobPair.Value;
							blob.FileName = fileName;
							blob.Parent = sectionObject;
							sectionObject.Children.Add(blob);
							break;


						case LineType.DICOM:
							if (!LoadDICOM) break;

							DICOMLink dicom = new DICOMLink();
							var dicomPair = LineParser.ParseDicomString(LineParser.GetKeyValueFromString(subName).Value);
							string csvPath = dicomPair.Key;
							NormalizePath(ref ubnormalIndx, ref csvPath);
							dicom.CSVFilePath = csvPath;
							dicom.IsActive = dicomPair.Value.Equals("1");
							dicom.FileName = fileName;
							dicom.Parent = sectionObject;
							dicom.Handler =handlersFactory.CreateByType(typeOfLine, null, null);
							//dicom.Handler = new DICOM.DicomSectionHandler();
							sectionObject.Children.Add(dicom);
							break;

						case LineType.IniLink:
							var pair = LineParser.ParseIniString(LineParser.GetKeyValueFromString(subName).Value);
							string iniSecName = pair.Value;
							string iniPath = pair.Key;
							NormalizePath(ref ubnormalIndx, ref iniPath);

							SectionBase iniSection = new SectionBase();
							iniSection.Parent = sectionObject;
							iniSection.FileName = iniPath;
							iniSection.Text = iniSecName;
							BuildChildren(iniPath, iniSecName, iniSection);
							if (!sectionObject.IsParentsHaveText(iniSection))
								sectionObject.Children.Add(iniSection);
							else
							{
								FaultSection fs = new FaultSection()
								{
									Parent = sectionObject,
									Text = iniSecName,
									FileName = iniPath
								};
								FaultSectionsList.Add(fs);
								sectionObject.Children.Add(fs);
							}
							break;

						case LineType.SettingsPair:
							if (sectionObject.Text.IndexOf("Configurations", StringComparison.OrdinalIgnoreCase) > 0)
							{
								SettingsPair setPair = new SettingsPair();
								var keyValPair = LineParser.GetKeyValueFromString(subName);
								setPair.Key = keyValPair.Key;
								setPair.Value = keyValPair.Value;
								sectionObject.Children.Add(setPair);
							}
							break;

						default:
							break;
					}
				}
				sectionObject.Content = m_filesSectionsData[fileName][secName].GenerateStringFromList();
			}
		}

		private void NormalizePath(ref int ubnormalIndx, ref string filePath)
		{
			filePath = filePath.StartsWith(".\\") ? System.IO.Path.Combine(_rootFolder, filePath) : filePath;

			ubnormalIndx = filePath.IndexOf(@"\.\", StringComparison.OrdinalIgnoreCase);
			filePath = ubnormalIndx > 1 ? filePath.Replace(@"\.\", @"\") : filePath;
		}

		public List<SectionBase> GetResult()
		{
			return _rootSections;
		}

		public List<string> GetFilesToOpen() { return _filesToOpen; }
	}
}
