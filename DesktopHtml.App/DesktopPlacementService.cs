using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using DesktopHtml.Core.Configuration;
using DesktopHtml.Core.Logging;
using DesktopHtml.Core.Monitors;
using DesktopHtml.Core.Placement;

namespace DesktopHtml.App;

public sealed class DesktopPlacementService
{
    private const string StageAPlacementMode = "behind-normal-windows";
    private const string WorkerWPlacementMode = "workerw";
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExAppWindow = 0x00040000L;
    private const string ShowDesktopPlacementMode = "show-desktop";
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndBottom = new(1);

    private readonly LogService? _logService;
    private readonly Dictionary<IntPtr, PlacementState> _windowStates = new();

    public DateTimeOffset? LastPlacementReapplyUtc { get; private set; }
    public string? LastPlacementReapplyReason { get; private set; }

    public DesktopPlacementService(LogService? logService = null)
    {
        _logService = logService;
    }

    public PlacementApplyResult ApplyPlacement(MainWindow window, DesktopHtmlConfig config)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Window handle is not available for placement.");
        }

        var targetBounds = ResolveTargetBounds(window.CurrentMonitor, config.Desktop.AvoidTaskbar);
        var requestedMode = NormalizeMode(config.Desktop.PlacementMode);
        var fallbackMode = NormalizeFallbackMode(config.Desktop.FallbackPlacementMode);
        var appliedMode = requestedMode;
        var attachedToWorkerW = false;
        string? parentHandleHex = null;
        string? error = null;

        try
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.ShowInTaskbar = config.Desktop.ShowInTaskbar;
            ApplyAltTabStyle(handle, config.Desktop.ShowInAltTab);
            ApplyWindowBounds(handle, targetBounds);

            if (string.Equals(requestedMode, WorkerWPlacementMode, StringComparison.OrdinalIgnoreCase))
            {
                var workerWResult = TryAttachToWorkerW(handle);
                if (workerWResult.Attached)
                {
                    attachedToWorkerW = true;
                    parentHandleHex = FormatHandle(workerWResult.WorkerWHandle);
                    appliedMode = WorkerWPlacementMode;
                }
                else
                {
                    error = workerWResult.ErrorMessage;
                    appliedMode = fallbackMode;
                    ApplyStageAPlacement(handle, targetBounds);
                    LogPlacementFallback(window, requestedMode, fallbackMode, targetBounds, error);
                }
            }
            else
            {
                ApplyStageAPlacement(handle, targetBounds);
                appliedMode = StageAPlacementMode;
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            appliedMode = fallbackMode;
            TryApplyStageAFallback(handle, targetBounds);
            LogPlacementFailure(window, requestedMode, fallbackMode, targetBounds, ex);
        }

        var state = new PlacementState(
            FormatHandle(handle),
            window.MonitorId,
            window.Title,
            targetBounds,
            requestedMode,
            appliedMode,
            attachedToWorkerW,
            parentHandleHex ?? FormatHandle(NativeMethods.GetParent(handle)),
            error);

        _windowStates[handle] = state;
        LogPlacementApplied(window, state);
        return new PlacementApplyResult(appliedMode, targetBounds, attachedToWorkerW, error);
    }

    public PlacementApplyResult ApplyShowDesktopPlacement(MainWindow window, DesktopHtmlConfig config)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Window handle is not available for placement.");
        }

        var targetBounds = ResolveTargetBounds(window.CurrentMonitor, config.Desktop.AvoidTaskbar);

        try
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.ShowInTaskbar = config.Desktop.ShowInTaskbar;
            ApplyAltTabStyle(handle, config.Desktop.ShowInAltTab);
            ApplyWindowBounds(handle, targetBounds);
            ApplyShowDesktopZOrder(handle, targetBounds);

            var state = new PlacementState(
                FormatHandle(handle),
                window.MonitorId,
                window.Title,
                targetBounds,
                NormalizeMode(config.Desktop.PlacementMode),
                ShowDesktopPlacementMode,
                false,
                FormatHandle(NativeMethods.GetParent(handle)),
                null);

            _windowStates[handle] = state;
            LogPlacementApplied(window, state);
            return new PlacementApplyResult(ShowDesktopPlacementMode, targetBounds, false, null);
        }
        catch (Exception ex)
        {
            LogPlacementFailure(
                window,
                ShowDesktopPlacementMode,
                NormalizeFallbackMode(config.Desktop.FallbackPlacementMode),
                targetBounds,
                ex);
            TryApplyStageAFallback(handle, targetBounds);
            return new PlacementApplyResult(
                NormalizeFallbackMode(config.Desktop.FallbackPlacementMode),
                targetBounds,
                false,
                ex.Message);
        }
    }

    public PlacementDiagnostics BuildDiagnostics(DesktopHtmlConfig config, IEnumerable<MainWindow> windows)
    {
        var monitorService = new MonitorService();
        var monitors = monitorService.GetMonitors();
        var shellWindows = DiscoverShellWindows();
        var shellSnapshot = CreateShellSnapshot(shellWindows);
        var hostWindows = windows
            .Select(window => CreateHostWindowDiagnostics(window, config))
            .Where(diagnostics => diagnostics is not null)
            .Select(diagnostics => diagnostics!)
            .ToArray();

        return new PlacementDiagnostics(
            config.Desktop.PlacementMode,
            config.Desktop.FallbackPlacementMode,
            config.Desktop.AvoidTaskbar,
            config.Desktop.ShowInAltTab,
            config.Desktop.ShowInTaskbar,
            monitors,
            shellWindows,
            hostWindows,
            shellWindows.WallpaperWorkerW is not null || shellWindows.WorkerWs.Count > 0,
            shellSnapshot,
            shellSnapshot.IsReady,
            shellSnapshot.Signature,
            LastPlacementReapplyUtc,
            LastPlacementReapplyReason);
    }

    public PlacementDiagnostics BuildStaticDiagnostics(DesktopHtmlConfig config)
    {
        var monitorService = new MonitorService();
        var monitors = monitorService.GetMonitors();
        var shellWindows = DiscoverShellWindows();
        var shellSnapshot = CreateShellSnapshot(shellWindows);

        return new PlacementDiagnostics(
            config.Desktop.PlacementMode,
            config.Desktop.FallbackPlacementMode,
            config.Desktop.AvoidTaskbar,
            config.Desktop.ShowInAltTab,
            config.Desktop.ShowInTaskbar,
            monitors,
            shellWindows,
            Array.Empty<HostWindowDiagnostics>(),
            shellWindows.WallpaperWorkerW is not null || shellWindows.WorkerWs.Count > 0,
            shellSnapshot,
            shellSnapshot.IsReady,
            shellSnapshot.Signature,
            LastPlacementReapplyUtc,
            LastPlacementReapplyReason);
    }

    public ShellSnapshot CaptureShellSnapshot() => CreateShellSnapshot(DiscoverShellWindows());

    public bool IsShellReady() => CaptureShellSnapshot().IsReady;

    public bool HasShellChanged(ShellSnapshot? previousSnapshot) =>
        CaptureShellSnapshot().HasChanged(previousSnapshot);

    public async Task<ShellSnapshot> WaitForShellReadyAsync(
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        ShellSnapshot snapshot;

        do
        {
            snapshot = CaptureShellSnapshot();
            if (snapshot.IsReady || DateTimeOffset.UtcNow >= deadline)
            {
                return snapshot;
            }

            await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
        }
        while (true);
    }

    public PlacementReapplyResult RecordPlacementReapply(string reason, int windowCount)
    {
        LastPlacementReapplyUtc = DateTimeOffset.UtcNow;
        LastPlacementReapplyReason = string.IsNullOrWhiteSpace(reason) ? "unspecified" : reason;

        return new PlacementReapplyResult(
            true,
            LastPlacementReapplyReason,
            windowCount,
            LastPlacementReapplyUtc.Value,
            CaptureShellSnapshot());
    }

    public ShellWindowDiagnostics DiscoverShellWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new ShellWindowDiagnostics(null, null, Array.Empty<WindowInfo>(), null);
        }

        var progman = NativeMethods.FindWindow("Progman", null);
        var shellDefView = IntPtr.Zero;
        var shellDefViewParent = IntPtr.Zero;
        var workerWs = new List<IntPtr>();

        foreach (var topLevelWindow in EnumerateTopLevelWindows())
        {
            var className = GetClassName(topLevelWindow);
            if (string.Equals(className, "WorkerW", StringComparison.Ordinal))
            {
                workerWs.Add(topLevelWindow);
            }

            var candidate = NativeMethods.FindWindowEx(topLevelWindow, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (candidate != IntPtr.Zero)
            {
                shellDefView = candidate;
                shellDefViewParent = topLevelWindow;
            }
        }

        if (shellDefView == IntPtr.Zero && progman != IntPtr.Zero)
        {
            shellDefView = NativeMethods.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            shellDefViewParent = progman;
        }

        var wallpaperWorkerW = FindWallpaperWorkerW(shellDefViewParent, workerWs);

        return new ShellWindowDiagnostics(
            progman == IntPtr.Zero ? null : CreateWindowInfo(progman),
            shellDefView == IntPtr.Zero ? null : CreateWindowInfo(shellDefView),
            workerWs.Select(CreateWindowInfo).ToArray(),
            wallpaperWorkerW == IntPtr.Zero ? null : CreateWindowInfo(wallpaperWorkerW));
    }

    private static ShellSnapshot CreateShellSnapshot(ShellWindowDiagnostics diagnostics) =>
        ShellSnapshot.Create(
            diagnostics.Progman?.HandleHex,
            diagnostics.ShellDllDefView?.HandleHex,
            diagnostics.WallpaperWorkerW?.HandleHex,
            diagnostics.WorkerWs.Count);

    private HostWindowDiagnostics? CreateHostWindowDiagnostics(MainWindow window, DesktopHtmlConfig config)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        if (!_windowStates.TryGetValue(handle, out var state))
        {
            var bounds = ResolveTargetBounds(window.CurrentMonitor, config.Desktop.AvoidTaskbar);
            state = new PlacementState(
                FormatHandle(handle),
                window.MonitorId,
                window.Title,
                bounds,
                NormalizeMode(config.Desktop.PlacementMode),
                StageAPlacementMode,
                false,
                FormatHandle(NativeMethods.GetParent(handle)),
                null);
        }

        return new HostWindowDiagnostics(
            state.Handle,
            state.MonitorId,
            state.Title,
            state.TargetBounds,
            state.RequestedPlacementMode,
            state.AppliedPlacementMode,
            state.WorkerWAttached,
            state.ParentHandle,
            state.LastPlacementError);
    }

    private WorkerWAttachResult TryAttachToWorkerW(IntPtr hostHandle)
    {
        try
        {
            var progman = NativeMethods.FindWindow("Progman", null);
            if (progman == IntPtr.Zero)
            {
                return WorkerWAttachResult.Failed("Progman was not found.");
            }

            NativeMethods.SendMessageTimeout(
                progman,
                0x052C,
                IntPtr.Zero,
                IntPtr.Zero,
                SendMessageTimeoutFlags.Normal,
                1000,
                out _);

            var shellWindows = DiscoverShellWindows();
            var workerWHandle = ParseHandle(shellWindows.WallpaperWorkerW?.HandleHex);
            if (workerWHandle == IntPtr.Zero && shellWindows.WorkerWs.Count > 0)
            {
                workerWHandle = ParseHandle(shellWindows.WorkerWs.First().HandleHex);
            }

            if (workerWHandle == IntPtr.Zero)
            {
                return WorkerWAttachResult.Failed("WorkerW was not found after sending the Progman message.");
            }

            NativeMethods.SetParent(hostHandle, workerWHandle);
            if (NativeMethods.GetParent(hostHandle) != workerWHandle)
            {
                var error = Marshal.GetLastWin32Error();
                if (error != 0)
                {
                    return WorkerWAttachResult.Failed(new Win32Exception(error).Message);
                }

                return WorkerWAttachResult.Failed("SetParent did not attach the host window to WorkerW.");
            }

            return WorkerWAttachResult.Success(workerWHandle);
        }
        catch (Exception ex)
        {
            return WorkerWAttachResult.Failed(ex.Message);
        }
    }

    private static IntPtr FindWallpaperWorkerW(IntPtr shellDefViewParent, IReadOnlyCollection<IntPtr> workerWs)
    {
        if (shellDefViewParent != IntPtr.Zero)
        {
            var sibling = NativeMethods.FindWindowEx(IntPtr.Zero, shellDefViewParent, "WorkerW", null);
            if (sibling != IntPtr.Zero)
            {
                return sibling;
            }
        }

        foreach (var workerW in workerWs)
        {
            var shellDefView = NativeMethods.FindWindowEx(workerW, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellDefView == IntPtr.Zero)
            {
                return workerW;
            }
        }

        return workerWs.FirstOrDefault();
    }

    private static IReadOnlyList<IntPtr> EnumerateTopLevelWindows()
    {
        var windows = new List<IntPtr>();
        NativeMethods.EnumWindows((handle, _) =>
        {
            windows.Add(handle);
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static void ApplyAltTabStyle(IntPtr handle, bool showInAltTab)
    {
        var style = NativeMethods.GetWindowLongPtr(handle, GwlExStyle);
        var styleValue = style.ToInt64();
        styleValue = showInAltTab
            ? styleValue & ~WsExToolWindow
            : (styleValue | WsExToolWindow) & ~WsExAppWindow;
        NativeMethods.SetWindowLongPtr(handle, GwlExStyle, new IntPtr(styleValue));
        NativeMethods.SetWindowPos(
            handle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SetWindowPosFlags.NoMove
            | SetWindowPosFlags.NoSize
            | SetWindowPosFlags.NoZOrder
            | SetWindowPosFlags.NoActivate
            | SetWindowPosFlags.FrameChanged);
    }

    /// <summary>
    /// Positions the window via SetWindowPos because the target bounds are
    /// physical pixels; WPF's Left/Top/Width/Height are DIPs and would land
    /// wrong on any monitor whose scale factor is not 100%. Moving onto a
    /// monitor with a different DPI raises WM_DPICHANGED mid-move and WPF's
    /// default handling can replace the rectangle with the OS-suggested one,
    /// so the bounds are asserted a second time when they did not stick.
    /// </summary>
    private static void ApplyWindowBounds(IntPtr handle, DesktopRectangle targetBounds)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            NativeMethods.SetWindowPos(
                handle,
                IntPtr.Zero,
                targetBounds.Left,
                targetBounds.Top,
                targetBounds.Width,
                targetBounds.Height,
                SetWindowPosFlags.NoZOrder | SetWindowPosFlags.NoActivate);

            if (NativeMethods.GetWindowRect(handle, out var rect)
                && rect.Left == targetBounds.Left
                && rect.Top == targetBounds.Top
                && rect.Right == targetBounds.Right
                && rect.Bottom == targetBounds.Bottom)
            {
                break;
            }
        }
    }

    private static void ApplyStageAPlacement(IntPtr handle, DesktopRectangle targetBounds)
    {
        NativeMethods.SetWindowPos(
            handle,
            HwndBottom,
            targetBounds.Left,
            targetBounds.Top,
            targetBounds.Width,
            targetBounds.Height,
            SetWindowPosFlags.NoActivate | SetWindowPosFlags.NoOwnerZOrder | SetWindowPosFlags.ShowWindow);
    }

    private static void ApplyShowDesktopZOrder(IntPtr handle, DesktopRectangle targetBounds)
    {
        NativeMethods.SetWindowPos(
            handle,
            HwndTopmost,
            targetBounds.Left,
            targetBounds.Top,
            targetBounds.Width,
            targetBounds.Height,
            SetWindowPosFlags.NoActivate | SetWindowPosFlags.NoOwnerZOrder | SetWindowPosFlags.ShowWindow);
    }

    private static void TryApplyStageAFallback(IntPtr handle, DesktopRectangle targetBounds)
    {
        try
        {
            ApplyWindowBounds(handle, targetBounds);
            ApplyStageAPlacement(handle, targetBounds);
        }
        catch
        {
        }
    }

    private static DesktopRectangle ResolveTargetBounds(MonitorSnapshot? monitor, bool avoidTaskbar)
    {
        if (monitor is not null)
        {
            return avoidTaskbar ? monitor.WorkArea : monitor.Bounds;
        }

        var area = avoidTaskbar ? SystemParameters.WorkArea : new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
        return new DesktopRectangle(
            (int)Math.Round(area.Left),
            (int)Math.Round(area.Top),
            (int)Math.Round(area.Width),
            (int)Math.Round(area.Height));
    }

    private static string NormalizeMode(string? mode)
    {
        if (string.Equals(mode, WorkerWPlacementMode, StringComparison.OrdinalIgnoreCase))
        {
            return WorkerWPlacementMode;
        }

        return StageAPlacementMode;
    }

    private static string NormalizeFallbackMode(string? mode) => StageAPlacementMode;

    private static WindowInfo CreateWindowInfo(IntPtr handle)
    {
        var parent = NativeMethods.GetParent(handle);
        return new WindowInfo(
            handle.ToInt64(),
            FormatHandle(handle),
            GetClassName(handle),
            GetWindowTitle(handle),
            parent == IntPtr.Zero ? null : parent.ToInt64(),
            parent == IntPtr.Zero ? null : FormatHandle(parent));
    }

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

    private static string FormatHandle(IntPtr handle) => $"0x{handle.ToInt64():X}";

    private static IntPtr ParseHandle(string? handleHex)
    {
        if (string.IsNullOrWhiteSpace(handleHex))
        {
            return IntPtr.Zero;
        }

        var text = handleHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? handleHex[2..]
            : handleHex;
        return long.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var value)
            ? new IntPtr(value)
            : IntPtr.Zero;
    }

    private void LogPlacementApplied(MainWindow window, PlacementState state)
    {
        _ = (_logService?.InfoAsync("placement", "Desktop placement applied.", new
        {
            monitorId = window.MonitorId,
            state.RequestedPlacementMode,
            state.AppliedPlacementMode,
            state.TargetBounds,
            state.WorkerWAttached,
            state.LastPlacementError
        }) ?? Task.CompletedTask);
    }

    private void LogPlacementFallback(
        MainWindow window,
        string requestedMode,
        string fallbackMode,
        DesktopRectangle targetBounds,
        string? error)
    {
        _ = (_logService?.WarningAsync("placement", "Desktop placement fell back.", new
        {
            monitorId = window.MonitorId,
            requestedMode,
            fallbackMode,
            targetBounds,
            error
        }) ?? Task.CompletedTask);
    }

    private void LogPlacementFailure(
        MainWindow window,
        string requestedMode,
        string fallbackMode,
        DesktopRectangle targetBounds,
        Exception exception)
    {
        _ = (_logService?.ErrorAsync("placement", "Desktop placement failed.", new
        {
            monitorId = window.MonitorId,
            requestedMode,
            fallbackMode,
            targetBounds,
            error = exception.Message,
            type = exception.GetType().Name
        }) ?? Task.CompletedTask);
    }

    private sealed record PlacementState(
        string Handle,
        string? MonitorId,
        string Title,
        DesktopRectangle TargetBounds,
        string RequestedPlacementMode,
        string AppliedPlacementMode,
        bool WorkerWAttached,
        string? ParentHandle,
        string? LastPlacementError);

    private sealed record WorkerWAttachResult(bool Attached, IntPtr WorkerWHandle, string? ErrorMessage)
    {
        public static WorkerWAttachResult Success(IntPtr workerWHandle) => new(true, workerWHandle, null);

        public static WorkerWAttachResult Failed(string errorMessage) => new(false, IntPtr.Zero, errorMessage);
    }

    private static class NativeMethods
    {
        public delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr FindWindow(string className, string? windowName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string? windowName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetClassName(IntPtr handle, StringBuilder className, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetWindowText(IntPtr handle, StringBuilder text, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetWindowTextLength(IntPtr handle);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetParent(IntPtr handle);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        public static extern IntPtr GetWindowLongPtr64(IntPtr handle, int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        public static extern int GetWindowLong32(IntPtr handle, int index);

        public static IntPtr GetWindowLongPtr(IntPtr handle, int index) =>
            IntPtr.Size == 8 ? GetWindowLongPtr64(handle, index) : new IntPtr(GetWindowLong32(handle, index));

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        public static extern IntPtr SetWindowLongPtr64(IntPtr handle, int index, IntPtr value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        public static extern int SetWindowLong32(IntPtr handle, int index, int value);

        public static IntPtr SetWindowLongPtr(IntPtr handle, int index, IntPtr value) =>
            IntPtr.Size == 8 ? SetWindowLongPtr64(handle, index, value) : new IntPtr(SetWindowLong32(handle, index, value.ToInt32()));

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(
            IntPtr handle,
            IntPtr insertAfter,
            int x,
            int y,
            int cx,
            int cy,
            SetWindowPosFlags flags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr childHandle, IntPtr newParentHandle);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowRect(IntPtr handle, out WindowRect rect);

        [StructLayout(LayoutKind.Sequential)]
        public struct WindowRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessageTimeout(
            IntPtr handle,
            uint message,
            IntPtr wParam,
            IntPtr lParam,
            SendMessageTimeoutFlags flags,
            uint timeoutMilliseconds,
            out IntPtr result);
    }
}

public sealed record PlacementDiagnostics(
    string ConfiguredPlacementMode,
    string FallbackPlacementMode,
    bool AvoidTaskbar,
    bool ShowInAltTab,
    bool ShowInTaskbar,
    IReadOnlyList<MonitorSnapshot> Monitors,
    ShellWindowDiagnostics ShellWindows,
    IReadOnlyList<HostWindowDiagnostics> HostWindows,
    bool WorkerWAttachmentAvailable,
    ShellSnapshot ShellSnapshot,
    bool ShellReady,
    string ShellSignature,
    DateTimeOffset? LastPlacementReapplyUtc,
    string? LastPlacementReapplyReason);

public sealed record PlacementReapplyResult(
    bool Reapplied,
    string Reason,
    int WindowCount,
    DateTimeOffset TimestampUtc,
    ShellSnapshot ShellSnapshot);

public sealed record ShellWindowDiagnostics(
    WindowInfo? Progman,
    WindowInfo? ShellDllDefView,
    IReadOnlyList<WindowInfo> WorkerWs,
    WindowInfo? WallpaperWorkerW);

public sealed record WindowInfo(
    long Handle,
    string HandleHex,
    string ClassName,
    string Title,
    long? ParentHandle,
    string? ParentHandleHex);

public sealed record HostWindowDiagnostics(
    string Handle,
    string? MonitorId,
    string Title,
    DesktopRectangle TargetBounds,
    string RequestedPlacementMode,
    string AppliedPlacementMode,
    bool WorkerWAttached,
    string? ParentHandle,
    string? LastPlacementError);

public sealed record PlacementApplyResult(
    string AppliedPlacementMode,
    DesktopRectangle TargetBounds,
    bool WorkerWAttached,
    string? Error);

[Flags]
internal enum SetWindowPosFlags : uint
{
    NoSize = 0x0001,
    NoMove = 0x0002,
    NoZOrder = 0x0004,
    NoActivate = 0x0010,
    FrameChanged = 0x0020,
    ShowWindow = 0x0040,
    NoOwnerZOrder = 0x0200
}

[Flags]
internal enum SendMessageTimeoutFlags : uint
{
    Normal = 0x0000
}
