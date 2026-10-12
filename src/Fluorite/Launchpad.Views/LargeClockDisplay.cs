using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Launchpad.Models;
using Launchpad.ViewModels;

namespace Launchpad.Views;

public sealed class LargeClockDisplay : UserControl
{
	public static readonly DependencyProperty ModelProperty = DependencyProperty.Register("Model", typeof(MainViewModel), typeof(LargeClockDisplay), new PropertyMetadata(null, (DependencyObject d, DependencyPropertyChangedEventArgs e) =>
	{
		((LargeClockDisplay)d).Attach();
	}));

	public static readonly DependencyProperty PreviewSettingsProperty = DependencyProperty.Register("PreviewSettings", typeof(AppSettings), typeof(LargeClockDisplay), new PropertyMetadata(null, (DependencyObject d, DependencyPropertyChangedEventArgs e) =>
	{
		((LargeClockDisplay)d).Attach();
	}));

	private MainViewModel? _watched;

	private AppSettings? _settings;

	private readonly List<TextBlock> _digits = new List<TextBlock>();

	private readonly Dictionary<TextBlock, Grid> _faces = new Dictionary<TextBlock, Grid>();

	private TextBlock? _date;

	private GlassClockDigits? _macTime;

	private Brush? _macInk;

	private LaunchpadWindow? _host;

	private int _generation;

	public MainViewModel? Model
	{
		get
		{
			return (MainViewModel)GetValue(ModelProperty);
		}
		set
		{
			SetValue(ModelProperty, value);
		}
	}

	public AppSettings? PreviewSettings
	{
		get
		{
			return (AppSettings)GetValue(PreviewSettingsProperty);
		}
		set
		{
			SetValue(PreviewSettingsProperty, value);
		}
	}

	public LargeClockDisplay()
	{
		IsHitTestVisible = false;
		Loaded += (object _, RoutedEventArgs _) =>
		{
			Attach();
		};
		Unloaded += (object _, RoutedEventArgs _) =>
		{
			Detach();
		};
	}

	private void Detach()
	{
		if (_watched != null)
		{
			_watched.PropertyChanged -= Changed;
		}
		if (_settings != null)
		{
			_settings.PropertyChanged -= SettingsChanged;
		}
		_watched = null;
		_settings = null;
		_generation++;
		if (_host != null)
		{
			_host.ClockBackdropChanged -= BackdropChanged;
		}
		_host = null;
	}

	private void Attach()
	{
		Detach();
		_watched = Model;
		_settings = Model?.Settings ?? PreviewSettings;
		if (Model != null && Window.GetWindow(this) is LaunchpadWindow launchpadWindow)
		{
			_host = launchpadWindow;
			launchpadWindow.ClockBackdropChanged += BackdropChanged;
		}
		if (_watched != null)
		{
			_watched.PropertyChanged += Changed;
		}
		if (_settings != null)
		{
			_settings.PropertyChanged += SettingsChanged;
		}
		Build();
	}

	private void BackdropChanged(object? sender, EventArgs e)
	{
		UpdateMacInk();
	}

