using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common.Models
{
	public class Comment : SectionBase
	{
		public Comment()
			: base()
		{
			Text = "Comment";
			Children = null;
			CommentLine = string.Empty;
		}

		public string CommentLine { get; set; }

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;
			else if (object.ReferenceEquals(this, obj))
				return true;

			else if (obj is Comment)
			{
				return this.CommentLine.Equals((obj as Comment).CommentLine, StringComparison.OrdinalIgnoreCase);
			}

			return false;
		}

		public override int GetHashCode()
		{
			return CommentLine.GetHashCode();
		}

		public override string ToString()
		{
			return this.CommentLine;
		}
	}
}
