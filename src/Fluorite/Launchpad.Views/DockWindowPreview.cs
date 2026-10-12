using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Launchpad.Models;
using Launchpad.Services;
using Launchpad.ViewModels;

namespace Launchpad.Views;

internal sealed class DockWindowPreview
{
	private sealed class PreviewCard : Window
	{
		private nint _thumbnail;

		private readonly List<(nint Handle, Rect Bounds)> _thumbnails = new List<(nint, Rect)>();

		private readonly List<(OpenAppWindow Window, Rect Bounds)> _targets = new List<(OpenAppWindow, Rect)>();

		internal PreviewCard(OpenAppWindow[] targets, AppItem app, double width, int columns, int total, int page, int pages, Action close, Action<OpenAppWindow> activate, Action<int> changePage)
		{
			PreviewCard previewCard = this;
			double pageNumber = (width - 44.0 - (double)((columns - 1) * 14)) / (double)columns;
			int settingIndex = (targets.Length + columns - 1) / columns;
			Width = width;
			Height = 80.0 + (double)settingIndex * 182.0 + 54.0;
			WindowStyle = WindowStyle.None;
			ResizeMode = ResizeMode.NoResize;
			ShowInTaskbar = false;
			ShowActivated = false;
			Topmost = true;
			SetResourceReference(Control.BackgroundProperty, "T.Sheet_FF");
			SetResourceReference(Control.FontFamilyProperty, "AppFont");
			WindowChrome.SetWindowChrome(this, new WindowChrome
			{
				CaptionHeight = 0.0,
				ResizeBorderThickness = new Thickness(0.0),
				GlassFrameThickness = new Thickness(0.0),
				CornerRadius = new CornerRadius(20.0),
				UseAeroCaptionButtons = false
			});
			Canvas canvas = new Canvas();
			Border border = new Border
			{
				CornerRadius = new CornerRadius(20.0),
				BorderThickness = new Thickness(1.0),
				Child = canvas
			};
			border.SetResourceReference(Border.BorderBrushProperty, "T.Fg_26");
			Content = border;
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				VerticalAlignment = VerticalAlignment.Center,
				Children = { (UIElement)new Image
				{
					Source = app.Icon,
					Width = 34.0,
					Height = 34.0,
					Margin = new Thickness(0.0, 0.0, 12.0, 0.0)
				} }
			};
			TextBlock textBlock = DialogKit.Title(app.DisplayName, 15.0);
			textBlock.TextWrapping = TextWrapping.NoWrap;
			textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
			textBlock.MaxWidth = Math.Max(70.0, width - 240.0);
			textBlock.VerticalAlignment = VerticalAlignment.Center;
			textBlock.Margin = new Thickness(0.0);
			stackPanel.Children.Add(textBlock);
			Border border2 = new Border
			{
				CornerRadius = new CornerRadius(9.0),
				Padding = new Thickness(10.0, 4.0, 10.0, 4.0),
				Margin = new Thickness(12.0, 0.0, 0.0, 0.0),
				Child = DialogKit.Body($"{total} 个窗口", 12.0, dim: true, new Thickness(0.0)),
				VerticalAlignment = VerticalAlignment.Center
			};
			border2.SetResourceReference(Border.BackgroundProperty, "T.Fg_14");
			stackPanel.Children.Add(border2);
			Place(stackPanel, 22.0, 22.0);
			Button button = DialogKit.Push("×", primary: false);
			button.Width = 34.0;
			button.Height = 32.0;
			button.Padding = new Thickness(0.0);
			button.Margin = new Thickness(0.0);
			button.ToolTip = "关闭预览";
			button.Click += (object _, RoutedEventArgs _) =>
			{
				close();
			};
			Place(button, width - 56.0, 23.0);
			for (int targetPage = 0; targetPage < targets.Length; targetPage++)
			{
				OpenAppWindow target = targets[targetPage];
				double num4 = 22.0 + (double)(targetPage % columns) * (pageNumber + 14.0);
				double num5 = 80.0 + (double)(targetPage / columns) * 182.0;
				Border item = new Border
				{
					Width = pageNumber,
					Height = 168.0,
					CornerRadius = new CornerRadius(12.0),
					BorderThickness = new Thickness(1.0),
					Cursor = Cursors.Hand,
					ToolTip = target.Title
				};
				item.SetResourceReference(Border.BackgroundProperty, "T.Fg_14");
				item.SetResourceReference(Border.BorderBrushProperty, "T.Fg_1A");
				Grid grid = new Grid
				{
					RowDefinitions = 
					{
						new RowDefinition
						{
							Height = new GridLength(132.0)
						},
						new RowDefinition
						{
							Height = new GridLength(36.0)
						}
					},
					Children = { (UIElement)new Image
					{
						Source = app.Icon,
						Width = 40.0,
						Height = 40.0,
						Opacity = 0.5
					} }
				};
				StackPanel stackPanel2 = new StackPanel
				{
					Orientation = Orientation.Horizontal,
					Margin = new Thickness(10.0, 0.0, 8.0, 0.0),
					VerticalAlignment = VerticalAlignment.Center
				};
				stackPanel2.Children.Add(new Image
				{
					Source = app.Icon,
					Width = 16.0,
					Height = 16.0,
					Margin = new Thickness(0.0, 0.0, 7.0, 0.0)
				});
				TextBlock textBlock2 = DialogKit.Body(target.Title, 12.0, dim: false, new Thickness(0.0));
				textBlock2.TextWrapping = TextWrapping.NoWrap;
				textBlock2.TextTrimming = TextTrimming.CharacterEllipsis;
				textBlock2.Width = pageNumber - 43.0;
				stackPanel2.Children.Add(textBlock2);
				Grid.SetRow(stackPanel2, 1);
				grid.Children.Add(stackPanel2);
				item.Child = grid;
				item.MouseEnter += (object _, MouseEventArgs _) =>
				{
					item.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
					item.BorderThickness = new Thickness(2.0);
				};
				item.MouseLeave += (object _, MouseEventArgs _) =>
				{
					item.SetResourceReference(Border.BorderBrushProperty, "T.Fg_1A");
					item.BorderThickness = new Thickness(1.0);
				};
				item.MouseLeftButtonUp += (object _, MouseButtonEventArgs e) =>
				{
					activate(target);
					e.Handled = true;
				};
				Place(item, num4, num5);
				_targets.Add((target, new Rect(num4 + 6.0, num5 + 6.0, pageNumber - 12.0, 120.0)));
			}
			double num6 = Height - 52.0;
			Border border3 = new Border();
			border3.Width = width - 44.0;
			border3.Height = 1.0;
			border3.SetResourceReference(Border.BackgroundProperty, "T.Fg_14");
			Place(border3, 22.0, num6 - 6.0);
			Place(DialogKit.Body("点击预览恢复窗口", 12.0, dim: true, new Thickness(0.0)), 22.0, num6 + 10.0);
			Button button2 = DialogKit.Push("‹", primary: false);
			Button button3 = DialogKit.Push("›", primary: false);
			button2.Width = (button3.Width = 32.0);
			button2.Height = (button3.Height = 30.0);
			button2.Padding = (button3.Padding = new Thickness(0.0));
			button2.Margin = (button3.Margin = new Thickness(0.0));
			button2.IsEnabled = (button3.IsEnabled = pages > 1);
			button2.ToolTip = "上一页";
			button3.ToolTip = "下一页";
			button2.Click += (object _, RoutedEventArgs _) =>
			{
				changePage(-1);
			};
			button3.Click += (object _, RoutedEventArgs _) =>
			{
				changePage(1);
			};
			Place(button2, width - 150.0, num6 + 2.0);
			Place(button3, width - 54.0, num6 + 2.0);
			Place(DialogKit.Body($"{page + 1} / {pages}", 12.0, dim: true, new Thickness(0.0)), width - 106.0, num6 + 9.0);
			MouseWheel += (object _, MouseWheelEventArgs e) =>
			{
				changePage((e.Delta < 0) ? 1 : (-1));
				e.Handled = true;
			};
			SourceInitialized += (object? _, EventArgs _) =>
			{
				nint handle = new WindowInteropHelper(previewCard).Handle;
				HwndSource.FromHwnd(handle)?.AddHook(NoActivate);
				foreach (var target2 in previewCard._targets)
				{
					if (DwmRegisterThumbnail(handle, target2.Window.Handle, out var thumbnail) == 0)
					{
						previewCard._thumbnails.Add((thumbnail, target2.Bounds));
					}
				}
				previewCard._thumbnail = ((previewCard._thumbnails.Count > 0) ? previewCard._thumbnails[0].Handle : IntPtr.Zero);
			};
			Loaded += (object _, RoutedEventArgs _) =>
			{
				previewCard.UpdateThumbnail();
			};
			Closed += (object? _, EventArgs _) =>
			{
				foreach (var thumbnail2 in previewCard._thumbnails)
				{
					DwmUnregisterThumbnail(thumbnail2.Handle);
				}
				previewCard._thumbnails.Clear();
				previewCard._thumbnail = IntPtr.Zero;
			};
			void Place(UIElement element, double x, double y)
			{
				Canvas.SetLeft(element, x);
				Canvas.SetTop(element, y);
				canvas.Children.Add(element);
			}
		}

