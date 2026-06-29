using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using DesktopHtml.Core;
using DesktopHtml.Core.Backups;
using DesktopHtml.Core.Bridge;
using DesktopHtml.Core.Configuration;
using DesktopHtml.Core.Execution;
using DesktopHtml.Core.FileSystem;
using DesktopHtml.Core.Logging;
using DesktopHtml.Core.Monitors;
using DesktopHtml.Core.Skins;
using DesktopHtml.Core.Startup;
using DesktopHtml.Core.Storage;

namespace DesktopHtml.App;

public sealed class DesktopBridgeDispatcher
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
            await _logService.InfoAsync("bridge", "Bridge call succeeded.", new
            {
                request.Method,
                elapsedMs = stopwatch.ElapsedMilliseconds
            }).ConfigureAwait(false);
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
                _config.Desktop.PlacementMode)),
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
            "readJson" => ReadJsonAsync(request.Params),
            "writeJson" => WriteJsonAsync(request.Params),
            "listDirectory" => Task.FromResult<object?>(_fileSystemService.ListDirectory(GetRequiredString(request.Params, "path"))),
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
            "getLogs" => Task.FromResult<object?>(_logService.ReadLines(GetOptionalInt32(request.Params, "maxLines") ?? 200)),
            "getLastError" => Task.FromResult<object?>(_lastError),
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
        "getLogs",
        "getLastError"
    ];

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
            skinPath = skinStore.GetSkinDirectory(skin.Id)
        }).ToArray();
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
}
