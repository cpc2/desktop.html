using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopHtml.Core.Backups;

namespace DesktopHtml.Core.Storage;

public sealed class SkinStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly AppPaths _paths;
    private readonly BackupService? _backupService;

    public SkinStorageService(AppPaths paths, BackupService? backupService = null)
    {
        _paths = paths;
        _backupService = backupService;
    }

    public async Task<JsonNode?> GetAsync(string skinId, string key, CancellationToken cancellationToken = default)
    {
        var storage = await ReadStorageAsync(skinId, cancellationToken).ConfigureAwait(false);
        return storage.TryGetPropertyValue(key, out var value) ? value?.DeepClone() : null;
    }

    public async Task SetAsync(string skinId, string key, JsonNode? value, CancellationToken cancellationToken = default)
    {
        var storage = await ReadStorageAsync(skinId, cancellationToken).ConfigureAwait(false);
        storage[key] = value?.DeepClone();
        await WriteStorageAsync(skinId, storage, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(string skinId, string key, CancellationToken cancellationToken = default)
    {
        var storage = await ReadStorageAsync(skinId, cancellationToken).ConfigureAwait(false);
        storage.Remove(key);
        await WriteStorageAsync(skinId, storage, cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearAsync(string skinId, CancellationToken cancellationToken = default)
    {
        if (_backupService is not null && File.Exists(GetStorageFile(skinId)))
        {
            await _backupService.CreateStorageBackupAsync(skinId, "storage-clear", cancellationToken)
                .ConfigureAwait(false);
        }

        await WriteStorageAsync(skinId, new JsonObject(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<JsonObject> GetAllAsync(string skinId, CancellationToken cancellationToken = default) =>
        (await ReadStorageAsync(skinId, cancellationToken).ConfigureAwait(false)).DeepClone().AsObject();

    private async Task<JsonObject> ReadStorageAsync(string skinId, CancellationToken cancellationToken)
    {
        var storageFile = GetStorageFile(skinId);
        if (!File.Exists(storageFile))
        {
            return new JsonObject();
        }

        await using var stream = File.OpenRead(storageFile);
        var node = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return node as JsonObject ?? new JsonObject();
    }

    private async Task WriteStorageAsync(string skinId, JsonObject storage, CancellationToken cancellationToken)
    {
        var storageFile = GetStorageFile(skinId);
        Directory.CreateDirectory(Path.GetDirectoryName(storageFile)!);
        var tempFile = $"{storageFile}.{Guid.NewGuid():N}.tmp";

        await File.WriteAllTextAsync(tempFile, storage.ToJsonString(JsonOptions), cancellationToken)
            .ConfigureAwait(false);

        File.Move(tempFile, storageFile, overwrite: true);
    }

    private string GetStorageFile(string skinId) =>
        Path.Combine(_paths.StorageDirectory, skinId, "storage.json");
}
