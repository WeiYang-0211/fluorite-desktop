using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace Launchpad.Infrastructure;

internal sealed class BufferedLog : IDisposable
{
	private readonly BlockingCollection<string> _queue = new BlockingCollection<string>(4096);

	private readonly Thread _worker;

	public BufferedLog(string path)
	{
		BufferedLog bufferedLog = this;
		_worker = new Thread(() =>
		{
			bufferedLog.WriteLoop(path);
		})
		{
			IsBackground = true,
			Name = "fluorite-log"
		};
		_worker.Start();
	}

	public void Write(string message)
	{
		try
		{
			_queue.TryAdd($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\r\n");
		}
		catch (InvalidOperationException)
		{
		}
	}

	private void WriteLoop(string path)
	{
		while (!_queue.IsCompleted)
		{
			if (!_queue.TryTake(out string item, 100))
			{
				continue;
			}
			StringBuilder stringBuilder = new StringBuilder(item);
			string item2;
			while (stringBuilder.Length < 65536 && _queue.TryTake(out item2))
			{
				stringBuilder.Append(item2);
			}
			try
			{
				if (File.Exists(path) && new FileInfo(path).Length > 4194304)
				{
					File.Move(path, path + ".previous", overwrite: true);
				}
				File.AppendAllText(path, stringBuilder.ToString());
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
				path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fluorite", "fluorite.log");
				try
				{
					Directory.CreateDirectory(Path.GetDirectoryName(path));
					File.AppendAllText(path, stringBuilder.ToString());
				}
				catch (IOException)
				{
				}
				catch (UnauthorizedAccessException)
				{
				}
			}
		}
	}

	public void Dispose()
	{
		_queue.CompleteAdding();
		_worker.Join(TimeSpan.FromSeconds(2.0));
	}
}
