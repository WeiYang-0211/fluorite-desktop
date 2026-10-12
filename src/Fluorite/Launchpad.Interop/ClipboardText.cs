using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Launchpad.Interop;

internal static class ClipboardText
{
	internal static bool TrySet(string text, nint owner)
	{
		byte[] bytes = Encoding.Unicode.GetBytes(text + "\0");
		nint pageNumber = GlobalAlloc(2u, (nuint)bytes.Length);
		if (pageNumber == IntPtr.Zero)
		{
			return false;
		}
		try
		{
			nint settingIndex = GlobalLock(pageNumber);
			if (settingIndex == IntPtr.Zero)
			{
				return false;
			}
			try
			{
				Marshal.Copy(bytes, 0, settingIndex, bytes.Length);
			}
			finally
			{
				GlobalUnlock(pageNumber);
			}
			if (!OpenClipboard(owner))
			{
				return false;
			}
			try
			{
				if (!EmptyClipboard() || SetClipboardData(13u, pageNumber) == IntPtr.Zero)
				{
					return false;
				}
				pageNumber = IntPtr.Zero;
				return true;
			}
			finally
			{
				CloseClipboard();
			}
		}
		finally
		{
			if (pageNumber != IntPtr.Zero)
			{
				GlobalFree(pageNumber);
			}
		}
	}

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool OpenClipboard(nint owner);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool EmptyClipboard();

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool CloseClipboard();

	[DllImport("user32.dll")]
	private static extern nint SetClipboardData(uint format, nint memory);

	[DllImport("kernel32.dll")]
	private static extern nint GlobalAlloc(uint flags, nuint bytes);

	[DllImport("kernel32.dll")]
	private static extern nint GlobalLock(nint memory);

	[DllImport("kernel32.dll")]
	private static extern bool GlobalUnlock(nint memory);

	[DllImport("kernel32.dll")]
	private static extern nint GlobalFree(nint memory);
}
