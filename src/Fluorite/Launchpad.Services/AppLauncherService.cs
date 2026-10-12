using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Launchpad.Models;

namespace Launchpad.Services;

public class AppLauncherService : IAppLauncherService
{
	private readonly ISteamScannerService? _steamScanner;

	public AppLauncherService(ISteamScannerService? steamScanner = null)
	{
		_steamScanner = steamScanner;
	}

	public bool Launch(AppItem app, out string errorMessage)
	{
		return LaunchInternal(app, asAdmin: false, null, out errorMessage);
	}

	public Task<(bool Success, string Error)> LaunchAsync(AppItem app)
	{
		return Task.Run(() => (success: Launch(app, out string errorMessage), error: errorMessage));
	}

	public bool LaunchWithFiles(AppItem app, IEnumerable<string> filePaths, out string errorMessage)
	{
		return LaunchInternal(app, asAdmin: false, filePaths, out errorMessage);
	}

	public bool RunAsAdmin(AppItem app, out string errorMessage)
	{
		return LaunchInternal(app, asAdmin: true, null, out errorMessage);
	}

	public bool ShowInFolder(AppItem app, out string errorMessage)
	{
		errorMessage = string.Empty;
		try
		{
			string text = Expand(app.ExecutablePath);
			if (text.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = "explorer.exe",
					Arguments = text,
					UseShellExecute = true
				});
				return true;
			}
			if (File.Exists(text))
			{
				Process.Start("explorer.exe", "/select,\"" + text + "\"");
				return true;
			}
			if (Directory.Exists(text))
			{
				Process.Start("explorer.exe", "\"" + text + "\"");
				return true;
			}
			string commandLinePath = Expand(app.IconPath);
			if (File.Exists(commandLinePath))
			{
				Process.Start("explorer.exe", "/select,\"" + commandLinePath + "\"");
				return true;
			}
			if (Directory.Exists(commandLinePath))
			{
				Process.Start("explorer.exe", "\"" + commandLinePath + "\"");
				return true;
			}
			string text3 = FindShortcutByName(app.Name);
			if (text3 != null)
			{
				Process.Start("explorer.exe", "/select,\"" + text3 + "\"");
				return true;
			}
			if (app.Source == "UWP")
			{
				Process.Start("explorer.exe", "shell:AppsFolder");
				return true;
			}
			if (app.Source == "Steam" || app.ExecutablePath.StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
			{
				string appId = ((!string.IsNullOrEmpty(app.Arguments)) ? app.Arguments : ExtractAppId(app.ExecutablePath));
				string text4 = _steamScanner?.FindGameInstallDirectory(appId);
				if (!string.IsNullOrEmpty(text4) && Directory.Exists(text4))
				{
					Process.Start("explorer.exe", "\"" + text4 + "\"");
					return true;
				}
				string text5 = Expand(app.IconPath);
				if (File.Exists(text5))
				{
					Process.Start("explorer.exe", "/select,\"" + text5 + "\"");
					return true;
				}
				string text6 = SteamScannerService.FindSteamPath();
				if (!string.IsNullOrEmpty(text6) && Directory.Exists(text6))
				{
					Process.Start("explorer.exe", "\"" + text6 + "\"");
					return true;
				}
			}
			errorMessage = "找不到 " + app.DisplayName + " 的安装位置";
			return false;
		}
		catch (Exception ex)
		{
			errorMessage = ex.Message;
			return false;
		}
	}

	public bool RequestUninstall(AppItem app, out string errorMessage)
	{
		errorMessage = string.Empty;
		try
		{
			if (app.Source == "Steam" || app.ExecutablePath.StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
			{
				string text = ((!string.IsNullOrEmpty(app.Arguments)) ? app.Arguments : ExtractAppId(app.ExecutablePath));
				if (!string.IsNullOrEmpty(text))
				{
					App.Log("Requesting uninstall for Steam game " + app.Name + ": steam://uninstall/" + text);
					Process.Start(new ProcessStartInfo
					{
						FileName = "steam://uninstall/" + text,
						UseShellExecute = true
					});
					return true;
				}
			}
			if (app.Source == "UWP")
			{
				string commandLinePath = app.ExecutablePath;
				int pageNumber = commandLinePath.IndexOf("AppsFolder\\", StringComparison.OrdinalIgnoreCase);
				if (pageNumber >= 0)
				{
					commandLinePath = commandLinePath.Substring(pageNumber + "AppsFolder\\".Length);
				}
				int settingIndex = commandLinePath.IndexOf('!');
				if (settingIndex > 0)
				{
					commandLinePath = commandLinePath.Substring(0, settingIndex);
				}
				string text3 = (commandLinePath.Contains('_') ? ("ms-settings:appsfeatures-app/" + commandLinePath) : "ms-settings:appsfeatures");
				App.Log("Uninstall request (UWP) for " + app.Name + ": " + text3);
				Process.Start(new ProcessStartInfo
				{
					FileName = text3,
					UseShellExecute = true
				});
				return true;
			}
			string text4 = UninstallResolver.FindUninstallCommand(app.ExecutablePath, app.Name);
			if (string.IsNullOrWhiteSpace(text4))
			{
				App.Log("No registry uninstall entry matched " + app.Name + ", opening apps settings");
				Process.Start(new ProcessStartInfo
				{
					FileName = "ms-settings:appsfeatures",
					UseShellExecute = true
				});
				return true;
			}
			if (text4.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
			{
				int startIndex = text4.IndexOf("msiexec", StringComparison.OrdinalIgnoreCase);
				int targetPage = text4.IndexOf(".exe", startIndex, StringComparison.OrdinalIgnoreCase);
				string text5 = ((targetPage > 0) ? text4.Substring(targetPage + 4).Trim() : string.Empty);
				App.Log("Uninstalling " + app.Name + " through msiexec: " + text5);
				Process.Start(new ProcessStartInfo
				{
					FileName = "msiexec.exe",
					Arguments = text5,
					UseShellExecute = true
				});
				return true;
			}
			var (text6, text7) = UninstallResolver.SplitCommand(Expand(text4));
			if (string.IsNullOrEmpty(text6))
			{
				errorMessage = app.DisplayName + " 的卸载命令无效";
				return false;
			}
			if (text6.Contains('\\') && !File.Exists(text6))
			{
				errorMessage = "找不到卸载程序：" + text6;
				return false;
			}
			string workingDirectory = (text6.Contains('\\') ? (Path.GetDirectoryName(text6) ?? string.Empty) : string.Empty);
			App.Log($"Uninstalling {app.Name}: {text6} {text7}");
			Process.Start(new ProcessStartInfo
			{
				FileName = text6,
				Arguments = text7,
				WorkingDirectory = workingDirectory,
				UseShellExecute = true
			});
			return true;
		}
		catch (Exception ex)
		{
			errorMessage = ex.Message;
			App.Log("Uninstall request failed for " + app.Name + ": " + ex.Message);
			return false;
		}
	}

	private static string? FindShortcutByName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return null;
		}
		string[] entries = new string[2]
		{
			Environment.GetFolderPath(Environment.SpecialFolder.Programs),
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs")
		};
		foreach (string text in entries)
		{
			try
			{
				string commandLinePath = Path.Combine(text, name + ".lnk");
				if (File.Exists(commandLinePath))
				{
					return commandLinePath;
				}
				string[] files = Directory.GetFiles(text, "*.lnk", SearchOption.AllDirectories);
				foreach (string text3 in files)
				{
					if (Path.GetFileNameWithoutExtension(text3).Equals(name, StringComparison.OrdinalIgnoreCase))
					{
						return text3;
					}
				}
			}
			catch
			{
			}
		}
		return null;
	}

	internal static ProcessStartInfo CreateFolderLaunchInfo(string path)
	{
		return new ProcessStartInfo
		{
			FileName = "explorer.exe",
			Arguments = "\"" + path + "\"",
			UseShellExecute = true
		};
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

	private bool LaunchInternal(AppItem app, bool asAdmin, IEnumerable<string>? filePaths, out string errorMessage)
	{
		errorMessage = string.Empty;
		if (string.IsNullOrWhiteSpace(app.ExecutablePath))
		{
			errorMessage = "Invalid executable path.";
			return false;
		}
		try
		{
			bool validated = filePaths?.Any() ?? false;
			bool flag2 = Directory.Exists(Expand(app.ExecutablePath));
			if (!flag2 && !asAdmin && !validated && TryActivateExistingProcess(app))
			{
				app.LastUsedTime = DateTime.UtcNow;
				return true;
			}
			ProcessStartInfo processStartInfo;
			if (flag2)
			{
				processStartInfo = CreateFolderLaunchInfo(Expand(app.ExecutablePath));
			}
			else if (app.Source == "Steam" || app.ExecutablePath.StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
			{
				processStartInfo = new ProcessStartInfo
				{
					FileName = app.ExecutablePath,
					UseShellExecute = true
				};
			}
			else if (app.ExecutablePath.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
			{
				processStartInfo = new ProcessStartInfo
				{
					FileName = "explorer.exe",
					Arguments = app.ExecutablePath,
					UseShellExecute = true
				};
			}
			else if (app.Source == "UWP" || !File.Exists(Expand(app.ExecutablePath)))
			{
				string text = (app.ExecutablePath.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase) ? app.ExecutablePath : ("shell:AppsFolder\\" + app.ExecutablePath));
				string arguments = text;
				if (validated)
				{
					string commandLinePath = string.Join(" ", from f in filePaths
						where !string.IsNullOrWhiteSpace(f)
						select "\"" + f.Trim().Trim('"') + "\"");
					if (!string.IsNullOrEmpty(commandLinePath))
					{
						arguments = text + " " + commandLinePath;
					}
				}
				processStartInfo = new ProcessStartInfo
				{
					FileName = "explorer.exe",
					Arguments = arguments,
					UseShellExecute = true
				};
			}
			else
			{
				string text3 = Expand(app.ExecutablePath);
				string text4 = null;
				try
				{
					text4 = Path.GetDirectoryName(text3);
				}
				catch
				{
				}
				string text5 = app.Arguments ?? string.Empty;
				if (validated)
				{
					string text6 = string.Join(" ", from f in filePaths
						where !string.IsNullOrWhiteSpace(f)
						select "\"" + f.Trim().Trim('"') + "\"");
					if (!string.IsNullOrEmpty(text6))
					{
						text5 = (string.IsNullOrWhiteSpace(text5) ? text6 : (text5 + " " + text6));
					}
				}
				processStartInfo = new ProcessStartInfo
				{
					FileName = text3,
					Arguments = text5,
					WorkingDirectory = ((!string.IsNullOrEmpty(text4) && Directory.Exists(text4)) ? text4 : string.Empty),
					UseShellExecute = true
				};
				if (asAdmin)
				{
					processStartInfo.Verb = "runas";
				}
			}
			using Process process = Process.Start(processStartInfo);
			App.Log($"[LAUNCH] normal entry requested: {processStartInfo.FileName}, pid={process?.Id}");
			app.LastUsedTime = DateTime.UtcNow;
			return true;
		}
		catch (Exception ex)
		{
			errorMessage = ex.Message;
			App.Log("Launch failed: " + ex.Message);
			return false;
		}
	}

	private bool TryActivateExistingProcess(AppItem app)
	{
		try
		{
			return ExistingAppWindow.TryActivate(Expand(app.ExecutablePath), app.Arguments, app.Name);
		}
		catch (InvalidOperationException)
		{
			throw;
		}
		catch
		{
		}
		return false;
	}

	private static string ExtractAppId(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return string.Empty;
		}
		int pageNumber = path.LastIndexOf('/');
		if (pageNumber < 0)
		{
			return path;
		}
		return path.Substring(pageNumber + 1);
	}
}
