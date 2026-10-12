using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using Launchpad.Interop;
using Launchpad.Models;
using Launchpad.ViewModels;

namespace Launchpad.Services;

internal sealed class OpenWindowsMonitor : IDisposable
{
	private delegate bool WindowVisitor(nint hwnd, nint parameter);

	private readonly MainViewModel _model;

	private readonly Dispatcher _dispatcher;

	private AppSettings _settings;

	private Timer? _timer;

	private int _busy;

	private bool _disposed;

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(WindowVisitor visitor, nint parameter);

	[DllImport("user32.dll")]
	private static extern bool EnumChildWindows(nint parent, WindowVisitor visitor, nint parameter);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(nint hwnd);

	[DllImport("user32.dll")]
	private static extern nint GetWindow(nint hwnd, uint command);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowText(nint hwnd, StringBuilder title, int count);

	[DllImport("dwmapi.dll")]
	private static extern int DwmGetWindowAttribute(nint hwnd, uint attribute, out int value, int size);

	[DllImport("kernel32.dll")]
	private static extern nint OpenProcess(uint access, bool inherit, uint id);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder path, ref uint size);

	[DllImport("kernel32.dll")]
	private static extern bool CloseHandle(nint handle);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetApplicationUserModelId(nint process, ref uint length, StringBuilder? id);

	private static string? AppIdOf(uint pid)
	{
		nint pageNumber = OpenProcess(4096u, inherit: false, pid);
		if (pageNumber == IntPtr.Zero)
		{
			return null;
		}
		try
		{
			uint length = 0u;
			if (GetApplicationUserModelId(pageNumber, ref length, null) != 122 || length == 0 || length > 32768)
			{
				return null;
			}
			StringBuilder stringBuilder = new StringBuilder((int)length);
			return (GetApplicationUserModelId(pageNumber, ref length, stringBuilder) == 0) ? stringBuilder.ToString() : null;
		}
		finally
		{
			CloseHandle(pageNumber);
		}
	}

	internal OpenWindowsMonitor(MainViewModel model, Dispatcher dispatcher)
	{
		_model = model;
		_dispatcher = dispatcher;
		_settings = model.Settings;
		_settings.PropertyChanged += Changed;
		_model.PropertyChanged += ModelChanged;
		Configure();
	}

	private void ModelChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "Settings")
		{
			_settings.PropertyChanged -= Changed;
			_settings = _model.Settings;
			_settings.PropertyChanged += Changed;
		}
		string propertyName = e.PropertyName;
		if ((propertyName == "Settings" || propertyName == "IsOpen") ? true : false)
		{
			Configure();
		}
	}

	private void Changed(object? sender, PropertyChangedEventArgs e)
	{
		bool validated;
		switch (e.PropertyName)
		{
		case "ShowOpenAppsInDock":
		case "EnableDock":
		case "ShowDesktopDock":
		case "EnableDesktopDockHotCorner":
			validated = true;
			break;
		default:
			validated = false;
			break;
		}
		if (validated)
		{
			Configure();
		}
	}

	private void Configure()
	{
		if (!_disposed && _settings.EnableDock && _settings.ShowOpenAppsInDock && (_model.IsOpen || _settings.ShowDesktopDock || _settings.EnableDesktopDockHotCorner))
		{
			if (_timer == null)
			{
				_timer = new Timer((object? _) =>
				{
					Sample();
				}, null, 0, 1000);
			}
			return;
		}
		_timer?.Dispose();
		_timer = null;
		if (!_settings.ShowOpenAppsInDock)
		{
			_model.SetOpenWindowsAsync(Array.Empty<OpenAppWindow>());
		}
	}

	private void Sample()
	{
		if (_disposed || Interlocked.Exchange(ref _busy, 1) != 0)
		{
			return;
		}
		try
		{
			IReadOnlyList<OpenAppWindow> snapshot = Capture();
			_dispatcher.BeginInvoke((Action)(async () =>
			{
				try
				{
					if (!_disposed && _settings.EnableDock && _settings.ShowOpenAppsInDock)
					{
						await _model.SetOpenWindowsAsync(snapshot);
					}
				}
				catch (Exception ex2)
				{
					App.Log("[OPEN-WINDOWS] " + ex2.Message);
				}
				finally
				{
					Volatile.Write(ref _busy, 0);
				}
			}), DispatcherPriority.Background);
		}
		catch (Exception ex)
		{
			App.Log("[OPEN-WINDOWS] " + ex.Message);
			Volatile.Write(ref _busy, 0);
		}
	}

	private static string? PathOf(uint pid)
	{
		nint pageNumber = OpenProcess(4096u, inherit: false, pid);
		if (pageNumber == IntPtr.Zero)
		{
			return null;
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder(32768);
			uint size = 32768u;
			return QueryFullProcessImageName(pageNumber, 0u, stringBuilder, ref size) ? stringBuilder.ToString() : null;
		}
		finally
		{
			CloseHandle(pageNumber);
		}
	}

	internal static bool IsAppWindow(bool visible, bool cloaked, bool owned, long style, string title)
	{
		if (visible && !cloaked && title.Length > 0 && (style & 0x80) == 0L && (!owned || (style & 0x40000) != 0L))
		{
			if ((style & 0x8000000) != 0L)
			{
				return (style & 0x40000) != 0;
			}
			return true;
		}
		return false;
	}

	internal static IReadOnlyList<OpenAppWindow> Capture()
	{
		List<OpenAppWindow> result = new List<OpenAppWindow>();
		Dictionary<uint, string?> paths = new Dictionary<uint, string>();
		EnumWindows((nint hwnd, nint _) =>
		{
			if (!IsWindowVisible(hwnd))
			{
				return true;
			}
			NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
			if (pid == (uint)Environment.ProcessId)
			{
				return true;
			}
			StringBuilder stringBuilder = new StringBuilder(512);
			GetWindowText(hwnd, stringBuilder, stringBuilder.Capacity);
			DwmGetWindowAttribute(hwnd, 14u, out var value, 4);
			if (!IsAppWindow(visible: true, value != 0, GetWindow(hwnd, 4u) != IntPtr.Zero, ((IntPtr)NativeMethods.GetWindowLongPtr(hwnd, -20)).ToInt64(), stringBuilder.ToString()))
			{
				return true;
			}
			string path = PathFor(pid);
			uint identityPid = pid;
			if (path != null && Path.GetFileName(path).Equals("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
			{
				EnumChildWindows(hwnd, (nint child, nint pageNumber) =>
				{
					NativeMethods.GetWindowThreadProcessId(child, out var lpdwProcessId);
					if (lpdwProcessId == pid)
					{
						return true;
					}
					string text = PathFor(lpdwProcessId);
					if (text == null)
					{
						return true;
					}
					path = text;
					identityPid = lpdwProcessId;
					return false;
				}, IntPtr.Zero);
			}
			if (path == null || hwnd == NativeMethods.GetShellWindow())
			{
				return true;
			}
			result.Add(new OpenAppWindow(hwnd, pid, path, stringBuilder.ToString(), AppIdOf(identityPid)));
			return true;
		}, IntPtr.Zero);
		return result;
		string? PathFor(uint pid)
		{
			if (!paths.TryGetValue(pid, out string value))
			{
				value = (paths[pid] = PathOf(pid));
			}
			return value;
		}
	}

	public void Dispose()
	{
		_disposed = true;
		_timer?.Dispose();
		_settings.PropertyChanged -= Changed;
		_model.PropertyChanged -= ModelChanged;
	}
}
