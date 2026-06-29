using System.Text.Json;
using DesktopHtml.Core;
using DesktopHtml.Core.Backups;

namespace DesktopHtml.Tests;

public sealed class BackupServiceTests
{
    [Fact]
    public async Task CreateConfigBackupAsync_CopiesConfigAndWritesManifest()
    {
        using var temp = TempDirectory.Create();
        var paths = CreatePaths(temp.Path);
        paths.EnsureCreated();
        await File.WriteAllTextAsync(paths.ConfigFile, """{"schemaVersion":1}""");

        var manifest = await new BackupService(paths, "test-version")
            .CreateConfigBackupAsync("manual config");

        Assert.Equal(BackupKinds.Config, manifest.Kind);
        Assert.Equal("config.json", manifest.RelativeTargetPath);
        Assert.Equal("test-version", manifest.DesktopHtmlVersion);
        Assert.Contains("config.json", manifest.Files);
        Assert.True(File.Exists(Path.Combine(paths.BackupsDirectory, manifest.Id, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(paths.BackupsDirectory, manifest.Id, "payload", "config.json")));
    }

    [Fact]
    public async Task CreateSkinBackupAsync_CopiesNestedSkinFolder()
    {
        using var temp = TempDirectory.Create();
        var paths = CreatePaths(temp.Path);
        paths.EnsureCreated();
        var skinDirectory = Path.Combine(paths.SkinsDirectory, "example.skin");
        Directory.CreateDirectory(Path.Combine(skinDirectory, "assets"));
        await File.WriteAllTextAsync(Path.Combine(skinDirectory, "manifest.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(skinDirectory, "assets", "thing.txt"), "asset");

        var manifest = await new BackupService(paths)
            .CreateSkinBackupAsync("example.skin", "overwrite");

        Assert.Equal(BackupKinds.Skin, manifest.Kind);
        Assert.Equal("skins/example.skin", manifest.RelativeTargetPath);
        Assert.Contains(Path.Combine("assets", "thing.txt"), manifest.Files);
        Assert.True(File.Exists(Path.Combine(paths.BackupsDirectory, manifest.Id, "payload", "assets", "thing.txt")));
    }

    [Fact]
    public async Task CreateStorageBackupAsync_CopiesSkinStorageFile()
    {
        using var temp = TempDirectory.Create();
        var paths = CreatePaths(temp.Path);
        paths.EnsureCreated();
        var storageFile = Path.Combine(paths.StorageDirectory, "example.skin", "storage.json");
        Directory.CreateDirectory(Path.GetDirectoryName(storageFile)!);
        await File.WriteAllTextAsync(storageFile, """{"layout":"saved"}""");

        var manifest = await new BackupService(paths)
            .CreateStorageBackupAsync("example.skin", "clear");

        Assert.Equal(BackupKinds.Storage, manifest.Kind);
        Assert.Equal("storage/example.skin/storage.json", manifest.RelativeTargetPath);
        Assert.True(File.Exists(Path.Combine(paths.BackupsDirectory, manifest.Id, "payload", "storage.json")));
    }

    [Fact]
    public async Task RestoreAsync_ReplacesConfigAndCreatesSafetyBackup()
    {
        using var temp = TempDirectory.Create();
        var paths = CreatePaths(temp.Path);
        paths.EnsureCreated();
        await File.WriteAllTextAsync(paths.ConfigFile, """{"schemaVersion":1,"name":"before"}""");
        var service = new BackupService(paths);
        var manifest = await service.CreateConfigBackupAsync("known-good");
        await File.WriteAllTextAsync(paths.ConfigFile, """{"schemaVersion":1,"name":"after"}""");

        var result = await service.RestoreAsync(manifest.Id);

        Assert.Equal(manifest.Id, result.Restored.Id);
        Assert.NotNull(result.SafetyBackup);
        Assert.Contains("before", await File.ReadAllTextAsync(paths.ConfigFile));
        Assert.True(File.Exists(Path.Combine(paths.BackupsDirectory, result.SafetyBackup!.Id, "payload", "config.json")));
    }

    [Fact]
    public async Task RestoreAsync_ReplacesExistingSkinFolder()
    {
        using var temp = TempDirectory.Create();
        var paths = CreatePaths(temp.Path);
        paths.EnsureCreated();
        var skinDirectory = Path.Combine(paths.SkinsDirectory, "example.skin");
        Directory.CreateDirectory(skinDirectory);
        await File.WriteAllTextAsync(Path.Combine(skinDirectory, "index.html"), "before");
        var service = new BackupService(paths);
        var manifest = await service.CreateSkinBackupAsync("example.skin", "known-good");
        await File.WriteAllTextAsync(Path.Combine(skinDirectory, "index.html"), "after");

        await service.RestoreAsync(manifest.Id);

        Assert.Equal("before", await File.ReadAllTextAsync(Path.Combine(skinDirectory, "index.html")));
    }

    [Fact]
    public async Task ListAsync_ReturnsNewestFirst()
    {
        using var temp = TempDirectory.Create();
        var paths = CreatePaths(temp.Path);
        paths.EnsureCreated();
        await File.WriteAllTextAsync(paths.ConfigFile, "{}");
        var service = new BackupService(paths);

        var older = await service.CreateConfigBackupAsync("older");
        await Task.Delay(5);
        var newer = await service.CreateConfigBackupAsync("newer");

        var backups = await service.ListAsync();

        Assert.Equal(newer.Id, backups[0].Id);
        Assert.Equal(older.Id, backups[1].Id);
    }

    [Fact]
    public async Task PruneAsync_KeepsNewestBackups()
    {
        using var temp = TempDirectory.Create();
        var paths = CreatePaths(temp.Path);
        paths.EnsureCreated();
        await File.WriteAllTextAsync(paths.ConfigFile, "{}");
        var service = new BackupService(paths);

        var oldest = await service.CreateConfigBackupAsync("oldest");
        await Task.Delay(5);
        var newest = await service.CreateConfigBackupAsync("newest");

        var result = await service.PruneAsync(1);

        Assert.Equal([oldest.Id], result.Deleted.Select(item => item.Id).ToArray());
        Assert.True(Directory.Exists(Path.Combine(paths.BackupsDirectory, newest.Id)));
        Assert.False(Directory.Exists(Path.Combine(paths.BackupsDirectory, oldest.Id)));
    }

    [Fact]
    public async Task RestoreAsync_RejectsUnknownBackupId()
    {
        using var temp = TempDirectory.Create();
        var service = new BackupService(CreatePaths(temp.Path));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RestoreAsync("missing"));

        Assert.Contains("was not found", ex.Message);
    }

    [Fact]
    public async Task RestoreAsync_RejectsEscapingTargetPath()
    {
        using var temp = TempDirectory.Create();
        var paths = CreatePaths(temp.Path);
        paths.EnsureCreated();
        var backupDirectory = Path.Combine(paths.BackupsDirectory, "bad");
        Directory.CreateDirectory(Path.Combine(backupDirectory, "payload"));
        await File.WriteAllTextAsync(Path.Combine(backupDirectory, "payload", "config.json"), "{}");
        var manifest = new BackupManifest(
            "bad",
            DateTimeOffset.UtcNow,
            BackupKinds.Config,
            "bad",
            paths.ConfigFile,
            "../config.json",
            "test",
            ["config.json"]);
        await File.WriteAllTextAsync(
            Path.Combine(backupDirectory, "manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new BackupService(paths).RestoreAsync("bad"));

        Assert.Contains("unsafe", ex.Message);
    }

    private static AppPaths CreatePaths(string root)
    {
        return new AppPaths(
            root,
            Path.Combine(root, "config.json"),
            Path.Combine(root, "state.json"),
            Path.Combine(root, "logs"),
            Path.Combine(root, "skins"),
            Path.Combine(root, "storage"),
            Path.Combine(root, "backups"),
            Path.Combine(root, "webview2"));
    }
}
