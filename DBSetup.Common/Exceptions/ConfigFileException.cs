using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common.Exceptions
{
	[Serializable]
	public class ConfigFileException : ApplicationException
	{
		public ConfigFileException(String message)
			: base(message)
		{
		}

		public ConfigFileException(String message, Exception innerException)
			: base(message, innerException)
		{
		}

		public ConfigFileException(string file, string section, string message)
			: this(message)
		{
			FileName = file;
			Section = section;
		}

		public ConfigFileException(string file, string section, string message, Exception inner)
			: this(message, inner)
		{
			FileName = file;
			Section = section;
		}

		public string FileName { get; set; }
		public string Section { get; set; }

		public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
		{
			base.GetObjectData(info, context);
		}
	}
}
