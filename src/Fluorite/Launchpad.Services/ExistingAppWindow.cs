using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Launchpad.Interop;

namespace Launchpad.Services;

internal static class ExistingAppWindow
{
	private sealed record CachedWindow(nint Handle, uint ProcessId, long Created, DateTime Expires);

	private delegate bool WindowVisitor(nint window, nint parameter);

	private static readonly ConcurrentDictionary<string, CachedWindow> Windows = new ConcurrentDictionary<string, CachedWindow>(StringComparer.OrdinalIgnoreCase);

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(WindowVisitor visitor, nint parameter);

	[DllImport("user32.dll")]
	private static extern bool EnumChildWindows(nint parent, WindowVisitor visitor, nint parameter);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(nint window);

	[DllImport("user32.dll")]
	private static extern bool IsIconic(nint window);

	[DllImport("user32.dll")]
	private static extern bool IsWindow(nint window);

	[DllImport("user32.dll")]
	private static extern bool ShowWindowAsync(nint window, int command);

	[DllImport("user32.dll")]
	private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowTextLengthW(nint window);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowTextW(nint window, StringBuilder text, int count);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetClassNameW(nint window, StringBuilder text, int count);

	[DllImport("kernel32.dll")]
	private static extern nint OpenProcess(uint access, bool inherit, uint id);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder path, ref uint size);

	[DllImport("kernel32.dll")]
	private static extern bool CloseHandle(nint handle);

	[DllImport("kernel32.dll")]
	private static extern bool GetProcessTimes(nint process, out long created, out long exited, out long kernel, out long user);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetApplicationUserModelId(nint process, ref uint length, StringBuilder? id);

	private static bool MatchesPackage(uint pid, string applicationId)
	{
		nint pageNumber = OpenProcess(4096u, inherit: false, pid);
		if (pageNumber == IntPtr.Zero)
		{
			return false;
		}
		try
		{
			uint length = 0u;
			if (GetApplicationUserModelId(pageNumber, ref length, null) != 122 || length == 0 || length > 32768)
			{
				return false;
			}
			StringBuilder stringBuilder = new StringBuilder((int)length);
			return GetApplicationUserModelId(pageNumber, ref length, stringBuilder) == 0 && stringBuilder.ToString().Equals(applicationId, StringComparison.OrdinalIgnoreCase);
		}
		finally
		{
			CloseHandle(pageNumber);
		}
	}

	private static string? ProcessPath(uint id)
	{
		nint pageNumber = OpenProcess(4096u, inherit: false, id);
		if (pageNumber == IntPtr.Zero)
		{
			return null;
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder(32768);
			uint size = (uint)stringBuilder.Capacity;
			return QueryFullProcessImageName(pageNumber, 0u, stringBuilder, ref size) ? stringBuilder.ToString() : null;
		}
		finally
		{
			CloseHandle(pageNumber);
		}
	}

	internal static string ResolveLaunchTarget(string executable, string? arguments)
	{
		if (!Path.GetFileName(executable).Equals("Update.exe", StringComparison.OrdinalIgnoreCase))
		{
			return executable;
		}
		Match match = Regex.Match(arguments ?? "", "--processStart(?:=|\\s+)(?:\"(?<file>[^\"/\\\\]+\\.exe)\"|(?<file>[^\\s\"/\\\\]+\\.exe))(?=\\s|$)", RegexOptions.IgnoreCase);
		if (!match.Success)
		{
			return executable;
		}
		string value = match.Groups["file"].Value;
		if (value == ".exe" || value.Contains(':'))
		{
			return executable;
		}
		return Path.Combine(Path.GetDirectoryName(executable), value);
	}

	internal static bool IsWeChatExecutable(string path)
	{
		if (!Path.GetFileName(path).Equals("WeChat.exe", StringComparison.OrdinalIgnoreCase))
		{
			return Path.GetFileName(path).Equals("Weixin.exe", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	internal static bool IsQqExecutable(string path)
	{
		if (!Path.GetFileName(path).Equals("QQ.exe", StringComparison.OrdinalIgnoreCase))
		{
			return Path.GetFileName(path).Equals("QQNT.exe", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	internal static bool ShouldRestoreWindow(bool visible, bool minimized)
	{
		return visible & minimized;
	}

	internal static bool IsWeChatMainWindow(string title, string className)
	{
		if (!className.Equals("WeChatMainWndForPC", StringComparison.OrdinalIgnoreCase) && !className.Equals("WeixinMainWndForPC", StringComparison.OrdinalIgnoreCase) && !title.Equals("微信", StringComparison.OrdinalIgnoreCase) && !title.Equals("Weixin", StringComparison.OrdinalIgnoreCase))
		{
			return title.Equals("WeChat", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	internal static bool MatchesExecutable(string target, string processPath)
	{
		try
		{
			target = Path.GetFullPath(target);
			processPath = Path.GetFullPath(processPath);
			if (string.Equals(target, processPath, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (LeagueWindowIdentity.Matches(target, processPath))
			{
				return true;
			}
			if (Path.GetFileName(target).Equals(Path.GetFileName(processPath), StringComparison.OrdinalIgnoreCase) && string.Equals(InstallRoot(target), InstallRoot(processPath), StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			string value = Path.GetDirectoryName(target) + Path.DirectorySeparatorChar;
			return IsWeChatExecutable(target) && IsWeChatExecutable(processPath) && processPath.StartsWith(value, StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
		static string InstallRoot(string path)
		{
			string text = Path.GetDirectoryName(path);
			while (Regex.IsMatch(Path.GetFileName(text), "^(?:app-)?\\d+(?:\\.\\d+)*(?:[-+][\\w.-]+)?$", RegexOptions.IgnoreCase))
			{
				string directoryName = Path.GetDirectoryName(text);
				if (directoryName == null)
				{
					break;
				}
				text = directoryName;
			}
			return text;
		}
	}

	internal static bool TryActivate(string executable, string? arguments = null, string? appName = null)
	{
		if (Path.GetExtension(executable).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
		{
			(string, string, string, int) tuple = ShellLinkHelper.ResolveShortcut(executable);
			executable = tuple.Item1;
			arguments = (string.IsNullOrWhiteSpace(arguments) ? tuple.Item2 : arguments);
		}
		bool leagueShortcut = LeagueWindowIdentity.InstallRoot(executable) != null;
		string cacheKey = executable + "\n" + arguments;
		if (!leagueShortcut && Windows.TryGetValue(cacheKey, out CachedWindow value))
		{
			NativeMethods.GetWindowThreadProcessId(value.Handle, out var lpdwProcessId);
			if (value.Expires > DateTime.UtcNow && IsWindow(value.Handle) && IsWindowVisible(value.Handle) && lpdwProcessId == value.ProcessId && ProcessCreated(lpdwProcessId) == value.Created)
			{
				ActivateWindow(value.Handle);
				App.Log("[LAUNCH] cached window restored: " + executable);
				return true;
			}
			Windows.TryRemove(cacheKey, out CachedWindow _);
		}
		if (executable.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
		{
			string text = executable;
			int length = "shell:AppsFolder\\".Length;
			executable = text.Substring(length, text.Length - length);
		}
		bool packaged = !Path.IsPathFullyQualified(executable) && executable.Contains('!');
		if (!packaged)
		{
			if (!Path.GetExtension(executable).Equals(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(executable))
			{
				return false;
			}
			executable = ResolveLaunchTarget(executable, arguments);
		}
		Dictionary<uint, bool> processMatches = new Dictionary<uint, bool>();
		nint selected = IntPtr.Zero;
		int selectedPriority = 0;
		nint hiddenChat = IntPtr.Zero;
		bool runningWindow = false;
		EnumWindows((nint window, nint _) =>
		{
			bool validated = IsWindowVisible(window);
			if (validated && GetWindowTextLengthW(window) == 0)
			{
				return true;
			}
			NativeMethods.GetWindowThreadProcessId(window, out var lpdwProcessId2);
			try
			{
				if (!MatchesWindow(window))
				{
					return true;
				}
				runningWindow = true;
				if (!validated)
				{
					StringBuilder stringBuilder2 = new StringBuilder(256);
					StringBuilder stringBuilder3 = new StringBuilder(256);
					GetWindowTextW(window, stringBuilder2, stringBuilder2.Capacity);
					GetClassNameW(window, stringBuilder3, stringBuilder3.Capacity);
					if (hiddenChat == IntPtr.Zero && IsWeChatMainWindow(stringBuilder2.ToString(), stringBuilder3.ToString()))
					{
						hiddenChat = window;
					}
					return true;
				}
				if (!IsIconic(window))
				{
					if ((((IntPtr)NativeMethods.GetWindowLongPtr(window, -20)).ToInt64() & 0x80) != 0L)
					{
						return true;
					}
					if (!NativeMethods.GetWindowRect(window, out var lpRect) || lpRect.Right - lpRect.Left < 80 || lpRect.Bottom - lpRect.Top < 60)
					{
						return true;
					}
				}
				int targetPage = ((!leagueShortcut) ? 1 : LeagueWindowIdentity.Priority(ProcessPath(lpdwProcessId2) ?? ""));
				if (targetPage > selectedPriority)
				{
					selected = window;
					selectedPriority = targetPage;
				}
				return leagueShortcut;
			}
			catch
			{
				return true;
			}
		}, IntPtr.Zero);
		if (selected == IntPtr.Zero && !runningWindow && (IsQqExecutable(executable) || IsWeChatExecutable(executable)))
		{
			string[] entries = ((!IsWeChatExecutable(executable)) ? new string[1] { Path.GetFileNameWithoutExtension(executable) } : new string[2] { "WeChat", "Weixin" });
			for (int length = 0; length < entries.Length; length++)
			{
				Process[] processesByName = Process.GetProcessesByName(entries[length]);
				foreach (Process process in processesByName)
				{
					using (process)
					{
						try
						{
							if (MatchesProcess((uint)process.Id))
							{
								runningWindow = true;
							}
						}
						catch
						{
						}
					}
				}
			}
		}
		if ((selected == IntPtr.Zero) & runningWindow)
		{
			string[] names;
			if (IsWeChatExecutable(executable))
			{
				names = new string[3] { "微信", "WeChat", "Weixin" };
			}
			else if (Path.GetFileName(executable).Equals("cloudmusic.exe", StringComparison.OrdinalIgnoreCase))
			{
				names = new string[3] { "网易云音乐", "NetEase Cloud Music", "cloudmusic" };
			}
			else
			{
				names = ((!IsQqExecutable(executable)) ? new string[2]
				{
					appName ?? "",
					Path.GetFileNameWithoutExtension(executable)
				} : new string[3] { "QQ", "腾讯QQ", "腾讯 QQ" });
			}
			if (WeChatTrayActivation.TryActivate(names, IsWeChatExecutable(executable)))
			{
				for (int settingIndex = 0; settingIndex < 20; settingIndex++)
				{
					if (hiddenChat != IntPtr.Zero && IsWindowVisible(hiddenChat))
					{
						Remember(hiddenChat);
						ActivateWindow(hiddenChat);
						return true;
					}
					nint restored = IntPtr.Zero;
					EnumWindows((nint window, nint _) =>
					{
						if (!IsWindowVisible(window) || GetWindowTextLengthW(window) == 0)
						{
							return true;
						}
						NativeMethods.GetWindowThreadProcessId(window, out var _);
						if (!MatchesWindow(window))
						{
							return true;
						}
						if (!NativeMethods.GetWindowRect(window, out var lpRect) || lpRect.Right - lpRect.Left < 80 || lpRect.Bottom - lpRect.Top < 60)
						{
							return true;
						}
						restored = window;
						return false;
					}, IntPtr.Zero);
					if (restored != IntPtr.Zero)
					{
						Remember(restored);
						ActivateWindow(restored);
						return true;
					}
					Thread.Sleep(50);
				}
			}
			if (IsWeChatExecutable(executable))
			{
				App.Log("[LAUNCH] WeChat background session found; duplicate launch blocked: " + executable);
				throw new InvalidOperationException("微信已在后台运行，但未能唤回主窗口，请从系统托盘打开微信。");
			}
			if (IsQqExecutable(executable))
			{
				App.Log("[LAUNCH] QQ background session found; duplicate launch blocked: " + executable);
				throw new InvalidOperationException("QQ 已在后台运行，但未能唤回主窗口，请从系统托盘打开 QQ。");
			}
			App.Log("[LAUNCH] background window could not be recovered; falling back to normal launch: " + executable);
			return false;
		}
		if (selected == IntPtr.Zero)
		{
			return false;
		}
		Remember(selected);
		bool value3 = ActivateWindow(selected);
		StringBuilder stringBuilder = new StringBuilder(256);
		GetWindowTextW(selected, stringBuilder, stringBuilder.Capacity);
		App.Log($"[LAUNCH] existing window matched: {executable}, title='{stringBuilder}', minimized={IsIconic(selected)}, foreground={value3}");
		return true;
		bool MatchesProcess(uint pid)
		{
			if (processMatches.TryGetValue(pid, out var value4))
			{
				return value4;
			}
			string commandLinePath = (packaged ? null : ProcessPath(pid));
			bool validated = (packaged ? MatchesPackage(pid, executable) : (commandLinePath != null && MatchesExecutable(executable, commandLinePath)));
			processMatches[pid] = validated;
			return validated;
		}
		bool MatchesWindow(nint window)
		{
			NativeMethods.GetWindowThreadProcessId(window, out var pid);
			if (!packaged)
			{
				return MatchesProcess(pid);
			}
			if (MatchesProcess(pid))
			{
				return true;
			}
			bool matches = false;
			EnumChildWindows(window, (nint child, nint _) =>
			{
				NativeMethods.GetWindowThreadProcessId(child, out var lpdwProcessId2);
				if (lpdwProcessId2 == pid || !MatchesProcess(lpdwProcessId2))
				{
					return true;
				}
				matches = true;
				return false;
			}, IntPtr.Zero);
			return matches;
		}
		void Remember(nint window)
		{
			NativeMethods.GetWindowThreadProcessId(window, out var lpdwProcessId2);
			long targetPage = ProcessCreated(lpdwProcessId2);
			if (targetPage != 0L)
			{
				if (Windows.Count >= 256)
				{
					Windows.Clear();
				}
				Windows[cacheKey] = new CachedWindow(window, lpdwProcessId2, targetPage, DateTime.UtcNow.AddMinutes(10.0));
			}
		}
	}

	private static long ProcessCreated(uint pid)
	{
		nint pageNumber = OpenProcess(4096u, inherit: false, pid);
		if (pageNumber == IntPtr.Zero)
		{
			return 0L;
		}
		try
		{
			long created;
			long exited;
			long kernel;
			long user;
			return GetProcessTimes(pageNumber, out created, out exited, out kernel, out user) ? created : 0;
		}
		finally
		{
			CloseHandle(pageNumber);
		}
	}

	internal static bool TryActivateTrackedWindow(nint window, uint expectedProcess)
	{
		NativeMethods.GetWindowThreadProcessId(window, out var lpdwProcessId);
		if (lpdwProcessId == expectedProcess && IsWindow(window) && IsWindowVisible(window))
		{
			return ActivateWindow(window);
		}
		return false;
	}

	private static bool ActivateWindow(nint window)
	{
		if (ShouldRestoreWindow(IsWindowVisible(window), IsIconic(window)))
		{
			PostMessage(window, 274u, new IntPtr(61728), IntPtr.Zero);
			ShowWindowAsync(window, 9);
			NativeMethods.SetForegroundWindow(window);
			for (int i = 0; i < 20; i++)
			{
				if (!IsIconic(window))
				{
					break;
				}
				Thread.Sleep(10);
			}
		}
		return NativeMethods.SetForegroundWindow(window);
	}
}
