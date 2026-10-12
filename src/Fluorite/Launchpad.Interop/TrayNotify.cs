using System;
using System.Runtime.InteropServices;

namespace Launchpad.Interop;

public static class TrayNotify
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct NOTIFYICONDATA
	{
		public int cbSize;

		public nint hWnd;

		public int uID;

		public int uFlags;

		public int uCallbackMessage;

		public nint hIcon;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string szTip;

		public int dwState;

		public int dwStateMask;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
		public string szInfo;

		public int uTimeoutOrVersion;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
		public string szInfoTitle;

		public int dwInfoFlags;

		public Guid guidItem;

		public nint hBalloonIcon;
	}

	public const int WM_USER = 1024;

	public const int WM_TRAYICON = 1125;

	public const int NIM_ADD = 0;

	public const int NIM_MODIFY = 1;

	public const int NIM_DELETE = 2;

	public const int NIF_MESSAGE = 1;

	public const int NIF_ICON = 2;

	public const int NIF_TIP = 4;

	public const int NIF_INFO = 16;

	public const int NIIF_INFO = 1;

	public const int WM_LBUTTONUP = 514;

	public const int WM_RBUTTONUP = 517;

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	public static extern bool Shell_NotifyIcon(int dwMessage, [In] ref NOTIFYICONDATA lpData);
}
