using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using DesktopHtml.Core.Logging;

namespace DesktopHtml.App;

public sealed class DesktopRevealService : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint WineventOutOfContext = 0x0000;
    private const uint WineventSkipOwnProcess = 0x0002;

    private readonly Dispatcher _dispatcher;
    private readonly LogService? _logService;
    private readonly NativeMethods.WinEventProc _callback;
    private IntPtr _hook;
    private bool _disposed;

    public DesktopRevealService(Dispatcher dispatcher, LogService? logService = null)
    {
        _dispatcher = dispatcher;
        _logService = logService;
        _callback = OnWinEvent;
    }

    public event EventHandler<DesktopRevealChangedEventArgs>? ForegroundChanged;

    public bool IsRunning => _hook != IntPtr.Zero;

    public void Start()
    {
        if (_hook != IntPtr.Zero)
        {
            return;
        }

        _hook = NativeMethods.SetWinEventHook(
            EventSystemForeground,
            EventSystemForeground,
            IntPtr.Zero,
            _callback,
            0,
            0,
            WineventOutOfContext | WineventSkipOwnProcess);

        if (_hook == IntPtr.Zero)
        {
            _ = _logService?.WarningAsync("placement", "Desktop reveal foreground hook could not be started.");
            return;
        }

        _ = _logService?.InfoAsync("placement", "Desktop reveal foreground hook started.");
    }

    private void OnWinEvent(
        IntPtr hook,
        uint eventType,
        IntPtr windowHandle,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime)
    {
        if (_disposed || eventType != EventSystemForeground || windowHandle == IntPtr.Zero)
        {
            return;
        }

        var className = GetClassName(windowHandle);
        var title = GetWindowTitle(windowHandle);
        var isDesktopForeground = IsDesktopForegroundWindow(windowHandle, className);
        var args = new DesktopRevealChangedEventArgs(windowHandle, className, title, isDesktopForeground);

        _dispatcher.BeginInvoke(() => ForegroundChanged?.Invoke(this, args));
    }

    private static bool IsDesktopForegroundWindow(IntPtr windowHandle, string className)
    {
        if (IsDesktopClass(className))
        {
            return true;
        }

        var parent = NativeMethods.GetParent(windowHandle);
        while (parent != IntPtr.Zero)
        {
            if (IsDesktopClass(GetClassName(parent)))
            {
                return true;
            }

            parent = NativeMethods.GetParent(parent);
        }

        return false;
    }

    private static bool IsDesktopClass(string className) =>
        string.Equals(className, "Progman", StringComparison.Ordinal)
        || string.Equals(className, "WorkerW", StringComparison.Ordinal)
        || string.Equals(className, "SHELLDLL_DefView", StringComparison.Ordinal);

    private static string GetClassName(IntPtr handle)
    {
        var builder = new StringBuilder(256);
        NativeMethods.GetClassName(handle, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string GetWindowTitle(IntPtr handle)
    {
        var length = NativeMethods.GetWindowTextLength(handle);
        if (length <= 0)
        {
            return "";
        }

        var builder = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(handle, builder, builder.Capacity);
        return builder.ToString();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }

    private static class NativeMethods
    {
        public delegate void WinEventProc(
            IntPtr hook,
            uint eventType,
            IntPtr windowHandle,
            int objectId,
            int childId,
            uint eventThread,
            uint eventTime);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWinEventHook(
            uint eventMin,
            uint eventMax,
            IntPtr eventHookAssembly,
            WinEventProc eventProc,
            uint processId,
            uint threadId,
            uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnhookWinEvent(IntPtr hook);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetClassName(IntPtr handle, StringBuilder className, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetWindowText(IntPtr handle, StringBuilder text, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetWindowTextLength(IntPtr handle);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetParent(IntPtr handle);
    }
}

public sealed record DesktopRevealChangedEventArgs(
    IntPtr ForegroundHandle,
    string ForegroundClassName,
    string ForegroundTitle,
    bool IsDesktopForeground);
