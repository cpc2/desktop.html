using DesktopHtml.Core.FileSystem;

namespace DesktopHtml.Tests;

public sealed class FileSystemWatchServiceTests
{
    [Fact]
    public async Task Watch_ReportsCreatedFileAsBatchedChange()
    {
        using var temp = TempDirectory.Create();
        using var service = new FileSystemWatchService();
        var received = new TaskCompletionSource<IReadOnlyList<FileSystemChange>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var watchId = service.Watch(null, temp.Path, recursive: false,
            (_, changes) => received.TrySetResult(changes));

        await File.WriteAllTextAsync(Path.Combine(temp.Path, "new-file.txt"), "hello");

        var changes = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains(changes, change =>
            change.ChangeType is "created" or "changed" &&
            change.FullPath.EndsWith("new-file.txt", StringComparison.OrdinalIgnoreCase));

        Assert.True(service.Unwatch(watchId));
        Assert.False(service.Unwatch(watchId));
    }

    [Fact]
    public void Watch_ThrowsForMissingDirectory()
    {
        using var service = new FileSystemWatchService();
        Assert.Throws<DirectoryNotFoundException>(() =>
            service.Watch(null, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n")), false, (_, _) => { }));
    }

    [Fact]
    public async Task Unwatch_StopsEvents()
    {
        using var temp = TempDirectory.Create();
        using var service = new FileSystemWatchService();
        var fired = 0;

        var watchId = service.Watch("w1", temp.Path, false, (_, _) => Interlocked.Increment(ref fired));
        service.Unwatch(watchId);

        await File.WriteAllTextAsync(Path.Combine(temp.Path, "after-unwatch.txt"), "x");
        await Task.Delay(700);

        Assert.Equal(0, fired);
    }
}
