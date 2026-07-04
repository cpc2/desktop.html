using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using DesktopHtml.Core.Monitors;

namespace DesktopHtml.App;

/// <summary>
/// Detects when the desktop surface is fully covered by a maximized or
/// fullscreen window, so skins can pause animation work nobody can see.
/// Event-driven (WinEvent hooks on foreground / move-size / minimize), no
/// polling: window events are debounced and covering rects recomputed only
/// then. Must be created on the UI thread (the hook needs a message pump).
/// </summary>
public sealed class DesktopVisibilityService : IDisposable
{
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint EVENT_SYSTEM_MOVESIZEEND = 0x000B;
    private const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
    private const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const int DWMWA_CLOAKED = 14;

    public static DesktopVisibilityService? Instance { get; private set; }

    private readonly List<IntPtr> _hooks = new();
    // The delegate must outlive the hooks or the GC collects the callback.
    private readonly WinEventDelegate _callback;
    private readonly DispatcherTimer _debounce;
    private readonly int _ownProcessId = Environment.ProcessId;
    private RECT[] _coveringRects = [];

    /// <summary>Raised (debounced, on the UI thread) when the set of covering
    /// windows may have changed.</summary>
    public event Action? Changed;

    public static DesktopVisibilityService Initialize()
    {
        Instance ??= new DesktopVisibilityService();
        return Instance;
    }

    private DesktopVisibilityService()
    {
        _callback = OnWinEvent;
        _debounce = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            Recompute();
        };

        Hook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND);
        Hook(EVENT_SYSTEM_MOVESIZEEND, EVENT_SYSTEM_MOVESIZEEND);
        Hook(EVENT_SYSTEM_MINIMIZESTART, EVENT_SYSTEM_MINIMIZEEND);
        Recompute();
    }

    /// <summary>True when some covering window fully contains the monitor.</summary>
    public bool IsMonitorOccluded(DesktopRectangle monitorBounds)
    {
        foreach (var rect in _coveringRects)
        {
            if (rect.Left <= monitorBounds.Left && rect.Top <= monitorBounds.Top &&
                rect.Right >= monitorBounds.Right && rect.Bottom >= monitorBounds.Bottom)
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        foreach (var hook in _hooks)
        {
            UnhookWinEvent(hook);
        }

        _hooks.Clear();
        _debounce.Stop();
        Instance = null;
    }

    private void Hook(uint eventMin, uint eventMax)
    {
        var hook = SetWinEventHook(eventMin, eventMax, IntPtr.Zero, _callback, 0, 0, WINEVENT_OUTOFCONTEXT);
        if (hook != IntPtr.Zero)
        {
            _hooks.Add(hook);
        }
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
    {
        // Restart the debounce window on every event burst.
        _debounce.Stop();
        _debounce.Start();
    }

    private void Recompute()
    {
        var rects = new List<RECT>();
        EnumWindows((hwnd, _) =>
        {
            if (IsCoveringCandidate(hwnd, out var rect))
            {
                rects.Add(rect);
            }

            return true;
        }, IntPtr.Zero);

        _coveringRects = rects.ToArray();
        Changed?.Invoke();
    }

    private bool IsCoveringCandidate(IntPtr hwnd, out RECT rect)
    {
        rect = default;
        if (!IsWindowVisible(hwnd) || IsIconic(hwnd))
        {
            return false;
        }

        GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == _ownProcessId)
        {
            return false;
        }

        // Skip the shell's own full-screen surfaces (wallpaper hosts).
        var className = new StringBuilder(64);
        GetClassName(hwnd, className, className.Capacity);
        var cls = className.ToString();
        if (cls is "Progman" or "WorkerW")
        {
            return false;
        }

        // Skip DWM-cloaked windows (suspended UWP apps, other virtual desktops).
        if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
        {
            return false;
        }

        if (!GetWindowRect(hwnd, out rect) || rect.Right - rect.Left < 200 || rect.Bottom - rect.Top < 200)
        {
            return false;
        }

        // Only windows that fully contain some monitor can occlude a desktop
        // surface; anything smaller is ignored.
        foreach (var monitor in new MonitorService().GetMonitors())
        {
            var bounds = monitor.Bounds;
            if (rect.Left <= bounds.Left && rect.Top <= bounds.Top &&
                rect.Right >= bounds.Right && rect.Bottom >= bounds.Bottom)
            {
                return true;
            }
        }

        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint lpdwProcessId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);
}
