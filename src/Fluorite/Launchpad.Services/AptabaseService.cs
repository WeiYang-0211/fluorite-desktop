using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Launchpad.Services;

public class AptabaseService : IAptabaseClient
{
	private sealed class CachedEvent
	{
		public string Id { get; set; } = Guid.NewGuid().ToString("N");

		public EventPayload Event { get; set; } = new EventPayload();
	}

	private class EventPayload
	{
		[JsonPropertyName("timestamp")]
		public string Timestamp { get; set; } = string.Empty;

		[JsonPropertyName("sessionId")]
		public string SessionId { get; set; } = string.Empty;

		[JsonPropertyName("eventName")]
		public string EventName { get; set; } = string.Empty;

		[JsonPropertyName("systemProps")]
		public SystemProperties? SystemProps { get; set; }

		[JsonPropertyName("props")]
		public Dictionary<string, object>? Props { get; set; }
	}

	private class ErrorPayload
	{
		[JsonPropertyName("errorMessage")]
		public string ErrorMessage { get; set; } = string.Empty;

		[JsonPropertyName("errorType")]
		public string ErrorType { get; set; } = string.Empty;

		[JsonPropertyName("stackTrace")]
		public string? StackTrace { get; set; }

		[JsonPropertyName("timestamp")]
		public string Timestamp { get; set; } = string.Empty;

		[JsonPropertyName("platform")]
		public string? Platform { get; set; }

		[JsonPropertyName("osName")]
		public string? OsName { get; set; }

		[JsonPropertyName("osVersion")]
		public string? OsVersion { get; set; }

		[JsonPropertyName("appVersion")]
		public string? AppVersion { get; set; }

		[JsonPropertyName("sdkVersion")]
		public string? SdkVersion { get; set; }

		[JsonPropertyName("sessionId")]
		public string? SessionId { get; set; }

		[JsonPropertyName("severity")]
		public string Severity { get; set; } = "error";

		[JsonPropertyName("kind")]
		public string Kind { get; set; } = "handled";

		[JsonPropertyName("isDebug")]
		public bool IsDebug { get; set; }
	}

	private class SystemProperties
	{
		[JsonPropertyName("isDebug")]
		public bool IsDebug { get; set; }

		[JsonPropertyName("osName")]
		public string OsName { get; set; } = string.Empty;

		[JsonPropertyName("osVersion")]
		public string OsVersion { get; set; } = string.Empty;

		[JsonPropertyName("deviceModel")]
		public string DeviceModel { get; set; } = string.Empty;

		[JsonPropertyName("sdkVersion")]
		public string SdkVersion { get; set; } = string.Empty;

		[JsonPropertyName("locale")]
		public string Locale { get; set; } = string.Empty;

		[JsonPropertyName("appVersion")]
		public string AppVersion { get; set; } = string.Empty;

		[JsonPropertyName("appBuildNumber")]
		public string AppBuildNumber { get; set; } = string.Empty;
	}

	public const string DefaultAppKey = "A-EU-6908443133";

	private const string PlatformName = "Windows WPF";

	private const string SdkVersionName = "Aptabase.Wpf@0.2.0";

	private static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(60.0);

	private static readonly Lazy<AptabaseService> _lazyInstance = new Lazy<AptabaseService>(() => new AptabaseService());

	private HttpClient? _httpClient;

	private string? _appKey;

	private AptabaseOptions _options = new AptabaseOptions();

	private DateTime _lastTouched = DateTime.UtcNow;

	private string _sessionId = GenerateSessionId();

	private readonly SystemProperties _systemProps;

	private readonly string _cacheFilePath;

	private readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

	private int _isFlushingQueue;

	private readonly HttpMessageHandler? _handler;

	private readonly bool _flushOnInit = true;

