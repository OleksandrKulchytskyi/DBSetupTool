using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common.Models
{
	/// <summary>
	/// Additional section of tree view object model(BLOB). Represents the link to external report data file
	///<example>Blob=.\CrystalReports\MROReport.rpt REPORTS REP_DATA REP_NAME 'MROCrystalReport'</example>
	/// </summary>
	[Serializable]
	public class BlobLink : SectionBase
	{
		public BlobLink()
			: base()
		{
			Children = null;
			Content = null;
			Text = "Blob File";
		}

		private string _blobPath = null;
		public string BlobFilePath
		{
			get { return _blobPath; }
			set { _blobPath = value; }
		}

		private string _blobContent = null;
		public string BlobContent
		{
			get { return _blobContent; }
			set { _blobContent = value; }
		}

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;

			if (object.ReferenceEquals(this, obj))
				return true;

			if (obj is BlobLink)
			{
				return (string.Compare(BlobFilePath, (obj as BlobLink).BlobFilePath, StringComparison.OrdinalIgnoreCase) == 0
					&& string.Compare(BlobContent, (obj as BlobLink).BlobContent, StringComparison.OrdinalIgnoreCase) == 0);
			}

			return false;
		}

		public override int GetHashCode()
		{
			return BlobFilePath.Length + BlobContent.Length;
		}

		public override string ToString()
		{
			return string.Format("BLOB={0} {1}", BlobFilePath, BlobContent);
		}
	}
}
