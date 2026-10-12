using System;

namespace Launchpad.Models;

public sealed record WorkspaceEntry(string Id, string Name, DateTime CreatedUtc, int? ApplicationCount = null, int FolderCount = 0, bool Complete = true)
{
	public string Detail => CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

	public string Kind
	{
		get
		{
			if (!Complete)
			{
				return "基础设置";
			}
			return "完整快照";
		}
	}

	public string Summary
	{
		get
		{
			if (Complete)
			{
				int? applicationCount = ApplicationCount;
				if (applicationCount.HasValue)
				{
					int valueOrDefault = applicationCount.GetValueOrDefault();
					return $"{valueOrDefault} 个项目 · {FolderCount} 个文件夹 · 外观与程序坞";
				}
				return "排列、文件夹、外观与程序坞";
			}
			return "外观与基础设置";
		}
	}
}
