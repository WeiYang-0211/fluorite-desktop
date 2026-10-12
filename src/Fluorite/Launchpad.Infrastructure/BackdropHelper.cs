using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Launchpad.Models;
using Microsoft.Win32;

namespace Launchpad.Infrastructure;

public static class BackdropHelper
{
	private struct BITMAPINFOHEADER
	{
		public uint biSize;

		public int biWidth;

		public int biHeight;

		public ushort biPlanes;

		public ushort biBitCount;

		public uint biCompression;

		public uint biSizeImage;

		public int biXPelsPerMeter;

		public int biYPelsPerMeter;

		public uint biClrUsed;

		public uint biClrImportant;
	}

	private struct BITMAPINFO
	{
		public BITMAPINFOHEADER bmiHeader;
	}

	private const int SRCCOPY = 13369376;

	private const int HALFTONE = 4;

	private const int DIB_RGB_COLORS = 0;

	private static readonly object _cacheLock = new object();

	private static readonly BoundedBitmapCache _wallpaperCache = new BoundedBitmapCache(3, 100663296L);

	private static string? _cachedWallpaperPath;

	private static DateTime _cachedWallpaperTime;

	private static BitmapSource? _cachedCustom;

	private static string? _cachedCustomPath;

	private static int _cachedCustomRadius;

	private static int _cachedCustomW;

	private static int _cachedCustomH;

	private static DateTime _cachedCustomTime;

	[ThreadStatic]
	private static byte[]? _pxBuf;

	private const int FrostSaturationPercent = 55;

	private const int FrostLiftPercent = 10;

	private static readonly ParallelOptions BlurOptions = new ParallelOptions
	{
		MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
	};

	private const int BlurBandWidth = 128;

	[DllImport("user32.dll")]
	private static extern nint GetDesktopWindow();

	[DllImport("user32.dll")]
	private static extern nint GetDC(nint hWnd);

	[DllImport("user32.dll")]
	private static extern int ReleaseDC(nint hWnd, nint hDC);

	[DllImport("gdi32.dll")]
	private static extern nint CreateCompatibleDC(nint hdc);

	[DllImport("gdi32.dll")]
	private static extern nint CreateCompatibleBitmap(nint hdc, int nWidth, int nHeight);

	[DllImport("gdi32.dll")]
	private static extern nint SelectObject(nint hdc, nint hgdiobj);

