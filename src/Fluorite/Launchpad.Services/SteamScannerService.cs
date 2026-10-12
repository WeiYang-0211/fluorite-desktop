using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Launchpad.Models;
using Microsoft.Win32;

namespace Launchpad.Services;

public class SteamScannerService : ISteamScannerService
{
	private static readonly HashSet<string> ExcludedAppIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "228980", "1070560", "1391110", "1628350", "1826330", "250820" };

	private static readonly string[] ExcludedNamePatterns = new string[7] { "Steamworks", "Soundtrack", "Dedicated Server", "Editor", "SDK", "Proton", "Runtime" };

	private readonly ConcurrentDictionary<string, string> _installDirCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	public async Task<List<AppItem>> ScanSteamGamesAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return await Task.Run(() =>
		{
			List<AppItem> list = new List<AppItem>();
			string text = FindSteamPath();
			if (string.IsNullOrEmpty(text) || !Directory.Exists(text))
			{
				return list;
			}
			List<string> libraryFolders = GetLibraryFolders(text);
			foreach (string item in libraryFolders)
			{
				cancellationToken.ThrowIfCancellationRequested();
				string commandLinePath = Path.Combine(item, "steamapps");
				if (Directory.Exists(commandLinePath))
				{
					string[] files;
					try
					{
						files = Directory.GetFiles(commandLinePath, "appmanifest_*.acf", SearchOption.TopDirectoryOnly);
					}
					catch (Exception ex)
					{
						App.Log("[STEAM] Error reading manifest files in " + commandLinePath + ": " + ex.Message);
						continue;
					}
					string[] entries = files;
					foreach (string text3 in entries)
					{
						cancellationToken.ThrowIfCancellationRequested();
						try
						{
							AppItem appItem = ParseManifest(text3, text, commandLinePath);
							if (appItem != null)
							{
								list.Add(appItem);
							}
						}
						catch (Exception ex2)
						{
							App.Log("[STEAM] Error parsing manifest '" + text3 + "': " + ex2.Message);
						}
					}
				}
			}
			App.Log($"[STEAM] Scanned {list.Count} installed games from {libraryFolders.Count} Steam library folders.");
			return list;
		}, cancellationToken);
	}

	public string? FindGameInstallDirectory(string appId)
	{
		if (string.IsNullOrWhiteSpace(appId))
		{
			return null;
		}
		if (_installDirCache.TryGetValue(appId, out string value) && Directory.Exists(value))
		{
			return value;
		}
		string text = FindSteamPath();
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		foreach (string libraryFolder in GetLibraryFolders(text))
		{
			string commandLinePath = Path.Combine(libraryFolder, "steamapps", "appmanifest_" + appId + ".acf");
			if (File.Exists(commandLinePath) && ParseManifestInfo(commandLinePath).TryGetValue("installdir", out string failure))
			{
				string text3 = Path.Combine(libraryFolder, "steamapps", "common", failure);
				if (Directory.Exists(text3))
				{
					_installDirCache[appId] = text3;
					return text3;
				}
			}
		}
		return null;
	}

	public static string? FindSteamPath()
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam");
			string text = registryKey?.GetValue("SteamPath")?.ToString();
			if (!string.IsNullOrEmpty(text))
			{
				text = text.Replace('/', Path.DirectorySeparatorChar);
				if (Directory.Exists(text))
				{
					return text;
				}
			}
		}
		catch
		{
		}
		try
		{
			using RegistryKey registryKey2 = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Valve\\Steam");
			string commandLinePath = registryKey2?.GetValue("InstallPath")?.ToString();
			if (!string.IsNullOrEmpty(commandLinePath))
			{
				commandLinePath = commandLinePath.Replace('/', Path.DirectorySeparatorChar);
				if (Directory.Exists(commandLinePath))
				{
					return commandLinePath;
				}
			}
		}
		catch
		{
		}
		string text3 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
		if (!Directory.Exists(text3))
		{
			return null;
		}
		return text3;
	}

	private static List<string> GetLibraryFolders(string steamPath)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamPath };
		string path = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
		if (!File.Exists(path))
		{
			return hashSet.ToList();
		}
		try
		{
			string input = File.ReadAllText(path);
			foreach (Match item in Regex.Matches(input, "\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase))
			{
				if (item.Groups.Count > 1)
				{
					string text = item.Groups[1].Value.Replace("\\\\", "\\").Trim();
					if (Directory.Exists(text))
					{
						hashSet.Add(text);
					}
				}
			}
			foreach (Match item2 in Regex.Matches(input, "\"\\d+\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase))
			{
				if (item2.Groups.Count > 1)
				{
					string commandLinePath = item2.Groups[1].Value.Replace("\\\\", "\\").Trim();
					if (Directory.Exists(commandLinePath) && !hashSet.Contains(commandLinePath))
					{
						hashSet.Add(commandLinePath);
					}
				}
			}
		}
		catch (Exception ex)
		{
			App.Log("[STEAM] Error parsing libraryfolders.vdf: " + ex.Message);
		}
		return hashSet.ToList();
	}

	private AppItem? ParseManifest(string manifestPath, string steamPath, string steamAppsDir)
	{
		Dictionary<string, string> dictionary = ParseManifestInfo(manifestPath);
		if (!dictionary.TryGetValue("appid", out var value) || string.IsNullOrWhiteSpace(value))
		{
			return null;
		}
		if (ExcludedAppIds.Contains(value))
		{
			return null;
		}
		if (!dictionary.TryGetValue("name", out var failure) || string.IsNullOrWhiteSpace(failure))
		{
			return null;
		}
		string[] excludedNamePatterns = ExcludedNamePatterns;
		foreach (string value3 in excludedNamePatterns)
		{
			if (failure.IndexOf(value3, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return null;
			}
		}
		dictionary.TryGetValue("installdir", out var value4);
		string text = ((!string.IsNullOrEmpty(value4)) ? Path.Combine(steamAppsDir, "common", value4) : string.Empty);
		if (!string.IsNullOrEmpty(text) && Directory.Exists(text))
		{
			_installDirCache[value] = text;
		}
		string iconPath = ResolveGameIcon(steamPath, text, value);
		return new AppItem
		{
			Name = CleanGameName(failure),
			ExecutablePath = "steam://rungameid/" + value,
			Arguments = value,
			IconPath = iconPath,
			IconIndex = 0,
			Source = "Steam",
			Category = "游戏"
		};
	}

	private static Dictionary<string, string> ParseManifestInfo(string manifestPath)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			string[] entries = File.ReadAllLines(manifestPath);
			for (int i = 0; i < entries.Length; i++)
			{
				Match match = Regex.Match(entries[i], "^\\s*\"([^\"]+)\"\\s+\"([^\"]*)\"");
				if (match.Success)
				{
					dictionary[match.Groups[1].Value] = match.Groups[2].Value;
				}
			}
		}
		catch
		{
		}
		return dictionary;
	}

	private static string ResolveGameIcon(string steamPath, string gameInstallDir, string appId)
	{
		string text = Path.Combine(steamPath, "appcache", "librarycache", appId + "_icon.jpg");
		if (File.Exists(text))
		{
			return text;
		}
		string path = Path.Combine(steamPath, "appcache", "librarycache", appId);
		if (Directory.Exists(path))
		{
			try
			{
				string[] files = Directory.GetFiles(path, "*.jpg", SearchOption.TopDirectoryOnly);
				if (files.Length != 0)
				{
					return files[0];
				}
			}
			catch
			{
			}
		}
		string commandLinePath = Path.Combine(steamPath, "appcache", "librarycache", appId + "_header.jpg");
		if (File.Exists(commandLinePath))
		{
			return commandLinePath;
		}
		if (!string.IsNullOrEmpty(gameInstallDir) && Directory.Exists(gameInstallDir))
		{
			try
			{
				List<string> list = (from f in Directory.GetFiles(gameInstallDir, "*.exe", SearchOption.TopDirectoryOnly)
					where !IsUtilityExe(Path.GetFileName(f))
					orderby new FileInfo(f).Length descending
					select f).ToList();
				if (list.Count > 0)
				{
					return list[0];
				}
				List<string> list2 = (from f in Directory.GetFiles(gameInstallDir, "*.exe", SearchOption.AllDirectories)
					where !IsUtilityExe(Path.GetFileName(f))
					orderby new FileInfo(f).Length descending
					select f).Take(5).ToList();
				if (list2.Count > 0)
				{
					return list2[0];
				}
			}
			catch
			{
			}
		}
		string text3 = Path.Combine(steamPath, "steam.exe");
		if (!File.Exists(text3))
		{
			return string.Empty;
		}
		return text3;
	}

	private static bool IsUtilityExe(string fileName)
	{
		string text = fileName.ToLowerInvariant();
		if (!text.Contains("crash") && !text.Contains("report") && !text.Contains("update") && !text.Contains("unins") && !text.Contains("setup"))
		{
			if (text.Contains("launcher"))
			{
				return !text.Equals("launcher.exe", StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}
		return true;
	}

	private static string CleanGameName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return "Unknown Game";
		}
		return name.Replace("™", "").Replace("®", "").Trim();
	}
}
