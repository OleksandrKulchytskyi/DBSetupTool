using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DBSetup.Common.Native;

namespace DbSetup.UnitTest
{
	[TestClass]
	public class ConsoleGuiWriterTest
	{
		[TestMethod]
		[ExpectedException(typeof(System.ComponentModel.Win32Exception))]
		public void TestConsoleWrapper()
		{
			try
			{
				GUIConsoleWriter native = new GUIConsoleWriter();
				native.InitHandles();

				native.Dispose();
				Assert.IsTrue(native.Disposed);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(ex.Message);
				throw;
			}
		}
	}
}
