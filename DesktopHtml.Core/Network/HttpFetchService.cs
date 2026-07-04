using System.Text;

namespace DesktopHtml.Core.Network;

public sealed class HttpFetchOptions
{
    public string Url { get; set; } = "";
    public string Method { get; set; } = "GET";
    public Dictionary<string, string>? Headers { get; set; }
    public string? Body { get; set; }

    /// <summary>Request timeout in milliseconds. Clamped to [1000, 120000].</summary>
    public int TimeoutMs { get; set; } = 30_000;

    /// <summary>Response size cap in bytes. Clamped to [1024, 64 MB].</summary>
    public long MaxResponseBytes { get; set; } = 8 * 1024 * 1024;

    /// <summary>Return the body base64-encoded (for binary responses).</summary>
    public bool AsBase64 { get; set; }
}

public sealed record HttpFetchResult(
    int Status,
    string? StatusText,
    IReadOnlyDictionary<string, string> Headers,
    string? ContentType,
    string? Body,
    string? BodyBase64,
    bool Truncated);

/// <summary>
/// Host-side HTTP for skins: one shared client, per-request timeout, response
/// size cap, and optional base64 bodies so skins can fetch binary content.
/// </summary>
public sealed class HttpFetchService
{
    private static readonly HttpClient Client = new()
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    public async Task<HttpFetchResult> FetchAsync(HttpFetchOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Url))
        {
            throw new InvalidOperationException("httpFetch requires a url value.");
        }

        var timeoutMs = Math.Clamp(options.TimeoutMs, 1_000, 120_000);
        var maxBytes = Math.Clamp(options.MaxResponseBytes, 1_024, 64L * 1024 * 1024);

        using var message = new HttpRequestMessage(new HttpMethod(options.Method), options.Url);
        if (options.Body != null)
        {
            message.Content = new StringContent(options.Body, Encoding.UTF8);
        }

        if (options.Headers != null)
        {
            foreach (var (key, value) in options.Headers)
            {
                if (!message.Headers.TryAddWithoutValidation(key, value) && message.Content != null)
                {
                    message.Content.Headers.TryAddWithoutValidation(key, value);
                }
            }
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeoutMs);

        try
        {
            using var response = await Client
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                .ConfigureAwait(false);

            var (bytes, truncated) = await ReadCappedAsync(response, maxBytes, timeoutCts.Token).ConfigureAwait(false);

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in response.Headers)
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }

            foreach (var header in response.Content.Headers)
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }

            return new HttpFetchResult(
                (int)response.StatusCode,
                response.ReasonPhrase,
                headers,
                response.Content.Headers.ContentType?.ToString(),
                options.AsBase64 ? null : Encoding.UTF8.GetString(bytes),
                options.AsBase64 ? Convert.ToBase64String(bytes) : null,
                truncated);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"httpFetch timed out after {timeoutMs} ms: {options.Url}");
        }
    }

    private static async Task<(byte[] Bytes, bool Truncated)> ReadCappedAsync(
        HttpResponseMessage response,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var memory = new MemoryStream();
        var buffer = new byte[81920];

        while (memory.Length < maxBytes)
        {
            var toRead = (int)Math.Min(buffer.Length, maxBytes - memory.Length);
            var read = await stream.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                return (memory.ToArray(), false);
            }

            memory.Write(buffer, 0, read);
        }

        // Cap reached — probe one byte to tell "exactly at cap" from "truncated".
        var probe = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
        return (memory.ToArray(), probe > 0);
    }
}
