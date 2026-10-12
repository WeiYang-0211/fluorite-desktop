using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Launchpad.Services.Update;

namespace Launchpad.Views;

public sealed class UpdateDialog : Window
{
	private enum Mode
	{
		ReadyToDownload,
		Downloading,
		ReadyToInstall,
		Failed
	}

	private readonly UpdateService _service;

	private readonly UpdateInfo _info;

	private readonly Border _track;

	private readonly Border _fill;

	private readonly TextBlock _status;

	private readonly Button _later;

	private readonly Button _primary;

	private CancellationTokenSource? _cts;

	private string? _packagePath;

	private bool _busy;

	private bool _closed;

	private double _fraction;

	public UpdateDialog(UpdateInfo info, UpdateService service)
	{
		_info = info;
		_service = service;
		Window window = DialogKit.OwnerOrDefault(null, out var centerScreen);
		DialogKit.Configure(this, centerScreen);
		if (window != null)
		{
			Owner = window;
		}
		Title = "Fluorite 更新";
		StackPanel stackPanel = new StackPanel();
		Grid grid = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 22.0)
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid.Children.Add(new Image
		{
			Width = 40.0,
			Height = 40.0,
			Margin = new Thickness(0.0, 0.0, 14.0, 0.0),
			Source = new BitmapImage(new Uri("pack://application:,,,/Fluorite;component/Assets/logo-ui.png"))
		});
		StackPanel stackPanel2 = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		stackPanel2.Children.Add(DialogKit.Title("软件更新", 18.0));
		stackPanel2.Children.Add(DialogKit.Body("Fluorite 有新版本可用", 12.0, dim: true, new Thickness(0.0)));
		Grid.SetColumn(stackPanel2, 1);
		grid.Children.Add(stackPanel2);
		Button close = DialogKit.Push("×", primary: false);
		close.ToolTip = "稍后提醒";
		close.VerticalAlignment = VerticalAlignment.Top;
		close.Width = 30.0;
		close.Height = 30.0;
		close.FontSize = 16.0;
		close.Padding = new Thickness(0.0);
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(15.0));
		frameworkElementFactory.SetValue(Border.BackgroundProperty, new DynamicResourceExtension("T.Rp_14"));
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory2.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		frameworkElementFactory2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		close.Template = new ControlTemplate(typeof(Button))
		{
			VisualTree = frameworkElementFactory
		};
		close.MouseEnter += (object _, MouseEventArgs _) =>
		{
			close.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.7, TimeSpan.FromMilliseconds(100.0)));
		};
		close.MouseLeave += (object _, MouseEventArgs _) =>
		{
			close.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(100.0)));
		};
		close.Click += async (object _, RoutedEventArgs _) =>
		{
			if (_busy)
			{
				CancelDownload();
				Close();
			}
			else
			{
				await OnLaterAsync();
			}
		};
		Grid.SetColumn(close, 2);
		grid.Children.Add(close);
		stackPanel.Children.Add(grid);
		Grid grid2 = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 14.0)
		};
		grid2.ColumnDefinitions.Add(new ColumnDefinition());
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.Children.Add(DialogKit.Body("当前 v" + service.CurrentVersion + "   →   v" + info.Version, 13.0, dim: false, new Thickness(0.0, 3.0, 0.0, 3.0)));
		Border border = new Border
		{
			CornerRadius = new CornerRadius(7.0),
			Padding = new Thickness(10.0, 4.0, 10.0, 4.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		border.SetResourceReference(Border.BackgroundProperty, "AccentDimBrush");
		border.Child = DialogKit.Value("新版可用", 11.0);
		((TextBlock)border.Child).Margin = new Thickness(0.0);
		Grid.SetColumn(border, 1);
		grid2.Children.Add(border);
		stackPanel.Children.Add(grid2);
		Border border2 = new Border
		{
			CornerRadius = new CornerRadius(14.0),
			Padding = new Thickness(16.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0),
			BorderThickness = new Thickness(1.0)
		};
		border2.SetResourceReference(Border.BackgroundProperty, "T.PresetCard");
		border2.SetResourceReference(Border.BorderBrushProperty, "T.Fg_14");
		StackPanel stackPanel3 = new StackPanel
		{
			Children = { (UIElement)DialogKit.Title("本次更新", 13.0) }
		};
		ScrollViewer scrollViewer = new ScrollViewer
		{
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
			MaxHeight = Math.Clamp(SystemParameters.WorkArea.Height - 370.0, 64.0, 210.0),
			Content = DialogKit.Body(string.IsNullOrWhiteSpace(info.Notes) ? "体验优化与问题修复。" : info.Notes.Trim(), 12.5, dim: false, new Thickness(0.0))
		};
		if (TryFindResource("DialogScrollBar") is Style value)
		{
			scrollViewer.Resources.Add(typeof(ScrollBar), value);
		}
		stackPanel3.Children.Add(scrollViewer);
		border2.Child = stackPanel3;
		stackPanel.Children.Add(border2);
		stackPanel.Children.Add(DialogKit.Body((info.SizeBytes > 0) ? ("安装包 " + Human(info.SizeBytes) + " · 安装后自动重启") : "安装后自动重启 Fluorite", 11.5));
		_track = new Border
		{
			Height = 6.0,
			CornerRadius = new CornerRadius(3.0),
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0),
			Visibility = Visibility.Collapsed
		};
		_track.SetResourceReference(Border.BackgroundProperty, "T.Rp_14");
		_fill = new Border
		{
			Height = 6.0,
			CornerRadius = new CornerRadius(3.0),
			HorizontalAlignment = HorizontalAlignment.Left,
			Width = 0.0
		};
		_fill.SetResourceReference(Border.BackgroundProperty, "SliderFillBrush");
		_track.Child = _fill;
		_track.SizeChanged += (object s, SizeChangedEventArgs e) =>
		{
			ApplyFraction(_fraction);
		};
		stackPanel.Children.Add(_track);
		_status = DialogKit.Body(string.Empty, 12.5, dim: true, new Thickness(0.0, 9.0, 0.0, 0.0));
		_status.Visibility = Visibility.Collapsed;
		stackPanel.Children.Add(_status);
		_later = DialogKit.Push("稍后提醒", primary: false);
		_later.Click += async (object s, RoutedEventArgs e) =>
		{
			await OnLaterAsync();
		};
		_primary = DialogKit.Push("下载更新", primary: true);
		_primary.Foreground = Brushes.White;
		_primary.IsDefault = true;
		_primary.Click += (object s, RoutedEventArgs e) =>
		{
			OnPrimaryClick();
		};
		stackPanel.Children.Add(DialogKit.Buttons(_later, _primary));
		Border border3 = DialogKit.Card(Math.Min(440.0, SystemParameters.WorkArea.Width - 32.0));
		border3.SetResourceReference(Border.BackgroundProperty, "T.Chip_F2");
		border3.SetResourceReference(Border.BorderBrushProperty, "T.Fg_14");
		UseLayoutRounding = true;
		Loaded += (object _, RoutedEventArgs _) =>
		{
			BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(150.0)));
		};
		border3.Child = stackPanel;
		Content = border3;
		Closed += (object? _, EventArgs _) =>
		{
			_closed = true;
			CancelDownload();
		};
		PreviewKeyDown += async (object s, KeyEventArgs e) =>
		{
			if (e.Key == Key.Escape)
			{
				e.Handled = true;
				if (_busy)
				{
					CancelDownload();
				}
				else
				{
					await OnLaterAsync();
				}
			}
		};
		if (info.AlreadyDownloaded)
		{
			_packagePath = info.LocalPath;
			Apply(Mode.ReadyToInstall, "更新包已就绪，安装后自动重启 Fluorite");
		}
		else
		{
			Apply(Mode.ReadyToDownload, string.Empty);
		}
	}

	private void ApplyFraction(double value)
	{
		_fraction = Math.Clamp(value, 0.0, 1.0);
		if (_track.ActualWidth > 0.0)
		{
			_fill.Width = Math.Max(0.0, _track.ActualWidth * _fraction);
		}
	}

	private void Apply(Mode mode, string message)
	{
		_busy = mode == Mode.Downloading;
		switch (mode)
		{
		case Mode.Downloading:
			_track.Visibility = Visibility.Visible;
			ApplyFraction(0.0);
			_primary.Content = "下载中";
			_primary.IsEnabled = false;
			_later.Content = "取消下载";
			_later.IsEnabled = true;
			break;
		case Mode.ReadyToInstall:
			_track.Visibility = Visibility.Visible;
			ApplyFraction(1.0);
			_primary.Content = "安装并重启";
			_primary.IsEnabled = true;
			_later.Content = "稍后提醒";
			_later.IsEnabled = true;
			break;
		case Mode.Failed:
			_track.Visibility = Visibility.Collapsed;
			_primary.Content = "重试";
			_primary.IsEnabled = true;
			_later.Content = "稍后提醒";
			_later.IsEnabled = true;
			break;
		default:
			_track.Visibility = Visibility.Collapsed;
			_primary.Content = "下载更新";
			_primary.IsEnabled = true;
			_later.Content = "稍后提醒";
			_later.IsEnabled = true;
			break;
		}
		if (string.IsNullOrEmpty(message))
		{
			_status.Visibility = Visibility.Collapsed;
			return;
		}
		_status.Text = message;
		_status.Visibility = Visibility.Visible;
		_status.SetResourceReference(TextBlock.ForegroundProperty, (mode == Mode.Failed) ? "T.Danger_FF" : "SheetDimBrush");
	}

	private void OnPrimaryClick()
	{
		if (!_busy)
		{
			if (_packagePath != null)
			{
				Install(_packagePath);
			}
			else
			{
				DownloadAsync();
			}
		}
	}

	private async Task DownloadAsync()
	{
		_cts = new CancellationTokenSource();
		Apply(Mode.Downloading, "正在下载更新包");
		Progress<DownloadProgress> progress = new Progress<DownloadProgress>((DownloadProgress p) =>
		{
			if (!_closed && _busy)
			{
				if (p.Percent >= 0)
				{
					_track.Visibility = Visibility.Visible;
					ApplyFraction((double)p.Percent / 100.0);
				}
				_status.Text = p.Line;
				_status.Visibility = Visibility.Visible;
				_status.SetResourceReference(TextBlock.ForegroundProperty, "SheetDimBrush");
			}
		});
		try
		{
			_packagePath = await _service.DownloadAsync(_info, progress, _cts.Token);
			if (!_closed)
			{
				Apply(Mode.ReadyToInstall, "下载完成，校验通过。安装后自动重启 Fluorite");
			}
		}
		catch (OperationCanceledException)
		{
			App.Log("[UPDATE] download of " + _info.Version + " cancelled by the user");
			if (!_closed)
			{
				Apply(Mode.ReadyToDownload, "已取消下载");
			}
		}
		catch (Exception ex2)
		{
			App.Log($"[UPDATE] download of {_info.Version} failed: {ex2}");
			string message = ((ex2 is InvalidOperationException) ? ex2.Message : "下载失败，请稍后重试或查看日志");
			if (!_closed)
			{
				Apply(Mode.Failed, message);
			}
		}
		finally
		{
			_cts.Dispose();
			_cts = null;
		}
	}

	private void CancelDownload()
	{
		_cts?.Cancel();
	}

	private void Install(string path)
	{
		try
		{
			_service.InstallAndExit(path);
		}
		catch (Exception value)
		{
			App.Log($"[UPDATE] install could not start: {value}");
			Apply(Mode.Failed, "无法启动安装程序，详见日志");
			return;
		}
		Application.Current.Shutdown();
	}

	private async Task OnLaterAsync()
	{
		if (_busy)
		{
			CancelDownload();
			return;
		}
		await _service.SnoozeAsync(_info.Version);
		Close();
	}

	private static string Human(double bytes)
	{
		string[] entries = new string[4] { "B", "KB", "MB", "GB" };
		int pageNumber = 0;
		while (bytes >= 1024.0 && pageNumber < entries.Length - 1)
		{
			bytes /= 1024.0;
			pageNumber++;
		}
		return bytes.ToString("0.#", CultureInfo.InvariantCulture) + " " + entries[pageNumber];
	}
}
