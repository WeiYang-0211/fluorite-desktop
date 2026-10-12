using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Launchpad.Infrastructure;

internal static class IconNormalizer
{
	private const byte ContentAlpha = 120;

	private const byte FringeAlpha = 15;

	public static BitmapSource? TrimTransparentMargins(BitmapSource source)
	{
		try
		{
			BitmapSource bitmapSource = ToBgra32(source, out int width, out int height, out byte[] pixels, out int stride);
			if (bitmapSource == null)
			{
				return null;
			}
			if (!TryContentBox(pixels, stride, width, height, 120, out var minX, out var minY, out var maxX, out var maxY) && !TryContentBox(pixels, stride, width, height, 15, out minX, out minY, out maxX, out maxY))
			{
				return null;
			}
			int pageNumber = Math.Max(2, (int)((double)Math.Max(width, height) * 0.02));
			minX = Math.Max(0, minX - pageNumber);
			minY = Math.Max(0, minY - pageNumber);
			maxX = Math.Min(width - 1, maxX + pageNumber);
			maxY = Math.Min(height - 1, maxY + pageNumber);
			int settingIndex = maxX - minX + 1;
			int targetPage = maxY - minY + 1;
			if ((double)settingIndex >= (double)width * 0.97 && (double)targetPage >= (double)height * 0.97)
			{
				return null;
			}
			return BitmapSnapshot.Detach(new CroppedBitmap(bitmapSource, new Int32Rect(minX, minY, settingIndex, targetPage)));
		}
		catch
		{
			return null;
		}
	}

	public static BitmapSource? ScaleToFullBleed(BitmapSource source)
	{
		try
		{
			BitmapSource bitmapSource = ToBgra32(source, out int width, out int height, out byte[] pixels, out int stride);
			if (bitmapSource == null)
			{
				return null;
			}
			if (!TryContentBox(pixels, stride, width, height, 15, out var minX, out var minY, out var maxX, out var maxY))
			{
				return null;
			}
			int pageNumber = maxX - minX + 1;
			int settingIndex = maxY - minY + 1;
			if ((double)pageNumber >= (double)width * 0.97 && (double)settingIndex >= (double)height * 0.97)
			{
				return null;
			}
			double targetPage = (double)width / (double)pageNumber;
			double num4 = (double)height / (double)settingIndex;
			DrawingVisual drawingVisual = new DrawingVisual();
			using (DrawingContext drawingContext = drawingVisual.RenderOpen())
			{
				TransformGroup transformGroup = new TransformGroup();
				transformGroup.Children.Add(new ScaleTransform(targetPage, num4));
				transformGroup.Children.Add(new TranslateTransform((double)(-minX) * targetPage, (double)(-minY) * num4));
				drawingContext.PushTransform(transformGroup);
				drawingContext.DrawImage(bitmapSource, new Rect(0.0, 0.0, width, height));
				drawingContext.Pop();
			}
			RenderOptions.SetBitmapScalingMode(drawingVisual, BitmapScalingMode.HighQuality);
			RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(width, height, 96.0, 96.0, PixelFormats.Pbgra32);
			renderTargetBitmap.Render(drawingVisual);
			return BitmapSnapshot.Detach(renderTargetBitmap);
		}
		catch
		{
			return null;
		}
	}

	private static BitmapSource? ToBgra32(BitmapSource source, out int width, out int height, out byte[] pixels, out int stride)
	{
		width = source.PixelWidth;
		height = source.PixelHeight;
		pixels = Array.Empty<byte>();
		stride = 0;
		if (width < 8 || height < 8)
		{
			return null;
		}
		BitmapSource target = ((source.Format == PixelFormats.Bgra32) ? source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0.0));
		stride = width * 4;
		pixels = new byte[stride * height];
		target.CopyPixels(pixels, stride, 0);
		return target;
	}

	private static bool TryContentBox(byte[] pixels, int stride, int width, int height, byte alphaThreshold, out int minX, out int minY, out int maxX, out int maxY)
	{
		minX = width;
		minY = height;
		maxX = -1;
		maxY = -1;
		for (int i = 0; i < height; i++)
		{
			int pageNumber = i * stride;
			for (int j = 0; j < width; j++)
			{
				if (pixels[pageNumber + j * 4 + 3] >= alphaThreshold)
				{
					if (j < minX)
					{
						minX = j;
					}
					if (j > maxX)
					{
						maxX = j;
					}
					if (i < minY)
					{
						minY = i;
					}
					if (i > maxY)
					{
						maxY = i;
					}
				}
			}
		}
		if (maxX >= minX)
		{
			return maxY >= minY;
		}
		return false;
	}
}
