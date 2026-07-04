using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO;

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

    public async Task<string> WriteBytesAsync(string path, byte[] bytes, bool unique = false, CancellationToken cancellationToken = default)
    {
        EnsureParentDirectory(path);
        var targetPath = unique ? GetUniquePath(path) : path;
        await File.WriteAllBytesAsync(targetPath, bytes, cancellationToken).ConfigureAwait(false);
        return targetPath;
    }

    public static string GetUniquePath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        for (var counter = 2; ; counter++)
        {
            var candidate = Path.Combine(directory, $"{name} ({counter}){extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    public IReadOnlyList<DirectoryEntryInfo> ListDirectory(string path)
    {
        var resolvedPath = path;
        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            var resolved = ShortcutResolver.ResolveDirectory(path);
            if (!string.IsNullOrEmpty(resolved) && Directory.Exists(resolved))
            {
                resolvedPath = resolved;
            }
        }
        else if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
        {
            var resolved = ShortcutResolver.ResolveUrlShortcutDirectory(path);
            if (!string.IsNullOrEmpty(resolved))
            {
                resolvedPath = resolved;
            }
        }

        return Directory.EnumerateFileSystemEntries(resolvedPath)
            .Select(entryPath => CreateEntryInfo(entryPath))
            .OrderByDescending(entry => entry.IsDirectory)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<DirectoryEntryInfo> ListDesktopItems(params string[] desktopPaths)
    {
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<DirectoryEntryInfo>();

        foreach (var desktopPath in desktopPaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var fullDesktopPath = Path.GetFullPath(desktopPath);
            if (!Directory.Exists(fullDesktopPath) || !seenPaths.Add(fullDesktopPath))
            {
                continue;
            }

            entries.AddRange(Directory.EnumerateFileSystemEntries(fullDesktopPath)
                .Where(path => !IsShellMetadataFile(path))
                .Select(path => CreateEntryInfo(path, fullDesktopPath)));
        }

        return entries
            .OrderByDescending(entry => entry.IsDirectory)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.SourceDirectory, StringComparer.OrdinalIgnoreCase)
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

    private static DirectoryEntryInfo CreateEntryInfo(string path, string? sourceDirectory = null)
    {
        var isDirectory = Directory.Exists(path);
        var resolvedPath = path;
        var isDirectoryShortcut = false;
        
        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            var resolved = ShortcutResolver.ResolveDirectory(path);
            if (!string.IsNullOrEmpty(resolved) && Directory.Exists(resolved))
            {
                isDirectory = true;
                resolvedPath = resolved;
                isDirectoryShortcut = true;
            }
        }
        else if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
        {
            var resolved = ShortcutResolver.ResolveUrlShortcutDirectory(path);
            if (!string.IsNullOrEmpty(resolved))
            {
                isDirectory = true;
                resolvedPath = resolved;
                isDirectoryShortcut = true;
            }
        }

        if (isDirectory)
        {
            var directory = new DirectoryInfo(resolvedPath);
            return new DirectoryEntryInfo(
                isDirectoryShortcut ? Path.GetFileNameWithoutExtension(path) : new DirectoryInfo(path).Name,
                Path.GetFullPath(path),
                true,
                null,
                File.Exists(path) ? new FileInfo(path).LastWriteTime : directory.LastWriteTime,
                sourceDirectory);
        }

        var file = new FileInfo(path);
        return new DirectoryEntryInfo(
            file.Name,
            file.FullName,
            false,
            file.Length,
            file.LastWriteTime,
            sourceDirectory);
    }

    private static bool IsShellMetadataFile(string path) =>
        string.Equals(Path.GetFileName(path), "desktop.ini", StringComparison.OrdinalIgnoreCase);

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
