using System.Windows;
using System.Diagnostics;
using System.IO;
using DesktopHtml.Core;
using DesktopHtml.Core.Backups;
using DesktopHtml.Core.Configuration;

namespace DesktopHtml.App;

public interface IDesktopHostActions
{
    Task ReloadSkinAsync();
    Task ReloadMonitorSkinAsync(string monitorId);
    Task RefreshHostWindowsAsync();
    Task OpenSettingsAsync();
    Task OpenLogsAsync();
    Task SetSafeModeAsync(bool enabled);
    Task<PlacementDiagnostics> GetPlacementDiagnosticsAsync();
    Task<PlacementReapplyResult> ReapplyPlacementAsync(string reason);
    Task ExitAsync();
}

public sealed class WpfDesktopHostActions : IDesktopHostActions
{
    private readonly AppPaths _paths;
    private readonly DesktopHtmlConfig _config;
    private readonly ConfigService _configService;
    private readonly DesktopPlacementService _placementService;
    private readonly Action? _refreshHostWindows;
    private readonly List<MainWindow> _windows = new();
    private SettingsWindow? _settingsWindow;

    public WpfDesktopHostActions(
        AppPaths paths,
        DesktopHtmlConfig config,
        DesktopPlacementService placementService,
        ConfigService? configService = null,
        Action? refreshHostWindows = null)
    {
        _paths = paths;
        _config = config;
        _placementService = placementService;
        _configService = configService ?? new ConfigService(paths);
        _refreshHostWindows = refreshHostWindows;
    }

    public void RegisterWindow(MainWindow window)
    {
        if (!_windows.Contains(window))
        {
            _windows.Add(window);
        }
    }

    public void UnregisterWindow(MainWindow window)
    {
        _windows.Remove(window);
    }

    public Task ReloadSkinAsync()
    {
        return System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var reloads = _windows
                .Where(window => window.IsLoaded)
                .Select(window => window.ReloadSkinAsync())
                .ToArray();

            return Task.WhenAll(reloads);
        });
    }

    public Task ReloadMonitorSkinAsync(string monitorId)
    {
        return System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var window = _windows.FirstOrDefault(candidate =>
                string.Equals(candidate.MonitorId, monitorId, StringComparison.OrdinalIgnoreCase));

            if (window is null)
            {
                throw new InvalidOperationException($"No active desktop.html host window is assigned to monitor '{monitorId}'.");
            }

            return window.ReloadSkinAsync();
        });
    }

    public Task RefreshHostWindowsAsync()
    {
        return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            _refreshHostWindows?.Invoke();
        }).Task;
    }

    public Task OpenSettingsAsync()
    {
        return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (_settingsWindow is { IsVisible: true })
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsWindow(_paths, _config, this);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }).Task;
    }

    public Task OpenLogsAsync()
    {
        return System.Windows.Application.Current.Dispatcher.InvokeAsync(OpenLogsDirectory).Task;
    }

    public async Task SetSafeModeAsync(bool enabled)
    {
        if (File.Exists(_paths.ConfigFile))
        {
            await new BackupService(_paths, AppVersion.Current)
                .CreateConfigBackupAsync("safe-mode")
                .ConfigureAwait(false);
        }

        _config.App.SafeMode = enabled;
        await _configService.SaveAsync(_config).ConfigureAwait(false);
    }

    public Task<PlacementDiagnostics> GetPlacementDiagnosticsAsync()
    {
        return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            _placementService.BuildDiagnostics(_config, _windows)).Task;
    }

    public Task<PlacementReapplyResult> ReapplyPlacementAsync(string reason)
    {
        return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var windows = _windows.Where(window => window.IsLoaded).ToArray();
            foreach (var window in windows)
            {
                window.ReapplyDesktopPlacement();
            }

            return _placementService.RecordPlacementReapply(reason, windows.Length);
        }).Task;
    }

    public Task ExitAsync()
    {
        return System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
        {
            System.Windows.Application.Current.Shutdown(0);
        }).Task;
    }

    private Window? GetOwner() => _windows.FirstOrDefault(window => window.IsLoaded) ?? _windows.FirstOrDefault();

    private void OpenLogsDirectory()
    {
        Directory.CreateDirectory(_paths.LogsDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = _paths.LogsDirectory,
            UseShellExecute = true
        });
    }
}
