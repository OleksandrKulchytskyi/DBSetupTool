using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common.DICOM
{
	[Serializable]
	public sealed class DicomOperationCancelledException : ApplicationException
	{
		public DicomOperationCancelledException()
			: base()
		{
			SetCancellation();
		}

		public DicomOperationCancelledException(string message)
			: base(message)
		{
			SetCancellation();
		}

		private void SetCancellation()
		{
			CancellationTime = DateTime.Now;
		}

		public DateTime CancellationTime { get; set; }
	}
}
