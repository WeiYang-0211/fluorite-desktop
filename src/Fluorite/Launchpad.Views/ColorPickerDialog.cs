using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Launchpad.Views;

public sealed class ColorPickerDialog : Window
{
	private readonly Border _hueBase = new Border();

	private readonly Border _preview = new Border
	{
		Width = 48.0,
		Height = 48.0,
		CornerRadius = new CornerRadius(8.0)
	};

	private readonly Canvas _field = new Canvas
	{
		Height = 160.0,
		Background = Brushes.Transparent,
		ClipToBounds = true,
		Cursor = Cursors.Cross
	};

	private readonly Ellipse _marker = new Ellipse
	{
		Width = 12.0,
		Height = 12.0,
		Stroke = Brushes.White,
		StrokeThickness = 2.0,
		Fill = Brushes.Transparent,
		IsHitTestVisible = false
	};

	private readonly Slider _hue = new Slider
	{
		Minimum = 0.0,
		Maximum = 360.0,
		Height = 24.0,
		IsMoveToPointEnabled = true,
		Margin = new Thickness(0.0, 10.0, 0.0, 12.0)
	};

	private readonly TextBox _hex;

	private readonly TextBox[] _rgb = new TextBox[3];

	private readonly TextBlock _error = DialogKit.Body("");

	private readonly Button _accept = DialogKit.Push("应用颜色", primary: true);

	private double _saturation;

	private double _brightness;

	private bool _syncing;

	public string? SelectedColor { get; private set; }

	public Color PreviewColor { get; private set; }

