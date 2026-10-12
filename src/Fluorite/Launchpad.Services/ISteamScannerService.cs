using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Launchpad.Models;

namespace Launchpad.Services;

public interface ISteamScannerService
{
	Task<List<AppItem>> ScanSteamGamesAsync(CancellationToken cancellationToken = default(CancellationToken));

	string? FindGameInstallDirectory(string appId);
}
