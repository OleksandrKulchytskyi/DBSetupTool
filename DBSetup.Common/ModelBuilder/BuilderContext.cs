using DBSetup.Common.Helpers;
using DBSetup.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common.ModelBuilder
{
	/// <summary>
	/// Represent build strategy pattern which choose which kind of build strategy to perform
	/// </summary>
	public class ModelBuilderContext
	{
		private GenericWeakReference<IBuilder> _weakBuilder;

		public void SetBuilder(IBuilder builder)
		{
			if (builder == null)
				throw new ArgumentNullException("builder");

			_weakBuilder = new GenericWeakReference<IBuilder>(builder);
		}

		public IBuilder GetBuilder() { return (_weakBuilder.Target); }

		public List<SectionBase> ExecuteBuild()
		{
			if (_weakBuilder.Target == null) return null;

			_weakBuilder.Target.Build();
			return _weakBuilder.Target.GetResult();
		}

		public List<FaultSection> GetFaults() { return (_weakBuilder.Target).FaultSectionsList; }
	}
}
