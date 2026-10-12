using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Launchpad.Interop;

internal static class ShellIcons
{
	[ComImport]
	[Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IShellItemImageFactory
	{
		[PreserveSig]
		int GetImage([In][MarshalAs(UnmanagedType.Struct)] SIZE size, [In] SIIGBF flags, out nint phbm);
	}

	internal struct SIZE(int cx, int cy)
	{
		public int cx = cx;

		public int cy = cy;
	}

	[Flags]
	internal enum SIIGBF
	{
		SIIGBF_RESIZETOFIT = 0,
		SIIGBF_BIGGERSIZEOK = 1,
		SIIGBF_MEMORYONLY = 2,
		SIIGBF_ICONONLY = 4,
		SIIGBF_THUMBNAILONLY = 8,
		SIIGBF_INCACHEONLY = 0x10,
		SIIGBF_CROPTOSQUARE = 0x20,
		SIIGBF_WIDETHUMBNAILS = 0x40,
		SIIGBF_ICONBACKGROUND = 0x80,
		SIIGBF_SCALEUP = 0x100
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	internal struct SHFILEINFO
	{
		public nint hIcon;

		public int iIcon;

		public uint dwAttributes;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
		public string szDisplayName;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
		public string szTypeName;
	}

	private static readonly Guid IShellItemImageFactoryGuid = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");

	internal const uint SHGFI_ICON = 256u;

	internal const uint SHGFI_LARGEICON = 0u;

	internal const uint SHGFI_USEFILEATTRIBUTES = 16u;

	internal const uint FILE_ATTRIBUTE_NORMAL = 128u;

	[DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	internal static extern int SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string pszPath, nint pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);

	[DllImport("gdi32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool DeleteObject(nint hObject);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	internal static extern uint PrivateExtractIcons(string szFileName, int nIconIndex, int cxIcon, int cyIcon, nint[] phicon, uint[] piconid, uint nIcons, uint flags);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool DestroyIcon(nint hIcon);

	public static BitmapSource? ExtractHighResolutionIcon(string path, int targetSize = 128, int iconIndex = 0)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return null;
		}
		if (File.Exists(path))
		{
			try
			{
				nint[] entries = new nint[1];
				uint[] piconid = new uint[1];
				if (PrivateExtractIcons(path, iconIndex, targetSize, targetSize, entries, piconid, 1u, 0u) != 0 && entries[0] != IntPtr.Zero)
				{
					try
					{
						BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHIcon(entries[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
						bitmapSource.Freeze();
						return bitmapSource;
					}
					finally
					{
						DestroyIcon(entries[0]);
					}
				}
			}
			catch
			{
			}
		}
		string pszPath = path;
		if (!path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) && !File.Exists(path) && !Directory.Exists(path))
		{
			pszPath = "shell:AppsFolder\\" + path;
		}
		try
		{
			Guid riid = IShellItemImageFactoryGuid;
			if (SHCreateItemFromParsingName(pszPath, IntPtr.Zero, ref riid, out object ppv) == 0 && ppv is IShellItemImageFactory shellItemImageFactory)
			{
				SIZE size = new SIZE(targetSize, targetSize);
				if (shellItemImageFactory.GetImage(size, SIIGBF.SIIGBF_BIGGERSIZEOK | SIIGBF.SIIGBF_ICONONLY, out var phbm) == 0 && phbm != IntPtr.Zero)
				{
					try
					{
						BitmapSource bitmapSource2 = Imaging.CreateBitmapSourceFromHBitmap(phbm, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
						bitmapSource2.Freeze();
						return bitmapSource2;
					}
					finally
					{
						DeleteObject(phbm);
					}
				}
			}
		}
		catch
		{
		}
		return null;
	}

	[DllImport("shell32.dll", CharSet = CharSet.Auto)]
	internal static extern nint SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

	public static BitmapSource? GetFileIcon(string path, int targetSize = 64)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return null;
		}
		BitmapSource bitmapSource = ExtractHighResolutionIcon(path, targetSize);
		if (bitmapSource != null)
		{
			return bitmapSource;
		}
		try
		{
			SHFILEINFO psfi = default;
			uint pageNumber = 256u;
			if (!File.Exists(path) && !Directory.Exists(path))
			{
				pageNumber |= 0x10;
			}
			if (SHGetFileInfo(path, 128u, ref psfi, (uint)Marshal.SizeOf(psfi), pageNumber) != IntPtr.Zero && psfi.hIcon != IntPtr.Zero)
			{
				try
				{
					BitmapSource bitmapSource2 = Imaging.CreateBitmapSourceFromHIcon(psfi.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
					bitmapSource2.Freeze();
					return bitmapSource2;
				}
				finally
				{
					DestroyIcon(psfi.hIcon);
				}
			}
		}
		catch
		{
		}
		return null;
	}
}
