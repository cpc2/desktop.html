using System.Text.Json;
using System.Text;

namespace DesktopHtml.Core.Logging;

public sealed class LogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppPaths _paths;

    public LogService(AppPaths paths)
    {
        _paths = paths;
    }

    public Task InfoAsync(string category, string message, object? data = null, CancellationToken cancellationToken = default) =>
        WriteAsync("info", category, message, data, cancellationToken);

    public Task WarningAsync(string category, string message, object? data = null, CancellationToken cancellationToken = default) =>
        WriteAsync("warning", category, message, data, cancellationToken);

    public Task ErrorAsync(string category, string message, object? data = null, CancellationToken cancellationToken = default) =>
        WriteAsync("error", category, message, data, cancellationToken);

    public async Task WriteAsync(
        string level,
        string category,
        string message,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_paths.LogsDirectory);
        var entry = new LogEntry(DateTimeOffset.Now, level, category, message, data);
        var line = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;
        var bytes = Encoding.UTF8.GetBytes(line);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                await using var stream = new FileStream(
                    GetCurrentLogFile(),
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite,
                    bufferSize: 4096,
                    useAsync: true);
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * (attempt + 1)), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    public IReadOnlyList<string> ReadLines(int maxLines = 200)
    {
        var logFile = GetCurrentLogFile();
        if (!File.Exists(logFile))
        {
            return Array.Empty<string>();
        }

        using var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines.TakeLast(Math.Clamp(maxLines, 1, 5000)).ToArray();
    }

    public string GetCurrentLogFile() => Path.Combine(_paths.LogsDirectory, "desktop-html.log");
}

public sealed record LogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Category,
    string Message,
    object? Data);
