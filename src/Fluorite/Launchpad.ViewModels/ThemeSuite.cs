using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Launchpad.Models;

namespace Launchpad.ViewModels;

public sealed record ThemeSuite(string Id, string Name, string Description, string Start, string End, LargeClockStyle Clock, AppTheme Theme, bool Pro) : INotifyPropertyChanged
{
	private Brush? _preview;

	private Task? _previewLoad;

	public string WallpaperFile => Id switch
	{
		"default" => "Reflection 3.jpg", 
		"midnight" => "Reflection 1.jpg", 
		"aurora" => "Reflection 4.jpg", 
		"cream" => "Reflection 2.jpg", 
		_ => "Antelope Canyon.jpg", 
	};

	public Uri WallpaperUri => new Uri("pack://application:,,,/Fluorite;component/Assets/Wallpapers/" + Uri.EscapeDataString(WallpaperFile));

	public Brush Preview
	{
		get
		{
			PreparePreviewAsync();
			return _preview ?? PreviewPlaceholder;
		}
	}

	public string Badge
	{
		get
		{
			if (!Pro)
			{
				return "自定义";
			}
			return "PRO";
		}
	}

	private static readonly Brush PreviewPlaceholder = CreatePlaceholder();

	public Brush PreviewInk
	{
		get
		{
			if (!(Id == "cream"))
			{
				return Brushes.White;
			}
			return new SolidColorBrush(Color.FromRgb(73, 54, 43));
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	private static Brush CreatePlaceholder()
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromRgb(52, 62, 78));
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	public Task PreparePreviewAsync()
	{
		return _previewLoad ?? (_previewLoad = LoadPreviewAsync());
	}

	private async Task LoadPreviewAsync()
	{
		if (Id == "custom")
		{
			_preview = PreviewPlaceholder;
			return;
		}
		try
		{
			Brush brush = await Task.Run((Func<Brush>)LoadPreview).ConfigureAwait(continueOnCapturedContext: false);
			Dispatcher dispatcher = Application.Current?.Dispatcher;
			if (dispatcher != null && !dispatcher.HasShutdownStarted)
			{
				await dispatcher.InvokeAsync(() =>
				{
					_preview = brush;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Preview"));
				}, DispatcherPriority.Background);
			}
		}
		catch (Exception ex)
		{
			App.Log("Wallpaper thumbnail unavailable: " + ex.Message);
		}
	}

	private Brush LoadPreview()
	{
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.UriSource = WallpaperUri;
		bitmapImage.DecodePixelWidth = 420;
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.EndInit();
		bitmapImage.Freeze();
		ImageBrush imageBrush = new ImageBrush(bitmapImage);
		imageBrush.Stretch = Stretch.UniformToFill;
		imageBrush.Freeze();
		return imageBrush;
	}
}
