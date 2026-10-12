using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Launchpad.Interop;
using Launchpad.Models;
using Microsoft.Win32;

namespace Launchpad.Services;

public class AppScannerService : IAppScannerService
{
	private static readonly string[] ExcludeKeywords = new string[70]
	{
		"uninstall", "卸载", "反安装", "unins000", "remove", "setup", "update", "updater", "installer", "documentation",
		"readme", "license", "help", "website", "support", "faq", "faqs", "release notes", "tutorial", "manual",
		"quickstart", "changelog", "user guide", "getting started", "参考文档", "文档", "帮助", "手册", "指南", "教程",
		"使用说明", "常见问题", "更新日志", "许可协议", "帮助文档", "使用手册", "客服", "支持", "native tools", "cross tools",
		"developer command prompt", "developer powershell", "command line client", "node.js command prompt", "debuggable package", "sdk command", "command prompt for vs", "powershell for vs", "visual studio command prompt", "prompt for vs",
		"vcvars", "nodevars", "administrative tools", "windows 管理工具", "windows 工具", "事件查看器", "本地安全策略", "性能监视器", "任务计划程序", "资源监视器",
		"计算机管理", "组件服务", "数据源 (odbc)", "打印管理", "内存诊断", "系统配置", "odbc data sources", "recovery drive", "system information", "windows memory diagnostic"
	};