		private static nint NoActivate(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
		{
			if (msg == 33)
			{
				handled = true;
				return new IntPtr(3);
			}
			return IntPtr.Zero;
		}

		private void UpdateThumbnail()
		{
			Matrix matrix = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
			foreach (var thumbnail in _thumbnails)
			{
				if (DwmQueryThumbnailSourceSize(thumbnail.Handle, out var size) == 0 && size.X > 0 && size.Y > 0)
				{
					Rect item = thumbnail.Bounds;
					double pageNumber = Math.Min(item.Width / (double)size.X, item.Height / (double)size.Y);
					double settingIndex = (double)size.X * pageNumber;
					double targetPage = (double)size.Y * pageNumber;
					Point point = matrix.Transform(new Point(item.X + (item.Width - settingIndex) / 2.0, item.Y + (item.Height - targetPage) / 2.0));
					Point point2 = matrix.Transform(new Point(item.X + (item.Width + settingIndex) / 2.0, item.Y + (item.Height + targetPage) / 2.0));
					ThumbnailProperties properties = new ThumbnailProperties
					{
						Flags = 29,
						Opacity = byte.MaxValue,
						Visible = true,
						ClientOnly = false,
						Destination = new NativeRect
						{
							Left = (int)point.X,
							Top = (int)point.Y,
							Right = (int)point2.X,
							Bottom = (int)point2.Y
						}
					};
					DwmUpdateThumbnailProperties(thumbnail.Handle, ref properties);
				}
			}
		}
	}

