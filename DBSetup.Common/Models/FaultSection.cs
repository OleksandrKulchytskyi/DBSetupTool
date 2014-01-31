using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSetup.Common.Models
{
	/// <summary>
	/// Additional section of tree view object model. Represents the fault section, which can cause the circle link in tree view object model
	/// </summary>
	[Serializable]
	public class FaultSection : SectionBase
	{
		public FaultSection()
			: base()
		{
			Children = null;
			_isHandled = false;
		}

		private bool _isHandled;
		public bool IsHandled
		{
			get { return _isHandled; }
			set { _isHandled = value; }
		}

		public override string ToString()
		{
			return base.ToString();
		}
	}


	[Serializable]
	public sealed class FakeSection : SectionBase
	{
		public FakeSection()
			: base()
		{
			Children = null;
		}
	}
}
