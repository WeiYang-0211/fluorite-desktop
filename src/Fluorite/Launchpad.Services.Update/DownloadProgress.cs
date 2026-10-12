using System;
using System.Globalization;

namespace Launchpad.Services.Update;

public sealed record DownloadProgress(long BytesDone, long BytesTotal, double SpeedBytesPerSecond)
{
	public int Percent
	{
		get
		{
			if (BytesTotal <= 0)
			{
				return -1;
			}
			return (int)Math.Clamp((double)BytesDone * 100.0 / (double)BytesTotal, 0.0, 100.0);
		}
	}

	public string Line
	{
		get
		{
			string text = Human(BytesDone);
			string commandLinePath = ((BytesTotal > 0) ? (" / " + Human(BytesTotal)) : string.Empty);
			string text3 = ((SpeedBytesPerSecond > 0.0) ? ("  " + Human(SpeedBytesPerSecond) + "/s") : string.Empty);
			return text + commandLinePath + text3;
		}
	}

	private static string Human(double bytes)
	{
		string[] entries = new string[4] { "B", "KB", "MB", "GB" };
		int pageNumber = 0;
		while (bytes >= 1024.0 && pageNumber < entries.Length - 1)
		{
			bytes /= 1024.0;
			pageNumber++;
		}
		return bytes.ToString("0.#", CultureInfo.InvariantCulture) + " " + entries[pageNumber];
	}
}
