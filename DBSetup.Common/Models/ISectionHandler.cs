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
		ILog Logger { get; set; }

		bool Handle(ISection entity);

		void OnPreHandler(Action<String, object> onPreHandle);
		void OnStepHandler(Action<String> onStep);
		void OnErrorHandler(Action<Exception> onError);
	}

	public interface ISectionHandlerFactory
	{
		ISectionHandler CreateByType(LineType type, ILog logger, object parameters);
	}
}
