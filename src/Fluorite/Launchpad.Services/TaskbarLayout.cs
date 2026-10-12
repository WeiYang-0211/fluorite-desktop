using System;
using System.Runtime.InteropServices;
using Launchpad.Infrastructure;
using Launchpad.Interop;

namespace Launchpad.Services;

internal static class TaskbarLayout
{
	private struct AppBarData
	{
		public uint Size;

		public nint Window;

		public uint Callback;

		public uint Edge;

		public NativeMethods.RECT Rect;

		public nint Parameter;
	}

	internal static bool IsTaskbarSurface(string className)
	{
		switch (className)
		{
		case "Shell_TrayWnd":
		case "Shell_SecondaryTrayWnd":
		case "NotifyIconOverflowWindow":
		case "TopLevelWindowForOverflowIsland":
		case "XamlExplorerHostIslandWindow":
			return true;
		default:
			return false;
		}
	}

	[DllImport("shell32.dll")]
	private static extern nint SHAppBarMessage(uint message, ref AppBarData data);

	internal static double GetDockBottom(MonitorBoundsInfo monitor, double workBottom, double offset)
	{
		AppBarData data = new AppBarData
		{
			Size = (uint)Marshal.SizeOf<AppBarData>(),
			Edge = 3u,
			Rect = new NativeMethods.RECT
			{
				Left = monitor.PhysicalLeft,
				Top = monitor.PhysicalTop,
				Right = monitor.PhysicalLeft + monitor.PhysicalWidth,
				Bottom = monitor.PhysicalTop + monitor.PhysicalHeight
			}
		};
		nint pageNumber = SHAppBarMessage(11u, ref data);
		if (pageNumber == IntPtr.Zero || !NativeMethods.GetWindowRect(pageNumber, out var lpRect))
		{
			return workBottom - offset;
		}
		double screenBottom = (double)(monitor.PhysicalTop + monitor.PhysicalHeight) / monitor.DpiScaleY;
		double availableBottom = CalculateBottom(monitor.PhysicalTop + monitor.PhysicalHeight, lpRect.Top, lpRect.Bottom, monitor.DpiScaleY);
		return AvoidOverlap(screenBottom, availableBottom, offset);
	}

	internal static double AvoidOverlap(double screenBottom, double availableBottom, double offset)
	{
		double pageNumber = screenBottom - offset;
		if (!(pageNumber <= availableBottom))
		{
			return availableBottom - Math.Min(offset, 8.0);
		}
		return pageNumber;
	}

	internal static double CalculateBottom(int screenBottom, int barTop, int barBottom, double dpi)
	{
		dpi = ((dpi > 0.0) ? dpi : 1.0);
		int pageNumber = Math.Max(0, barBottom - barTop);
		int settingIndex = Math.Max(0, Math.Min(screenBottom, barBottom) - barTop);
		return (double)(screenBottom - (((double)settingIndex > 4.0 * dpi) ? pageNumber : 0)) / dpi;
	}
}
