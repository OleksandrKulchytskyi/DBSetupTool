
namespace DBSetup.Common
{
	public interface IExecutor
	{
		void SetParameters(string config);
		void Execute();
	}
}
