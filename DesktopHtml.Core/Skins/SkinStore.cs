using System.IO.Compression;
using DesktopHtml.Core.Backups;
using DesktopHtml.Core.Configuration;
using DesktopHtml.Core.FileSystem;

namespace DesktopHtml.Core.Skins;

public sealed class SkinStore
{
    private readonly AppPaths _paths;
    private readonly SkinValidator _validator;
    private readonly BackupService? _backupService;

    public SkinStore(AppPaths paths, SkinValidator? validator = null, BackupService? backupService = null)
    {
        _paths = paths;
        _validator = validator ?? new SkinValidator();
        _backupService = backupService;
    }

    public string GetSkinDirectory(string skinId) => Path.Combine(_paths.SkinsDirectory, skinId);

    /// <summary>Finds the skin folder inside an extracted package: either the
    /// root itself or a single wrapping directory containing manifest.json.</summary>
    private static string LocateSkinRoot(string extractRoot)
    {
        if (File.Exists(Path.Combine(extractRoot, "manifest.json")))
        {
            return extractRoot;
        }

        var directories = Directory.GetDirectories(extractRoot);
        if (directories.Length == 1 && File.Exists(Path.Combine(directories[0], "manifest.json")))
        {
            return directories[0];
        }

        throw new InvalidOperationException("Skin package does not contain a manifest.json at its root.");
    }

