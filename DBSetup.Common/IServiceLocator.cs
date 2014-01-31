using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common
{
	public interface IServiceLocator
	{
		T GetService<T>();
	}
}
