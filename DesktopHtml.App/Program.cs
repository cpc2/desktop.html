using System.Runtime.InteropServices;
using System.Windows;
using DesktopHtml.Core;
using DesktopHtml.Core.Configuration;
using DesktopHtml.Core.Ipc;
using DesktopHtml.Core.Logging;
using DesktopHtml.Core.Skins;
using Velopack;

namespace DesktopHtml.App;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Must precede Velopack, WPF, WinForms, monitor enumeration, and any
        // HWND creation so Windows never virtualizes monitor coordinates.
        DpiAwarenessBootstrap.EnablePerMonitorV2();

        // Must run first: handles Velopack install/update/uninstall hooks
        // (Start Menu shortcuts, version migration) and exits early during them.
        VelopackApp.Build().Run();

        if (args.Length > 0)
        {
            // Built as WinExe so no console window opens at startup; CLI mode
            // reattaches to the invoking terminal so output remains visible.
            AttachConsole(AttachParentProcess);
        }

        var paths = AppPaths.CreateDefault();
        paths.EnsureCreated();

        var configService = new ConfigService(paths);
        var logService = new LogService(paths);
        logService.InfoAsync("app", "desktop.html starting.", new
        {
            AppVersion.Current,
            args = args.Length,
            dpiAwareness = DpiAwarenessBootstrap.DescribeCurrentThread()
        }).GetAwaiter().GetResult();
        new SampleSkinWriter(paths).EnsureInstalledAsync().GetAwaiter().GetResult();

        if (args.Length > 0)
        {
            return CliRunner.RunAsync(args, paths, configService).GetAwaiter().GetResult();
        }

        // Only one desktop host may run at a time; a second launch just surfaces
        // the settings window of the running instance.
        using var singleInstanceMutex = new Mutex(initiallyOwned: true, @"Local\desktop-html-host", out var isFirstInstance);
        if (!isFirstInstance)
        {
            logService.InfoAsync("app", "Another desktop.html instance is already running; opening its settings and exiting.")
                .GetAwaiter().GetResult();
            try
            {
                new RuntimeIpcClient().SendAsync("openSettings").GetAwaiter().GetResult();
            }
            catch
            {
                // The running instance is still starting up or shutting down; nothing else to do.
            }

            return 0;
        }

        var app = new App();
        return app.Run();
    }

    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);
}
