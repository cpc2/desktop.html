using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using DesktopHtml.Core;
using DesktopHtml.Core.Backups;
using DesktopHtml.Core.Bridge;
using DesktopHtml.Core.Configuration;
using DesktopHtml.Core.Execution;
using DesktopHtml.Core.FileSystem;
using DesktopHtml.Core.Logging;
using DesktopHtml.Core.Monitors;
using DesktopHtml.Core.Network;
using DesktopHtml.Core.Skins;
using DesktopHtml.Core.Startup;
using DesktopHtml.Core.Storage;
using DesktopHtml.Core.SystemInfo;
using DesktopHtml.Core.Terminal;

namespace DesktopHtml.App;

public sealed class DesktopBridgeDispatcher : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppPaths _paths;
    private readonly DesktopHtmlConfig _config;
    private readonly ResolvedSkin _activeSkin;
    private readonly IDesktopHostActions _hostActions;
    private readonly Func<MonitorSnapshot?>? _currentMonitorProvider;
    private readonly MonitorService _monitorService = new();
    private readonly FileSystemService _fileSystemService = new();
    private readonly SkinStorageService _skinStorageService;
    private readonly ConfigService _configService;
    private readonly BackupService _backupService;
    private readonly LogService _logService;
    private BridgeError? _lastError;

    private readonly TerminalSessionService _terminalService;
    private readonly HttpFetchService _httpFetchService = new();
    private readonly FileSystemWatchService _watchService = new();
    private readonly SystemStatsService _statsService = new();
    private readonly MediaService _mediaService = new();
    private readonly List<int> _hotkeyIds = new();
    private System.Threading.Timer? _statsTimer;
    private bool? _lastDesktopVisible;
    private bool _disposed;

    // High-frequency methods (every keystroke / every layout change) whose
    // successes must not each write a log line to disk.
    private static readonly HashSet<string> QuietMethods = new(StringComparer.Ordinal)
    {
        "terminal.write",
        "terminal.resize",
        "getIcon"
    };

    public event Action<string>? OnPostMessage;

    public DesktopBridgeDispatcher(
        AppPaths paths,
        DesktopHtmlConfig config,
        ResolvedSkin activeSkin,
        IDesktopHostActions hostActions,
        Func<MonitorSnapshot?>? currentMonitorProvider = null)
    {
        _paths = paths;
        _config = config;
        _activeSkin = activeSkin;
        _hostActions = hostActions;
        _currentMonitorProvider = currentMonitorProvider;
        _backupService = new BackupService(paths, AppVersion.Current);
        _skinStorageService = new SkinStorageService(paths, _backupService);
        _configService = new ConfigService(paths);
        _logService = new LogService(paths);
        _terminalService = new TerminalSessionService(_logService);
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        if (DesktopVisibilityService.Instance is { } visibility && _currentMonitorProvider is not null)
        {
            visibility.Changed += OnDesktopVisibilityChanged;
        }
    }

    private void OnDesktopVisibilityChanged()
    {
        var monitor = _currentMonitorProvider?.Invoke();
        var service = DesktopVisibilityService.Instance;
        if (monitor is null || service is null)
        {
            return;
        }

        var visible = !service.IsMonitorOccluded(monitor.Bounds);
        if (_lastDesktopVisible == visible)
        {
            return;
        }

        _lastDesktopVisible = visible;
        EmitEvent(new { type = "desktopVisibilityChanged", visible });
    }

    /// <summary>
    /// Tears down page-scoped bridge state (terminal sessions, file watchers,
    /// stats subscriptions). Called when the page navigates away, e.g. on skin
    /// reload, so resources do not leak across page lifetimes.
    /// </summary>
    public void ResetPageState()
    {
        _terminalService.KillAll();
        _watchService.UnwatchAll();
        StopStatsTimer();
        _mediaService.Unsubscribe();
        foreach (var hotkeyId in _hotkeyIds.ToArray())
        {
            HotkeyService.Instance?.Unregister(hotkeyId);
        }

        _hotkeyIds.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        if (DesktopVisibilityService.Instance is { } visibility)
        {
            visibility.Changed -= OnDesktopVisibilityChanged;
        }

        ResetPageState();
        _terminalService.Dispose();
        _watchService.Dispose();
        _mediaService.Dispose();
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.StatusChange)
        {
            return;
        }

        var battery = _statsService.GetStats().Battery;
        EmitEvent(new
        {
            type = "powerStatusChanged",
            onBattery = !battery.OnAc,
            batteryPercent = battery.Percent
        });
    }

    private void EmitEvent(object payload) =>
        OnPostMessage?.Invoke(JsonSerializer.Serialize(payload, JsonOptions));

    private void StopStatsTimer()
    {
        _statsTimer?.Dispose();
        _statsTimer = null;
    }

    public async Task<string> DispatchAsync(string requestJson)
    {
        BridgeRequest? request = null;

        try
        {
            request = JsonSerializer.Deserialize<BridgeRequest>(requestJson, JsonOptions);
            if (request is null || string.IsNullOrWhiteSpace(request.Id))
            {
                return Serialize(new BridgeResponse("", false, Error: new BridgeError("INVALID_REQUEST", "Bridge request is missing an id.")));
            }

            var stopwatch = Stopwatch.StartNew();
            var result = await ExecuteAsync(request).ConfigureAwait(false);
            stopwatch.Stop();
            if (!QuietMethods.Contains(request.Method))
            {
                await _logService.InfoAsync("bridge", "Bridge call succeeded.", new
                {
                    request.Method,
                    elapsedMs = stopwatch.ElapsedMilliseconds
                }).ConfigureAwait(false);
            }

            return Serialize(new BridgeResponse(request.Id, true, result));
        }
        catch (Exception ex)
        {
            _lastError = new BridgeError("NATIVE_ERROR", ex.Message, new { type = ex.GetType().Name });
            if (request is not null)
            {
                await _logService.ErrorAsync("bridge", "Bridge call failed.", new
                {
                    request.Method,
                    error = ex.Message,
                    type = ex.GetType().Name
                }).ConfigureAwait(false);
            }

            return Serialize(new BridgeResponse(
                request?.Id ?? "",
                false,
                Error: _lastError));
        }
    }

    private Task<object?> ExecuteAsync(BridgeRequest request)
    {
        return request.Method switch
        {
            "getRuntimeInfo" => Task.FromResult<object?>(new RuntimeInfo(
                AppVersion.Current,
                _paths.Root,
                _paths.ConfigFile,
                _activeSkin.Manifest.Id,
                _activeSkin.Directory,
                _config.Desktop.PlacementMode,
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory))),
            "getVersion" => Task.FromResult<object?>(AppVersion.Current),
            "getCapabilities" => Task.FromResult<object?>(GetCapabilities()),
            "getConfig" => Task.FromResult<object?>(_config),
            "getInstalledSkins" => GetInstalledSkinsAsync(),
            "getInstalledSkinDetails" => GetInstalledSkinDetailsAsync(),
            "setConfigPatch" => SetConfigPatchAsync(request.Params),
            "getBackups" => GetBackupsAsync(),
            "createBackup" => CreateBackupAsync(request.Params),
            "restoreBackup" => RestoreBackupAsync(request.Params),
            "installSkinFromFolder" => InstallSkinFromFolderAsync(request.Params),
            "activateSkin" => ActivateSkinAsync(request.Params),
            "assignMonitorSkin" => AssignMonitorSkinAsync(request.Params),
            "clearMonitorSkin" => ClearMonitorSkinAsync(request.Params),
            "configureSpanning" => ConfigureSpanningAsync(request.Params),
            "setMonitorMode" => SetMonitorModeAsync(request.Params),
            "setStartupEnabled" => SetStartupEnabledAsync(request.Params),
            "getStartupStatus" => Task.FromResult<object?>(new { enabled = new StartupService().IsEnabled() }),
            "openSkinFolder" => OpenSkinFolderAsync(request.Params),
            "reload" => HostActionAsync(_hostActions.ReloadSkinAsync),
            "reloadSkin" => HostActionAsync(_hostActions.ReloadSkinAsync),
            "openSettings" => HostActionAsync(_hostActions.OpenSettingsAsync),
            "openLogs" => HostActionAsync(_hostActions.OpenLogsAsync),
            "exit" => HostActionAsync(_hostActions.ExitAsync),
            "getMonitors" => Task.FromResult<object?>(_monitorService.GetMonitors()),
            "getCurrentMonitor" => Task.FromResult<object?>(GetCurrentMonitor()),
            "getVirtualDesktopBounds" => Task.FromResult<object?>(_monitorService.GetVirtualDesktopBounds()),
            "getWorkArea" => Task.FromResult<object?>(GetCurrentMonitor()?.WorkArea),
            "log" => LogAsync(request.Params),
            "openPath" => ShellExecutePathAsync(GetRequiredString(request.Params, "path")),
            "openFile" => ShellExecutePathAsync(GetRequiredString(request.Params, "path")),
            "openFolder" => ShellExecutePathAsync(GetRequiredString(request.Params, "path")),
            "openUrl" => ShellExecutePathAsync(GetRequiredString(request.Params, "url")),
            "openShortcut" => ShellExecutePathAsync(GetRequiredString(request.Params, "path")),
            "revealInExplorer" => RevealInExplorerAsync(GetRequiredString(request.Params, "path")),
            "openWindowsSettings" => ShellExecutePathAsync(GetRequiredString(request.Params, "uri")),
            "shellExecute" => ShellExecuteAsync(Parse<ShellExecutionOptions>(request.Params)),
            "run" => RunAsync(Parse<RunOptions>(request.Params)),
            "runCommandLine" => RunCommandLineAsync(Parse<CommandLineOptions>(request.Params)),
            "runPowerShell" => RunPowerShellAsync(Parse<PowerShellOptions>(request.Params)),
            "runBatch" => RunBatchAsync(Parse<BatchOptions>(request.Params)),
            "exists" => Task.FromResult<object?>(_fileSystemService.Exists(GetRequiredString(request.Params, "path"))),
            "readText" => ReadTextAsync(request.Params),
            "writeText" => WriteTextAsync(request.Params),
            "writeFileBase64" => WriteFileBase64Async(request.Params),
            "readJson" => ReadJsonAsync(request.Params),
            "writeJson" => WriteJsonAsync(request.Params),
            "listDirectory" => Task.FromResult<object?>(_fileSystemService.ListDirectory(GetRequiredString(request.Params, "path"))),
            "listDesktopItems" => Task.FromResult<object?>(ListDesktopItems()),
            "createDirectory" => CreateDirectoryAsync(request.Params),
            "deletePath" => DeletePathAsync(request.Params),
            "movePath" => MovePathAsync(request.Params),
            "copyPath" => CopyPathAsync(request.Params),
            "storage.get" => StorageGetAsync(request.Params),
            "storage.set" => StorageSetAsync(request.Params),
            "storage.remove" => StorageRemoveAsync(request.Params),
            "storage.clear" => StorageClearAsync(),
            "storage.getAll" => StorageGetAllAsync(),
            "clipboard.readText" => Task.FromResult<object?>(System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : ""),
            "clipboard.writeText" => ClipboardWriteTextAsync(request.Params),
            "clipboard.saveToDirectory" => ClipboardSaveToDirectoryAsync(request.Params),
            "getLogs" => Task.FromResult<object?>(_logService.ReadLines(GetOptionalInt32(request.Params, "maxLines") ?? 200)),
            "getLastError" => Task.FromResult<object?>(_lastError),
            "httpFetch" => HttpFetchAsync(request.Params),
            "terminal.start" => StartTerminalAsync(request.Params),
            "terminal.write" => WriteTerminalAsync(request.Params),
            "terminal.resize" => ResizeTerminalAsync(request.Params),
            "getIcon" => GetIconAsync(request.Params),
            "watch" => WatchAsync(request.Params),
            "unwatch" => UnwatchAsync(request.Params),
            "getSystemStats" => Task.FromResult<object?>(_statsService.GetStats()),
            "subscribeSystemStats" => SubscribeSystemStatsAsync(request.Params),
            "unsubscribeSystemStats" => UnsubscribeSystemStatsAsync(),
            "notify" => NotifyAsync(request.Params),
            "media.getNowPlaying" => MediaGetNowPlayingAsync(request.Params),
            "media.control" => MediaControlAsync(request.Params),
            "media.subscribe" => MediaSubscribeAsync(),
            "media.unsubscribe" => MediaUnsubscribeAsync(),
            "registerHotkey" => RegisterHotkeyAsync(request.Params),
            "unregisterHotkey" => UnregisterHotkeyAsync(request.Params),
            _ => throw new InvalidOperationException($"Unknown bridge method '{request.Method}'.")
        };
    }

    private static string[] GetCapabilities() =>
    [
        "getRuntimeInfo",
        "getVersion",
        "getCapabilities",
        "getConfig",
        "getInstalledSkins",
        "getInstalledSkinDetails",
        "setConfigPatch",
        "getBackups",
        "createBackup",
        "restoreBackup",
        "installSkinFromFolder",
        "activateSkin",
        "assignMonitorSkin",
        "clearMonitorSkin",
        "configureSpanning",
        "setMonitorMode",
        "setStartupEnabled",
        "getStartupStatus",
        "openSkinFolder",
        "reload",
        "reloadSkin",
        "openSettings",
        "openLogs",
        "exit",
        "getMonitors",
        "getCurrentMonitor",
        "getVirtualDesktopBounds",
        "getWorkArea",
        "log",
        "openPath",
        "openFile",
        "openFolder",
        "openUrl",
        "openShortcut",
        "revealInExplorer",
        "openWindowsSettings",
        "shellExecute",
        "run",
        "runCommandLine",
        "runPowerShell",
        "runBatch",
        "exists",
        "readText",
        "writeText",
        "readJson",
        "writeJson",
        "listDirectory",
        "listDesktopItems",
        "createDirectory",
        "deletePath",
        "movePath",
        "copyPath",
        "storage.get",
        "storage.set",
        "storage.remove",
        "storage.clear",
        "storage.getAll",
        "clipboard.readText",
        "clipboard.writeText",
        "clipboard.saveToDirectory",
        "writeFileBase64",
        "getLogs",
        "getLastError",
        "httpFetch",
        "terminal.start",
        "terminal.write",
        "terminal.resize",
        "getIcon",
        "watch",
        "unwatch",
        "getSystemStats",
        "subscribeSystemStats",
        "unsubscribeSystemStats",
        "notify",
        "media.getNowPlaying",
        "media.control",
        "media.subscribe",
        "media.unsubscribe",
        "registerHotkey",
        "unregisterHotkey"
    ];

    private object ListDesktopItems()
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var publicDesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        var items = _fileSystemService.ListDesktopItems(desktopPath, publicDesktopPath);

        return new
        {
            desktopPath,
            publicDesktopPath,
            items
        };
    }

    private MonitorSnapshot? GetCurrentMonitor()
    {
        var currentMonitor = _currentMonitorProvider?.Invoke();
        if (currentMonitor is not null)
        {
            return currentMonitor;
        }

        var monitors = _monitorService.GetMonitors();
        return monitors.FirstOrDefault(monitor => monitor.IsPrimary) ?? monitors.FirstOrDefault();
    }

    private async Task<object?> LogAsync(JsonElement parameters)
    {
        var level = GetOptionalString(parameters, "level") ?? "info";
        var message = GetOptionalString(parameters, "message") ?? "";
        await _logService.WriteAsync(level, "skin", message).ConfigureAwait(false);
        return null;
    }

    private async Task<object?> SetConfigPatchAsync(JsonElement parameters)
    {
        var patch = parameters.TryGetProperty("patch", out var patchElement)
            ? patchElement.Deserialize<JsonObject>(JsonOptions)
            : null;

        if (patch is null)
        {
            throw new InvalidOperationException("setConfigPatch requires a patch object.");
        }

        var shouldRefreshHosts = patch.ContainsKey("skins")
            || (patch.TryGetPropertyValue("app", out var appPatch)
                && appPatch is JsonObject appPatchObject
                && appPatchObject.ContainsKey("safeMode"));
        var patched = ConfigPatchService.ApplyPatch(_config, patch);
        await SaveConfigWithBackupAsync(patched, "bridge-config-patch").ConfigureAwait(false);
        CopyConfig(patched, _config);
        if (shouldRefreshHosts)
        {
            await _hostActions.RefreshHostWindowsAsync().ConfigureAwait(false);
        }

        return _config;
    }

    private async Task<object?> GetInstalledSkinsAsync()
    {
        return await new SkinStore(_paths).ListInstalledAsync().ConfigureAwait(false);
    }

    private async Task<object?> GetBackupsAsync() =>
        await _backupService.ListAsync().ConfigureAwait(false);

    private async Task<object?> CreateBackupAsync(JsonElement parameters)
    {
        var kind = GetRequiredString(parameters, "kind");
        var skinId = GetOptionalString(parameters, "skinId");
        var reason = GetOptionalString(parameters, "reason") ?? "manual";
        return await _backupService.CreateBackupAsync(kind, reason, skinId).ConfigureAwait(false);
    }

    private async Task<object?> RestoreBackupAsync(JsonElement parameters)
    {
        var result = await _backupService.RestoreAsync(GetRequiredString(parameters, "id")).ConfigureAwait(false);
        if (string.Equals(result.Restored.Kind, BackupKinds.Config, StringComparison.OrdinalIgnoreCase))
        {
            var restoredConfig = await _configService.LoadOrCreateAsync().ConfigureAwait(false);
            CopyConfig(restoredConfig, _config);
            await _hostActions.RefreshHostWindowsAsync().ConfigureAwait(false);
        }
        else if (string.Equals(result.Restored.Kind, BackupKinds.Skin, StringComparison.OrdinalIgnoreCase))
        {
            await _hostActions.RefreshHostWindowsAsync().ConfigureAwait(false);
        }

        return result;
    }

    private async Task<object?> GetInstalledSkinDetailsAsync()
    {
        var skinStore = new SkinStore(_paths);
        var skins = await skinStore.ListInstalledAsync().ConfigureAwait(false);
        return skins.Select(skin => new
        {
            skin.SchemaVersion,
            skin.Id,
            skin.Name,
            skin.Version,
            skin.Author,
            skin.Description,
            skin.Entry,
            skin.Entries,
            skin.MinimumDesktopHtmlVersion,
            skin.Permissions,
            skinPath = skinStore.GetSkinDirectory(skin.Id),
            storageBytes = GetSkinStorageBytes(skin.Id)
        }).ToArray();
    }

    private long GetSkinStorageBytes(string skinId)
    {
        try
        {
            var directory = Path.Combine(_paths.StorageDirectory, skinId);
            if (!Directory.Exists(directory))
            {
                return 0;
            }

            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Sum(file => new FileInfo(file).Length);
        }
        catch
        {
            return 0;
        }
    }

    private async Task<object?> InstallSkinFromFolderAsync(JsonElement parameters)
    {
        var path = GetRequiredString(parameters, "path");
        var overwrite = GetOptionalBool(parameters, "overwrite") ?? false;
        var skinStore = new SkinStore(_paths, backupService: _backupService);
        var manifest = await skinStore.InstallAsync(path, overwrite).ConfigureAwait(false);

        return new
        {
            installed = true,
            manifest.Id,
            manifest.Name,
            manifest.Version,
            skinPath = skinStore.GetSkinDirectory(manifest.Id),
            warning = "desktop.html skins are full-trust local programs. Only install skins from sources you trust."
        };
    }

    private async Task<object?> ActivateSkinAsync(JsonElement parameters)
    {
        var config = await LoadConfigForMutationAsync().ConfigureAwait(false);
        await new SkinStore(_paths)
            .ActivateAsync(config, GetRequiredString(parameters, "skinId"), GetOptionalString(parameters, "entry"))
            .ConfigureAwait(false);
        return await SaveAndRefreshAsync(config, "skin-activate").ConfigureAwait(false);
    }

    private async Task<object?> AssignMonitorSkinAsync(JsonElement parameters)
    {
        var config = await LoadConfigForMutationAsync().ConfigureAwait(false);
        await new SkinStore(_paths)
            .AssignMonitorAsync(
                config,
                GetRequiredString(parameters, "monitorId"),
                GetRequiredString(parameters, "skinId"),
                GetOptionalString(parameters, "entry"))
            .ConfigureAwait(false);
        return await SaveAndRefreshAsync(config, "monitor-assign").ConfigureAwait(false);
    }

    private async Task<object?> ClearMonitorSkinAsync(JsonElement parameters)
    {
        var config = await LoadConfigForMutationAsync().ConfigureAwait(false);
        new SkinStore(_paths).ClearMonitorAssignment(config, GetRequiredString(parameters, "monitorId"));
        return await SaveAndRefreshAsync(config, "monitor-clear").ConfigureAwait(false);
    }

    private async Task<object?> ConfigureSpanningAsync(JsonElement parameters)
    {
        var config = await LoadConfigForMutationAsync().ConfigureAwait(false);
        await new SkinStore(_paths)
            .AssignSpanningAsync(
                config,
                GetRequiredString(parameters, "skinId"),
                GetOptionalString(parameters, "entry"),
                GetStringArray(parameters, "monitors"))
            .ConfigureAwait(false);
        return await SaveAndRefreshAsync(config, "monitor-span").ConfigureAwait(false);
    }

    private async Task<object?> SetMonitorModeAsync(JsonElement parameters)
    {
        var config = await LoadConfigForMutationAsync().ConfigureAwait(false);
        new SkinStore(_paths).SetActiveMode(config, GetRequiredString(parameters, "mode"));
        return await SaveAndRefreshAsync(config, "monitor-mode").ConfigureAwait(false);
    }

    private async Task<object?> SetStartupEnabledAsync(JsonElement parameters)
    {
        var enabled = GetOptionalBool(parameters, "enabled")
            ?? throw new InvalidOperationException("setStartupEnabled requires an enabled boolean.");

        var startupService = new StartupService();
        if (enabled)
        {
            startupService.Enable(ResolveExecutablePath());
        }
        else
        {
            startupService.Disable();
        }

        var config = await LoadConfigForMutationAsync().ConfigureAwait(false);
        config.App.StartWithWindows = enabled;
        await SaveConfigWithBackupAsync(config, "startup-toggle").ConfigureAwait(false);
        CopyConfig(config, _config);
        return new { enabled };
    }

    private Task<object?> OpenSkinFolderAsync(JsonElement parameters)
    {
        var skinId = GetRequiredString(parameters, "skinId");
        return ShellExecutePathAsync(new SkinStore(_paths).GetSkinDirectory(skinId));
    }

    private static async Task<object?> HostActionAsync(Func<Task> action)
    {
        await action().ConfigureAwait(false);
        return null;
    }

    private async Task<DesktopHtmlConfig> LoadConfigForMutationAsync() =>
        await _configService.LoadOrCreateAsync().ConfigureAwait(false);

    private async Task<DesktopHtmlConfig> SaveAndRefreshAsync(DesktopHtmlConfig config, string backupReason)
    {
        await SaveConfigWithBackupAsync(config, backupReason).ConfigureAwait(false);
        CopyConfig(config, _config);
        await _hostActions.RefreshHostWindowsAsync().ConfigureAwait(false);
        return _config;
    }

    private async Task SaveConfigWithBackupAsync(DesktopHtmlConfig config, string reason)
    {
        if (File.Exists(_paths.ConfigFile))
        {
            await _backupService.CreateConfigBackupAsync(reason).ConfigureAwait(false);
        }

        await _configService.SaveAsync(config).ConfigureAwait(false);
    }

    private Task<object?> ShellExecutePathAsync(string path)
    {
        return ShellExecuteAsync(new ShellExecutionOptions { File = path });
    }

    private static Task<object?> RevealInExplorerAsync(string path)
    {
        return StartRawProcessAsync(
            "explorer.exe",
            [$"/select,{path}"],
            null,
            waitForExit: false,
            captureOutput: false);
    }

    private static async Task<object?> ShellExecuteAsync(ShellExecutionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.File))
        {
            throw new InvalidOperationException("shellExecute requires a file value.");
        }

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = options.File,
            WorkingDirectory = options.WorkingDirectory ?? "",
            Verb = options.Verb ?? "",
            UseShellExecute = true,
            WindowStyle = ToProcessWindowStyle(options.ShowWindow)
        };

        foreach (var arg in options.Args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();

        int? exitCode = null;
        if (options.WaitForExit)
        {
            await process.WaitForExitAsync().ConfigureAwait(false);
            exitCode = process.ExitCode;
        }

        int? processId;
        try
        {
            processId = process.Id;
        }
        catch (InvalidOperationException)
        {
            processId = null;
        }

        return new ShellExecutionResult(processId, exitCode);
    }

    private async Task<object?> ReadTextAsync(JsonElement parameters) =>
        await _fileSystemService.ReadTextAsync(GetRequiredString(parameters, "path")).ConfigureAwait(false);

    private async Task<object?> WriteTextAsync(JsonElement parameters)
    {
        await _fileSystemService.WriteTextAsync(
                GetRequiredString(parameters, "path"),
                GetOptionalString(parameters, "content") ?? "")
            .ConfigureAwait(false);
        return null;
    }

    private async Task<object?> ReadJsonAsync(JsonElement parameters) =>
        await _fileSystemService.ReadJsonAsync(GetRequiredString(parameters, "path")).ConfigureAwait(false);

    private async Task<object?> WriteJsonAsync(JsonElement parameters)
    {
        await _fileSystemService.WriteJsonAsync(
                GetRequiredString(parameters, "path"),
                parameters.GetProperty("value").Deserialize<JsonNode>(JsonOptions))
            .ConfigureAwait(false);
        return null;
    }

    private Task<object?> CreateDirectoryAsync(JsonElement parameters)
    {
        _fileSystemService.CreateDirectory(GetRequiredString(parameters, "path"));
        return Task.FromResult<object?>(null);
    }

    private Task<object?> DeletePathAsync(JsonElement parameters)
    {
        var options = parameters.TryGetProperty("options", out var optionsElement)
            ? optionsElement.Deserialize<DeletePathOptions>(JsonOptions)
            : null;
        _fileSystemService.DeletePath(GetRequiredString(parameters, "path"), options);
        return Task.FromResult<object?>(null);
    }

    private Task<object?> MovePathAsync(JsonElement parameters)
    {
        _fileSystemService.MovePath(
            GetRequiredString(parameters, "source"),
            GetRequiredString(parameters, "destination"));
        return Task.FromResult<object?>(null);
    }

    private Task<object?> CopyPathAsync(JsonElement parameters)
    {
        _fileSystemService.CopyPath(
            GetRequiredString(parameters, "source"),
            GetRequiredString(parameters, "destination"));
        return Task.FromResult<object?>(null);
    }

    private async Task<object?> StorageGetAsync(JsonElement parameters) =>
        await _skinStorageService.GetAsync(_activeSkin.Manifest.Id, GetRequiredString(parameters, "key"))
            .ConfigureAwait(false);

    private async Task<object?> StorageSetAsync(JsonElement parameters)
    {
        await _skinStorageService.SetAsync(
                _activeSkin.Manifest.Id,
                GetRequiredString(parameters, "key"),
                parameters.GetProperty("value").Deserialize<JsonNode>(JsonOptions))
            .ConfigureAwait(false);
        return null;
    }

    private async Task<object?> StorageRemoveAsync(JsonElement parameters)
    {
        await _skinStorageService.RemoveAsync(_activeSkin.Manifest.Id, GetRequiredString(parameters, "key"))
            .ConfigureAwait(false);
        return null;
    }

    private async Task<object?> StorageClearAsync()
    {
        await _skinStorageService.ClearAsync(_activeSkin.Manifest.Id).ConfigureAwait(false);
        return null;
    }

    private async Task<object?> StorageGetAllAsync() =>
        await _skinStorageService.GetAllAsync(_activeSkin.Manifest.Id).ConfigureAwait(false);

    private static Task<object?> ClipboardWriteTextAsync(JsonElement parameters)
    {
        System.Windows.Clipboard.SetText(GetOptionalString(parameters, "text") ?? "");
        return Task.FromResult<object?>(null);
    }

    private async Task<object?> WriteFileBase64Async(JsonElement parameters)
    {
        var path = GetRequiredString(parameters, "path");
        var dataBase64 = GetRequiredString(parameters, "dataBase64");
        var unique = GetOptionalBool(parameters, "unique") ?? false;
        var bytes = Convert.FromBase64String(dataBase64);
        var savedPath = await _fileSystemService.WriteBytesAsync(path, bytes, unique).ConfigureAwait(false);
        return new { path = savedPath };
    }

    private Task<object?> ClipboardSaveToDirectoryAsync(JsonElement parameters)
    {
        // Clipboard access must happen on the calling (UI) thread, so this
        // method stays fully synchronous — no awaits before the clipboard reads.
        var directory = GetRequiredString(parameters, "directory");
        Directory.CreateDirectory(directory);
        var savedPaths = new List<string>();

        if (System.Windows.Clipboard.ContainsFileDropList())
        {
            foreach (var source in System.Windows.Clipboard.GetFileDropList())
            {
                if (string.IsNullOrWhiteSpace(source))
                {
                    continue;
                }

                var trimmed = source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var destination = FileSystemService.GetUniquePath(
                    Path.Combine(directory, Path.GetFileName(trimmed)));
                _fileSystemService.CopyPath(source, destination);
                savedPaths.Add(destination);
            }
        }
        else if (System.Windows.Clipboard.ContainsImage())
        {
            var image = System.Windows.Clipboard.GetImage();
            if (image is not null)
            {
                var destination = FileSystemService.GetUniquePath(
                    Path.Combine(directory, $"Pasted {DateTime.Now:yyyy-MM-dd HH.mm.ss}.png"));
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                using var stream = File.Create(destination);
                encoder.Save(stream);
                savedPaths.Add(destination);
            }
        }

        return Task.FromResult<object?>(new { savedPaths });
    }

    private static Task<object?> RunAsync(RunOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Command))
        {
            throw new InvalidOperationException("run requires a command value.");
        }

        return StartRawProcessAsync(
            options.Command,
            options.Args,
            options.WorkingDirectory,
            options.WaitForExit,
            options.CaptureOutput);
    }

    private static Task<object?> RunCommandLineAsync(CommandLineOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.CommandLine))
        {
            throw new InvalidOperationException("runCommandLine requires a commandLine value.");
        }

        return StartRawProcessAsync(
            "cmd.exe",
            ["/d", "/s", "/c", options.CommandLine],
            options.WorkingDirectory,
            options.WaitForExit,
            options.CaptureOutput);
    }

    private static Task<object?> RunPowerShellAsync(PowerShellOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ScriptOrFile))
        {
            throw new InvalidOperationException("runPowerShell requires a scriptOrFile value.");
        }

        var args = File.Exists(options.ScriptOrFile)
            ? new List<string> { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", options.ScriptOrFile }
            : new List<string> { "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", options.ScriptOrFile };

        return StartRawProcessAsync(
            "powershell.exe",
            args,
            options.WorkingDirectory,
            options.WaitForExit,
            options.CaptureOutput);
    }

    private static Task<object?> RunBatchAsync(BatchOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ScriptOrFile))
        {
            throw new InvalidOperationException("runBatch requires a scriptOrFile value.");
        }

        return StartRawProcessAsync(
            "cmd.exe",
            ["/d", "/s", "/c", options.ScriptOrFile],
            options.WorkingDirectory,
            options.WaitForExit,
            options.CaptureOutput);
    }

    private static async Task<object?> StartRawProcessAsync(
        string fileName,
        IReadOnlyList<string> args,
        string? workingDirectory,
        bool waitForExit,
        bool captureOutput)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory ?? "",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput
        };

        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();

        if (!waitForExit)
        {
            return new RunResult(process.Id, null, null, null);
        }

        Task<string>? standardOutputTask = null;
        Task<string>? standardErrorTask = null;

        if (captureOutput)
        {
            standardOutputTask = process.StandardOutput.ReadToEndAsync();
            standardErrorTask = process.StandardError.ReadToEndAsync();
        }

        await process.WaitForExitAsync().ConfigureAwait(false);

        return new RunResult(
            process.Id,
            process.ExitCode,
            standardOutputTask is null ? null : await standardOutputTask.ConfigureAwait(false),
            standardErrorTask is null ? null : await standardErrorTask.ConfigureAwait(false));
    }

    private static ProcessWindowStyle ToProcessWindowStyle(string? showWindow)
    {
        return showWindow?.ToLowerInvariant() switch
        {
            "hidden" => ProcessWindowStyle.Hidden,
            "minimized" => ProcessWindowStyle.Minimized,
            "maximized" => ProcessWindowStyle.Maximized,
            _ => ProcessWindowStyle.Normal
        };
    }

    private static T Parse<T>(JsonElement element)
    {
        return element.Deserialize<T>(JsonOptions)
            ?? throw new InvalidOperationException($"Could not parse bridge parameters as {typeof(T).Name}.");
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        return GetOptionalString(element, propertyName)
            ?? throw new InvalidOperationException($"Missing required parameter '{propertyName}'.");
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static int? GetOptionalInt32(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetInt32()
            : null;
    }

    private static bool? GetOptionalBool(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : null;
    }

    private static string[] GetStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return [];
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Parameter '{propertyName}' must be an array.");
        }

        return property
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
    }

    private static string ResolveExecutablePath()
    {
        return Environment.ProcessPath
            ?? System.Reflection.Assembly.GetEntryAssembly()?.Location
            ?? throw new InvalidOperationException("Could not determine the desktop-html executable path.");
    }

    private static string Serialize(BridgeResponse response) => JsonSerializer.Serialize(response, JsonOptions);

    private static void CopyConfig(DesktopHtmlConfig source, DesktopHtmlConfig target)
    {
        target.SchemaVersion = source.SchemaVersion;
        target.App = source.App;
        target.Desktop = source.Desktop;
        target.Performance = source.Performance;
        target.Skins = source.Skins;
    }

    private async Task<object?> HttpFetchAsync(JsonElement parameters)
    {
        var options = new HttpFetchOptions
        {
            Url = GetRequiredString(parameters, "url"),
            Method = GetOptionalString(parameters, "method") ?? "GET",
            Body = GetOptionalString(parameters, "body"),
            AsBase64 = GetOptionalBool(parameters, "asBase64") ?? false
        };

        if (parameters.TryGetProperty("headers", out var headersProp) && headersProp.ValueKind == JsonValueKind.Object)
        {
            options.Headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersProp, JsonOptions);
        }

        if (GetOptionalInt32(parameters, "timeoutMs") is { } timeoutMs)
        {
            options.TimeoutMs = timeoutMs;
        }

        if (GetOptionalInt32(parameters, "maxResponseBytes") is { } maxBytes)
        {
            options.MaxResponseBytes = maxBytes;
        }

        return await _httpFetchService.FetchAsync(options).ConfigureAwait(false);
    }

    private Task<object?> StartTerminalAsync(JsonElement parameters)
    {
        var options = new TerminalStartOptions
        {
            SessionId = GetOptionalString(parameters, "sessionId"),
            Command = GetOptionalString(parameters, "command") ?? "wsl.exe",
            Args = GetStringArray(parameters, "args"),
            WorkingDirectory = GetOptionalString(parameters, "workingDirectory"),
            Pty = GetOptionalBool(parameters, "pty") ?? false,
            Cols = GetOptionalInt32(parameters, "cols") ?? 80,
            Rows = GetOptionalInt32(parameters, "rows") ?? 24
        };

        var sessionId = _terminalService.Start(
            options,
            (id, text) => EmitEvent(new { type = "terminalOutput", sessionId = id, text }),
            (id, exitCode) => EmitEvent(new { type = "terminalExit", sessionId = id, exitCode }));

        return Task.FromResult<object?>(new { sessionId });
    }

    private Task<object?> WriteTerminalAsync(JsonElement parameters)
    {
        _terminalService.Write(
            GetRequiredString(parameters, "sessionId"),
            GetOptionalString(parameters, "text") ?? "");
        return Task.FromResult<object?>(null);
    }

    private Task<object?> ResizeTerminalAsync(JsonElement parameters)
    {
        var resized = _terminalService.Resize(
            GetRequiredString(parameters, "sessionId"),
            GetOptionalInt32(parameters, "cols") ?? 80,
            GetOptionalInt32(parameters, "rows") ?? 24);
        return Task.FromResult<object?>(new { resized });
    }

    private static Task<object?> GetIconAsync(JsonElement parameters)
    {
        var path = GetRequiredString(parameters, "path");
        var size = GetOptionalString(parameters, "size")
            ?? ((GetOptionalBool(parameters, "small") ?? false) ? "small" : "large");

        return System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            try
            {
                return Task.FromResult<object?>(IconService.GetIconDataUrl(path, size));
            }
            catch (Exception)
            {
                return Task.FromResult<object?>(null);
            }
        });
    }

    private Task<object?> WatchAsync(JsonElement parameters)
    {
        var watchId = _watchService.Watch(
            GetOptionalString(parameters, "watchId"),
            GetRequiredString(parameters, "path"),
            GetOptionalBool(parameters, "recursive") ?? false,
            (id, changes) => EmitEvent(new { type = "fileSystemChanged", watchId = id, changes }));

        return Task.FromResult<object?>(new { watchId });
    }

    private Task<object?> UnwatchAsync(JsonElement parameters)
    {
        var removed = _watchService.Unwatch(GetRequiredString(parameters, "watchId"));
        return Task.FromResult<object?>(new { removed });
    }

    private Task<object?> SubscribeSystemStatsAsync(JsonElement parameters)
    {
        // One subscription per page; resubscribing replaces the old timer. The
        // host enforces a floor so skins cannot poll faster than once a second.
        var intervalMs = Math.Clamp(GetOptionalInt32(parameters, "intervalMs") ?? 2000, 1000, 3600_000);

        StopStatsTimer();
        _statsTimer = new System.Threading.Timer(
            _ => EmitEvent(new { type = "systemStats", stats = _statsService.GetStats() }),
            null,
            0,
            intervalMs);

        return Task.FromResult<object?>(new { intervalMs });
    }

    private Task<object?> UnsubscribeSystemStatsAsync()
    {
        StopStatsTimer();
        return Task.FromResult<object?>(null);
    }

    private Task<object?> NotifyAsync(JsonElement parameters)
    {
        _hostActions.ShowNotification(
            GetRequiredString(parameters, "title"),
            GetOptionalString(parameters, "message") ?? "");
        return Task.FromResult<object?>(null);
    }

    private async Task<object?> MediaGetNowPlayingAsync(JsonElement parameters) =>
        await _mediaService.GetNowPlayingAsync(GetOptionalBool(parameters, "thumbnail") ?? false)
            .ConfigureAwait(false);

    private async Task<object?> MediaControlAsync(JsonElement parameters) =>
        new
        {
            handled = await _mediaService.ControlAsync(GetRequiredString(parameters, "action")).ConfigureAwait(false)
        };

    private async Task<object?> MediaSubscribeAsync()
    {
        await _mediaService.SubscribeAsync(() => _ = EmitMediaChangedAsync()).ConfigureAwait(false);
        return null;
    }

    private Task<object?> MediaUnsubscribeAsync()
    {
        _mediaService.Unsubscribe();
        return Task.FromResult<object?>(null);
    }

    private async Task EmitMediaChangedAsync()
    {
        try
        {
            var nowPlaying = await _mediaService.GetNowPlayingAsync(includeThumbnail: false).ConfigureAwait(false);
            EmitEvent(new { type = "mediaChanged", nowPlaying });
        }
        catch
        {
        }
    }

    private Task<object?> RegisterHotkeyAsync(JsonElement parameters)
    {
        var service = HotkeyService.Instance
            ?? throw new InvalidOperationException("Global hotkeys are unavailable in this host.");

        var key = GetRequiredString(parameters, "key");
        var modifiers = GetStringArray(parameters, "modifiers");

        // The id is only known after registration; box it so the callback can
        // report the right id.
        var idBox = new int[1];
        var hotkeyId = service.Register(modifiers, key,
            () => EmitEvent(new { type = "hotkeyPressed", hotkeyId = idBox[0] }));
        idBox[0] = hotkeyId;
        _hotkeyIds.Add(hotkeyId);
        return Task.FromResult<object?>(new { hotkeyId });
    }

    private Task<object?> UnregisterHotkeyAsync(JsonElement parameters)
    {
        var hotkeyId = GetOptionalInt32(parameters, "hotkeyId")
            ?? throw new InvalidOperationException("unregisterHotkey requires a hotkeyId value.");
        _hotkeyIds.Remove(hotkeyId);
        var removed = HotkeyService.Instance?.Unregister(hotkeyId) ?? false;
        return Task.FromResult<object?>(new { removed });
    }
}
