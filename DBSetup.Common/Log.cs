using DBSetup.Common.Helpers;
using log4net;
using System;
using System.ComponentModel.Composition;

namespace DBSetup.Common
{
	[Export(typeof(ILog))]
	public sealed class Log : SingletonBase<Log>, ILog, IDisposable
	{
		private bool _isDisposed = false;
		private log4net.ILog _Logger = null;
		private Log()
		{
			_Logger = LogManager.GetLogger(typeof(Log).FullName);
			log4net.Config.XmlConfigurator.Configure();
		}

		public void Fatal(string message, Exception ex)
		{
			ThrowExceptionIfDisposed();
			_Logger.Fatal(message, ex);
		}

		public void Error(string message)
		{
			ThrowExceptionIfDisposed();
			_Logger.Error(message);
		}

		public void Error(string message, Exception ex)
		{
			ThrowExceptionIfDisposed();
			_Logger.Error(message, ex);
		}

		public void Info(string message)
		{
			ThrowExceptionIfDisposed();
			_Logger.Info(message);
		}

		public void Warn(string message)
		{
			ThrowExceptionIfDisposed();
			_Logger.Warn(message);
		}

		public void Dispose()
		{
			ThrowExceptionIfDisposed();
			_isDisposed = true;
			log4net.LogManager.Shutdown();
		}

		private void ThrowExceptionIfDisposed()
		{
			if (_isDisposed)
				throw new ObjectDisposedException("Log.Instance");
		}
	}
}
