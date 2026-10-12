using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Launchpad.Models;

namespace Launchpad.Services;

public interface IIconService
{
	string CacheDirectory { get; }

	void SetActivePack(IconPack? pack);

	void SetAutoTile(bool enabled);

	void SetFrostedTile(bool enabled)
	{
	}

	bool SetPixelSize(int pixels)
	{
		return false;
	}

	Task<ImageSource?> GetIconAsync(AppItem app, CancellationToken cancellationToken = default(CancellationToken));

	Task PreloadIconsAsync(IEnumerable<AppItem> apps, int onScreenCount = 0, CancellationToken cancellationToken = default(CancellationToken));
}
