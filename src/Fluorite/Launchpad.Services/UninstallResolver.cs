using System;
using System.IO;
using Microsoft.Win32;

namespace Launchpad.Services;

internal static class UninstallResolver
{
	private const string UninstallPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall";

	public static string? FindUninstallCommand(string executablePath, string displayName)
	{
		string text = Expand(executablePath);
		string directory = GetDirectory(text);
		string appName = (displayName ?? string.Empty).Trim();
		if (IsUnderWindowsFolder(directory))
		{
			return null;
		}
		string result = null;
		int pageNumber = 0;
		(RegistryHive, RegistryView)[] entries = new (RegistryHive, RegistryView)[3]
		{
			(RegistryHive.CurrentUser, RegistryView.Default),
			(RegistryHive.LocalMachine, RegistryView.Registry64),
			(RegistryHive.LocalMachine, RegistryView.Registry32)
		};
		for (int i = 0; i < entries.Length; i++)
		{
			var (registryHive, registryView) = entries[i];
			try
			{
				using RegistryKey registryKey = RegistryKey.OpenBaseKey(registryHive, registryView);
				using RegistryKey registryKey2 = registryKey.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
				if (registryKey2 == null)
				{
					continue;
				}
				string[] subKeyNames = registryKey2.GetSubKeyNames();
				foreach (string commandLinePath in subKeyNames)
				{
					try
					{
						using RegistryKey registryKey3 = registryKey2.OpenSubKey(commandLinePath);
						if (registryKey3 == null || (registryKey3.GetValue("SystemComponent") is int settingIndex && settingIndex == 1))
						{
							continue;
						}
						string text3 = registryKey3.GetValue("DisplayName") as string;
						if (string.IsNullOrWhiteSpace(text3))
						{
							continue;
						}
						string text4 = registryKey3.GetValue("UninstallString") as string;
						if (string.IsNullOrWhiteSpace(text4))
						{
							text4 = registryKey3.GetValue("QuietUninstallString") as string;
						}
						if (!string.IsNullOrWhiteSpace(text4))
						{
							int targetPage = Score(registryKey3, text3, text4, text, directory, appName);
							if (targetPage > pageNumber)
							{
								pageNumber = targetPage;
								result = text4;
							}
						}
					}
					catch (Exception ex)
					{
						App.Log("Uninstall scan skipped '" + commandLinePath + "': " + ex.Message);
					}
				}
			}
			catch (Exception ex2)
			{
				App.Log($"Uninstall hive {registryHive}/{registryView} unreadable: {ex2.Message}");
			}
		}
		if (pageNumber < 40)
		{
			return null;
		}
		return result;
	}

	private static int Score(RegistryKey entry, string product, string command, string exePath, string exeDir, string appName)
	{
		int pageNumber = 0;
		string directory = GetDirectory(StripQuotes(SplitCommand(command).file));
		if (IsUnderWindowsFolder(directory) || IsUnderWindowsFolder(exeDir))
		{
			return 0;
		}
		if (!string.IsNullOrEmpty(exeDir) && entry.GetValue("InstallLocation") is string text && !string.IsNullOrWhiteSpace(text))
		{
			string commandLinePath = Expand(text).TrimEnd('\\', '/');
			if (exeDir.Equals(commandLinePath, StringComparison.OrdinalIgnoreCase))
			{
				return 200;
			}
			if (exeDir.StartsWith(commandLinePath + "\\", StringComparison.OrdinalIgnoreCase) || commandLinePath.StartsWith(exeDir + "\\", StringComparison.OrdinalIgnoreCase))
			{
				pageNumber = Math.Max(pageNumber, 150);
			}
		}
		if (entry.GetValue("DisplayIcon") is string icon)
		{
			string text3 = Expand(StripIconIndex(icon));
			if (text3.Equals(exePath, StringComparison.OrdinalIgnoreCase))
			{
				pageNumber = Math.Max(pageNumber, 120);
			}
			else if (!string.IsNullOrEmpty(exeDir) && GetDirectory(text3).Equals(exeDir, StringComparison.OrdinalIgnoreCase))
			{
				pageNumber = Math.Max(pageNumber, 110);
			}
		}
		if (product.Equals(appName, StringComparison.OrdinalIgnoreCase))
		{
			pageNumber = Math.Max(pageNumber, 90);
		}
		else if (!string.IsNullOrEmpty(appName) && (product.StartsWith(appName, StringComparison.OrdinalIgnoreCase) || appName.StartsWith(product, StringComparison.OrdinalIgnoreCase)))
		{
			pageNumber = Math.Max(pageNumber, 60);
		}
		else if (!string.IsNullOrEmpty(appName) && (product.Contains(appName, StringComparison.OrdinalIgnoreCase) || appName.Contains(product, StringComparison.OrdinalIgnoreCase)))
		{
			pageNumber = Math.Max(pageNumber, 40);
		}
		if (!string.IsNullOrEmpty(exeDir) && directory.Equals(exeDir, StringComparison.OrdinalIgnoreCase))
		{
			pageNumber = Math.Max(pageNumber, 100);
		}
		return pageNumber;
	}

	private static bool IsUnderWindowsFolder(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return false;
		}
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
		if (string.IsNullOrEmpty(folderPath))
		{
			return false;
		}
		try
		{
			return path.TrimEnd('\\', '/').StartsWith(folderPath.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	public static (string file, string args) SplitCommand(string commandLine)
	{
		string text = (commandLine ?? string.Empty).Trim();
		if (text.Length == 0)
		{
			return (file: string.Empty, args: string.Empty);
		}
		if (text[0] == '"')
		{
			int pageNumber = text.IndexOf('"', 1);
			if (pageNumber > 1)
			{
				return (file: text.Substring(1, pageNumber - 1), args: text.Substring(Math.Min(pageNumber + 1, text.Length)).Trim());
			}
			return (file: text.Trim('"'), args: string.Empty);
		}
		int settingIndex = text.IndexOf(' ');
		if (settingIndex < 0)
		{
			return (file: text, args: string.Empty);
		}
		string commandLinePath = text.Substring(0, settingIndex);
		if (commandLinePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
		{
			return (file: commandLinePath, args: text.Substring(settingIndex + 1).Trim());
		}
		int targetPage = text.IndexOf(' ', settingIndex + 1);
		if (targetPage >= 0)
		{
			return (file: text.Substring(0, targetPage), args: text.Substring(targetPage + 1).Trim());
		}
		return (file: text, args: string.Empty);
	}

	private static string StripIconIndex(string icon)
	{
		int pageNumber = icon.LastIndexOf(',');
		if (pageNumber > 1 && pageNumber < icon.Length - 1 && int.TryParse(icon.Substring(pageNumber + 1).Trim(), out var _))
		{
			return icon.Substring(0, pageNumber);
		}
		return icon;
	}

	private static string StripQuotes(string value)
	{
		return value.Trim('"');
	}

	private static string Expand(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return string.Empty;
		}
		try
		{
			return Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
		}
		catch
		{
			return path;
		}
	}

	private static string GetDirectory(string path)
	{
		try
		{
			return Path.GetDirectoryName(Expand(path)) ?? string.Empty;
		}
		catch
		{
			return string.Empty;
		}
	}
}
