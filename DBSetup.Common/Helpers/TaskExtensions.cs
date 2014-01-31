using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DBSetup.Common.Helpers
{
	public static class TaskExtensions
	{
		/// <summary> 
		/// Registers a UI thread handler for when the specified task finishes execution,
		/// whether it finishes with success, failiure, or cancellation. 
		/// </summary> 
		/// <param name="task">The task to monitor for completion.</param> 
		/// <param name="action">The action to take when the task has completed, in the context of the UI thread.</param> 
		/// <returns>The continuation created to handle completion. This is normally ignored.</returns> 
		public static Task RegisterContinuation(this Task task, Action action, TaskScheduler scheduler = null)
		{
			return task.ContinueWith(_ => action(), CancellationToken.None, TaskContinuationOptions.None,
				scheduler == null ? TaskScheduler.Default : scheduler);
		}

		/// <summary> 
		/// Registers a UI thread handler for when the specified task successfully finishes execution
		/// and returns a result. 
		/// </summary> 
		/// <typeparam name="TResult">The type of the task result.</typeparam> 
		/// <param name="task">The task to monitor for successful completion.</param> 
		/// <param name="action">The action to take when the task has successfully completed, in the context of the UI thread.
		/// The argument to the action is the return value of the task.</param> 
		/// <returns>The continuation created to handle successful completion. This is normally ignored.</returns> 
		public static Task RegisterSucceededHandler<TResult>(this Task<TResult> task, Action<TResult> action, TaskScheduler scheduler = null)
		{
			return task.ContinueWith(t => action(t.Result), CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion,
				scheduler == null ? TaskScheduler.Default : scheduler);
		}

		public static Task RegisterSucceededHandler(this Task task, Action action, TaskScheduler scheduler = null)
		{
			return task.ContinueWith(t => action(), CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion,
				scheduler == null ? TaskScheduler.Default : scheduler);
		}

		/// <summary> 
		/// Registers a UI thread handler for when the specified task becomes faulted. 
		/// </summary> 
		/// <param name="task">The task to monitor for faulting.</param> 
		/// <param name="action">The action to take when the task has faulted, in the context of the UI thread.</param> 
		/// <returns>The continuation created to handle faulting. This is normally ignored.</returns> 
		public static Task RegisterFaultedHandler(this Task task, Action<Exception> action, TaskScheduler scheduler = null)
		{
			return task.ContinueWith(t => action(t.Exception), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted,
					scheduler == null ? TaskScheduler.Default : scheduler);
		}

		/// <summary> 
		/// Registers a UI thread handler for when the specified task is cancelled. 
		/// </summary> 
		/// <param name="task">The task to monitor for cancellation.</param> 
		/// <param name="action">The action to take when the task is cancelled, in the context of the UI thread.</param> 
		/// <returns>The continuation created to handle cancellation. This is normally ignored.</returns> 
		public static Task RegisterCancelledHandler(this Task task, Action action, TaskScheduler scheduler = null)
		{
			return task.ContinueWith(_ => action(), CancellationToken.None, TaskContinuationOptions.OnlyOnCanceled,
				scheduler == null ? TaskScheduler.Default : scheduler);
		}

		/// <summary> 
		/// Registers a UI thread handler for when the specified task is cancelled. 
		/// </summary> 
		/// <typeparam name="TResult">The type of the task result.</typeparam> 
		/// <param name="task">The task to monitor for cancellation.</param> 
		/// <param name="action">The action to take when the task is cancelled, in the context of the UI thread.</param> 
		/// <returns>The continuation created to handle cancellation. This is normally ignored.</returns> 
		public static Task RegisterCancelledHandler<TResult>(this Task<TResult> task, Action action, TaskScheduler scheduler = null)
		{
			return task.ContinueWith(_ => action(), CancellationToken.None, TaskContinuationOptions.OnlyOnCanceled,
				scheduler == null ? TaskScheduler.Default : scheduler);
		}

	}
}
