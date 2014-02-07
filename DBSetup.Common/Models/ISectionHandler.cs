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

		/// <summary>
		/// 
		/// </summary>
		/// <param name="onProcessed">Action on entry has been processed (action, file, state)</param>
		void OnEntryProcessed(Action<string, string, object> onProcessed);
	}

	public interface ISectionHandlerFactory
	{
		ISectionHandler CreateByType(LineType type, ILog logger, object parameters);
	}
}
