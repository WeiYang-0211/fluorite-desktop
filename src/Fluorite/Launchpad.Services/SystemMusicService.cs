using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Launchpad.Services;

public sealed class SystemMusicService : IDisposable
{
	private GlobalSystemMediaTransportControlsSessionManager? _manager;

	private GlobalSystemMediaTransportControlsSession? _session;

	private readonly SemaphoreSlim _serial = new SemaphoreSlim(1, 1);

	private bool _disposed;

	private string? _artKey;

	private byte[]? _artwork;

	private string? _lastError;

	public async Task<MusicState> ReadAsync(CancellationToken cancellation)
	{
		await _serial.WaitAsync(cancellation);
		try
		{
			if (_disposed)
			{
				return MusicState.Empty;
			}
			if ((object)_manager == null)
			{
				_manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(cancellation);
			}
			_session = _manager.GetCurrentSession();
			if (_session == null)
			{
				_artKey = null;
				_artwork = null;
				return MusicState.Empty;
			}
			GlobalSystemMediaTransportControlsSession session = _session;
			GlobalSystemMediaTransportControlsSessionMediaProperties metadata = await session.TryGetMediaPropertiesAsync().AsTask(cancellation);
			GlobalSystemMediaTransportControlsSessionPlaybackInfo info = session.GetPlaybackInfo();
			string key = session.SourceAppUserModelId + "|" + metadata.Title + "|" + metadata.Artist + "|" + metadata.AlbumTitle;
			byte[] art = ((key == _artKey) ? _artwork : null);
			if (key != _artKey && metadata.Thumbnail != null)
			{
				using IRandomAccessStreamWithContentType stream = await metadata.Thumbnail.OpenReadAsync().AsTask(cancellation);
				ulong size = stream.Size;
				if (size != 0 && size <= 2000000)
				{
					using DataReader reader = new DataReader(stream);
					await reader.LoadAsync((uint)stream.Size).AsTask(cancellation);
					art = new byte[(uint)stream.Size];
					reader.ReadBytes(art);
				}
			}
			_artKey = key;
			_artwork = art;
			_lastError = null;
			double progress = -1.0;
			try
			{
				GlobalSystemMediaTransportControlsSessionTimelineProperties timelineProperties = session.GetTimelineProperties();
				double totalSeconds = (timelineProperties.EndTime - timelineProperties.StartTime).TotalSeconds;
				if (totalSeconds > 0.0)
				{
					double pageNumber = (timelineProperties.Position - timelineProperties.StartTime).TotalSeconds;
					if (info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
					{
						double totalSeconds2 = (DateTimeOffset.UtcNow - timelineProperties.LastUpdatedTime).TotalSeconds;
						if (totalSeconds2 >= 0.0 && totalSeconds2 < totalSeconds)
						{
							pageNumber += totalSeconds2 * (info.PlaybackRate ?? 1.0);
						}
					}
					progress = Math.Clamp(pageNumber / totalSeconds, 0.0, 1.0);
				}
			}
			catch
			{
			}
			return new MusicState(string.IsNullOrWhiteSpace(metadata.Title) ? "正在播放" : metadata.Title, metadata.Artist ?? "", session.SourceAppUserModelId, info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing, info.Controls.IsPlayPauseToggleEnabled, info.Controls.IsNextEnabled, info.Controls.IsPreviousEnabled, art, null, progress);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex2)
		{
			if (_lastError != ex2.Message)
			{
				App.Log("Music session unavailable: " + ex2.Message);
			}
			_lastError = ex2.Message;
			_manager = null;
			_session = null;
			return MusicState.Empty with
			{
				Artist = "暂时无法读取播放器，可稍后重试",
				Error = ex2.Message
			};
		}
		finally
		{
			_serial.Release();
		}
	}

	public async Task<bool> ControlAsync(string action, CancellationToken cancellation)
	{
		await _serial.WaitAsync(cancellation);
		try
		{
			if (_disposed || _manager == null)
			{
				return false;
			}
			GlobalSystemMediaTransportControlsSession currentSession = _manager.GetCurrentSession();
			if (currentSession == null)
			{
				return false;
			}
			GlobalSystemMediaTransportControlsSessionPlaybackControls controls = currentSession.GetPlaybackInfo().Controls;
			bool result;
			switch (action)
			{
			case "toggle":
				if (controls.IsPlayPauseToggleEnabled)
				{
					result = await currentSession.TryTogglePlayPauseAsync().AsTask(cancellation);
					break;
				}
				goto default;
			case "next":
				if (controls.IsNextEnabled)
				{
					result = await currentSession.TrySkipNextAsync().AsTask(cancellation);
					break;
				}
				goto default;
			case "previous":
				if (controls.IsPreviousEnabled)
				{
					result = await currentSession.TrySkipPreviousAsync().AsTask(cancellation);
					break;
				}
				goto default;
			default:
				result = false;
				break;
			}
			return result;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex2)
		{
			App.Log("Music control: " + ex2.Message);
			return false;
		}
		finally
		{
			_serial.Release();
		}
	}

	public void Dispose()
	{
		_disposed = true;
		_session = null;
		_manager = null;
	}
}
