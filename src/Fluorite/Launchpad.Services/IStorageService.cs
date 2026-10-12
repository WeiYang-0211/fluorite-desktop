using System.Collections.Generic;
using System.Threading.Tasks;
using Launchpad.Models;

namespace Launchpad.Services;

public interface IStorageService
{
	string StorageDirectory { get; }

	Task<List<AppItem>> LoadApplicationsAsync();

	Task<bool> SaveApplicationsAsync(IEnumerable<AppItem> applications);

	Task<AppSettings> LoadSettingsAsync();

	Task<bool> SaveSettingsAsync(AppSettings settings);

	Task<List<FolderItem>> LoadFoldersAsync();

	Task<bool> SaveFoldersAsync(IEnumerable<FolderItem> folders);

	Task<List<AppItem>> LoadManualAppsAsync();

	Task<bool> SaveManualAppsAsync(IEnumerable<AppItem> manualApps);

	Task<bool> SaveWorkspaceAsync(IEnumerable<AppItem> applications, IEnumerable<AppItem> manualApps, IEnumerable<FolderItem> folders, AppSettings settings);

	Task ResetApplicationDatabaseAsync();

	Task<UpdateState> LoadUpdateStateAsync();

	Task SaveUpdateStateAsync(UpdateState state);
}
