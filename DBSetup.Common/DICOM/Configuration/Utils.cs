using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;

namespace DBSetup.Common
{
	public class Utils
	{
		#region AppSettings

		/// <summary>
		/// Returns the App Setting value
		/// </summary>
		/// <param name="name">Setting Name</param>
		/// <param name="defaultValue">Default value is setting is not found</param>
		/// <returns></returns>
		public static string GetAppSetting(string name, string defaultValue)
		{
			return string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings[name]) ? defaultValue : ConfigurationManager.AppSettings[name];
		}

		/// <summary>
		/// Returns the App Setting value and normalize path, just by adding addendum
		/// </summary>
		/// <param name="name">Setting Name</param>
		/// <param name="defaultValue">Default value is setting is not found</param>
		/// <returns></returns>
		public static string GetAppSettingAndNormalizePath(string name, IServiceLocator ioc, string defaultValue)
		{
			string result= string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings[name]) ? defaultValue : ConfigurationManager.AppSettings[name];
			if(!result.Equals(defaultValue))
			{
				result = NormalizePath(result, ioc, "rootPath");
			}

			return result;
		}

		public static string NormalizePath(string path, IServiceLocator locator, string valueKey)
		{
			string rootFolder = locator.GetService<IGlobalState>().GetState<string>(valueKey);
			string normalizedPath = path;
			int ubnormalIndx = 0;

			normalizedPath = normalizedPath.StartsWith(".\\") ? System.IO.Path.Combine(rootFolder, normalizedPath) : normalizedPath;
			ubnormalIndx = normalizedPath.IndexOf(@"\.\", StringComparison.OrdinalIgnoreCase);
			normalizedPath = ubnormalIndx > 1 ? normalizedPath.Replace(@"\.\", @"\") : normalizedPath;
			return normalizedPath;
		}

		/// <summary>
		/// Returns the App Setting value
		/// </summary>
		/// <param name="name">Setting Name</param>
		/// <param name="defaultValue">Default value is setting is not found</param>
		/// <returns></returns>
		public static int GetAppSetting(string name, int defaultValue)
		{
			int result = defaultValue;
			if (!string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings[name]))
			{
				if (!Int32.TryParse(ConfigurationManager.AppSettings[name], out result))
				{
					result = defaultValue;
				}
			}
			return result;
		}

		/// <summary>
		/// Returns the App Setting value
		/// </summary>
		/// <param name="name">Setting Name</param>
		/// <param name="defaultValue">Default value is setting is not found</param>
		/// <returns></returns>
		public static float GetAppSetting(string name, float defaultValue)
		{
			float result = defaultValue;
			if (!string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings[name]))
			{
				if (!float.TryParse(ConfigurationManager.AppSettings[name], out result))
				{
					result = defaultValue;
				}
			}
			return result;
		}

		/// <summary>
		/// Returns the App Setting value
		/// </summary>
		/// <param name="name">Setting Name</param>
		/// <param name="defaultValue">Default value is setting is not found</param>
		/// <returns></returns>
		public static bool GetAppSetting(string name, bool defaultValue)
		{
			bool result = defaultValue;
			if (!string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings[name]))
			{
				if (!bool.TryParse(ConfigurationManager.AppSettings[name], out result))
				{
					result = defaultValue;
				}
			}
			return result;
		}


		/// <summary>
		/// Returns the App Setting value
		/// </summary>
		/// <typeparam name="T">Enumeration Type</typeparam>
		/// <param name="name">Setting Name</param>
		/// <param name="defaultValue">Default value is setting is not found</param>
		/// <returns></returns>
		public static T GetAppSetting<T>(string name, T defaultValue)
		{
			return (T)Enum.Parse(typeof(T), GetAppSetting(name, defaultValue.ToString()));
		}

		#endregion
	}
}
