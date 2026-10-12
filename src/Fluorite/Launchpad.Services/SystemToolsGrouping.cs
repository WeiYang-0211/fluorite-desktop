using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Launchpad.Models;

namespace Launchpad.Services;

internal static class SystemToolsGrouping
{
	private const string Category = "SystemTools";

	private static readonly HashSet<string> Executables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"control.exe", "taskmgr.exe", "regedit.exe", "cmd.exe", "powershell.exe", "wt.exe", "mmc.exe", "msconfig.exe", "msinfo32.exe", "resmon.exe",
		"perfmon.exe", "cleanmgr.exe", "dfrgui.exe", "mstsc.exe", "charmap.exe", "magnify.exe", "narrator.exe", "osk.exe", "livecaptions.exe", "voiceaccess.exe",
		"snippingtool.exe", "notepad.exe", "calc.exe", "winver.exe", "mblctr.exe", "recoverydrive.exe", "systempropertiesadvanced.exe", "systempropertiesprotection.exe", "systempropertiesremote.exe", "computerdefaults.exe",
		"optionalfeatures.exe", "quickassist.exe", "psr.exe", "printmanagement.exe"
	};

	private static readonly string[] AppIds = new string[8] { "Microsoft.WindowsCalculator_", "Microsoft.WindowsNotepad_", "Microsoft.ScreenSketch_", "Microsoft.WindowsTerminal_", "Microsoft.WindowsTerminalPreview_", "MicrosoftCorporationII.QuickAssist_", "Microsoft.SecHealthUI_", "Microsoft.Windows.SecHealthUI_" };

	internal static bool IsSystemTool(AppItem app)
	{
		if (app.IsFolder || app.IsWidget || app.Source.Equals("Manual", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (app.Category == "SystemTools")
		{
			return true;
		}
		string text = Environment.ExpandEnvironmentVariables(app.ExecutablePath.Trim().Trim('"'));
		bool validated = text.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase);
		if (validated || app.Source.Equals("UWP", StringComparison.OrdinalIgnoreCase))
		{
			string commandLinePath;
			if (!validated)
			{
				commandLinePath = text;
			}
			else
			{
				string text3 = text;
				int length = "shell:AppsFolder\\".Length;
				commandLinePath = text3.Substring(length, text3.Length - length);
			}
			string id = commandLinePath;
			if (!id.Equals("windows.immersivecontrolpanel", StringComparison.OrdinalIgnoreCase) && !id.Equals("wt", StringComparison.OrdinalIgnoreCase) && (!id.StartsWith("MicrosoftWindows.Client.CBS_", StringComparison.OrdinalIgnoreCase) || (!id.EndsWith("!LiveCaptions", StringComparison.OrdinalIgnoreCase) && !id.EndsWith("!VoiceAccess", StringComparison.OrdinalIgnoreCase))))
			{
				return AppIds.Any((string prefix) => id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
			}
			return true;
		}
		string text4 = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\') + "\\";
		if (text4.Length > 1 && text.StartsWith(text4, StringComparison.OrdinalIgnoreCase))
		{
			return Executables.Contains(Path.GetFileName(text));
		}
		return false;
	}

	private static AppItem[] Pending(IEnumerable<AppItem> apps, IEnumerable<AppItem> manualApps)
	{
		HashSet<string> manualPaths = manualApps.Select((AppItem a) => a.ExecutablePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
		return apps.Where((AppItem a) => !a.SystemToolGroupingHandled && !manualPaths.Contains(a.ExecutablePath) && IsSystemTool(a)).ToArray();
	}

	internal static bool NeedsGrouping(IEnumerable<AppItem> apps, IEnumerable<AppItem> manualApps)
	{
		return Pending(apps, manualApps).Length != 0;
	}

	internal static bool Group(List<AppItem> apps, IEnumerable<AppItem> manualApps)
	{
		AppItem[] entries = Pending(apps, manualApps);
		if (entries.Length == 0)
		{
			return false;
		}
		AppItem[] buffer = entries;
		for (int i = 0; i < buffer.Length; i++)
		{
			buffer[i].SystemToolGroupingHandled = true;
		}
		AppItem[] array3 = entries.Where((AppItem a) => !a.IsDock && !a.IsHidden && string.IsNullOrEmpty(a.FolderId)).ToArray();
		if (array3.Length == 0)
		{
			return true;
		}
		AppItem appItem = apps.FirstOrDefault((AppItem a) => a.IsFolder && (a.Category == "SystemTools" || a.DisplayName == "系统工具"));
		if ((appItem != null && (appItem.IsHidden || appItem.IsDock)) ? true : false)
		{
			return true;
		}
		if (appItem == null)
		{
			appItem = new AppItem
			{
				Name = "系统工具",
				IsFolder = true,
				Category = "SystemTools",
				SortIndex = array3.Min((AppItem a) => a.SortIndex)
			};
			apps.Add(appItem);
		}
		appItem.Category = "SystemTools";
		foreach (AppItem app in array3.OrderBy((AppItem a) => a.SortIndex))
		{
			app.FolderId = appItem.Id;
			if (!appItem.Children.Any((AppItem child) => child.Id == app.Id))
			{
				appItem.Children.Add(app);
			}
		}
		return true;
	}
}
