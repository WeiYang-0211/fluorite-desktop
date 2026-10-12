using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Launchpad.Infrastructure;

internal static class IconTileRenderer
{
	private enum Material
	{
		White,
		Dark,
		Tile,
		Frosted
	}

	private record Metrics(double Opaque, double Corner, double Luma, double Saturation, int BoxX, int BoxY, int BoxW, int BoxH, int CanvasW, int CanvasH)
	{
		public double Cover => Math.Min((double)BoxW / (double)CanvasW, (double)BoxH / (double)CanvasH);

		public double Aspect => (double)BoxW / (double)Math.Max(1, BoxH);
	}

	private const int Canvas = 256;

	private const int Supersample = 2;

	private const double TileFraction = 0.8;

	private const double SuperExponent = 5.0;

	private const int PathSteps = 240;

	public const string GeometryVersion = "t6-white-artwork";

	public static BitmapSource? Render(BitmapSource? source, bool frosted = false)
	{
		if (source == null)
		{
			return null;
		}
		BitmapSource bitmapSource = ToBgra32(source, out int width, out int height, out byte[] pixels, out int stride);
		if (bitmapSource == null || width < 16 || height < 16)
		{
			return null;
		}
		Metrics metrics = Measure(pixels, stride, width, height);
		if (metrics == null)
		{
			return null;
		}
		if (IsBlank(pixels, stride, width, height))
		{
			return null;
		}
		(Material, double) tuple = PickMaterial(metrics);
		Material material = tuple.Item1;
		double glyphScale = tuple.Item2;
		bool validated = material == Material.Tile;
		if (frosted)
		{
			material = Material.Frosted;
			if (validated)
			{
				glyphScale = 0.84;
			}
			else if (metrics.Aspect > 1.35)
			{
				glyphScale = 0.84;
			}
			else
			{
				glyphScale = ((metrics.Aspect < 0.74) ? 0.68 : 0.78);
			}
		}
		return Draw(bitmapSource, metrics, material, glyphScale, frosted & validated);
	}

	private static Metrics? Measure(byte[] px, int stride, int w, int h)
	{
		int pageNumber = w;
		int settingIndex = h;
		int targetPage = -1;
		int num4 = -1;
		int num5 = w;
		int num6 = h;
		int num7 = -1;
		int num8 = -1;
		int num9 = 0;
		int num10 = 0;
		double num11 = 0.0;
		double num12 = 0.0;
		double[] entries = new double[4];
		for (int i = 0; i < h; i++)
		{
			int num13 = i * stride;
			for (int j = 0; j < w; j++)
			{
				int num14 = num13 + j * 4;
				byte b = px[num14 + 3];
				if (b > 24)
				{
					num9++;
					if (j < pageNumber)
					{
						pageNumber = j;
					}
					if (j > targetPage)
					{
						targetPage = j;
					}
					if (i < settingIndex)
					{
						settingIndex = i;
					}
					if (i > num4)
					{
						num4 = i;
					}
					if (b > 120)
					{
						if (j < num5)
						{
							num5 = j;
						}
						if (j > num7)
						{
							num7 = j;
						}
						if (i < num6)
						{
							num6 = i;
						}
						if (i > num8)
						{
							num8 = i;
						}
					}
					if (b > 200)
					{
						int num15 = px[num14 + 2];
						int num16 = px[num14 + 1];
						int num17 = px[num14];
						num11 += 0.299 * (double)num15 + 0.587 * (double)num16 + 0.114 * (double)num17;
						num12 += (double)(Math.Max(num15, Math.Max(num16, num17)) - Math.Min(num15, Math.Min(num16, num17)));
						num10++;
					}
				}
				if (j < 6 && i < 6)
				{
					entries[0] += (int)b;
				}
				else if (j >= w - 6 && i < 6)
				{
					entries[1] += (int)b;
				}
				else if (j < 6 && i >= h - 6)
				{
					entries[2] += (int)b;
				}
				else if (j >= w - 6 && i >= h - 6)
				{
					entries[3] += (int)b;
				}
			}
		}
		if (targetPage < pageNumber || num4 < settingIndex)
		{
			return null;
		}
		int num18;
		int num19;
		if (num7 >= num5)
		{
			num18 = ((num8 >= num6) ? 1 : 0);
			if (num18 != 0)
			{
				num19 = num5;
				goto IL_01e8;
			}
		}
		else
		{
			num18 = 0;
		}
		num19 = pageNumber;
		goto IL_01e8;
		IL_01e8:
		int num20 = num19;
		int num21 = ((num18 != 0) ? num6 : settingIndex);
		int boxW = ((num18 != 0) ? num7 : targetPage) - num20 + 1;
		int boxH = ((num18 != 0) ? num8 : num4) - num21 + 1;
		double num22 = (double)w * (double)h;
		return new Metrics((double)num9 / num22, (entries[0] + entries[1] + entries[2] + entries[3]) / 144.0 / 255.0, (num10 > 0) ? (num11 / (double)num10 / 255.0) : 0.0, (num10 > 0) ? (num12 / (double)num10 / 255.0) : 0.0, num20, num21, boxW, boxH, w, h);
	}

