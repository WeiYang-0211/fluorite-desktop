namespace Launchpad.Services;

internal sealed record OpenAppWindow(nint Handle, uint ProcessId, string Executable, string Title, string? ApplicationId = null);
