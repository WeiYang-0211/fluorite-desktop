using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Launchpad.Interop;

namespace Launchpad.Services;

public class HotkeyService : IHotkeyService, IDisposable
{
	private readonly int _hotkeyId;

	private nint _hWnd = IntPtr.Zero;

	private HwndSource? _source;

	private bool _isRegistered;

	public bool IsRegistered => _isRegistered;

	public event Action? HotkeyPressed;

	public HotkeyService(int hotkeyId = 9001)
	{
		_hotkeyId = hotkeyId;
	}

	public bool Register(nint windowHandle, uint modifiers, uint key, out string errorMessage)
	{
		errorMessage = string.Empty;
		if (_isRegistered)
		{
			Unregister();
		}
		if (windowHandle == IntPtr.Zero)
		{
			errorMessage = "Invalid window handle.";
			return false;
		}
		_hWnd = windowHandle;
		_source = HwndSource.FromHwnd(_hWnd);
		_source?.AddHook(HwndHook);
		uint fsModifiers = modifiers | 0x4000;
		bool validated = NativeMethods.RegisterHotKey(_hWnd, _hotkeyId, fsModifiers, key);
		if (!validated)
		{
			validated = NativeMethods.RegisterHotKey(_hWnd, _hotkeyId, modifiers, key);
		}
		if (!validated)
		{
			int lastWin32Error = Marshal.GetLastWin32Error();
			errorMessage = new Win32Exception(lastWin32Error).Message;
			_source?.RemoveHook(HwndHook);
			_source = null;
			_hWnd = IntPtr.Zero;
			_isRegistered = false;
			return false;
		}
		_isRegistered = true;
		return true;
	}

	public void Unregister()
	{
		if (_isRegistered && _hWnd != IntPtr.Zero)
		{
			NativeMethods.UnregisterHotKey(_hWnd, _hotkeyId);
			_source?.RemoveHook(HwndHook);
			_source = null;
			_hWnd = IntPtr.Zero;
			_isRegistered = false;
		}
	}

	private nint HwndHook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
	{
		if (msg == 786 && ((IntPtr)wParam).ToInt32() == _hotkeyId)
		{
			HotkeyPressed?.Invoke();
			handled = true;
		}
		return IntPtr.Zero;
	}

	public void Dispose()
	{
		Unregister();
		GC.SuppressFinalize(this);
	}
}
