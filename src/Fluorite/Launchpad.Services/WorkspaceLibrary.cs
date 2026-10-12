using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Launchpad.Models;

namespace Launchpad.Services;

public sealed class WorkspaceLibrary
{
	private readonly string _root;

	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private const int MaxBytes = 104857600;

	public WorkspaceLibrary(string directory)
	{
		_root = Path.Combine(directory, "workspace-library");
	}

	public static T Clone<T>(T value)
	{
		return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Json), Json);
	}

	public static WorkspaceBundle CreateFreshScene(string name, IEnumerable<AppItem> apps)
	{
		List<AppItem> list = (from g in (from a in apps.SelectMany(Flatten)
				where !a.IsFolder && !a.IsWidget
				select a).GroupBy((AppItem a) => a.ExecutablePath, StringComparer.OrdinalIgnoreCase)
			select Clone(g.First())).OrderBy((AppItem a) => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
		for (int pageNumber = 0; pageNumber < list.Count; pageNumber++)
		{
			AppItem appItem = list[pageNumber];
			appItem.IsDock = false;
			appItem.DockIndex = 0;
			appItem.FolderId = null;
			appItem.IsHidden = false;
			appItem.IconOverridePath = null;
			appItem.CustomName = null;
			appItem.SortIndex = pageNumber;
		}
		return new WorkspaceBundle
		{
			Name = name,
			Complete = true,
			Settings = new AppSettings(),
			Applications = list
		};
	}

	public async Task<WorkspaceBundle> CaptureAsync(string name, AppSettings settings, IEnumerable<AppItem> apps, IStorageService storage, bool complete)
	{
		WorkspaceBundle bundle = new WorkspaceBundle
		{
			Name = name,
			Settings = Clone(settings),
			Complete = complete
		};
		if (!complete)
		{
			return bundle;
		}
		bundle.Applications = Clone(apps.ToList());
		WorkspaceBundle workspaceBundle = bundle;
		workspaceBundle.ManualApps = Clone(await storage.LoadManualAppsAsync());
		workspaceBundle = bundle;
		workspaceBundle.Folders = Clone(await storage.LoadFoldersAsync());
		if (File.Exists(bundle.Settings.CustomBackdropPath))
		{
			AppSettings settings2 = bundle.Settings;
			settings2.CustomBackdropPath = await AddAssetAsync(bundle, bundle.Settings.CustomBackdropPath);
		}
		foreach (AppItem item in bundle.Applications.Concat(bundle.ManualApps).SelectMany(Flatten))
		{
			if (!string.IsNullOrEmpty(item.IconOverridePath) && File.Exists(item.IconOverridePath))
			{
				AppItem appItem = item;
				appItem.IconOverridePath = await AddAssetAsync(bundle, item.IconOverridePath);
			}
		}
		IconPack pack = IconPackService.Find(bundle.Settings.IconPack);
		if (pack != null && !pack.IsOfficial)
		{
			foreach (string item2 in Directory.EnumerateFiles(pack.RootDirectory, "*", SearchOption.AllDirectories))
			{
				string key = "pack/" + Path.GetRelativePath(pack.RootDirectory, item2).Replace('\\', '/');
				await AddAssetAsync(bundle, item2, key);
			}
		}
		return bundle;
	}

	private static async Task<string> AddAssetAsync(WorkspaceBundle bundle, string path, string? key = null)
	{
		if (key == null)
		{
			key = "images/" + Guid.NewGuid().ToString("N") + Path.GetExtension(path);
		}
		if (new FileInfo(path).Length + ((IEnumerable<byte[]>)bundle.Assets.Values).Sum((Func<byte[], long>)((byte[] b) => b.Length)) > 104857600)
		{
			throw new InvalidDataException("备份资源超过 100 MB，请缩小背景图片或图标包");
		}
		Dictionary<string, byte[]> assets = bundle.Assets;
		string key2 = key;
		assets[key2] = await File.ReadAllBytesAsync(path);
		return "asset://" + key;
	}

	public async Task WriteAsync(string path, WorkspaceBundle bundle)
	{
		Validate(bundle);
		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
		string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			await File.WriteAllTextAsync(temp, await Task.Run(() => JsonSerializer.Serialize(bundle, Json)));
			File.Move(temp, path, overwrite: true);
		}
		finally
		{
			if (File.Exists(temp))
			{
				File.Delete(temp);
			}
		}
	}

	public async Task<WorkspaceBundle> ReadAsync(string path)
	{
		if (new FileInfo(path).Length > 209715200)
		{
			throw new InvalidDataException("备份文件过大");
		}
		string content = await File.ReadAllTextAsync(path);
		WorkspaceBundle workspaceBundle = (await Task.Run(() => JsonSerializer.Deserialize<WorkspaceBundle>(content, Json))) ?? throw new InvalidDataException("无法读取备份");
		Validate(workspaceBundle);
		return workspaceBundle;
	}

	private void Validate(WorkspaceBundle bundle, bool nested = false)
	{
		if (bundle.Version != 1 || bundle.Settings == null || bundle.Applications == null || bundle.ManualApps == null || bundle.Folders == null || bundle.Assets == null || bundle.SavedLayouts == null)
		{
			throw new InvalidDataException("不支持的备份格式");
		}
		if (((IEnumerable<byte[]>)bundle.Assets.Values).Sum((Func<byte[], long>)((byte[] b) => b.Length)) > 104857600 || bundle.Applications.Count > 10000 || bundle.ManualApps.Count > 10000 || bundle.SavedLayouts.Count > 100 || (nested && bundle.SavedLayouts.Count != 0))
		{
			throw new InvalidDataException("备份内容超出限制");
		}
		if (!bundle.Complete && (bundle.Assets.Count != 0 || bundle.Applications.Count != 0 || bundle.SavedLayouts.Count != 0))
		{
			throw new InvalidDataException("基础设置不能包含完整布局资源");
		}
		foreach (string key in bundle.Assets.Keys)
		{
			ResolveInside(Path.Combine(_root, "assets"), key);
		}
		foreach (WorkspaceBundle savedLayout in bundle.SavedLayouts)
		{
			Validate(savedLayout, nested: true);
		}
		if (!nested && ((IEnumerable<byte[]>)bundle.Assets.Values).Sum((Func<byte[], long>)((byte[] b) => b.Length)) + bundle.SavedLayouts.Sum((WorkspaceBundle s) => ((IEnumerable<byte[]>)s.Assets.Values).Sum((Func<byte[], long>)((byte[] b) => b.Length))) > 104857600)
		{
			throw new InvalidDataException("所有布局的资源合计超过 100 MB");
		}
	}

	internal static string ResolveInside(string root, string relative)
	{
		string fullPath = Path.GetFullPath(Path.Combine(root, relative));
		if (!fullPath.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("资源路径无效");
		}
		return fullPath;
	}

	public async Task MaterializeAsync(WorkspaceBundle bundle)
	{
		string root = Path.Combine(_root, "assets", Guid.NewGuid().ToString("N"));
		foreach (var (relative, bytes) in bundle.Assets)
		{
			string path = ResolveInside(root, relative);
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			await File.WriteAllBytesAsync(path, bytes);
		}
		bundle.Settings.CustomBackdropPath = Resolve(bundle.Settings.CustomBackdropPath) ?? "";
		foreach (AppItem item in bundle.Applications.Concat(bundle.ManualApps).SelectMany(Flatten))
		{
			item.IconOverridePath = Resolve(item.IconOverridePath);
		}
		if (!bundle.Assets.Keys.Any((string k) => k.StartsWith("pack/", StringComparison.Ordinal)))
		{
			return;
		}
		string packRoot = Path.Combine(root, "pack");
		string manifest = Path.Combine(packRoot, "pack.json");
		JsonNode jsonNode = JsonNode.Parse(await File.ReadAllTextAsync(manifest));
		foreach (KeyValuePair<string, JsonNode> item2 in (jsonNode["icons"] as JsonObject) ?? throw new InvalidDataException("图标包清单无效"))
		{
			if (!File.Exists(ResolveInside(packRoot, item2.Value?.GetValue<string>() ?? "")))
			{
				throw new InvalidDataException("图标包缺少图片");
			}
		}
		JsonNode jsonNode2 = jsonNode["default"];
		if (jsonNode2 != null && !File.Exists(ResolveInside(packRoot, jsonNode2.GetValue<string>())))
		{
			throw new InvalidDataException("图标包缺少默认图片");
		}
		string id = "restored-" + Guid.NewGuid().ToString("N");
		jsonNode["id"] = id;
		await File.WriteAllTextAsync(manifest, jsonNode.ToJsonString());
		string destDirName = Path.Combine(IconPackService.UserDirectory, id);
		Directory.CreateDirectory(IconPackService.UserDirectory);
		Directory.Move(packRoot, destDirName);
		IconPackService.Invalidate();
		bundle.Settings.IconPack = id;
		string? Resolve(string? commandLinePath)
		{
			if (commandLinePath == null || !commandLinePath.StartsWith("asset://", StringComparison.Ordinal))
			{
				return commandLinePath;
			}
			Dictionary<string, byte[]> assets = bundle.Assets;
			string text3 = commandLinePath;
			if (!assets.ContainsKey(text3.Substring(8, text3.Length - 8)))
			{
				throw new InvalidDataException("备份缺少资源");
			}
			string root2 = root;
			text3 = commandLinePath;
			return ResolveInside(root2, text3.Substring(8, text3.Length - 8));
		}
	}

	private static IEnumerable<AppItem> Flatten(AppItem app)
	{
		yield return app;
		foreach (AppItem child in app.Children)
		{
			foreach (AppItem item in Flatten(child))
			{
				yield return item;
			}
		}
	}

	public async Task<string> SaveAsync(string category, WorkspaceBundle bundle, string? id = null)
	{
		if (id == null)
		{
			id = Guid.NewGuid().ToString("N");
		}
		if (category == "layouts" && File.Exists(EntryPath(category, id)))
		{
			bundle.CreatedUtc = (await LoadAsync(category, id)).CreatedUtc;
		}
		await WriteAsync(EntryPath(category, id), bundle);
		if (category == "history")
		{
			foreach (WorkspaceEntry item in List(category).Skip(20))
			{
				File.Delete(EntryPath(category, item.Id));
			}
		}
		return id;
	}

	private string EntryPath(string category, string id)
	{
		bool validated = ((category == "layouts" || category == "history") ? true : false);
		if (!validated || !Guid.TryParseExact(id, "N", out var _))
		{
			throw new InvalidDataException("布局标识无效");
		}
		return Path.Combine(_root, category, id + ".json");
	}

	public Task<WorkspaceBundle> LoadAsync(string category, string id)
	{
		return ReadAsync(EntryPath(category, id));
	}

	public void Delete(string category, string id)
	{
		File.Delete(EntryPath(category, id));
	}

	public async Task SaveSceneOrderAsync(IEnumerable<string> ids)
	{
		Directory.CreateDirectory(_root);
		string path = Path.Combine(_root, "scene-order.json");
		string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(ids.Distinct().ToArray()));
			File.Move(temp, path, overwrite: true);
		}
		finally
		{
			if (File.Exists(temp))
			{
				File.Delete(temp);
			}
		}
	}

	public List<WorkspaceEntry> List(string category)
	{
		string path = Path.Combine(_root, category);
		if (!Directory.Exists(path))
		{
			return new List<WorkspaceEntry>();
		}
		List<WorkspaceEntry> list = new List<WorkspaceEntry>();
		foreach (string item in Directory.EnumerateFiles(path, "*.json"))
		{
			try
			{
				using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(item));
				list.Add(new WorkspaceEntry(Path.GetFileNameWithoutExtension(item), jsonDocument.RootElement.GetProperty("Name").GetString() ?? "布局", jsonDocument.RootElement.GetProperty("CreatedUtc").GetDateTime(), jsonDocument.RootElement.TryGetProperty("Applications", out var value) ? new int?(value.GetArrayLength()) : ((int?)null), jsonDocument.RootElement.TryGetProperty("Folders", out var failure) ? failure.GetArrayLength() : 0, jsonDocument.RootElement.TryGetProperty("Complete", out var value3) && value3.GetBoolean()));
			}
			catch (Exception ex)
			{
				App.Log("Skipped unreadable workspace: " + ex.Message);
			}
		}
		if (category != "layouts")
		{
			return list.OrderByDescending((WorkspaceEntry e) => e.CreatedUtc).ToList();
		}
		List<WorkspaceEntry> list2 = list.OrderBy((WorkspaceEntry e) => e.CreatedUtc).ThenBy((WorkspaceEntry e) => e.Id, StringComparer.Ordinal).ToList();
		try
		{
			string path2 = Path.Combine(_root, "scene-order.json");
			string[] source = (File.Exists(path2) ? (JsonSerializer.Deserialize<string[]>(File.ReadAllText(path2)) ?? Array.Empty<string>()) : Array.Empty<string>());
			Dictionary<string, int> rank = source.Distinct().Select((string id, int index) => (id: id, index: index)).ToDictionary(((string id, int index) x) => x.id, ((string id, int index) x) => x.index);
			return list2.OrderBy((WorkspaceEntry e) => (!rank.TryGetValue(e.Id, out var value4)) ? int.MaxValue : value4).ToList();
		}
		catch (Exception ex2)
		{
			App.Log("Scene order unavailable: " + ex2.Message);
			return list2;
		}
	}
}
