using System.Text;

namespace Launchpad.Interop;

public static class ShellLinkHelper
{
	public static (string targetPath, string arguments, string iconPath, int iconIndex) ResolveShortcut(string lnkPath)
	{
		try
		{
			IShellLinkW shellLinkW = (IShellLinkW)new ShellLink();
			((IPersistFile)shellLinkW).Load(lnkPath, 0u);
			StringBuilder stringBuilder = new StringBuilder(260);
			shellLinkW.GetPath(stringBuilder, stringBuilder.Capacity, out var _, 0u);
			StringBuilder stringBuilder2 = new StringBuilder(1024);
			shellLinkW.GetArguments(stringBuilder2, stringBuilder2.Capacity);
			StringBuilder stringBuilder3 = new StringBuilder(260);
			shellLinkW.GetIconLocation(stringBuilder3, stringBuilder3.Capacity, out var piIcon);
			return (targetPath: stringBuilder.ToString(), arguments: stringBuilder2.ToString(), iconPath: stringBuilder3.ToString(), iconIndex: piIcon);
		}
		catch
		{
			return (targetPath: string.Empty, arguments: string.Empty, iconPath: string.Empty, iconIndex: 0);
		}
	}
}
