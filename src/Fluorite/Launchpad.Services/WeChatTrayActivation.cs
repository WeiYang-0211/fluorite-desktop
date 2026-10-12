using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace Launchpad.Services;

internal static class WeChatTrayActivation
{
	private sealed record CachedIcon(AutomationElement Element, DateTime Expires);

	private static readonly object RequestGate = new object();

	private static Task<bool>? _activeRequest;

	private static string _activeKey = "";

	private static readonly ConcurrentDictionary<string, CachedIcon> Icons = new ConcurrentDictionary<string, CachedIcon>();

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern nint FindWindow(string className, string? title);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(nint window);

	internal static bool IsOverflowVisible()
	{
		if (!IsWindowVisible(FindWindow("NotifyIconOverflowWindow", null)))
		{
			return IsWindowVisible(FindWindow("TopLevelWindowForOverflowXamlIsland", null));
		}
		return true;
	}

	internal static void InspectTray()
	{
		string[] entries = new string[3] { "Shell_TrayWnd", "NotifyIconOverflowWindow", "TopLevelWindowForOverflowXamlIsland" };
		foreach (string text in entries)
		{
			nint pageNumber = FindWindow(text, null);
			if (pageNumber == IntPtr.Zero)
			{
				continue;
			}
			foreach (AutomationElement item in AutomationElement.FromHandle(pageNumber).FindAll(TreeScope.Descendants, Condition.TrueCondition))
			{
				AutomationElement.AutomationElementInformation current = item.Current;
				if (!string.IsNullOrWhiteSpace(current.Name))
				{
					Console.WriteLine($"{text}: {current.ControlType.ProgrammaticName} name='{current.Name}' id='{current.AutomationId}' invoke={item.TryGetCurrentPattern(InvokePattern.Pattern, out var _)}");
				}
			}
		}
	}

	internal static bool MatchesTrayName(string name)
	{
		return MatchesTrayName(name, new string[3] { "微信", "WeChat", "Weixin" });
	}

