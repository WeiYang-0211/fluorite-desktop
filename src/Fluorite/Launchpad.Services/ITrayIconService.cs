using System;

namespace Launchpad.Services;

public interface ITrayIconService : IDisposable
{
	event Action? OpenRequested;

	event Action? SettingsRequested;

	event Action? RescanRequested;

	event Action? AddApplicationRequested;

	event Action? ExitRequested;

	event Action? ToggleStartupRequested;

	event Action? CheckUpdateRequested;

	void Initialize(nint windowHandle);

	void SetStatusContext(Func<bool> isStartupEnabled, Func<string> getHotkeyDisplay);

	void SetUpdateContext(Func<string> getUpdateStatus);

	void ShowBalloonNotification(string title, string text);

	void Remove();
}