	private static readonly HashSet<string> ValidExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".exe", ".bat", ".cmd", ".com", ".msi" };

	private readonly ISteamScannerService _steamScanner;

	public ISteamScannerService SteamScanner => _steamScanner;

	internal static AppItem CreateThisPcApplication()
	{
		return new AppItem
		{
			Name = "此电脑",
			ExecutablePath = "shell:MyComputerFolder",
			IconPath = "shell:MyComputerFolder",
			Source = "Shell",
			Category = "General"
		};
	}

	public static bool IsFluoriteApplication(AppItem app)
	{
		string text = app.ExecutablePath?.Trim().Trim('"') ?? string.Empty;
		if (!string.Equals(Path.GetFileName(text), "Fluorite.exe", StringComparison.OrdinalIgnoreCase))
		{
			if (string.Equals(app.Name?.Trim(), "Fluorite", StringComparison.OrdinalIgnoreCase))
			{
				return text.StartsWith("shell:", StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}
		return true;
	}

	public AppScannerService(ISteamScannerService? steamScanner = null)
	{
		_steamScanner = steamScanner ?? new SteamScannerService();
	}

	public async Task<List<AppItem>> ScanApplicationsAsync(IEnumerable<AppItem>? existingApps = null, IEnumerable<AppItem>? manualApps = null, bool scanSteam = true, CancellationToken cancellationToken = default(CancellationToken))
	{
		return await Task.Run(() =>
		{
			Dictionary<string, AppItem> dictionary = new Dictionary<string, AppItem>(StringComparer.OrdinalIgnoreCase);
			if (existingApps != null)
			{
				foreach (AppItem existingApp in existingApps)
				{
					if (!string.IsNullOrEmpty(existingApp.ExecutablePath))
					{
						dictionary[existingApp.ExecutablePath] = existingApp;
					}
					if (!string.IsNullOrEmpty(existingApp.Name) && !dictionary.ContainsKey(existingApp.Name))
					{
						dictionary[existingApp.Name] = existingApp;
					}
				}
			}
			List<AppItem> list = new List<AppItem>();
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string[] entries = new string[2]
			{
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft\\Windows\\Start Menu\\Programs"),
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft\\Windows\\Start Menu\\Programs")
			};
			foreach (string dirPath in entries)
			{
				cancellationToken.ThrowIfCancellationRequested();
				ScanDirectory(dirPath, "StartMenu", list, hashSet, cancellationToken);
			}
			entries = new string[2]
			{
				Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
				Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
			};
			foreach (string dirPath2 in entries)
			{
				cancellationToken.ThrowIfCancellationRequested();
				ScanDirectory(dirPath2, "Desktop", list, hashSet, cancellationToken);
			}
			cancellationToken.ThrowIfCancellationRequested();
			ScanAppsFolder(list, hashSet, cancellationToken);
			ScanRegisteredApplications(list, hashSet, cancellationToken);
			AppItem appItem = CreateThisPcApplication();
			if (hashSet.Add(appItem.ExecutablePath))
			{
				hashSet.Add(appItem.Name);
				list.Add(appItem);
			}
			if (scanSteam)
			{
				cancellationToken.ThrowIfCancellationRequested();
				try
				{
					foreach (AppItem game in _steamScanner.ScanSteamGamesAsync(cancellationToken).GetAwaiter().GetResult())
					{
						cancellationToken.ThrowIfCancellationRequested();
						string item = game.Name.ToLowerInvariant();
						string item2 = game.ExecutablePath.ToLowerInvariant();
						if (!hashSet.Contains(item2))
						{
							if (hashSet.Contains(item))
							{
								AppItem appItem2 = list.FirstOrDefault((AppItem a) => string.Equals(a.Name, game.Name, StringComparison.OrdinalIgnoreCase));
								if (appItem2 != null)
								{
									if (string.IsNullOrEmpty(appItem2.IconPath) && !string.IsNullOrEmpty(game.IconPath))
									{
										appItem2.IconPath = game.IconPath;
									}
									continue;
								}
							}
							hashSet.Add(item2);
							hashSet.Add(item);
							list.Add(game);
						}
					}
				}
				catch (Exception ex)
				{
					App.Log("[STEAM] Error integrating Steam games during scan: " + ex.Message);
				}
			}
			if (manualApps != null)
			{
				foreach (AppItem manualApp in manualApps)
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (!string.IsNullOrEmpty(manualApp.ExecutablePath))
					{
						string item3 = manualApp.ExecutablePath.ToLowerInvariant();
						if (!hashSet.Contains(item3) && (manualApp.ExecutablePath.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) || File.Exists(manualApp.ExecutablePath) || Directory.Exists(manualApp.ExecutablePath)))
						{
							hashSet.Add(item3);
							if (!string.IsNullOrEmpty(manualApp.Name))
							{
								hashSet.Add(manualApp.Name.ToLowerInvariant());
							}
							list.Add(manualApp);
						}
					}
				}
			}
			RetainShortcutTargets(dictionary.Values.Distinct(), list, hashSet, cancellationToken);
			list.RemoveAll(IsFluoriteApplication);
			int pageNumber = ((dictionary.Values.Count > 0) ? (dictionary.Values.Max((AppItem a) => a.SortIndex) + 1) : 0);
			foreach (AppItem item4 in list)
			{
				if (dictionary.TryGetValue(item4.ExecutablePath, out var value) || dictionary.TryGetValue(item4.Name, out value))
				{
					item4.Id = value.Id;
					item4.IsDock = value.IsDock;
					item4.DockIndex = value.DockIndex;
					item4.SortIndex = value.SortIndex;
					item4.LastUsedTime = value.LastUsedTime;
					item4.FolderId = value.FolderId;
					item4.IsHidden = value.IsHidden;
					item4.IconOverridePath = value.IconOverridePath;
					item4.CustomName = value.CustomName;
					item4.SystemToolGroupingHandled = value.SystemToolGroupingHandled;
				}
				else
				{
					item4.SortIndex = pageNumber++;
				}
			}
			return (from a in list
				orderby a.IsDock descending, a.SortIndex
				select a).ThenBy((AppItem a) => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
		}, cancellationToken);
	}

	internal static void RetainShortcutTargets(IEnumerable<AppItem> existingApps, List<AppItem> scannedApps, HashSet<string> seenKeys, CancellationToken cancellationToken)
	{
		foreach (AppItem existingApp in existingApps)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string source = existingApp.Source;
			bool validated = ((source == "Desktop" || source == "StartMenu") ? true : false);
			if (validated && !existingApp.IsFolder && !existingApp.IsWidget && !string.IsNullOrWhiteSpace(existingApp.ExecutablePath) && !IsFluoriteApplication(existingApp) && ValidExtensions.Contains(Path.GetExtension(existingApp.ExecutablePath)) && File.Exists(existingApp.ExecutablePath) && seenKeys.Add(existingApp.ExecutablePath))
			{
				if (!string.IsNullOrWhiteSpace(existingApp.Name))
				{
					seenKeys.Add(existingApp.Name);
				}
				scannedApps.Add(existingApp);
			}
		}
	}

	public AppItem? CreateAppFromPath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return null;
		}
		bool validated = File.Exists(path);
		bool flag2 = Directory.Exists(path);
		if (!validated && !flag2)
		{
			return null;
		}
		if (flag2)
		{
			path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
		}
		string text = (flag2 ? Path.GetFileName(path) : Path.GetFileNameWithoutExtension(path));
		if (string.IsNullOrEmpty(text))
		{
			text = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
		}
		if (string.IsNullOrEmpty(text))
		{
			text = path;
		}
		string extension = Path.GetExtension(path);
		if (validated && extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
		{
			var (commandLinePath, arguments, text3, iconIndex) = ShellLinkHelper.ResolveShortcut(path);
			if (!string.IsNullOrWhiteSpace(commandLinePath) && (File.Exists(commandLinePath) || Directory.Exists(commandLinePath)))
			{
				return new AppItem
				{
					Name = CleanDisplayName(text),
					ExecutablePath = commandLinePath,
					Arguments = arguments,
					IconPath = (string.IsNullOrWhiteSpace(text3) ? commandLinePath : text3),
					IconIndex = iconIndex,
					Source = "Manual",
					Category = DetectCategory(commandLinePath, text)
				};
			}
			return new AppItem
			{
				Name = CleanDisplayName(text),
				ExecutablePath = path,
				Arguments = string.Empty,
				IconPath = path,
				IconIndex = 0,
				Source = "Manual",
				Category = DetectCategory(path, text)
			};
		}
		return new AppItem
		{
			Name = CleanDisplayName(text),
			ExecutablePath = path,
			Arguments = string.Empty,
			IconPath = path,
			IconIndex = 0,
			Source = "Manual",
			Category = DetectCategory(path, text)
		};
	}

	private void ScanDirectory(string dirPath, string source, List<AppItem> result, HashSet<string> seenKeys, CancellationToken ct)
	{
		if (!Directory.Exists(dirPath))
		{
			return;
		}
		try
		{
			string[] files = Directory.GetFiles(dirPath, "*.lnk", SearchOption.AllDirectories);
			foreach (string text in files)
			{
				ct.ThrowIfCancellationRequested();
				try
				{
					ProcessLnkFile(text, source, result, seenKeys);
				}
				catch (Exception ex)
				{
					App.Log("Error processing shortcut '" + text + "': " + ex.Message);
				}
			}
			files = Directory.GetFiles(dirPath, "*.url", SearchOption.AllDirectories);
			foreach (string commandLinePath in files)
			{
				ct.ThrowIfCancellationRequested();
				AppItem appItem = CreateAppFromPath(commandLinePath);
				if (appItem != null && WebShortcutIcon.IsWeb(appItem) && seenKeys.Add(commandLinePath.ToLowerInvariant()))
				{
					appItem.Source = source;
					result.Add(appItem);
				}
			}
			files = Directory.GetFiles(dirPath, "*.exe", SearchOption.TopDirectoryOnly);
			foreach (string text3 in files)
			{
				ct.ThrowIfCancellationRequested();
				try
				{
					ProcessExeFile(text3, source, result, seenKeys);
				}
				catch (Exception ex2)
				{
					App.Log("Error processing executable '" + text3 + "': " + ex2.Message);
				}
			}
		}
		catch (Exception ex3)
		{
			App.Log("Error enumerating directory '" + dirPath + "': " + ex3.Message);
		}
	}

	private void ProcessLnkFile(string lnkPath, string source, List<AppItem> result, HashSet<string> seenKeys)
	{
		if (lnkPath.Contains("Administrative Tools", StringComparison.OrdinalIgnoreCase) || lnkPath.Contains("Windows 管理工具", StringComparison.OrdinalIgnoreCase) || lnkPath.Contains("Windows 工具", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(lnkPath);
		if (ShouldExclude(fileNameWithoutExtension))
		{
			return;
		}
		(string targetPath, string arguments, string iconPath, int iconIndex) tuple = ShellLinkHelper.ResolveShortcut(lnkPath);
		string item = tuple.targetPath;
		string item2 = tuple.arguments;
		string item3 = tuple.iconPath;
		int item4 = tuple.iconIndex;
		AppItem appItem = new AppItem
		{
			Name = CleanDisplayName(fileNameWithoutExtension),
			ExecutablePath = lnkPath,
			Arguments = "",
			IconPath = lnkPath,
			Source = source
		};
		if (WebShortcutIcon.IsWeb(appItem))
		{
			if (seenKeys.Add(lnkPath.ToLowerInvariant()))
			{
				result.Add(appItem);
			}
		}
		else
		{
			if (string.IsNullOrWhiteSpace(item))
			{
				return;
			}
			string extension = Path.GetExtension(item);
			if (!ValidExtensions.Contains(extension) || !File.Exists(item) || ShouldExclude(Path.GetFileNameWithoutExtension(item)))
			{
				return;
			}
			string fileName = Path.GetFileName(item);
			if ((!fileName.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase) && !fileName.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase)) || (string.IsNullOrWhiteSpace(item2) && !fileNameWithoutExtension.Contains("prompt", StringComparison.OrdinalIgnoreCase) && !fileNameWithoutExtension.Contains("tools", StringComparison.OrdinalIgnoreCase) && !fileNameWithoutExtension.Contains("client", StringComparison.OrdinalIgnoreCase)))
			{
				string item5 = item.ToLowerInvariant();
				if (!seenKeys.Contains(item5) && !seenKeys.Contains(fileNameWithoutExtension.ToLowerInvariant()))
				{
					seenKeys.Add(item5);
					seenKeys.Add(fileNameWithoutExtension.ToLowerInvariant());
					AppItem item6 = new AppItem
					{
						Name = CleanDisplayName(fileNameWithoutExtension),
						ExecutablePath = item,
						Arguments = item2,
						IconPath = (string.IsNullOrWhiteSpace(item3) ? item : item3),
						IconIndex = item4,
						Source = source,
						Category = DetectCategory(item, fileNameWithoutExtension)
					};
					result.Add(item6);
				}
			}
		}
	}

	private void ProcessExeFile(string exePath, string source, List<AppItem> result, HashSet<string> seenKeys)
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(exePath);
		if (!ShouldExclude(fileNameWithoutExtension))
		{
			string item = exePath.ToLowerInvariant();
			if (!seenKeys.Contains(item) && !seenKeys.Contains(fileNameWithoutExtension.ToLowerInvariant()))
			{
				seenKeys.Add(item);
				seenKeys.Add(fileNameWithoutExtension.ToLowerInvariant());
				AppItem item2 = new AppItem
				{
					Name = CleanDisplayName(fileNameWithoutExtension),
					ExecutablePath = exePath,
					Arguments = string.Empty,
					IconPath = exePath,
					IconIndex = 0,
					Source = source,
					Category = DetectCategory(exePath, fileNameWithoutExtension)
				};
				result.Add(item2);
			}
		}
	}

	private void ScanRegisteredApplications(List<AppItem> result, HashSet<string> seenKeys, CancellationToken ct)
	{
		RegistryHive[] entries = new RegistryHive[2]
		{
			RegistryHive.CurrentUser,
			RegistryHive.LocalMachine
		};
		foreach (RegistryHive registryHive in entries)
		{
			RegistryView[] buffer = new RegistryView[2]
			{
				RegistryView.Registry64,
				RegistryView.Registry32
			};
			foreach (RegistryView registryView in buffer)
			{
				ct.ThrowIfCancellationRequested();
				try
				{
					using RegistryKey registryKey = RegistryKey.OpenBaseKey(registryHive, registryView);
					using RegistryKey registryKey2 = registryKey.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths");
					if (registryKey2 == null)
					{
						continue;
					}
					string[] subKeyNames = registryKey2.GetSubKeyNames();
					foreach (string text in subKeyNames)
					{
						ct.ThrowIfCancellationRequested();
						try
						{
							using RegistryKey registryKey3 = registryKey2.OpenSubKey(text);
							string commandLinePath = Environment.ExpandEnvironmentVariables((registryKey3?.GetValue(null) as string) ?? string.Empty).Trim().Trim('"');
							if (!Path.IsPathFullyQualified(commandLinePath) || !commandLinePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(commandLinePath))
							{
								continue;
							}
							string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(commandLinePath);
							if (ShouldExclude(fileNameWithoutExtension) || seenKeys.Contains(commandLinePath))
							{
								continue;
							}
							string name = fileNameWithoutExtension;
							try
							{
								FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(commandLinePath);
								if (!string.IsNullOrWhiteSpace(versionInfo.FileDescription))
								{
									name = versionInfo.FileDescription.Trim();
								}
								else if (!string.IsNullOrWhiteSpace(versionInfo.ProductName))
								{
									name = versionInfo.ProductName.Trim();
								}
							}
							catch (Exception ex)
							{
								App.Log("Error reading application name '" + commandLinePath + "': " + ex.Message);
							}
							name = CleanDisplayName(name);
							if (!ShouldExclude(name) && !seenKeys.Contains(name) && !seenKeys.Contains(fileNameWithoutExtension))
							{
								seenKeys.Add(commandLinePath);
								seenKeys.Add(name);
								seenKeys.Add(fileNameWithoutExtension);
								result.Add(new AppItem
								{
									Name = name,
									ExecutablePath = commandLinePath,
									IconPath = commandLinePath,
									Source = "Registry",
									Category = DetectCategory(commandLinePath, name)
								});
							}
						}
						catch (Exception ex2)
						{
							App.Log("Error scanning registered application '" + text + "': " + ex2.Message);
						}
					}
				}
				catch (Exception ex3) when (!(ex3 is OperationCanceledException))
				{
					App.Log($"Error scanning registered applications ({registryHive}, {registryView}): {ex3.Message}");
				}
			}
		}
	}

	private void ScanAppsFolder(List<AppItem> result, HashSet<string> seenKeys, CancellationToken ct)
	{
		try
		{
			Type typeFromProgID = Type.GetTypeFromProgID("Shell.Application");
			if (typeFromProgID == null)
			{
				return;
			}
			dynamic val = Activator.CreateInstance(typeFromProgID);
			if (val == null)
			{
				return;
			}
			dynamic val2 = val.NameSpace("shell:AppsFolder");
			if (val2 == null)
			{
				return;
			}
			foreach (dynamic item3 in val2.Items())
			{
				ct.ThrowIfCancellationRequested();
				try
				{
					string text = item3.Name?.ToString() ?? string.Empty;
					string commandLinePath = item3.Path?.ToString() ?? string.Empty;
					if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(commandLinePath) && !ShouldExclude(text) && !ShouldExclude(commandLinePath) && !commandLinePath.StartsWith("{") && !commandLinePath.Contains("_{"))
					{
						string item = text.ToLowerInvariant();
						if (!seenKeys.Contains(item) && !seenKeys.Contains(commandLinePath.ToLowerInvariant()))
						{
							seenKeys.Add(item);
							seenKeys.Add(commandLinePath.ToLowerInvariant());
							AppItem item2 = new AppItem
							{
								Name = CleanDisplayName(text),
								ExecutablePath = commandLinePath,
								Source = (commandLinePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? "Desktop" : "UWP"),
								Category = "Windows App"
							};
							result.Add(item2);
						}
					}
				}
				catch
				{
				}
			}
		}
		catch (Exception ex)
		{
			App.Log("Error scanning AppsFolder: " + ex.Message);
		}
	}

	private static bool ShouldExclude(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return true;
		}
		return ExcludeKeywords.Any((string k) => name.Contains(k, StringComparison.OrdinalIgnoreCase));
	}

	private static string CleanDisplayName(string name)
	{
		return name.Replace(" - 快捷方式", "").Replace(" - Shortcut", "").Trim();
	}

	private static string DetectCategory(string path, string name)
	{
		string text = (path + " " + name).ToLowerInvariant();
		if (text.Contains("chrome") || text.Contains("edge") || text.Contains("browser") || text.Contains("firefox"))
		{
			return "Browser";
		}
		if (text.Contains("code") || text.Contains("studio") || text.Contains("git") || text.Contains("terminal") || text.Contains("cursor"))
		{
			return "Developer";
		}
		if (text.Contains("office") || text.Contains("word") || text.Contains("excel") || text.Contains("wps") || text.Contains("note"))
		{
			return "Productivity";
		}
		if (text.Contains("music") || text.Contains("video") || text.Contains("player") || text.Contains("media") || text.Contains("photo"))
		{
			return "Media";
		}
		if (text.Contains("game") || text.Contains("steam") || text.Contains("wegame"))
		{
			return "Games";
		}
		return "General";
	}
}
