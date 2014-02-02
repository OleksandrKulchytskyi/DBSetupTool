
using System.Collections.Generic;
namespace DBSetup.Common
{
	public interface IState
	{
		string Name { get; }
	}

	public interface IGlobalState
	{
		T GetState<T>(string key);
		void SetState<T>(string key, T state);
		void Clear();
	}

	public class GlobalState : IGlobalState
	{
		Dictionary<string, object> _container;

		public GlobalState()
		{
			_container = new Dictionary<string, object>();
		}

		public T GetState<T>(string key)
		{
			if (_container.ContainsKey(key))
				return (T)_container[key];
			return default(T);
		}

		public void SetState<T>(string key, T state)
		{
			_container[key] = state;
		}

		public void Clear()
		{
			_container.Clear();
		}
	}

}
