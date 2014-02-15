using DBSetup.Common;
using DBSetup.Common.Helpers;
using System.Collections.Generic;
using System.Linq;

namespace DBSetup.States
{
	internal class StateContainer : SingletonBase<StateContainer>
	{
		private readonly Dictionary<int, IState> _States;
		private bool _isExitRequired = false;

		private StateContainer()
		{
			_States = new Dictionary<int, IState>(7);
		}

		public void AddState(int indx, IState state)
		{
			_States[indx] = state;
		}

		public IState this[int ind]
		{
			get
			{
				if (ind > 6 || ind < 0 || !_States.ContainsKey(ind))
					return null;
				return _States[ind];
			}
		}

		public bool IsExitRequired
		{
			get { return _isExitRequired; }
			set { _isExitRequired = value; }
		}

		public T GetState<T>()
		{
			return _States.Values.OfType<T>().FirstOrDefault();
		}
	}
}