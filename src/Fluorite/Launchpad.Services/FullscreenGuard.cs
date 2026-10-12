using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Launchpad.Interop;

namespace Launchpad.Services;

public static class FullscreenGuard
{
	private const int Tolerance = 2;

	private static readonly int OwnProcessId = Process.GetCurrentProcess().Id;

	public static bool IsScreenOwned(int cornerX, int cornerY)
	{
		if (ShellState.Query() == UserNotificationState.RunningD3DFullScreen)
		{
			return true;
		}
		nint foregroundWindow = NativeMethods.GetForegroundWindow();
		if (foregroundWindow == IntPtr.Zero)
		{
			return false;
		}
		NativeMethods.GetWindowThreadProcessId(foregroundWindow, out var lpdwProcessId);
		if (lpdwProcessId == (uint)OwnProcessId)
		{
			return false;
		}
		if (foregroundWindow == NativeMethods.GetShellWindow())
		{
			return false;
		}
		if (((int)((IntPtr)NativeMethods.GetWindowLongPtr(foregroundWindow, -16)).ToInt64() & 0xC40000) != 0)
		{
			return false;
		}
		if (!NativeMethods.GetWindowRect(foregroundWindow, out var lpRect))
		{
			return false;
		}
		nint pageNumber = NativeMethods.MonitorFromPoint(new NativeMethods.POINT
		{
			X = cornerX,
			Y = cornerY
		}, 2u);
		if (pageNumber == IntPtr.Zero)
		{
			return false;
		}
		NativeMethods.MONITORINFO lpmi = new NativeMethods.MONITORINFO
		{
			cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>()
		};
		if (!NativeMethods.GetMonitorInfo(pageNumber, ref lpmi))
		{
			return false;
		}
		NativeMethods.RECT rcMonitor = lpmi.rcMonitor;
		if (lpRect.Left <= rcMonitor.Left + 2 && lpRect.Top <= rcMonitor.Top + 2 && lpRect.Right >= rcMonitor.Right - 2)
		{
			return lpRect.Bottom >= rcMonitor.Bottom - 2;
		}
		return false;
	}
}
