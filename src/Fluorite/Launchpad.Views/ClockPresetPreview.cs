using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Launchpad.Models;

namespace Launchpad.Views;

public sealed class ClockPresetPreview : UserControl
{
	public sealed record SampleClock(AppSettings Settings)
	{
		public string ClockTime => "12:45";

		public string ClockSeconds => "08";

		public string ClockDate => "10月4日";

		public string ClockDayOfWeek => "星期日";

		public string ClockLunar => "八月廿四";
	}

	public static readonly DependencyProperty KindProperty = DependencyProperty.Register("Kind", typeof(object), typeof(ClockPresetPreview), new PropertyMetadata(null, Rebuild));

	public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register("Settings", typeof(AppSettings), typeof(ClockPresetPreview), new PropertyMetadata(null, Rebuild));

	public static readonly DependencyProperty ShowBackdropProperty = DependencyProperty.Register("ShowBackdrop", typeof(bool), typeof(ClockPresetPreview), new PropertyMetadata(true, Rebuild));

	private AppSettings? _watched;

	public bool ShowBackdrop
	{
		get
		{
			return (bool)GetValue(ShowBackdropProperty);
		}
		set
		{
			SetValue(ShowBackdropProperty, value);
		}
	}

	public object Kind
	{
		get
		{
			return GetValue(KindProperty);
		}
		set
		{
			SetValue(KindProperty, value);
		}
	}

	public AppSettings? Settings
	{
		get
		{
			return (AppSettings)GetValue(SettingsProperty);
		}
		set
		{
			SetValue(SettingsProperty, value);
		}
	}

	public ClockPresetPreview()
	{
		IsHitTestVisible = false;
		Loaded += (object _, RoutedEventArgs _) =>
		{
			Build();
		};
		Unloaded += (object _, RoutedEventArgs _) =>
		{
			if (_watched != null)
			{
				_watched.PropertyChanged -= Changed;
			}
			_watched = null;
		};
	}

	private static void Rebuild(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		((ClockPresetPreview)d).Build();
	}

	private void Changed(object? sender, PropertyChangedEventArgs e)
	{
		string? propertyName = e.PropertyName;
		if (propertyName == null || !propertyName.StartsWith("Clock"))
		{
			string? propertyName2 = e.PropertyName;
			if ((propertyName2 == null || !propertyName2.StartsWith("TopClock")) && !(e.PropertyName == "Theme"))
			{
				return;
			}
		}
		Build();
	}

	private void Build()
	{
		if (_watched != Settings)
		{
			if (_watched != null)
			{
				_watched.PropertyChanged -= Changed;
			}
			_watched = Settings;
			if (IsLoaded && _watched != null)
			{
				_watched.PropertyChanged += Changed;
			}
			else
			{
				_watched = null;
			}
		}
		if (Kind == null)
		{
			return;
		}
		AppSettings appSettings = new AppSettings();
		if (Settings != null)
		{
			PropertyInfo[] properties = typeof(AppSettings).GetProperties(BindingFlags.Instance | BindingFlags.Public);
			foreach (PropertyInfo propertyInfo in properties)
			{
				if (propertyInfo.CanRead && propertyInfo.CanWrite && propertyInfo.GetIndexParameters().Length == 0)
				{
					propertyInfo.SetValue(appSettings, propertyInfo.GetValue(Settings));
					continue;
				}
				appSettings.ClockShowSeconds = false;
				appSettings.ClockShowDate = true;
			}
		}
		FrameworkElement child;
		if (Kind is LargeClockStyle clockLargeStyle)
		{
			appSettings.ClockLargeStyle = clockLargeStyle;
			child = new LargeClockDisplay
			{
				PreviewSettings = appSettings
			};
		}
		else
		{
			if (!(Kind is ClockStyle clockStyle))
			{
				return;
			}
			appSettings.ClockStyle = clockStyle;
			if (!(TryFindResource("TopClockCapsuleContent") is DataTemplate contentTemplate))
			{
				return;
			}
			child = new ContentControl
			{
				Content = new SampleClock(appSettings),
				ContentTemplate = contentTemplate
			};
		}
		Color color = (TryFindResource("SheetTextBrush") as SolidColorBrush)?.Color ?? Colors.White;
		object kind = Kind;
		if ((!(kind is LargeClockStyle) || (LargeClockStyle)kind != LargeClockStyle.Mac) && !string.IsNullOrWhiteSpace(appSettings.ClockTextColor))
		{
			try
			{
				color = (Color)ColorConverter.ConvertFromString(appSettings.ClockTextColor);
			}
			catch
			{
			}
		}
		bool validated = 0.2126 * (double)(int)color.R + 0.7152 * (double)(int)color.G + 0.0722 * (double)(int)color.B > 140.0;
		Content = new Border
		{
			CornerRadius = new CornerRadius(7.0),
			Background = (ShowBackdrop ? new SolidColorBrush(validated ? Color.FromRgb(35, 45, 60) : Color.FromRgb(237, 241, 246)) : Brushes.Transparent),
			Child = new Viewbox
			{
				Stretch = Stretch.Uniform,
				Margin = new Thickness(7.0),
				Child = child
			}
		};
	}
}
