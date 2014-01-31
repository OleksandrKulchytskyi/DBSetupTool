using System;
using System.Data;
using DBSetup.Common;
using DBSetup.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace DbSetup.UnitTest
{
	[TestClass]
	public class StringExtensionTest
	{
		[TestMethod]
		public void TestSecureSettings()
		{
			using (DBSetup.Common.ISqlConnectionSettings settings = new SqlConnectionSettings())
			{
				settings.Password = "HelloWorld";
				settings.DatabaseName = "sadasd";

				settings.Password = "HelloWorld3";

				string pass = settings.Password;
			}

		}

		[TestMethod]
		public void IsCommentColsed()
		{
			List<bool> values = new List<bool>();
			string commentLines = string.Format("asdds /* dfgdgdf */ {0} /* dsfsdfsdsdsfd /* sdfsdfsdfsdsdf */  sdfsdfsd*/ {0}  hello there unclosed /*dgfdfgdfgd /*dfgfdgdf */", Environment.NewLine);
			using (var sr = new System.IO.StringReader(commentLines))
			{
				string line = null;
				while ((line = sr.ReadLine()) != null)
				{
					values.Add(line.IsCommentLineClosed());
				}
			}

			if (values[2] != false)
				Assert.Fail();

			if (values[0] != true && values[1] != true)
				Assert.Fail();
		}

		[TestMethod]
		public void IsSqlContainsClosedComment()
		{
			string test = string.Empty;
			test = test.Trim();
			List<string> values = new List<string>();

			string commentLines = string.Format("Use master /* dfgdgdf */ {0} /* dsfsdfsdsdsfd /* sdfsdfsdfsdsdf */  sdfsdfsd*/ {0}  hello there unclosed /*dgfdfgdfgd /*dfgfdgdf */", Environment.NewLine);
			using (var sr = new System.IO.StringReader(commentLines))
			{
				string line = null;
				while ((line = sr.ReadLine()) != null)
				{
					if (line.IsCommentLineClosed())
					{
						string sql = line.GetSqlWithoutComment();
						if (!string.IsNullOrEmpty(sql) && !string.IsNullOrWhiteSpace(sql))
						{
							values.Add(sql);
						}
					}
				}
			}

			if (values.Count > 1)
				Assert.Fail();
		}

		[TestMethod]
		public void IsCommentLineClosedTest()
		{
			string testCommentLine = " /* sfdsfsdsffsdffsd /* dfgdfgdfgd */ dfgfdfdf";

			bool isCommentClosed = testCommentLine.IsCommentLineClosed();

			if (isCommentClosed)
				Assert.Fail();
		}

		[TestMethod]
		public void GetSqlVestion()
		{
			System.Data.SqlClient.SqlConnectionStringBuilder builder = new System.Data.SqlClient.SqlConnectionStringBuilder();
			builder.DataSource = "vinwt1112";
			builder.UserID = "sa";
			builder.Password = "Columbia03";
			int version = SqlServerHelper.GetDbVersionFromVesrsionTable(builder.ToString(), "Comm4");
			if (version == -1)
				Assert.Fail();
		}

		[TestMethod]
		public void TaskTest()
		{
			Task task = Task.Factory.StartNew(() =>
			{
				throw new InvalidOperationException("Test messsage fro exceptional state");
			});
			task.RegisterFaultedHandler(OnError);

			System.Threading.Thread.Sleep(10000);
		}

		private void OnError(Exception obj)
		{
			string msg1 = obj.Message;
			string msg2 = (obj as AggregateException).Flatten().InnerException.Message;
			System.Diagnostics.Debug.WriteLine(msg1);
			System.Diagnostics.Debug.WriteLine(msg2);
			Assert.AreNotSame(msg1, msg2);
		}

		[TestMethod]
		public void PerformanceTestMatchExact()
		{
			string fpath2 = @"D:\DbSetupPS360_2-13-2013\DB\Populate_Categories.sql";

			long time1 = 0;
			long time2 = 0;

			int count1 = 0;
			int count2 = 0;

			Stopwatch sw = new Stopwatch();
			StringBuilder sb = new StringBuilder();

			sw.Start();
			using (var sr = new System.IO.StreamReader(fpath2))
			{
				string line = string.Empty;
				bool isGoFound = false;
				while ((line = sr.ReadLine()) != null)
				{
					if (line.MatchExact("GO"))
					{
						isGoFound = true;
						count1++;
						continue;
					}
					else if (isGoFound && line.TrimStart().StartsWith(Environment.NewLine))
					{
						isGoFound = false;
						continue;
					}

					sb.AppendLine(line);
				}
				var overall = sb.ToString();
				sb.Clear();
				//sb = null;
				sr.Close();
			}
			sw.Stop();
			time1 = sw.ElapsedTicks;
			sw.Reset();

			sw.Start();
			using (var sr = new System.IO.StreamReader(fpath2))
			{
				string line = string.Empty;
				bool isGoFound = false;
				while ((line = sr.ReadLine()) != null)
				{
					if (line.ContainsOnly("GO"))
					{
						count2++;
						isGoFound = true;
						continue;
					}
					else if (isGoFound && line.TrimStart().StartsWith(Environment.NewLine))
					{
						isGoFound = false;
						continue;
					}
					sb.AppendLine(line);
				}
				var overall = sb.ToString();
				sb.Clear();
				//sb = null;
				sr.Close();
			}
			sw.Stop();
			time2 = sw.ElapsedTicks;
			sw.Reset();

			Assert.IsTrue(time1 != time2);
			Assert.IsTrue((time1 / time2) >= 2);
			Assert.IsTrue(count1 == count2);
		}

		[TestMethod]
		public void MatchExactSimpleTest()
		{
			int count = 0;
			string lines = string.Format("Hello world {0} this is our test going to performs {0} go {0} we are go {0} go", Environment.NewLine);
			using (var sr = new StringReader(lines))
			{
				string line = string.Empty;
				while ((line = sr.ReadLine()) != null)
				{
					if (line.ContainsOnly("GO"))
						count++;
				}
			}

			Assert.IsTrue(count == 2);
		}

		[TestMethod]
		public void CheckFormathMethodTest()
		{
			string source = "Hello world, {0}";
			string actual = source.FormatWith("1");
			Assert.IsTrue(actual.EndsWith("1"));
		}

		[TestMethod]
		public void StartsWithIgnoreSpacesTest()
		{
			string source = "  hello my name is";
			bool actual = source.StartsWithIgnoreSpaces("hello");
			Assert.IsTrue(actual);
		}

		[TestMethod]
		public void EndsWithIgnoreSpacesTest()
		{
			int indx = -1;
			string source = "  hello my name is  ";

			indx = source.IndexOf("hello");
			Assert.Equals(indx, 2);

			bool actual = source.EndsWithIgnoreSpaces("is");
			Assert.IsTrue(actual);
		}

		[TestMethod]
		public void TestProductivity()
		{
			StringBuilder sb = new StringBuilder(9500, 10000);
			for (int i = 0; i < 1000; i++)
			{
				sb.AppendLine("[Hello]");
			}

			Stopwatch sw = new Stopwatch();
			sw.Start();
			using (StringReader sr = new StringReader(sb.ToString()))
			{
				string line;
				while ((line = sr.ReadLine()) != null)
				{
					string s = line.Trim('[', ']');
				}
			}
			sw.Stop();
			long time1 = sw.ElapsedTicks;
			sw.Reset();

			sw.Start();
			using (StringReader sr = new StringReader(sb.ToString()))
			{
				string line;
				while ((line = sr.ReadLine()) != null)
				{
					string s = GetSecName(line);
				}
			}
			sw.Stop();
			long time2 = sw.ElapsedTicks;
			sw.Reset();

			sw.Start();
			using (StringReader sr = new StringReader(sb.ToString()))
			{
				string line;
				while ((line = sr.ReadLine()) != null)
				{
					string s = GetSecName2(line);
				}
			}
			sw.Stop();
			long time3 = sw.ElapsedTicks;
			Assert.IsTrue(time1 < time2);
			Assert.AreNotEqual(time2, time3);
		}

		[TestMethod]
		public void GetSecName2TestMethod()
		{
			string secname = GetSecName2(" [hello] ");
			Assert.IsTrue(secname.Equals("hello"));
		}

		private string GetSecName(string line)
		{
			if (string.IsNullOrEmpty(line))
				throw new ArgumentNullException(line);
			int indx1 = -1;
			int indx2 = -1;
			for (int i = 0; i < line.Length; i++)
			{
				if (line[i] == '[')
					indx1 = i;

				else if (line[i] == ']')
				{
					indx2 = i;
					break;
				}
			}

			if (indx1 != -1 && indx2 != -2)
				return line.Substring(indx1, indx2);
			return string.Empty;
		}

		private string GetSecName2(string line)
		{
			if (string.IsNullOrEmpty(line))
				throw new ArgumentNullException(line);
			int found = -1;
			var sb = new StringBuilder(line.Length - 2);
			for (int i = 0; i < line.Length; i++)
			{
				if (line[i] == '[')
				{
					found = i;
					continue;
				}
				else if (line[i] == ']')
					break;

				else if (found != -1)
					sb.Append(line[i]);
			}

			return sb.ToString();
		}
	}
}
