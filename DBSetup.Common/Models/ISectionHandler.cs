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
		void OnBunchHandled(Action<object> onBunch);

		void OnErrorHandler(Func<Exception, object, object> onError);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="onProcessing">Action on entry has been processed (action that is performing, file that is currently processing, state)</param>
		void OnEntryProcessing(Action<string, string, object> onProcessing);
	}

	public interface ISectionHandlerFactory
	{
		ISectionHandler CreateByType(LineType type, ILog logger, object parameters);
	}
}