	[DllImport("gdi32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool DeleteDC(nint hdc);

	[DllImport("gdi32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool DeleteObject(nint hObject);

	[DllImport("gdi32.dll")]
	private static extern bool BitBlt(nint hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, nint hdcSrc, int nXSrc, int nYSrc, int dwRop);

	[DllImport("gdi32.dll")]
	private static extern int SetStretchBltMode(nint hdc, int mode);

	[DllImport("gdi32.dll")]
	private static extern bool StretchBlt(nint hdcDest, int nXOriginDest, int nYOriginDest, int nWidthDest, int nHeightDest, nint hdcSrc, int nXOriginSrc, int nYOriginSrc, int nWidthSrc, int nHeightSrc, int dwRop);

	[DllImport("gdi32.dll")]
	private static extern int GetDIBits(nint hdc, nint hbm, uint start, uint cLines, nint lpvBits, ref BITMAPINFO lpbmi, uint usage);

	public static BitmapSource? GetWallpaper(MonitorBoundsInfo bounds, int radius)
	{
		int physicalWidth = bounds.PhysicalWidth;
		int physicalHeight = bounds.PhysicalHeight;
		if (physicalWidth <= 0 || physicalHeight <= 0)
		{
			return null;
		}
		radius = Math.Max(0, radius);
		string text = ResolveDesktopWallpaperPath();
		DateTime dateTime = DateTime.MinValue;
		if (!string.IsNullOrEmpty(text) && File.Exists(text))
		{
			try
			{
				dateTime = File.GetLastWriteTimeUtc(text);
			}
			catch
			{
			}
		}
		string key = $"{text}|{physicalWidth}x{physicalHeight}|{radius}";
		lock (_cacheLock)
		{
			BitmapSource image;
			if (_cachedWallpaperPath != text || _cachedWallpaperTime != dateTime)
			{
				_wallpaperCache.Clear();
				_cachedWallpaperPath = text;
				_cachedWallpaperTime = dateTime;
			}
			else if (_wallpaperCache.TryGetValue(key, out image))
			{
				return image;
			}
		}
		if (!string.IsNullOrEmpty(text) && File.Exists(text))
		{
			try
			{
				BitmapSource bitmapSource = RenderWallpaperFromFile(text, physicalWidth, physicalHeight, radius);
				if (bitmapSource != null)
				{
					lock (_cacheLock)
					{
						_wallpaperCache.Set(key, bitmapSource);
					}
					return bitmapSource;
				}
			}
			catch (Exception ex)
			{
				App.Log("[BackdropHelper] Failed to render wallpaper from file: " + ex.Message);
			}
		}
		BitmapSource bitmapSource2 = CaptureScreen(bounds, radius);
		if (bitmapSource2 != null)
		{
			lock (_cacheLock)
			{
				_wallpaperCache.Set(key, bitmapSource2);
			}
		}
		return bitmapSource2;
	}

	public static BitmapSource? GetBackdrop(MonitorBoundsInfo bounds, int radius, BackdropSource source, string? customImagePath)
	{
		switch (source)
		{
		case BackdropSource.Screen:
			return CaptureScreen(bounds, radius);
		case BackdropSource.CustomImage:
		{
			BitmapSource customImage = GetCustomImage(customImagePath, bounds, radius);
			if (customImage != null)
			{
				return customImage;
			}
			App.Log("[BackdropHelper] Custom backdrop unavailable, falling back to the wallpaper");
			break;
		}
		}
		return GetWallpaper(bounds, radius);
	}

	public static void InvalidateCustomCache()
	{
		lock (_cacheLock)
		{
			_cachedCustom = null;
			_cachedCustomPath = null;
			_cachedCustomRadius = -1;
			_cachedCustomTime = DateTime.MinValue;
		}
	}

	private static BitmapSource? GetCustomImage(string? path, MonitorBoundsInfo bounds, int radius)
	{
		if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
		{
			return null;
		}
		int physicalWidth = bounds.PhysicalWidth;
		int physicalHeight = bounds.PhysicalHeight;
		if (physicalWidth <= 0 || physicalHeight <= 0)
		{
			return null;
		}
		radius = Math.Max(0, radius);
		DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
		lock (_cacheLock)
		{
			if (_cachedCustom != null && _cachedCustomPath == path && _cachedCustomRadius == radius && _cachedCustomW == physicalWidth && _cachedCustomH == physicalHeight && _cachedCustomTime == lastWriteTimeUtc)
			{
				return _cachedCustom;
			}
		}
		try
		{
			BitmapSource bitmapSource = RenderWallpaperFromFile(path, physicalWidth, physicalHeight, radius);
			if (bitmapSource == null)
			{
				return null;
			}
			lock (_cacheLock)
			{
				_cachedCustom = bitmapSource;
				_cachedCustomPath = path;
				_cachedCustomRadius = radius;
				_cachedCustomW = physicalWidth;
				_cachedCustomH = physicalHeight;
				_cachedCustomTime = lastWriteTimeUtc;
			}
			return bitmapSource;
		}
		catch (Exception ex)
		{
			App.Log("[BackdropHelper] Custom backdrop failed to render: " + ex.Message);
			return null;
		}
	}

	private static string? ResolveDesktopWallpaperPath()
	{
		try
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft\\Windows\\Themes\\TranscodedWallpaper");
			if (File.Exists(text) && new FileInfo(text).Length > 0)
			{
				return text;
			}
			string commandLinePath = Registry.GetValue("HKEY_CURRENT_USER\\Control Panel\\Desktop", "WallPaper", null) as string;
			if (!string.IsNullOrEmpty(commandLinePath) && File.Exists(commandLinePath))
			{
				return commandLinePath;
			}
		}
		catch (Exception ex)
		{
			App.Log("[BackdropHelper] Error resolving wallpaper path: " + ex.Message);
		}
		return null;
	}

	private static BitmapSource? RenderWallpaperFromFile(string filePath, int physW, int physH, int radius)
	{
		using FileStream bitmapStream = File.OpenRead(filePath);
		BitmapDecoder bitmapDecoder = BitmapDecoder.Create(bitmapStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
		if (bitmapDecoder.Frames.Count == 0)
		{
			return null;
		}
		BitmapFrame bitmapFrame = bitmapDecoder.Frames[0];
		int pageNumber = ((radius <= 0) ? 1 : 2);
		int settingIndex = Math.Max(320, physW / pageNumber);
		int targetPage = Math.Max(180, physH / pageNumber);
		double num4 = bitmapFrame.PixelWidth;
		double num5 = bitmapFrame.PixelHeight;
		if (num4 <= 0.0 || num5 <= 0.0)
		{
			return null;
		}
		double num6 = Math.Max((double)settingIndex / num4, (double)targetPage / num5);
		double num7 = num4 * num6;
		double num8 = num5 * num6;
		double x = ((double)settingIndex - num7) / 2.0;
		double y = ((double)targetPage - num8) / 2.0;
		DrawingVisual drawingVisual = new DrawingVisual();
		RenderOptions.SetBitmapScalingMode(drawingVisual, BitmapScalingMode.HighQuality);
		using (DrawingContext drawingContext = drawingVisual.RenderOpen())
		{
			drawingContext.DrawImage(bitmapFrame, new Rect(x, y, num7, num8));
		}
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(settingIndex, targetPage, 96.0, 96.0, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(drawingVisual);
		int num9 = settingIndex * 4;
		byte[] entries = new byte[num9 * targetPage];
		renderTargetBitmap.CopyPixels(entries, num9, 0);
		if (radius > 0)
		{
			FastGaussianBlur(entries, settingIndex, targetPage, radius);
		}
		BitmapSource bitmapSource = BitmapSource.Create(settingIndex, targetPage, 96.0, 96.0, PixelFormats.Pbgra32, null, entries, num9);
		bitmapSource.Freeze();
		return bitmapSource;
	}

	private static byte[] TakeBuffer(ref byte[]? slot, int length)
	{
		if (slot == null || slot.Length != length)
		{
			slot = new byte[length];
		}
		return slot;
	}

	private static void ApplyFrost(int[] pixels, int width, int height)
	{
		int pageNumber = width * height * 4;
		for (int i = 0; i < pageNumber; i += 4)
		{
			double settingIndex = ((double)pixels[i + 2] * 299.0 + (double)pixels[i + 1] * 587.0 + (double)pixels[i] * 114.0) / 1000.0;
			for (int j = 0; j < 3; j++)
			{
				double targetPage = settingIndex + ((double)pixels[i + j] - settingIndex) * 55.0 / 100.0;
				pixels[i + j] = (int)Math.Round(targetPage + (65280.0 - targetPage) * 10.0 / 100.0);
			}
		}
	}

	public static BitmapSource? CaptureScreen(MonitorBoundsInfo bounds, int radius)
	{
		int physicalWidth = bounds.PhysicalWidth;
		int physicalHeight = bounds.PhysicalHeight;
		int physicalLeft = bounds.PhysicalLeft;
		int physicalTop = bounds.PhysicalTop;
		if (physicalWidth <= 0 || physicalHeight <= 0)
		{
			return null;
		}
		int pageNumber = ((radius <= 0) ? 1 : 2);
		int settingIndex = Math.Max(320, physicalWidth / pageNumber);
		int targetPage = Math.Max(180, physicalHeight / pageNumber);
		nint desktopWindow = GetDesktopWindow();
		nint dC = GetDC(desktopWindow);
		nint num4 = CreateCompatibleDC(dC);
		nint num5 = CreateCompatibleBitmap(dC, settingIndex, targetPage);
		nint hgdiobj = SelectObject(num4, num5);
		try
		{
			if (pageNumber == 1)
			{
				BitBlt(num4, 0, 0, settingIndex, targetPage, dC, physicalLeft, physicalTop, 13369376);
			}
			else
			{
				SetStretchBltMode(num4, 4);
				StretchBlt(num4, 0, 0, settingIndex, targetPage, dC, physicalLeft, physicalTop, physicalWidth, physicalHeight, 13369376);
			}
			BITMAPINFO lpbmi = new BITMAPINFO
			{
				bmiHeader = 
				{
					biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
					biWidth = settingIndex,
					biHeight = -targetPage,
					biPlanes = 1,
					biBitCount = 32,
					biCompression = 0u
				}
			};
			int num6 = settingIndex * 4;
			byte[] entries = TakeBuffer(ref _pxBuf, num6 * targetPage);
			GCHandle gCHandle = GCHandle.Alloc(entries, GCHandleType.Pinned);
			try
			{
				GetDIBits(num4, num5, 0u, (uint)targetPage, gCHandle.AddrOfPinnedObject(), ref lpbmi, 0u);
			}
			finally
			{
				gCHandle.Free();
			}
			if (radius > 0)
			{
				ProcessBackdropPixels(entries, settingIndex, targetPage, radius, frost: true);
			}
			BitmapSource bitmapSource = BitmapSource.Create(settingIndex, targetPage, 96.0, 96.0, PixelFormats.Bgr32, null, entries, num6);
			bitmapSource.Freeze();
			return bitmapSource;
		}
		catch (Exception ex)
		{
			App.Log("Error in CaptureScreen: " + ex.Message);
			return null;
		}
		finally
		{
			SelectObject(num4, hgdiobj);
			DeleteObject(num5);
			DeleteDC(num4);
			ReleaseDC(desktopWindow, dC);
		}
	}

	public static BitmapSource? BuildFrost(BitmapSource capture, int downscale, int radius)
	{
		try
		{
			TransformedBitmap transformedBitmap = new TransformedBitmap(capture, new ScaleTransform(1.0 / (double)Math.Max(1, downscale), 1.0 / (double)Math.Max(1, downscale)));
			int pixelWidth = transformedBitmap.PixelWidth;
			int pixelHeight = transformedBitmap.PixelHeight;
			if (pixelWidth < 8 || pixelHeight < 8)
			{
				return null;
			}
			int pageNumber = pixelWidth * 4;
			byte[] entries = new byte[pageNumber * pixelHeight];
			transformedBitmap.CopyPixels(entries, pageNumber, 0);
			ProcessBackdropPixels(entries, pixelWidth, pixelHeight, radius, frost: true);
			BitmapSource bitmapSource = BitmapSource.Create(pixelWidth, pixelHeight, 96.0, 96.0, PixelFormats.Bgr32, null, entries, pageNumber);
			bitmapSource.Freeze();
			return bitmapSource;
		}
		catch (Exception ex)
		{
			App.Log("Error in BuildFrost: " + ex.Message);
			return null;
		}
	}

	private static byte[] FastGaussianBlur(byte[] source, int width, int height, int radius)
	{
		return ProcessBackdropPixels(source, width, height, radius, frost: false);
	}

	private unsafe static byte[] ProcessBackdropPixels(byte[] source, int width, int height, int radius, bool frost)
	{
		if (radius <= 0 && !frost)
		{
			return source;
		}
		int pageNumber = width * height * 4;
		int[] entries = new int[pageNumber];
		int[] buffer = new int[pageNumber];
		for (int i = 0; i < pageNumber; i++)
		{
			entries[i] = source[i] << 8;
		}
		if (radius > 0)
		{
			fixed (int* ptr = entries)
			{
				fixed (int* ptr2 = buffer)
				{
					BoxBlurHorizontal(ptr, ptr2, width, height, radius);
					BoxBlurVertical(ptr2, ptr, width, height, radius);
					BoxBlurHorizontal(ptr, ptr2, width, height, radius);
					BoxBlurVertical(ptr2, ptr, width, height, radius);
					BoxBlurHorizontal(ptr, ptr2, width, height, radius);
					BoxBlurVertical(ptr2, ptr, width, height, radius);
				}
			}
		}
		if (frost)
		{
			ApplyFrost(entries, width, height);
		}
		for (int j = 0; j < pageNumber; j += 4)
		{
			uint settingIndex = (uint)(j / 4 * 747796405 + -1403630843);
			settingIndex = ((settingIndex >> (int)((settingIndex >> 28) + 4)) ^ settingIndex) * 277803737;
			settingIndex = (settingIndex >> 22) ^ settingIndex;
			int targetPage = (int)(settingIndex & 0xFF);
			for (int k = 0; k < 4; k++)
			{
				source[j + k] = (byte)Math.Clamp(entries[j + k] + targetPage >> 8, 0, 255);
			}
		}
		return source;
	}

	private unsafe static void BoxBlurHorizontal(int* src, int* dst, int w, int h, int r)
	{
		double inv = 1.0 / (double)(r + r + 1);
		Parallel.For(0, h, BlurOptions, (int y) =>
		{
			int pageNumber = y * w * 4;
			int settingIndex = 0;
			int targetPage = 0;
			int num4 = 0;
			int num5 = 0;
			for (int i = -r; i <= r; i++)
			{
				int num6 = Math.Clamp(i, 0, w - 1);
				int num7 = pageNumber + num6 * 4;
				settingIndex += src[num7];
				targetPage += src[num7 + 1];
				num4 += src[num7 + 2];
				num5 += src[num7 + 3];
			}
			for (int j = 0; j < w; j++)
			{
				int num8 = pageNumber + j * 4;
				dst[num8] = (int)((double)settingIndex * inv + 0.5);
				dst[num8 + 1] = (int)((double)targetPage * inv + 0.5);
				dst[num8 + 2] = (int)((double)num4 * inv + 0.5);
				dst[num8 + 3] = (int)((double)num5 * inv + 0.5);
				int num9 = Math.Clamp(j - r, 0, w - 1);
				int num10 = Math.Clamp(j + r + 1, 0, w - 1);
				int num11 = pageNumber + num9 * 4;
				int num12 = pageNumber + num10 * 4;
				settingIndex += src[num12] - src[num11];
				targetPage += src[num12 + 1] - src[num11 + 1];
				num4 += src[num12 + 2] - src[num11 + 2];
				num5 += src[num12 + 3] - src[num11 + 3];
			}
		});
	}

	private unsafe static void BoxBlurVertical(int* src, int* dst, int w, int h, int r)
	{
		double inv = 1.0 / (double)(r + r + 1);
		int rowBytes = w * 4;
		Parallel.For(0, (w + 128 - 1) / 128, BlurOptions, (int band) =>
		{
			int pageNumber = band * 128;
			int settingIndex = Math.Min(128, w - pageNumber) * 4;
			fixed (int* ptr = new int[settingIndex])
			{
				for (int i = -r; i <= r; i++)
				{
					int* ptr2 = src + Math.Clamp(i, 0, h - 1) * rowBytes + pageNumber * 4;
					for (int j = 0; j < settingIndex; j++)
					{
						ptr[j] += ptr2[j];
					}
				}
				for (int k = 0; k < h; k++)
				{
					int* ptr3 = dst + k * rowBytes + pageNumber * 4;
					for (int l = 0; l < settingIndex; l++)
					{
						ptr3[l] = (int)((double)ptr[l] * inv + 0.5);
					}
					int* ptr4 = src + Math.Clamp(k + r + 1, 0, h - 1) * rowBytes + pageNumber * 4;
					int* ptr5 = src + Math.Clamp(k - r, 0, h - 1) * rowBytes + pageNumber * 4;
					for (int m = 0; m < settingIndex; m++)
					{
						ptr[m] += ptr4[m] - ptr5[m];
					}
				}
			}
		});
	}
}
