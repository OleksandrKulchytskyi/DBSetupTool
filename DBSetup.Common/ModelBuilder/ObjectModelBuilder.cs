using DBSetup.Common.Helpers;
using DBSetup.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DBSetup.Common.ModelBuilder
{
	/// <summary>
	/// This class performs all operations connected with building of tree view object model
	/// </summary>
	public class ObjectModelBuilder : IBuilder
	{
		private const string c_Languages = "Languages";
		private const string c_SetupConfigurations = "Setup Configurations"; //The name of root section for almost all active setup branches
		private readonly string[] ReadOnlySections = new string[] { "Languages", "Setup Configuration", "SQL Server Configurations", "SetupConfig", "Database Configuration" };

		private List<SectionBase> m_RootSections = null; //The list of tree view objects - the source for tree view
		private List<SectionBase> m_UnusedSections = null; //The list of unsused sections, which don't have root element
		private Dictionary<string, List<string>> m_sections; //Sections collection
		private Dictionary<string, List<string>> m_uhandledsections; //Sections which are unhandled yet
		private string m_currentProcessFile;
		string m_currentLanguage = string.Empty; //Language of current branch of tree

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
		/// Contains the list of fault sections
		/// </summary>
		public List<FaultSection> FaultSectionsList { get; set; }

		public ObjectModelBuilder()
		{
			LoadSql = false;
			LoadBLOB = false;
			LoadJava = false;
			FaultSectionsList = new List<FaultSection>();
		}

		public ObjectModelBuilder(Dictionary<string, List<string>> sections)
		{
			if (sections == null)
				throw new ArgumentNullException("sections");

			m_sections = sections;
			m_uhandledsections = new Dictionary<string, List<string>>(sections, StringComparer.OrdinalIgnoreCase); //Copy sections collection to temp collection
			FaultSectionsList = new List<FaultSection>();
		}

		public void SetIniFileSection(Dictionary<string, List<string>> sections)
		{
			if (sections == null)
				throw new ArgumentNullException("sections");

			m_sections = sections;
		}

		/// <summary>
		/// The main building flow of tree view object model 
		/// </summary>
		public void Build()
		{
			//Initialize sections list object
			m_RootSections = new List<SectionBase>();
			m_UnusedSections = new List<SectionBase>();
			foreach (string secName in m_sections.Keys)
			{
				//Do not build tree for section that was already added as a child
				if (!m_uhandledsections.ContainsKey(secName))
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
				section.FileName = m_currentProcessFile;

				//check for section which must be ReadOnly
				if (ReadOnlySections.Any(x => secName.Contains(x)))
					section.ReadOnly = true;
				if (m_sections[secName].Any(x => x.Contains("ActiveSection=")))
					section.ReadOnly = true;

				//Build section object tree
				BuildChildren(section.Text, section);
				//Remove all the childs of sections, that could be added earlier to collection
				foreach (SectionBase sb in section.Children)
					if (m_RootSections.Contains(sb)) m_RootSections.Remove(sb);


				//Add only root sections to final collection, otherwise add section to unused section collection
				if (section.Text.Contains(c_Languages) || section.Text.Contains(c_SetupConfigurations))
					m_RootSections.Add(section); //Add section to final sections collection
				else
				{
					section.ReadOnly = false;
					m_UnusedSections.Add(section);
				}

			}
			//If there are one or more unused section - add them to final sections collections as unused
			if (m_UnusedSections.Count > 0)
			{
				SectionBase sb = new SectionBase()
				{
					Parent = null,
					Content = "All unused sections",
					Text = "Unused Sections",
					FileName = m_currentProcessFile,
					Children = m_UnusedSections
				};
				m_RootSections.Add(sb);
			}
		}

		/// <summary>
		/// This function uses the recursion call of itself to build the complete tree view object model
		/// </summary>
		/// <param name="secName">Section name to process</param>
		/// <param name="sectionObject">Section object in current state</param>
		private void BuildChildren(string secName, SectionBase sectionObject)
		{
			if (m_sections.ContainsKey(secName))
			{
				m_uhandledsections.Remove(secName); //Remove this section from unused sections collection
				sectionObject.Children = sectionObject.Children ?? new List<SectionBase>();
				//All subsections of current section should be added as its childs list
				foreach (string subName in m_sections[secName])
				{
					if (string.IsNullOrEmpty(subName))
						continue;

					//Define the type of child object
					//Fot sectionLink object we should recursively call BuildChildren() until last child is found
					switch (LineParser.getLineType(subName))
					{
						case LineType.SectionLink:
							string sectionName = LineParser.GetKeyValueFromString(subName, false).Value;
							if (m_sections.ContainsKey(sectionName) || m_sections.ContainsKey(string.Format("{0} {1}", sectionName, m_currentLanguage)))
							{
								SectionBase subSection = new SectionBase();
								subSection.Parent = sectionObject;
								subSection.FileName = m_currentProcessFile;
								subSection.Text = m_sections.ContainsKey(sectionName) ? sectionName : string.Format("{0} {1}", sectionName, m_currentLanguage);

								if (m_sections[subSection.Text].Any(x => x.Contains("ActiveSection=")))
									subSection.ReadOnly = true;

								//If section has circle link to parent - make it type Faultsection
								//This should prevent stack overflow problem
								if (!sectionObject.IsParentsHaveText(sectionName))
								{
									sectionObject.Children.Add(subSection);
									BuildChildren(subSection.Text, subSection);
								}
								else
								{
									FaultSection fs = new FaultSection()
									{
										Parent = subSection.Parent,
										Text = subSection.Text,
										FileName = m_currentProcessFile
									};
									FaultSectionsList.Add(fs);
									sectionObject.Children.Add(fs);
								}
							}
							//Check if we already in Languages sections and curent section name is Language
							//we add language to Languages section
							else if (secName.Contains("Language"))
							{
								SectionBase languageSec = new SectionBase();
								languageSec.Content = string.Empty;
								languageSec.FileName = m_currentProcessFile;
								languageSec.Parent = sectionObject;
								languageSec.Children = null; languageSec.Text = sectionName;
								sectionObject.Children.Add(languageSec);
							}
							break;
						case LineType.SqlLink:
							if (!LoadSql)
								break;
							SqlLink sql = new SqlLink();
							sql.SqlFilePath = LineParser.GetKeyValueFromString(subName).Value;
							sql.Parent = sectionObject;
							sql.FileName = m_currentProcessFile;
							sectionObject.Children.Add(sql);
							break;

						case LineType.BlobLink:
							if (!LoadBLOB)
								break;
							BlobLink blob = new BlobLink();
							var BlobPair = LineParser.ParseBlobString(LineParser.GetKeyValueFromString(subName).Value);
							blob.BlobFilePath = BlobPair.Key;
							blob.BlobContent = BlobPair.Value;
							blob.FileName = m_currentProcessFile;
							blob.Parent = sectionObject;
							sectionObject.Children.Add(blob);
							break;

						case LineType.DICOM:
							if (!LoadDICOM)
								break;

							DICOMLink dicom = new DICOMLink();
							var dicomPair = LineParser.ParseDicomString(LineParser.GetKeyValueFromString(subName).Value);
							dicom.CSVFilePath = dicomPair.Key;
							dicom.IsActive = dicomPair.Value.Equals("1");
							dicom.FileName = m_currentProcessFile;
							dicom.Parent = sectionObject;
							sectionObject.Children.Add(dicom);
							break;

						case LineType.JavaLink:
							if (!LoadJava)
								break;
							JavaLink java = new JavaLink();
							java.JavaFilePath = LineParser.GetKeyValueFromString(subName).Value;
							java.Parent = sectionObject;
							java.FileName = m_currentProcessFile;
							sectionObject.Children.Add(java);
							break;

						case LineType.IniLink:
							IniFileLink ini = new IniFileLink();
							ini.Children = ini.Children ?? new List<SectionBase>();
							ini.Children.Add(new FakeSection());
							var pair = LineParser.ParseIniString(LineParser.GetKeyValueFromString(subName).Value);
							ini.IniSection = pair.Value;
							ini.IniFilePath = pair.Key;
							ini.Parent = sectionObject;
							ini.FileName = m_currentProcessFile;
							sectionObject.Children.Add(ini);
							break;
						default:
							break;
					}
				}
				sectionObject.Content = m_sections[secName].GenerateStringFromList();
			}
		}

		/// <summary>
		/// Returns the section tree objects list. Each object in list is a separate branch.
		/// </summary>
		/// <returns></returns>
		public List<SectionBase> GetResult()
		{
			return m_RootSections;
		}

		/// <summary>
		/// Build section tree from another ini file
		/// </summary>
		/// <param name="content"></param>
		/// <param name="sectionName"></param>
		/// <returns></returns>
		public SectionBase ConvertIniToSection(Dictionary<string, List<string>> content, string sectionName, SectionBase parent)
		{
			if (content == null)
				throw new ArgumentNullException("content");

			if (string.IsNullOrEmpty(sectionName))
				throw new ArgumentNullException("sectionName");

			if (parent == null)
				throw new ArgumentNullException("parent");

			if (content.ContainsKey(sectionName))
			{
				SectionBase root = new SectionBase();
				root.Parent = parent;
				root.FileName = m_currentProcessFile;
				root.Text = sectionName;
				root.Children = root.Children ?? new List<SectionBase>();

				BuildChildObjects(root, content);
				content.Clear();
				content = null;
				return root;
			}

			return null;
		}

		private void BuildChildObjects(SectionBase sectionObject, Dictionary<string, List<string>> content)
		{
			foreach (string subName in content[sectionObject.Text])
			{
				if (string.IsNullOrEmpty(subName))
					continue;

				//Define the type of child object
				//Fot sectionLink object we should recursively call BuildChildren() until last child is found
				switch (LineParser.getLineType(subName))
				{
					case LineType.SectionLink:
						string sectionName = LineParser.GetKeyValueFromString(subName, false).Value;
						if (content.ContainsKey(sectionName) || content.ContainsKey(string.Format("{0} {1}", sectionName, m_currentLanguage)))
						{
							SectionBase subSection = new SectionBase();
							subSection.Parent = sectionObject;
							subSection.Text = content.ContainsKey(sectionName) ? sectionName : string.Format("{0} {1}", sectionName, m_currentLanguage);
							subSection.FileName = m_currentProcessFile;
							//If section has circle link to parent - make it type Faultsection
							//This should prevent stack overflow problem
							if (!sectionObject.IsParentsHaveText(sectionName))
							{
								sectionObject.Children.Add(subSection);
								BuildChildObjects(subSection, content);
							}
							else
							{
								sectionObject.Children.Add(new FaultSection()
								{
									Parent = subSection.Parent,
									Text = subSection.Text,
									FileName = m_currentProcessFile,
									Content = null,
									Children = null
								});
							}
						}
						//Check if we already in Languages sections and curent section name is Language
						//we add language to Languages section
						else if (sectionObject.Text.Contains("Language"))
						{
							SectionBase sb = new SectionBase();
							sb.Content = string.Empty;
							sb.Parent = sectionObject;
							sb.Children = null; sb.Text = sectionName;
							sb.FileName = m_currentProcessFile;
							sectionObject.Children.Add(sb);
						}
						break;
					case LineType.SqlLink:
						if (!LoadSql)
							break;
						SqlLink sql = new SqlLink();
						sql.SqlFilePath = LineParser.GetKeyValueFromString(subName).Value;
						sql.Parent = sectionObject;
						sql.FileName = m_currentProcessFile;
						sectionObject.Children.Add(sql);
						break;

					case LineType.IniLink:
						IniFileLink ini = new IniFileLink();
						ini.Children = ini.Children ?? new List<SectionBase>();
						ini.Children.Add(new FakeSection());
						var pair = LineParser.ParseIniString(LineParser.GetKeyValueFromString(subName).Value);
						ini.IniSection = pair.Value;
						ini.IniFilePath = pair.Key;
						ini.Parent = sectionObject;
						ini.FileName = m_currentProcessFile;
						if (sectionObject.Children == null) //Temporary fix - need to clarify why children property is null here
							sectionObject.Children = new List<SectionBase>();
						sectionObject.Children.Add(ini);
						break;

					case LineType.JavaLink:
						if (!LoadJava)
							break;
						JavaLink java = new JavaLink();
						java.JavaFilePath = LineParser.GetKeyValueFromString(subName).Value;
						java.Parent = sectionObject;
						java.FileName = m_currentProcessFile;
						sectionObject.Children.Add(java);
						break;

					case LineType.BlobLink:
						if (!LoadBLOB)
							break;
						BlobLink blob = new BlobLink();
						var BlobPair = LineParser.ParseBlobString(LineParser.GetKeyValueFromString(subName).Value);
						blob.BlobFilePath = BlobPair.Key;
						blob.BlobContent = BlobPair.Value;
						blob.FileName = m_currentProcessFile;
						blob.Parent = sectionObject;
						sectionObject.Children.Add(blob);
						break;

					default:
						break;
				}
			}
			sectionObject.Content = content[sectionObject.Text].GenerateStringFromList();
		}

		/// <summary>
		/// Set file which currently will be processed
		/// </summary>
		/// <param name="fileName"></param>
		public void SetCurrentProcessingFile(string fileName)
		{
			if (string.IsNullOrEmpty(fileName))
				throw new ArgumentNullException("fileName");

			m_currentProcessFile = fileName;
		}

		/// <summary>
		/// Build List of objects by text
		/// </summary>
		/// <param name="parent"></param>
		/// <param name="data"></param>
		/// <remarks>Not included building of section sub content</remarks>
		/// <returns></returns>
		public List<SectionBase> BuildByText(SectionBase parent, Dictionary<string, List<string>> data)
		{
			if (data == null || data.Keys.Count == 0)
				return null;

			List<SectionBase> resultList = new List<SectionBase>();
			foreach (string subName in data[data.Keys.First()])
			{
				if (string.IsNullOrEmpty(subName))
					continue;

				//Define the type of child object
				//Fot sectionLink object we should recursively call BuildChildren() until last child is found
				switch (LineParser.getLineType(subName))
				{
					case LineType.SectionLink:
						string sectionName = LineParser.GetKeyValueFromString(subName, false).Value;
						if (parent.IsParentsHaveText(sectionName))
						{
							//If section has circle link to parent - make it type Faultsection
							//This should prevent stack overflow problem
							FaultSection fault = new FaultSection()
							{
								Parent = parent,
								Text = sectionName,
								FileName = parent.FileName,
								Content = null,
								Children = null
							};
							resultList.Add(fault);
							continue;
						}
						else
						{
							SectionBase subSection = new SectionBase();
							subSection.Parent = parent;
							subSection.Text = sectionName;
							subSection.FileName = parent.FileName;
							if (m_sections.ContainsKey(sectionName))
							{
								BuildChildObjects(subSection, m_sections);
							}

							resultList.Add(subSection);
						}
						break;

					case LineType.SqlLink:
						if (!LoadSql)
							break;
						SqlLink sql = new SqlLink();
						sql.Text = "SQL File";
						sql.SqlFilePath = LineParser.GetKeyValueFromString(subName).Value;
						sql.Parent = parent;
						sql.FileName = parent.FileName;
						resultList.Add(sql);
						break;

					case LineType.JavaLink:
						if (!LoadJava)
							break;
						JavaLink java = new JavaLink();
						java.JavaFilePath = LineParser.GetKeyValueFromString(subName).Value;
						java.Parent = parent;
						java.FileName = parent.FileName;
						resultList.Add(java);
						break;

					case LineType.BlobLink:
						if (!LoadBLOB)
							break;
						BlobLink blob = new BlobLink();
						var BlobPair = LineParser.ParseBlobString(LineParser.GetKeyValueFromString(subName).Value);
						blob.BlobFilePath = BlobPair.Key;
						blob.BlobContent = BlobPair.Value;
						blob.FileName = m_currentProcessFile;
						blob.Parent = parent;
						resultList.Add(blob);
						break;

					case LineType.IniLink:
						IniFileLink ini = new IniFileLink();
						ini.Text = "Ini File";
						ini.Children = ini.Children ?? new List<SectionBase>();
						ini.Children.Add(new FakeSection());
						var pair = LineParser.ParseIniString(LineParser.GetKeyValueFromString(subName).Value);
						ini.IniSection = pair.Value;
						ini.IniFilePath = pair.Key;
						ini.Parent = parent;
						ini.FileName = parent.FileName;
						resultList.Add(ini);
						break;
					default:
						break;
				}
			}
			return resultList;
		}
	}
}
