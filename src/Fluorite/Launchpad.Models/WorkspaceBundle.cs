using System;
using System.Collections.Generic;

namespace Launchpad.Models;

public sealed class WorkspaceBundle
{
	public int Version { get; set; } = 1;

	public string Name { get; set; } = "我的布局";

	public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

	public bool Complete { get; set; }

	public AppSettings Settings { get; set; } = new AppSettings();

	public List<AppItem> Applications { get; set; } = new List<AppItem>();

	public List<AppItem> ManualApps { get; set; } = new List<AppItem>();

	public List<FolderItem> Folders { get; set; } = new List<FolderItem>();

	public Dictionary<string, byte[]> Assets { get; set; } = new Dictionary<string, byte[]>();

	public List<WorkspaceBundle> SavedLayouts { get; set; } = new List<WorkspaceBundle>();
}
