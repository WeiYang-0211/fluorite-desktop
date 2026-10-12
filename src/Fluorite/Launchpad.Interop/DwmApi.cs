using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Launchpad.Interop;

public static class DwmApi
{
	public enum SystemBackdropType
	{
		Auto,
		None,
		MainWindow,
		TransientWindow,
		TabbedWindow
	}

	public struct MARGINS(int left, int right, int top, int bottom)
	{
		public int cxLeftWidth = left;

		public int cxRightWidth = right;

		public int cyTopHeight = top;

		public int cyBottomHeight = bottom;
	}

	public struct WindowCompositionAttributeData
	{
		public WindowCompositionAttribute Attribute;

		public nint Data;

		public int SizeOfData;
	}

	public enum WindowCompositionAttribute
	{
		WCA_ACCENT_POLICY = 19
	}

	public enum AccentState
	{
		ACCENT_DISABLED,
		ACCENT_ENABLE_GRADIENT,
		ACCENT_ENABLE_TRANSPARENTGRADIENT,
		ACCENT_ENABLE_BLURBEHIND,
		ACCENT_ENABLE_ACRYLICBLURBEHIND,
		ACCENT_INVALID_STATE
	}

	public struct AccentPolicy
	{
		public AccentState AccentState;

		public int AccentFlags;

		public int GradientColor;

		public int AnimationId;
	}

	public const int DWMWA_MICA_EFFECT = 1029;

	public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

	public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

	[DllImport("dwmapi.dll")]
	public static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);

	[DllImport("dwmapi.dll")]
	public static extern int DwmExtendFrameIntoClientArea(nint hWnd, ref MARGINS pMarInset);

	[DllImport("user32.dll")]
	public static extern int SetWindowCompositionAttribute(nint hwnd, ref WindowCompositionAttributeData data);

	public static bool ApplyModernBlur(Window window, bool isDarkMode = true)
	{
		nint handle = new WindowInteropHelper(window).Handle;
		if (handle == IntPtr.Zero)
		{
			return false;
		}
		try
		{
			int attrValue = (isDarkMode ? 1 : 0);
			DwmSetWindowAttribute(handle, 20, ref attrValue, 4);
			int attrValue2 = 3;
			if (DwmSetWindowAttribute(handle, 38, ref attrValue2, 4) == 0)
			{
				ExtendFrame(handle);
				return true;
			}
			int attrValue3 = 1;
			if (DwmSetWindowAttribute(handle, 1029, ref attrValue3, 4) == 0)
			{
				ExtendFrame(handle);
				return true;
			}
			int gradientColor = (isDarkMode ? 1711276032 : 1728053247);
			AccentPolicy structure = new AccentPolicy
			{
				AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
				AccentFlags = 2,
				GradientColor = gradientColor
			};
			int pageNumber = Marshal.SizeOf(structure);
			nint settingIndex = Marshal.AllocHGlobal(pageNumber);
			Marshal.StructureToPtr(structure, settingIndex, fDeleteOld: false);
			WindowCompositionAttributeData data = new WindowCompositionAttributeData
			{
				Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
				SizeOfData = pageNumber,
				Data = settingIndex
			};
			int targetPage = SetWindowCompositionAttribute(handle, ref data);
			Marshal.FreeHGlobal(settingIndex);
			if (targetPage != 0)
			{
				ExtendFrame(handle);
				return true;
			}
		}
		catch (Exception ex)
		{
			App.Log("DWM backdrop fallback error: " + ex.Message);
		}
		return false;
	}

	private static void ExtendFrame(nint hWnd)
	{
		MARGINS pMarInset = new MARGINS(-1, -1, -1, -1);
		DwmExtendFrameIntoClientArea(hWnd, ref pMarInset);
	}
}
