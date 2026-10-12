using System;
using System.IO;

namespace Launchpad.Services;

internal static class LeagueWindowIdentity
{
	private static readonly string[] WindowPaths = new string[6] { "Game\\League of Legends.exe", "WeGameLauncher\\launcher.exe", "Launcher\\Client.exe", "TCLS\\client.exe", "LeagueClient.exe", "LeagueClientUx.exe" };

	internal static string? InstallRoot(string path)
	{
		try
		{
			path = Path.GetFullPath(path);
			string[] windowPaths = WindowPaths;
			foreach (string text in windowPaths)
			{
				string commandLinePath = Path.DirectorySeparatorChar + text;
				if (path.EndsWith(commandLinePath, StringComparison.OrdinalIgnoreCase))
				{
					string text3 = path;
					int length = commandLinePath.Length;
					string text4 = text3.Substring(0, text3.Length - length);
					string fileName = Path.GetFileName(text4);
					if (text.StartsWith("Game\\", StringComparison.OrdinalIgnoreCase) || text.StartsWith("LeagueClient", StringComparison.OrdinalIgnoreCase) || fileName.Equals("英雄联盟", StringComparison.OrdinalIgnoreCase) || fileName.Equals("League of Legends", StringComparison.OrdinalIgnoreCase) || File.Exists(Path.Combine(text4, "Game\\League of Legends.exe")))
					{
						return text4;
					}
				}
			}
		}
		catch
		{
		}
		return null;
	}

	internal static bool Matches(string target, string process)
	{
		string text = InstallRoot(target);
		if (text != null)
		{
			return string.Equals(text, InstallRoot(process), StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	internal static int Priority(string path)
	{
		if (!path.EndsWith("Game\\League of Legends.exe", StringComparison.OrdinalIgnoreCase))
		{
			if (!Path.GetFileName(path).Equals("launcher.exe", StringComparison.OrdinalIgnoreCase))
			{
				return 2;
			}
			return 1;
		}
		return 3;
	}
}
