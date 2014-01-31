using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DBSetup.Common.Helpers
{
	public static class StringExtension
	{
		public static System.Text.RegularExpressions.Regex RegexSection { get; private set; }

		static StringExtension()
		{
			RegexSection = new Regex(@"(?ms)^\[[^]\r\n]+](?:(?!^\[[^]\r\n]+]).)*", System.Text.RegularExpressions.RegexOptions.Compiled);
		}

		/// <summary>
		/// Generate list of string from text snippet
		/// </summary>
		/// <param name="text"></param>
		/// <returns></returns>
		public static List<string> SplitByLines(this string text)
		{
			if (string.IsNullOrEmpty(text))
				return new List<string>();

			return text.Split(new string[] { "\r\n" }, StringSplitOptions.None).ToList();
		}

		/// <summary>
		/// Generate full section text including section name [secName] and full section content (key value pairs)
		/// </summary>
		/// <param name="sectionContent"></param>
		/// <param name="sectionName"></param>
		/// <returns></returns>
		public static string GenerateFullSectionText(this string sectionContent, string sectionName)
		{
			if (string.IsNullOrEmpty(sectionName))
				return null;

			StringBuilder sb = new StringBuilder();
			//Add section header
			sb.Append(string.Format("[{0}]{1}", sectionName, Environment.NewLine));
			//Add section content
			sb.Append(sectionContent);
			return sb.ToString();
		}

		public static string GetSectionContentAsText(this string fullText, string sectionWithBraces)
		{
			if (string.IsNullOrEmpty(fullText) || string.IsNullOrEmpty(sectionWithBraces))
				return null;

			StringBuilder sb = new StringBuilder();
			using (var reader = new System.IO.StringReader(fullText))
			{
				string line = null;
				bool found = false;
				while ((line = reader.ReadLine()) != null)
				{
					if (string.Compare(line, sectionWithBraces, StringComparison.OrdinalIgnoreCase) == 0)
					{
						found = true;
						continue;
					}
					if (found)
					{
						if (RegexSection.IsMatch(line))
							break;
						sb.AppendLine(line);
					}
				}
			}
			return sb.ToString();
		}

		public static string GetSectionNameFromContentLine(this string fullText, int lineNumber, string contentText)
		{
			if (string.IsNullOrEmpty(fullText) || string.IsNullOrEmpty(contentText))
				return null;
			string sectionHeadeText = null;

			List<string> data = fullText.SplitByLines();
			if (string.Compare(data[lineNumber - 1], contentText, true) == 0)
			{
				for (int i = lineNumber - 1; i >= 0; i--)
				{
					if (RegexSection.IsMatch(data[i]))
					{
						sectionHeadeText = data[i];
						break;
					}
				}
			}
			return sectionHeadeText;
		}

		public static bool MatchExact(this string input, string match)
		{
			return Regex.IsMatch(input, string.Format(@"\b{0}\b", Regex.Escape(match)), RegexOptions.IgnoreCase);
		}

		/// <summary>
		/// Check if the line contains only specified value, empty spaces are allowed
		/// </summary>
		/// <param name="line">source string</param>
		/// <param name="value">value to search</param>
		/// <returns></returns>
		public static bool ContainsOnly(this string line, string value)
		{
			if (string.IsNullOrEmpty(line) || string.IsNullOrEmpty(value))
				return false;

			bool result = false;
			for (int i = 0; i < line.Length; i++)
			{
				//TODO: the line of code below creates to much strings
				if (!char.IsWhiteSpace(line[i]) && value.IndexOf(line[i].ToString(), StringComparison.OrdinalIgnoreCase) == -1)
					return false;

				if (char.ToLowerInvariant(line[i]) == char.ToLowerInvariant(value[0]))
				{
					result = true;
					for (int j = 1; j < value.Length; j++)
					{
						if (i == line.Length)
							break;
						if (char.ToLowerInvariant(line[++i]) != char.ToLowerInvariant(value[j]))
						{
							result = false;
							break;
						}
					}
					if (result) break;
				}

			}
			return result;
		}

		public static bool IsCommentLineClosed(this string commentedLine)
		{
			if (string.IsNullOrEmpty(commentedLine) || string.IsNullOrWhiteSpace(commentedLine))
				return true;

			int openCount = 0;
			int closeCount = 0;
			int offset = 0;
			int openIndex = commentedLine.IndexOf("/*");

			if (openIndex != -1)
			{
				openCount++;
				offset = openIndex;
				while ((offset = commentedLine.IndexOf("/*", offset + 2)) != -1)
				{
					openCount++;
				}
			}

			int closeIndex = commentedLine.IndexOf("*/");
			if (closeIndex != -1)
			{
				closeCount++;
				offset = closeIndex;
				while ((offset = commentedLine.IndexOf("*/", offset + 2)) != -1)
				{
					closeCount++;
				}
			}

			return (openCount == closeCount);
		}

		public static bool IsSqlContainsClosedComment(this string commentedLine)
		{
			if (string.IsNullOrEmpty(commentedLine) || string.IsNullOrWhiteSpace(commentedLine))
				return false;

			bool result = false;
			bool isCommentClosed = commentedLine.IsCommentLineClosed();

			if (isCommentClosed)
			{
				StringBuilder sqlWithoutComment = new StringBuilder();

				int openIndex = commentedLine.IndexOf("/*");
				int closedIndex = commentedLine.LastIndexOf("*/");
				if (openIndex >= 1)
				{
					sqlWithoutComment.Append(commentedLine.Substring(0, openIndex));
				}
				if ((commentedLine.Length - 1) > (closedIndex + 2))
				{
					sqlWithoutComment.AppendFormat(" {1}", commentedLine.Substring(closedIndex + 2));
				}

				result = sqlWithoutComment.Length > 2;
			}

			return result;
		}

		public static string GetSqlWithoutComment(this string commentedLine)
		{
			if (string.IsNullOrEmpty(commentedLine) || string.IsNullOrWhiteSpace(commentedLine))
				return string.Empty;

			bool isCommentClosed = commentedLine.IsCommentLineClosed();
			StringBuilder sqlWithoutComment = new StringBuilder(string.Empty);

			if (isCommentClosed)
			{
				int openIndex = commentedLine.IndexOf("/*");
				int closedIndex = commentedLine.LastIndexOf("*/");
				if (openIndex >= 1)
				{
					sqlWithoutComment.Append(commentedLine.Substring(0, openIndex));
				}
				if ((commentedLine.Length - 1) > (closedIndex + 2))
				{
					sqlWithoutComment.AppendFormat(" {0}", commentedLine.Substring(closedIndex + 2));
				}
			}

			return sqlWithoutComment.ToString();
		}

		public static void CountCommentSigns(this string line, out int openingSignCount, out int closingSignCount)
		{
			openingSignCount = 0;
			closingSignCount = 0;

			if (string.IsNullOrEmpty(line) || string.IsNullOrWhiteSpace(line))
				return;

			for (int i = 0; i < line.Length; i++)
			{
				if (line.Length > i + 1)
				{
					switch (line[i])
					{
						case '/':
							if (line[i + 1].Equals('*'))
							{
								i = i + 2;
								openingSignCount++;
								continue;
							}
							break;

						case '*':
							if (line[i + 1].Equals('/'))
							{
								i = i + 2;
								closingSignCount++;
								continue;
							}
							break;

						default:
							break;
					}
				}
			}

		}

		public static List<string> ParseSQL(this String statements)
		{
			List<string> statementList = new List<string>();
			int iLeft = 0, iRight = 0, tempiLeft, commentLeft, commentRight, quoteLeft, quoteRight;
			String temp, stringUpper;
			bool foundBreak, goLeftEmpty, goRightEmpty,
			bPrintedGo = false, bPrintedComment = false, scanAgain;

			stringUpper = statements.ToUpper();
			do
			{
				// string and stringUpper are same except for case
				// just use stringUpper for compare
				foundBreak = false;
				tempiLeft = iLeft;
				while (!foundBreak)  // "GO" or end of string
				{
					do // skip any go found in a comment
					{
						//goInComment = false;
						scanAgain = false; // if go out of comment, scan again from end of comment
						iRight = stringUpper.IndexOf("GO", tempiLeft);
						commentLeft = stringUpper.IndexOf("/*", tempiLeft);
						if (commentLeft >= 0)
						{
							commentRight = stringUpper.IndexOf("*/", tempiLeft);

							if (commentLeft < commentRight)
							{
								if (((commentLeft < iRight) && (iRight < commentRight)) || (iRight > commentRight))
								{
									tempiLeft = commentRight + 2;
									scanAgain = true;
									continue;
								}
							}
						}
						//check to see if "go" is inside a quoted string part of a statement
						quoteLeft = stringUpper.IndexOf("'", tempiLeft);
						if (quoteLeft >= 0)
						{
							quoteRight = stringUpper.IndexOf("'", quoteLeft + 1);
							if (quoteLeft < quoteRight)
							{
								if (((quoteLeft < iRight) && (iRight < quoteRight)) || (iRight > quoteRight))
								{
									scanAgain = true;
									tempiLeft = quoteRight + 1;  //skip past 'string'
									continue;
								}
							}
						}
					}
					while (scanAgain);

					// if found "go" make sure it is not part of another word
					if (iRight < 0)
					{
						foundBreak = true;
					}
					else
					{
						goLeftEmpty = false;
						goRightEmpty = false;
						try
						{
							temp = statements.Substring(iRight - 1, iRight + 2).Trim();
							if (temp.Equals("GO", StringComparison.OrdinalIgnoreCase))
								goLeftEmpty = true;
						}
						catch (IndexOutOfRangeException)
						{
							goLeftEmpty = true; // because at beginning of string
						}
						try
						{
							temp = statements.Substring(iRight, iRight + 3).Trim();
							if (temp.Equals("GO", StringComparison.OrdinalIgnoreCase))
								goRightEmpty = true;
						}
						catch (IndexOutOfRangeException)
						{
							goRightEmpty = true; // because at end of string
						}

						if (goLeftEmpty && goRightEmpty)
						{
							foundBreak = true;
						}

						if (!foundBreak)
							tempiLeft = iRight + 2; // +2 for "go"
					}// end if iRight >= 0
				}

				if (iRight > iLeft)
				{
					temp = statements.Substring(iLeft, iRight).Trim();
					if (temp.Length > 0)
					{
						temp = parseVarName(temp, string.Empty);
						statementList.Add(temp);
					}

					iLeft = iRight + 2; // +2 for "go"
				}
			}
			while (iRight >= 0);
			// add last one, or only one if no "go"
			temp = statements.Substring(iLeft).Trim();
			//System.out.println(temp.toString());
			if (temp.Length > 0)
			{
				temp = parseVarName(temp, string.Empty);
				statementList.Add(temp);
			}

			return statementList;
		}

		private static String parseVarName(String statement, string DBName)
		{
			int varIndex = statement.IndexOf(DBName);
			String retString = statement;
			if (varIndex == -1)
			{
				retString = new String(new char[] { ' ' });
			}
			else if (varIndex > 0)
			{
				retString = statement.Substring(0, varIndex) + DBName;
			}
			return retString;
		}

		public static string FormatWith(this string pattern, params object[] args)
		{
			if (string.IsNullOrEmpty(pattern))
				return string.Empty;
			else
				return string.Format(pattern, args);
		}

		public static bool StartsWithIgnoreSpaces(this string source, string search, bool ignoreCase = true)
		{
			if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(search))
				return false;
			else
			{
				int indx = 0;
				while (char.IsWhiteSpace(source[indx]) && (indx != source.Length - 1))
				{
					indx++;
				}
				bool result = true;

				if (ignoreCase)
				{
					for (int i = indx, j = 0; i < source.Length; i++, j++)
					{
						if (char.ToLowerInvariant(source[i]) != char.ToLowerInvariant(search[j]))
						{
							result = false;
							break;
						}
						if (j == search.Length - 1)
							break;
					}
				}
				else
				{
					for (int i = indx, j = 0; i < source.Length; i++, j++)
					{
						if (source[i] != search[j])
						{
							result = false;
							break;
						}
						if (j == search.Length - 1)
							break;
					}
				}
				return result;
			}
		}

		public static bool EndsWithIgnoreSpaces(this string source, string search, bool ignoreCase = true)
		{
			if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(search))
				return false;
			else
			{
				int indx = source.Length - 1;
				while (char.IsWhiteSpace(source[indx]) && (indx != 0))
				{
					indx--;
				}
				bool result = true;
				if (ignoreCase)
				{
					for (int i = indx, j = search.Length - 1; j != -1; i--, j--)
					{
						if (char.ToLowerInvariant(source[i]) != char.ToLowerInvariant(search[j]))
						{
							result = false;
							break;
						}
					}
				}
				else
				{
					for (int i = indx, j = search.Length - 1; j != -1; i--, j--)
					{
						if (source[i] != search[j])
						{
							result = false;
							break;
						}
					}
				}
				return result;
			}
		}
	}
}
