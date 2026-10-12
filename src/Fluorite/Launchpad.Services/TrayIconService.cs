using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Launchpad.Infrastructure;
using Launchpad.Interop;

namespace Launchpad.Services;

public class TrayIconService : ITrayIconService, IDisposable
{
	private const int TRAY_ID = 10086;

	private nint _hWnd;

	private HwndSource? _source;

	private ContextMenu? _trayMenu;

	private MenuItem? _openItem;

	private MenuItem? _startupItem;

	private MenuItem? _updateItem;

	private bool _isAdded;

	private Func<bool>? _isStartupEnabledFunc;

	private Func<string>? _getHotkeyDisplayFunc;

	private Func<string>? _getUpdateStatusFunc;

	public event Action? OpenRequested;

	public event Action? SettingsRequested;

	public event Action? RescanRequested;

	public event Action? AddApplicationRequested;

	public event Action? ExitRequested;

	public event Action? ToggleStartupRequested;

	public event Action? CheckUpdateRequested;

	public void SetStatusContext(Func<bool> isStartupEnabled, Func<string> getHotkeyDisplay)
	{
		_isStartupEnabledFunc = isStartupEnabled;
		_getHotkeyDisplayFunc = getHotkeyDisplay;
	}

	public void SetUpdateContext(Func<string> getUpdateStatus)
	{
		_getUpdateStatusFunc = getUpdateStatus;
	}

	public void Initialize(nint windowHandle)
	{
		if (!_isAdded && windowHandle != IntPtr.Zero)
		{
			_hWnd = windowHandle;
			_source = HwndSource.FromHwnd(_hWnd);
			_source?.AddHook(HwndHook);
			TrayNotify.NOTIFYICONDATA lpData = new TrayNotify.NOTIFYICONDATA
			{
				cbSize = Marshal.SizeOf(typeof(TrayNotify.NOTIFYICONDATA)),
				hWnd = _hWnd,
				uID = 10086,
				uFlags = 7,
				uCallbackMessage = 1125,
				szTip = "Fluorite"
			};
			lpData.hIcon = GetAppIconHandle();
			_isAdded = TrayNotify.Shell_NotifyIcon(0, ref lpData);
			CreateTrayContextMenu();
		}
	}

	private static nint GetAppIconHandle()
	{
		try
		{
			string processPath = Environment.ProcessPath;
			if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
			{
				int pageNumber = NativeMethods.GetSystemMetrics(49);
				if (pageNumber <= 0)
				{
					pageNumber = 16;
				}
				nint[] entries = new nint[1];
				uint[] piconid = new uint[1];
				if (ShellIcons.PrivateExtractIcons(processPath, 0, pageNumber, pageNumber, entries, piconid, 1u, 0u) != 0 && entries[0] != IntPtr.Zero)
				{
					App.Log($"[TRAY] exe icon {pageNumber}x{pageNumber} handle=0x{((IntPtr)entries[0]).ToInt64():X}");
					return entries[0];
				}
				App.Log("[TRAY] icon extraction from " + processPath + " returned no handle");
			}
		}
		catch (Exception ex)
		{
			App.Log("[TRAY] icon extraction failed: " + ex.Message);
		}
		return IntPtr.Zero;
	}

