using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common.Models
{
	public class SettingsPair : SectionBase
	{
		public SettingsPair()
			: base()
		{
			Text = "Configuration settings";
			Key = string.Empty;
			Value = string.Empty;
		}

		public string Key { get; set; }

		public string Value { get; set; }

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;

			if (Object.ReferenceEquals(this, obj))
				return true;

			if (obj is SettingsPair)
			{
				return (this.Key.Equals((obj as SettingsPair).Key, StringComparison.OrdinalIgnoreCase)
						&& this.Value.Equals((obj as SettingsPair).Value, StringComparison.OrdinalIgnoreCase));
			}

			return false;
		}

		public override int GetHashCode()
		{
			return Value.GetHashCode() + Key.GetHashCode();
		}

		public override string ToString()
		{
			return string.Format("{0}={1}", Key, Value);
		}
	}
}
