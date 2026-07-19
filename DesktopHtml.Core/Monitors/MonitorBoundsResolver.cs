namespace DesktopHtml.Core.Monitors;

public static class MonitorBoundsResolver
{
    public static DesktopRectangle SelectTargetBounds(MonitorSnapshot monitor, bool avoidTaskbar) =>
        avoidTaskbar ? monitor.WorkArea : monitor.Bounds;
}