	private static (Material, double) PickMaterial(Metrics m)
	{
		if (m.Opaque > 0.86 && (m.Corner > 0.5 || (m.Cover > 0.92 && m.Aspect > 0.85 && m.Aspect < 1.18)))
		{
			return (Material.Tile, 0.0);
		}
		if ((m.Luma > 0.82 || (m.Luma > 0.7 && m.Saturation < 0.16)) && m.Opaque < 0.75)
		{
			return (Material.White, 0.7);
		}
		double item;
		if (m.Aspect > 1.35)
		{
			item = 0.8;
		}
		else
		{
			item = ((m.Aspect < 0.74) ? 0.62 : 0.72);
		}
		return (Material.White, item);
	}

	private static bool IsBlank(byte[] px, int stride, int w, int h)
	{
		int pageNumber = 0;
		int settingIndex = 0;
		int targetPage = 0;
		int num4 = 0;
		for (int i = 0; i < h; i += 4)
		{
			int num5 = i * stride;
			for (int j = 0; j < w; j += 4)
			{
				int num6 = num5 + j * 4;
				num4++;
				if (px[num6 + 3] >= 20)
				{
					settingIndex++;
					if (px[num6 + 3] >= 200)
					{
						targetPage++;
					}
					int num7 = px[num6 + 2];
					int val = px[num6 + 1];
					int val2 = px[num6];
					int num8 = Math.Max(num7, Math.Max(val, val2));
					int num9 = Math.Min(num7, Math.Min(val, val2));
					if (num8 - num9 > 24 || num7 < 231)
					{
						pageNumber++;
					}
				}
			}
		}
		if (settingIndex != 0)
		{
			if ((double)pageNumber / (double)settingIndex < 0.02)
			{
				return (double)targetPage > (double)num4 * 0.95;
			}
			return false;
		}
		return true;
	}

	private static BitmapSource? Draw(BitmapSource flat, Metrics m, Material material, double glyphScale, bool insetPlate)
	{
		try
		{
			int pageNumber = 512;
			int settingIndex = 204;
			double targetPage = (256 - settingIndex) / 2 * 2;
			double num4 = targetPage;
			double num5 = settingIndex * 2;
			Geometry geometry = Squircle(targetPage, num4, num5, 5.0);
			DrawingVisual drawingVisual = new DrawingVisual();
			using (DrawingContext drawingContext = drawingVisual.RenderOpen())
			{
				drawingContext.PushClip(geometry);
				if (TryPlateBrush(material, out Brush brush))
				{
					drawingContext.DrawRectangle(brush, null, new Rect(0.0, 0.0, pageNumber, pageNumber));
					brush.Freeze();
				}
				if (material == Material.Frosted)
				{
					Random random = new Random(17);
					SolidColorBrush brush2 = new SolidColorBrush(Color.FromArgb(8, byte.MaxValue, byte.MaxValue, byte.MaxValue));
					for (int i = 0; i < 600; i++)
					{
						drawingContext.DrawEllipse(brush2, null, new Point(targetPage + random.NextDouble() * num5, num4 + random.NextDouble() * num5), 0.7, 0.7);
					}
					LinearGradientBrush brush3 = new LinearGradientBrush(Color.FromArgb(105, byte.MaxValue, byte.MaxValue, byte.MaxValue), Color.FromArgb(20, byte.MaxValue, byte.MaxValue, byte.MaxValue), 90.0);
					drawingContext.DrawGeometry(null, new Pen(brush3, 1.5), geometry);
				}
				if (material == Material.Tile)
				{
					double num6 = num5 * 1.1;
					drawingContext.DrawImage(CoverSource(flat, num6), new Rect(targetPage - (num6 - num5) / 2.0, num4 - (num6 - num5) / 2.0, num6, num6));
				}
				else
				{
					double num7 = num5 * glyphScale;
					double num8 = (double)m.BoxH / (double)Math.Max(1, m.BoxW);
					if (insetPlate)
					{
						num7 /= Math.Max(1.0, num8);
					}
					double num9 = num7 * num8;
					Int32Rect sourceRect = new Int32Rect(m.BoxX, m.BoxY, m.BoxW, m.BoxH);
					Rect rectangle = new Rect(targetPage + (num5 - num7) / 2.0, num4 + (num5 - num9) / 2.0, num7, num9);
					CroppedBitmap imageSource = new CroppedBitmap(flat, sourceRect);
					if (insetPlate)
					{
						Geometry geometry2 = Squircle(rectangle.X, rectangle.Y, rectangle.Width, 5.0).Clone();
						geometry2.Transform = new ScaleTransform(1.0, rectangle.Height / rectangle.Width, rectangle.X, rectangle.Y);
						drawingContext.PushClip(geometry2);
					}
					drawingContext.DrawImage(imageSource, rectangle);
					if (insetPlate)
					{
						LinearGradientBrush brush4 = new LinearGradientBrush(Color.FromArgb(16, byte.MaxValue, byte.MaxValue, byte.MaxValue), Color.FromArgb(0, byte.MaxValue, byte.MaxValue, byte.MaxValue), 90.0);
						drawingContext.DrawRectangle(brush4, null, rectangle);
						drawingContext.Pop();
					}
				}
				drawingContext.Pop();
			}
			RenderOptions.SetBitmapScalingMode(drawingVisual, BitmapScalingMode.HighQuality);
			RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(pageNumber, pageNumber, 96.0, 96.0, PixelFormats.Pbgra32);
			renderTargetBitmap.Render(drawingVisual);
			DrawingVisual drawingVisual2 = new DrawingVisual();
			using (DrawingContext drawingContext2 = drawingVisual2.RenderOpen())
			{
				drawingContext2.DrawImage(renderTargetBitmap, new Rect(0.0, 0.0, 256.0, 256.0));
			}
			RenderOptions.SetBitmapScalingMode(drawingVisual2, BitmapScalingMode.HighQuality);
			RenderTargetBitmap renderTargetBitmap2 = new RenderTargetBitmap(256, 256, 96.0, 96.0, PixelFormats.Pbgra32);
			renderTargetBitmap2.Render(drawingVisual2);
			return BitmapSnapshot.Detach(renderTargetBitmap2);
		}
		catch (Exception ex)
		{
			App.Log("[TILE] render failed: " + ex.Message);
			return null;
		}
	}

