using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Launchpad.Infrastructure;
using Launchpad.Models;

namespace Launchpad.Services.Update;

public sealed class UpdateService
{
	public static readonly TimeSpan AutoCheckInterval = TimeSpan.FromHours(12.0);

	public const int FirstCheckDelaySeconds = 25;

	public const long MinInstallerBytes = 1048576L;

	public const long MaxInstallerBytes = 536870912L;

	private readonly IStorageService _storage;

	private readonly HttpClient _download;

	private readonly SemaphoreSlim _checkGate = new SemaphoreSlim(1, 1);

	private UpdateState _state = new UpdateState();

	private bool _loopStarted;

	public UpdateInfo? Pending { get; private set; }

	public Func<bool>? AutoCheckEnabled { get; set; }

	public string CurrentVersion => FluoriteVersion.Text;

	public DateTime LastCheckedAt => _state.LastCheckedAt;

	public string PackageDirectory => Path.Combine(_storage.StorageDirectory, "updates");

	public event Action? Changed;

	public event Action<UpdateInfo>? UpdateFound;

	public UpdateService(IStorageService storage)
	{
		_storage = storage;
		_download = new HttpClient(new SocketsHttpHandler
		{
			ConnectTimeout = TimeSpan.FromSeconds(10.0),
			PooledConnectionLifetime = TimeSpan.FromMinutes(10.0)
		})
		{
			Timeout = Timeout.InfiniteTimeSpan
		};
		_download.DefaultRequestHeaders.UserAgent.ParseAdd("Fluorite/" + FluoriteVersion.Text + " (Windows NT; updater)");
	}

