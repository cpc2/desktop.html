using DesktopHtml.Core.SystemInfo;

namespace DesktopHtml.Tests;

public sealed class SystemStatsServiceTests
{
    [Fact]
    public async Task GetStats_ReturnsPlausibleValues()
    {
        var service = new SystemStatsService();

        var first = service.GetStats();
        await Task.Delay(400);
        var second = service.GetStats();

        Assert.InRange(second.CpuPercent, 0, 100);
        Assert.True(second.Memory.TotalMb > 0);
        Assert.InRange(second.Memory.UsedMb, 0, second.Memory.TotalMb);
        Assert.NotEmpty(second.Disks);
        Assert.All(second.Disks, disk => Assert.True(disk.TotalGb > 0));
        Assert.True(second.Network.ReceivedBytesPerSec >= 0);
        Assert.NotNull(first.Battery);
    }
}
