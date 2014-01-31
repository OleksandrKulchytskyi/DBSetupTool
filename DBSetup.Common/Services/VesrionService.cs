using System;
using System.IO;
using System.Text;

namespace DBSetup.Common.Services
{
	public class VesrionService : IVersionService
	{
		private string _sourceFile;

		public int RetrieveVersion()
		{
			int version = -1;
			using (StreamReader sr = new StreamReader(_sourceFile, true))
			{
				string line = null;
				while ((line = sr.ReadLine()) != null)
				{
					if (ParseLine(line, ref version))
						break;
				}
			}

			return version;
		}

		private bool ParseLine(string line, ref int version)
		{
			if (string.IsNullOrEmpty(line))
				return false;

			if (line.IndexOf("INSERT INTO", StringComparison.OrdinalIgnoreCase) != -1 &&
				line.IndexOf("Version", StringComparison.OrdinalIgnoreCase) != -1 &&
				line.IndexOf("VALUES", StringComparison.OrdinalIgnoreCase) != -1)
			{
				int valIndx = line.IndexOf("values", StringComparison.OrdinalIgnoreCase);
				string rest = line.Substring(valIndx + 6);
				int i = 0;
				while (!char.IsDigit(rest[i]))
				{
					i++;
					if (i > rest.Length - 1)
						break;
				}

				StringBuilder versionBuilder = new StringBuilder();
				while (char.IsDigit(rest[i]))
				{
					versionBuilder.Append(rest[i]);
					i++;
					if (i > rest.Length - 1)
						break;
				}
				if (Int32.TryParse(versionBuilder.ToString(), out version))
					return true;
				else
					throw new InvalidProgramException("Fail to parse latest database version");

			}

			return false;
		}

		public void SetSource(string fpath)
		{
			if (string.IsNullOrEmpty(fpath) && System.IO.File.Exists(fpath))
				throw new ArgumentException(fpath);
			_sourceFile = fpath;
		}
	}
}