    public async Task<SkinManifest> InstallAsync(
        string sourceDirectory,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();

        // A .zip package installs by extracting to a temp folder first.
        if (File.Exists(sourceDirectory) &&
            sourceDirectory.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var extractRoot = Path.Combine(Path.GetTempPath(), "desktop-html-install-" + Guid.NewGuid().ToString("n"));
            try
            {
                ZipFile.ExtractToDirectory(sourceDirectory, extractRoot);
                return await InstallAsync(LocateSkinRoot(extractRoot), overwrite, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    Directory.Delete(extractRoot, recursive: true);
                }
                catch
                {
                }
            }
        }

        var validation = await _validator.ValidateAsync(sourceDirectory, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid || validation.Manifest is null)
        {
            throw new InvalidOperationException($"Skin is invalid: {string.Join("; ", validation.Errors)}");
        }

        var targetDirectory = GetSkinDirectory(validation.Manifest.Id);
        var sourceFullPath = Path.GetFullPath(validation.SkinDirectory);
        var targetFullPath = Path.GetFullPath(targetDirectory);

        if (string.Equals(sourceFullPath.TrimEnd(Path.DirectorySeparatorChar), targetFullPath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            return validation.Manifest;
        }

        if (Directory.Exists(targetFullPath))
        {
            if (!overwrite)
            {
                throw new InvalidOperationException(
                    $"Skin '{validation.Manifest.Id}' is already installed. Pass --force to overwrite it.");
            }

            if (_backupService is not null)
            {
                await _backupService.CreateSkinBackupAsync(validation.Manifest.Id, "skin-overwrite", cancellationToken)
                    .ConfigureAwait(false);
            }

            Directory.Delete(targetFullPath, recursive: true);
        }

        CopyDirectory(sourceFullPath, targetFullPath);
        return validation.Manifest;
    }

    public async Task<IReadOnlyList<SkinManifest>> ListInstalledAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        var skins = new List<SkinManifest>();

        foreach (var directory in Directory.EnumerateDirectories(_paths.SkinsDirectory))
        {
            var result = await _validator.ValidateAsync(directory, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (result is { IsValid: true, Manifest: not null })
            {
                skins.Add(result.Manifest);
            }
        }

        return skins.OrderBy(skin => skin.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<ResolvedSkin> ResolveActiveSkinAsync(
        DesktopHtmlConfig config,
        CancellationToken cancellationToken = default)
    {
        var skinId = config.Skins.ActiveSkinId ?? SampleSkinConstants.Id;
        var entry = string.IsNullOrWhiteSpace(config.Skins.Entry) ? "index.html" : config.Skins.Entry;
        return await ResolveAsync(skinId, entry, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ResolvedSkin> ResolveForMonitorAsync(
        DesktopHtmlConfig config,
        string monitorId,
        CancellationToken cancellationToken = default)
    {
        if (config.Skins.PerMonitor.TryGetValue(monitorId, out var assignment)
            && !string.IsNullOrWhiteSpace(assignment.SkinId))
        {
            return await ResolveAsync(assignment.SkinId, assignment.Entry, cancellationToken)
                .ConfigureAwait(false);
        }

        return await ResolveActiveSkinAsync(config, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ResolvedSkin> ResolveSpanningAsync(
        DesktopHtmlConfig config,
        CancellationToken cancellationToken = default)
    {
        var skinId = string.IsNullOrWhiteSpace(config.Skins.Spanning.SkinId)
            ? config.Skins.ActiveSkinId ?? SampleSkinConstants.Id
            : config.Skins.Spanning.SkinId;

        var entry = string.IsNullOrWhiteSpace(config.Skins.Spanning.Entry)
            ? config.Skins.Entry
            : config.Skins.Spanning.Entry;

        return await ResolveAsync(skinId, entry, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ResolvedSkin> ResolveAsync(
        string skinId,
        string? requestedEntry,
        CancellationToken cancellationToken = default)
    {
        var directory = GetSkinDirectory(skinId);
        var validation = await _validator.ValidateAsync(directory, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!validation.IsValid || validation.Manifest is null)
        {
            throw new InvalidOperationException(
                $"Skin '{skinId}' is invalid: {string.Join("; ", validation.Errors)}");
        }

        var entry = string.IsNullOrWhiteSpace(requestedEntry)
            ? validation.Manifest.Entry
            : requestedEntry;

        if (validation.Manifest.Entries.TryGetValue(entry, out var namedEntry))
        {
            entry = namedEntry;
        }

        if (!SkinValidator.IsSafeRelativePath(entry))
        {
            throw new InvalidOperationException($"Skin entry '{entry}' is not a safe relative path.");
        }

        var entryFile = Path.GetFullPath(Path.Combine(directory, entry));
        if (!entryFile.StartsWith(Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Skin entry '{entry}' escapes the skin folder.");
        }

        if (!File.Exists(entryFile))
        {
            throw new FileNotFoundException($"Skin entry '{entry}' does not exist.", entryFile);
        }

        return new ResolvedSkin(validation.Manifest, directory, entry, entryFile);
    }

    public async Task ActivateAsync(
        DesktopHtmlConfig config,
        string skinId,
        string? entry,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(skinId, entry, cancellationToken).ConfigureAwait(false);
        config.Skins.ActiveMode = "single-monitor";
        config.Skins.ActiveSkinId = resolved.Manifest.Id;
        config.Skins.Entry = resolved.Entry;
    }

    public async Task AssignMonitorAsync(
        DesktopHtmlConfig config,
        string monitorId,
        string skinId,
        string? entry,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(skinId, entry, cancellationToken).ConfigureAwait(false);
        config.Skins.ActiveMode = "per-monitor";
        config.Skins.PerMonitor[monitorId] = new MonitorSkinAssignment
        {
            SkinId = resolved.Manifest.Id,
            Entry = resolved.Entry
        };
    }

    public void ClearMonitorAssignment(DesktopHtmlConfig config, string monitorId)
    {
        config.Skins.PerMonitor.Remove(monitorId);
        if (config.Skins.PerMonitor.Count == 0 && config.Skins.ActiveMode == "per-monitor")
        {
            config.Skins.ActiveMode = "single-monitor";
        }
    }

    public async Task AssignSpanningAsync(
        DesktopHtmlConfig config,
        string skinId,
        string? entry,
        IEnumerable<string> monitorIds,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(skinId, entry, cancellationToken).ConfigureAwait(false);
        config.Skins.ActiveMode = "spanning";
        config.Skins.Spanning.SkinId = resolved.Manifest.Id;
        config.Skins.Spanning.Entry = resolved.Entry;
        config.Skins.Spanning.Monitors = monitorIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void SetActiveMode(DesktopHtmlConfig config, string mode)
    {
        var normalized = mode.Trim().ToLowerInvariant();
        if (normalized is not ("single-monitor" or "per-monitor" or "spanning"))
        {
            throw new InvalidOperationException("Mode must be one of: single-monitor, per-monitor, spanning.");
        }

        config.Skins.ActiveMode = normalized;
    }

    public void Delete(DesktopHtmlConfig config, string skinId, bool toRecycleBin = true, Action<string>? onWarning = null)
    {
        _paths.EnsureCreated();
        var targetDirectory = GetSkinDirectory(skinId);

        if (!Directory.Exists(targetDirectory))
        {
            throw new DirectoryNotFoundException($"Skin folder '{skinId}' does not exist.");
        }

        if (string.Equals(config.Skins.ActiveSkinId, skinId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot delete skin because it is currently set as the active skin.");
        }

        if (config.Skins.PerMonitor.Values.Any(assignment => string.Equals(assignment.SkinId, skinId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Cannot delete skin because it is currently assigned to one or more monitors.");
        }

        if (string.Equals(config.Skins.Spanning.SkinId, skinId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot delete skin because it is currently assigned to spanning mode.");
        }

        DeleteDirectory(targetDirectory, toRecycleBin);

        var storageDirectory = Path.Combine(_paths.StorageDirectory, skinId);
        if (Directory.Exists(storageDirectory))
        {
            try
            {
                DeleteDirectory(storageDirectory, toRecycleBin);
            }
            catch (Exception ex)
            {
                onWarning?.Invoke($"Skin '{skinId}' was deleted, but its storage folder could not be removed: {ex.Message}");
            }
        }
    }

    private static void DeleteDirectory(string directory, bool toRecycleBin)
    {
        if (toRecycleBin && RecycleBin.TryMoveToRecycleBin(directory))
        {
            return;
        }

        Directory.Delete(directory, recursive: true);
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            var targetFile = Path.Combine(targetDirectory, Path.GetFileName(file));
            File.Copy(file, targetFile, overwrite: true);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            var targetSubdirectory = Path.Combine(targetDirectory, Path.GetFileName(directory));
            CopyDirectory(directory, targetSubdirectory);
        }
    }
}

public sealed record ResolvedSkin(
    SkinManifest Manifest,
    string Directory,
    string Entry,
    string EntryFile);
