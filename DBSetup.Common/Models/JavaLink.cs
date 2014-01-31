using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common.Models
{
	/// <summary>
	/// Additional section of tree view object model. Represents the link to external SQL Data file
	/// </summary>
	[Serializable]
	public class JavaLink : SectionBase
	{
		public JavaLink()
			: base()
		{
			Children = null;
			Content = null;
			Text = "Java File";
		}

		private string _javaPath = null;
		public string JavaFilePath
		{
			get { return _javaPath; }
			set { _javaPath = value; }
		}

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;

			if (object.ReferenceEquals(this, obj))
				return true;

			if (obj is JavaLink)
			{
				return string.Compare(JavaFilePath, (obj as JavaLink).JavaFilePath, StringComparison.OrdinalIgnoreCase) == 0;
			}

			return false;
		}

		public override int GetHashCode()
		{
			return JavaFilePath.Length;
		}

		public override string ToString()
		{
			return string.Format("Java={0}", JavaFilePath);
		}
	}
}