	internal static bool MatchesTrayName(string name, string[] names)
	{
		name = name.Trim();
		foreach (string text in names)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				continue;
			}
			if (name.Equals(text, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (name.StartsWith(text, StringComparison.OrdinalIgnoreCase) && name.Length > text.Length)
			{
				if (IsQqTrayName(name, text))
				{
					return true;
				}
				bool validated;
				switch (text)
				{
				case "微信":
				case "WeChat":
				case "Weixin":
					validated = true;
					break;
				default:
					validated = false;
					break;
				}
				if (validated && (name[text.Length] == '\r' || name[text.Length] == '\n'))
				{
					return true;
				}
				if (char.IsWhiteSpace(name[text.Length]) && (text.Equals("网易云音乐", StringComparison.OrdinalIgnoreCase) || text.Equals("NetEase Cloud Music", StringComparison.OrdinalIgnoreCase) || text.Equals("cloudmusic", StringComparison.OrdinalIgnoreCase)))
				{
					return true;
				}
				string commandLinePath = name;
				int length = text.Length;
				string text3 = commandLinePath.Substring(length, commandLinePath.Length - length).TrimStart();
				if (text3.Length > 0 && "(:：（-[【,，".Contains(text3[0]))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool IsQqTrayName(string name, string alias)
	{
		bool validated;
		switch (alias)
		{
		case "QQ":
		case "腾讯QQ":
		case "腾讯 QQ":
			validated = true;
			break;
		default:
			validated = false;
			break;
		}
		if (!validated)
		{
			return false;
		}
		int length = alias.Length;
		string text = name.Substring(length, name.Length - length);
		if (!text.StartsWith("\r", StringComparison.Ordinal))
		{
			return text.StartsWith("\n", StringComparison.Ordinal);
		}
		return true;
	}

	internal static bool TryActivate(string[] names, bool inspectHiddenOverflow = false)
	{
		Task<bool> activeRequest2;
		lock (RequestGate)
		{
			string text = string.Join("|", names) + "|" + inspectHiddenOverflow;
			Task<bool> activeRequest = _activeRequest;
			if (activeRequest != null && !activeRequest.IsCompleted && text != _activeKey)
			{
				return false;
			}
			if (_activeRequest == null || _activeRequest.IsCompleted)
			{
				_activeKey = text;
				_activeRequest = Task.Run(() => ActivateCore(names, inspectHiddenOverflow));
			}
			activeRequest2 = _activeRequest;
		}
		try
		{
			return activeRequest2.Wait(TimeSpan.FromSeconds(3.0)) && activeRequest2.Result;
		}
		catch (Exception ex)
		{
			App.Log("[WECHAT] tray activation failed: " + ex.Message);
			return false;
		}
	}

	private static bool ActivateCore(string[] names, bool inspectHiddenOverflow)
	{
		try
		{
			string key = string.Join("|", names);
			if (Icons.TryGetValue(key, out CachedIcon value))
			{
				try
				{
					if (value.Expires > DateTime.UtcNow && !value.Element.Current.IsOffscreen && MatchesTrayName(value.Element.Current.Name, names) && value.Element.TryGetCurrentPattern(InvokePattern.Pattern, out var patternObject))
					{
						((InvokePattern)patternObject).Invoke();
						App.Log("[TRAY] cached icon invoked");
						return true;
					}
				}
				catch (ElementNotAvailableException)
				{
				}
				Icons.TryRemove(key, out CachedIcon _);
			}
			nint pageNumber = FindWindow("Shell_TrayWnd", null);
			if (pageNumber == IntPtr.Zero)
			{
				return false;
			}
			AutomationElement automationElement = AutomationElement.FromHandle(pageNumber);
			if (TryInvokeIcon(automationElement, names))
			{
				return true;
			}
			AutomationElement automationElement2 = null;
			string[] entries = new string[2] { "NotifyIconOverflowWindow", "TopLevelWindowForOverflowXamlIsland" };
			for (int i = 0; i < entries.Length; i++)
			{
				nint settingIndex = FindWindow(entries[i], null);
				if (settingIndex != IntPtr.Zero)
				{
					automationElement2 = FindIcon(AutomationElement.FromHandle(settingIndex), names);
					if (automationElement2 != null)
					{
						break;
					}
				}
			}
			if (automationElement2 != null && !automationElement2.Current.IsOffscreen)
			{
				return InvokeIcon(automationElement2, names);
			}
			if (automationElement2 == null && !inspectHiddenOverflow)
			{
				return false;
			}
			AutomationElement automationElement3 = automationElement.FindFirst(TreeScope.Descendants, new OrCondition(new PropertyCondition(AutomationElement.AutomationIdProperty, "SystemTray.ChevronButton"), new PropertyCondition(AutomationElement.AutomationIdProperty, "Chevron"), new AndCondition(new PropertyCondition(AutomationElement.AutomationIdProperty, "SystemTrayIcon"), new OrCondition(new PropertyCondition(AutomationElement.NameProperty, "显示隐藏的图标"), new PropertyCondition(AutomationElement.NameProperty, "Show hidden icons")))));
			if (automationElement3 == null || !automationElement3.TryGetCurrentPattern(InvokePattern.Pattern, out var patternObject2))
			{
				return false;
			}
			bool validated = !IsOverflowVisible();
			if (validated)
			{
				((InvokePattern)patternObject2).Invoke();
			}
			try
			{
				for (int j = 0; j < 10; j++)
				{
					entries = new string[2] { "NotifyIconOverflowWindow", "TopLevelWindowForOverflowXamlIsland" };
					for (int i = 0; i < entries.Length; i++)
					{
						nint targetPage = FindWindow(entries[i], null);
						if (targetPage != IntPtr.Zero && TryInvokeIcon(AutomationElement.FromHandle(targetPage), names))
						{
							return true;
						}
					}
					Thread.Sleep(50);
				}
				return false;
			}
			finally
			{
				if (validated && IsOverflowVisible())
				{
					try
					{
						((InvokePattern)patternObject2).Invoke();
					}
					catch (ElementNotAvailableException)
					{
					}
				}
			}
		}
		catch (Exception ex3)
		{
			App.Log("[WECHAT] tray provider unavailable: " + ex3.Message);
			return false;
		}
	}

	private static bool TryInvokeIcon(AutomationElement root, string[] names)
	{
		AutomationElement automationElement = FindIcon(root, names);
		if (automationElement != null && !automationElement.Current.IsOffscreen)
		{
			return InvokeIcon(automationElement, names);
		}
		return false;
	}

	private static AutomationElement? FindIcon(AutomationElement root, string[] names)
	{
		List<AutomationElement> list = new List<AutomationElement>();
		foreach (AutomationElement item in root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)))
		{
			if (!item.Current.AutomationId.StartsWith("Appid:", StringComparison.OrdinalIgnoreCase) && MatchesTrayName(item.Current.Name, names))
			{
				list.Add(item);
			}
		}
		if (list.Count != 1)
		{
			return null;
		}
		return list[0];
	}

	private static bool InvokeIcon(AutomationElement icon, string[] names)
	{
		if (!icon.TryGetCurrentPattern(InvokePattern.Pattern, out var patternObject))
		{
			return false;
		}
		((InvokePattern)patternObject).Invoke();
		if (Icons.Count >= 128)
		{
			Icons.Clear();
		}
		Icons[string.Join("|", names)] = new CachedIcon(icon, DateTime.UtcNow.AddMinutes(2.0));
		App.Log("[TRAY] matching icon invoked");
		return true;
	}
}
