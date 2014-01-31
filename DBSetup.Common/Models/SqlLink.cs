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
	public class SqlLink : SectionBase
	{
		public SqlLink()
			: base()
		{
			Children = null;
			Content = null;
			Text = "SQL File";
		}

		private string _sqlPath = null;
		public string SqlFilePath
		{
			get { return _sqlPath; }
			set { _sqlPath = value; }
		}

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;

			if (object.ReferenceEquals(this, obj))
				return true;

			if (obj is SqlLink)
			{
				return string.Compare(SqlFilePath, (obj as SqlLink).SqlFilePath, StringComparison.OrdinalIgnoreCase) == 0;
			}

			return false;
		}

		public override int GetHashCode()
		{
			return SqlFilePath.Length;
		}

		public override string ToString()
		{
			return String.Format("SQL={0}", SqlFilePath);
		}
	}
}