	private static bool TryPlateBrush(Material material, out Brush brush)
	{
		brush = Brushes.Transparent;
		Color startColor;
		Color endColor;
		switch (material)
		{
		case Material.Tile:
			return false;
		case Material.Frosted:
			startColor = Color.FromArgb(78, byte.MaxValue, byte.MaxValue, byte.MaxValue);
			endColor = Color.FromArgb(28, 235, 242, 250);
			break;
		case Material.White:
			startColor = Color.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue);
			endColor = Color.FromRgb(233, 236, 240);
			break;
		default:
		{
			Color c = Color.FromRgb(46, 50, 60);
			startColor = Shade(c, 0.12);
			endColor = Shade(c, -0.18);
			break;
		}
		}
		brush = new LinearGradientBrush(startColor, endColor, 90.0);
		return true;
	}

	private static Color Shade(Color c, double amount)
	{
		double f = 1.0 - Math.Abs(amount);
		double target = ((amount > 0.0) ? 255 : 0);
		return Color.FromRgb(Mix(c.R), Mix(c.G), Mix(c.B));
		byte Mix(byte v)
		{
			return (byte)Math.Min(255.0, Math.Round((double)(int)v * f + target * (1.0 - f)));
		}
	}

	private static BitmapSource CoverSource(BitmapSource src, double targetSide)
	{
		double val = targetSide / (double)src.PixelWidth;
		double val2 = targetSide / (double)src.PixelHeight;
		double pageNumber = Math.Max(val, val2);
		DrawingVisual drawingVisual = new DrawingVisual();
		using (DrawingContext drawingContext = drawingVisual.RenderOpen())
		{
			double settingIndex = (double)src.PixelWidth * pageNumber;
			double targetPage = (double)src.PixelHeight * pageNumber;
			drawingContext.DrawImage(src, new Rect((targetSide - settingIndex) / 2.0, (targetSide - targetPage) / 2.0, settingIndex, targetPage));
		}
		RenderOptions.SetBitmapScalingMode(drawingVisual, BitmapScalingMode.HighQuality);
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap((int)Math.Ceiling(targetSide), (int)Math.Ceiling(targetSide), 96.0, 96.0, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(drawingVisual);
		renderTargetBitmap.Freeze();
		return renderTargetBitmap;
	}

	private static Geometry Squircle(double x, double y, double size, double n)
	{
		double pageNumber = x + size / 2.0;
		double settingIndex = y + size / 2.0;
		double targetPage = size / 2.0;
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
		{
			for (int i = 0; i < 240; i++)
			{
				double num4 = Math.PI * 2.0 * (double)i / 240.0;
				double value = Math.Cos(num4);
				double failure = Math.Sin(num4);
				Point point = new Point(pageNumber + targetPage * (double)Math.Sign(value) * Math.Pow(Math.Abs(value), 2.0 / n), settingIndex + targetPage * (double)Math.Sign(failure) * Math.Pow(Math.Abs(failure), 2.0 / n));
				if (i == 0)
				{
					streamGeometryContext.BeginFigure(point, isFilled: true, isClosed: true);
				}
				else
				{
					streamGeometryContext.LineTo(point, isStroked: true, isSmoothJoin: false);
				}
			}
		}
		streamGeometry.Freeze();
		return streamGeometry;
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
		BitmapSource obj = ((source.Format == PixelFormats.Bgra32) ? source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0.0));
		stride = width * 4;
		pixels = new byte[stride * height];
		obj.CopyPixels(pixels, stride, 0);
		return obj;
	}
}
