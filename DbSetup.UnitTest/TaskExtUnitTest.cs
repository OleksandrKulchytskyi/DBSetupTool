using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DBSetup.Common.Helpers;
using System.Threading.Tasks;
using System.Threading;

namespace DbSetup.UnitTest
{
	[TestClass]
	public class TaskExtUnitTest
	{
		private ManualResetEvent mre = null;

		[TestInitialize]
		public void InitTest()
		{
			mre = new ManualResetEvent(false);
		}

		[TestCleanup]
		public void OnCleanup()
		{
			mre.Dispose();
		}

		[TestMethod]
		public void CheckSuccededHandler()
		{
			Task mainTask = Task.Factory.StartNew(SimulateLongWork);
			mainTask.RegisterSucceededHandler(OnSucceed);
			if (!mre.WaitOne(TimeSpan.FromSeconds(2)))
				Assert.Fail();
			else
				System.Diagnostics.Debug.WriteLine("Test passed");
		}

		private void OnSucceed()
		{
			mre.Set();
		}

		private void SimulateLongWork()
		{
			Thread.Sleep(TimeSpan.FromSeconds(1));
		}

		[TestMethod]
		public void CheckErrorHandler()
		{
			Task mainTask = Task.Factory.StartNew(SimulateLongWorkWithError);
			mainTask.RegisterSucceededHandler(OnSucceed);
			mainTask.RegisterFaultedHandler(OnError);
			if (!mre.WaitOne(TimeSpan.FromSeconds(2)))
				Assert.Fail();
			else
				System.Diagnostics.Debug.WriteLine("Test passed");
		}

		private void OnError(Exception obj)
		{
			mre.Set();
		}

		private void SimulateLongWorkWithError()
		{
			Thread.Sleep(TimeSpan.FromSeconds(1));
			throw new NotImplementedException();
		}


		[TestMethod]
		public void CheckCancellationHandler()
		{
			CancellationTokenSource cts = new CancellationTokenSource();

			Task mainTask = Task.Factory.StartNew(SimulateLongWork, cts.Token);
			cts.Cancel();
			mainTask.RegisterCancelledHandler(OnCancelled);
			mainTask.RegisterSucceededHandler(OnSucceed);
			mainTask.RegisterFaultedHandler(OnError);

			if (!mre.WaitOne(TimeSpan.FromSeconds(2)))
			{
				cts.Dispose();
				Assert.Fail();
			}
			else
			{
				cts.Dispose();
				System.Diagnostics.Debug.WriteLine("Test passed");
			}
		}

		private void OnCancelled()
		{
			mre.Set();
		}
	}
}
