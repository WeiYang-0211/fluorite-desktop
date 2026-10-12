using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Launchpad.Views;

public static class DialogKit
{
	public const double CardWidth = 420.0;

	public static void Configure(Window window, bool centerScreen)
	{
		window.WindowStyle = WindowStyle.None;
		window.AllowsTransparency = true;
		window.Background = Brushes.Transparent;
		window.ResizeMode = ResizeMode.NoResize;
		window.ShowInTaskbar = false;
		window.SizeToContent = SizeToContent.WidthAndHeight;
		window.Topmost = true;
		window.ShowActivated = true;
		window.WindowStartupLocation = (centerScreen ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner);
	}

	public static Border Card(double width = 420.0)
	{
		Border border = new Border();
		border.Width = width;
		border.CornerRadius = new CornerRadius(20.0);
		border.BorderThickness = new Thickness(1.0);
		border.Padding = new Thickness(24.0, 22.0, 24.0, 18.0);
		border.SetResourceReference(Border.BackgroundProperty, "T.Sheet_FF");
		border.SetResourceReference(Border.BorderBrushProperty, "T.Fg_26");
		return border;
	}

	public static TextBlock Title(string text, double size = 14.0)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontSize = size;
		textBlock.FontWeight = FontWeights.SemiBold;
		textBlock.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.SetResourceReference(TextBlock.ForegroundProperty, "SheetTextBrush");
		textBlock.SetResourceReference(TextBlock.FontFamilyProperty, "AppFont");
		return textBlock;
	}

	public static TextBlock Body(string text, double size = 12.5, bool dim = true, Thickness? margin = null)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontSize = size;
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.LineHeight = ((size >= 14.0) ? 19 : 17);
		textBlock.Margin = margin ?? new Thickness(0.0, 0.0, 0.0, 6.0);
		textBlock.SetResourceReference(TextBlock.ForegroundProperty, dim ? "SheetDimBrush" : "SheetTextBrush");
		textBlock.SetResourceReference(TextBlock.FontFamilyProperty, "AppFont");
		return textBlock;
	}

	public static TextBlock Value(string text, double size = 13.0)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontSize = size;
		textBlock.FontWeight = FontWeights.SemiBold;
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.Margin = new Thickness(0.0, 2.0, 0.0, 8.0);
		textBlock.SetResourceReference(TextBlock.ForegroundProperty, "T.AccText_FF");
		textBlock.SetResourceReference(TextBlock.FontFamilyProperty, "AppFont");
		return textBlock;
	}

	public static Button Push(string text, bool primary, bool destructive = false)
	{
		Button button = new Button
		{
			Content = text,
			FontSize = 12.0,
			FontWeight = FontWeights.Medium,
			Padding = new Thickness(14.0, 6.0, 14.0, 6.0),
			Cursor = Cursors.Hand,
			Focusable = false,
			BorderThickness = new Thickness(0.0),
			Margin = new Thickness(9.0, 0.0, 0.0, 0.0)
		};
		if (destructive)
		{
			button.SetResourceReference(Control.ForegroundProperty, "T.Danger_FF");
			button.Background = new SolidColorBrush(Color.FromArgb(20, byte.MaxValue, 69, 58));
			button.BorderBrush = new SolidColorBrush(Color.FromArgb(43, byte.MaxValue, 69, 58));
			button.BorderThickness = new Thickness(1.0);
		}
		else
		{
			if (primary)
			{
				button.Foreground = Brushes.White;
			}
			else
			{
				button.SetResourceReference(Control.ForegroundProperty, "T.Fg_F5");
			}
			button.SetResourceReference(Control.BackgroundProperty, primary ? "AccentBrush" : "T.Rp_14");
		}
		button.Template = SimpleTemplate();
		button.SetResourceReference(Control.FontFamilyProperty, "AppFont");
		return button;
	}

	public static StackPanel Buttons(params Button[] items)
	{
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 18.0, 0.0, 0.0)
		};
		foreach (Button element in items)
		{
			stackPanel.Children.Add(element);
		}
		return stackPanel;
	}

	private static ControlTemplate SimpleTemplate()
	{
		ControlTemplate controlTemplate = new ControlTemplate(typeof(Button));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6.0));
		frameworkElementFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
		frameworkElementFactory.SetBinding(Border.BorderBrushProperty, Templated("BorderBrush", typeof(Border)));
		frameworkElementFactory.SetBinding(Border.BorderThicknessProperty, Templated("BorderThickness", typeof(Border)));
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(Grid));
		FrameworkElementFactory frameworkElementFactory3 = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory3.SetValue(Border.CornerRadiusProperty, new CornerRadius(6.0));
		frameworkElementFactory3.SetBinding(Border.BackgroundProperty, Templated("Background", typeof(Border)));
		FrameworkElementFactory frameworkElementFactory4 = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory4.Name = "Lift";
		frameworkElementFactory4.SetValue(Border.CornerRadiusProperty, new CornerRadius(6.0));
		frameworkElementFactory4.SetValue(UIElement.OpacityProperty, 0.0);
		frameworkElementFactory4.SetValue(Border.BackgroundProperty, new DynamicResourceExtension("T.Rp_1F"));
		FrameworkElementFactory frameworkElementFactory5 = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory5.SetBinding(FrameworkElement.MarginProperty, Templated("Padding", typeof(ContentPresenter)));
		frameworkElementFactory5.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		frameworkElementFactory5.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory2.AppendChild(frameworkElementFactory3);
		frameworkElementFactory2.AppendChild(frameworkElementFactory4);
		frameworkElementFactory2.AppendChild(frameworkElementFactory5);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		controlTemplate.VisualTree = frameworkElementFactory;
		Trigger trigger = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		trigger.EnterActions.Add(FadeTo("Lift", 1.0, 110));
		trigger.ExitActions.Add(FadeTo("Lift", 0.0, 150));
		controlTemplate.Triggers.Add(trigger);
		return controlTemplate;
	}

	private static Binding Templated(string path, Type target)
	{
		return new Binding(path)
		{
			RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
		};
	}

	private static TriggerAction FadeTo(string name, double to, int ms)
	{
		DoubleAnimation doubleAnimation = new DoubleAnimation
		{
			To = to,
			Duration = TimeSpan.FromMilliseconds(ms)
		};
		Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath(UIElement.OpacityProperty));
		Storyboard.SetTargetName(doubleAnimation, name);
		return new BeginStoryboard
		{
			Storyboard = new Storyboard
			{
				Children = { (Timeline)doubleAnimation }
			}
		};
	}

	public static Window? OwnerOrDefault(Window? preferred, out bool centerScreen)
	{
		Window window = preferred ?? Application.Current?.MainWindow;
		if (window == null || !window.IsLoaded)
		{
			centerScreen = true;
			return null;
		}
		centerScreen = false;
		return window;
	}
}
