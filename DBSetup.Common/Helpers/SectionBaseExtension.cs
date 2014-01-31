using DBSetup.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DBSetup.Common.Helpers
{
	public static class SectionBaseExtension
	{
		/// <summary>
		/// Go throught all parents of section and search for specified text/section name
		/// </summary>
		/// <param name="section"></param>
		/// <param name="textToSearch">Section name to find</param>
		/// <returns>true if text found</returns>
		/// <exception cref="System.ArgumentNullException" />
		public static bool IsParentsHaveText(this SectionBase section, string textToSearch)
		{
			if (section == null)
				throw new ArgumentNullException("section");
			if (string.IsNullOrEmpty(textToSearch))
				throw new ArgumentNullException("textToSearch");

			bool result = false;
			if (string.Compare(section.Text, textToSearch, StringComparison.OrdinalIgnoreCase) == 0)
				return true;

			SectionBase parent = section.Parent;
			while (parent != null)
			{
				if (string.Compare(parent.Text, textToSearch, StringComparison.OrdinalIgnoreCase) == 0)
				{
					result = true;
					break;
				}
				parent = parent.Parent;
			}
			return result;
		}

		/// <summary>
		/// Go throught all parents of section and search for specified text/section name
		/// In case when copied/moved section contains children, it also go through all children Sections
		/// </summary>
		/// <param name="section"></param>
		/// <param name="textToSearch">Section name to find</param>
		/// <returns>true if text found</returns>
		/// <exception cref="System.ArgumentNullException" />
		public static bool IsParentsHaveText(this SectionBase section, SectionBase searched)
		{
			if (section == null)
				throw new ArgumentNullException("section");
			if (searched == null)
				throw new ArgumentNullException("textToSearch");

			if (searched.Children == null || section.Children.Count == 0 || section.Children.OfType<SectionBase>().Count() == 0)
			{
				return section.IsParentsHaveText(searched.Text);
			}
			else
			{
				var listData = searched.ChildrenSectionNames();
				foreach (string secName in listData)
				{
					if (section.IsParentsHaveText(secName))
						return true;
				}
				return false;
			}
		}

		private static List<string> ChildrenSectionNames(this SectionBase section)
		{
			if (section == null)
				return null;

			List<string> resultList = new List<string>();

			resultList.Add(section.Text);
			foreach (SectionBase subSec in section.Children)
			{
				if (string.Compare(subSec.GetType().Name, "SectionBase", true) == -1
					|| string.Compare(subSec.GetType().Name, "FaultSection", true) == -1)
					continue;

				resultList.Add(subSec.Text);
				_getChildremNames(resultList, subSec);
			}
			return resultList;
		}

		private static void _getChildremNames(List<string> result, SectionBase section)
		{
			if (section == null || result == null || section.Children == null)
				return;

			foreach (SectionBase subSec in section.Children)
			{
				if (string.Compare(subSec.GetType().Name, "SectionBase", true) == -1
					|| string.Compare(subSec.GetType().Name, "FaultSection", true) == -1)
					continue;

				result.Add(subSec.Text);
				if (subSec.Children != null && subSec.Children.Count != 0)
				{
					_getChildremNames(result, subSec);
				}
			}
		}

		public static SectionBase GetProperUpgrade(List<SectionBase> setups, int commVersion)
		{
			if (setups == null)
				return null;
			string[] keywords = null;

			if (System.Configuration.ConfigurationManager.AppSettings["updateKeywords"] != null)
				keywords = System.Configuration.ConfigurationManager.AppSettings["updateKeywords"].Split(',');
			else
				keywords = new string[2] { "Update", "Upgrade" };

			foreach (var setup in setups)
			{
				if (ContainsWord(setup.Text, keywords))
				{
					string[] numbers = Regex.Split(setup.Text, @"\D+");

					if (numbers != null)
					{
						List<int> data = new List<int>(2);
						int previous = -1;
						foreach (string m in numbers)
						{	//small trick if version is equals 360 , obviously regex get nomber from PS360, omit it
							if (m.Equals("360")) continue;

							int version;
							if (Int32.TryParse(m, out version) && previous != version)
								data.Add(version);

							previous = version;
						}
						if (data.Count == 2 && commVersion >= data[0] && commVersion <= data[1])
							return setup;
						else if (data.Count == 1 && commVersion >= data[0])
							return setup;
					}
				}
			}

			return null;
		}

		private static bool ContainsWord(string text, string[] words)
		{
			foreach (var item in words)
			{
				if (text.IndexOf(item, StringComparison.OrdinalIgnoreCase) != -1)
					return true;
			}
			return false;
		}

		public static SectionBase RetreiveNewSetup(List<SectionBase> setups)
		{
			if (setups == null)
				return default(SectionBase);
			string[] keywords = null;

			if (System.Configuration.ConfigurationManager.AppSettings["newKeywords"] != null)
				keywords = System.Configuration.ConfigurationManager.AppSettings["newKeywords"].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			else
				keywords = new string[1] { "New" };

			foreach (var setup in setups)
			{
				if (StartsWith(setup.Text, keywords))
					return setup;
			}
			return default(SectionBase);
		}

		private static bool StartsWith(string text, string[] words)
		{
			foreach (var item in words)
			{
				if (text.StartsWithIgnoreSpaces(item))
					return true;
			}
			return false;
		}
	}
}
