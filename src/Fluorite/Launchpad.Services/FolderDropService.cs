using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Launchpad.Services;

internal static class FolderDropService
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct ShellOperation
	{
		public nint Owner;

		public uint Operation;

		[MarshalAs(UnmanagedType.LPWStr)]
		public string From;

		[MarshalAs(UnmanagedType.LPWStr)]
		public string To;

		public ushort Flags;

		[MarshalAs(UnmanagedType.Bool)]
		public bool Aborted;

		public nint NameMappings;

		[MarshalAs(UnmanagedType.LPWStr)]
		public string? ProgressTitle;
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	private static extern int SHFileOperation(ref ShellOperation operation);

	internal static string[] ValidateSources(string destination, IEnumerable<string> sources)
	{
		destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
		if (!Directory.Exists(destination))
		{
			throw new IOException("目标文件夹不存在。");
		}
		string[] paths = sources.Select((string p) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
		if (paths.Length == 0)
		{
			throw new IOException("没有可放入的文件。");
		}
		string[] entries = paths;
		foreach (string text in entries)
		{
			if (!File.Exists(text) && !Directory.Exists(text))
			{
				throw new IOException("找不到：" + text);
			}
			if (string.Equals(text, Path.GetPathRoot(text), StringComparison.OrdinalIgnoreCase))
			{
				throw new IOException("不能移动整个磁盘。");
			}
			if (string.Equals(text, destination, StringComparison.OrdinalIgnoreCase) || (Directory.Exists(text) && destination.StartsWith(text + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
			{
				throw new IOException("不能把文件夹放入自身或它的子文件夹。");
			}
			if (string.Equals(Path.GetDirectoryName(text), destination, StringComparison.OrdinalIgnoreCase))
			{
				throw new IOException("文件已经在目标文件夹中。");
			}
		}
		return paths.Where((string path) => !paths.Any((string parent) => parent != path && Directory.Exists(parent) && path.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))).ToArray();
	}

	internal static Task<string> TransferAsync(nint owner, string destination, IEnumerable<string> sources, bool copy = false)
	{
		TaskCompletionSource<string> pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		string[] input = sources.ToArray();
		Thread thread = new Thread(() =>
		{
			try
			{
				string[] value = ValidateSources(destination, input);
				ShellOperation operation = new ShellOperation
				{
					Owner = owner,
					Operation = ((!copy) ? 1u : 2u),
					From = string.Join('\0', value) + "\0\0",
					To = Path.GetFullPath(destination) + "\0\0",
					Flags = 576
				};
				int pageNumber = SHFileOperation(ref operation);
				TaskCompletionSource<string> taskCompletionSource = pending;
				string result = (operation.Aborted ? "操作已取消，部分文件可能已处理。" : ((pageNumber != 0) ? $"文件操作未完成（错误代码 {pageNumber}），请检查目标文件夹。" : (copy ? "已复制到文件夹。" : "已移入文件夹。")));
				taskCompletionSource.SetResult(result);
			}
			catch (Exception ex)
			{
				pending.SetResult("无法放入文件夹：" + ex.Message);
			}
		});
		thread.IsBackground = true;
		thread.Name = "Fluorite folder drop";
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		return pending.Task;
	}
}
