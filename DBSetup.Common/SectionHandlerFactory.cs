using DBSetup.Common.DICOM;
using DBSetup.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common
{
	public class SectionHandlerFactory : ISectionHandlerFactory
	{
		public ISectionHandler CreateByType(Helpers.LineType type, ILog logger, object parameters)
		{
			ISectionHandler handler = null;

			switch (type)
			{
				case DBSetup.Common.Helpers.LineType.Empty:
					break;
				case DBSetup.Common.Helpers.LineType.Comment:
					break;
				case DBSetup.Common.Helpers.LineType.IniLink:
					break;
				case DBSetup.Common.Helpers.LineType.SectionLink:
					break;
				case DBSetup.Common.Helpers.LineType.SqlLink:
					handler = new SQLSectionHandler();
					break;
				case DBSetup.Common.Helpers.LineType.Text:
					break;
				case DBSetup.Common.Helpers.LineType.SectionHeader:
					break;
				case DBSetup.Common.Helpers.LineType.JavaLink:
					break;
				case DBSetup.Common.Helpers.LineType.BlobLink:
					break;
				case DBSetup.Common.Helpers.LineType.Language:
					break;
				case DBSetup.Common.Helpers.LineType.SettingsPair:
					break;
				case DBSetup.Common.Helpers.LineType.DICOM:
					handler = new DicomSectionHandler();
					break;
				default:
					break;
			}

			if (handler != null)
			{
				handler.Logger = logger;
				handler.Parameters = parameters;
			}

			return handler;
		}
	}
}
