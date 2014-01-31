using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common.Models
{
	/// <summary>
	/// Additional section of tree view object mode. Represents the link to external file with links to INI data files
	/// </summary>
	[Serializable]
	public class IniFileLink : SectionBase
	{
		public IniFileLink()
			: base()
		{
			Children = null;
			Content = null;
			Text = "Ini File";
		}

		private string _iniPath = null;
		public string IniFilePath
		{
			get { return _iniPath; }
			set { _iniPath = value; }
		}

		private string _iniSection = null;
		public string IniSection
		{
			get { return _iniSection; }
			set { _iniSection = value; }
		}

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;

			if (object.ReferenceEquals(this, obj))
				return true;

			if (obj is IniFileLink)
			{
				return ((string.Compare(IniFilePath, (obj as IniFileLink).IniFilePath, StringComparison.OrdinalIgnoreCase) == 0)
					&& (string.Compare(IniSection, (obj as IniFileLink).IniSection, StringComparison.OrdinalIgnoreCase) == 0));
			}
			return false;
		}

		public override int GetHashCode()
		{
			return IniSection.Length + IniFilePath.Length;
		}

		public override string ToString()
		{
			return string.Format("Include={0}, {1}", IniFilePath, IniSection);
		}
	}
}
