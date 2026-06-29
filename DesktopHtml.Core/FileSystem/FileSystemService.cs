using System.Text.Json;
using System.Text.Json.Nodes;

namespace DesktopHtml.Core.FileSystem;

public sealed class FileSystemService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    public Task<string> ReadTextAsync(string path, CancellationToken cancellationToken = default) =>
        File.ReadAllTextAsync(path, cancellationToken);

    public async Task WriteTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        EnsureParentDirectory(path);
        await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
    }

    public async Task<JsonNode?> ReadJsonAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        return await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteJsonAsync(string path, JsonNode? value, CancellationToken cancellationToken = default)
    {
        EnsureParentDirectory(path);
        await File.WriteAllTextAsync(path, value?.ToJsonString(JsonOptions) ?? "null", cancellationToken)
            .ConfigureAwait(false);
    }

    public IReadOnlyList<DirectoryEntryInfo> ListDirectory(string path)
    {
        return Directory.EnumerateFileSystemEntries(path)
            .Select(CreateEntryInfo)
            .OrderByDescending(entry => entry.IsDirectory)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void DeletePath(string path, DeletePathOptions? options = null)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: options?.Recursive ?? false);
            return;
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void MovePath(string source, string destination)
    {
        EnsureParentDirectory(destination);

        if (Directory.Exists(source))
        {
            Directory.Move(source, destination);
            return;
        }

        File.Move(source, destination, overwrite: true);
    }

    public void CopyPath(string source, string destination)
    {
        EnsureParentDirectory(destination);

        if (Directory.Exists(source))
        {
            CopyDirectory(source, destination);
            return;
        }

        File.Copy(source, destination, overwrite: true);
    }

    private static DirectoryEntryInfo CreateEntryInfo(string path)
    {
        if (Directory.Exists(path))
        {
            var directory = new DirectoryInfo(path);
            return new DirectoryEntryInfo(
                directory.Name,
                directory.FullName,
                true,
                null,
                directory.LastWriteTime);
        }

        var file = new FileInfo(path);
        return new DirectoryEntryInfo(
            file.Name,
            file.FullName,
            false,
            file.Length,
            file.LastWriteTime);
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            File.Copy(file, Path.Combine(targetDirectory, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            CopyDirectory(directory, Path.Combine(targetDirectory, Path.GetFileName(directory)));
        }
    }

    private static void EnsureParentDirectory(string path)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }
    }
}
