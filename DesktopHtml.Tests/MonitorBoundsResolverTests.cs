using DesktopHtml.Core.Monitors;

namespace DesktopHtml.Tests;

public sealed class MonitorBoundsResolverTests
{
    private static readonly MonitorSnapshot Monitor = new(
        @"\\.\DISPLAY2",
        @"\\.\DISPLAY2",
        new DesktopRectangle(0, 0, 3840, 2160),
        new DesktopRectangle(0, 0, 3840, 2110),
        true,
        1.25);

    [Fact]
    public void SelectTargetBounds_UsesPhysicalWorkArea_WhenAvoidingTaskbar()
    {
        var target = MonitorBoundsResolver.SelectTargetBounds(Monitor, avoidTaskbar: true);

        Assert.Equal(Monitor.WorkArea, target);
        Assert.Equal(2110, target.Height);
    }

    [Fact]
    public void SelectTargetBounds_UsesFullMonitor_WhenTaskbarAvoidanceIsDisabled()
    {
        var target = MonitorBoundsResolver.SelectTargetBounds(Monitor, avoidTaskbar: false);

        Assert.Equal(Monitor.Bounds, target);
        Assert.Equal(2160, target.Height);
    }
}
