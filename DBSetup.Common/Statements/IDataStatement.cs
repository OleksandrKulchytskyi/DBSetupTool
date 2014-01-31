using DBSetup.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common.Statements
{
	public enum StatementType : int
	{
		None = 0,
		Sql = 1,
		Blob,
		Dicom
	}

	public interface IDataStatement
	{
		StatementType Type { get; }
		string DataFile { get; set; }
		ISection ContentRoot { get; set; }
	}
}
