using System;

namespace Launchpad.Models;

public sealed class UpdateState
{
	public DateTime LastCheckedAt { get; set; }

	public string DismissedVersion { get; set; } = string.Empty;

	public DateTime SnoozedUntil { get; set; }

	public string DownloadedVersion { get; set; } = string.Empty;

	public string DownloadedPath { get; set; } = string.Empty;

	public string DownloadedSha256 { get; set; } = string.Empty;

	public long DownloadedSize { get; set; }
}
