using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DesktopHtml.Core.Monitors;

public sealed class MonitorService
{
    public IReadOnlyList<MonitorSnapshot> GetMonitors()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Array.Empty<MonitorSnapshot>();
        }

        var monitors = new List<MonitorSnapshot>();

        var ok = NativeMethods.EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (monitorHandle, _, _, _) =>
            {
                var info = new NativeMethods.MonitorInfoEx();
                info.Size = Marshal.SizeOf<NativeMethods.MonitorInfoEx>();

                if (!NativeMethods.GetMonitorInfo(monitorHandle, ref info))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                var deviceName = info.DeviceName.TrimEnd('\0');
                monitors.Add(new MonitorSnapshot(
                    string.IsNullOrWhiteSpace(deviceName) ? $"MONITOR{monitors.Count + 1}" : deviceName,
                    deviceName,
                    ToRectangle(info.Monitor),
                    ToRectangle(info.WorkArea),
                    (info.Flags & NativeMethods.MonitorInfoPrimary) != 0,
                    1.0));

                return true;
            },
            IntPtr.Zero);

        if (!ok)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return monitors
            .OrderByDescending(monitor => monitor.IsPrimary)
            .ThenBy(monitor => monitor.Bounds.Left)
            .ThenBy(monitor => monitor.Bounds.Top)
            .ToArray();
    }

    public DesktopRectangle GetVirtualDesktopBounds()
    {
        return GetVirtualBounds(GetMonitors());
    }

    public static DesktopRectangle GetVirtualBounds(
        IEnumerable<MonitorSnapshot> monitors,
        bool useWorkArea = false)
    {
        var snapshots = monitors.ToArray();
        if (snapshots.Length == 0)
        {
            return new DesktopRectangle(0, 0, 0, 0);
        }

        var rectangles = snapshots.Select(monitor => useWorkArea ? monitor.WorkArea : monitor.Bounds).ToArray();
        var left = rectangles.Min(rectangle => rectangle.Left);
        var top = rectangles.Min(rectangle => rectangle.Top);
        var right = rectangles.Max(rectangle => rectangle.Right);
        var bottom = rectangles.Max(rectangle => rectangle.Bottom);

        return new DesktopRectangle(left, top, right - left, bottom - top);
    }

    private static DesktopRectangle ToRectangle(NativeMethods.Rect rect) =>
        new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

    private static class NativeMethods
    {
        public const int MonitorInfoPrimary = 0x00000001;

        public delegate bool MonitorEnumProc(
            IntPtr monitorHandle,
            IntPtr deviceContext,
            IntPtr rect,
            IntPtr data);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool EnumDisplayMonitors(
            IntPtr deviceContext,
            IntPtr clipRect,
            MonitorEnumProc callback,
            IntPtr data);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool GetMonitorInfo(IntPtr monitorHandle, ref MonitorInfoEx monitorInfo);

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MonitorInfoEx
        {
            public int Size;
            public Rect Monitor;
            public Rect WorkArea;
            public int Flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
        }
    }
}
