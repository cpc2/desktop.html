using DesktopHtml.Core;
using DesktopHtml.Core.Logging;

namespace DesktopHtml.Tests;

public sealed class LogServiceTests
{
    [Fact]
    public async Task WriteAndReadLines_ReturnsRecentEntries()
    {
        using var temp = TempDirectory.Create();
        var service = new LogService(CreatePaths(temp.Path));

        await service.InfoAsync("test", "first");
        await service.ErrorAsync("test", "second", new { code = 5 });

        var lines = service.ReadLines(1);

        Assert.Single(lines);
        Assert.Contains("second", lines[0]);
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
