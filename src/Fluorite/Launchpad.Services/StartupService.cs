using System;
using System.Diagnostics;
using Launchpad.Models;
using Microsoft.Win32;

namespace Launchpad.Services;

public class StartupService : IStartupService
{
	private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

	private const string AppName = "Fluorite";

	private const string LegacyAppName = "Fluorite";

	private static bool _legacyRunChecked;

	public static bool IsTrayStartup(string[] args)
	{
		return Array.Exists(args, (string arg) => string.Equals(arg, "--startup", StringComparison.OrdinalIgnoreCase));
	}

	public static string BuildStartupCommand(string exePath)
	{
		return "\"" + exePath + "\" --startup";
	}

	public static bool ShouldShowInitially(string[] args, AppSettings settings)
	{
		if (IsTrayStartup(args))
		{
			if (settings.LaunchAtStartup)
			{
				return settings.OpenLaunchpadAtStartup;
			}
			return false;
		}
		return true;
	}

	public void MigrateLegacyRunValue()
	{
		try
		{
			if (_legacyRunChecked)
			{
				return;
			}
			_legacyRunChecked = true;
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true);
			if (registryKey == null)
			{
				return;
			}
			if (registryKey.GetValue("Fluorite") is string text && !string.IsNullOrWhiteSpace(text) && !text.Contains("--startup", StringComparison.OrdinalIgnoreCase))
			{
				registryKey.SetValue("Fluorite", text.TrimEnd() + " --startup");
			}
			if (registryKey.GetValue("Fluorite") == null)
			{
				return;
			}
			registryKey.DeleteValue("Fluorite", throwOnMissingValue: false);
			if (registryKey.GetValue("Fluorite") == null)
			{
				string commandLinePath = Process.GetCurrentProcess().MainModule?.FileName;
				if (!string.IsNullOrEmpty(commandLinePath))
				{
					registryKey.SetValue("Fluorite", BuildStartupCommand(commandLinePath));
				}
			}
			App.Log("[STARTUP] migrated Run value Launchpad -> Fluorite");
		}
		catch (Exception ex)
		{
			App.Log("Error migrating startup registry value: " + ex.Message);
		}
	}

	public bool IsStartupEnabled()
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: false);
			return registryKey?.GetValue("Fluorite") != null;
		}
		catch (Exception ex)
		{
			App.Log("Error checking startup registry: " + ex.Message);
			return false;
		}
	}

	public bool SetStartup(bool enable)
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true);
			if (registryKey == null)
			{
				return false;
			}
			if (!enable)
			{
				if (registryKey.GetValue("Fluorite") != null)
				{
					registryKey.DeleteValue("Fluorite", throwOnMissingValue: false);
				}
				return true;
			}
			string text = Process.GetCurrentProcess().MainModule?.FileName;
			if (!string.IsNullOrEmpty(text))
			{
				registryKey.SetValue("Fluorite", BuildStartupCommand(text));
				return true;
			}
		}
		catch (Exception ex)
		{
			App.Log("Error setting startup registry: " + ex.Message);
		}
		return false;
	}
}