	private struct NativeRect
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private struct NativeSize
	{
		public int X;

		public int Y;
	}

	private struct MonitorInfo
	{
		public int Size;

		public NativeRect Monitor;

		public NativeRect Work;

		public int Flags;
	}

	private struct ThumbnailProperties
	{
		public int Flags;

		public NativeRect Destination;

		public NativeRect Source;

		public byte Opacity;

		[MarshalAs(UnmanagedType.Bool)]
		public bool Visible;

		[MarshalAs(UnmanagedType.Bool)]
		public bool ClientOnly;
	}

	private readonly MainViewModel _model;

	private readonly DispatcherTimer _timer;

	private readonly List<PreviewCard> _cards = new List<PreviewCard>();

	private FrameworkElement? _anchor;

	private AppItem? _app;

	private DateTime _entered;

	private DateTime _lastInside;

	private int _page;

	private string _signature = "";

	internal bool IsPointerOver => _cards.Any((PreviewCard c) => c.IsMouseOver);

	internal DockWindowPreview(MainViewModel model, Window owner)
	{
		DockWindowPreview dockWindowPreview = this;
		_model = model;
		_timer = new DispatcherTimer(TimeSpan.FromMilliseconds(80.0), DispatcherPriority.Background, Tick, owner.Dispatcher);
		_timer.Stop();
		owner.Closed += (object? _, EventArgs _) =>
		{
			dockWindowPreview.Close();
		};
		owner.IsVisibleChanged += (object _, DependencyPropertyChangedEventArgs _) =>
		{
			if (!owner.IsVisible)
			{
				dockWindowPreview.Close();
			}
		};
	}

	internal void Hover(FrameworkElement anchor, AppItem app)
	{
		if (!app.IsOpenWindowEntry)
		{
			Close();
		}
		else if (_anchor != anchor)
		{
			Close();
			_anchor = anchor;
			_app = app;
			_page = 0;
			_entered = (_lastInside = DateTime.UtcNow);
			_timer.Start();
		}
	}

	internal void Close()
	{
		_timer.Stop();
		ClearCards();
		_anchor = null;
		_app = null;
		_signature = "";
	}

	private void ClearCards()
	{
		foreach (PreviewCard card in _cards)
		{
			card.Close();
		}
		_cards.Clear();
	}

