using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Launchpad.Infrastructure;
using Launchpad.Interop;
using Launchpad.Models;
using Launchpad.Services;
using Launchpad.ViewModels;

namespace Launchpad.Views;

internal sealed class DesktopDockWindow : Window, IDisposable
{
	private delegate bool EnumWindowCallback(nint hwnd, nint parameter);

	private readonly MainViewModel _model;

	private DockWindowPreview? _windowPreview;

	private readonly Window? _launchpadWindow;

	private AppSettings _settings;

	private readonly DispatcherTimer _timer;

	private readonly HotCornerService _hotCorner;

	private readonly StackPanel _row = new StackPanel
	{
		Orientation = Orientation.Horizontal,
		VerticalAlignment = VerticalAlignment.Bottom
	};

	private readonly Border _plate;

	private readonly Grid _surface;

	private readonly TranslateTransform _revealShift = new TranslateTransform();

	private readonly ScaleTransform _revealScale = new ScaleTransform(1.0, 1.0);

	private bool _surfaceSuppressed;

	private bool _hiding;

	private int _revealVersion;

	private nint _handle;

	private bool _desktopWasForeground;

	private bool _disposed;

	private bool _cornerVisible;

	private bool _startupShown;

	private DateTime _cornerHideAt;

	private Point _press;

	private bool _dragging;

	private double? _hoverIndex;

	private double _liveIndex;

	private double _hoverStrength;

	private double _iconSize;

	private double _peak;

	private double _indexVelocity;

	private double _strengthVelocity;

	private double[] _magnificationExtras = Array.Empty<double>();

	private readonly ScaleTransform _plateScale = new ScaleTransform(1.0, 1.0);

	private bool _animating;

	private TimeSpan _lastFrame;

	private double _positionTarget = double.NaN;

	private bool LaunchpadVisible
	{
		get
		{
			if (!_model.IsOpen)
			{
				return _launchpadWindow?.IsVisible ?? false;
			}
			return true;
		}
	}

	private bool Enabled
	{
		get
		{
			if (_settings.EnableDock)
			{
				if (!_settings.ShowDesktopDock)
				{
					return _settings.EnableDesktopDockHotCorner;
				}
				return true;
			}
			return false;
		}
	}

	private bool StartupReady
	{
		get
		{
			if (!_startupShown)
			{
				if (_model.HasCompletedInitialization)
				{
					return _model.DockApps.All((AppItem a) => a.Icon != null);
				}
				return false;
			}
			return true;
		}
	}

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetClassName(nint hwnd, StringBuilder name, int count);

