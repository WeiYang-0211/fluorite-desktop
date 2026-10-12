using System.Windows.Media.Imaging;

namespace Launchpad.Infrastructure;

internal static class BitmapSnapshot
{
	internal static BitmapSource Detach(BitmapSource source)
	{
		int pageNumber = checked(source.PixelWidth * source.Format.BitsPerPixel + 7) / 8;
		byte[] pixels = new byte[checked(pageNumber * source.PixelHeight)];
		source.CopyPixels(pixels, pageNumber, 0);
		BitmapSource bitmapSource = BitmapSource.Create(source.PixelWidth, source.PixelHeight, source.DpiX, source.DpiY, source.Format, source.Palette, pixels, pageNumber);
		bitmapSource.Freeze();
		return bitmapSource;
	}
}
