using DesktopHtml.Core;
using DesktopHtml.Core.Backups;
using DesktopHtml.Core.Configuration;
using DesktopHtml.Core.Skins;

namespace DesktopHtml.Tests;

public sealed class SkinStoreTests
{
    [Fact]
    public async Task InstallAsync_CopiesValidSkinIntoAppData()
    {
        using var temp = TempDirectory.Create();
        var paths = CreatePaths(Path.Combine(temp.Path, "appdata"));
        var source = CreateSkin(temp.Path, "example.valid", "Example Skin");

        var manifest = await new SkinStore(paths).InstallAsync(source, overwrite: false);

        Assert.Equal("example.valid", manifest.Id);
        Assert.True(File.Exists(Path.Combine(paths.SkinsDirectory, "example.valid", "manifest.json")));
        Assert.True(File.Exists(Path.Combine(paths.SkinsDirectory, "example.valid", "index.html")));
    }

    [Fact]
    public async Task InstallAsync_RequiresForceToOverwriteInstalledSkin()
    {
        using var temp = TempDirectory.Create();
        var paths = CreatePaths(Path.Combine(temp.Path, "appdata"));
        var source = CreateSkin(temp.Path, "example.valid", "Example Skin");
        var store = new SkinStore(paths);

        await store.InstallAsync(source, overwrite: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.InstallAsync(source, overwrite: false));
        Assert.Contains("--force", ex.Message);

        await store.InstallAsync(source, overwrite: true);
    }

    [Fact]
    public async Task InstallAsync_WithBackupService_BacksUpOverwrittenSkin()
    {
        using var temp = TempDirectory.Create();
        var paths = CreatePaths(Path.Combine(temp.Path, "appdata"));
        var source = CreateSkin(temp.Path, "example.valid", "Example Skin");
        var backupService = new BackupService(paths, "test");
        var store = new SkinStore(paths, backupService: backupService);

        await store.InstallAsync(source, overwrite: false);
        await File.WriteAllTextAsync(Path.Combine(paths.SkinsDirectory, "example.valid", "old.txt"), "old");
        await store.InstallAsync(source, overwrite: true);

        var backup = Assert.Single(await backupService.ListAsync());
        Assert.Equal(BackupKinds.Skin, backup.Kind);
        Assert.True(File.Exists(Path.Combine(paths.BackupsDirectory, backup.Id, "payload", "old.txt")));
    }

    [Fact]
    public async Task ActivateAsync_UpdatesRuntimeConfig()
    {
        using var temp = TempDirectory.Create();
        var paths = CreatePaths(Path.Combine(temp.Path, "appdata"));
        var source = CreateSkin(temp.Path, "example.valid", "Example Skin");
        var store = new SkinStore(paths);
        await store.InstallAsync(source, overwrite: false);
        var config = ConfigService.CreateDefault();

        await store.ActivateAsync(config, "example.valid", "index.html");

        Assert.Equal("single-monitor", config.Skins.ActiveMode);
        Assert.Equal("example.valid", config.Skins.ActiveSkinId);
        Assert.Equal("index.html", config.Skins.Entry);
    }

    [Fact]
    public async Task AssignMonitorAsync_ResolvesAssignedMonitorSkin()
    {
        using var temp = TempDirectory.Create();
        var paths = CreatePaths(Path.Combine(temp.Path, "appdata"));
        var source = CreateSkin(temp.Path, "example.valid", "Example Skin");
        var store = new SkinStore(paths);
        await store.InstallAsync(source, overwrite: false);
        var config = ConfigService.CreateDefault();

        await store.AssignMonitorAsync(config, @"\\.\DISPLAY2", "example.valid", "index.html");
        var resolved = await store.ResolveForMonitorAsync(config, @"\\.\DISPLAY2");

        Assert.Equal("per-monitor", config.Skins.ActiveMode);
        Assert.Equal("example.valid", config.Skins.PerMonitor[@"\\.\DISPLAY2"].SkinId);
        Assert.Equal("example.valid", resolved.Manifest.Id);
    }

