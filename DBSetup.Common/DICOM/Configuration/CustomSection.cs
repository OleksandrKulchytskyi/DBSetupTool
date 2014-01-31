using System;
using System.Configuration;
using System.Collections.Generic;

namespace DBSetup.Common.DICOM.Configuration
{
	public class CustomSection : ConfigurationSection
	{
		public const string SectionName = "DICOMCustomSection";
		private const string CollectionName = "DICOMMergeFieldGroups";

		[ConfigurationProperty(CollectionName)]
		[ConfigurationCollection(typeof(DICOMMergeFieldGroup), AddItemName = "DICOMMergeFieldGroup")]
		public DICOMMergeFieldGroup MergeFieldCollectionData
		{
			get
			{
				return (DICOMMergeFieldGroup)base[CollectionName];
			}
		}
	}

	public class DICOMMergeFieldGroup : ConfigurationElementCollection
	{
		protected override ConfigurationElement CreateNewElement()
		{
			return new DICOMMergeFieldGroupElement();
		}

		protected override object GetElementKey(ConfigurationElement element)
		{
			return ((DICOMMergeFieldGroupElement)element).Name;
		}
	}

	public class DICOMMergeFieldGroupElement : ConfigurationElement
	{
		[ConfigurationProperty("name", IsKey = true, DefaultValue = "OB", IsRequired = true)]
		public string Name
		{
			get { return (string)this["name"]; }
			set { this["name"] = value; }
		}

		[ConfigurationProperty("desc", IsRequired = false)]
		public string Description
		{
			get { return (string)this["desc"]; }
			set { this["desc"] = value; }
		}

		[ConfigurationProperty("csv", IsRequired = true)]
		public string CsvFileName
		{
			get { return (string)this["csv"]; }
			set { this["csv"] = value; }
		}

		[ConfigurationProperty("xml", IsRequired = true)]
		public string XmlFileName
		{
			get { return (string)this["xml"]; }
			set { this["xml"] = value; }
		}

	}

	public class DICOMMergeFieldElements
	{
		private string name;
		private string csvfilename;
		private string xmlfilename;
		private string description;

		public string Name
		{
			get { return name; }
			set { name = value; }
		}

		public string Csvfilename
		{
			get { return csvfilename; }
			set { csvfilename = value; }
		}

		public string Xmlfilename
		{
			get { return xmlfilename; }
			set { xmlfilename = value; }
		}

		public string Description
		{
			get { return description; }
			set { description = value; }
		}


	}

	public class MergeFieldUtils
	{
		public static List<DICOMMergeFieldElements> GetCollection()
		{
			List<DICOMMergeFieldElements> list = new List<DICOMMergeFieldElements>();
			try
			{

				var collection = ConfigurationManager.GetSection(CustomSection.SectionName) as CustomSection;
				if (collection != null)
				{
					foreach (DICOMMergeFieldGroupElement item in collection.MergeFieldCollectionData)
					{
						list.Add(new DICOMMergeFieldElements { Name = item.Name, Csvfilename = item.CsvFileName, Description = item.Description, Xmlfilename = item.XmlFileName });
					}
				}
			}
			catch (Exception ex)
			{
				Log.Instance.Error("Error occurred during getting MergeField collection", ex);
			}

			return list;
		}
	}

}
