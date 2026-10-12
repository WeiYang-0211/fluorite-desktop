using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Launchpad.Models;

namespace Launchpad.Services;

public class StorageService : IStorageService
{
	private readonly string _storageDir;

	private readonly string _applicationsPath;

	private readonly string _settingsPath;

	private readonly string _foldersPath;

	private readonly string _manualAppsPath;

	private readonly string _updateStatePath;

	private readonly JsonSerializerOptions _jsonOptions;

	private static readonly SemaphoreSlim _writeGate = new SemaphoreSlim(1, 1);

	public string StorageDirectory => _storageDir;

	public event Action<string>? SaveFailed;

	public StorageService(string? storageDirectory = null)
	{
		_storageDir = storageDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fluorite");
		Directory.CreateDirectory(_storageDir);
		_applicationsPath = Path.Combine(_storageDir, "applications.json");
		_settingsPath = Path.Combine(_storageDir, "settings.json");
		_foldersPath = Path.Combine(_storageDir, "folders.json");
		_manualAppsPath = Path.Combine(_storageDir, "manual-apps.json");
		_updateStatePath = Path.Combine(_storageDir, "update-state.json");
		_jsonOptions = new JsonSerializerOptions
		{
			WriteIndented = true,
			PropertyNameCaseInsensitive = true,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
		};
		_writeGate.Wait();
		try
		{
			WorkspaceWriteTransaction.Recover(_storageDir);
		}
		finally
		{
			_writeGate.Release();
		}
	}

