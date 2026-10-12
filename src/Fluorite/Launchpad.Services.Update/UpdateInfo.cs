using System.IO;

namespace Launchpad.Services.Update;

public sealed record UpdateInfo(string Version, string Notes, string DownloadUrl, string? Sha256, long SizeBytes)
{
	public string? LocalPath { get; init; }

	public bool AlreadyDownloaded
	{
		get
		{
			if (!string.IsNullOrEmpty(LocalPath))
			{
				return File.Exists(LocalPath);
			}
			return false;
		}
	}
}
