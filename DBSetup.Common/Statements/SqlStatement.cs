using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DBSetup.Common.Helpers;
using DBSetup.Common.Models;

namespace DBSetup.Common.Statements
{
	public class SqlDataStatement : IDataStatement
	{
		string _sqlStatement;

		public SqlDataStatement()
		{
			DataFile = string.Empty;
			_type = StatementType.Sql;
		}

		public SqlDataStatement(string sqlStatement)
			: this()
		{
			this._sqlStatement = sqlStatement;
		}

		public SqlDataStatement(string sqlStatement, string file)
			: this()
		{
			this._sqlStatement = sqlStatement;
			this.DataFile = file;
		}

		public string SqlStatements
		{
			get { return _sqlStatement; }
		}

		public string DataFile { get; set; }

		private StatementType _type;
		public StatementType Type
		{
			get { return _type; }
		}

		public ISection ContentRoot
		{
			get;
			set;
		}

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;

			if (object.ReferenceEquals(this, obj))
				return true;

			if (obj is SqlDataStatement)
			{
				return (this.DataFile.Equals((obj as SqlDataStatement).DataFile, StringComparison.OrdinalIgnoreCase) &&
					this.SqlStatements.Equals((obj as SqlDataStatement).SqlStatements, StringComparison.OrdinalIgnoreCase));
			}

			return false;
		}

		public string[] SplitByGoStatementWithComments()
		{
			if (string.IsNullOrEmpty(_sqlStatement))
				return new List<string>().ToArray();

			StringBuilder partsBuilder = new StringBuilder();
			int lineNumber = 0;
			bool isMultipleComment = false;
			List<string> parts = new List<string>();

			using (var sr = new System.IO.StringReader(_sqlStatement))
			{
				string line = string.Empty;

				while ((line = sr.ReadLine()) != null)
				{
					lineNumber++;

					if ((line.IndexOf("/*") != -1 || line.IndexOf("*/") != -1) && !line.IsCommentLineClosed())
					{
						isMultipleComment = true;
						int openingSignCount = 0;
						int closingSignCount = 0;
						// begin count all closing and opening comment sign
						line.CountCommentSigns(out openingSignCount, out closingSignCount);
						partsBuilder.AppendLine(line);

						while (((line = sr.ReadLine()) != null) ||
								!isMultipleComment ||
								(openingSignCount != closingSignCount))
						{
							int osC, csC;
							lineNumber++;
							line.CountCommentSigns(out osC, out csC);
							//reevaluate coment sign
							openingSignCount = openingSignCount + osC;
							closingSignCount = closingSignCount + csC;
							//check for sign equality
							if (openingSignCount == closingSignCount)
							{
								partsBuilder.AppendLine(line);
								isMultipleComment = false;
								break;
							}
							else
								partsBuilder.AppendLine(line);
						}
						continue;
					}
					else if (line.StartsWithIgnoreSpaces("--") ||
							(line.StartsWithIgnoreSpaces("/*") && line.EndsWithIgnoreSpaces("*/")))
					{
						partsBuilder.AppendLine(line);
						continue;
					}
					if (line.IndexOf("/*") == -1 && line.IndexOf("*/") == -1 && line.IndexOf("--") == -1
						&& line.ContainsOnly("go") && line.Trim().Length < 4)
					{
						partsBuilder.AppendLine(line);
						parts.Add(partsBuilder.ToString());
						partsBuilder.Clear();
						continue;
					}

					partsBuilder.AppendLine(line);
				}

				if (partsBuilder.Length > 2)
				{
					parts.Add(partsBuilder.ToString());
					partsBuilder.Clear();
				}
			}

			return parts.ToArray();
		}

		public string[] SplitByGoStatementWithoutComments()
		{
			int lines;
			return SplitByGoStatementWithoutComments(out lines);
		}

		public string[] SplitByGoStatementWithoutComments(out int processedLines)
		{
			processedLines = 0;
			if (string.IsNullOrEmpty(_sqlStatement))
				return new List<string>().ToArray();

			StringBuilder partsBuilder = new StringBuilder();
			int lineNumber = 0;
			bool isMultipleComment = false;
			List<string> parts = new List<string>();

			using (var sr = new System.IO.StringReader(_sqlStatement))
			{
				string line = string.Empty;

				while ((line = sr.ReadLine()) != null)
				{
					lineNumber++;
					if (line.StartsWithIgnoreSpaces("--") ||
							(line.StartsWithIgnoreSpaces("/*") && line.EndsWithIgnoreSpaces("*/")))
					{
						isMultipleComment = false;
						continue;
					}
					else if (line.IsCommentLineClosed())
					{
						isMultipleComment = false;
						//get sql statements which may be contains before or after sql comment scope
						string sql = line.GetSqlWithoutComment();
						if (!string.IsNullOrEmpty(sql) && !string.IsNullOrWhiteSpace(sql))
							partsBuilder.AppendLine(sql);
						continue;
					}
					else if (!line.IsCommentLineClosed())
					{
						isMultipleComment = true;
						int openingSignCount = 0;
						int closingSignCount = 0;
						// begin count all closing and opening comment sign
						line.CountCommentSigns(out openingSignCount, out closingSignCount);

						while (((line = sr.ReadLine()) != null) ||
								!isMultipleComment ||
								(openingSignCount != closingSignCount))
						{
							int osC, csC;
							lineNumber++;
							line.CountCommentSigns(out osC, out csC);
							//reevaluate coment sign
							openingSignCount = openingSignCount + osC;
							closingSignCount = closingSignCount + csC;
							//check for sign equality
							if (openingSignCount == closingSignCount)
							{
								//get rest sql statement after closing comment sign
								string rest = line.Substring(line.LastIndexOf("*/") + 2);
								if (!string.IsNullOrEmpty(rest) && !string.IsNullOrWhiteSpace(rest))
									partsBuilder.AppendLine(string.Format("{0} {1}", new string(' ', line.LastIndexOf("*/") + 2), rest));
								else
									partsBuilder.Append(Environment.NewLine);

								isMultipleComment = false;
								break;
							}
							//else
							//	partsBuilder.AppendLine(line);
						}
						continue;
					}
					else if (line.IndexOf("/*") == -1 && line.IndexOf("*/") == -1 &&
						line.IndexOf("--") == -1 &&
						!isMultipleComment &&
						line.ContainsOnly("go"))
					{
						partsBuilder.AppendLine(line);
						parts.Add(partsBuilder.ToString());
						partsBuilder.Clear();
						continue;
					}

					//else if (line.IsCommentLineClosed() && line.IsSqlContainsClosedComment())
					//{
					//	partsBuilder.AppendLine(line.GetSqlWithoutComment());
					//}

					partsBuilder.AppendLine(line);
				}
				processedLines = lineNumber;

				if (partsBuilder.Length > 2)
				{
					parts.Add(partsBuilder.ToString());
					partsBuilder.Clear();
				}
			}
			return parts.ToArray();
		}
	}
}
