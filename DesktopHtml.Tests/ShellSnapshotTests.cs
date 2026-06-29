using DesktopHtml.Core.Placement;

namespace DesktopHtml.Tests;

public sealed class ShellSnapshotTests
{
    [Fact]
    public void Create_NormalizesSignatureParts()
    {
        var snapshot = ShellSnapshot.Create(" 0xabc ", "0xdef", "0x123", 2);

        Assert.True(snapshot.IsReady);
        Assert.Equal("0XABC", snapshot.ProgmanHandle);
        Assert.Equal("0XDEF", snapshot.ShellDllDefViewHandle);
        Assert.Equal("0X123", snapshot.WallpaperWorkerWHandle);
        Assert.Equal("0XABC|0XDEF|0X123|2", snapshot.Signature);
    }

    [Fact]
    public void HasChanged_UsesStableSignature()
    {
        var first = ShellSnapshot.Create("0x1", "0x2", "0x3", 1);
        var same = ShellSnapshot.Create("0X1", "0X2", "0X3", 1);
        var changed = ShellSnapshot.Create("0x1", "0x4", "0x3", 1);

        Assert.False(same.HasChanged(first));
        Assert.True(changed.HasChanged(first));
    }

    [Fact]
    public void IsReady_RequiresProgmanAndShellDefView()
    {
        var notReady = ShellSnapshot.Create("0x1", null, null, 0);

        Assert.False(notReady.IsReady);
    }
}
