using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace DBSetup.Helpers
{
	public static class UiHelper
	{
		public static void ExecAction<Ctrl>(this Ctrl ctrl, Action action) where Ctrl : Control
		{
			if (ctrl == null || ctrl.IsDisposed)
				return;

			if (ctrl.InvokeRequired)
			{
				if (!ctrl.IsDisposed)
					ctrl.Invoke(new MethodInvoker(action));
			}
			else
				action();
		}

		public static void DisableCtrlPlusA(this UserControl uc)
		{
			if (uc == null)
				return;

			List<TextBox> controlList = uc.Controls.OfType<TextBox>().ToList();

			if (controlList == null)
				return;

			else if (controlList.Count == 0)
			{
				List<GroupBox> grpList = uc.Controls.OfType<GroupBox>().ToList();
				foreach (GroupBox gb in grpList)
				{
					if (gb.Controls == null)
						continue;
					foreach (var tb in gb.Controls.OfType<TextBox>().ToList())
					{
						tb.KeyDown += tb_KeyDown;
					}
				}
				return;
			}

			foreach (TextBox tb in controlList)
			{
				tb.KeyDown += tb_KeyDown;
			}
		}

		static void tb_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Control && e.KeyCode == Keys.A && sender is TextBox)
			{
				e.SuppressKeyPress = true;
			}
		}

		public static void RevertCtrlPlusA(this UserControl uc)
		{
			if (uc == null)
				return;

			List<TextBox> controlList = uc.Controls.OfType<TextBox>().ToList();
			if (controlList == null)
				return;

			else if (controlList.Count == 0)
			{
				List<GroupBox> grpList = uc.Controls.OfType<GroupBox>().ToList();
				foreach (GroupBox gb in grpList)
				{
					if (gb.Controls == null)
						continue;
					foreach (var tb in gb.Controls.OfType<TextBox>().ToList())
					{
						tb.KeyDown -= tb_KeyDown;
					}
				}
				return;
			}

			foreach (TextBox tb in controlList)
			{
				tb.KeyDown -= tb_KeyDown;
			}
		}

		public static void CloseWindow(string title)
		{
			Win32Api.closeWindow(title);
		}

		private class Win32Api
		{
			private const string _user32 = "user32.dll";
			[DllImport(_user32)]
			private static extern int FindWindow(string lpClassName, string lpWindowName);
			[DllImport(_user32)]
			private static extern int SendMessage(int hWnd, uint Msg, int wParam, int lParam);
			/// <summary>
			/// Find window by Caption only. Note you must pass IntPtr.Zero as the first parameter.
			/// </summary>
			[DllImport(_user32, EntryPoint = "FindWindow", SetLastError = true)]
			private static extern IntPtr FindWindowByCaption(IntPtr ZeroOnly, string lpWindowName);

			public const int WM_SYSCOMMAND = 0x0112;
			public const int SC_CLOSE = 0xF060;

			public static void closeWindow(string title)
			{
				if (string.IsNullOrEmpty(title))
					return;
				// retrieve the handler of the window  
				IntPtr iHandle = FindWindowByCaption(IntPtr.Zero, title);
				if (iHandle.ToInt32() > 0)
				{
					// close the window using API        
					SendMessage(iHandle.ToInt32(), WM_SYSCOMMAND, SC_CLOSE, 0);
				}
			}
		}
	}

	/// <summary> System.Windows.Forms utilities </summary>
	public static class WinFormUtils
	{
		/// <summary> Processes all Paint events only </summary>
		public static void DoPaintEvents()
		{
			//MessageFilter registration
			Application.AddMessageFilter(PaintMessageFilter.Instance);
			//Process messages in the queue
			Application.DoEvents();
			//MessageFilter desregistration
			Application.RemoveMessageFilter(PaintMessageFilter.Instance);
		}

		/// <summary> Custom message filter </summary>
		private class PaintMessageFilter : IMessageFilter
		{
			static public IMessageFilter Instance = new PaintMessageFilter();

			#region IMessageFilter Members

			/// <summary> Message filter function </summary>
			public bool PreFilterMessage(ref System.Windows.Forms.Message m)
			{
				return (m.Msg != 0x000F); //WM_PAINT -> we only let WM_PAINT messages through
			}

			#endregion
		}
	}
}