	private void UpdateMacInk()
	{
		if (_macTime == null || _macInk == null)
		{
			return;
		}
		Color tint = (_macInk as SolidColorBrush)?.Color ?? Colors.White;
		BitmapSource bitmapSource = _host?.ClockFrostSource;
		_macTime.Tint = tint;
		_macTime.Backdrop = bitmapSource;
		Color color = (_host?.Background as SolidColorBrush)?.Color ?? Colors.Black;
		_macTime.AmbientBrightness = 0.2126 * (double)(int)color.R + 0.7152 * (double)(int)color.G + 0.0722 * (double)(int)color.B;
		if (bitmapSource != null && _host != null && _macTime.ActualWidth > 0.0 && _host.ActualWidth > 0.0)
		{
			Point point = _macTime.TranslatePoint(default, _host);
			_macTime.BackdropRegion = new Rect(point.X / _host.ActualWidth, point.Y / _host.ActualHeight, _macTime.ActualWidth / _host.ActualWidth, _macTime.ActualHeight / _host.ActualHeight);
			if (bitmapSource.Format == PixelFormats.Bgra32 || bitmapSource.Format == PixelFormats.Pbgra32 || bitmapSource.Format == PixelFormats.Bgr32)
			{
				Rect backdropRegion = _macTime.BackdropRegion;
				byte[] entries = new byte[4];
				double pageNumber = 0.0;
				for (int i = 0; i < 3; i++)
				{
					for (int j = 0; j < 3; j++)
					{
						int x = Math.Clamp((int)((backdropRegion.X + backdropRegion.Width * ((double)j + 0.5) / 3.0) * (double)bitmapSource.PixelWidth), 0, bitmapSource.PixelWidth - 1);
						int y = Math.Clamp((int)((backdropRegion.Y + backdropRegion.Height * ((double)i + 0.5) / 3.0) * (double)bitmapSource.PixelHeight), 0, bitmapSource.PixelHeight - 1);
						bitmapSource.CopyPixels(new Int32Rect(x, y, 1, 1), entries, 4, 0);
						pageNumber += 0.2126 * (double)(int)entries[2] + 0.7152 * (double)(int)entries[1] + 0.0722 * (double)(int)entries[0];
					}
				}
				_macTime.AmbientBrightness = pageNumber / 9.0;
			}
		}
		_macTime.InvalidateVisual();
	}

