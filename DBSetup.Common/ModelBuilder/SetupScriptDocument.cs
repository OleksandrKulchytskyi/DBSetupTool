using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common.ModelBuilder
{
	public class SetupScriptDocument
	{
		private StringBuilder _docBuilder = null;
		
		public SetupScriptDocument()
		{
			_docBuilder = new StringBuilder();
		}

		public int DocumentLinesCount { get; private set; }

		public void InsertString(int offset, string content)
		{
			string ofssetStr = new string(' ', offset);
			_docBuilder.AppendFormat("{0}{1}{2}", ofssetStr, content, Environment.NewLine);
			DocumentLinesCount++;
		}

		public void InsertStringFormat(int offset, string message, params object[] args)
		{
			if (string.IsNullOrEmpty(message))
				throw new ArgumentNullException("message");

			var formattedMsg = string.Format(message, args);
			InsertString(offset, formattedMsg);
		}

		public void RemoveLine(int lineIndex)
		{
			var all = _docBuilder.ToString();
			_docBuilder.Clear();
			string[] lines = all.Replace("\r", "").Split('\n');
			for (int i = 0; i < lines.Length; i++)
			{
				if (i == lineIndex)
				{
					continue;
				}

				_docBuilder.AppendLine(lines[i]);
			}
			all = null;
			DocumentLinesCount--;
		}

		public string GetDocumentText()
		{
			return _docBuilder.ToString();
		}

		public void Clear()
		{
			DocumentLinesCount = 0;
			_docBuilder.Clear();
		}
	}
}