	private void Tick(object? sender, EventArgs e)
	{
		if (_anchor == null || _app == null || !_anchor.IsVisible)
		{
			Close();
			return;
		}
		DateTime utcNow = DateTime.UtcNow;
		if (_anchor.IsMouseOver || _cards.Any((PreviewCard c) => c.IsMouseOver))
		{
			_lastInside = utcNow;
		}
		else if ((utcNow - _lastInside).TotalMilliseconds > 450.0)
		{
			Close();
			return;
		}
		if ((utcNow - _entered).TotalMilliseconds < 350.0 || Mouse.LeftButton == MouseButtonState.Pressed)
		{
			return;
		}
		IReadOnlyList<OpenAppWindow> readOnlyList = _model.OpenWindowsFor(_app);
		if (readOnlyList.Count == 0)
		{
			Close();
			return;
		}
		string text = string.Join("|", readOnlyList.Select((OpenAppWindow w) => $"{w.Handle}:{w.Title}"));
		if (_cards.Count == 0 || text != _signature)
		{
			_signature = text;
			ShowCards(readOnlyList);
		}
	}

	private void ShowCards(IReadOnlyList<OpenAppWindow> windows)
	{
		if (_anchor == null || _app == null)
		{
			return;
		}
		if (windows.Count == 0)
		{
			Close();
			return;
		}
		ClearCards();
		Matrix matrix = PresentationSource.FromVisual(_anchor)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
		Point point = _anchor.PointToScreen(new Point(_anchor.ActualWidth / 2.0, 0.0));
		Point point2 = matrix.Transform(point);
		Rect rect = SystemParameters.WorkArea;
		nint monitor = MonitorFromPoint(new NativeSize
		{
			X = (int)point.X,
			Y = (int)point.Y
		}, 2u);
		MonitorInfo info = new MonitorInfo
		{
			Size = Marshal.SizeOf<MonitorInfo>()
		};
		if (GetMonitorInfo(monitor, ref info))
		{
			rect = new Rect(matrix.Transform(new Point(info.Work.Left, info.Work.Top)), matrix.Transform(new Point(info.Work.Right, info.Work.Bottom)));
		}
		int pageNumber = Math.Max(1, Math.Min(4, (int)((rect.Width - 64.0) / 230.0)));
		int settingIndex = ((!(rect.Height >= 570.0)) ? 1 : 2);
		int targetPage = pageNumber * settingIndex;
		int pages = (windows.Count + targetPage - 1) / targetPage;
		_page = Math.Clamp(_page, 0, pages - 1);
		OpenAppWindow[] entries = windows.Skip(_page * targetPage).Take(targetPage).ToArray();
		pageNumber = Math.Min(pageNumber, entries.Length);
		double num4 = Math.Min(rect.Width - 24.0, Math.Max(360, pageNumber * 220 + (pageNumber - 1) * 14 + 44));
		PreviewCard previewCard = new PreviewCard(entries, _app, num4, pageNumber, windows.Count, _page, pages, Close, (OpenAppWindow target) =>
		{
			Close();
			_model.RestoreSpecificOpenWindow(target);
		}, ChangePage);
		previewCard.Left = Math.Clamp(point2.X - num4 / 2.0, rect.Left + 8.0, Math.Max(rect.Left + 8.0, rect.Right - num4 - 8.0));
		previewCard.Top = Math.Max(rect.Top + 8.0, point2.Y - previewCard.Height - 14.0);
		_cards.Add(previewCard);
		previewCard.Show();
		void ChangePage(int delta)
		{
			if (_app != null && pages > 1)
			{
				_page = (_page + delta + pages) % pages;
				ShowCards(_model.OpenWindowsFor(_app));
				_lastInside = DateTime.UtcNow;
			}
		}
	}

	[DllImport("user32.dll")]
	private static extern nint MonitorFromPoint(NativeSize point, uint flags);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

	[DllImport("dwmapi.dll")]
	private static extern int DwmRegisterThumbnail(nint destination, nint source, out nint thumbnail);

	[DllImport("dwmapi.dll")]
	private static extern int DwmUnregisterThumbnail(nint thumbnail);

	[DllImport("dwmapi.dll")]
	private static extern int DwmQueryThumbnailSourceSize(nint thumbnail, out NativeSize size);

	[DllImport("dwmapi.dll")]
	private static extern int DwmUpdateThumbnailProperties(nint thumbnail, ref ThumbnailProperties properties);
}
