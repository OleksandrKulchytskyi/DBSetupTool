using DBSetup.Common;
using DBSetup.Common.Statements;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Reflection;

namespace DBSetup.States
{
	internal sealed class RunScriptState : IState, IDisposable
	{
		private readonly string _name = "RunScriptState";
		private CompositionContainer _container = null;
		private uint disposed = 0;

		public RunScriptState()
		{
			IsSuccessed = -1;
		}

		public string Name
		{
			get { return _name; }
		}

		public int IsSuccessed { get; set; }

		public List<IDataStatement> DataStatements { get; set; }

		[Import(typeof(IDataStatementFactory))]
		public Lazy<IDataStatementFactory> StatementFactory { get; set; }

		[Import(typeof(StateDBSettings))]
		public StateDBSettings DbConSettings { get; set; }

		public void ComposeParts()
		{
			try
			{
				if (_container != null)
				{
					_container.Dispose();
					_container = null;
				}
			}
			catch (Exception ex)
			{
				Log.Instance.Error(string.Empty, ex);
			}

			try
			{
				using (AggregateCatalog agCatalog = new AggregateCatalog())
				{
					agCatalog.Catalogs.Add(new AssemblyCatalog(Assembly.GetExecutingAssembly()));
					agCatalog.Catalogs.Add(new AssemblyCatalog(typeof(Common.IDataStatementFactory).Assembly));

					_container = new CompositionContainer(agCatalog);

					_container.ComposeParts(this, StateContainer.Instance.GetState<DbSetupState>(),
											StateContainer.Instance.GetState<ConfigFileState>(),
											StateContainer.Instance.GetState<StateDBSettings>());
				}
			}
			catch (CompositionException ex)
			{
				Log.Instance.Fatal("Fail to compose parts.", ex);
				throw;
			}

			if (StatementFactory.Value != null)
			{
				var val = StatementFactory.Value;
				if ((val as Common.ModelBuilder.DataStatementsFactory).SelectedSetupConfig != null)
				{
					Log.Instance.Info("MEF initialization has been successfully performed.");
				}
			}
		}

		public void Dispose()
		{
			if (disposed == 1)
				return;
			disposed = 0;

			if (DataStatements != null && DataStatements.Count > 0)
				DataStatements.Clear();

			if (_container != null)
			{
				_container.Dispose();
				_container = null;
			}
		}
	}
}