using System.Windows;
using Microsoft.Win32;
using DesktopHtml.Core;
using DesktopHtml.Core.Configuration;
using DesktopHtml.Core.Logging;
using DesktopHtml.Core.Monitors;
using DesktopHtml.Core.Placement;
using DesktopHtml.Core.Skins;
using DesktopHtml.Core.Startup;
using System.Windows.Threading;
using System.Windows.Interop;

namespace DesktopHtml.App;

public partial class App : System.Windows.Application
{
    private readonly List<MainWindow> _windows = new();
    private AppPaths? _paths;
    private DesktopHtmlConfig? _config;
    private WpfDesktopHostActions? _hostActions;
    private DesktopPlacementService? _placementService;
    private LogService? _logService;
    private RuntimeCommandServer? _commandServer;
    private TrayService? _trayService;
    private DispatcherTimer? _placementRecoveryTimer;
    private DesktopRevealService? _desktopRevealService;
    private ShellSnapshot? _lastShellSnapshot;
    private bool _placementRecoveryTickRunning;
    private bool _showDesktopPlacementActive;
    private bool _desktopRevealHandlerRunning;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var paths = AppPaths.CreateDefault();
        paths.EnsureCreated();
        _paths = paths;

        var configService = new ConfigService(paths);
        var config = configService.LoadOrCreateAsync().GetAwaiter().GetResult();
        _config = config;
        _logService = new LogService(paths);
        if (config.App.StartWithWindows)
        {
            new StartupService().Enable(Environment.ProcessPath ?? "desktop-html.exe");
        }

        _placementService = new DesktopPlacementService(_logService);
        _lastShellSnapshot = _placementService.WaitForShellReadyAsync(
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(250))
            .GetAwaiter()
            .GetResult();

        var shellReadyLogLevel = _lastShellSnapshot.IsReady ? "info" : "warning";
        _ = _logService.WriteAsync(shellReadyLogLevel, "placement", "Explorer shell readiness checked before host creation.", new
            {
                shellReady = _lastShellSnapshot.IsReady,
                shellSignature = _lastShellSnapshot.Signature
            });

        var hostActions = new WpfDesktopHostActions(paths, config, _placementService, configService, RefreshHostWindows);
        _hostActions = hostActions;
        _ = _logService.InfoAsync("app", "Desktop host actions created.");
        _commandServer = new RuntimeCommandServer(hostActions, _logService);
        _commandServer.Start();
        _ = _logService.InfoAsync("app", "Runtime command server start requested.");
        _trayService = new TrayService(hostActions, config, _logService);
        hostActions.NotificationHandler = _trayService.ShowNotification;
        _ = _logService.InfoAsync("app", "Tray service created.");
        DesktopVisibilityService.Initialize();
        HotkeyService.Initialize();
        _ = _logService.InfoAsync("app", "Desktop visibility and hotkey services initialized.");
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        _ = _logService.InfoAsync("app", "Display settings handler registered.");

        if (config.App.SafeMode)
        {
            _ = _logService.InfoAsync("app", "Safe mode active; opening settings without skin hosts.");
            hostActions.OpenSettingsAsync().GetAwaiter().GetResult();
            return;
        }

        _ = _logService.InfoAsync("app", "Refreshing host windows.");
        RefreshHostWindows();
        _ = _logService.InfoAsync("app", "Host windows refreshed.", new { windowCount = _windows.Count });
        if (_windows.Count == 0)
        {
            Shutdown(1);
            return;
        }

