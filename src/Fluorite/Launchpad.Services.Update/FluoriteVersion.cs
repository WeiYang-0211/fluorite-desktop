using System;
using System.Reflection;

namespace Launchpad.Services.Update;

public static class FluoriteVersion
{
	public static string Text { get; } = Read();

	public static Version Parsed { get; } = Parse(Text) ?? new Version(0, 0, 0);

	private static string Read()
	{
		Version version = null;
		try
		{
			version = Assembly.GetEntryAssembly()?.GetName().Version;
		}
		catch
		{
		}
		if (version == null || version.Build < 0)
		{
			return "0.0.0";
		}
		return $"{version.Major}.{version.Minor}.{version.Build}";
	}

	public static Version? Parse(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return null;
		}
		string text = raw.Trim().TrimStart('v', 'V');
		if (text.Length == 0)
		{
			return null;
		}
		string[] entries = text.Split('.');
		if (entries.Length > 4)
		{
			return null;
		}
		int[] buffer = new int[4];
		for (int i = 0; i < entries.Length; i++)
		{
			string commandLinePath = entries[i];
			int j;
			for (j = 0; j < commandLinePath.Length && char.IsDigit(commandLinePath[j]); j++)
			{
			}
			if (j == 0)
			{
				return null;
			}
			if (!int.TryParse(commandLinePath.Substring(0, j), out buffer[i]))
			{
				return null;
			}
		}
		if (entries.Length < 3)
		{
			buffer[2] = 0;
		}
		if (entries.Length < 4)
		{
			buffer[3] = 0;
		}
		try
		{
			return new Version(buffer[0], buffer[1], buffer[2], buffer[3]);
		}
		catch
		{
			return null;
		}
	}

	public static bool IsNewer(string? advertised)
	{
		Version version = Parse(advertised);
		if (version == null)
		{
			return false;
		}
		return version > Parsed;
	}

	public static bool SameRelease(string? left, string? right)
	{
		Version version = Parse(left);
		Version version2 = Parse(right);
		if (version == null || version2 == null)
		{
			return false;
		}
		if (version.Major == version2.Major && version.Minor == version2.Minor)
		{
			return version.Build == version2.Build;
		}
		return false;
	}
}
