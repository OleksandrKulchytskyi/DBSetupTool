using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common.Models
{
	public class Language : SectionBase
	{
		public Language()
			: base()
		{
			Text = "Language";
			LanguageName = string.Empty;
		}

		public string LanguageName { get; set; }

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;
			else if (object.ReferenceEquals(this, obj))
				return true;

			else if (obj is Comment)
			{
				return this.LanguageName.Equals((obj as Language).LanguageName, StringComparison.OrdinalIgnoreCase);
			}

			return false;
		}

		public override int GetHashCode()
		{
			return LanguageName.GetHashCode();
		}

		public override string ToString()
		{
			return LanguageName;
		}
	}
}
