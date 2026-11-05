using Velopack.Sources;

namespace S4.Installer.Probe;

/// <summary>
/// Check (h), guardrail 2: records every URL Velopack requests and every header it adds, plus the
/// User-Agent its HttpClient sends, so the spike can show the request carries nothing about the user.
/// </summary>
public sealed class LoggingDownloader : IFileDownloader
{
    private readonly HttpClientFileDownloader _inner = new();

    public List<object> Requests { get; } = [];

    private void Record(string url, IDictionary<string, string>? headers) =>
        Requests.Add(new
        {
            url,
            userAgent = HttpClientFileDownloader.UserAgent?.ToString(),
            headers = headers?.ToDictionary(h => h.Key, h => h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ? "<present>" : h.Value),
        });

    public Task DownloadFile(string url, string targetFile, Action<int> progress, IDictionary<string, string>? headers = null, double timeout = 30, CancellationToken cancelToken = default)
    {
        Record(url, headers);
        return _inner.DownloadFile(url, targetFile, progress, headers, timeout, cancelToken);
    }

    public Task<byte[]> DownloadBytes(string url, IDictionary<string, string>? headers = null, double timeout = 30)
    {
        Record(url, headers);
        return _inner.DownloadBytes(url, headers, timeout);
    }

    public Task<string> DownloadString(string url, IDictionary<string, string>? headers = null, double timeout = 30)
    {
        Record(url, headers);
        return _inner.DownloadString(url, headers, timeout);
    }
}
