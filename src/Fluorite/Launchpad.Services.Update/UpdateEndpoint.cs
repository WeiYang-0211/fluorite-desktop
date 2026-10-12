using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Launchpad.Services.Update;

/// <summary>
/// Where release metadata is fetched from.
///
/// Point this at your own host before shipping a fork. Left empty, the
/// updater disables itself and the rest of the application is unaffected.
/// </summary>
public static class UpdateEndpoint
{
    public const string BaseUrl = "";

    public static bool Enabled => !string.IsNullOrWhiteSpace(BaseUrl);

    public static string LatestUrl => BaseUrl.TrimEnd('/') + "/v1/apps/fluorite/latest";

    public static bool IsLoopback
    {
        get
        {
            try
            {
                return new Uri(BaseUrl).IsLoopback;
            }
            catch
            {
                return false;
            }
        }
    }
}

/// <summary>Raised when the update feed cannot be reached at all.</summary>
public sealed class UpdateTransportException : Exception
{
    public UpdateTransportException(string message)
        : base(message)
    {
    }

    public UpdateTransportException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>Shape of the <c>GET /v1/apps/{app}/latest</c> response.</summary>
public sealed class LatestResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    public static readonly JsonSerializerOptions Json = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public static async System.Threading.Tasks.Task<LatestResponse> FetchAsync(string os, CancellationToken ct = default)
    {
        using var http = new HttpClient(new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(10.0),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10.0),
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(12.0)
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Fluorite/" + FluoriteVersion.Text + " (Windows NT; updater)");
        http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        string url = UpdateEndpoint.LatestUrl + "?os=" + Uri.EscapeDataString(os);
        try
        {
            using var response = await http.GetAsync(url, ct).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(body))
            {
                var parsed = JsonSerializer.Deserialize<LatestResponse>(body, Json);
                if (parsed != null)
                {
                    return parsed;
                }
            }
            throw new UpdateTransportException($"更新服务器返回了无法识别的内容（HTTP {(int)response.StatusCode}）");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (UpdateTransportException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new UpdateTransportException("无法连接更新服务器", ex);
        }
    }
}