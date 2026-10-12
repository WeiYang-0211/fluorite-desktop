using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Launchpad.Infrastructure;

namespace Launchpad.Views;

internal sealed class StartupLoadingWindow : Window
{
	private readonly TextBlock _status;

	private readonly TranslateTransform _flow = new TranslateTransform();

	private Task? _dismissal;

	private TaskCompletionSource? _dismissed;

	public StartupLoadingWindow()
	{
		Width = 360.0;
		Height = 178.0;
		WindowStyle = WindowStyle.None;
		ResizeMode = ResizeMode.NoResize;
		AllowsTransparency = true;
		Background = Brushes.Transparent;
		ShowInTaskbar = false;
		ShowActivated = false;
		Topmost = true;
		UseLayoutRounding = true;
		WindowStartupLocation = WindowStartupLocation.Manual;
		Border card = new Border
		{
			CornerRadius = new CornerRadius(22.0),
			Margin = new Thickness(14.0),
			BorderThickness = new Thickness(1.0),
			Effect = new DropShadowEffect
			{
				Color = Colors.Black,
				BlurRadius = 22.0,
				ShadowDepth = 4.0,
				Opacity = 0.18
			}
		};
		card.SetResourceReference(Border.BackgroundProperty, "T.Sheet_FF");
		card.SetResourceReference(Border.BorderBrushProperty, "T.Fg_14");
		Grid grid = new Grid
		{
			RowDefinitions = 
			{
				new RowDefinition(),
				new RowDefinition
				{
					Height = new GridLength(3.0)
				}
			}
		};
		Grid grid2 = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 18.0)
		};
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(56.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition());
		grid2.Children.Add(new Image
		{
			Source = new BitmapImage(new Uri("pack://application:,,,/Fluorite;component/Assets/logo-ui.png")),
			Width = 42.0,
			Height = 42.0,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Center
		});
		StackPanel stackPanel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock textBlock = new TextBlock
		{
			Text = "Fluorite",
			FontSize = 20.0,
			FontWeight = FontWeights.SemiBold
		};
		textBlock.SetResourceReference(TextBlock.ForegroundProperty, "SheetTextBrush");
		stackPanel.Children.Add(textBlock);
		_status = new TextBlock
		{
			Text = "正在准备你的桌面",
			FontSize = 12.0,
			Margin = new Thickness(0.0, 6.0, 0.0, 0.0)
		};
		_status.SetResourceReference(TextBlock.ForegroundProperty, "T.Fg_80");
		stackPanel.Children.Add(_status);
		Grid.SetColumn(stackPanel, 1);
		grid2.Children.Add(stackPanel);
		grid.Children.Add(grid2);
		Border border = new Border
		{
			CornerRadius = new CornerRadius(1.5),
			ClipToBounds = true
		};
		border.SetResourceReference(Border.BackgroundProperty, "T.Fg_14");
		Border child = new Border
		{
			Width = 110.0,
			CornerRadius = new CornerRadius(1.5),
			HorizontalAlignment = HorizontalAlignment.Left,
			RenderTransform = _flow,
			Background = new LinearGradientBrush(new GradientStopCollection
			{
				new GradientStop(Color.FromArgb(0, 10, 132, byte.MaxValue), 0.0),
				new GradientStop(Color.FromRgb(10, 132, byte.MaxValue), 0.5),
				new GradientStop(Color.FromArgb(0, 106, 163, byte.MaxValue), 1.0)
			}, new Point(0.0, 0.0), new Point(1.0, 0.0))
		};
		border.Child = child;
		Grid.SetRow(border, 1);
		grid.Children.Add(border);
		grid.Margin = new Thickness(24.0, 22.0, 24.0, 20.0);
		Grid grid3 = new Grid();
		Border tint = new Border
		{
			CornerRadius = new CornerRadius(21.0),
			Opacity = 0.72
		};
		tint.SetResourceReference(Border.BackgroundProperty, "T.Sheet_FF");
		grid3.Children.Add(tint);
		grid3.Children.Add(grid);
		card.Child = grid3;
		Content = card;
		SourceInitialized += (object? _, EventArgs _) =>
		{
			MonitorBoundsInfo currentMonitorBounds = ScreenHelper.GetCurrentMonitorBounds(this, followCursor: false);
			Left = currentMonitorBounds.LogicalLeft + (currentMonitorBounds.LogicalWidth - Width) / 2.0;
			Top = currentMonitorBounds.LogicalTop + (currentMonitorBounds.LogicalHeight - Height) / 2.0;
			if (SystemParameters.HighContrast)
			{
				tint.Opacity = 1.0;
			}
			else
			{
				BitmapSource bitmapSource = BackdropHelper.CaptureScreen(new MonitorBoundsInfo
				{
					PhysicalLeft = (int)Math.Round(Left * currentMonitorBounds.DpiScaleX),
					PhysicalTop = (int)Math.Round(Top * currentMonitorBounds.DpiScaleY),
					PhysicalWidth = (int)Math.Round(Width * currentMonitorBounds.DpiScaleX),
					PhysicalHeight = (int)Math.Round(Height * currentMonitorBounds.DpiScaleY)
				}, 0);
				if (bitmapSource == null)
				{
					tint.Opacity = 1.0;
				}
				else
				{
					BitmapSource bitmapSource2 = BackdropHelper.BuildFrost(bitmapSource, 2, 8);
					if (bitmapSource2 == null)
					{
						tint.Opacity = 1.0;
					}
					else
					{
						card.Background = new ImageBrush(bitmapSource2)
						{
							Stretch = Stretch.Fill,
							ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
							Viewbox = new Rect(14.0 * currentMonitorBounds.DpiScaleX / (double)bitmapSource.PixelWidth, 14.0 * currentMonitorBounds.DpiScaleY / (double)bitmapSource.PixelHeight, (Width - 28.0) * currentMonitorBounds.DpiScaleX / (double)bitmapSource.PixelWidth, (Height - 28.0) * currentMonitorBounds.DpiScaleY / (double)bitmapSource.PixelHeight)
						};
					}
				}
			}
		};
		Loaded += (object _, RoutedEventArgs _) =>
		{
			BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(160.0)));
			_flow.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-110.0, 284.0, TimeSpan.FromSeconds(1.5))
			{
				RepeatBehavior = RepeatBehavior.Forever,
				EasingFunction = new SineEase
				{
					EasingMode = EasingMode.EaseInOut
				}
			});
		};
		Closed += (object? _, EventArgs _) =>
		{
			_flow.BeginAnimation(TranslateTransform.XProperty, null);
			_dismissed?.TrySetResult();
		};
	}

	public void SetStatus(string text)
	{
		_status.Text = text.TrimEnd('…', '.');
	}

	public Task DismissAsync()
	{
		if (_dismissal != null)
		{
			return _dismissal;
		}
		if (!IsVisible)
		{
			return Task.CompletedTask;
		}
		_dismissed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_dismissal = _dismissed.Task;
		DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(120.0));
		doubleAnimation.Completed += (object? _, EventArgs _) =>
		{
			Close();
		};
		BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
		return _dismissal;
	}
}
