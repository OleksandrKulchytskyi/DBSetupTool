using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace DBSetup.Common.Native
{
	public static class Win32
	{
		private const string _kernel = "kernel32.dll";

		[DllImport(_kernel)]
		private static extern bool AttachConsole(int dwProcessId);

		[DllImport(_kernel, SetLastError = true)]
		internal static extern int AllocConsole();

		[DllImport(_kernel)]
		private static extern bool FreeConsole();

		private const int ATTACH_PARENT_PROCESS = -1;

		public static void AttachConsoleNative()
		{
			if (AttachConsole(ATTACH_PARENT_PROCESS))
				throw new Win32Exception(Marshal.GetLastWin32Error());
		}

		public static void AllocConsoleNative()
		{
			if (AllocConsole() == 0)
				throw new Win32Exception(Marshal.GetLastWin32Error());
		}

		public static void FreeConsoleNative()
		{
			if (FreeConsole())
				throw new Win32Exception(Marshal.GetLastWin32Error());
		}
	}

	public interface IConsoleWriter
	{
		void WriteLine(string line);
	}

	// This always writes to the parent console window and also to a redirected stdout if there is one.
	// It would be better to do the relevant thing (eg write to the redirected file if there is one, otherwise
	// write to the console) but it doesn't seem possible.
	public sealed class GUIConsoleWriter : IDisposable, IConsoleWriter
	{
		private const string _kernel = "kernel32.dll";
		private int _disposed = 0;

		private const UInt32 ATTACH_PARENT_PROCESS = 0xFFFFFFFF;
		private const UInt32 STD_OUTPUT_HANDLE = 0xFFFFFFF5;
		private const UInt32 STD_ERROR_HANDLE = 0xFFFFFFF4;
		private const UInt32 DUPLICATE_SAME_ACCESS = 2;

		[DllImport(_kernel)]
		private static extern bool GetFileInformationByHandle(SafeFileHandle hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);
		[DllImport(_kernel)]
		private static extern SafeFileHandle GetStdHandle(UInt32 nStdHandle);
		[DllImport(_kernel)]
		private static extern bool SetStdHandle(UInt32 nStdHandle, SafeFileHandle hHandle);
		[DllImport(_kernel)]
		private static extern bool DuplicateHandle(IntPtr hSourceProcessHandle, SafeFileHandle hSourceHandle, IntPtr hTargetProcessHandle,
		out SafeFileHandle lpTargetHandle, UInt32 dwDesiredAccess, Boolean bInheritHandle, UInt32 dwOptions);

		[DllImport(_kernel)]
		private static extern uint AttachConsole(UInt32 dwProcessId);
		[DllImport(_kernel, SetLastError = true)]
		private static extern int AllocConsole();
		[DllImport(_kernel)]
		private static extern uint FreeConsole();

		//private const int ATTACH_PARENT_PROCESS = -1;
		private StreamWriter _stdOutWriter = null;

		struct BY_HANDLE_FILE_INFORMATION
		{
			public UInt32 FileAttributes;
			public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
			public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
			public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
			public UInt32 VolumeSerialNumber;
			public UInt32 FileSizeHigh;
			public UInt32 FileSizeLow;
			public UInt32 NumberOfLinks;
			public UInt32 FileIndexHigh;
			public UInt32 FileIndexLow;
		}

		private SafeFileHandle hStdOut, hStdErr, hStdOutDup, hStdErrDup;

		public bool Disposed
		{
			get { return _disposed == 1; }
		}

		// this must be called early in the program
		public GUIConsoleWriter()
		{
			// this needs to happen before attachconsole.
			// If the output is not redirected we still get a valid stream but it doesn't appear to write anywhere
			// I guess it probably does write somewhere, but nowhere I can find out about
			var stdout = Console.OpenStandardOutput();
			_stdOutWriter = new StreamWriter(stdout);
			_stdOutWriter.AutoFlush = true;
		}

		~GUIConsoleWriter()
		{
			Dispose();
		}

		public void InitHandles()
		{
			BY_HANDLE_FILE_INFORMATION bhfi;
			hStdOut = GetStdHandle(STD_OUTPUT_HANDLE);
			hStdErr = GetStdHandle(STD_ERROR_HANDLE);
			IntPtr hProcess = Process.GetCurrentProcess().Handle;// Get current process handle

			DuplicateHandle(hProcess, hStdOut, hProcess, out hStdOutDup, 0, true, DUPLICATE_SAME_ACCESS);// Duplicate Stdout handle to save initial value
			DuplicateHandle(hProcess, hStdErr, hProcess, out hStdErrDup, 0, true, DUPLICATE_SAME_ACCESS);// Duplicate Stderr handle to save initial value

			if (AttachConsole(ATTACH_PARENT_PROCESS) == 0)// Attach to console window – this may modify the standard handles
				throw new Win32Exception(Marshal.GetLastWin32Error());

			if (GetFileInformationByHandle(GetStdHandle(STD_OUTPUT_HANDLE), out bhfi))// Adjust the standard handles
				SetStdHandle(STD_OUTPUT_HANDLE, hStdOutDup);
			else
				SetStdHandle(STD_OUTPUT_HANDLE, hStdOut);

			if (GetFileInformationByHandle(GetStdHandle(STD_ERROR_HANDLE), out bhfi))
				SetStdHandle(STD_ERROR_HANDLE, hStdErrDup);
			else
				SetStdHandle(STD_ERROR_HANDLE, hStdErr);
		}

		public void WriteLine(string line)
		{
			if (_stdOutWriter != null)
			{
				try
				{
					_stdOutWriter.WriteLine(line);
					Console.WriteLine(line);
				}
				catch { }
			}
		}

		public void Dispose()
		{
			if (_disposed == 0)
			{
				_disposed = 1;
				try
				{
					if (!hStdErr.IsInvalid && !hStdErr.IsClosed) hStdErr.Close();
					if (!hStdErrDup.IsInvalid && !hStdErrDup.IsClosed) hStdErrDup.Close();
					if (!hStdOut.IsInvalid && !hStdOut.IsClosed) hStdOut.Close();
					if (!hStdOutDup.IsInvalid && !hStdOutDup.IsClosed) hStdOutDup.Close();
					//make handles rootless
					hStdErr = null;
					hStdErrDup = null;
					hStdOut = null;
					hStdOutDup = null;

					if (FreeConsole() == 0)//free console 
						throw new Win32Exception(Marshal.GetLastWin32Error());

					if (_stdOutWriter != null)//clsoe output stream
					{
						_stdOutWriter.Close();
						_stdOutWriter.Dispose();
					}
				}
				catch (Exception ex) { Log.Instance.Error("Dispose", ex); }
				finally
				{
					GC.SuppressFinalize(this);
				}
			}
		}
	}
}