using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Launchpad.Models;

namespace Launchpad.Services;

public interface IAppScannerService
{
	ISteamScannerService SteamScanner { get; }

	Task<List<AppItem>> ScanApplicationsAsync(IEnumerable<AppItem>? existingApps = null, IEnumerable<AppItem>? manualApps = null, bool scanSteam = true, CancellationToken cancellationToken = default(CancellationToken));

	AppItem? CreateAppFromPath(string path);
}
