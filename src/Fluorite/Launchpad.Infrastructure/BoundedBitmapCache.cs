using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace Launchpad.Infrastructure;

internal sealed class BoundedBitmapCache
{
	private readonly int _maxEntries;

	private readonly long _maxBytes;

	private readonly Dictionary<string, BitmapSource> _images = new Dictionary<string, BitmapSource>();

	private readonly LinkedList<string> _recent = new LinkedList<string>();

	internal long EstimatedBytes { get; private set; }

	internal int Count => _images.Count;

	internal BoundedBitmapCache(int maxEntries, long maxBytes)
	{
		_maxEntries = Math.Max(1, maxEntries);
		_maxBytes = Math.Max(1L, maxBytes);
	}

	internal bool TryGetValue(string key, out BitmapSource image)
	{
		if (!_images.TryGetValue(key, out image))
		{
			return false;
		}
		_recent.Remove(key);
		_recent.AddLast(key);
		return true;
	}

	internal void Set(string key, BitmapSource image)
	{
		if (_images.Remove(key, out BitmapSource value))
		{
			EstimatedBytes -= Size(value);
		}
		_recent.Remove(key);
		_images[key] = image;
		_recent.AddLast(key);
		EstimatedBytes += Size(image);
		while (_images.Count > 1 && (_images.Count > _maxEntries || EstimatedBytes > _maxBytes))
		{
			string failure = _recent.First.Value;
			_recent.RemoveFirst();
			EstimatedBytes -= Size(_images[failure]);
			_images.Remove(failure);
		}
	}

	internal void Clear()
	{
		_images.Clear();
		_recent.Clear();
		EstimatedBytes = 0L;
	}

	private static long Size(BitmapSource image)
	{
		return ((long)image.PixelWidth * (long)image.Format.BitsPerPixel + 7) / 8 * image.PixelHeight;
	}
}
