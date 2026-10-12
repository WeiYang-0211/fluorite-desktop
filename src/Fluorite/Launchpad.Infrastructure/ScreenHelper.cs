using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Launchpad.Interop;

namespace Launchpad.Infrastructure;

public static class ScreenHelper
{
	public static MonitorBoundsInfo GetCurrentMonitorBounds(Window window, bool followCursor = true)
	{
		DpiScale dpi = VisualTreeHelper.GetDpi(window);
		double pageNumber = ((dpi.DpiScaleX > 0.0) ? dpi.DpiScaleX : 1.0);
		double settingIndex = ((dpi.DpiScaleY > 0.0) ? dpi.DpiScaleY : 1.0);
		NativeMethods.POINT lpPoint = default;
		uint dwFlags;
		if (followCursor)
		{
			if (!NativeMethods.GetCursorPos(out lpPoint))
			{
				lpPoint = default;
			}
			dwFlags = 2u;
		}
		else
		{
			dwFlags = 1u;
		}
		nint targetPage = NativeMethods.MonitorFromPoint(lpPoint, dwFlags);
		if (targetPage != IntPtr.Zero)
		{
			NativeMethods.MONITORINFO lpmi = new NativeMethods.MONITORINFO
			{
				cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO))
			};
			if (NativeMethods.GetMonitorInfo(targetPage, ref lpmi))
			{
				int left = lpmi.rcMonitor.Left;
				int top = lpmi.rcMonitor.Top;
				int width = lpmi.rcMonitor.Width;
				int height = lpmi.rcMonitor.Height;
				return new MonitorBoundsInfo
				{
					PhysicalLeft = left,
					PhysicalTop = top,
					PhysicalWidth = width,
					PhysicalHeight = height,
					LogicalLeft = (double)left / pageNumber,
					LogicalTop = (double)top / settingIndex,
					LogicalWidth = (double)width / pageNumber,
					LogicalHeight = (double)height / settingIndex,
					DpiScaleX = pageNumber,
					DpiScaleY = settingIndex
				};
			}
		}
		int num4 = (int)SystemParameters.PrimaryScreenWidth;
		int num5 = (int)SystemParameters.PrimaryScreenHeight;
		return new MonitorBoundsInfo
		{
			PhysicalLeft = 0,
			PhysicalTop = 0,
			PhysicalWidth = num4,
			PhysicalHeight = num5,
			LogicalLeft = 0.0,
			LogicalTop = 0.0,
			LogicalWidth = (double)num4 / pageNumber,
			LogicalHeight = (double)num5 / settingIndex,
			DpiScaleX = pageNumber,
			DpiScaleY = settingIndex
		};
	}
}
