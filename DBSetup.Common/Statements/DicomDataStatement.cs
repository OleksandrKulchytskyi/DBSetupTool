using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common.Statements
{
	public class DicomDataStatement : IDataStatement
	{
		public DicomDataStatement()
		{
			_type = StatementType.Dicom;
		}

		public DicomDataStatement(string file, bool isActive)
			: this()
		{
			DataFile = file;
			IsActive = isActive;
		}

		private StatementType _type;
		public StatementType Type
		{
			get { return _type; }
		}

		public string DataFile { get; set; }

		public bool IsActive { get; set; }

		public Models.ISection ContentRoot
		{
			get;
			set;
		}
	}
}
