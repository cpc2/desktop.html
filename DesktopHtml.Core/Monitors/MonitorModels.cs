namespace DesktopHtml.Core.Monitors;

public sealed record DesktopRectangle(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
}

public sealed record MonitorSnapshot(
    string Id,
    string DeviceName,
    DesktopRectangle Bounds,
    DesktopRectangle WorkArea,
    bool IsPrimary,
    double DpiScale);