    [Fact]
    public async Task ResolveForMonitorAsync_FallsBackToActiveSkin()
    {
        using var temp = TempDirectory.Create();
        var paths = CreatePaths(Path.Combine(temp.Path, "appdata"));
        var source = CreateSkin(temp.Path, "example.valid", "Example Skin");
        var store = new SkinStore(paths);
        await store.InstallAsync(source, overwrite: false);
        var config = ConfigService.CreateDefault();
        await store.ActivateAsync(config, "example.valid", "index.html");

        var resolved = await store.ResolveForMonitorAsync(config, @"\\.\DISPLAY3");

        Assert.Equal("example.valid", resolved.Manifest.Id);
    }

    [Fact]
    public async Task AssignSpanningAsync_UpdatesSpanningConfig()
    {
        using var temp = TempDirectory.Create();
        var paths = CreatePaths(Path.Combine(temp.Path, "appdata"));
        var source = CreateSkin(temp.Path, "example.valid", "Example Skin");
        var store = new SkinStore(paths);
        await store.InstallAsync(source, overwrite: false);
        var config = ConfigService.CreateDefault();

        await store.AssignSpanningAsync(config, "example.valid", "index.html", [@"\\.\DISPLAY2", @"\\.\DISPLAY3"]);
        var resolved = await store.ResolveSpanningAsync(config);

        Assert.Equal("spanning", config.Skins.ActiveMode);
        Assert.Equal("example.valid", config.Skins.Spanning.SkinId);
        Assert.Equal("index.html", config.Skins.Spanning.Entry);
        Assert.Equal([@"\\.\DISPLAY2", @"\\.\DISPLAY3"], config.Skins.Spanning.Monitors);
        Assert.Equal("example.valid", resolved.Manifest.Id);
    }

    [Fact]
    public void SetActiveMode_RejectsUnknownMode()
    {
        var store = new SkinStore(CreatePaths(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        var config = ConfigService.CreateDefault();

        Assert.Throws<InvalidOperationException>(() => store.SetActiveMode(config, "banana"));
    }

    [Fact]
    public async Task InstallAsync_InstallsFromZipPackage()
    {
        using var temp = TempDirectory.Create();
        var paths = CreatePaths(Path.Combine(temp.Path, "appdata"));
        var source = CreateSkin(temp.Path, "example.zipped", "Zipped Skin");
        var zipPath = Path.Combine(temp.Path, "example.zipped-0.1.0.zip");
        System.IO.Compression.ZipFile.CreateFromDirectory(source, zipPath, System.IO.Compression.CompressionLevel.Fastest, includeBaseDirectory: false);

        var manifest = await new SkinStore(paths).InstallAsync(zipPath, overwrite: false);

        Assert.Equal("example.zipped", manifest.Id);
        Assert.True(File.Exists(Path.Combine(paths.SkinsDirectory, "example.zipped", "index.html")));
    }

    [Fact]
    public async Task InstallAsync_InstallsFromZipWithWrappingDirectory()
    {
        using var temp = TempDirectory.Create();
        var paths = CreatePaths(Path.Combine(temp.Path, "appdata"));
        var source = CreateSkin(temp.Path, "example.wrapped", "Wrapped Skin");
        var zipPath = Path.Combine(temp.Path, "wrapped.zip");
        System.IO.Compression.ZipFile.CreateFromDirectory(source, zipPath, System.IO.Compression.CompressionLevel.Fastest, includeBaseDirectory: true);

        var manifest = await new SkinStore(paths).InstallAsync(zipPath, overwrite: false);

        Assert.Equal("example.wrapped", manifest.Id);
        Assert.True(File.Exists(Path.Combine(paths.SkinsDirectory, "example.wrapped", "manifest.json")));
    }

    private static string CreateSkin(string root, string id, string name)
    {
        var skinRoot = Path.Combine(root, "source-skin");
        Directory.CreateDirectory(skinRoot);
        File.WriteAllText(Path.Combine(skinRoot, "manifest.json"), $$"""
{
  "schemaVersion": 1,
  "id": "{{id}}",
  "name": "{{name}}",
  "version": "0.1.0",
  "author": "test",
  "entry": "index.html",
  "permissions": {
    "fullTrust": true,
    "network": true,
    "rawExecution": true
  }
}
""");
        File.WriteAllText(Path.Combine(skinRoot, "index.html"), "<!doctype html><title>test</title>");
        return skinRoot;
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