	private static System.Windows.Shapes.Path CreateIconPath(string geometryData)
	{
		System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
		path.Width = 14.0;
		path.Height = 14.0;
		path.Stretch = Stretch.Uniform;
		path.Data = Geometry.Parse(geometryData);
		path.SetBinding(Shape.FillProperty, new Binding("Foreground")
		{
			RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(MenuItem), 1)
		});
		return path;
	}

	private void CreateTrayContextMenu()
	{
		Style style = Application.Current?.TryFindResource("MacContextMenuStyle") as Style;
		Style style2 = Application.Current?.TryFindResource("MacMenuItemStyle") as Style;
		Style style3 = Application.Current?.TryFindResource("MacMenuSeparatorStyle") as Style;
		_trayMenu = new ContextMenu();
		_trayMenu.Resources = ThemeManager.LightPalette();
		if (Application.Current?.TryFindResource("AppFont") is FontFamily fontFamily)
		{
			_trayMenu.FontFamily = fontFamily;
			TextOptions.SetTextFormattingMode(_trayMenu, TextFormattingMode.Ideal);
		}
		if (style != null)
		{
			_trayMenu.Style = style;
		}
		_openItem = new MenuItem
		{
			Header = "打开 Fluorite",
			InputGestureText = (_getHotkeyDisplayFunc?.Invoke() ?? "Alt + Space"),
			Icon = CreateIconPath("M3,3H9V9H3V3M11,3H17V9H11V3M3,11H9V17H3V11M11,11H17V17H11V11Z")
		};
		if (style2 != null)
		{
			_openItem.Style = style2;
		}
		_openItem.Click += (object s, RoutedEventArgs e) =>
		{
			OpenRequested?.Invoke();
		};
		MenuItem menuItem = new MenuItem
		{
			Header = "Fluorite 设置…",
			Icon = CreateIconPath("M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.08C16.04,5.66 15.5,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2H10C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.08L4.95,5.05C4.73,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63C2.27,14.78 2.21,15.05 2.34,15.27L4.34,18.73C4.46,18.95 4.73,19.04 4.95,18.95L7.44,17.92C7.96,18.34 8.5,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22H14C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.5,18.68 16.04,18.34 16.56,17.92L19.05,18.95C19.27,19.04 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z")
		};
		if (style2 != null)
		{
			menuItem.Style = style2;
		}
		menuItem.Click += (object s, RoutedEventArgs e) =>
		{
			SettingsRequested?.Invoke();
		};
		MenuItem menuItem2 = new MenuItem
		{
			Header = "重新扫描应用",
			Icon = CreateIconPath("M17.65,6.35C16.2,4.9 14.21,4 12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20C15.73,20 18.84,17.45 19.73,14H17.65C16.83,16.33 14.61,18 12,18A6,6 0 0,1 6,12A6,6 0 0,1 12,6C13.66,6 15.14,6.69 16.22,7.78L13,11H20V4L17.65,6.35Z")
		};
		if (style2 != null)
		{
			menuItem2.Style = style2;
		}
		menuItem2.Click += (object s, RoutedEventArgs e) =>
		{
			RescanRequested?.Invoke();
		};
		MenuItem menuItem3 = new MenuItem
		{
			Header = "添加应用…",
			Icon = CreateIconPath("M13,7H11V11H7V13H11V17H13V13H17V11H13V7M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z")
		};
		if (style2 != null)
		{
			menuItem3.Style = style2;
		}
		menuItem3.Click += (object s, RoutedEventArgs e) =>
		{
			AddApplicationRequested?.Invoke();
		};
		Separator separator = new Separator();
		if (style3 != null)
		{
			separator.Style = style3;
		}
		_startupItem = new MenuItem
		{
			Header = "开机自启动",
			IsCheckable = true
		};
		if (style2 != null)
		{
			_startupItem.Style = style2;
		}
		_startupItem.Click += (object s, RoutedEventArgs e) =>
		{
			ToggleStartupRequested?.Invoke();
		};
		Separator separator2 = new Separator();
		if (style3 != null)
		{
			separator2.Style = style3;
		}
		_updateItem = new MenuItem
		{
			Header = "检查更新…",
			InputGestureText = (_getUpdateStatusFunc?.Invoke() ?? string.Empty),
			Icon = CreateIconPath("M13,4.6 L11,4.6 L11,12.2 L7.6,8.8 L6.2,10.2 L12,16 L17.8,10.2 L16.4,8.8 L13,12.2 Z M5,18.2 L19,18.2 L19,20.2 L5,20.2 Z")
		};
		if (style2 != null)
		{
			_updateItem.Style = style2;
		}
		_updateItem.Click += (object s, RoutedEventArgs e) =>
		{
			CheckUpdateRequested?.Invoke();
		};
		MenuItem menuItem4 = new MenuItem
		{
			Header = "退出 Fluorite",
			Icon = CreateIconPath("M16.56,5.44L15.11,6.89C16.84,8.63 17.63,11.08 17.25,13.6C16.71,17.15 13.84,20 10.28,20.47C6.07,21.03 2.5,17.5 2.5,13.25C2.5,10.63 3.79,8.31 5.78,6.89L4.33,5.44C1.94,7.24 0.5,10.07 0.5,13.25C0.5,18.89 5.11,23.5 10.75,23.5C16.39,23.5 21,18.89 21,13.25C21,10.07 19.56,7.24 16.56,5.44M9.75,1H11.75V13H9.75V1Z")
		};
		if (style2 != null)
		{
			menuItem4.Style = style2;
		}
		menuItem4.Click += (object s, RoutedEventArgs e) =>
		{
			ExitRequested?.Invoke();
		};
		_trayMenu.Items.Add(_openItem);
		_trayMenu.Items.Add(menuItem);
		_trayMenu.Items.Add(menuItem2);
		_trayMenu.Items.Add(menuItem3);
		_trayMenu.Items.Add(separator);
		_trayMenu.Items.Add(_startupItem);
		_trayMenu.Items.Add(separator2);
		_trayMenu.Items.Add(_updateItem);
		_trayMenu.Items.Add(menuItem4);
	}

	private nint HwndHook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
	{
		if (msg == 1125 && ((IntPtr)wParam).ToInt32() == 10086)
		{
			switch (((IntPtr)lParam).ToInt32())
			{
			case 514:
				OpenRequested?.Invoke();
				handled = true;
				break;
			case 517:
				ShowContextMenu();
				handled = true;
				break;
			}
		}
		return IntPtr.Zero;
	}

	private void ShowContextMenu()
	{
		if (_trayMenu != null)
		{
			if (_isStartupEnabledFunc != null && _startupItem != null)
			{
				_startupItem.IsChecked = _isStartupEnabledFunc();
			}
			if (_getHotkeyDisplayFunc != null && _openItem != null)
			{
				_openItem.InputGestureText = _getHotkeyDisplayFunc();
			}
			if (_getUpdateStatusFunc != null && _updateItem != null)
			{
				_updateItem.InputGestureText = _getUpdateStatusFunc();
			}
			NativeMethods.SetForegroundWindow(_hWnd);
			_trayMenu.Placement = PlacementMode.MousePoint;
			_trayMenu.HorizontalOffset = 0.0;
			_trayMenu.VerticalOffset = -2.0;
			_trayMenu.IsOpen = true;
		}
	}

	public void Remove()
	{
		if (_isAdded && _hWnd != IntPtr.Zero)
		{
			TrayNotify.NOTIFYICONDATA lpData = new TrayNotify.NOTIFYICONDATA
			{
				cbSize = Marshal.SizeOf(typeof(TrayNotify.NOTIFYICONDATA)),
				hWnd = _hWnd,
				uID = 10086
			};
			TrayNotify.Shell_NotifyIcon(2, ref lpData);
			_source?.RemoveHook(HwndHook);
			_source = null;
			_isAdded = false;
		}
	}

	public void ShowBalloonNotification(string title, string text)
	{
		if (!_isAdded || _hWnd == IntPtr.Zero)
		{
			return;
		}
		try
		{
			TrayNotify.NOTIFYICONDATA lpData = new TrayNotify.NOTIFYICONDATA
			{
				cbSize = Marshal.SizeOf(typeof(TrayNotify.NOTIFYICONDATA)),
				hWnd = _hWnd,
				uID = 10086,
				uFlags = 16,
				szInfoTitle = (title ?? "Fluorite"),
				szInfo = (text ?? string.Empty),
				dwInfoFlags = 1,
				uTimeoutOrVersion = 3000
			};
			TrayNotify.Shell_NotifyIcon(1, ref lpData);
		}
		catch (Exception ex)
		{
			App.Log("Failed to show balloon notification: " + ex.Message);
		}
	}

	public void Dispose()
	{
		Remove();
		GC.SuppressFinalize(this);
	}
}
