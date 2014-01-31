using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common.Models
{
	[Serializable]
	public class DICOMLink : SectionBase
	{
		public DICOMLink()
			: base()
		{
			Children = null;
			Content = null;
			Text = "DICOM File";
		}

		private string _csvPath = null;
		public string CSVFilePath
		{
			get { return _csvPath; }
			set { _csvPath = value; }
		}

		private bool _isActive;
		public bool IsActive
		{
			get { return _isActive; }
			set { _isActive = value; }
		}

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;

			if (object.ReferenceEquals(this, obj))
				return true;

			if (obj is SqlLink)
			{
				return (string.Compare(CSVFilePath, (obj as DICOMLink).CSVFilePath, StringComparison.OrdinalIgnoreCase) == 0
					&& IsActive == (obj as DICOMLink).IsActive);
			}

			return false;
		}

		public override int GetHashCode()
		{
			return CSVFilePath.GetHashCode();
		}

		public override string ToString()
		{
			return String.Format("DICOM={0},{1}", CSVFilePath, IsActive);
		}
	}
}
