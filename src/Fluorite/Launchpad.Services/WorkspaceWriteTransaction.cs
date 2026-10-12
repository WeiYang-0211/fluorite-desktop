using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Launchpad.Services;

internal static class WorkspaceWriteTransaction
{
	internal const string JournalName = "workspace-restore-journal.json";

	private static readonly string[] Names = new string[4] { "applications.json", "manual-apps.json", "folders.json", "settings.json" };

	internal static void Save(string root, Dictionary<string, object> values, JsonSerializerOptions options)
	{
		Recover(root);
		Dictionary<string, byte[]> dictionary = Names.ToDictionary((string name) => name, (string name) => (!File.Exists(Path.Combine(root, name))) ? null : File.ReadAllBytes(Path.Combine(root, name)));
		string path = Path.Combine(root, "workspace-restore-journal.json");
		List<string> list = new List<string>();
		try
		{
			string[] names = Names;
			foreach (string text in names)
			{
				File.WriteAllBytes(Path.Combine(root, text + ".restore.tmp"), JsonSerializer.SerializeToUtf8Bytes(values[text], options));
			}
			AtomicWrite(path, JsonSerializer.SerializeToUtf8Bytes(dictionary));
			names = Names;
			foreach (string commandLinePath in names)
			{
				File.Move(Path.Combine(root, commandLinePath + ".restore.tmp"), Path.Combine(root, commandLinePath), overwrite: true);
				list.Add(commandLinePath);
			}
			File.Delete(path);
		}
		catch
		{
			foreach (string item in list.AsEnumerable().Reverse())
			{
				Restore(root, item, dictionary[item]);
			}
			if (File.Exists(path))
			{
				File.Delete(path);
			}
			throw;
		}
		finally
		{
			string[] names = Names;
			foreach (string text3 in names)
			{
				try
				{
					File.Delete(Path.Combine(root, text3 + ".restore.tmp"));
				}
				catch (IOException)
				{
				}
			}
		}
	}

	internal static void Recover(string root)
	{
		string path = Path.Combine(root, "workspace-restore-journal.json");
		if (File.Exists(path))
		{
			Dictionary<string, byte[]?> originals = JsonSerializer.Deserialize<Dictionary<string, byte[]>>(File.ReadAllBytes(path)) ?? throw new InvalidDataException("布局恢复记录无效");
			if (originals.Count != Names.Length || Names.Any((string name) => !originals.ContainsKey(name)))
			{
				throw new InvalidDataException("布局恢复记录不完整");
			}
			string[] names = Names;
			foreach (string text in names)
			{
				Restore(root, text, originals[text]);
			}
			File.Delete(path);
		}
	}

	private static void Restore(string root, string name, byte[]? original)
	{
		string path = Path.Combine(root, name);
		if (original == null)
		{
			File.Delete(path);
		}
		else
		{
			AtomicWrite(path, original);
		}
	}

	private static void AtomicWrite(string path, byte[] content)
	{
		string text = path + ".rollback.tmp";
		try
		{
			File.WriteAllBytes(text, content);
			File.Move(text, path, overwrite: true);
		}
		finally
		{
			if (File.Exists(text))
			{
				File.Delete(text);
			}
		}
	}
}
