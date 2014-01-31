using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common.Exceptions
{
	[Serializable]
	public class SqlServerException : ApplicationException
	{
		public SqlServerException(string message)
			: base(message)
		{

		}

		public SqlServerException(string message, Exception innerExc)
			: base(message, innerExc)
		{

		}
	}
}
