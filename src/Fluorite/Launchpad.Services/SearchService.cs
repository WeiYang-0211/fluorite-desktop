using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Launchpad.Models;

namespace Launchpad.Services;

public class SearchService : ISearchService
{
	private static readonly string[] PinyinBoundaries = new string[23]
	{
		"啊", "芭", "擦", "搭", "蛾", "发", "噶", "哈", "击", "喀",
		"垃", "妈", "拿", "哦", "啪", "期", "然", "撒", "塌", "挖",
		"昔", "压", "匝"
	};

	private const string PinyinLetters = "abcdefghjklmnopqrstwxyz";

	private static readonly StringComparer ZhComparer = StringComparer.Create(new CultureInfo("zh-CN"), ignoreCase: false);

	private static readonly ConcurrentDictionary<char, char> PinyinCharCache = new ConcurrentDictionary<char, char>();

	private static readonly ConcurrentDictionary<string, string> PinyinNameCache = new ConcurrentDictionary<string, string>();

	private static readonly char[] WordDelimiters = new char[4] { ' ', '-', '_', '.' };

	private static char LookupPinyinInitial(char c)
	{
		return PinyinCharCache.GetOrAdd(c, (char ch) =>
		{
			if (ch >= 'a' && ch <= 'z')
			{
				return ch;
			}
			if (ch >= 'A' && ch <= 'Z')
			{
				return (char)(ch + 32);
			}
			if (ch < '一' || ch > '鿿')
			{
				return '\0';
			}
			string y = ch.ToString();
			int pageNumber = 0;
			int settingIndex = PinyinBoundaries.Length - 1;
			int targetPage = -1;
			while (pageNumber <= settingIndex)
			{
				int num4 = (pageNumber + settingIndex) / 2;
				if (ZhComparer.Compare(PinyinBoundaries[num4], y) <= 0)
				{
					targetPage = num4;
					pageNumber = num4 + 1;
				}
				else
				{
					settingIndex = num4 - 1;
				}
			}
			return (targetPage >= 0) ? "abcdefghjklmnopqrstwxyz"[targetPage] : '\0';
		});
	}

	public static string GetPinyinInitials(string name)
	{
		return PinyinNameCache.GetOrAdd(name, (string n) =>
		{
			StringBuilder stringBuilder = new StringBuilder(n.Length);
			for (int i = 0; i < n.Length; i++)
			{
				char c = LookupPinyinInitial(n[i]);
				if (c != '\0')
				{
					stringBuilder.Append(c);
				}
			}
			return stringBuilder.ToString();
		});
	}

	private static bool IsAsciiLettersOnly(string s)
	{
		if (s.Length == 0)
		{
			return false;
		}
		foreach (char c in s)
		{
			if (c < 'a' || c > 'z')
			{
				return false;
			}
		}
		return true;
	}

	public static void EnsureSearchCache(AppItem app)
	{
		if (app.SearchLowerName == null)
		{
			string text = app.Name ?? string.Empty;
			app.SearchLowerName = text.ToLowerInvariant();
			app.SearchPinyinInitials = GetPinyinInitials(text);
			app.SearchWords = app.SearchLowerName.Split(WordDelimiters, StringSplitOptions.RemoveEmptyEntries);
			if (app.SearchWords.Length > 1)
			{
				char[] entries = new char[app.SearchWords.Length];
				for (int i = 0; i < app.SearchWords.Length; i++)
				{
					entries[i] = app.SearchWords[i][0];
				}
				app.SearchAcronym = new string(entries);
			}
			else
			{
				app.SearchAcronym = string.Empty;
			}
			if (!string.IsNullOrEmpty(app.ExecutablePath))
			{
				try
				{
					string path = Environment.ExpandEnvironmentVariables(app.ExecutablePath.Trim().Trim('"'));
					app.SearchExeName = Path.GetFileNameWithoutExtension(path)?.ToLowerInvariant() ?? string.Empty;
				}
				catch
				{
					app.SearchExeName = string.Empty;
				}
			}
			else
			{
				app.SearchExeName = string.Empty;
			}
		}
		if (!app.HasCustomName || app.SearchLowerCustomName != null)
		{
			return;
		}
		string commandLinePath = app.CustomName ?? string.Empty;
		app.SearchLowerCustomName = commandLinePath.ToLowerInvariant();
		app.SearchPinyinCustomInitials = GetPinyinInitials(commandLinePath);
		app.SearchCustomWords = app.SearchLowerCustomName.Split(WordDelimiters, StringSplitOptions.RemoveEmptyEntries);
		if (app.SearchCustomWords.Length > 1)
		{
			char[] buffer = new char[app.SearchCustomWords.Length];
			for (int j = 0; j < app.SearchCustomWords.Length; j++)
			{
				buffer[j] = app.SearchCustomWords[j][0];
			}
			app.SearchCustomAcronym = new string(buffer);
		}
		else
		{
			app.SearchCustomAcronym = string.Empty;
		}
	}

