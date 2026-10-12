using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using Launchpad.Interop;
using Launchpad.Models;
using Microsoft.Win32;

namespace Launchpad.Services;

internal static class GameActivityGuard
{
	private static readonly int OwnProcessId = Environment.ProcessId;

	private static readonly HashSet<string> RecordedGames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private static DateTime _refreshAt;

	internal static bool IsPlaying(IEnumerable<AppItem>? apps = null)
	{
		if (ShellState.Query() == UserNotificationState.RunningD3DFullScreen)
		{
			return true;
		}
		nint foregroundWindow = NativeMethods.GetForegroundWindow();
		if (foregroundWindow == IntPtr.Zero || foregroundWindow == NativeMethods.GetShellWindow())
		{
			return false;
		}
		NativeMethods.GetWindowThreadProcessId(foregroundWindow, out var lpdwProcessId);
		if (lpdwProcessId == 0 || lpdwProcessId == OwnProcessId)
		{
			return false;
		}
		try
		{
			using Process process = Process.GetProcessById((int)lpdwProcessId);
			string text = process.MainModule?.FileName;
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}
			RefreshRecordedGames();
			return MatchesGame(text, RecordedGames, apps);
		}
		catch (Exception ex) when ((ex is Win32Exception || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException) ? true : false)
		{
			return false;
		}
	}

	internal static bool MatchesGame(string path, IEnumerable<string> recorded, IEnumerable<AppItem>? apps)
	{
		if (path.Replace('/', '\\').Contains("\\steamapps\\common\\", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		foreach (string item in recorded)
		{
			if (string.Equals(path, item, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		if (apps != null)
		{
			foreach (AppItem app in apps)
			{
				if ((app.Category == "游戏" || app.Category == "Games") && string.Equals(path, app.ExecutablePath, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static void RefreshRecordedGames()
	{
		if (DateTime.UtcNow < _refreshAt)
		{
			return;
		}
		_refreshAt = DateTime.UtcNow.AddMinutes(1.0);
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("System\\GameConfigStore\\Children");
			if (registryKey == null)
			{
				return;
			}
			RecordedGames.Clear();
			string[] subKeyNames = registryKey.GetSubKeyNames();
			foreach (string name in subKeyNames)
			{
				using RegistryKey registryKey2 = registryKey.OpenSubKey(name);
				if (registryKey2?.GetValue("MatchedExeFullPath") is string text && Path.IsPathFullyQualified(text))
				{
					RecordedGames.Add(text);
				}
			}
		}
		catch (Exception ex) when ((ex is SecurityException || ex is UnauthorizedAccessException || ex is IOException) ? true : false)
		{
		}
	}
}
