using System.Text.Json;

namespace DesktopHtml.Core.Backups;

public static class BackupKinds
{
    public const string Config = "config";
    public const string Skin = "skin";
    public const string Storage = "storage";
}

public sealed record BackupManifest(
    string Id,
    DateTimeOffset CreatedUtc,
    string Kind,
    string Reason,
    string SourcePath,
    string RelativeTargetPath,
    string DesktopHtmlVersion,
    IReadOnlyList<string> Files);

public sealed record BackupRestoreResult(
    BackupManifest Restored,
    BackupManifest? SafetyBackup);

public sealed record BackupPruneResult(
    int Keep,
    IReadOnlyList<BackupManifest> Deleted);

public sealed class BackupService
{
    private const string ManifestFileName = "manifest.json";
    private const string PayloadDirectoryName = "payload";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly AppPaths _paths;
    private readonly string _desktopHtmlVersion;

    public BackupService(AppPaths paths, string desktopHtmlVersion = "unknown")
    {
        _paths = paths;
        _desktopHtmlVersion = string.IsNullOrWhiteSpace(desktopHtmlVersion) ? "unknown" : desktopHtmlVersion;
    }

    public Task<BackupManifest> CreateConfigBackupAsync(
        string reason,
        CancellationToken cancellationToken = default) =>
        CreateBackupFromTargetAsync(
            BackupKinds.Config,
            reason,
            _paths.ConfigFile,
            "config.json",
            cancellationToken);

    public Task<BackupManifest> CreateSkinBackupAsync(
        string skinId,
        string reason,
        CancellationToken cancellationToken = default) =>
        CreateBackupFromTargetAsync(
            BackupKinds.Skin,
            reason,
            Path.Combine(_paths.SkinsDirectory, skinId),
            Path.Combine("skins", skinId),
            cancellationToken);

    public Task<BackupManifest> CreateStorageBackupAsync(
        string skinId,
        string reason,
        CancellationToken cancellationToken = default) =>
        CreateBackupFromTargetAsync(
            BackupKinds.Storage,
            reason,
            Path.Combine(_paths.StorageDirectory, skinId, "storage.json"),
            Path.Combine("storage", skinId, "storage.json"),
            cancellationToken);

    public Task<BackupManifest> CreateBackupAsync(
        string kind,
        string reason,
        string? skinId = null,
        CancellationToken cancellationToken = default)
    {
        return NormalizeKind(kind) switch
        {
            BackupKinds.Config => CreateConfigBackupAsync(reason, cancellationToken),
            BackupKinds.Skin => CreateSkinBackupAsync(RequireSkinId(skinId, BackupKinds.Skin), reason, cancellationToken),
            BackupKinds.Storage => CreateStorageBackupAsync(RequireSkinId(skinId, BackupKinds.Storage), reason, cancellationToken),
            _ => throw new InvalidOperationException("Backup kind must be one of: config, skin, storage.")
        };
    }

    public async Task<IReadOnlyList<BackupManifest>> ListAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        if (!Directory.Exists(_paths.BackupsDirectory))
        {
            return [];
        }

