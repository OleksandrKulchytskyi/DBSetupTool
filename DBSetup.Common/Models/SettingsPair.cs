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
			SettingKey = string.Empty;
			SettingValue = string.Empty;
		}

		public string SettingKey { get; set; }

		public string SettingValue { get; set; }

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;

			if (Object.ReferenceEquals(this, obj))
				return true;

			if (obj is SettingsPair)
			{
				return (this.SettingKey.Equals((obj as SettingsPair).SettingKey, StringComparison.OrdinalIgnoreCase)
						&& this.SettingValue.Equals((obj as SettingsPair).SettingValue, StringComparison.OrdinalIgnoreCase));
			}

			return false;
		}

		public override int GetHashCode()
		{
			return SettingValue.GetHashCode() + SettingKey.GetHashCode();
		}

		public override string ToString()
		{
			return string.Format("{0}={1}", SettingKey, SettingValue);
		}
	}
}
