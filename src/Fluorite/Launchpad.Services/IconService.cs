using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Launchpad.Infrastructure;
using Launchpad.Interop;
using Launchpad.Models;

namespace Launchpad.Services;

public class IconService : IIconService
{
	private readonly string _cacheDir;

	private readonly ConcurrentDictionary<string, WeakReference<ImageSource>> _memoryCache = new ConcurrentDictionary<string, WeakReference<ImageSource>>(StringComparer.OrdinalIgnoreCase);

	private IconPack? _activePack;

	private bool _autoTile;

	private bool _frostedTile;

	private int _pixelSize = 256;

	private const int OffScreenChunk = 30;

	public string CacheDirectory => _cacheDir;

	public bool SetPixelSize(int pixels)
	{
		int pageNumber = ((pixels > 256) ? 512 : 256);
		if (pageNumber == _pixelSize)
		{
			return false;
		}
		_pixelSize = pageNumber;
		_memoryCache.Clear();
		return true;
	}

	public IconService(string? cacheDirectory = null)
	{
		_cacheDir = cacheDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fluorite", "icon-cache");
		Directory.CreateDirectory(_cacheDir);
	}

	public void SetActivePack(IconPack? pack)
	{
		if (pack != _activePack)
		{
			_activePack = pack;
			_memoryCache.Clear();
		}
	}

	public void SetAutoTile(bool enabled)
	{
		if (enabled != _autoTile)
		{
			_autoTile = enabled;
			_memoryCache.Clear();
		}
	}

	public void SetFrostedTile(bool enabled)
	{
		if (_frostedTile != enabled)
		{
			_frostedTile = enabled;
			_memoryCache.Clear();
		}
	}

