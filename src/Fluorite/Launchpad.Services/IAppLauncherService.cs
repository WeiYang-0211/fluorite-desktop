using System.Collections.Generic;
using System.Threading.Tasks;
using Launchpad.Models;

namespace Launchpad.Services;

public interface IAppLauncherService
{
	bool Launch(AppItem app, out string errorMessage);

	Task<(bool Success, string Error)> LaunchAsync(AppItem app)
	{
		string errorMessage;
		return Task.FromResult((Launch(app, out errorMessage), errorMessage));
	}

	bool LaunchWithFiles(AppItem app, IEnumerable<string> filePaths, out string errorMessage);

	bool RunAsAdmin(AppItem app, out string errorMessage);

	bool ShowInFolder(AppItem app, out string errorMessage);

	bool RequestUninstall(AppItem app, out string errorMessage);
}
