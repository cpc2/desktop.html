using System.Text.Json.Nodes;
using DesktopHtml.Core;
using DesktopHtml.Core.Backups;
using DesktopHtml.Core.Storage;

namespace DesktopHtml.Tests;

public sealed class SkinStorageServiceTests
{
    [Fact]
    public async Task StorageRoundTrip_PersistsValuesBySkinId()
    {
        using var temp = TempDirectory.Create();
        var service = new SkinStorageService(CreatePaths(temp.Path));

        await service.SetAsync("skin.one", "position", new JsonObject
        {
            ["x"] = 42,
            ["y"] = 18
        });

        var value = await service.GetAsync("skin.one", "position");
        var all = await service.GetAllAsync("skin.one");

        Assert.Equal(42, value?["x"]?.GetValue<int>());
        Assert.True(all.ContainsKey("position"));
    }

    [Fact]
    public async Task RemoveAndClear_UpdateStoredObject()
    {
        using var temp = TempDirectory.Create();
        var service = new SkinStorageService(CreatePaths(temp.Path));

        await service.SetAsync("skin.one", "first", JsonValue.Create("a"));
        await service.SetAsync("skin.one", "second", JsonValue.Create("b"));
        await service.RemoveAsync("skin.one", "first");

        Assert.Null(await service.GetAsync("skin.one", "first"));
        Assert.NotNull(await service.GetAsync("skin.one", "second"));

        await service.ClearAsync("skin.one");
        Assert.Empty(await service.GetAllAsync("skin.one"));
    }

    [Fact]
    public async Task ClearAsync_WithBackupService_BacksUpExistingStorage()
    {
        using var temp = TempDirectory.Create();
        var paths = CreatePaths(temp.Path);
        var backupService = new BackupService(paths, "test");
        var service = new SkinStorageService(paths, backupService);

        await service.SetAsync("skin.one", "position", JsonValue.Create("before"));
        await service.ClearAsync("skin.one");

        var backup = Assert.Single(await backupService.ListAsync());
        Assert.Equal(BackupKinds.Storage, backup.Kind);
        Assert.True(File.Exists(Path.Combine(paths.BackupsDirectory, backup.Id, "payload", "storage.json")));
        Assert.Empty(await service.GetAllAsync("skin.one"));
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