	public IReadOnlyList<AppItem> Search(string query, IEnumerable<AppItem> sourceApps)
	{
		if (string.IsNullOrWhiteSpace(query))
		{
			return sourceApps.ToList();
		}
		string text = query.Trim().ToLowerInvariant();
		bool pinyinApplicable = IsAsciiLettersOnly(text);
		List<(AppItem, int)> list = new List<(AppItem, int)>();
		foreach (AppItem sourceApp in sourceApps)
		{
			int pageNumber = CalculateMatchScore(text, pinyinApplicable, sourceApp);
			if (pageNumber <= 0)
			{
				continue;
			}
			if (sourceApp.IsDock)
			{
				pageNumber += 60;
			}
			if (sourceApp.LastUsedTime > DateTime.MinValue)
			{
				double totalDays = (DateTime.UtcNow - sourceApp.LastUsedTime).TotalDays;
				if (totalDays < 1.0)
				{
					pageNumber += 40;
				}
				else if (totalDays < 7.0)
				{
					pageNumber += 20;
				}
				else if (totalDays < 30.0)
				{
					pageNumber += 10;
				}
			}
			list.Add((sourceApp, pageNumber));
		}
		return list.OrderByDescending(((AppItem App, int Score) r) => r.Score).ThenBy(((AppItem App, int Score) r) => r.App.Name).Select(((AppItem App, int Score) r) => r.App)
			.ToList();
	}

	private static int CalculateMatchScore(string query, bool pinyinApplicable, AppItem app)
	{
		EnsureSearchCache(app);
		int pageNumber = ScoreCached(query, pinyinApplicable, app.SearchLowerName ?? string.Empty, app.SearchPinyinInitials ?? string.Empty, app.SearchWords, app.SearchAcronym);
		if (app.HasCustomName && app.SearchLowerCustomName != null)
		{
			int settingIndex = ScoreCached(query, pinyinApplicable, app.SearchLowerCustomName, app.SearchPinyinCustomInitials ?? string.Empty, app.SearchCustomWords, app.SearchCustomAcronym);
			if (settingIndex > pageNumber)
			{
				pageNumber = settingIndex;
			}
		}
		if (pageNumber > 0)
		{
			return pageNumber;
		}
		if (!string.IsNullOrEmpty(app.SearchExeName))
		{
			if (app.SearchExeName.StartsWith(query, StringComparison.Ordinal))
			{
				return 180;
			}
			if (app.SearchExeName.Contains(query, StringComparison.Ordinal))
			{
				return 100;
			}
		}
		return 0;
	}

	private static int ScoreCached(string query, bool pinyinApplicable, string name, string initials, string[]? words, string? acronym)
	{
		if (string.IsNullOrEmpty(name))
		{
			return 0;
		}
		bool validated = pinyinApplicable && initials.Length > 0 && initials != name;
		if (name == query)
		{
			return 1000;
		}
		if (name.StartsWith(query, StringComparison.Ordinal))
		{
			return 500 + ((name.Length == query.Length) ? 50 : 0);
		}
		if (validated && initials.StartsWith(query, StringComparison.Ordinal))
		{
			return 400;
		}
		if (words != null)
		{
			for (int i = 0; i < words.Length; i++)
			{
				if (words[i].StartsWith(query, StringComparison.Ordinal))
				{
					return 350 - i * 20;
				}
			}
		}
		if (!string.IsNullOrEmpty(acronym) && acronym.StartsWith(query, StringComparison.Ordinal))
		{
			return 300;
		}
		int pageNumber = name.IndexOf(query, StringComparison.Ordinal);
		if (pageNumber >= 0)
		{
			return 200 - Math.Min(pageNumber * 5, 50);
		}
		if (validated && initials.Contains(query, StringComparison.Ordinal))
		{
			return 150;
		}
		return 0;
	}
}