	public async Task StartupAsync()
	{
		if (DistributionChannel.IsStore || !UpdateEndpoint.Enabled)
		{
			// No release feed is configured (common for forks), so the updater
			// stays quiet rather than hammering an unreachable host.
			App.Log("[UPDATE] no release endpoint configured, auto-update disabled");
			return;
		}
		if (!DistributionChannel.IsStore)
		{
			try
			{
				_state = await _storage.LoadUpdateStateAsync().ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (Exception ex)
			{
				App.Log("[UPDATE] state unreadable: " + ex.Message);
			}
			await Task.Delay(TimeSpan.FromSeconds(25.0)).ConfigureAwait(continueOnCapturedContext: false);
			StartLoop();
			await RunAutomaticCheckAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private void StartLoop()
	{
		if (_loopStarted)
		{
			return;
		}
		_loopStarted = true;
		Task.Run(async () =>
		{
			while (true)
			{
				await Task.Delay(AutoCheckInterval).ConfigureAwait(continueOnCapturedContext: false);
				await RunAutomaticCheckAsync().ConfigureAwait(continueOnCapturedContext: false);
			}
		});
	}

	private async Task RunAutomaticCheckAsync()
	{
		if (AutoCheckEnabled != null && !AutoCheckEnabled())
		{
			return;
		}
		CheckResult checkResult = await CheckAsync(manual: false).ConfigureAwait(continueOnCapturedContext: false);
		if (checkResult.Ok && !(checkResult.Info == null))
		{
			UpdateInfo info = checkResult.Info;
			if (!IsPostponed(info.Version))
			{
				App.Log("[UPDATE] prompting for " + info.Version);
				Raise();
				UpdateFound?.Invoke(info);
			}
		}
	}

	public async Task<CheckResult> CheckAsync(bool manual)
	{
		if (DistributionChannel.IsStore)
		{
			return new CheckResult(Ok: true, "此版本由 Microsoft Store 更新，请在商店的下载页面检查更新。", null);
		}
		if (!UpdateEndpoint.Enabled)
		{
			return CheckResult.Failed("尚未配置更新服务器，已停用自动检查更新");
		}
		await _checkGate.WaitAsync().ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			LatestResponse latest;
			try
			{
				latest = await LatestResponse.FetchAsync("windows").ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (UpdateTransportException ex)
			{
				return CheckResult.Failed(manual ? ex.Message : "无法连接更新服务器");
			}
			catch (Exception value)
			{
				App.Log($"[UPDATE] check threw: {value}");
				return CheckResult.Failed("检查更新失败，详见日志");
			}
			_state.LastCheckedAt = DateTime.Now;
			await _storage.SaveUpdateStateAsync(_state).ConfigureAwait(continueOnCapturedContext: false);
			if (!latest.Ok)
			{
				return CheckResult.Failed(string.IsNullOrEmpty(latest.Error) ? "更新服务器拒绝了请求" : latest.Error);
			}
			string text = (latest.Version ?? string.Empty).Trim();
			if (!FluoriteVersion.IsNewer(text))
			{
				Pending = null;
				return CheckResult.UpToDate(FluoriteVersion.Text);
			}
			UpdateInfo info = new UpdateInfo(text, (latest.Notes ?? string.Empty).Trim(), (latest.DownloadUrl ?? string.Empty).Trim(), string.IsNullOrWhiteSpace(latest.Sha256) ? null : latest.Sha256.Trim(), latest.SizeBytes);
			if (string.IsNullOrEmpty(info.DownloadUrl))
			{
				App.Log("[UPDATE] " + text + " has no download url published");
				return CheckResult.Failed("服务器发布了 " + text + "，但没有可下载的更新包");
			}
			string commandLinePath = Reject(info);
			if (commandLinePath != null)
			{
				App.Log("[UPDATE] " + text + " refused: " + commandLinePath);
				return CheckResult.Failed(commandLinePath);
			}
			string text3 = PackagePath(info.Version);
			if (File.Exists(text3) && Verify(text3, info, out string _))
			{
				info = info with
				{
					LocalPath = text3
				};
				_state.DownloadedVersion = info.Version;
				_state.DownloadedPath = text3;
				await _storage.SaveUpdateStateAsync(_state).ConfigureAwait(continueOnCapturedContext: false);
			}
			Pending = info;
			App.Log($"[UPDATE] {FluoriteVersion.Text} -> {info.Version} available: {info.DownloadUrl}");
			return new CheckResult(Ok: true, "发现新版本 " + info.Version, info);
		}
		finally
		{
			_checkGate.Release();
			Raise();
		}
	}

	public async Task<string> DownloadAsync(UpdateInfo info, IProgress<DownloadProgress>? progress, CancellationToken ct)
	{
		if (DistributionChannel.IsStore)
		{
			throw new InvalidOperationException("此版本由 Microsoft Store 更新，请在商店的下载页面检查更新。");
		}
		string text = Reject(info);
		if (text != null)
		{
			throw new InvalidOperationException(text);
		}
		string target = PackagePath(info.Version);
		string part = target + ".part";
		Directory.CreateDirectory(Path.GetDirectoryName(target));
		if (info.AlreadyDownloaded && Verify(info.LocalPath, info, out string _))
		{
			return info.LocalPath;
		}
		App.Log("[UPDATE] downloading " + info.DownloadUrl + " -> " + target);
		using HttpResponseMessage response = await _download.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(continueOnCapturedContext: false);
		response.EnsureSuccessStatusCode();
		long? declared = response.Content.Headers.ContentLength;
		if (declared.HasValue && (declared.Value < 1048576 || declared.Value > 536870912))
		{
			throw new InvalidOperationException($"更新包大小异常（{declared.Value} 字节）");
		}
		await using (Stream remote = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(continueOnCapturedContext: false))
		{
			await using FileStream local = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 131072, useAsync: true);
			byte[] buffer = new byte[131072];
			long done = 0L;
			Stopwatch clock = Stopwatch.StartNew();
			TimeSpan lastReport = TimeSpan.Zero;
			while (true)
			{
				int pageNumber;
				int read = (pageNumber = await remote.ReadAsync(buffer, ct).ConfigureAwait(continueOnCapturedContext: false));
				if (pageNumber <= 0)
				{
					break;
				}
				await local.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(continueOnCapturedContext: false);
				done += read;
				if (clock.Elapsed - lastReport > TimeSpan.FromMilliseconds(120.0))
				{
					lastReport = clock.Elapsed;
					progress?.Report(new DownloadProgress(done, declared.GetValueOrDefault(), (double)done / Math.Max(0.001, clock.Elapsed.TotalSeconds)));
				}
			}
			await local.FlushAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
			progress?.Report(new DownloadProgress(done, declared ?? done, (double)done / Math.Max(0.001, clock.Elapsed.TotalSeconds)));
		}
		if (new FileInfo(part).Length < 1048576)
		{
			throw new InvalidOperationException("更新包不完整，请重新下载");
		}
		File.Move(part, target, overwrite: true);
		if (!Verify(target, info, out string error2))
		{
			try
			{
				File.Delete(target);
			}
			catch
			{
			}
			throw new InvalidOperationException(error2);
		}
		_state.DownloadedVersion = info.Version;
		_state.DownloadedPath = target;
		_state.DownloadedSize = new FileInfo(target).Length;
		if (info.Sha256 != null)
		{
			_state.DownloadedSha256 = info.Sha256.ToLowerInvariant();
		}
		await _storage.SaveUpdateStateAsync(_state).ConfigureAwait(continueOnCapturedContext: false);
		PruneOldPackages(target);
		App.Log("[UPDATE] downloaded and verified " + target);
		return target;
	}

	public bool Verify(string path, UpdateInfo info, out string error)
	{
		error = string.Empty;
		if (!File.Exists(path))
		{
			error = "更新包不存在，需要重新下载";
			return false;
		}
		FileInfo fileInfo = new FileInfo(path);
		if (fileInfo.Length < 1048576 || fileInfo.Length > 536870912)
		{
			error = "更新包大小不符合预期，已拒绝";
			return false;
		}
		_ = new byte[2];
		try
		{
			using FileStream fileStream = File.OpenRead(path);
			if (fileStream.ReadByte() != 77 || fileStream.ReadByte() != 90)
			{
				error = "更新包不是可执行文件，已拒绝";
				return false;
			}
		}
		catch (Exception ex)
		{
			error = "更新包无法读取：" + ex.Message;
			return false;
		}
		if (info.Sha256 != null && !string.Equals(Sha256Of(path), info.Sha256, StringComparison.OrdinalIgnoreCase))
		{
			error = "更新包校验值与服务器公布的不一致，已拒绝";
			return false;
		}
		string text = EmbeddedVersion(path);
		if (text == null)
		{
			if (!UpdateEndpoint.IsLoopback)
			{
				error = "更新包没有版本信息，无法确认它就是本次发布，已拒绝";
				return false;
			}
			App.Log("[UPDATE] mock package without a version resource accepted");
		}
		else if (!FluoriteVersion.SameRelease(text, info.Version))
		{
			error = $"更新包内嵌版本 {text} 与服务器公布的 {info.Version} 不符，已拒绝";
			return false;
		}
		return true;
	}

	public void InstallAndExit(string path)
	{
		if (DistributionChannel.IsStore)
		{
			throw new InvalidOperationException("此版本由 Microsoft Store 更新，请在商店的下载页面检查更新。");
		}
		if (!File.Exists(path))
		{
			throw new FileNotFoundException("更新包不存在", path);
		}
		string text = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fluorite.exe");
		string value = BuildInstallerArguments(Path.GetDirectoryName(text));
		string commandLinePath = "$ErrorActionPreference='SilentlyContinue';" + $"$p = Start-Process -FilePath '{Quote(path)}' -ArgumentList '{Quote(value)}' -Wait -PassThru;" + "if ($p.ExitCode -eq 0) { Start-Process -FilePath '" + Quote(text) + "' }";
		if (Process.Start(new ProcessStartInfo
		{
			FileName = "powershell.exe",
			Arguments = "-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"" + commandLinePath.Replace("\"", "\\\"") + "\"",
			UseShellExecute = false,
			CreateNoWindow = true
		}) == null)
		{
			throw new InvalidOperationException("无法启动安装程序");
		}
		App.Log("[UPDATE] installer launched for " + path + "; exiting so the files can be replaced");
	}

	internal static string BuildInstallerArguments(string directory)
	{
		string fullPath = Path.GetFullPath(directory);
		bool validated = new string[2]
		{
			Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
			Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
		}.Any((string root) => !string.IsNullOrEmpty(root) && fullPath.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
		return $"/SILENT /SUPPRESSMSGBOXES /NORESTART {(validated ? "/ALLUSERS" : "/CURRENTUSER")} /DIR=\"{fullPath}\"";
	}

	public bool IsPostponed(string version)
	{
		if (string.Equals(_state.DismissedVersion, version, StringComparison.OrdinalIgnoreCase))
		{
			return _state.SnoozedUntil > DateTime.Now;
		}
		return false;
	}

	public async Task SnoozeAsync(string version)
	{
		_state.DismissedVersion = version;
		_state.SnoozedUntil = DateTime.Now.AddHours(12.0);
		Pending = null;
		await _storage.SaveUpdateStateAsync(_state).ConfigureAwait(continueOnCapturedContext: false);
		Raise();
	}

	private string PackagePath(string version)
	{
		string text = version.Replace("\\", "_").Replace("/", "_").Replace(":", "_");
		return Path.Combine(PackageDirectory, "Fluorite_Setup_" + text + ".exe");
	}

	private static string? Reject(UpdateInfo info)
	{
		if (!Uri.TryCreate(info.DownloadUrl, UriKind.Absolute, out Uri result))
		{
			return "更新下载地址无法解析，已拒绝";
		}
		bool validated = result.Scheme == Uri.UriSchemeHttps;
		bool isLoopback = result.IsLoopback;
		if (!validated && !isLoopback)
		{
			return "更新包必须通过 HTTPS 下载，已拒绝";
		}
		if (result.IsLoopback && UpdateEndpoint.IsLoopback && !validated)
		{
			App.Log("[UPDATE] plain-http download accepted because both the server and the package host are loopback");
		}
		return null;
	}

	private void PruneOldPackages(string keep)
	{
		try
		{
			string[] files = Directory.GetFiles(PackageDirectory, "Fluorite_Setup_*.exe");
			foreach (string text in files)
			{
				if (!string.Equals(text, keep, StringComparison.OrdinalIgnoreCase))
				{
					File.Delete(text);
					App.Log("[UPDATE] removed superseded package " + Path.GetFileName(text));
				}
			}
		}
		catch (Exception ex)
		{
			App.Log("[UPDATE] package prune failed: " + ex.Message);
		}
	}

	private static string Sha256Of(string path)
	{
		using FileStream source = File.OpenRead(path);
		return Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
	}

	private static string? EmbeddedVersion(string path)
	{
		try
		{
			FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(path);
			string text = versionInfo.ProductVersion;
			if (string.IsNullOrWhiteSpace(text))
			{
				text = versionInfo.FileVersion;
			}
			return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
		}
		catch (Exception ex)
		{
			App.Log("[UPDATE] version resource unreadable: " + ex.Message);
			return null;
		}
	}

	private static string Quote(string value)
	{
		return value.Replace("'", "''");
	}

	private void Raise()
	{
		Dispatcher dispatcher = Application.Current?.Dispatcher;
		if (dispatcher == null)
		{
			Changed?.Invoke();
			return;
		}
		dispatcher.BeginInvoke((Action)(() =>
		{
			Changed?.Invoke();
		}));
	}
}
