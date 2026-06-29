using System.Text.Json.Nodes;
using DesktopHtml.Core.Configuration;

namespace DesktopHtml.Tests;

public sealed class ConfigPatchServiceTests
{
    [Fact]
    public void ApplyPatch_MergesNestedObjects()
    {
        var config = ConfigService.CreateDefault();
        var patched = ConfigPatchService.ApplyPatch(config, new JsonObject
        {
            ["app"] = new JsonObject
            {
                ["safeMode"] = true
            },
            ["desktop"] = new JsonObject
            {
                ["placementMode"] = "workerw",
                ["fallbackPlacementMode"] = "behind-normal-windows"
            }
        });

        Assert.True(patched.App.SafeMode);
        Assert.Equal("workerw", patched.Desktop.PlacementMode);
        Assert.Equal("behind-normal-windows", patched.Desktop.FallbackPlacementMode);
        Assert.True(patched.App.ShowTrayIcon);
    }

    [Fact]
    public void SetPath_SetsSingleNestedValue()
    {
        var config = ConfigService.CreateDefault();
        var patched = ConfigPatchService.SetPath(config, "performance.pauseWhenOnBattery", JsonValue.Create(true));

        Assert.True(patched.Performance.PauseWhenOnBattery);
        Assert.True(patched.Performance.PauseWhenFullscreenAppActive);
    }
}