	public ColorPickerDialog(Window? owner, string current)
	{
		Window window = DialogKit.OwnerOrDefault(owner, out var centerScreen);
		DialogKit.Configure(this, centerScreen);
		SetResourceReference(Control.FontFamilyProperty, "AppFont");
		if (window != null)
		{
			Owner = window;
		}
		if (window != null)
		{
			string[] entries = new string[4] { "STitle", "SHint", "SInput", "SheetButton" };
			foreach (string text in entries)
			{
				if (window.TryFindResource(text) is Style value)
				{
					Resources[text] = value;
				}
			}
		}
		Title = "选择背景颜色";
		StackPanel stackPanel = new StackPanel();
		TextBlock textBlock = DialogKit.Title(Title);
		UseTextStyle(textBlock, "STitle");
		stackPanel.Children.Add(textBlock);
		TextBlock textBlock2 = DialogKit.Body("拖动色板或输入颜色值");
		UseTextStyle(textBlock2, "SHint");
		stackPanel.Children.Add(textBlock2);
		Grid fieldGrid = new Grid();
		fieldGrid.SizeChanged += (object _, SizeChangedEventArgs _) =>
		{
			fieldGrid.Clip = new RectangleGeometry(new Rect(fieldGrid.RenderSize), 10.0, 10.0);
		};
		fieldGrid.Children.Add(_hueBase);
		fieldGrid.Children.Add(new Border
		{
			Background = new LinearGradientBrush(Colors.White, Colors.Transparent, 0.0)
		});
		fieldGrid.Children.Add(new Border
		{
			Background = new LinearGradientBrush(Colors.Transparent, Colors.Black, 90.0)
		});
		_field.Children.Add(_marker);
		fieldGrid.Children.Add(_field);
		Border element = new Border
		{
			CornerRadius = new CornerRadius(12.0),
			ClipToBounds = true,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0),
			Child = fieldGrid
		};
		stackPanel.Children.Add(element);
		LinearGradientBrush linearGradientBrush = new LinearGradientBrush
		{
			StartPoint = new Point(0.0, 0.5),
			EndPoint = new Point(1.0, 0.5)
		};
		for (int pageNumber = 0; pageNumber <= 6; pageNumber++)
		{
			linearGradientBrush.GradientStops.Add(new GradientStop(HsvToColor(pageNumber * 60, 1.0, 1.0), (double)pageNumber / 6.0));
		}
		_hue.Background = linearGradientBrush;
		_hue.Template = (ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"\n    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" TargetType=\"Slider\">\n    <Grid Background=\"Transparent\">\n        <Border Height=\"10\" CornerRadius=\"5\" Background=\"{TemplateBinding Background}\" IsHitTestVisible=\"False\"/>\n        <Track x:Name=\"PART_Track\" Minimum=\"{TemplateBinding Minimum}\" Maximum=\"{TemplateBinding Maximum}\" Value=\"{TemplateBinding Value}\">\n            <Track.DecreaseRepeatButton><RepeatButton Command=\"{x:Static Slider.DecreaseLarge}\" Opacity=\"0\" Focusable=\"False\"/></Track.DecreaseRepeatButton>\n            <Track.Thumb><Thumb Width=\"16\" Height=\"16\"><Thumb.Template><ControlTemplate TargetType=\"Thumb\"><Border CornerRadius=\"8\" Background=\"White\" BorderBrush=\"#66000000\" BorderThickness=\"1\"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>\n            <Track.IncreaseRepeatButton><RepeatButton Command=\"{x:Static Slider.IncreaseLarge}\" Opacity=\"0\" Focusable=\"False\"/></Track.IncreaseRepeatButton>\n        </Track>\n    </Grid>\n</ControlTemplate>");
		stackPanel.Children.Add(_hue);
		Grid grid = new Grid
		{
			ColumnDefinitions = 
			{
				new ColumnDefinition
				{
					Width = new GridLength(60.0)
				},
				new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				}
			}
		};
		StackPanel stackPanel2 = new StackPanel();
		TextBlock textBlock3 = DialogKit.Body("HEX", 12.5, dim: true, new Thickness(0.0, 0.0, 0.0, 4.0));
		UseTextStyle(textBlock3, "SHint");
		stackPanel2.Children.Add(textBlock3);
		_hex = Input(window);
		_hex.MaxLength = 7;
		stackPanel2.Children.Add(_hex);
		grid.Children.Add(_preview);
		grid.Children.Add(stackPanel2);
		Grid.SetColumn(stackPanel2, 1);
		stackPanel.Children.Add(grid);
		Grid grid2 = new Grid
		{
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		for (int settingIndex = 0; settingIndex < 3; settingIndex++)
		{
			int failure = settingIndex;
			grid2.ColumnDefinitions.Add(new ColumnDefinition());
			StackPanel stackPanel3 = new StackPanel
			{
				Margin = new Thickness((settingIndex != 0) ? 8 : 0, 0.0, 0.0, 0.0)
			};
			TextBlock textBlock4 = DialogKit.Body((new string[3] { "红 R", "绿 G", "蓝 B" })[settingIndex], 12.5, dim: true, new Thickness(0.0, 0.0, 0.0, 4.0));
			UseTextStyle(textBlock4, "SHint");
			stackPanel3.Children.Add(textBlock4);
			_rgb[settingIndex] = Input(window);
			_rgb[settingIndex].MaxLength = 3;
			_rgb[settingIndex].TextChanged += (object _, TextChangedEventArgs _) =>
			{
				ReadRgb();
			};
			stackPanel3.Children.Add(_rgb[settingIndex]);
			grid2.Children.Add(stackPanel3);
			Grid.SetColumn(stackPanel3, failure);
		}
		_error.Height = 18.0;
		_error.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		UseTextStyle(_error, "SHint");
		stackPanel.Children.Add(grid2);
		stackPanel.Children.Add(_error);
		Button button = DialogKit.Push("取消", primary: false);
		UseButtonStyle(button, primary: false);
		UseButtonStyle(_accept, primary: true);
		button.Click += (object _, RoutedEventArgs _) =>
		{
			Close();
		};
		_accept.IsDefault = true;
		_accept.Click += (object _, RoutedEventArgs _) =>
		{
			if (_accept.IsEnabled)
			{
				SelectedColor = Hex(PreviewColor);
				Close();
			}
		};
		StackPanel stackPanel4 = DialogKit.Buttons(button, _accept);
		stackPanel4.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		stackPanel.Children.Add(stackPanel4);
		Border border = DialogKit.Card(380.0);
		border.Child = stackPanel;
		Content = border;
		border.Padding = new Thickness(20.0, 20.0, 20.0, 16.0);
		_hex.TextChanged += (object _, TextChangedEventArgs _) =>
		{
			ReadHex();
		};
		_hue.ValueChanged += (object _, RoutedPropertyChangedEventArgs<double> _) =>
		{
			if (!_syncing)
			{
				SetColor(HsvToColor(_hue.Value, _saturation, _brightness), updateHsv: false);
			}
		};
		_field.MouseLeftButtonDown += (object _, MouseButtonEventArgs e) =>
		{
			_field.CaptureMouse();
			Pick(e.GetPosition(_field));
		};
		_field.MouseMove += (object _, MouseEventArgs e) =>
		{
			if (_field.IsMouseCaptured)
			{
				Pick(e.GetPosition(_field));
			}
		};
		_field.MouseLeftButtonUp += (object _, MouseButtonEventArgs _) =>
		{
			_field.ReleaseMouseCapture();
		};
		_field.SizeChanged += (object _, SizeChangedEventArgs _) =>
		{
			PlaceMarker();
		};
		PreviewKeyDown += (object _, KeyEventArgs e) =>
		{
			if (e.Key == Key.Escape)
			{
				Close();
				e.Handled = true;
			}
		};
		Color color = Color.FromRgb(37, 44, 55);
		if (TryHex(current, out var color2))
		{
			color = color2;
		}
		SetColor(color, updateHsv: true);
	}

	private static TextBox Input(Window? host)
	{
		TextBox textBox = new TextBox();
		if (host?.TryFindResource("SInput") is Style style)
		{
			textBox.Style = style;
		}
		return textBox;
	}

	private void UseTextStyle(TextBlock block, string key)
	{
		if (TryFindResource(key) is Style style)
		{
			block.ClearValue(TextBlock.FontSizeProperty);
			block.ClearValue(TextBlock.FontWeightProperty);
			block.ClearValue(TextBlock.FontFamilyProperty);
			block.ClearValue(TextBlock.ForegroundProperty);
			block.Style = style;
		}
	}

	private void UseButtonStyle(Button button, bool primary)
	{
		if (TryFindResource("SheetButton") is Style style)
		{
			DependencyProperty[] entries = new DependencyProperty[7]
			{
				Control.TemplateProperty,
				Control.BackgroundProperty,
				Control.ForegroundProperty,
				Control.FontSizeProperty,
				Control.FontFamilyProperty,
				Control.PaddingProperty,
				Control.BorderThicknessProperty
			};
			foreach (DependencyProperty dp in entries)
			{
				button.ClearValue(dp);
			}
			button.Style = style;
			if (primary)
			{
				button.SetResourceReference(Control.BackgroundProperty, "AccentBrush");
			}
		}
	}

	private void Pick(Point point)
	{
		_saturation = Math.Clamp(point.X / Math.Max(1.0, _field.ActualWidth), 0.0, 1.0);
		_brightness = 1.0 - Math.Clamp(point.Y / Math.Max(1.0, _field.ActualHeight), 0.0, 1.0);
		SetColor(HsvToColor(_hue.Value, _saturation, _brightness), updateHsv: false);
	}

	private void SetColor(Color color, bool updateHsv, TextBox? editing = null)
	{
		_syncing = true;
		PreviewColor = color;
		if (updateHsv)
		{
			double pageNumber = (double)(int)color.R / 255.0;
			double settingIndex = (double)(int)color.G / 255.0;
			double targetPage = (double)(int)color.B / 255.0;
			double num4 = Math.Max(pageNumber, Math.Max(settingIndex, targetPage));
			double num5 = Math.Min(pageNumber, Math.Min(settingIndex, targetPage));
			double num6 = num4 - num5;
			_brightness = num4;
			_saturation = ((num4 == 0.0) ? 0.0 : (num6 / num4));
			if (num6 > 0.0)
			{
				_hue.Value = (((num4 == pageNumber) ? ((settingIndex - targetPage) / num6) : ((num4 == settingIndex) ? (2.0 + (targetPage - pageNumber) / num6) : (4.0 + (pageNumber - settingIndex) / num6))) * 60.0 + 360.0) % 360.0;
			}
		}
		_hueBase.Background = new SolidColorBrush(HsvToColor(_hue.Value, 1.0, 1.0));
		_preview.Background = new SolidColorBrush(color);
		if (editing != _hex)
		{
			_hex.Text = Hex(color);
		}
		byte[] entries = new byte[3] { color.R, color.G, color.B };
		for (int i = 0; i < 3; i++)
		{
			if (editing != _rgb[i])
			{
				_rgb[i].Text = entries[i].ToString(CultureInfo.InvariantCulture);
			}
		}
		_error.Text = "";
		_accept.IsEnabled = true;
		PlaceMarker();
		_syncing = false;
	}

	private void PlaceMarker()
	{
		Canvas.SetLeft(_marker, _saturation * Math.Max(0.0, _field.ActualWidth - 12.0));
		Canvas.SetTop(_marker, (1.0 - _brightness) * Math.Max(0.0, _field.ActualHeight - 12.0));
	}

	private void ReadHex()
	{
		if (!_syncing)
		{
			if (TryHex(_hex.Text, out var color))
			{
				SetColor(color, updateHsv: true, _hex);
				return;
			}
			_error.Text = "请输入 6 位 HEX 颜色，例如 #18243B。";
			_accept.IsEnabled = false;
		}
	}

	private void ReadRgb()
	{
		if (_syncing)
		{
			return;
		}
		byte[] entries = new byte[3];
		for (int i = 0; i < 3; i++)
		{
			if (!byte.TryParse(_rgb[i].Text, NumberStyles.None, CultureInfo.InvariantCulture, out entries[i]))
			{
				_error.Text = "RGB 数值范围为 0–255。";
				_accept.IsEnabled = false;
				return;
			}
		}
		SetColor(Color.FromRgb(entries[0], entries[1], entries[2]), updateHsv: true, Keyboard.FocusedElement as TextBox);
	}

	private static string Hex(Color color)
	{
		return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
	}

	private static bool TryHex(string value, out Color color)
	{
		color = default;
		string text = value.Trim().TrimStart('#');
		if (text.Length != 6 || !uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
		{
			return false;
		}
		color = Color.FromRgb((byte)(result >> 16), (byte)(result >> 8), (byte)result);
		return true;
	}

	public static Color HsvToColor(double hue, double saturation, double brightness)
	{
		double pageNumber = (hue % 360.0 + 360.0) % 360.0 / 60.0;
		double settingIndex = brightness * saturation;
		double targetPage = settingIndex * (1.0 - Math.Abs(pageNumber % 2.0 - 1.0));
		double num4 = brightness - settingIndex;
		var (num5, num6, num7) = ((pageNumber < 1.0) ? (settingIndex, targetPage, 0.0) : ((pageNumber < 2.0) ? (targetPage, settingIndex, 0.0) : ((pageNumber < 3.0) ? (0.0, settingIndex, targetPage) : ((pageNumber < 4.0) ? (0.0, targetPage, settingIndex) : ((!(pageNumber < 5.0)) ? (settingIndex, 0.0, targetPage) : (targetPage, 0.0, settingIndex))))));
		return Color.FromRgb((byte)Math.Round((num5 + num4) * 255.0), (byte)Math.Round((num6 + num4) * 255.0), (byte)Math.Round((num7 + num4) * 255.0));
	}
}