	private void Changed(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "Settings")
		{
			Attach();
			return;
		}
		bool validated;
		switch (e.PropertyName)
		{
		case "ClockTime":
		case "ClockSeconds":
		case "ClockDate":
		case "ClockFullDate":
		case "ClockDayOfWeek":
			validated = true;
			break;
		default:
			validated = false;
			break;
		}
		if (validated)
		{
			Update(animate: true);
		}
	}

	private void SettingsChanged(object? sender, PropertyChangedEventArgs e)
	{
		bool validated;
		switch (e.PropertyName)
		{
		case "ClockFontWeight":
		case "ClockLargeStyle":
		case "ClockLargeFontSize":
		case "ClockShowSeconds":
		case "ClockShowDate":
		case "ClockTransparentBackground":
		case "ClockTextColor":
			validated = true;
			break;
		default:
			validated = false;
			break;
		}
		if (validated)
		{
			Build();
			return;
		}
		switch (e.PropertyName)
		{
		case "Backdrop":
		case "CustomBackdropPath":
		case "BackdropSource":
			validated = true;
			break;
		default:
			validated = false;
			break;
		}
		if (validated)
		{
			UpdateMacInk();
		}
	}

	private static Brush Color(string hex)
	{
		return (Brush)new BrushConverter().ConvertFromString(hex);
	}

	private void Build()
	{
		_generation++;
		_digits.Clear();
		_faces.Clear();
		_macTime = null;
		_macInk = null;
		if (_settings == null)
		{
			Content = null;
			return;
		}
		AppSettings settings = _settings;
		LargeClockStyle clockLargeStyle = settings.ClockLargeStyle;
		bool validated = clockLargeStyle == LargeClockStyle.Flip;
		bool flag2 = clockLargeStyle == LargeClockStyle.Paper;
		bool flag3 = clockLargeStyle == LargeClockStyle.Digital;
		bool flag4 = clockLargeStyle == LargeClockStyle.Glass;
		string hex;
		if (flag2)
		{
			hex = "#40372F";
		}
		else
		{
			hex = (flag3 ? "#BADBC6" : "#F7F7F8");
		}
		Brush brush = Color(hex);
		if (clockLargeStyle != LargeClockStyle.Mac && !string.IsNullOrWhiteSpace(settings.ClockTextColor))
		{
			try
			{
				brush = Color(settings.ClockTextColor);
			}
			catch
			{
			}
		}
		double clockLargeFontSize = settings.ClockLargeFontSize;
		StackPanel stackPanel = new StackPanel
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_date = new TextBlock
		{
			FontSize = clockLargeFontSize * 0.26,
			Foreground = brush,
			Opacity = 0.7,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0),
			Visibility = ((!settings.ClockShowDate) ? Visibility.Collapsed : Visibility.Visible)
		};
		stackPanel.Children.Add(_date);
		if (clockLargeStyle == LargeClockStyle.Mac)
		{
			_date.FontSize = clockLargeFontSize * 0.26;
			_date.FontWeight = FontWeights.SemiBold;
			_date.Opacity = 0.95;
			_date.Margin = new Thickness(0.0, 0.0, 0.0, clockLargeFontSize * 0.025);
			_macTime = new GlassClockDigits
			{
				FontSize = clockLargeFontSize,
				FontWeight = ((settings.ClockFontWeight == "Normal") ? FontWeights.Bold : ((FontWeight)new FontWeightConverter().ConvertFromString(settings.ClockFontWeight))),
				HorizontalAlignment = HorizontalAlignment.Center
			};
			_macInk = brush;
			_macTime.SizeChanged += (object _, SizeChangedEventArgs _) =>
			{
				UpdateMacInk();
			};
			stackPanel.Children.Add(_macTime);
			Content = new Border
			{
				Child = stackPanel,
				Padding = new Thickness(12.0, 14.0, 12.0, 14.0),
				Background = Brushes.Transparent
			};
			Update(animate: false);
			UpdateMacInk();
			return;
		}
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		stackPanel.Children.Add(stackPanel2);
		for (int pageNumber = 0; pageNumber < (settings.ClockShowSeconds ? 3 : 2); pageNumber++)
		{
			if (pageNumber > 0)
			{
				stackPanel2.Children.Add(new TextBlock
				{
					Text = (validated ? "·" : (flag2 ? "" : ":")),
					FontSize = clockLargeFontSize * (validated ? 0.22 : 0.68),
					Foreground = brush,
					VerticalAlignment = VerticalAlignment.Center,
					Margin = new Thickness(validated ? 8 : 4, 0.0, validated ? 8 : 4, 0.0),
					Opacity = 0.4
				});
			}
			StackPanel stackPanel3 = new StackPanel
			{
				VerticalAlignment = VerticalAlignment.Bottom
			};
			StackPanel stackPanel4 = new StackPanel
			{
				Orientation = Orientation.Horizontal
			};
			stackPanel3.Children.Add(stackPanel4);
			double settingIndex = clockLargeFontSize;
			for (int targetPage = 0; targetPage < 1; targetPage++)
			{
				TextBlock textBlock = new TextBlock
				{
					FontSize = (validated ? (settingIndex * 0.94) : clockLargeFontSize),
					Foreground = brush,
					TextAlignment = TextAlignment.Center,
					FontFamily = new FontFamily(flag2 ? "Georgia" : (flag3 ? "Consolas" : "Segoe UI Variable Display")),
					FontWeight = (validated ? FontWeights.SemiBold : (flag3 ? FontWeights.Bold : (flag4 ? FontWeights.Normal : ((FontWeight)new FontWeightConverter().ConvertFromString(settings.ClockFontWeight))))),
					VerticalAlignment = VerticalAlignment.Center,
					RenderTransformOrigin = new Point(0.5, 0.5),
					RenderTransform = new ScaleTransform(1.0, 1.0),
					Width = (validated ? (settingIndex * 1.38) : (clockLargeFontSize * 1.44))
				};
				Typography.SetNumeralAlignment(textBlock, FontNumeralAlignment.Tabular);
				_digits.Add(textBlock);
				Grid grid = new Grid
				{
					Width = (validated ? (settingIndex * 1.52) : (clockLargeFontSize * 1.65)),
					Height = (validated ? (settingIndex * 1.18) : (clockLargeFontSize * 1.4))
				};
				if (validated)
				{
					RectangleGeometry rectangleGeometry = new RectangleGeometry(new Rect(0.0, 0.0, grid.Width, grid.Height), 6.0, 6.0);
					rectangleGeometry.Freeze();
					grid.Clip = rectangleGeometry;
				}
				grid.Children.Add(textBlock);
				_faces[textBlock] = grid;
				if (validated)
				{
					Border element = new Border
					{
						Height = 2.0,
						Background = Color("#E0101114"),
						VerticalAlignment = VerticalAlignment.Center
					};
					Panel.SetZIndex(element, 10);
					grid.Children.Add(element);
				}
				stackPanel4.Children.Add(new Border
				{
					Child = grid,
					CornerRadius = new CornerRadius(validated ? 7 : 18),
					Margin = new Thickness(validated ? 3 : (flag2 ? 5 : 0), 0.0, validated ? 3 : (flag2 ? 5 : 0), 0.0),
					Background = (validated ? new LinearGradientBrush(System.Windows.Media.Color.FromRgb(25, 26, 29), System.Windows.Media.Color.FromRgb(10, 11, 13), 90.0) : (flag2 ? Color("#EEE5D6") : Brushes.Transparent)),
					BorderBrush = (validated ? Color("#35363B") : (flag2 ? Color("#D9CDBB") : Brushes.Transparent)),
					BorderThickness = new Thickness((validated | flag2) ? 1 : 0),
					Effect = (validated ? new DropShadowEffect
					{
						BlurRadius = 12.0,
						ShadowDepth = 4.0,
						Opacity = 0.25
					} : null)
				});
			}
			stackPanel2.Children.Add(stackPanel3);
		}
		Border content = new Border
		{
			Child = stackPanel,
			Padding = new Thickness((flag4 | flag3) ? 32 : 12, flag4 ? 22 : 14, (flag4 | flag3) ? 32 : 12, flag4 ? 22 : 14),
			CornerRadius = new CornerRadius(28.0),
			Background = (((clockLargeStyle == LargeClockStyle.Minimal) | validated) ? Brushes.Transparent : (flag4 ? new LinearGradientBrush(System.Windows.Media.Color.FromArgb((byte)(settings.ClockTransparentBackground ? 65 : 175), 112, 126, 149), System.Windows.Media.Color.FromArgb((byte)(settings.ClockTransparentBackground ? 24 : 125), 30, 42, 61), 65.0) : (settings.ClockTransparentBackground ? Brushes.Transparent : (flag3 ? Color("#F0192320") : Color("#E6D9CDBB"))))),
			BorderBrush = (flag4 ? ((Brush)new LinearGradientBrush(System.Windows.Media.Color.FromArgb(170, byte.MaxValue, byte.MaxValue, byte.MaxValue), System.Windows.Media.Color.FromArgb(25, byte.MaxValue, byte.MaxValue, byte.MaxValue), 90.0)) : ((Brush)Brushes.Transparent)),
			BorderThickness = new Thickness(flag4 ? 1 : 0),
			Effect = (flag4 ? new DropShadowEffect
			{
				BlurRadius = 28.0,
				ShadowDepth = 8.0,
				Opacity = 0.18
			} : null)
		};
		Content = content;
		Update(animate: false);
	}

	private void Update(bool animate)
	{
		if (_settings == null)
		{
			return;
		}
		if (_macTime != null)
		{
			_macTime.Text = (Model?.ClockTime ?? "12:45") + (_settings.ClockShowSeconds ? (":" + (Model?.ClockSeconds ?? "08")) : "");
			if (_date != null)
			{
				_date.Text = ((Model == null) ? "10月4日周日" : (Model.ClockDate + Model.ClockDayOfWeek.Replace("星期", "周")));
			}
			return;
		}
		if (_date != null)
		{
			_date.Text = ((Model == null) ? "2026年10月4日 · 星期日" : (Model.ClockFullDate + " · " + Model.ClockDayOfWeek));
		}
		string[] entries = (Model?.ClockTime ?? "12:45").Split(':').Concat(new string[1] { Model?.ClockSeconds ?? "08" }).ToArray();
		for (int i = 0; i < Math.Min(_digits.Count, entries.Length); i++)
		{
			TextBlock label = _digits[i];
			string text = entries[i];
			if (label.Text == text)
			{
				continue;
			}
			ScaleTransform scaleTransform = (ScaleTransform)label.RenderTransform;
			scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			scaleTransform.ScaleY = 1.0;
			if (!animate || _settings.ClockLargeStyle != LargeClockStyle.Flip || !IsVisible)
			{
				label.Text = text;
				continue;
			}
			int generation = _generation;
			Grid face = _faces[label];
			string commandLinePath = label.Text;
			label.Text = text;
			Grid oldBottom = Leaf(commandLinePath, bottom: true);
			Grid oldTop = Leaf(commandLinePath, bottom: false);
			Grid newBottom = Leaf(text, bottom: true);
			((ScaleTransform)newBottom.RenderTransform).ScaleY = 0.0;
			face.Children.Add(oldBottom);
			face.Children.Add(newBottom);
			face.Children.Add(oldTop);
			DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(130.0))
			{
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseIn
				}
			};
			doubleAnimation.Completed += (object? _, EventArgs _) =>
			{
				face.Children.Remove(oldTop);
				face.Children.Remove(oldBottom);
				if (generation != _generation)
				{
					face.Children.Remove(newBottom);
				}
				else
				{
					DoubleAnimation doubleAnimation2 = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180.0))
					{
						EasingFunction = new CubicEase
						{
							EasingMode = EasingMode.EaseOut
						}
					};
					doubleAnimation2.Completed += (object? target, EventArgs e) =>
					{
						face.Children.Remove(newBottom);
					};
					((ScaleTransform)newBottom.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, doubleAnimation2);
				}
			};
			((ScaleTransform)oldTop.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, doubleAnimation);
			Grid Leaf(string text3, bool bottom)
			{
				double pageNumber = face.Height / 2.0;
				Grid grid = new Grid
				{
					Width = face.Width,
					Height = pageNumber,
					VerticalAlignment = (bottom ? VerticalAlignment.Bottom : VerticalAlignment.Top),
					ClipToBounds = true,
					Background = Color(bottom ? "#0A0B0D" : "#191A1D"),
					RenderTransformOrigin = new Point(0.5, (!bottom) ? 1 : 0),
					RenderTransform = new ScaleTransform(1.0, 1.0)
				};
				CombinedGeometry combinedGeometry = new CombinedGeometry(GeometryCombineMode.Intersect, new RectangleGeometry(new Rect(0.0, 0.0, face.Width, pageNumber)), new RectangleGeometry(new Rect(0.0, bottom ? (0.0 - pageNumber) : 0.0, face.Width, face.Height), 6.0, 6.0));
				combinedGeometry.Freeze();
				grid.Clip = combinedGeometry;
				Canvas canvas = new Canvas();
				TextBlock element = new TextBlock
				{
					Text = text3,
					FontSize = label.FontSize,
					FontFamily = label.FontFamily,
					FontWeight = label.FontWeight,
					Foreground = label.Foreground,
					TextAlignment = TextAlignment.Center,
					Width = face.Width,
					Height = face.Height,
					Padding = new Thickness(0.0, Math.Max(0.0, (face.Height - label.DesiredSize.Height) / 2.0), 0.0, 0.0)
				};
				Typography.SetNumeralAlignment(element, FontNumeralAlignment.Tabular);
				Canvas.SetTop(element, bottom ? (0.0 - pageNumber) : 0.0);
				canvas.Children.Add(element);
				grid.Children.Add(canvas);
				return grid;
			}
		}
	}
}
