using System;
using System.Collections.Generic;

namespace Launchpad.Services;

public sealed class IconPack
{
	public required string Id { get; init; }

	public required string Name { get; init; }

	public required string RootDirectory { get; init; }

	public string? DefaultFile { get; init; }

	public required Dictionary<string, string> Icons { get; init; }

	public DateTime Stamp { get; init; }

	public bool IsOfficial { get; init; }
}
