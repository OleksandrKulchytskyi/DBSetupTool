using DBSetup.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common.Helpers
{
	/// <summary>
	/// Specifies the enumeration of available line types, could be extended
	/// </summary>
	public enum LineType
	{
		Empty = 0,
		Comment = 1,
		IniLink = 2,
		SectionLink = 3,
		SqlLink = 4,
		Text = 5,
		SectionHeader = 6,
		JavaLink = 7,
		BlobLink = 8,
		Language = 9,
		SettingsPair = 10,
		DICOM = 11
	}

	/// <summary>
	/// This class have methods to perform parsing of lines inside section
	/// </summary>
	public class LineParser
	{
		/// <summary>
		/// Returns the type of the line. The type is determined by LineType enumeration
		/// </summary>
		/// <param name="line">The line to determine it type</param>
		/// <returns></returns>
		public static LineType getLineType(string line)
		{
			//due to performance issue with string.ToLower() method in string instance it would be better to replace it with string.Compare()
			line = line.Trim();
			int indxEqueals = line.IndexOf('=');

			if (string.IsNullOrEmpty(line) || string.IsNullOrWhiteSpace(line)) //Line is empty
				return LineType.Empty;

			//Line is a comment
			else if (line.StartsWith("::", StringComparison.OrdinalIgnoreCase) || line.StartsWith(";", StringComparison.OrdinalIgnoreCase))
				return LineType.Comment;

			//Line contains link to another INI file
			else if (line.StartsWith("Include=", StringComparison.OrdinalIgnoreCase))
				return LineType.IniLink;

			//Line contains link to another section
			else if (line.StartsWith("Setup=", StringComparison.OrdinalIgnoreCase) ||
					line.StartsWith("ActiveSection=", StringComparison.OrdinalIgnoreCase) ||
					line.StartsWith("Section=", StringComparison.OrdinalIgnoreCase))
				return LineType.SectionLink;

			//Line contains link to sql file
			else if (line.StartsWith("SQL=", StringComparison.OrdinalIgnoreCase) ||
					line.StartsWith("DDL=", StringComparison.OrdinalIgnoreCase))
				return LineType.SqlLink;

			else if (line.StartsWith("Java=", StringComparison.OrdinalIgnoreCase))
				return LineType.JavaLink;

			else if (line.StartsWith("BLOB=", StringComparison.OrdinalIgnoreCase))
				return LineType.BlobLink;

			else if (line.StartsWith("Language=", StringComparison.OrdinalIgnoreCase))
				return LineType.Language;

			else if (line.StartsWith("DICOM=", StringComparison.OrdinalIgnoreCase) ||
					 line.StartsWith("DICOM =", StringComparison.OrdinalIgnoreCase))
				return LineType.DICOM;

			else if (line.StartsWith("[", StringComparison.OrdinalIgnoreCase) && line.EndsWith("]", StringComparison.OrdinalIgnoreCase))
				return LineType.SectionHeader;

			else if (indxEqueals > 0 && line.Substring(0, indxEqueals + 1).Length > 1 && (line.Length - indxEqueals + 1) > 1)
				return LineType.SettingsPair;
			//Line is a plain text
			else
				return LineType.Text;
		}

		public static LineType getLineTypeOptimized(string line)
		{
			//due to performance issue with string.ToLower() method in string instance it would be better to replace it with string.Compare()
			int indxEqueals = line.IndexOf('=');

			if (string.IsNullOrEmpty(line) || string.IsNullOrWhiteSpace(line)) //Line is empty
				return LineType.Empty;

			//Line is a comment
			else if (line.StartsWithIgnoreSpaces("::", false) || line.StartsWithIgnoreSpaces(";", false))
				return LineType.Comment;

			//Line contains link to another INI file
			else if (line.StartsWithIgnoreSpaces("Include=", true))
				return LineType.IniLink;

			//Line contains link to another section
			else if (line.StartsWithIgnoreSpaces("Setup=") ||
					line.StartsWithIgnoreSpaces("ActiveSection=") ||
					line.StartsWithIgnoreSpaces("Section="))
				return LineType.SectionLink;

			//Line contains link to sql file
			else if (line.StartsWithIgnoreSpaces("SQL=") ||
					line.StartsWithIgnoreSpaces("DDL="))
				return LineType.SqlLink;

			else if (line.StartsWithIgnoreSpaces("Java="))
				return LineType.JavaLink;

			else if (line.StartsWithIgnoreSpaces("BLOB="))
				return LineType.BlobLink;

			else if (line.StartsWithIgnoreSpaces("Language="))
				return LineType.Language;

			else if (line.StartsWithIgnoreSpaces("DICOM="))
				return LineType.DICOM;

			else if (line.StartsWithIgnoreSpaces("[", false) && line.EndsWithIgnoreSpaces("]", false))
				return LineType.SectionHeader;

			else if (indxEqueals > 0 && line.Substring(0, indxEqueals + 1).Length > 1 && (line.Length - indxEqueals + 1) > 1)
				return LineType.SettingsPair;
			//Line is a plain text
			else
				return LineType.Text;
		}

		/// <summary>
		/// Returns the collections of Key/Value pair which was obtained from simple text line
		/// </summary>
		/// <param name="line">The line which contains key/value</param>
		/// <param name="toLowercase">If set - change case of value to lower</param>
		/// <returns></returns>
		public static KeyValuePair<string, string> GetKeyValueFromString(string line, bool toLowercase = false)
		{
			if (string.IsNullOrEmpty(line) || string.IsNullOrWhiteSpace(line))
				throw new ArgumentNullException("line");

			int i = line.IndexOf('=');
			int j = line.Length - i - 1;
			string Key = toLowercase == false ? line.Substring(0, i).Trim() : line.Substring(0, i).Trim().ToLower(); //Get key from line
			if (Key.Length > 0)
			{
				string Value = (j > 0) ? (line.Substring(i + 1, j).Trim()) : (string.Empty); //Get value from line
				KeyValuePair<string, string> pair = new KeyValuePair<string, string>(Key, toLowercase == false ? Value : Value.ToLower()); //Add key/value pair to collection
				return pair;
			}
			return new KeyValuePair<string, string>(Key, string.Empty);
		}

		/// <summary>
		/// Returns the collection of Key/Value pair obtained from filelink line type
		/// </summary>
		/// <param name="line">The line which contains link to external ini file and external sections</param>
		/// <param name="toLowerCase">If set - change case of value to lower</param>
		/// <returns></returns>
		public static KeyValuePair<string, string> ParseIniString(string line, bool toLowerCase = false)
		{
			if (line.Trim().Contains(','))
			{
				string[] array = line.Trim().Split(new string[] { "," }, 2, StringSplitOptions.RemoveEmptyEntries);
				if (array.Length != 2)
					throw new InvalidOperationException(string.Format("String contains invalid format. {0}", line));

				KeyValuePair<string, string> pair = new KeyValuePair<string, string>(array[0].Trim(), array[1].Trim());

				return pair;
			}
			throw new InvalidOperationException(string.Format("String contains invalid format. {0}", line));
		}

		public static KeyValuePair<string, string> ParseBlobString(string line)
		{
			return SplitGeneral(line, " ");
		}

		public static KeyValuePair<string, string> ParseDicomString(string line)
		{
			return SplitGeneral(line, ",", 1);
		}

		private static KeyValuePair<string, string> SplitGeneral(string line, string separator, int offset = 0)
		{
			if (string.IsNullOrEmpty(line) || string.IsNullOrWhiteSpace(line))
				throw new ArgumentNullException("line");

			if (string.IsNullOrEmpty(separator))
				throw new ArgumentNullException("separator");

			int index = line.IndexOf(separator);
			string filePath = line.Substring(0, offset > 0 ? index - offset : index + 1).TrimEnd();
			string content = line.Substring(offset > 0 ? index + offset : index).TrimStart();
			return new KeyValuePair<string, string>(filePath, content);
		}
	}

	public static class ListExtension
	{
		public static string GenerateStringFromList(this IEnumerable<string> list)
		{
			if (list == null || list.Count() == 0)
				return string.Empty;

			StringBuilder sb = new StringBuilder();
			foreach (string s in list)
			{
				sb.AppendLine(s);
			}
			string result = sb.ToString();
			sb.Clear();
			sb = null;
			return result;
		}
	}

	public class KeyValueExtension
	{
		public static string GenerateString(KeyValuePair<string, string> pair)
		{
			return string.Format("{0}={1}", pair.Key, pair.Value);
		}
	}
}