	[DllImport("user32.dll")]
	private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(nint hwnd);

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowCallback callback, nint parameter);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern nint FindWindowEx(nint parent, nint after, string className, string? title);

	[DllImport("user32.dll")]
	private static extern nint GetWindow(nint hwnd, uint command);

	public DesktopDockWindow(MainViewModel model, Window? launchpadWindow = null)
	{
		_model = model;
		_launchpadWindow = launchpadWindow;
		_settings = model.Settings;
		Title = "Fluorite Desktop Dock";
		WindowStyle = WindowStyle.None;
		ResizeMode = ResizeMode.NoResize;
		AllowsTransparency = true;
		Background = Brushes.Transparent;
		ShowInTaskbar = false;
		ShowActivated = false;
		Topmost = false;
		UseLayoutRounding = true;
		RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
		_plate = new Border
		{
			Background = new SolidColorBrush(Color.FromArgb(205, 75, 82, 90)),
			BorderBrush = new SolidColorBrush(Color.FromArgb(95, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(20.0),
			Padding = new Thickness(12.0, 8.0, 12.0, 8.0),
			VerticalAlignment = VerticalAlignment.Bottom
		};
		_plate.RenderTransformOrigin = new Point(0.5, 1.0);
		_plate.RenderTransform = _plateScale;
		Grid grid = (_surface = new Grid
		{
			Background = Brushes.Transparent
		});
		grid.RenderTransformOrigin = new Point(0.5, 1.0);
		grid.RenderTransform = new TransformGroup
		{
			Children = 
			{
				(Transform)_revealScale,
				(Transform)_revealShift
			}
		};
		grid.Children.Add(_plate);
		_row.Margin = new Thickness(12.0, 8.0, 12.0, 8.0);
		grid.Children.Add(_row);
		Content = grid;
		SourceInitialized += (object? _, EventArgs _) =>
		{
			_handle = new WindowInteropHelper(this).Handle;
			_model.SetDesktopDockDeviceScale(VisualTreeHelper.GetDpi(this).DpiScaleX);
			HwndSource.FromHwnd(_handle)?.AddHook(WindowMessage);
		};
		MouseMove += (object _, MouseEventArgs e) =>
		{
			Magnify(e.GetPosition(this).X);
		};
		MouseLeave += (object _, MouseEventArgs _) =>
		{
			Magnify(CursorInDock());
		};
		IsVisibleChanged += (object _, DependencyPropertyChangedEventArgs _) =>
		{
			if (!IsVisible)
			{
				ResetMagnification();
			}
		};
		_settings.PropertyChanged += SettingsChanged;
		_model.PropertyChanged += ModelChanged;
		_model.DockApps.CollectionChanged += DockChanged;
		_timer = new DispatcherTimer(DispatcherPriority.Background)
		{
			Interval = TimeSpan.FromMilliseconds(100.0)
		};
		_timer.Tick += Tick;
		_hotCorner = new HotCornerService(() => !LaunchpadVisible && _settings.EnableDock && _settings.EnableDesktopDockHotCorner, () => _settings.DesktopDockHotCorner, () => _settings.PauseHotCornerInFullscreen, () => _model.AllApps);
		_hotCorner.CornerTriggered += RevealFromCorner;
		if (_launchpadWindow != null)
		{
			_launchpadWindow.IsVisibleChanged += LaunchpadVisibilityChanged;
		}
		new WindowInteropHelper(this).EnsureHandle();
		Refresh();
	}

	private nint WindowMessage(nint hwnd, int msg, nint wp, nint lp, ref bool handled)
	{
		if (msg == 33)
		{
			handled = true;
			return new IntPtr(3);
		}
		return IntPtr.Zero;
	}

	private void SettingsChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "DesktopDockBottomOffset")
		{
			Position();
			return;
		}
		string propertyName = e.PropertyName;
		if ((propertyName == "DesktopDockBackgroundStyle" || propertyName == "DesktopDockBackgroundOpacity") ? true : false)
		{
			ApplyBackgroundStyle();
			return;
		}
		if (e.PropertyName == "EnableDesktopDockHotCorner" && !_settings.EnableDesktopDockHotCorner)
		{
			DismissCorner(animate: false);
		}
		bool validated;
		switch (e.PropertyName)
		{
		case "DockIconSpacing":
		case "ShowDesktopDock":
		case "DockIconSize":
		case "ReduceMotion":
		case "EnableDock":
		case "EnableDesktopDockHotCorner":
		case "EnableDockMagnification":
		case "DockMagnifyScale":
		case "ShowRunningIndicators":
			validated = true;
			break;
		default:
			validated = false;
			break;
		}
		if (validated)
		{
			Refresh();
		}
	}

	private void ModelChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "Settings")
		{
			_settings.PropertyChanged -= SettingsChanged;
			_settings = _model.Settings;
			_settings.PropertyChanged += SettingsChanged;
			Refresh();
		}
		else if (e.PropertyName == "IsOpen")
		{
			if (_model.IsOpen)
			{
				DismissCorner(animate: false);
			}
			Tick(null, EventArgs.Empty);
		}
	}

	private void DockChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		Refresh();
	}

	private void LaunchpadVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		Tick(null, EventArgs.Empty);
	}

	private void ApplyBackgroundStyle()
	{
		double strength = (double.IsFinite(_settings.DesktopDockBackgroundOpacity) ? (Math.Clamp(_settings.DesktopDockBackgroundOpacity, 0.0, 100.0) / 100.0) : 1.0);
		string desktopDockBackgroundStyle = _settings.DesktopDockBackgroundStyle;
		_plate.BorderThickness = new Thickness((!(desktopDockBackgroundStyle == "None")) ? 1 : 0);
		_plate.BorderBrush = new SolidColorBrush(Ink(95, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		Border plate = _plate;
		plate.Background = desktopDockBackgroundStyle switch
		{
			"None" => (Brush)Brushes.Transparent, 
			"Glass" => new LinearGradientBrush(Ink(100, byte.MaxValue, byte.MaxValue, byte.MaxValue), Ink(35, 210, 226, 245), 90.0), 
			"Light" => new SolidColorBrush(Ink(225, 245, 247, 250)), 
			"Dark" => new SolidColorBrush(Ink(230, 25, 29, 36)), 
			_ => new SolidColorBrush(Ink(205, 75, 82, 90)), 
		};
		Color Ink(byte alpha, byte r, byte g, byte b)
		{
			return Color.FromArgb((byte)Math.Round((double)(int)alpha * strength), r, g, b);
		}
	}

	private void Refresh()
	{
		if (_disposed)
		{
			return;
		}
		ApplyBackgroundStyle();
		StopMagnificationFrames();
		_hoverIndex = null;
		_hoverStrength = 0.0;
		_row.Children.Clear();
		Image image = new Image
		{
			Source = new BitmapImage(new Uri("pack://application:,,,/Fluorite;component/Assets/logo-ui.png")),
			Stretch = Stretch.Uniform
		};
		RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
		Grid grid = new Grid();
		grid.Children.Add(image);
		Button button = new Button
		{
			Tag = "Fluorite",
			Content = grid,
			ToolTip = "打开 Fluorite",
			Cursor = Cursors.Hand,
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Padding = new Thickness(0.0),
			Margin = new Thickness(5.0, 0.0, 5.0, 0.0),
			VerticalAlignment = VerticalAlignment.Bottom,
			Template = MakeButtonTemplate(),
			AllowDrop = false,
			FocusVisualStyle = null
		};
		button.Click += (object _, RoutedEventArgs _) =>
		{
			DismissCorner();
			_model.OpenLaunchpad();
		};
		button.DragOver += (object _, DragEventArgs e) =>
		{
			e.Effects = DragDropEffects.None;
			e.Handled = true;
		};
		_row.Children.Add(button);
		foreach (AppItem app in _model.DockApps)
		{
			Image image2 = new Image
			{
				Stretch = Stretch.Uniform
			};
			RenderOptions.SetBitmapScalingMode(image2, BitmapScalingMode.HighQuality);
			image2.SetBinding(Image.SourceProperty, new Binding("Icon")
			{
				Source = app
			});
			Grid grid2 = new Grid();
			grid2.Children.Add(image2);
			Border border = new Border
			{
				Width = 2.0,
				Height = 40.0,
				CornerRadius = new CornerRadius(1.0),
				Background = new SolidColorBrush(Color.FromArgb(210, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
				Effect = new DropShadowEffect
				{
					Color = Colors.Black,
					Opacity = 0.35,
					BlurRadius = 3.0,
					ShadowDepth = 0.0
				},
				IsHitTestVisible = false,
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(-5.0, 0.0, 0.0, 0.0)
			};
			border.SetBinding(UIElement.VisibilityProperty, new Binding("StartsOpenWindowSection")
			{
				Source = app,
				Converter = new BooleanToVisibilityConverter()
			});
			grid2.Children.Add(border);
			if (_settings.ShowRunningIndicators)
			{
				Ellipse ellipse = new Ellipse
				{
					Width = 4.0,
					Height = 4.0,
					Fill = Brushes.White,
					VerticalAlignment = VerticalAlignment.Bottom,
					HorizontalAlignment = HorizontalAlignment.Center,
					RenderTransformOrigin = new Point(0.5, 1.0),
					RenderTransform = new TransformGroup
					{
						Children = 
						{
							(Transform)new ScaleTransform(1.0, 1.0),
							(Transform)new TranslateTransform(0.0, 6.0)
						}
					}
				};
				ellipse.SetBinding(UIElement.VisibilityProperty, new Binding("IsRunning")
				{
					Source = app,
					Converter = new BooleanToVisibilityConverter()
				});
				grid2.Children.Add(ellipse);
			}
			Button button2 = new Button
			{
				Tag = app,
				Content = grid2,
				ToolTip = app.Name,
				Background = Brushes.Transparent,
				BorderThickness = new Thickness(0.0),
				Padding = new Thickness(0.0),
				Margin = new Thickness(5.0, 0.0, 5.0, 0.0),
				VerticalAlignment = VerticalAlignment.Bottom,
				Cursor = Cursors.Hand,
				AllowDrop = true,
				Template = MakeButtonTemplate(),
				FocusVisualStyle = null
			};
			button2.MouseEnter += (object _, MouseEventArgs _) =>
			{
				(_windowPreview ?? (_windowPreview = new DockWindowPreview(_model, this))).Hover(button2, app);
			};
			button2.Click += (object _, RoutedEventArgs _) =>
			{
				_windowPreview?.Close();
				if (!_dragging)
				{
					DismissCorner();
					_model.LaunchApp(app);
				}
			};
			button2.PreviewMouseRightButtonDown += (object _, MouseButtonEventArgs e) =>
			{
				if (app.IsOpenWindowEntry)
				{
					ContextMenu contextMenu = OpenWindowMenu.Create(_model, app, this);
					contextMenu.PlacementTarget = button2;
					button2.ContextMenu = contextMenu;
					contextMenu.IsOpen = true;
					e.Handled = true;
				}
			};
			button2.PreviewMouseLeftButtonDown += (object _, MouseButtonEventArgs e) =>
			{
				_press = e.GetPosition(this);
			};
			button2.PreviewMouseMove += (object _, MouseEventArgs e) =>
			{
				if (!_settings.IconsLocked && !app.IsOpenWindowEntry && e.LeftButton == MouseButtonState.Pressed && !_dragging)
				{
					Point position = e.GetPosition(this);
					if (!(Math.Abs(position.X - _press.X) < SystemParameters.MinimumHorizontalDragDistance) || !(Math.Abs(position.Y - _press.Y) < SystemParameters.MinimumVerticalDragDistance))
					{
						_dragging = true;
						try
						{
							DragDrop.DoDragDrop(button2, new DataObject("Fluorite.DesktopDock", app.Id), DragDropEffects.Move);
						}
						finally
						{
							_dragging = false;
							Magnify(null);
						}
					}
				}
			};
			button2.DragOver += (object _, DragEventArgs e) =>
			{
				e.Effects = ((!app.IsOpenWindowEntry && !_settings.IconsLocked && e.Data.GetDataPresent("Fluorite.DesktopDock")) ? DragDropEffects.Move : DragDropEffects.None);
				e.Handled = true;
			};
			button2.Drop += async (object _, DragEventArgs e) =>
			{
				if (_settings.IconsLocked || app.IsOpenWindowEntry)
				{
					e.Handled = true;
				}
				else
				{
					object data = e.Data.GetData("Fluorite.DesktopDock");
					string id = data as string;
					if (id != null)
					{
						e.Handled = true;
						AppItem appItem = _model.DockApps.FirstOrDefault((AppItem a) => a.Id == id);
						if (appItem != null && appItem != app)
						{
							await _model.AddToDockAsync(appItem, _model.DockApps.IndexOf(app), deferSave: true);
							await _model.SaveDockStateAsync();
						}
					}
				}
			};
			_row.Children.Add(button2);
		}
		ConfigureGeometry();
		foreach (Button child in _row.Children)
		{
			TransformGroup renderTransform = new TransformGroup
			{
				Children = 
				{
					(Transform)new ScaleTransform(1.0, 1.0),
					(Transform)new TranslateTransform()
				}
			};
			child.RenderTransformOrigin = new Point(0.5, 1.0);
			child.RenderTransform = renderTransform;
			child.UseLayoutRounding = false;
		}
		ResetMagnification();
		if (Enabled)
		{
			_timer.Start();
		}
		else
		{
			_timer.Stop();
		}
		Tick(null, EventArgs.Empty);
	}

	private static ControlTemplate MakeButtonTemplate()
	{
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
		frameworkElementFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch);
		return new ControlTemplate(typeof(Button))
		{
			VisualTree = frameworkElementFactory
		};
	}

	private double? CursorInDock()
	{
		if (!IsVisible || _surface.Visibility != Visibility.Visible || !NativeMethods.GetCursorPos(out var lpPoint))
		{
			return null;
		}
		Point point = PointFromScreen(new Point(lpPoint.X, lpPoint.Y));
		if (!(point.X >= 0.0) || !(point.X <= ActualWidth) || !(point.Y >= 0.0) || !(point.Y <= ActualHeight))
		{
			return null;
		}
		return point.X;
	}

	private void Magnify(double? x)
	{
		double? hoverIndex = _hoverIndex;
		_hoverIndex = null;
		if (x.HasValue && _peak > 1.0 && _row.Children.Count > 0)
		{
			double pageNumber = (x.Value - Width / 2.0) / (_iconSize + _settings.DockIconSpacing) + (double)(_row.Children.Count - 1) / 2.0;
			_hoverIndex = pageNumber;
			if (_hoverStrength < 0.001)
			{
				_liveIndex = pageNumber;
			}
		}
		if (_settings.ReduceMotion)
		{
			AdvanceMagnification(10.0);
		}
		else if (hoverIndex != _hoverIndex && !_animating && IsVisible)
		{
			_animating = true;
			_lastFrame = TimeSpan.Zero;
			CompositionTarget.Rendering += MagnificationFrame;
		}
	}

	private void ConfigureGeometry()
	{
		_peak = (_settings.EnableDockMagnification ? Math.Clamp(_settings.DockMagnifyScale, 1.0, 2.5) : 1.0);
		double dockIconSpacing = _settings.DockIconSpacing;
		for (int i = 0; i < _row.Children.Count; i++)
		{
			((Button)_row.Children[i]).Margin = new Thickness((i == 0) ? 0.0 : (dockIconSpacing / 2.0), 0.0, (i == _row.Children.Count - 1) ? 0.0 : (dockIconSpacing / 2.0), 0.0);
		}
		_iconSize = Math.Min(Math.Clamp(_settings.DockIconSize, 36.0, 84.0), Math.Max(16.0, (SystemParameters.WorkArea.Width - 42.0 + dockIconSpacing) / Math.Max(1.0, (double)_row.Children.Count + 3.3 * (_peak - 1.0)) - dockIconSpacing));
		double pageNumber = (double)_row.Children.Count * (_iconSize + dockIconSpacing) - dockIconSpacing;
		Width = Math.Min(SystemParameters.WorkArea.Width - 16.0, pageNumber + 3.3 * (_iconSize + dockIconSpacing) * (_peak - 1.0) + 26.0);
		Height = _iconSize * _peak + 26.0;
		_plate.Height = _iconSize + 18.0;
		Position();
	}

	private void MagnificationFrame(object? sender, EventArgs e)
	{
		TimeSpan renderingTime = ((RenderingEventArgs)e).RenderingTime;
		if (!(_lastFrame == renderingTime))
		{
			double dt = ((_lastFrame == TimeSpan.Zero) ? (1.0 / 60.0) : Math.Clamp((renderingTime - _lastFrame).TotalSeconds, 0.0, 0.05));
			_lastFrame = renderingTime;
			if (_hoverIndex.HasValue)
			{
				Magnify(CursorInDock());
			}
			if (AdvanceMagnification(dt) && !_hoverIndex.HasValue)
			{
				StopMagnificationFrames();
			}
		}
	}

	private bool AdvanceMagnification(double dt)
	{
		double pageNumber = (_hoverIndex.HasValue ? 1 : 0);
		if (_settings.ReduceMotion)
		{
			_hoverStrength = pageNumber;
			if (_hoverIndex.HasValue)
			{
				_liveIndex = _hoverIndex.Value;
			}
			_indexVelocity = (_strengthVelocity = 0.0);
		}
		else
		{
			_hoverStrength = DockMotion.Follow(_hoverStrength, pageNumber, ref _strengthVelocity, dt, 0.16);
			if (_hoverIndex.HasValue)
			{
				_liveIndex = DockMotion.Follow(_liveIndex, _hoverIndex.Value, ref _indexVelocity, dt, 0.045);
			}
		}
		bool result = Math.Abs(pageNumber - _hoverStrength) < 0.001 && (!_hoverIndex.HasValue || Math.Abs(_hoverIndex.Value - _liveIndex) < 0.001);
		int count = _row.Children.Count;
		double settingIndex = _iconSize + _settings.DockIconSpacing;
		double targetPage = settingIndex * (_peak - 1.0) * _hoverStrength;
		if (_magnificationExtras.Length != count)
		{
			_magnificationExtras = new double[count];
		}
		double[] magnificationExtras = _magnificationExtras;
		double num4 = 0.0;
		for (int i = 0; i < count; i++)
		{
			magnificationExtras[i] = DockMotion.Bump((double)i - _liveIndex) * targetPage;
			num4 += magnificationExtras[i];
		}
		double num5 = DockMotion.Normalize(num4, count, targetPage);
		double num6 = num4 * num5;
		double num7 = 0.0;
		for (int j = 0; j < count; j++)
		{
			Button button = (Button)_row.Children[j];
			double num8 = magnificationExtras[j] * num5;
			TransformGroup transformGroup = (TransformGroup)button.RenderTransform;
			ScaleTransform scaleTransform = (ScaleTransform)transformGroup.Children[0];
			TranslateTransform translateTransform = (TranslateTransform)transformGroup.Children[1];
			double scaleX = (scaleTransform.ScaleY = 1.0 + num8 / settingIndex);
			scaleTransform.ScaleX = scaleX;
			UpdateIndicator(button, scaleTransform.ScaleY);
			translateTransform.X = num7 - num6 / 2.0 + num8 / 2.0;
			num7 += num8;
		}
		_plateScale.ScaleX = 1.0 + num6 / ((double)_row.Children.Count * settingIndex - _settings.DockIconSpacing + 26.0);
		return result;
	}

	private void LayoutPlate()
	{
		double pageNumber = _row.Children.Cast<Button>().Sum((Button b) => b.Width + b.Margin.Left + b.Margin.Right);
		_row.HorizontalAlignment = HorizontalAlignment.Center;
		_plate.Width = Math.Min(Width, pageNumber + 26.0);
	}

	private void ResetMagnification()
	{
		StopMagnificationFrames();
		_hoverIndex = null;
		_hoverStrength = 0.0;
		_indexVelocity = (_strengthVelocity = 0.0);
		foreach (Button child in _row.Children)
		{
			double width = (child.Height = _iconSize);
			child.Width = width;
			TransformGroup transformGroup = (TransformGroup)child.RenderTransform;
			ScaleTransform scaleTransform = (ScaleTransform)transformGroup.Children[0];
			TranslateTransform translateTransform = (TranslateTransform)transformGroup.Children[1];
			width = (scaleTransform.ScaleY = 1.0);
			scaleTransform.ScaleX = width;
			UpdateIndicator(child, 1.0);
			width = (translateTransform.Y = 0.0);
			translateTransform.X = width;
		}
		_plateScale.ScaleX = 1.0;
		_plate.CornerRadius = new CornerRadius(20.0);
		LayoutPlate();
	}

	private void StopMagnificationFrames()
	{
		if (_animating)
		{
			CompositionTarget.Rendering -= MagnificationFrame;
			_animating = false;
			_lastFrame = TimeSpan.Zero;
		}
	}

	private static void UpdateIndicator(Button button, double scale)
	{
		if (!(button.Content is Grid grid))
		{
			return;
		}
		foreach (UIElement child in grid.Children)
		{
			if (child is Ellipse ellipse)
			{
				TransformGroup transformGroup = (TransformGroup)ellipse.RenderTransform;
				ScaleTransform scaleTransform = (ScaleTransform)transformGroup.Children[0];
				double scaleX = (scaleTransform.ScaleY = 1.0 / scale);
				scaleTransform.ScaleX = scaleX;
				((TranslateTransform)transformGroup.Children[1]).Y = 6.0 / scale;
			}
		}
	}

	private void Position()
	{
		Rect workArea = SystemParameters.WorkArea;
		Left = workArea.Left + (workArea.Width - Width) / 2.0;
		MonitorBoundsInfo currentMonitorBounds = ScreenHelper.GetCurrentMonitorBounds(this, followCursor: false);
		_model.SetDesktopDockDeviceScale(currentMonitorBounds.DpiScaleX);
		double pageNumber = Math.Max(workArea.Top, TaskbarLayout.GetDockBottom(currentMonitorBounds, workArea.Bottom, _settings.DesktopDockBottomOffset) - Height);
		if (double.IsNaN(_positionTarget) || !(Math.Abs(pageNumber - _positionTarget) < 0.5))
		{
			_positionTarget = pageNumber;
			double top = Top;
			BeginAnimation(Window.TopProperty, null);
			Top = pageNumber;
			if (IsVisible && !double.IsNaN(top) && !_settings.ReduceMotion && SystemParameters.ClientAreaAnimation)
			{
				BeginAnimation(Window.TopProperty, new DoubleAnimation(top, pageNumber, TimeSpan.FromMilliseconds(220.0))
				{
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				});
			}
		}
	}

	private void Tick(object? sender, EventArgs e)
	{
		if (_disposed)
		{
			return;
		}
		if (_surfaceSuppressed != LaunchpadVisible)
		{
			_surfaceSuppressed = LaunchpadVisible;
			if (_surfaceSuppressed)
			{
				AnimateReveal(0.0, 8.0, IsVisible, () =>
				{
					_surface.Visibility = Visibility.Hidden;
				});
			}
			else
			{
				_surface.Visibility = Visibility.Visible;
				_surface.UpdateLayout();
				AnimateReveal(1.0, 0.0, IsVisible, null);
			}
		}
		_surface.IsHitTestVisible = !LaunchpadVisible;
		if (!StartupReady)
		{
			return;
		}
		if (!Enabled)
		{
			_cornerVisible = false;
			HideDock(animate: false);
			_desktopWasForeground = false;
			return;
		}
		if (IsVisible)
		{
			Position();
		}
		if (_hiding)
		{
			return;
		}
		if (_cornerVisible)
		{
			UpdateCornerVisibility(DateTime.UtcNow, IsMouseOver);
			if (_cornerVisible || _hiding)
			{
				return;
			}
		}
		if (!_settings.ShowDesktopDock)
		{
			if (IsVisible)
			{
				HideDock(animate: false);
			}
			_desktopWasForeground = false;
			return;
		}
		if (!IsVisible)
		{
			Position();
			ShowDock(animate: true);
			PlaceAboveDesktop();
		}
		if (LaunchpadVisible)
		{
			if (!IsVisible)
			{
				Position();
				ShowDock(animate: false);
			}
			PlaceAboveDesktop();
			_desktopWasForeground = false;
			return;
		}
		nint foregroundWindow = NativeMethods.GetForegroundWindow();
		StringBuilder stringBuilder = new StringBuilder(128);
		GetClassName(foregroundWindow, stringBuilder, stringBuilder.Capacity);
		bool validated = foregroundWindow != IntPtr.Zero;
		if (validated)
		{
			bool flag2 = foregroundWindow == NativeMethods.GetShellWindow();
			if (!flag2)
			{
				string text = stringBuilder.ToString();
				bool flag3 = ((text == "WorkerW" || text == "Progman") ? true : false);
				flag2 = flag3;
			}
			validated = flag2;
		}
		bool flag4 = validated;
		bool flag5 = TaskbarLayout.IsTaskbarSurface(stringBuilder.ToString());
		if (flag5)
		{
			NativeMethods.GetWindowThreadProcessId(foregroundWindow, out var lpdwProcessId);
			NativeMethods.GetWindowThreadProcessId(NativeMethods.GetShellWindow(), out var lpdwProcessId2);
			flag5 = lpdwProcessId2 != 0 && lpdwProcessId == lpdwProcessId2;
		}
		if (!flag5)
		{
			if (flag4 && (!_desktopWasForeground || !IsVisible || !IsWindowVisible(_handle) || DesktopCoversDock(foregroundWindow)))
			{
				Position();
				ShowDock(!IsVisible);
				SetWindowPos(_handle, IntPtr.Zero, 0, 0, 0, 0, 83u);
			}
			else if (!flag4)
			{
				PlaceAboveDesktop();
			}
			_desktopWasForeground = flag4;
		}
	}

	private static bool IsAboveWindow(nint window, nint other)
	{
		if (window == IntPtr.Zero || other == IntPtr.Zero)
		{
			return false;
		}
		if (window == other)
		{
			return true;
		}
		nint pageNumber = GetWindow(other, 3u);
		int settingIndex = 0;
		while (pageNumber != IntPtr.Zero && settingIndex < 512)
		{
			if (pageNumber == window)
			{
				return true;
			}
			nint window2 = GetWindow(pageNumber, 3u);
			if (window2 == pageNumber)
			{
				break;
			}
			pageNumber = window2;
			settingIndex++;
		}
		return false;
	}

	private bool DesktopCoversDock(nint foreground)
	{
		if (!IsAboveWindow(_handle, foreground))
		{
			return true;
		}
		nint pageNumber = FindDesktopWindow();
		if (pageNumber != IntPtr.Zero)
		{
			return !IsAboveWindow(_handle, pageNumber);
		}
		return false;
	}

	internal static nint FindDesktopWindow()
	{
		nint desktop = IntPtr.Zero;
		EnumWindows((nint window, nint _) =>
		{
			if (IsWindowVisible(window) && FindWindowEx(window, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
			{
				desktop = window;
				return false;
			}
			return true;
		}, IntPtr.Zero);
		return desktop;
	}

	private void PlaceAboveDesktop()
	{
		PlaceWindowAboveDesktop(_handle);
	}

	internal static void PlaceWindowAboveDesktop(nint handle)
	{
		nint pageNumber = FindDesktopWindow();
		if (pageNumber != IntPtr.Zero)
		{
			nint window = GetWindow(pageNumber, 3u);
			if (window != handle)
			{
				SetWindowPos(handle, window, 0, 0, 0, 0, 19u);
			}
		}
	}

	internal void RevealFromCorner()
	{
		if (!_disposed && Enabled && !LaunchpadVisible && StartupReady)
		{
			_cornerVisible = true;
			_cornerHideAt = DateTime.UtcNow.AddSeconds(3.0);
			Position();
			ShowDock(animate: true, raise: true);
		}
	}

	internal void UpdateCornerVisibility(DateTime now, bool pointerInside)
	{
		if (!_cornerVisible)
		{
			return;
		}
		if (!pointerInside && !_dragging)
		{
			DockWindowPreview? windowPreview = _windowPreview;
			if (windowPreview == null || !windowPreview.IsPointerOver)
			{
				if (now >= _cornerHideAt)
				{
					DismissCorner();
				}
				return;
			}
		}
		_cornerHideAt = now.AddMilliseconds(700.0);
	}

	private void DismissCorner(bool animate = true)
	{
		if (_cornerVisible)
		{
			_cornerVisible = false;
			HideDock(animate);
			_desktopWasForeground = false;
		}
	}

	private void ShowDock(bool animate, bool raise = false)
	{
		_startupShown = true;
		bool validated = !IsVisible;
		_revealVersion++;
		double opacity = _surface.Opacity;
		double y = _revealShift.Y;
		double scaleX = _revealScale.ScaleX;
		_surface.BeginAnimation(UIElement.OpacityProperty, null);
		_revealShift.BeginAnimation(TranslateTransform.YProperty, null);
		_surface.Opacity = (validated ? 0.0 : opacity);
		_revealShift.Y = (validated ? 10.0 : y);
		_revealScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		_revealScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		ScaleTransform revealScale = _revealScale;
		double scaleX2 = (_revealScale.ScaleY = (validated ? 0.96 : scaleX));
		revealScale.ScaleX = scaleX2;
		_hiding = false;
		IsHitTestVisible = true;
		if (raise)
		{
			Topmost = true;
		}
		if (validated)
		{
			Show();
		}
		if (raise && _handle != IntPtr.Zero)
		{
			SetWindowPos(_handle, new IntPtr(-1), 0, 0, 0, 0, 83u);
		}
		AnimateReveal(1.0, 0.0, animate, null);
	}

	private void HideDock(bool animate)
	{
		_hiding = true;
		IsHitTestVisible = false;
		AnimateReveal(0.0, 8.0, animate && IsVisible, () =>
		{
			_hiding = false;
			Topmost = false;
			Hide();
		});
	}

	private void AnimateReveal(double opacity, double offset, bool animate, Action? completed)
	{
		int version = ++_revealVersion;
		double opacity2 = _surface.Opacity;
		double y = _revealShift.Y;
		double scaleX = _revealScale.ScaleX;
		double pageNumber = ((opacity > 0.0) ? 1.0 : 0.96);
		_surface.BeginAnimation(UIElement.OpacityProperty, null);
		_revealShift.BeginAnimation(TranslateTransform.YProperty, null);
		_revealScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		_revealScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		ScaleTransform revealScale = _revealScale;
		double scaleX2 = (_revealScale.ScaleY = pageNumber);
		revealScale.ScaleX = scaleX2;
		_surface.Opacity = opacity2;
		_revealShift.Y = y;
		if (!animate || _settings.ReduceMotion || !SystemParameters.ClientAreaAnimation)
		{
			_surface.Opacity = opacity;
			_revealShift.Y = offset;
			completed?.Invoke();
			return;
		}
		TimeSpan timeSpan = TimeSpan.FromMilliseconds((opacity > 0.0) ? 150 : 110);
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		DoubleAnimation doubleAnimation = new DoubleAnimation(opacity2, opacity, timeSpan)
		{
			EasingFunction = easingFunction
		};
		doubleAnimation.Completed += (object? _, EventArgs _) =>
		{
			if (!_disposed && version == _revealVersion)
			{
				_surface.Opacity = opacity;
				_revealShift.Y = offset;
				_surface.BeginAnimation(UIElement.OpacityProperty, null);
				_revealShift.BeginAnimation(TranslateTransform.YProperty, null);
				completed?.Invoke();
			}
		};
		_revealShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(y, offset, timeSpan)
		{
			EasingFunction = easingFunction
		});
		_surface.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
		_revealScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scaleX, pageNumber, timeSpan)
		{
			EasingFunction = easingFunction,
			FillBehavior = FillBehavior.Stop
		});
		_revealScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scaleX, pageNumber, timeSpan)
		{
			EasingFunction = easingFunction,
			FillBehavior = FillBehavior.Stop
		});
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			BeginAnimation(Window.TopProperty, null);
			_revealVersion++;
			_surface.BeginAnimation(UIElement.OpacityProperty, null);
			_revealShift.BeginAnimation(TranslateTransform.YProperty, null);
			StopMagnificationFrames();
			_hotCorner.CornerTriggered -= RevealFromCorner;
			_hotCorner.Dispose();
			_timer.Stop();
			_timer.Tick -= Tick;
			_settings.PropertyChanged -= SettingsChanged;
			_model.PropertyChanged -= ModelChanged;
			if (_launchpadWindow != null)
			{
				_launchpadWindow.IsVisibleChanged -= LaunchpadVisibilityChanged;
			}
			_model.DockApps.CollectionChanged -= DockChanged;
			Close();
		}
	}
}
