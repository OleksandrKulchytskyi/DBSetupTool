
namespace DBSetup.Common
{
	public interface IVersionService
	{
		void SetSource(string fpath);
		int RetrieveVersion();
	}
}
