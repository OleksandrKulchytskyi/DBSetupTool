using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;

namespace DBSetup.Common
{
	public interface ISqlConnectionSettings : IDisposable
	{
		string ServerName { get; set; }

		string DatabaseName { get; set; }

		string UserName { get; set; }

		string Password { get; set; }
	}

	public class SqlConnectionSettings : ISqlConnectionSettings
	{
		public string ServerName { get; set; }

		public string DatabaseName { get; set; }

		public string UserName { get; set; }

		private SecureString _secure;
		public string Password
		{
			get
			{
				if (_secure == null)
					return string.Empty;
				else
					return ConvertToUnsecureString( _secure);
			}
			set
			{
				if (_secure == null)
					_secure = new SecureString();
				else _secure.Clear();

				if (!string.IsNullOrEmpty(value))
				{
					foreach (char item in value.ToCharArray())
					{
						_secure.AppendChar(item);
					}
				}
			}
		}

		private string ConvertToUnsecureString(SecureString securePassword)
		{
			if (securePassword == null)
				throw new ArgumentNullException("securePassword");

			IntPtr unmanagedString = IntPtr.Zero;
			try
			{
				unmanagedString = System.Runtime.InteropServices.Marshal.SecureStringToGlobalAllocUnicode(securePassword);
				return System.Runtime.InteropServices.Marshal.PtrToStringUni(unmanagedString);
			}
			finally
			{
				System.Runtime.InteropServices.Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
			}
		}

		//private SecureString ConvertToSecureString(string password)
		//{
		//	if (password == null)
		//		throw new ArgumentNullException("password");

		//	unsafe
		//	{
		//		fixed (char* passwordChars = password)
		//		{
		//			var securePassword = new SecureString(passwordChars, password.Length);
		//			securePassword.MakeReadOnly();
		//			return securePassword;
		//		}
		//	}
		//}

		public override string ToString()
		{
			return string.Format("Server: {1}{0}Database: {2}{0}User: {3}{0}Password: {4}", Environment.NewLine, ServerName, DatabaseName, UserName, Password);
		}

		public void Dispose()
		{
			if (_secure != null && _secure.Length > 0)
			{
				_secure.Clear();
				_secure.Dispose();
			}
		}
	}
}