	private static readonly Dictionary<string, string> Hosts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		{ "US", "https://us.aptabase.com" },
		{ "EU", "https://eu.aptabase.com" },
		{ "DEV", "http://localhost:3000" }
	};

	public static AptabaseService Current => _lazyInstance.Value;

	public bool IsInitialized => _httpClient != null;

	public AptabaseService()
		: this(null, null, flushOnInit: true)
	{
	}

	internal AptabaseService(string? directory, HttpMessageHandler? handler, bool flushOnInit = false)
	{
		_handler = handler;
		_flushOnInit = flushOnInit;
		_systemProps = CollectSystemProperties();
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		string text = directory ?? Path.Combine(folderPath, "Fluorite");
		Directory.CreateDirectory(text);
		_cacheFilePath = Path.Combine(text, "aptabase_events_cache.json");
	}

	public void Init(string appKey = "A-EU-6908443133", AptabaseOptions? options = null)
	{
		_appKey = appKey;
		_options = options ?? new AptabaseOptions();
		string[] entries = appKey.Split('-');
		if (entries.Length != 3)
		{
			App.Log("[Aptabase] Warning: App Key '" + appKey + "' is malformed.");
			return;
		}
		string text = entries[1].ToUpperInvariant();
		string commandLinePath = null;
		if (text == "SH")
		{
			commandLinePath = _options.Host;
			if (string.IsNullOrEmpty(commandLinePath))
			{
				App.Log("[Aptabase] Warning: Self-hosted App Key specified but Host is missing.");
				return;
			}
		}
		else
		{
			if (!Hosts.TryGetValue(text, out string value))
			{
				App.Log("[Aptabase] Warning: Unknown region '" + text + "' in App Key.");
				return;
			}
			commandLinePath = value;
		}
		_systemProps.IsDebug = _options.IsDebugMode == true;
		_httpClient = ((_handler == null) ? new HttpClient() : new HttpClient(_handler));
		_httpClient.BaseAddress = new Uri(commandLinePath);
		_httpClient.Timeout = TimeSpan.FromSeconds(10.0);
		_httpClient.DefaultRequestHeaders.Add("App-Key", appKey);
		App.Log($"[Aptabase] Initialized for region {text} (BaseUrl: {commandLinePath}, IsDebug: {_systemProps.IsDebug})");
		if (_options.EnablePersistence && _flushOnInit)
		{
			Task.Run((Func<Task?>)FlushPersistedQueueAsync);
		}
	}

	public void TrackEvent(string eventName, Dictionary<string, object>? props = null)
	{
		if (IsInitialized)
		{
			Task.Run(() => TrackEventAsync(eventName, props));
		}
	}

	public async Task TrackEventAsync(string eventName, Dictionary<string, object>? props = null)
	{
		if (!IsInitialized || _httpClient == null)
		{
			return;
		}
		try
		{
			RefreshSession();
			EventPayload payload = new EventPayload
			{
				Timestamp = DateTime.UtcNow.ToString("o"),
				SessionId = _sessionId,
				EventName = eventName,
				SystemProps = _systemProps,
				Props = SanitizeProps(props)
			};
			using HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v0/event", payload).ConfigureAwait(continueOnCapturedContext: false);
			if (!response.IsSuccessStatusCode)
			{
				string value = await response.Content.ReadAsStringAsync();
				App.Log($"[Aptabase] TrackEvent failed with status {response.StatusCode}: {value}");
				if (_options.EnablePersistence)
				{
					await PersistEventAsync(payload);
				}
			}
		}
		catch (Exception ex)
		{
			App.Log("[Aptabase] Network exception tracking '" + eventName + "': " + ex.Message);
			if (_options.EnablePersistence)
			{
				EventPayload payload2 = new EventPayload
				{
					Timestamp = DateTime.UtcNow.ToString("o"),
					SessionId = _sessionId,
					EventName = eventName,
					SystemProps = _systemProps,
					Props = SanitizeProps(props)
				};
				await PersistEventAsync(payload2);
			}
		}
	}

	public void TrackError(Exception exception, bool fatal = false, string kind = "handled")
	{
		if (!IsInitialized || !_options.EnableCrashReporting)
		{
			return;
		}
		if (fatal)
		{
			try
			{
				Task.Run(() => TrackErrorAsync(exception, fatal, kind)).Wait(TimeSpan.FromMilliseconds(1500.0));
				return;
			}
			catch
			{
				return;
			}
		}
		Task.Run(() => TrackErrorAsync(exception, fatal, kind));
	}

	public async Task TrackErrorAsync(Exception exception, bool fatal = false, string kind = "handled")
	{
		if (!IsInitialized || _httpClient == null || !_options.EnableCrashReporting)
		{
			return;
		}
		try
		{
			RefreshSession();
			ErrorPayload errorPayload = new ErrorPayload
			{
				ErrorMessage = (Truncate(exception.Message, 5000) ?? "Unknown Exception"),
				ErrorType = (Truncate(exception.GetType().Name, 100) ?? "Exception"),
				StackTrace = Truncate(exception.StackTrace, 10000),
				Timestamp = DateTime.UtcNow.ToString("o"),
				Platform = Truncate("Windows WPF", 30),
				OsName = Truncate(_systemProps.OsName, 30),
				OsVersion = Truncate(_systemProps.OsVersion, 100),
				AppVersion = Truncate(_systemProps.AppVersion, 50),
				SdkVersion = Truncate(_systemProps.SdkVersion, 40),
				SessionId = Truncate(_sessionId, 100),
				Severity = (fatal ? "fatal" : "error"),
				Kind = kind,
				IsDebug = _systemProps.IsDebug
			};
			using CancellationTokenSource timeout = new CancellationTokenSource(fatal ? TimeSpan.FromMilliseconds(1500.0) : TimeSpan.FromSeconds(10.0));
			using HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v0/error", errorPayload, timeout.Token).ConfigureAwait(continueOnCapturedContext: false);
			if (response.IsSuccessStatusCode)
			{
				App.Log($"[Aptabase] Error tracked ({errorPayload.ErrorType}, fatal={fatal}, kind={kind})");
			}
			else
			{
				string value = await response.Content.ReadAsStringAsync();
				App.Log($"[Aptabase] TrackError failed with status {response.StatusCode}: {value}");
			}
		}
		catch (Exception ex)
		{
			App.Log("[Aptabase] Network exception sending error report: " + ex.Message);
		}
	}

	private void RefreshSession()
	{
		DateTime utcNow = DateTime.UtcNow;
		if (utcNow - _lastTouched >= SessionTimeout)
		{
			_sessionId = GenerateSessionId();
		}
		_lastTouched = utcNow;
	}

	private static string GenerateSessionId()
	{
		long pageNumber = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		long settingIndex = RandomNumberGenerator.GetInt32(0, 99999999);
		return (pageNumber * 100000000 + settingIndex).ToString();
	}

	private static string? Truncate(string? value, int maxLen)
	{
		if (string.IsNullOrEmpty(value))
		{
			return value;
		}
		if (value.Length <= maxLen)
		{
			return value;
		}
		return value.Substring(0, maxLen);
	}

	private static Dictionary<string, object>? SanitizeProps(Dictionary<string, object>? props)
	{
		if (props == null || props.Count == 0)
		{
			return null;
		}
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		foreach (KeyValuePair<string, object> prop in props)
		{
			object value = prop.Value;
			if ((value is string || value is int || value is long || value is double || value is float || value is decimal || value is bool) ? true : false)
			{
				dictionary[prop.Key] = prop.Value;
			}
			else if (prop.Value != null)
			{
				dictionary[prop.Key] = prop.Value.ToString() ?? string.Empty;
			}
		}
		return dictionary;
	}

	private static SystemProperties CollectSystemProperties()
	{
		string text = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version?.ToString(3) ?? "1.0.0";
		return new SystemProperties
		{
			IsDebug = false,
			OsName = "Windows",
			OsVersion = Environment.OSVersion.Version.ToString(),
			DeviceModel = Environment.MachineName,
			SdkVersion = "Aptabase.Wpf@0.2.0",
			Locale = CultureInfo.CurrentCulture.Name,
			AppVersion = text,
			AppBuildNumber = text
		};
	}

	private async Task<List<CachedEvent>> ReadQueueAsync()
	{
		if (!File.Exists(_cacheFilePath))
		{
			return new List<CachedEvent>();
		}
		string json = await File.ReadAllTextAsync(_cacheFilePath).ConfigureAwait(continueOnCapturedContext: false);
		using JsonDocument jsonDocument = JsonDocument.Parse(json);
		JsonElement value = jsonDocument.RootElement;
		if (value.GetArrayLength() == 0)
		{
			return new List<CachedEvent>();
		}
		if (jsonDocument.RootElement[0].TryGetProperty("Event", out value))
		{
			return JsonSerializer.Deserialize<List<CachedEvent>>(json) ?? new List<CachedEvent>();
		}
		return (JsonSerializer.Deserialize<List<EventPayload>>(json) ?? new List<EventPayload>()).ConvertAll((EventPayload payload) => new CachedEvent
		{
			Event = payload
		});
	}

	private async Task WriteQueueAsync(List<CachedEvent> queue)
	{
		string temp = _cacheFilePath + ".tmp";
		try
		{
			await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(queue)).ConfigureAwait(continueOnCapturedContext: false);
			File.Move(temp, _cacheFilePath, overwrite: true);
		}
		finally
		{
			if (File.Exists(temp))
			{
				File.Delete(temp);
			}
		}
	}

	private async Task PersistEventAsync(EventPayload payload)
	{
		await _fileLock.WaitAsync().ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			List<CachedEvent> list = await ReadQueueAsync().ConfigureAwait(continueOnCapturedContext: false);
			if (list.Count >= 100)
			{
				list.RemoveAt(0);
			}
			list.Add(new CachedEvent
			{
				Event = payload
			});
			await WriteQueueAsync(list).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex)
		{
			App.Log("[Aptabase] Could not persist event: " + ex.Message);
		}
		finally
		{
			_fileLock.Release();
		}
	}

	internal async Task FlushPersistedQueueAsync()
	{
		if (Interlocked.CompareExchange(ref _isFlushingQueue, 1, 0) != 0)
		{
			return;
		}
		try
		{
			if (_httpClient == null)
			{
				return;
			}
			await _fileLock.WaitAsync().ConfigureAwait(continueOnCapturedContext: false);
			List<CachedEvent> snapshot;
			try
			{
				snapshot = await ReadQueueAsync().ConfigureAwait(continueOnCapturedContext: false);
				if (snapshot.Count == 0)
				{
					return;
				}
				await WriteQueueAsync(snapshot).ConfigureAwait(continueOnCapturedContext: false);
			}
			finally
			{
				_fileLock.Release();
			}
			HashSet<string> sent = new HashSet<string>(StringComparer.Ordinal);
			foreach (CachedEvent entry in snapshot)
			{
				try
				{
					using HttpResponseMessage httpResponseMessage = await _httpClient.PostAsJsonAsync("/api/v0/event", entry.Event).ConfigureAwait(continueOnCapturedContext: false);
					if (httpResponseMessage.IsSuccessStatusCode)
					{
						sent.Add(entry.Id);
					}
				}
				catch (Exception ex)
				{
					App.Log("[Aptabase] Queue send interrupted: " + ex.Message);
					break;
				}
			}
			await _fileLock.WaitAsync().ConfigureAwait(continueOnCapturedContext: false);
			try
			{
				List<CachedEvent> list = await ReadQueueAsync().ConfigureAwait(continueOnCapturedContext: false);
				list.RemoveAll((CachedEvent cachedEvent) => sent.Contains(cachedEvent.Id));
				await WriteQueueAsync(list).ConfigureAwait(continueOnCapturedContext: false);
			}
			finally
			{
				_fileLock.Release();
			}
		}
		catch (Exception ex2)
		{
			App.Log("[Aptabase] Could not flush queue: " + ex2.Message);
		}
		finally
		{
			Interlocked.Exchange(ref _isFlushingQueue, 0);
		}
	}
}
