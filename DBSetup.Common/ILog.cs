using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common
{
	public interface ILog
	{
		void Fatal(string message, Exception ex);

		void Error(string message);

		void Error(string message, Exception ex);

		void Info(string message);

		void Warn(string message);
	}
}