        var backups = new List<BackupManifest>();
        foreach (var directory in Directory.EnumerateDirectories(_paths.BackupsDirectory))
        {
            var manifestFile = Path.Combine(directory, ManifestFileName);
            if (!File.Exists(manifestFile))
            {
                continue;
            }

            await using var stream = File.OpenRead(manifestFile);
            var manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(
                    stream,
                    JsonOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            if (manifest is not null)
            {
                backups.Add(manifest);
            }
        }

        return backups
            .OrderByDescending(backup => backup.CreatedUtc)
            .ThenByDescending(backup => backup.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<BackupRestoreResult> RestoreAsync(
        string backupId,
        CancellationToken cancellationToken = default)
    {
        var (manifest, backupDirectory) = await FindBackupAsync(backupId, cancellationToken)
            .ConfigureAwait(false);
        var relativeTargetPath = ValidateRelativeTargetPath(manifest.RelativeTargetPath);
        var targetPath = ResolveRootPath(relativeTargetPath);
        var payloadDirectory = ResolveBackupChildPath(backupDirectory, PayloadDirectoryName);

        if (!Directory.Exists(payloadDirectory))
        {
            throw new InvalidOperationException($"Backup '{backupId}' is missing its payload directory.");
        }

        var safetyBackup = await CreateSafetyBackupAsync(manifest.Kind, targetPath, relativeTargetPath, manifest.Id, cancellationToken)
            .ConfigureAwait(false);

        switch (NormalizeKind(manifest.Kind))
        {
            case BackupKinds.Config:
            case BackupKinds.Storage:
                RestoreFile(payloadDirectory, targetPath);
                break;
            case BackupKinds.Skin:
                RestoreDirectory(payloadDirectory, targetPath);
                break;
            default:
                throw new InvalidOperationException($"Unsupported backup kind '{manifest.Kind}'.");
        }

        return new BackupRestoreResult(manifest, safetyBackup);
    }

    public async Task<BackupPruneResult> PruneAsync(int keep, CancellationToken cancellationToken = default)
    {
        if (keep < 0)
        {
            throw new InvalidOperationException("Backup prune keep count must be zero or greater.");
        }

        var backups = await ListAsync(cancellationToken).ConfigureAwait(false);
        var deleted = new List<BackupManifest>();
        foreach (var backup in backups.Skip(keep))
        {
            var directory = ResolveBackupChildPath(_paths.BackupsDirectory, backup.Id);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
                deleted.Add(backup);
            }
        }

        return new BackupPruneResult(keep, deleted);
    }

    private async Task<BackupManifest> CreateBackupFromTargetAsync(
        string kind,
        string reason,
        string sourcePath,
        string relativeTargetPath,
        CancellationToken cancellationToken)
    {
        _paths.EnsureCreated();

        var normalizedKind = NormalizeKind(kind);
        relativeTargetPath = ValidateRelativeTargetPath(relativeTargetPath);
        var fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath) && !Directory.Exists(fullSourcePath))
        {
            throw new FileNotFoundException($"Cannot create {normalizedKind} backup because '{fullSourcePath}' does not exist.", fullSourcePath);
        }

        var id = CreateBackupId(normalizedKind, reason);
        var backupDirectory = ResolveBackupChildPath(_paths.BackupsDirectory, id);
        var payloadDirectory = Path.Combine(backupDirectory, PayloadDirectoryName);
        Directory.CreateDirectory(payloadDirectory);

        IReadOnlyList<string> files;
        if (File.Exists(fullSourcePath))
        {
            var targetFile = Path.Combine(payloadDirectory, Path.GetFileName(fullSourcePath));
            File.Copy(fullSourcePath, targetFile, overwrite: false);
            files = [Path.GetFileName(fullSourcePath)];
        }
        else
        {
            CopyDirectory(fullSourcePath, payloadDirectory);
            files = Directory
                .EnumerateFiles(payloadDirectory, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(payloadDirectory, file))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        var manifest = new BackupManifest(
            id,
            DateTimeOffset.UtcNow,
            normalizedKind,
            NormalizeReason(reason),
            fullSourcePath,
            relativeTargetPath.Replace(Path.DirectorySeparatorChar, '/'),
            _desktopHtmlVersion,
            files);

        await File.WriteAllTextAsync(
                Path.Combine(backupDirectory, ManifestFileName),
                JsonSerializer.Serialize(manifest, JsonOptions),
                cancellationToken)
            .ConfigureAwait(false);

        return manifest;
    }

    private async Task<BackupManifest?> CreateSafetyBackupAsync(
        string kind,
        string targetPath,
        string relativeTargetPath,
        string restoredBackupId,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(targetPath) && !Directory.Exists(targetPath))
        {
            return null;
        }

