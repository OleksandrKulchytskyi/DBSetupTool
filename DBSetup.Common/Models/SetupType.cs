using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common.Models
{
	public enum SetupType : int
	{
		None = 0,
		New = 1,
		Update = 2,
		Load = 3
	}
}
