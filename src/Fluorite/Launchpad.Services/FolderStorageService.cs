using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualBasic.FileIO;

namespace Launchpad.Services;

internal static class FolderStorageService
{
	internal static string PlanDestination(string source, string storage)
	{
		source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
		storage = Path.TrimEndingDirectorySeparator(Path.GetFullPath(storage));
		if (!Directory.Exists(source))
		{
			throw new IOException("原文件夹不存在。");
		}
		if (string.Equals(source, Path.GetPathRoot(source), StringComparison.OrdinalIgnoreCase) || new Environment.SpecialFolder[11]
		{
			Environment.SpecialFolder.DesktopDirectory,
			Environment.SpecialFolder.UserProfile,
			Environment.SpecialFolder.Windows,
			Environment.SpecialFolder.System,
			Environment.SpecialFolder.ProgramFiles,
			Environment.SpecialFolder.ProgramFilesX86,
			Environment.SpecialFolder.CommonDesktopDirectory,
			Environment.SpecialFolder.ApplicationData,
			Environment.SpecialFolder.LocalApplicationData,
			Environment.SpecialFolder.CommonApplicationData,
			Environment.SpecialFolder.Personal
		}.Any((Environment.SpecialFolder folder) => string.Equals(source, Environment.GetFolderPath(folder), StringComparison.OrdinalIgnoreCase)))
		{
			throw new IOException("请选择需要收纳的子文件夹，不能移动整个桌面、磁盘或系统目录。");
		}
		if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
		{
			throw new IOException("请选择实际文件夹，不能收纳目录链接。");
		}
		if (string.Equals(source, storage, StringComparison.OrdinalIgnoreCase) || storage.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
		{
			throw new IOException("收纳目录不能位于原文件夹内部。");
		}
		if (source.StartsWith(storage + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
		{
			return source;
		}
		string fileName = Path.GetFileName(source);
		string text = Path.Combine(storage, fileName);
		int pageNumber = 2;
		while (Directory.Exists(text) || File.Exists(text))
		{
			text = Path.Combine(storage, $"{fileName} ({pageNumber})");
			pageNumber++;
		}
		return text;
	}

	internal static Task<string> MoveAsync(string source, string storage)
	{
		TaskCompletionSource<string> pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		Thread thread = new Thread(() =>
		{
			try
			{
				source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
				storage = Path.TrimEndingDirectorySeparator(Path.GetFullPath(storage));
				string text = PlanDestination(source, storage);
				if (!string.Equals(source, text, StringComparison.OrdinalIgnoreCase))
				{
					Directory.CreateDirectory(storage);
					if (Directory.Exists(text) || File.Exists(text))
					{
						throw new IOException("目标名称已被占用，请重新收纳。");
					}
					if (string.Equals(Path.GetPathRoot(source), Path.GetPathRoot(text), StringComparison.OrdinalIgnoreCase))
					{
						try
						{
							Directory.Move(source, text);
						}
						catch (IOException ex) when ((ex.HResult & 0xFFFF) == 17)
						{
							FileSystem.MoveDirectory(source, text, UIOption.AllDialogs, UICancelOption.ThrowException);
						}
					}
					else
					{
						FileSystem.MoveDirectory(source, text, UIOption.AllDialogs, UICancelOption.ThrowException);
					}
				}
				if (!Directory.Exists(text) || (source != text && Directory.Exists(source)))
				{
					throw new IOException("移动未完成，请检查原目录和收纳目录：" + storage);
				}
				pending.SetResult(text);
			}
			catch (Exception exception)
			{
				pending.SetException(exception);
			}
		});
		thread.IsBackground = true;
		thread.Name = "Fluorite folder storage";
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		return pending.Task;
	}
}
