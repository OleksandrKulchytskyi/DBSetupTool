using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DBSetup.Common;
using DBSetup.Common.Services;

namespace DbSetup.UnitTest
{
	[TestClass]
	public class VersionServiceTest
	{
		[TestMethod]
		public void RetrieveVersionTest()
		{
			IVersionService version = new VesrionService();
			version.SetSource(@"D:\DBCreate\populate.sql");

			int data = version.RetrieveVersion();
			Assert.IsTrue(data != -1 && data > 10);
		}

		[TestMethod]
		public void MyTestMethod()
		{
			int a = 10;
			int b = 12;
			a = a + b;
			b = a - b;
			a = a - b;
			Assert.IsTrue(b == 10);
			Assert.IsTrue(a == 12);

			a = 9;
			b = 12;
			a ^= b;
			b ^= a;
			a ^= b;
			Assert.IsTrue(b == 9);
			Assert.IsTrue(a == 12);
			
		}
	}
}
