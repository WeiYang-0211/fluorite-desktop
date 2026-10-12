using System;
using System.Runtime.InteropServices;

namespace Launchpad.Interop;

internal static class NativeMethods
{
	public struct POINT
	{
		public int X;

		public int Y;
	}

	public struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;

		public int Width => Right - Left;

		public int Height => Bottom - Top;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	public struct MONITORINFO
	{
		public int cbSize;

		public RECT rcMonitor;

		public RECT rcWork;

		public int dwFlags;
	}

	public struct FILETIME
	{
		public uint dwLowDateTime;

		public uint dwHighDateTime;

		public ulong ToUInt64()
		{
			return ((ulong)dwHighDateTime << 32) | dwLowDateTime;
		}
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	public class MEMORYSTATUSEX
	{
		public uint dwLength;

		public uint dwMemoryLoad;

		public ulong ullTotalPhys;

		public ulong ullAvailPhys;

		public ulong ullTotalPageFile;

		public ulong ullAvailPageFile;

		public ulong ullTotalVirtual;

		public ulong ullAvailVirtual;

		public ulong ullAvailExtendedVirtual;

		public MEMORYSTATUSEX()
		{
			dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
		}
	}

	public const int WM_HOTKEY = 786;

	public const int DWMWA_CLOAK = 13;

	public const int DWMWA_CLOAKED = 14;

	public const uint MOD_ALT = 1u;

	public const uint MOD_CONTROL = 2u;

	public const uint MOD_SHIFT = 4u;

	public const uint MOD_WIN = 8u;

	public const uint MOD_NOREPEAT = 16384u;

	public const uint VK_SPACE = 32u;

	public const uint VK_ESCAPE = 27u;

	public const int SM_XVIRTUALSCREEN = 76;

	public const int SM_YVIRTUALSCREEN = 77;

	public const int SM_CXVIRTUALSCREEN = 78;

	public const int SM_CYVIRTUALSCREEN = 79;

	public const int SM_CXSMICON = 49;

	public const int GWL_STYLE = -16;

	public const int WS_CAPTION = 12582912;

	public const int WS_THICKFRAME = 262144;

	public const uint MONITOR_DEFAULTTOPRIMARY = 1u;

	public const uint MONITOR_DEFAULTTONEAREST = 2u;

	public const int SW_RESTORE = 9;

	public const int SW_SHOW = 5;

	public const uint ATTACH_PARENT_PROCESS = 3u;

	[DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
	public static extern int SetDwmWindowAttribute(nint hwnd, int attribute, ref int value, int size);

	[DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
	public static extern int GetDwmWindowAttribute(nint hwnd, int attribute, out int value, int size);

	[DllImport("dwmapi.dll")]
	public static extern int DwmFlush();

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool UnregisterHotKey(nint hWnd, int id);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool GetCursorPos(out POINT lpPoint);

	[DllImport("user32.dll")]
	public static extern int GetSystemMetrics(int nIndex);

	[DllImport("user32.dll")]
	public static extern nint MonitorFromPoint(POINT pt, uint dwFlags);

	[DllImport("user32.dll")]
	public static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	public static extern nint GetShellWindow();

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

	[DllImport("user32.dll")]
	public static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
	private static extern nint GetWindowLongPtr64(nint hWnd, int nIndex);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
	private static extern int GetWindowLong32(nint hWnd, int nIndex);

	public static nint GetWindowLongPtr(nint hWnd, int nIndex)
	{
		if (IntPtr.Size != 8)
		{
			return new IntPtr(GetWindowLong32(hWnd, nIndex));
		}
		return GetWindowLongPtr64(hWnd, nIndex);
	}

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

	[DllImport("gdi32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool DeleteObject(nint hObject);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool SetForegroundWindow(nint hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool ShowWindow(nint hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool IsIconic(nint hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool BringWindowToTop(nint hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

	[DllImport("kernel32.dll")]
	public static extern uint GetCurrentThreadId();

	[DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern int SetCurrentProcessExplicitAppUserModelID(string AppID);

	[DllImport("kernel32.dll", SetLastError = true)]
	public static extern bool AttachConsole(uint dwFlags);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool GlobalMemoryStatusEx([In][Out] MEMORYSTATUSEX lpBuffer);
}
