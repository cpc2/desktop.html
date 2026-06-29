using DesktopHtml.Core;
using DesktopHtml.Core.Configuration;

namespace DesktopHtml.Tests;

public sealed class ConfigServiceTests
{
    [Fact]
    public async Task LoadOrCreateAsync_CreatesDefaultConfig()
    {
        using var temp = TempDirectory.Create();
        var paths = CreatePaths(temp.Path);
        var service = new ConfigService(paths);

        var config = await service.LoadOrCreateAsync();

        Assert.Equal(1, config.SchemaVersion);
        Assert.Equal("single-monitor", config.Skins.ActiveMode);
        Assert.True(File.Exists(paths.ConfigFile));
    }

    [Fact]
    public async Task SaveAsync_OverwritesExistingConfig()
    {
        using var temp = TempDirectory.Create();
        var paths = CreatePaths(temp.Path);
        var service = new ConfigService(paths);
        var config = ConfigService.CreateDefault();

        await service.SaveAsync(config);
        config.App.SafeMode = true;
        await service.SaveAsync(config);

        var reloaded = await service.LoadOrCreateAsync();
        Assert.True(reloaded.App.SafeMode);
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
