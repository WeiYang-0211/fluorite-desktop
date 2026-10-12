using System;

namespace Launchpad.Services;

public interface IHotkeyService : IDisposable
{
	bool IsRegistered { get; }

	event Action? HotkeyPressed;

	bool Register(nint windowHandle, uint modifiers, uint key, out string errorMessage);

	void Unregister();
}
