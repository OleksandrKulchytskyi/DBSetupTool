using DBSetup.Common;
using DBSetup.Common.Helpers;
using DBSetup.Common.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace DBSetup.Common
{
	public class ServiceLocator : SingletonBase<ServiceLocator>, IServiceLocator
	{
		// a map between contracts -> concrete implementation classes
		private IDictionary<Type, Type> servicesType;
		// a map containing references to concrete implementation already instantiated(the service locator uses lazy instantiation).
		private IDictionary<Type, object> instantiatedServices;

		private  ServiceLocator()
		{
			this.servicesType = new Dictionary<Type, Type>();
			this.instantiatedServices = new Dictionary<Type, object>();

			this.BuildServiceTypesMap();
		}

		public T GetService<T>()
		{
			if (this.instantiatedServices.ContainsKey(typeof(T)))
			{
				return (T)this.instantiatedServices[typeof(T)];
			}
			else
			{
				// lazy initialization
				try
				{
					// use reflection to invoke the service
					ConstructorInfo constructor = servicesType[typeof(T)].GetConstructor(new Type[0]);
					//Debug.Assert(constructor != null, "Cannot find a suitable constructor for " + typeof(T));

					T service = (T)constructor.Invoke(null);
					// add the service to the ones that we have already instantiated
					instantiatedServices.Add(typeof(T), service);

					return service;
				}
				catch (KeyNotFoundException ex)
				{
					Log.Instance.Error("GetService<T>", ex);
					throw new ApplicationException("The requested service is not registered");
				}
			}
		}

		private void BuildServiceTypesMap()
		{
			servicesType.Add(typeof(IVersionService), typeof(Common.Services.VesrionService));
			servicesType.Add(typeof(IExecutor), typeof(Common.Services.NoUIExecutor));
			servicesType.Add(typeof(IGlobalState), typeof(GlobalState));
			servicesType.Add(typeof(ISectionHandlerFactory), typeof(SectionHandlerFactory));
		}
	}
}