        MainWindow = _windows[0];
        StartPlacementRecoveryMonitor();
        StartDesktopRevealMonitor();
        _ = Task.Run(CheckForAppUpdatesAsync);
    }

    private async Task CheckForAppUpdatesAsync()
    {
        try
        {
            var updater = new UpdateService();
            if (!updater.IsInstalled)
            {
                await (_logService?.InfoAsync("update", "Not an installed build; skipping update check.") ?? Task.CompletedTask);
                return;
            }

            var update = await updater.CheckForUpdatesAsync();
            if (update is null)
            {
                await (_logService?.InfoAsync("update", "No update available.", new { version = AppVersion.Current }) ?? Task.CompletedTask);
                return;
            }

            var targetVersion = update.TargetFullRelease.Version.ToString();
            await (_logService?.InfoAsync("update", "Update found; downloading.", new
            {
                current = AppVersion.Current,
                target = targetVersion
            }) ?? Task.CompletedTask);

            await updater.DownloadUpdatesAsync(update);
            updater.StageUpdateForRestart(update);
            await (_logService?.InfoAsync("update", "Update downloaded; restarting to apply.", new { target = targetVersion }) ?? Task.CompletedTask);
            await Dispatcher.InvokeAsync(() => Shutdown(0));
        }
        catch (Exception ex)
        {
            await (_logService?.WriteAsync("warning", "update", "Update check failed.", new
            {
                error = ex.Message,
                type = ex.GetType().Name
            }) ?? Task.CompletedTask);
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        _ = Dispatcher.InvokeAsync(async () =>
        {
            if (_logService is not null)
            {
                await _logService.InfoAsync("monitor", "Display settings changed; refreshing host windows.");
            }
            RefreshHostWindows();
            if (_hostActions is not null)
            {
                await _hostActions.ReapplyPlacementAsync("displaySettingsChanged");
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        StopDesktopRevealMonitor();
        StopPlacementRecoveryMonitor();
        _trayService?.Dispose();
        if (_commandServer is not null)
        {
            _commandServer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        base.OnExit(e);
    }

    private void RefreshHostWindows()
    {
        if (_paths is null || _config is null || _hostActions is null || _placementService is null)
        {
            return;
        }

        if (_config.App.SafeMode)
        {
            foreach (var existingWindow in _windows.ToArray())
            {
                _windows.Remove(existingWindow);
                existingWindow.HostMinimized -= OnHostWindowMinimized;
                existingWindow.Close();
            }

            MainWindow = null;
            return;
        }

        var desiredWindows = CreateHostWindowSpecs(_paths, _config).ToArray();

        foreach (var existingWindow in _windows.ToArray())
        {
            var replacement = desiredWindows.FirstOrDefault(spec =>
                string.Equals(spec.Monitor?.Id, existingWindow.MonitorId, StringComparison.OrdinalIgnoreCase)
                && spec.Monitor is not null);

            if (replacement is null)
            {
                _windows.Remove(existingWindow);
                existingWindow.HostMinimized -= OnHostWindowMinimized;
                existingWindow.Close();
                continue;
            }

            if (!string.Equals(replacement.Skin.Manifest.Id, existingWindow.SkinId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(replacement.Skin.Entry, existingWindow.SkinEntry, StringComparison.OrdinalIgnoreCase))
            {
                _windows.Remove(existingWindow);
                existingWindow.HostMinimized -= OnHostWindowMinimized;
                existingWindow.Close();
                continue;
            }

            existingWindow.ApplyMonitorSnapshot(replacement.Monitor);
        }

        foreach (var spec in desiredWindows)
        {
            if (spec.Monitor is not null
                && _windows.Any(window => string.Equals(window.MonitorId, spec.Monitor.Id, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var window = new MainWindow(_paths, _config, spec.Skin, spec.Monitor, _hostActions, _placementService);
            window.HostMinimized += OnHostWindowMinimized;
            _windows.Add(window);
            window.Show();
        }

        MainWindow = _windows.FirstOrDefault();
    }

    private void StartPlacementRecoveryMonitor()
    {
        if (_placementRecoveryTimer is not null)
        {
            return;
        }

        _placementRecoveryTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _placementRecoveryTimer.Tick += OnPlacementRecoveryTimerTick;
        _placementRecoveryTimer.Start();
    }

    private void StopPlacementRecoveryMonitor()
    {
        if (_placementRecoveryTimer is null)
        {
            return;
        }

        _placementRecoveryTimer.Tick -= OnPlacementRecoveryTimerTick;
        _placementRecoveryTimer.Stop();
        _placementRecoveryTimer = null;
    }

    private void StartDesktopRevealMonitor()
    {
        if (_desktopRevealService is not null)
        {
            return;
        }

        _desktopRevealService = new DesktopRevealService(Dispatcher, _logService);
        _desktopRevealService.ForegroundChanged += OnDesktopRevealForegroundChanged;
        _desktopRevealService.Start();
    }

    private void StopDesktopRevealMonitor()
    {
        if (_desktopRevealService is null)
        {
            return;
        }

        _desktopRevealService.ForegroundChanged -= OnDesktopRevealForegroundChanged;
        _desktopRevealService.Dispose();
        _desktopRevealService = null;
    }

    private async void OnDesktopRevealForegroundChanged(object? sender, DesktopRevealChangedEventArgs e)
    {
        if (_desktopRevealHandlerRunning || _placementService is null || _hostActions is null || _config is null)
        {
            return;
        }

        _desktopRevealHandlerRunning = true;
        try
        {
            if (e.IsDesktopForeground)
            {
                await ApplyShowDesktopPlacementAsync("Show Desktop foreground detected; elevated desktop host windows.", new
                {
                    e.ForegroundClassName,
                    e.ForegroundTitle
                });
                return;
            }

            if (!_showDesktopPlacementActive || IsDesktopHtmlHostWindow(e.ForegroundHandle))
            {
                return;
            }

            _showDesktopPlacementActive = false;
            await (_logService?.InfoAsync("placement", "Show Desktop foreground ended; restoring desktop host placement.", new
            {
                e.ForegroundClassName,
                e.ForegroundTitle
            }) ?? Task.CompletedTask);
            await _hostActions.ReapplyPlacementAsync("desktopRevealEnded");
        }
        catch (Exception ex)
        {
            await (_logService?.ErrorAsync("placement", "Desktop reveal placement handler failed.", new
            {
                error = ex.Message,
                type = ex.GetType().Name
            }) ?? Task.CompletedTask);
        }
        finally
        {
            _desktopRevealHandlerRunning = false;
        }
    }

    private async void OnHostWindowMinimized(object? sender, EventArgs e)
    {
        if (_desktopRevealHandlerRunning || _placementService is null || _config is null)
        {
            return;
        }

        _desktopRevealHandlerRunning = true;
        try
        {
            await ApplyShowDesktopPlacementAsync("Host window minimized; elevated desktop host windows for Show Desktop.");
        }
        catch (Exception ex)
        {
            await (_logService?.ErrorAsync("placement", "Host minimize placement handler failed.", new
            {
                error = ex.Message,
                type = ex.GetType().Name
            }) ?? Task.CompletedTask);
        }
        finally
        {
            _desktopRevealHandlerRunning = false;
        }
    }

    private async Task ApplyShowDesktopPlacementAsync(string message, object? details = null)
    {
        if (_placementService is null || _config is null)
        {
            return;
        }

        if (_showDesktopPlacementActive)
        {
            return;
        }

        _showDesktopPlacementActive = true;
        foreach (var window in _windows.Where(window => window.IsLoaded))
        {
            window.RefreshMonitorSnapshot();
            _placementService.ApplyShowDesktopPlacement(window, _config);
        }

        await (_logService?.InfoAsync("placement", message, details) ?? Task.CompletedTask);
    }

    private bool IsDesktopHtmlHostWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        return _windows.Any(window =>
            window.IsLoaded
            && new WindowInteropHelper(window).Handle == handle);
    }

    private async void OnPlacementRecoveryTimerTick(object? sender, EventArgs e)
    {
        if (_placementRecoveryTickRunning || _placementService is null || _hostActions is null)
        {
            return;
        }

        _placementRecoveryTickRunning = true;
        try
        {
            var currentSnapshot = _placementService.CaptureShellSnapshot();
            if (_lastShellSnapshot is null)
            {
                _lastShellSnapshot = currentSnapshot;
                return;
            }

            if (!currentSnapshot.HasChanged(_lastShellSnapshot))
            {
                return;
            }

            var previousSnapshot = _lastShellSnapshot;
            _lastShellSnapshot = currentSnapshot;
            await (_logService?.InfoAsync("placement", "Explorer shell hierarchy changed; reapplying desktop placement.", new
            {
                previousSignature = previousSnapshot?.Signature,
                currentSignature = currentSnapshot.Signature,
                shellReady = currentSnapshot.IsReady
            }) ?? Task.CompletedTask);

            await _hostActions.ReapplyPlacementAsync("shellHierarchyChanged");
        }
        catch (Exception ex)
        {
            await (_logService?.ErrorAsync("placement", "Placement recovery monitor failed.", new
            {
                error = ex.Message,
                type = ex.GetType().Name
            }) ?? Task.CompletedTask);
        }
        finally
        {
            _placementRecoveryTickRunning = false;
        }
    }

    private static IEnumerable<HostWindowSpec> CreateHostWindowSpecs(
        AppPaths paths,
        DesktopHtmlConfig config)
    {
        var skinStore = new SkinStore(paths);
        var monitors = new MonitorService().GetMonitors();
        var usePerMonitor = string.Equals(config.Skins.ActiveMode, "per-monitor", StringComparison.OrdinalIgnoreCase)
            && monitors.Count > 0;
        var useSpanning = string.Equals(config.Skins.ActiveMode, "spanning", StringComparison.OrdinalIgnoreCase)
            && monitors.Count > 0;

        if (useSpanning)
        {
            var spanMonitors = SelectSpanMonitors(monitors, config.Skins.Spanning.Monitors);
            var skin = skinStore.ResolveSpanningAsync(config).GetAwaiter().GetResult();
            yield return new HostWindowSpec(skin, CreateSpanMonitor(spanMonitors));
            yield break;
        }

        if (!usePerMonitor)
        {
            var activeSkin = skinStore.ResolveActiveSkinAsync(config).GetAwaiter().GetResult();
            var primaryMonitor = monitors.FirstOrDefault(monitor => monitor.IsPrimary) ?? monitors.FirstOrDefault();
            yield return new HostWindowSpec(activeSkin, primaryMonitor);
            yield break;
        }

        foreach (var monitor in monitors)
        {
            var skin = skinStore.ResolveForMonitorAsync(config, monitor.Id).GetAwaiter().GetResult();
            yield return new HostWindowSpec(skin, monitor);
        }
    }

    private static IReadOnlyList<MonitorSnapshot> SelectSpanMonitors(
        IReadOnlyList<MonitorSnapshot> monitors,
        IReadOnlyCollection<string> selectedMonitorIds)
    {
        if (selectedMonitorIds.Count == 0)
        {
            return monitors;
        }

        var selected = monitors
            .Where(monitor => selectedMonitorIds.Contains(monitor.Id, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        return selected.Length == 0 ? monitors : selected;
    }

    private static MonitorSnapshot CreateSpanMonitor(IReadOnlyList<MonitorSnapshot> monitors)
    {
        var bounds = MonitorService.GetVirtualBounds(monitors);
        var workArea = MonitorService.GetVirtualBounds(monitors, useWorkArea: true);
        return new MonitorSnapshot("span", "span", bounds, workArea, monitors.Any(monitor => monitor.IsPrimary), 1.0);
    }

    private sealed record HostWindowSpec(ResolvedSkin Skin, MonitorSnapshot? Monitor);
}
