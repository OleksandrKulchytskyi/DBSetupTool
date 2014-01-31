using DBSetup.Common.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common.Models
{
	public interface ISectionHandler
	{
		object Parameters { get; set; }
		void Handle(ISection entity);

		ILog Logger { get; set; }
	}

	public interface ISectionHandlerFactory
	{
		ISectionHandler CreateByType(LineType type, ILog logger, object parameters);
	}
}
