using System.Runtime.InteropServices;

namespace DesktopHtml.App;

/// <summary>
/// Establishes the coordinate contract used by the native monitor and window
/// placement code: all rectangles are physical pixels and each WPF/WebView2
/// host follows the DPI of the monitor it occupies.
/// </summary>
internal static class DpiAwarenessBootstrap
{
    private static readonly IntPtr PerMonitorAwareV2 = new(-4);

    public static void EnablePerMonitorV2()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // The application manifest normally establishes this before Main.
        // Calling the API as well protects unpackaged/debug and updater launch
        // paths where a generated host executable can lose the manifest.
        if (NativeMethods.AreDpiAwarenessContextsEqual(
                NativeMethods.GetThreadDpiAwarenessContext(),
                PerMonitorAwareV2))
        {
            return;
        }

        _ = NativeMethods.SetProcessDpiAwarenessContext(PerMonitorAwareV2);
    }

    public static string DescribeCurrentThread()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "unsupported";
        }

        var context = NativeMethods.GetThreadDpiAwarenessContext();
        if (NativeMethods.AreDpiAwarenessContextsEqual(context, PerMonitorAwareV2))
        {
            return "per-monitor-v2";
        }

        return NativeMethods.GetAwarenessFromDpiAwarenessContext(context) switch
        {
            0 => "unaware",
            1 => "system",
            2 => "per-monitor",
            _ => "unknown"
        };
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        public static extern IntPtr GetThreadDpiAwarenessContext();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AreDpiAwarenessContextsEqual(IntPtr first, IntPtr second);

        [DllImport("user32.dll")]
        public static extern int GetAwarenessFromDpiAwarenessContext(IntPtr value);
    }
}
