using System;

namespace DBSetup.Common
{
	public interface IExecutor
	{
		void Execute();
		void OnStep(Action<object> onStep);
		void SetParameters(string config);
	}
}