	public async Task<bool> SaveWorkspaceAsync(IEnumerable<AppItem> applications, IEnumerable<AppItem> manualApps, IEnumerable<FolderItem> folders, AppSettings settings)
	{
		Dictionary<string, object> snapshot = PersistenceSnapshot.Capture(() => new Dictionary<string, object>
		{
			["applications.json"] = DropDuplicateApps(new List<AppItem>(applications)).ConvertAll<AppItem>(PersistenceSnapshot.App),
			["manual-apps.json"] = new List<AppItem>(manualApps).ConvertAll<AppItem>(PersistenceSnapshot.App),
			["folders.json"] = new List<FolderItem>(folders).ConvertAll<FolderItem>(PersistenceSnapshot.Folder),
			["settings.json"] = PersistenceSnapshot.Settings(settings)
		});
		await _writeGate.WaitAsync().ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			await Task.Run(() =>
			{
				WorkspaceWriteTransaction.Save(_storageDir, snapshot, _jsonOptions);
			}).ConfigureAwait(continueOnCapturedContext: false);
			return true;
		}
		catch (Exception ex)
		{
			App.Log("Workspace save failed: " + ex.Message);
			SaveFailed?.Invoke("布局");
			return false;
		}
		finally
		{
			_writeGate.Release();
		}
	}

	public async Task<List<AppItem>> LoadApplicationsAsync()
	{
		List<AppItem> apps = (await LoadJsonFileAsync<List<AppItem>>(_applicationsPath)) ?? new List<AppItem>();
		apps = DropDuplicateApps(apps);
		CarryOldFavoritesIntoDock(apps);
		RelinkFolderChildren(apps);
		return apps;
	}

	private List<AppItem> DropDuplicateApps(List<AppItem> apps)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> hashSet2 = new HashSet<string>(StringComparer.Ordinal);
		List<AppItem> list = new List<AppItem>(apps.Count);
		foreach (AppItem app in apps)
		{
			if (app != null && (string.IsNullOrEmpty(app.Id) || hashSet2.Add(app.Id)))
			{
				if (app.IsFolder || app.IsWidget)
				{
					list.Add(app);
				}
				else if (string.IsNullOrEmpty(app.ExecutablePath) || hashSet.Add(app.ExecutablePath))
				{
					list.Add(app);
				}
			}
		}
		return list;
	}

	private void RelinkFolderChildren(List<AppItem> apps)
	{
		Dictionary<string, AppItem> dictionary = new Dictionary<string, AppItem>();
		foreach (AppItem app in apps)
		{
			if (!app.IsFolder && !string.IsNullOrEmpty(app.Id))
			{
				dictionary[app.Id] = app;
			}
		}
		foreach (AppItem app2 in apps)
		{
			if (!app2.IsFolder)
			{
				continue;
			}
			for (int i = 0; i < app2.Children.Count; i++)
			{
				if (!string.IsNullOrEmpty(app2.Children[i].Id) && dictionary.TryGetValue(app2.Children[i].Id, out var value) && value != app2.Children[i])
				{
					app2.Children[i] = value;
				}
			}
		}
	}

	private void CarryOldFavoritesIntoDock(List<AppItem> apps)
	{
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(_applicationsPath));
			if (jsonDocument.RootElement.ValueKind != JsonValueKind.Array)
			{
				return;
			}
			int pageNumber = 0;
			int settingIndex = 0;
			foreach (JsonElement item in jsonDocument.RootElement.EnumerateArray())
			{
				if (settingIndex < apps.Count)
				{
					if (item.TryGetProperty("isFavorite", out var value) && value.ValueKind == JsonValueKind.True && !apps[settingIndex].IsDock)
					{
						apps[settingIndex].IsDock = true;
						apps[settingIndex].DockIndex = pageNumber++;
					}
					settingIndex++;
					continue;
				}
				break;
			}
		}
		catch (Exception ex)
		{
			App.Log("Dock migration skipped: " + ex.Message);
		}
	}

	public async Task<bool> SaveApplicationsAsync(IEnumerable<AppItem> applications)
	{
		List<AppItem> data = PersistenceSnapshot.Capture(() => DropDuplicateApps(new List<AppItem>(applications)).ConvertAll<AppItem>(PersistenceSnapshot.App));
		return await SaveJsonFileAsync(_applicationsPath, data);
	}

	public async Task<AppSettings> LoadSettingsAsync()
	{
		AppSettings appSettings = await LoadJsonFileAsync<AppSettings>(_settingsPath);
		if (appSettings != null && appSettings.BackdropSource == BackdropSource.Screen)
		{
			appSettings.BackdropSource = BackdropSource.Wallpaper;
		}
		return appSettings ?? new AppSettings();
	}

	public async Task<bool> SaveSettingsAsync(AppSettings settings)
	{
		AppSettings data = PersistenceSnapshot.Capture(() => PersistenceSnapshot.Settings(settings));
		return await SaveJsonFileAsync(_settingsPath, data);
	}

	public async Task<List<FolderItem>> LoadFoldersAsync()
	{
		return (await LoadJsonFileAsync<List<FolderItem>>(_foldersPath)) ?? new List<FolderItem>();
	}

	public async Task<List<AppItem>> LoadManualAppsAsync()
	{
		return (await LoadJsonFileAsync<List<AppItem>>(_manualAppsPath)) ?? new List<AppItem>();
	}

	public async Task<bool> SaveManualAppsAsync(IEnumerable<AppItem> manualApps)
	{
		List<AppItem> data = PersistenceSnapshot.Capture(() => new List<AppItem>(manualApps).ConvertAll<AppItem>(PersistenceSnapshot.App));
		return await SaveJsonFileAsync(_manualAppsPath, data);
	}

	public async Task<bool> SaveFoldersAsync(IEnumerable<FolderItem> folders)
	{
		List<FolderItem> data = PersistenceSnapshot.Capture(() => new List<FolderItem>(folders).ConvertAll<FolderItem>(PersistenceSnapshot.Folder));
		return await SaveJsonFileAsync(_foldersPath, data);
	}

	public async Task ResetApplicationDatabaseAsync()
	{
		try
		{
			if (File.Exists(_applicationsPath))
			{
				File.Delete(_applicationsPath);
			}
			if (File.Exists(_foldersPath))
			{
				File.Delete(_foldersPath);
			}
			string path = Path.Combine(_storageDir, "icon-cache");
			if (Directory.Exists(path))
			{
				Directory.Delete(path, recursive: true);
				Directory.CreateDirectory(path);
			}
		}
		catch (Exception ex)
		{
			App.Log("Error resetting application database: " + ex.Message);
		}
		await Task.CompletedTask;
	}

	public async Task<UpdateState> LoadUpdateStateAsync()
	{
		return (await LoadJsonFileAsync<UpdateState>(_updateStatePath)) ?? new UpdateState();
	}

	public async Task SaveUpdateStateAsync(UpdateState state)
	{
		await SaveJsonFileAsync(_updateStatePath, state);
	}

	private async Task<T?> LoadJsonFileAsync<T>(string filePath) where T : class
	{
		if (!File.Exists(filePath))
		{
			return null;
		}
		try
		{
			using FileStream stream = File.OpenRead(filePath);
			return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions);
		}
		catch (JsonException ex)
		{
			App.Log("Error deserializing " + Path.GetFileName(filePath) + ": " + ex.Message);
			try
			{
				string destFileName = filePath + $".corrupt.{DateTime.Now:yyyyMMddHHmmss}.bak";
				File.Copy(filePath, destFileName, overwrite: true);
			}
			catch
			{
			}
			return null;
		}
		catch (Exception ex2)
		{
			App.Log("Unexpected error loading " + filePath + ": " + ex2.Message);
			return null;
		}
	}

	private async Task<bool> SaveJsonFileAsync<T>(string filePath, T data)
	{
		string tempFile = filePath + ".tmp";
		await _writeGate.WaitAsync().ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			WorkspaceWriteTransaction.Recover(_storageDir);
			int attempt = 1;
			while (true)
			{
				try
				{
					await Task.Run(() =>
					{
						File.WriteAllBytes(tempFile, JsonSerializer.SerializeToUtf8Bytes(data, _jsonOptions));
						File.Move(tempFile, filePath, overwrite: true);
					}).ConfigureAwait(continueOnCapturedContext: false);
					return true;
				}
				catch (Exception ex)
				{
					if (attempt >= 2)
					{
						App.Log("Failed to save " + Path.GetFileName(filePath) + ": " + ex.Message);
						try
						{
							if (File.Exists(tempFile))
							{
								File.Delete(tempFile);
							}
						}
						catch
						{
						}
						SaveFailed?.Invoke(Path.GetFileName(filePath));
						return false;
					}
					await Task.Delay(40).ConfigureAwait(continueOnCapturedContext: false);
				}
				attempt++;
			}
		}
		finally
		{
			_writeGate.Release();
		}
	}
}
