using DBSetup.Common.Helpers;
using DBSetup.Common.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DBSetup.Common.ModelBuilder
{
	public class ScriptConsequencyBuilder
	{
		private const string _msgBegin = "(begin)";
		private const string _msgEnd = "(end)";
		private const string _msgFormatTemplate = "{0} [{1}] {2}";
		private const string _msgAddScripting = "adding script and dialog generated statements";
		private readonly string _msgBuildFail = "Fail to build setup script tree";

		private GenericWeakReference<SetupScriptDocument> _docWeak = null;
		private readonly object _locker;

		public ScriptConsequencyBuilder()
		{
			_docWeak = new GenericWeakReference<SetupScriptDocument>(new SetupScriptDocument());
			_docWeak.Target.InsertString(0, _msgAddScripting);
			_locker = new object();
		}

		#region Properties

		private bool _isTaskCompleted = false;
		public bool TaskCompleted
		{
			get
			{
				lock (_locker)
				{
					return _isTaskCompleted;
				}
			}
			set
			{
				lock (_locker)
					_isTaskCompleted = value;
			}
		}

		private bool _isTaskStarted = false;
		public bool TaskStarted
		{
			get
			{
				lock (_locker)
				{
					return _isTaskStarted;
				}
			}
			set
			{
				lock (_locker)
					_isTaskStarted = value;
			}
		}
		#endregion

		public void Build(SectionBase setupSection)
		{
			if (setupSection == null)
				throw new ArgumentNullException("setupSection");

			_docWeak.Target.InsertStringFormat(0, _msgFormatTemplate, setupSection.FileName, setupSection.Text, _msgBegin);
			foreach (var section in setupSection.Children)
			{
				BuildSubTree(section, section.Children, 2);
			}
			_docWeak.Target.InsertStringFormat(0, _msgFormatTemplate, setupSection.FileName, setupSection.Text, _msgEnd);
		}

		public void BuildAsync(SectionBase setupSection)
		{
			var buildTaks = Task.Factory.StartNew(() => { TaskCompleted = false; TaskStarted = true; Build(setupSection); });

			buildTaks.ContinueWith(prevTask =>
			{
				TaskStarted = false; TaskCompleted = true;
				prevTask.Dispose();
			}, TaskContinuationOptions.OnlyOnRanToCompletion);

			buildTaks.ContinueWith(prevTask =>
			{
				TaskStarted = false; TaskCompleted = true;
				Log.Instance.Error(_msgBuildFail, prevTask.Exception);
				prevTask.Dispose();
			}, TaskContinuationOptions.NotOnRanToCompletion);
		}

		public Task<SetupScriptDocument> AsyncBuild(SectionBase setupSection)
		{
			TaskCompletionSource<SetupScriptDocument> tcs = new TaskCompletionSource<SetupScriptDocument>();

			var buildTaks = Task.Factory.StartNew(() => { TaskCompleted = false; TaskStarted = true; Build(setupSection); });

			buildTaks.ContinueWith(prevTask =>
			{
				tcs.TrySetResult(_docWeak.Target);
				TaskStarted = false; TaskCompleted = true;
				//prevTask.Dispose();
			}, TaskContinuationOptions.OnlyOnRanToCompletion);

			buildTaks.ContinueWith(prevTask =>
			{
				tcs.TrySetException(prevTask.Exception.Flatten().InnerException);
				TaskStarted = false; TaskCompleted = true;
				Log.Instance.Error(_msgBuildFail, prevTask.Exception);
				//prevTask.Dispose();
			}, TaskContinuationOptions.NotOnRanToCompletion);

			return tcs.Task;
		}

		public SetupScriptDocument GetDocumentResult()
		{
			return _docWeak.Target;
		}

		void BuildSubTree(SectionBase subSection, List<SectionBase> children, int offset)
		{
			offset = offset + 3;
			switch (subSection.GetType().Name)
			{
				case "BlobLink":
					_docWeak.Target.InsertStringFormat(offset + 1, "Blob={0} {1}", (subSection as BlobLink).BlobFilePath, (subSection as BlobLink).BlobContent);
					break;

				case "DICOMLink":
					_docWeak.Target.InsertStringFormat(offset + 1, "DICOM={0},{1}", (subSection as DICOMLink).CSVFilePath, (subSection as DICOMLink).IsActive ? "1" : "0");
					break;

				case "SqlLink":
					_docWeak.Target.InsertStringFormat(offset + 1, "SQL={0}", (subSection as SqlLink).SqlFilePath);
					break;

				case "SectionBase":
					_docWeak.Target.InsertStringFormat(offset, _msgFormatTemplate, subSection.FileName, subSection.Text, _msgBegin);
					if (subSection.Children != null && subSection.Children.Count > 0)
					{
						foreach (var child in subSection.Children)
						{
							BuildSubTree(child, child.Children, offset);
						}
					}
					_docWeak.Target.InsertStringFormat(offset, _msgFormatTemplate, subSection.FileName, subSection.Text, _msgEnd);
					break;

				default:
					break;
			}

		}
	}
}
