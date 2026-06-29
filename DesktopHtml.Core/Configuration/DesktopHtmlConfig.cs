using System.Text.Json.Serialization;
using DesktopHtml.Core.Skins;

namespace DesktopHtml.Core.Configuration;

public sealed class DesktopHtmlConfig
{
    public int SchemaVersion { get; set; } = 1;
    public AppSettings App { get; set; } = new();
    public DesktopSettings Desktop { get; set; } = new();
    public PerformanceSettings Performance { get; set; } = new();
    public SkinSettings Skins { get; set; } = new();
}

public sealed class AppSettings
{
    public bool StartWithWindows { get; set; }
    public bool ShowTrayIcon { get; set; } = true;
    public bool SafeMode { get; set; }
    public string LogLevel { get; set; } = "info";
}

public sealed class DesktopSettings
{
    public string PlacementMode { get; set; } = "behind-normal-windows";
    public string FallbackPlacementMode { get; set; } = "behind-normal-windows";
    public bool AvoidTaskbar { get; set; } = true;
    public bool ShowInAltTab { get; set; }
    public bool ShowInTaskbar { get; set; }
}

public sealed class PerformanceSettings
{
    public bool PauseWhenFullscreenAppActive { get; set; } = true;
    public bool PauseWhenOnBattery { get; set; }
    public int? TargetFrameRate { get; set; }
}

public sealed class SkinSettings
{
    public string ActiveMode { get; set; } = "single-monitor";
    public string? ActiveSkinId { get; set; } = SampleSkinConstants.Id;
    public string Entry { get; set; } = "index.html";
    public Dictionary<string, MonitorSkinAssignment> PerMonitor { get; set; } = new();
    public SpanningSkinAssignment Spanning { get; set; } = new();
}

public sealed class MonitorSkinAssignment
{
    public string? SkinId { get; set; }
    public string Entry { get; set; } = "index.html";
}

public sealed class SpanningSkinAssignment
{
    public string? SkinId { get; set; }
    public string Entry { get; set; } = "index.html";
    public List<string> Monitors { get; set; } = new();
}