        return await CreateBackupFromTargetAsync(
                kind,
                $"pre-restore-{restoredBackupId}",
                targetPath,
                relativeTargetPath,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<(BackupManifest Manifest, string BackupDirectory)> FindBackupAsync(
        string backupId,
        CancellationToken cancellationToken)
    {
        _paths.EnsureCreated();

        if (string.IsNullOrWhiteSpace(backupId)
            || backupId.Contains(Path.DirectorySeparatorChar)
            || backupId.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidOperationException("Backup id is invalid.");
        }

        foreach (var directory in Directory.EnumerateDirectories(_paths.BackupsDirectory))
        {
            var manifestFile = Path.Combine(directory, ManifestFileName);
            if (!File.Exists(manifestFile))
            {
                continue;
            }

            await using var stream = File.OpenRead(manifestFile);
            var manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(
                    stream,
                    JsonOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            if (manifest is not null && string.Equals(manifest.Id, backupId, StringComparison.Ordinal))
            {
                return (manifest, directory);
            }
        }

        throw new InvalidOperationException($"Backup '{backupId}' was not found.");
    }

    private string ResolveRootPath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_paths.Root, relativePath));
        var root = Path.GetFullPath(_paths.Root);
        if (!fullPath.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Backup target path escapes the desktop-html AppData directory.");
        }

        return fullPath;
    }

    private static string ResolveBackupChildPath(string parentDirectory, string childName)
    {
        var fullPath = Path.GetFullPath(Path.Combine(parentDirectory, childName));
        var parent = Path.GetFullPath(parentDirectory);
        if (!fullPath.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fullPath, parent, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Backup path escapes the backup directory.");
        }

        return fullPath;
    }

    private static string ValidateRelativeTargetPath(string relativeTargetPath)
    {
        if (string.IsNullOrWhiteSpace(relativeTargetPath)
            || Path.IsPathRooted(relativeTargetPath))
        {
            throw new InvalidOperationException("Backup target path must be relative.");
        }

        var normalized = relativeTargetPath.Replace('/', Path.DirectorySeparatorChar);
        var parts = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(part => part == ".." || part == "."))
        {
            throw new InvalidOperationException("Backup target path contains unsafe path segments.");
        }

        return Path.Combine(parts);
    }

    private static void RestoreFile(string payloadDirectory, string targetPath)
    {
        var payloadFiles = Directory.GetFiles(payloadDirectory);
        if (payloadFiles.Length != 1)
        {
            throw new InvalidOperationException("File backup payload must contain exactly one file.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var tempFile = $"{targetPath}.{Guid.NewGuid():N}.restore.tmp";
        File.Copy(payloadFiles[0], tempFile, overwrite: false);
        File.Move(tempFile, targetPath, overwrite: true);
    }

    private static void RestoreDirectory(string payloadDirectory, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var tempDirectory = $"{targetPath}.{Guid.NewGuid():N}.restore.tmp";
        CopyDirectory(payloadDirectory, tempDirectory);
        if (Directory.Exists(targetPath))
        {
            Directory.Delete(targetPath, recursive: true);
        }

        Directory.Move(tempDirectory, targetPath);
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            File.Copy(file, Path.Combine(targetDirectory, Path.GetFileName(file)), overwrite: false);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            CopyDirectory(directory, Path.Combine(targetDirectory, Path.GetFileName(directory)));
        }
    }

    private static string CreateBackupId(string kind, string reason)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var reasonSlug = Slugify(NormalizeReason(reason));
        var shortId = Guid.NewGuid().ToString("N")[..8];
        return $"{timestamp}_{kind}_{reasonSlug}_{shortId}";
    }

    private static string Slugify(string value)
    {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        slug = slug.Trim('-');
        if (slug.Length == 0)
        {
            slug = "manual";
        }

        return slug.Length > 40 ? slug[..40].Trim('-') : slug;
    }

    private static string NormalizeReason(string reason) =>
        string.IsNullOrWhiteSpace(reason) ? "manual" : reason.Trim();

    private static string NormalizeKind(string kind) =>
        kind.Trim().ToLowerInvariant();

    private static string RequireSkinId(string? skinId, string kind)
    {
        if (string.IsNullOrWhiteSpace(skinId))
        {
            throw new InvalidOperationException($"Backup kind '{kind}' requires --skin-id.");
        }

        if (skinId.Contains(Path.DirectorySeparatorChar)
            || skinId.Contains(Path.AltDirectorySeparatorChar)
            || skinId.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Skin id is not safe for backup paths.");
        }

        return skinId;
    }
}
