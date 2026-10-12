using System.Runtime.InteropServices;

namespace Launchpad.Interop;

internal static class ShellState
{
	[DllImport("shell32.dll")]
	private static extern int SHQueryUserNotificationState(out int state);

	public static UserNotificationState Query()
	{
		if (SHQueryUserNotificationState(out var state) != 0)
		{
			return UserNotificationState.Other;
		}
		return state switch
		{
			1 => UserNotificationState.NotPresent, 
			2 => UserNotificationState.Busy, 
			3 => UserNotificationState.RunningD3DFullScreen, 
			_ => UserNotificationState.Other, 
		};
	}
}