	public async Task<ImageSource?> GetIconAsync(AppItem app, CancellationToken cancellationToken = default(CancellationToken))
	{
		bool webShortcut = !app.HasIconOverride && WebShortcutIcon.IsWeb(app);
		string packFile = ((_activePack != null && !app.HasIconOverride && !webShortcut) ? IconPackService.ResolveImageFile(_activePack, app) : null);
		bool tiling = _autoTile && packFile == null && !app.HasIconOverride;
		bool frosted = _frostedTile;
		int decodePixels = ((packFile != null) ? _pixelSize : 256);
		string cacheKey = (webShortcut ? $"web-globe-v1-{tiling}-{frosted}" : ComputeCacheKey(app, packFile, tiling, decodePixels, frosted));
		if (_memoryCache.TryGetValue(cacheKey, out WeakReference<ImageSource> value) && value.TryGetTarget(out var target))
		{
			return target;
		}
		string cacheFilePath = Path.Combine(_cacheDir, cacheKey + ".png");
		if (File.Exists(cacheFilePath))
		{
			try
			{
				ImageSource imageSource = await Task.Run(() => LoadBitmapFromDisk(cacheFilePath, decodePixels), cancellationToken);
				if (imageSource != null)
				{
					LogIcon(app, "disk-cache", imageSource);
					CacheImage(cacheKey, imageSource);
					return imageSource;
				}
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex2)
			{
				App.Log("Error reading icon cache file '" + cacheFilePath + "': " + ex2.Message);
			}
		}
		bool tiled = false;
		if (await Task.Run(() =>
		{
			if (packFile != null)
			{
				return LoadImageFile(packFile, decodePixels);
			}
			string text = ResolveIconPath(app);
			ImageSource imageSource2 = (webShortcut ? WebShortcutIcon.Render() : null);
			if ((string.Equals(text, app.IconOverridePath, StringComparison.OrdinalIgnoreCase) || text.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || text.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) || text.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || text.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)) && File.Exists(text))
			{
				imageSource2 = LoadImageFile(text, 256);
			}
			string commandLinePath = ((imageSource2 == null && !app.HasIconOverride) ? PackagedAppIcon.Resolve(text) : null);
			BitmapSource bitmapSource2 = ((commandLinePath != null) ? LoadImageFile(commandLinePath, 256) : null);
			if (imageSource2 == null)
			{
				imageSource2 = bitmapSource2 ?? ShellIcons.ExtractHighResolutionIcon(text, tiling ? 256 : 128, string.Equals(text, NormalizeIconPath(app.IconPath), StringComparison.OrdinalIgnoreCase) ? app.IconIndex : 0);
			}
			LogIcon(app, (bitmapSource2 != null) ? "package" : "shell", imageSource2);
			if (!tiling || !(imageSource2 is BitmapSource source))
			{
				return imageSource2;
			}
			BitmapSource bitmapSource3 = IconTileRenderer.Render(source, frosted);
			if (bitmapSource3 == null)
			{
				return imageSource2;
			}
			tiled = true;
			return bitmapSource3;
		}, cancellationToken) is BitmapSource bitmapSource)
		{
			BitmapSource normalized = (((packFile != null) | tiled) ? (IconNormalizer.ScaleToFullBleed(bitmapSource) ?? bitmapSource) : (IconNormalizer.TrimTransparentMargins(bitmapSource) ?? bitmapSource));
			CacheImage(cacheKey, normalized);
			AppItem app2 = app;
			string stage;
			if (packFile != null)
			{
				stage = "pack-final";
			}
			else
			{
				stage = (tiled ? "tile-final" : "raw-final");
			}
			LogIcon(app2, stage, normalized);
			Task.Run(() =>
			{
				SaveBitmapToDisk(normalized, cacheFilePath);
			}, CancellationToken.None);
			return normalized;
		}
		return null;
	}

	private void CacheImage(string key, ImageSource image)
	{
		if (_memoryCache.Count >= 512)
		{
			WeakReference<ImageSource> value;
			foreach (KeyValuePair<string, WeakReference<ImageSource>> item in _memoryCache)
			{
				if (!item.Value.TryGetTarget(out var _))
				{
					_memoryCache.TryRemove(item.Key, out value);
				}
			}
			if (_memoryCache.Count >= 512)
			{
				string text = _memoryCache.Keys.FirstOrDefault();
				if (text != null)
				{
					_memoryCache.TryRemove(text, out value);
				}
			}
		}
		_memoryCache[key] = new WeakReference<ImageSource>(image);
	}

	private static void LogIcon(AppItem app, string stage, ImageSource? image)
	{
		string value = ((image is BitmapSource bitmapSource) ? $"{bitmapSource.PixelWidth}x{bitmapSource.PixelHeight}" : "missing");
		App.Log($"[ICON-DIAG] app='{app.Name}' source={app.Source} stage={stage} pixels={value}");
	}

	internal static string ResolveIconPath(AppItem app)
	{
		if (app.HasIconOverride && File.Exists(app.IconOverridePath))
		{
			return app.IconOverridePath;
		}
		string text = NormalizeIconPath(app.IconPath);
		if (text.Length == 0 || (Path.IsPathRooted(text) && !File.Exists(text) && !Directory.Exists(text)))
		{
			return NormalizeIconPath(app.ExecutablePath);
		}
		return text;
	}

	private static string NormalizeIconPath(string? path)
	{
		return Environment.ExpandEnvironmentVariables(path ?? string.Empty).Trim().Trim('"');
	}

	private static BitmapSource? LoadImageFile(string path, int decodeWidth = 128)
	{
		try
		{
			using FileStream streamSource = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.DecodePixelWidth = decodeWidth;
			bitmapImage.StreamSource = streamSource;
			bitmapImage.EndInit();
			bitmapImage.Freeze();
			return bitmapImage;
		}
		catch (Exception ex)
		{
			App.Log("[ICON] cannot decode '" + path + "': " + ex.Message);
			return null;
		}
	}

	public async Task PreloadIconsAsync(IEnumerable<AppItem> apps, int onScreenCount = 0, CancellationToken cancellationToken = default(CancellationToken))
	{
		Dispatcher dispatcher = Application.Current?.Dispatcher;
		if (dispatcher == null)
		{
			return;
		}
		IReadOnlyList<AppItem> list = (apps as IReadOnlyList<AppItem>) ?? apps.ToList();
		int head = Math.Clamp(onScreenCount, 0, list.Count);
		List<(AppItem, ImageSource)> visible = new List<(AppItem, ImageSource)>();
		await ExtractAsync(list.Take(head), visible, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		await dispatcher.InvokeAsync(() =>
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			foreach (var item3 in visible)
			{
				AppItem item = item3.Item1;
				ImageSource item2 = item3.Item2;
				item.Icon = item2;
			}
		}, DispatcherPriority.DataBind);
		for (int i = head; i < list.Count; i += 30)
		{
			cancellationToken.ThrowIfCancellationRequested();
			List<(AppItem, ImageSource)> slice = new List<(AppItem, ImageSource)>();
			await ExtractAsync(list.Skip(i).Take(30), slice, cancellationToken);
			await dispatcher.InvokeAsync(() =>
			{
				if (cancellationToken.IsCancellationRequested)
				{
					return;
				}
				foreach (var item4 in slice)
				{
					AppItem item = item4.Item1;
					ImageSource item2 = item4.Item2;
					item.Icon = item2;
				}
			}, DispatcherPriority.Background);
		}
	}

	private async Task ExtractAsync(IEnumerable<AppItem> apps, List<(AppItem App, ImageSource Icon)> into, CancellationToken cancellationToken)
	{
		object gate = new object();
		ParallelOptions parallelOptions = new ParallelOptions
		{
			MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount / 2, 1, 4),
			CancellationToken = cancellationToken
		};
		await Parallel.ForEachAsync(apps, parallelOptions, async (AppItem app, CancellationToken ct) =>
		{
			if (app.Icon == null)
			{
				ImageSource imageSource = await GetIconAsync(app, ct);
				if (imageSource != null)
				{
					lock (gate)
					{
						into.Add((app, imageSource));
					}
				}
			}
		});
	}

	private string ComputeCacheKey(AppItem app, string? packFile, bool tiling, int decodePixels, bool frosted)
	{
		if (packFile != null)
		{
			long value = _activePack?.Stamp.Ticks ?? 0;
			long failure = 0L;
			try
			{
				failure = File.GetLastWriteTimeUtc(packFile).Ticks;
			}
			catch
			{
			}
			string s = $"v9|pack|{decodePixels}|{_activePack?.Id}|{value}|{packFile}|{failure}";
			using MD5 mD = MD5.Create();
			return Convert.ToHexString(mD.ComputeHash(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
		}
		string text = ResolveIconPath(app);
		long value3 = 0L;
		if (File.Exists(text))
		{
			try
			{
				value3 = File.GetLastWriteTimeUtc(text).Ticks;
			}
			catch
			{
			}
		}
		string value4 = ((!app.HasIconOverride && text.Contains('!')) ? "v8-package" : "v10-resource-index");
		string s2 = $"{value4}|{(tiling ? $"tile{"t6-white-artwork"}-{frosted}" : "raw")}|{text}|{app.IconIndex}|{value3}";
		using MD5 mD2 = MD5.Create();
		return Convert.ToHexString(mD2.ComputeHash(Encoding.UTF8.GetBytes(s2))).ToLowerInvariant();
	}

	private static ImageSource? LoadBitmapFromDisk(string filePath, int decodePixels)
	{
		try
		{
			using FileStream streamSource = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.DecodePixelWidth = decodePixels;
			bitmapImage.StreamSource = streamSource;
			bitmapImage.EndInit();
			bitmapImage.Freeze();
			return bitmapImage;
		}
		catch
		{
			return null;
		}
	}

	private static void SaveBitmapToDisk(BitmapSource bitmap, string targetPath)
	{
		try
		{
			string text = targetPath + ".tmp";
			PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
			pngBitmapEncoder.Frames.Add(BitmapFrame.Create(bitmap));
			using (FileStream stream = new FileStream(text, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				pngBitmapEncoder.Save(stream);
			}
			File.Move(text, targetPath, overwrite: true);
		}
		catch
		{
		}
	}
}
