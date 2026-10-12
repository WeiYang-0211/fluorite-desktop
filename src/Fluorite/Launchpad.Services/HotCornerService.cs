using System;
using System.Collections.Generic;
using System.Windows.Threading;
using Launchpad.Interop;
using Launchpad.Models;

namespace Launchpad.Services;

public sealed class HotCornerService : IDisposable
{
	private const int TriggerRadius = 6;

	private const int RearmRadius = 40;

	private readonly Func<bool> _enabled;

	private readonly Func<HotCornerPosition> _corner;

	private readonly Func<bool> _pauseFullscreen;

	private readonly Func<IEnumerable<AppItem>>? _apps;

	private readonly DispatcherTimer _timer;

	private bool _armed = true;

	public event Action? CornerTriggered;

	public HotCornerService(Func<AppSettings> settingsProvider)
		: this(() => ShouldOpenLaunchpad(settingsProvider()), () => settingsProvider().HotCorner, () => settingsProvider().PauseHotCornerInFullscreen)
	{
	}

	internal static bool ShouldOpenLaunchpad(AppSettings settings)
	{
		return settings.EnableHotCorner;
	}

	public HotCornerService(Func<bool> enabled, Func<HotCornerPosition> corner, Func<bool> pauseFullscreen, Func<IEnumerable<AppItem>>? apps = null)
	{
		_enabled = enabled;
		_corner = corner;
		_pauseFullscreen = pauseFullscreen;
		_apps = apps;
		_timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(150.0)
		};
		_timer.Tick += (object? s, EventArgs e) =>
		{
			Poll();
		};
		_timer.Start();
	}

	private void Poll()
	{
		if (!_enabled())
		{
			_armed = true;
		}
		else
		{
			if (!NativeMethods.GetCursorPos(out var lpPoint))
			{
				return;
			}
			int systemMetrics = NativeMethods.GetSystemMetrics(76);
			int systemMetrics2 = NativeMethods.GetSystemMetrics(77);
			int item = systemMetrics + NativeMethods.GetSystemMetrics(78);
			int item2 = systemMetrics2 + NativeMethods.GetSystemMetrics(79);
			(int, int) tuple = _corner() switch
			{
				HotCornerPosition.BottomRight => (item, item2), 
				HotCornerPosition.TopLeft => (systemMetrics, systemMetrics2), 
				HotCornerPosition.TopRight => (item, systemMetrics2), 
				_ => (systemMetrics, item2), 
			};
			int item3 = tuple.Item1;
			int item4 = tuple.Item2;
			int pageNumber = Math.Abs(lpPoint.X - item3);
			int settingIndex = Math.Abs(lpPoint.Y - item4);
			if (pageNumber > 40 || settingIndex > 40)
			{
				_armed = true;
			}
			if (pageNumber <= 6 && settingIndex <= 6 && _armed)
			{
				_armed = false;
				if (GameActivityGuard.IsPlaying(_apps?.Invoke()) || (_pauseFullscreen() && FullscreenGuard.IsScreenOwned(item3, item4)))
				{
					App.Log($"[HOTCORNER] suppressed, something else owns the screen at ({lpPoint.X},{lpPoint.Y})");
				}
				else
				{
					App.Log($"[HOTCORNER] fired at ({lpPoint.X},{lpPoint.Y}) corner={_corner()}");
					CornerTriggered?.Invoke();
				}
			}
		}
	}

	public void Dispose()
	{
		_timer.Stop();
	}
}
