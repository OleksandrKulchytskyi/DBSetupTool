using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DBSetup.Common;
using DBSetup.Common.Services;

namespace DbSetup.UnitTest
{
	[TestClass]
	public class NoUiExecutorTest
	{
		[TestMethod]
		public void TestNoUIExecutor()
		{
			string config = @"<Settings>
	<INI path=""D:\DbSetupPS360_2-13-2013\DB\SQLDBSetup.ini""/>
	<SqlSever>
		<Database>Comm4</Database>
		<Server>VINW4S3S85J</Server>
		<User>sa</User>
		<Password>Columbia03</Password>
	</SqlSever>
	<Setup>
		<Language>US English</Language>
		<Type>New PS360 Database</Type>
	</Setup>
	<Procedures>
		<spHealthSystem>dbo.ps360_CreateHeathSystem</spHealthSystem>
		<spUser>dbo.PS360_AddSQLUsers</spUser>
	</Procedures>
	<Final>
		<HealthName>Test health</HealthName>
		<SiteName>Test site</SiteName>
		<Workflow>Radiology</Workflow>
		<User>admin</User>
		<Password>123dsfsd</Password>
	</Final>
</Settings>";

			IExecutor exec = new NoUIExecutor();
			exec.SetParameters(config);
			try
			{
				exec.Execute();
			}
			catch (Exception)
			{
				Assert.Fail("Something was wrong");
			}
		}
	}
}
