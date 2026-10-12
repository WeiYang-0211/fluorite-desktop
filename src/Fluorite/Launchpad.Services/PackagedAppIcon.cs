using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace Launchpad.Services;

internal static class PackagedAppIcon
{
	private static readonly ConcurrentDictionary<string, Lazy<string?>> Roots = new ConcurrentDictionary<string, Lazy<string>>(StringComparer.OrdinalIgnoreCase);

	internal static string? Resolve(string path)
	{
		if (path.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
		{
			string text = path;
			int length = "shell:AppsFolder\\".Length;
			path = text.Substring(length, text.Length - length);
		}
		int pageNumber = path.IndexOf('!');
		if (pageNumber <= 0 || pageNumber == path.Length - 1)
		{
			return null;
		}
		try
		{
			string key = path.Substring(0, pageNumber);
			string commandLinePath = Roots.GetOrAdd(key, (string packageFamilyName) => new Lazy<string>(() =>
			{
				try
				{
					return (from p in new PackageManager().FindPackagesForUser(string.Empty, packageFamilyName)
						where !p.IsResourcePackage && !p.IsFramework
						select p.InstalledLocation.Path).FirstOrDefault();
				}
				catch
				{
					return (string?)null;
				}
			}, LazyThreadSafetyMode.ExecutionAndPublication)).Value ?? string.Empty;
			if (commandLinePath.Length == 0)
			{
				return null;
			}
			string text3 = path;
			int length = pageNumber + 1;
			return ResolveFromManifest(commandLinePath, text3.Substring(length, text3.Length - length));
		}
		catch
		{
			return null;
		}
	}

	internal static string? ResolveFromManifest(string root, string applicationId)
	{
		XElement xElement = XDocument.Load(Path.Combine(root, "AppxManifest.xml")).Descendants().FirstOrDefault((XElement e) => e.Name.LocalName == "Application" && string.Equals((string?)e.Attribute("Id"), applicationId, StringComparison.OrdinalIgnoreCase))?.Elements().FirstOrDefault((XElement e) => e.Name.LocalName == "VisualElements");
		string text = ((string?)xElement?.Attribute("Square44x44Logo")) ?? ((string?)xElement?.Attribute("Square30x30Logo"));
		if (string.IsNullOrWhiteSpace(text) || text.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		string value = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
		string fullPath = Path.GetFullPath(Path.Combine(root, text));
		if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		string directoryName = Path.GetDirectoryName(fullPath);
		if (directoryName == null || !Directory.Exists(directoryName))
		{
			return null;
		}
		string stem = Path.GetFileNameWithoutExtension(fullPath);
		string extension = Path.GetExtension(fullPath);
		return (from file in Directory.EnumerateFiles(directoryName, stem + "*" + extension)
			where Path.GetFileName(file).Equals(stem + extension, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(file).StartsWith(stem + ".", StringComparison.OrdinalIgnoreCase)
			where !Path.GetFileName(file).Contains("contrast-", StringComparison.OrdinalIgnoreCase)
			select (File: file, Score: Score(Path.GetFileName(file))) into item
			orderby item.Score descending
			select item.File).FirstOrDefault();
	}

	private static int Score(string name)
	{
		int pageNumber = (name.Contains("altform-unplated", StringComparison.OrdinalIgnoreCase) ? 10000 : 0);
		int settingIndex = name.IndexOf("targetsize-", StringComparison.OrdinalIgnoreCase);
		if (settingIndex >= 0)
		{
			int targetPage = settingIndex + "targetsize-".Length;
			if (int.TryParse(new string(name.Substring(targetPage, name.Length - targetPage).TakeWhile(char.IsDigit).ToArray()), out var result))
			{
				pageNumber += 2000 - Math.Min(1500, Math.Abs(result - 256));
			}
		}
		else if (name.Contains("scale-", StringComparison.OrdinalIgnoreCase))
		{
			pageNumber += 100;
		}
		return pageNumber;
	}
}
