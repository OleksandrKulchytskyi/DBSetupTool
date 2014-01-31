using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common.Models
{
	public interface ISection
	{
		string Text { get; set; }
		string Content { get; set; }

		string FileName { get; set; }

		ISectionHandler Handler { get; }

		//ISection Parent { get; set; }
	}

	public class SectionBase : ISection
	{
		public SectionBase()
		{
			Children = null;
			Parent = null;
			Text = string.Empty;
			Content = string.Empty;
			FileName = string.Empty;
			ReadOnly = false;
			SetupType = SetupType.None;
		}

		public string Text { get; set; }

		public string Content { get; set; }

		public SectionBase Parent { get; set; }

		public List<SectionBase> Children { get; set; }

		public string FileName { get; set; }

		public bool ReadOnly { get; set; }

		public SetupType SetupType { get; set; }

		public ISectionHandler Handler { get; set; }

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;

			if (object.ReferenceEquals(this, obj))
				return true;

			if (obj is SectionBase)
			{
				return (string.Compare(this.FileName, (obj as SectionBase).FileName, true) == 0
					&& string.Compare(this.Text, (obj as SectionBase).Text, true) == 0
					&& string.Compare(this.Content, (obj as SectionBase).Content, true) == 0);
			}

			return false;
		}

		public override int GetHashCode()
		{
			return Text.Length + Content.Length + FileName.Length;
		}

		public override string ToString()
		{
			return string.Format("[{0},{1}]", Text, System.IO.Path.GetFileName(FileName ?? string.Empty));
		}
	}
}
