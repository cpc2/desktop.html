using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopHtml.Core;
using DesktopHtml.Core.Backups;
using DesktopHtml.Core.Configuration;
using DesktopHtml.Core.Ipc;
using DesktopHtml.Core.Logging;
using DesktopHtml.Core.Monitors;
using DesktopHtml.Core.Skins;
using DesktopHtml.Core.Startup;

namespace DesktopHtml.App;

public sealed record SkinDevResult(
    bool Installed,
    bool Activated,
    bool Reloaded,
    string? SkinId,
    string? Entry,
    SkinValidationResult Validation,
    RuntimeCommandResponse? ReloadResponse);

public static class CliRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static async Task<int> RunAsync(string[] args, AppPaths paths, ConfigService configService)
    {
        var logService = new LogService(paths);
        try
        {
            var exitCode = args switch
            {
                ["--version"] or ["version"] => WriteVersion(),
                ["status", .. var rest] => await WriteStatusAsync(rest, paths, configService),
                ["skin", "list", .. var rest] => await ListSkinsAsync(rest, paths),
                ["skin", "validate", var folder, .. var rest] => await ValidateSkinAsync(folder, rest),
                ["skin", "scaffold", var skinId, .. var rest] => await ScaffoldSkinAsync(skinId, rest),
                ["skin", "dev", var folder, .. var rest] => await DevSkinAsync(folder, rest, paths, configService),
                ["skin", "install", var folder, .. var rest] => await InstallSkinAsync(folder, rest, paths),
                ["skin", "activate", var skinId, .. var rest] => await ActivateSkinAsync(skinId, rest, paths, configService),
                ["skin", "reload", .. var rest] => await SendRuntimeCommandAsync("reloadSkin", rest),
                ["monitor", "list", .. var rest] => ListMonitors(rest),
                ["monitor", "assign", var monitorId, var skinId, .. var rest] => await AssignMonitorAsync(monitorId, skinId, rest, paths, configService),
                ["monitor", "clear", var monitorId, .. var rest] => await ClearMonitorAsync(monitorId, rest, paths, configService),
                ["monitor", "reload", var monitorId, .. var rest] => await SendRuntimeCommandAsync(
                    "reloadMonitorSkin",
                    rest,
                    new JsonObject { ["monitorId"] = monitorId }),
                ["monitor", "mode", var mode, .. var rest] => await SetMonitorModeAsync(mode, rest, paths, configService),
                ["monitor", "span", var skinId, .. var rest] => await SetSpanningAsync(skinId, rest, paths, configService),
                ["placement", "diagnostics", .. var rest] => await PlacementDiagnosticsAsync(rest, paths, configService),
                ["placement", "reapply", .. var rest] => await SendRuntimeCommandAsync("placementReapply", rest),
                ["logs", .. var rest] => ShowLogs(rest, paths),
                ["ping", .. var rest] => await SendRuntimeCommandAsync("ping", rest),
                ["open-settings", .. var rest] => await SendRuntimeCommandAsync("openSettings", rest),
                ["exit", .. var rest] => await SendRuntimeCommandAsync("exit", rest),
                ["startup", "status", .. var rest] => StartupStatus(rest),
                ["startup", "on", .. var rest] => await SetStartupAsync(true, rest, paths, configService),
                ["startup", "off", .. var rest] => await SetStartupAsync(false, rest, paths, configService),
                ["config", "get", .. var rest] => await GetConfigAsync(rest, configService),
                ["config", "patch", var patch, .. var rest] => await PatchConfigAsync(patch, rest, paths, configService),
                ["config", "set", var path, var value, .. var rest] => await SetConfigAsync(path, value, rest, paths, configService),
                ["backup", "list", .. var rest] => await ListBackupsAsync(rest, paths),
                ["backup", "create", var kind, .. var rest] => await CreateBackupAsync(kind, rest, paths),
                ["backup", "restore", var backupId, .. var rest] => await RestoreBackupAsync(backupId, rest, paths),
                ["backup", "prune", .. var rest] => await PruneBackupsAsync(rest, paths),
                ["safe-mode", "on", .. var rest] => await SetSafeModeAsync(true, rest, paths, configService),
                ["safe-mode", "off", .. var rest] => await SetSafeModeAsync(false, rest, paths, configService),
                ["help"] or ["--help"] or ["-h"] => WriteHelp(),
                _ => WriteError($"Unknown command: {string.Join(' ', args)}", 2)
            };

            await logService.InfoAsync("cli", "CLI command completed.", new
            {
                command = string.Join(' ', args),
                exitCode
            });
            return exitCode;
        }
        catch (Exception ex)
        {
            await logService.ErrorAsync("cli", "CLI command failed.", new
            {
                command = string.Join(' ', args),
                error = ex.Message
            });
            return WriteError(ex.Message, 1);
        }
    }

    private static int WriteVersion()
    {
        Console.WriteLine(AppVersion.Current);
        return 0;
    }

    private static async Task<int> WriteStatusAsync(string[] args, AppPaths paths, ConfigService configService)
    {
        var config = await configService.LoadOrCreateAsync();
        var skinStore = new SkinStore(paths);
        ResolvedSkin? activeSkin = null;
        string? activeSkinError = null;

        try
        {
            activeSkin = await skinStore.ResolveActiveSkinAsync(config);
        }
        catch (Exception ex)
        {
            activeSkinError = ex.Message;
        }

        var status = new
        {
            version = AppVersion.Current,
            appDataRoot = paths.Root,
            configFile = paths.ConfigFile,
            safeMode = config.App.SafeMode,
            placementMode = config.Desktop.PlacementMode,
            activeMode = config.Skins.ActiveMode,
            activeSkinId = config.Skins.ActiveSkinId,
            activeSkinEntry = config.Skins.Entry,
            activeSkinPath = activeSkin?.Directory,
            monitorAssignments = config.Skins.PerMonitor,
            spanning = config.Skins.Spanning,
            activeSkinError
        };

        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(status, JsonOptions));
        }
        else
        {
            Console.WriteLine($"desktop.html {AppVersion.Current}");
            Console.WriteLine($"AppData: {paths.Root}");
            Console.WriteLine($"Config: {paths.ConfigFile}");
            Console.WriteLine($"Safe mode: {config.App.SafeMode}");
            Console.WriteLine($"Placement: {config.Desktop.PlacementMode}");
            Console.WriteLine($"Active mode: {config.Skins.ActiveMode}");
            Console.WriteLine($"Active skin: {config.Skins.ActiveSkinId ?? "(none)"}");
            Console.WriteLine($"Monitor assignments: {config.Skins.PerMonitor.Count}");
            Console.WriteLine($"Spanning skin: {config.Skins.Spanning.SkinId ?? "(none)"}");
            if (activeSkinError is not null)
            {
                Console.WriteLine($"Skin error: {activeSkinError}");
            }
        }

        return activeSkinError is null ? 0 : 1;
    }

    private static async Task<int> ListSkinsAsync(string[] args, AppPaths paths)
    {
        var skins = await new SkinStore(paths).ListInstalledAsync();

        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(skins, JsonOptions));
        }
        else
        {
            foreach (var skin in skins)
            {
                Console.WriteLine($"{skin.Id}\t{skin.Name}\t{skin.Version}");
            }
        }

        return 0;
    }

    private static async Task<int> SetMonitorModeAsync(
        string mode,
        string[] args,
        AppPaths paths,
        ConfigService configService)
    {
        var hadConfig = File.Exists(paths.ConfigFile);
        var config = await configService.LoadOrCreateAsync();
        new SkinStore(paths).SetActiveMode(config, mode);
        await SaveConfigWithBackupAsync(paths, configService, config, "monitor-mode", hadConfig);

        var result = new { activeMode = config.Skins.ActiveMode };
        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        }
        else
        {
            Console.WriteLine($"Monitor mode: {config.Skins.ActiveMode}");
        }

        return 0;
    }

    private static async Task<int> SetSpanningAsync(
        string skinId,
        string[] args,
        AppPaths paths,
        ConfigService configService)
    {
        var entry = GetOptionValue(args, "--entry");
        var monitors = (GetOptionValue(args, "--monitors") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hadConfig = File.Exists(paths.ConfigFile);
        var config = await configService.LoadOrCreateAsync();
        var skinStore = new SkinStore(paths);
        await skinStore.AssignSpanningAsync(config, skinId, entry, monitors);
        await SaveConfigWithBackupAsync(paths, configService, config, "monitor-span", hadConfig);

        var result = new
        {
            activeMode = config.Skins.ActiveMode,
            skinId = config.Skins.Spanning.SkinId,
            entry = config.Skins.Spanning.Entry,
            monitors = config.Skins.Spanning.Monitors
        };

        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        }
        else
        {
            var monitorText = config.Skins.Spanning.Monitors.Count == 0
                ? "all monitors"
                : string.Join(", ", config.Skins.Spanning.Monitors);
            Console.WriteLine($"Spanning skin: {config.Skins.Spanning.SkinId} ({config.Skins.Spanning.Entry}) over {monitorText}.");
        }

        return 0;
    }

    private static async Task<int> AssignMonitorAsync(
        string monitorId,
        string skinId,
        string[] args,
        AppPaths paths,
        ConfigService configService)
    {
        var entry = GetOptionValue(args, "--entry");
        var hadConfig = File.Exists(paths.ConfigFile);
        var config = await configService.LoadOrCreateAsync();
        var skinStore = new SkinStore(paths);
        await skinStore.AssignMonitorAsync(config, monitorId, skinId, entry);
        await SaveConfigWithBackupAsync(paths, configService, config, "monitor-assign", hadConfig);

        var assignment = config.Skins.PerMonitor[monitorId];
        var result = new
        {
            assigned = true,
            activeMode = config.Skins.ActiveMode,
            monitorId,
            skinId = assignment.SkinId,
            entry = assignment.Entry
        };

        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        }
        else
        {
            Console.WriteLine($"Assigned {assignment.SkinId} ({assignment.Entry}) to monitor {monitorId}.");
        }

        return 0;
    }

    private static async Task<int> ClearMonitorAsync(
        string monitorId,
        string[] args,
        AppPaths paths,
        ConfigService configService)
    {
        var hadConfig = File.Exists(paths.ConfigFile);
        var config = await configService.LoadOrCreateAsync();
        new SkinStore(paths).ClearMonitorAssignment(config, monitorId);
        await SaveConfigWithBackupAsync(paths, configService, config, "monitor-clear", hadConfig);

        var result = new
        {
            cleared = true,
            activeMode = config.Skins.ActiveMode,
            monitorId
        };

        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        }
        else
        {
            Console.WriteLine($"Cleared monitor assignment: {monitorId}");
        }

        return 0;
    }

    private static async Task<int> ValidateSkinAsync(string folder, string[] args)
    {
        var strict = args.Contains("--strict");
        var result = await new SkinValidator().ValidateAsync(folder, strict);

        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        }
        else if (result.IsValid && result.Manifest is not null)
        {
            Console.WriteLine($"Valid skin: {result.Manifest.Id}");
        }
        else
        {
            Console.WriteLine("Invalid skin:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"- {error}");
            }
        }

        foreach (var warning in result.Warnings)
        {
            Console.WriteLine($"Warning: {warning}");
        }

        return result.IsValid ? 0 : 1;
    }

    private static async Task<int> ScaffoldSkinAsync(string skinId, string[] args)
    {
        var template = GetOptionValue(args, "--template") ?? "blank";
        var outputDirectory = GetOptionValue(args, "--out")
            ?? Path.Combine(Environment.CurrentDirectory, skinId);
        var overwrite = args.Contains("--force");
        var result = await new SkinScaffoldService().ScaffoldAsync(skinId, outputDirectory, template, overwrite);

        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        }
        else
        {
            Console.WriteLine($"Created skin scaffold: {result.Directory}");
        }

        return 0;
    }

    private static async Task<int> DevSkinAsync(
        string folder,
        string[] args,
        AppPaths paths,
        ConfigService configService)
    {
        if (args.Contains("--mock-bridge") || args.Contains("--no-execution"))
        {
            throw new InvalidOperationException("skin dev --mock-bridge and --no-execution are planned but not implemented in this pass.");
        }

        var watch = args.Contains("--watch");
        var entry = GetOptionValue(args, "--entry");
        var strict = !args.Contains("--no-strict");
        var result = await SyncDevSkinAsync(folder, entry, strict, paths, configService);
        WriteDevResult(result, args.Contains("--json"));

        if (!watch)
        {
            return result.Validation.IsValid ? 0 : 1;
        }

        if (!result.Validation.IsValid)
        {
            return 1;
        }

        if (!args.Contains("--json"))
        {
            Console.WriteLine("Watching skin folder. Press Ctrl+C to stop.");
        }
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stop.Cancel();
        };

        using var watcher = new FileSystemWatcher(Path.GetFullPath(folder))
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.DirectoryName
                | NotifyFilters.LastWrite
                | NotifyFilters.Size
        };

        var changed = new SemaphoreSlim(0);
        FileSystemEventHandler onChanged = (_, _) => changed.Release();
        RenamedEventHandler onRenamed = (_, _) => changed.Release();
        watcher.Changed += onChanged;
        watcher.Created += onChanged;
        watcher.Deleted += onChanged;
        watcher.Renamed += onRenamed;

        while (!stop.IsCancellationRequested)
        {
            try
            {
                await changed.WaitAsync(stop.Token);
                await Task.Delay(250, stop.Token);
                while (changed.CurrentCount > 0)
                {
                    await changed.WaitAsync(stop.Token);
                }

                result = await SyncDevSkinAsync(folder, entry, strict, paths, configService);
                WriteDevResult(result, args.Contains("--json"));
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                break;
            }
        }

        watcher.Changed -= onChanged;
        watcher.Created -= onChanged;
        watcher.Deleted -= onChanged;
        watcher.Renamed -= onRenamed;
        return 0;
    }

    private static async Task<SkinDevResult> SyncDevSkinAsync(
        string folder,
        string? entry,
        bool strict,
        AppPaths paths,
        ConfigService configService)
    {
        var validation = await new SkinValidator().ValidateAsync(folder, strict);
        if (!validation.IsValid || validation.Manifest is null)
        {
            return new SkinDevResult(false, false, false, null, entry, validation, null);
        }

        var store = new SkinStore(paths, backupService: new BackupService(paths, AppVersion.Current));
        var manifest = await store.InstallAsync(folder, overwrite: true);
        var hadConfig = File.Exists(paths.ConfigFile);
        var config = await configService.LoadOrCreateAsync();
        await store.ActivateAsync(config, manifest.Id, entry);
        await SaveConfigWithBackupAsync(paths, configService, config, "skin-dev", hadConfig);

        RuntimeCommandResponse? reloadResponse = null;
        var reloaded = false;
        try
        {
            reloadResponse = await new RuntimeIpcClient().SendAsync("reloadSkin", timeout: TimeSpan.FromSeconds(1));
            reloaded = reloadResponse.Ok;
        }
        catch (Exception ex)
        {
            reloadResponse = new RuntimeCommandResponse
            {
                Ok = false,
                Error = new RuntimeCommandError("NO_HOST", ex.Message)
            };
        }

        return new SkinDevResult(true, true, reloaded, manifest.Id, entry ?? config.Skins.Entry, validation, reloadResponse);
    }

    private static void WriteDevResult(SkinDevResult result, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return;
        }

        if (!result.Validation.IsValid)
        {
            Console.WriteLine("Dev skin validation failed:");
            foreach (var error in result.Validation.Errors)
            {
                Console.WriteLine($"- {error}");
            }
        }
        else
        {
            Console.WriteLine($"Dev skin active: {result.SkinId} ({result.Entry})");
            Console.WriteLine(result.Reloaded
                ? "Running host reloaded."
                : $"Running host not reloaded: {result.ReloadResponse?.Error?.Message ?? "not running"}");
        }

        foreach (var warning in result.Validation.Warnings)
        {
            Console.WriteLine($"Warning: {warning}");
        }
    }

    private static async Task<int> InstallSkinAsync(string folder, string[] args, AppPaths paths)
    {
        var overwrite = args.Contains("--force");
        var manifest = await new SkinStore(
                paths,
                backupService: new BackupService(paths, AppVersion.Current))
            .InstallAsync(folder, overwrite);
        var result = new
        {
            installed = true,
            manifest.Id,
            manifest.Name,
            manifest.Version,
            skinPath = Path.Combine(paths.SkinsDirectory, manifest.Id)
        };

        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        }
        else
        {
            Console.WriteLine($"Installed skin: {manifest.Id}");
        }

        return 0;
    }

    private static async Task<int> ActivateSkinAsync(
        string skinId,
        string[] args,
        AppPaths paths,
        ConfigService configService)
    {
        var entry = GetOptionValue(args, "--entry");
        var hadConfig = File.Exists(paths.ConfigFile);
        var config = await configService.LoadOrCreateAsync();
        var skinStore = new SkinStore(paths);
        await skinStore.ActivateAsync(config, skinId, entry);
        await SaveConfigWithBackupAsync(paths, configService, config, "skin-activate", hadConfig);

        var result = new
        {
            activated = true,
            activeSkinId = config.Skins.ActiveSkinId,
            activeSkinEntry = config.Skins.Entry
        };

        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        }
        else
        {
            Console.WriteLine($"Activated skin: {config.Skins.ActiveSkinId} ({config.Skins.Entry})");
        }

        return 0;
    }

    private static int ListMonitors(string[] args)
    {
        var monitors = new MonitorService().GetMonitors();

        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(monitors, JsonOptions));
        }
        else
        {
            foreach (var monitor in monitors)
            {
                var primary = monitor.IsPrimary ? " primary" : "";
                Console.WriteLine($"{monitor.Id}{primary}\t{monitor.Bounds.Width}x{monitor.Bounds.Height}+{monitor.Bounds.Left}+{monitor.Bounds.Top}");
            }
        }

        return 0;
    }

    private static int ShowLogs(string[] args, AppPaths paths)
    {
        var maxLines = TryParseOptionInt32(args, "--lines") ?? 200;
        var lines = new LogService(paths).ReadLines(maxLines);

        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(lines, JsonOptions));
        }
        else
        {
            foreach (var line in lines)
            {
                Console.WriteLine(line);
            }
        }

        return 0;
    }

    private static async Task<int> PlacementDiagnosticsAsync(
        string[] args,
        AppPaths paths,
        ConfigService configService)
    {
        var config = await configService.LoadOrCreateAsync();
        PlacementDiagnostics? fallbackDiagnostics = null;
        RuntimeCommandResponse? hostResponse = null;

        try
        {
            hostResponse = await new RuntimeIpcClient().SendAsync(
                "placementDiagnostics",
                timeout: TimeSpan.FromSeconds(1));
        }
        catch
        {
            fallbackDiagnostics = new DesktopPlacementService(new LogService(paths)).BuildStaticDiagnostics(config);
        }

        if (args.Contains("--json"))
        {
            if (hostResponse is { Ok: true, Result: not null })
            {
                Console.WriteLine(hostResponse.Result.ToJsonString(JsonOptions));
            }
            else
            {
                fallbackDiagnostics ??= new DesktopPlacementService(new LogService(paths)).BuildStaticDiagnostics(config);
                Console.WriteLine(JsonSerializer.Serialize(fallbackDiagnostics, JsonOptions));
            }
        }
        else
        {
            fallbackDiagnostics ??= hostResponse is { Ok: true, Result: not null }
                ? hostResponse.Result.Deserialize<PlacementDiagnostics>(JsonOptions)
                : new DesktopPlacementService(new LogService(paths)).BuildStaticDiagnostics(config);

            Console.WriteLine($"Placement: {fallbackDiagnostics?.ConfiguredPlacementMode}");
            Console.WriteLine($"Fallback: {fallbackDiagnostics?.FallbackPlacementMode}");
            Console.WriteLine($"Avoid taskbar: {fallbackDiagnostics?.AvoidTaskbar}");
            Console.WriteLine($"Show in Alt-Tab: {fallbackDiagnostics?.ShowInAltTab}");
            Console.WriteLine($"WorkerW available: {fallbackDiagnostics?.WorkerWAttachmentAvailable}");
            Console.WriteLine($"Monitors: {fallbackDiagnostics?.Monitors.Count ?? 0}");
            Console.WriteLine($"WorkerW windows: {fallbackDiagnostics?.ShellWindows.WorkerWs.Count ?? 0}");
            Console.WriteLine($"desktop.html host windows: {fallbackDiagnostics?.HostWindows.Count ?? 0}");
        }

        if (hostResponse is { Ok: false })
        {
            return 1;
        }

        return 0;
    }

    private static async Task<int> SendRuntimeCommandAsync(
        string command,
        string[] args,
        JsonObject? parameters = null)
    {
        var response = await new RuntimeIpcClient().SendAsync(command, parameters);
        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(response, JsonOptions));
        }
        else if (response.Ok)
        {
            Console.WriteLine($"Runtime command sent: {command}");
        }
        else
        {
            Console.Error.WriteLine(response.Error?.Message ?? "Runtime command failed.");
        }

        return response.Ok ? 0 : 1;
    }

    private static int StartupStatus(string[] args)
    {
        var enabled = new StartupService().IsEnabled();
        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(new { enabled }, JsonOptions));
        }
        else
        {
            Console.WriteLine($"Startup: {(enabled ? "enabled" : "disabled")}");
        }

        return 0;
    }

    private static async Task<int> SetStartupAsync(
        bool enabled,
        string[] args,
        AppPaths paths,
        ConfigService configService)
    {
        var startupService = new StartupService();
        if (enabled)
        {
            startupService.Enable(ResolveExecutablePath());
        }
        else
        {
            startupService.Disable();
        }

        var hadConfig = File.Exists(paths.ConfigFile);
        var config = await configService.LoadOrCreateAsync();
        config.App.StartWithWindows = enabled;
        await SaveConfigWithBackupAsync(paths, configService, config, "startup-toggle", hadConfig);

        var result = new { enabled };
        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        }
        else
        {
            Console.WriteLine($"Startup {(enabled ? "enabled" : "disabled")}.");
        }

        return 0;
    }

    private static async Task<int> GetConfigAsync(string[] args, ConfigService configService)
    {
        var config = await configService.LoadOrCreateAsync();

        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(config, JsonOptions));
        }
        else
        {
            Console.WriteLine($"Safe mode: {config.App.SafeMode}");
            Console.WriteLine($"Startup: {config.App.StartWithWindows}");
            Console.WriteLine($"Placement: {config.Desktop.PlacementMode}");
            Console.WriteLine($"Active mode: {config.Skins.ActiveMode}");
            Console.WriteLine($"Active skin: {config.Skins.ActiveSkinId ?? "(none)"}");
            Console.WriteLine($"Entry: {config.Skins.Entry}");
        }

        return 0;
    }

    private static async Task<int> PatchConfigAsync(
        string patchValue,
        string[] args,
        AppPaths paths,
        ConfigService configService)
    {
        var patchText = File.Exists(patchValue)
            ? await File.ReadAllTextAsync(patchValue)
            : patchValue;

        var patch = JsonNode.Parse(patchText) as JsonObject
            ?? throw new InvalidOperationException("Config patch must be a JSON object or a path to a JSON object file.");

        var hadConfig = File.Exists(paths.ConfigFile);
        var config = ConfigPatchService.ApplyPatch(await configService.LoadOrCreateAsync(), patch);
        await SaveConfigWithBackupAsync(paths, configService, config, "config-patch", hadConfig);

        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(config, JsonOptions));
        }
        else
        {
            Console.WriteLine("Config patched.");
        }

        return 0;
    }

    private static async Task<int> SetConfigAsync(
        string path,
        string value,
        string[] args,
        AppPaths paths,
        ConfigService configService)
    {
        var node = JsonNode.Parse(value);
        var hadConfig = File.Exists(paths.ConfigFile);
        var config = ConfigPatchService.SetPath(await configService.LoadOrCreateAsync(), path, node);
        await SaveConfigWithBackupAsync(paths, configService, config, "config-set", hadConfig);

        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(config, JsonOptions));
        }
        else
        {
            Console.WriteLine($"Config set: {path}");
        }

        return 0;
    }

    private static async Task<int> SetSafeModeAsync(
        bool enabled,
        string[] args,
        AppPaths paths,
        ConfigService configService)
    {
        var hadConfig = File.Exists(paths.ConfigFile);
        var config = await configService.LoadOrCreateAsync();
        config.App.SafeMode = enabled;
        await SaveConfigWithBackupAsync(paths, configService, config, "safe-mode", hadConfig);
        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(new { safeMode = enabled }, JsonOptions));
        }
        else
        {
            Console.WriteLine($"Safe mode {(enabled ? "enabled" : "disabled")}.");
        }

        return 0;
    }

    private static async Task<int> ListBackupsAsync(string[] args, AppPaths paths)
    {
        var backups = await new BackupService(paths, AppVersion.Current).ListAsync();
        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(backups, JsonOptions));
        }
        else
        {
            foreach (var backup in backups)
            {
                Console.WriteLine($"{backup.Id}\t{backup.Kind}\t{backup.CreatedUtc:u}\t{backup.Reason}");
            }
        }

        return 0;
    }

    private static async Task<int> CreateBackupAsync(string kind, string[] args, AppPaths paths)
    {
        var reason = GetOptionValue(args, "--reason") ?? "manual";
        var skinId = GetOptionValue(args, "--skin-id");
        var manifest = await new BackupService(paths, AppVersion.Current)
            .CreateBackupAsync(kind, reason, skinId);

        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(manifest, JsonOptions));
        }
        else
        {
            Console.WriteLine($"Created backup: {manifest.Id}");
        }

        return 0;
    }

    private static async Task<int> RestoreBackupAsync(string backupId, string[] args, AppPaths paths)
    {
        var result = await new BackupService(paths, AppVersion.Current).RestoreAsync(backupId);
        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        }
        else
        {
            Console.WriteLine($"Restored backup: {result.Restored.Id}");
            if (result.SafetyBackup is not null)
            {
                Console.WriteLine($"Safety backup: {result.SafetyBackup.Id}");
            }
        }

        return 0;
    }

    private static async Task<int> PruneBackupsAsync(string[] args, AppPaths paths)
    {
        var keep = TryParseOptionInt32(args, "--keep")
            ?? throw new InvalidOperationException("backup prune requires --keep <count>.");
        var result = await new BackupService(paths, AppVersion.Current).PruneAsync(keep);

        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        }
        else
        {
            Console.WriteLine($"Deleted backups: {result.Deleted.Count}");
        }

        return 0;
    }

    private static async Task SaveConfigWithBackupAsync(
        AppPaths paths,
        ConfigService configService,
        DesktopHtmlConfig config,
        string reason,
        bool createBackup)
    {
        if (createBackup && File.Exists(paths.ConfigFile))
        {
            await new BackupService(paths, AppVersion.Current).CreateConfigBackupAsync(reason);
        }

        await configService.SaveAsync(config);
    }

    private static int WriteHelp()
    {
        Console.WriteLine("""
desktop-html commands:
  desktop-html --version
  desktop-html status [--json]
  desktop-html skin list [--json]
  desktop-html skin validate <folder> [--json]
  desktop-html skin validate <folder> --strict [--json]
  desktop-html skin scaffold <skin-id> [--template blank|classic|launcher|dashboard] [--out folder] [--force] [--json]
  desktop-html skin dev <folder> [--entry index.html] [--watch] [--no-strict] [--json]
  desktop-html skin install <folder> [--force] [--json]
  desktop-html skin activate <skin-id> [--entry index.html] [--json]
  desktop-html skin reload [--json]
  desktop-html monitor list [--json]
  desktop-html monitor assign <monitor-id> <skin-id> [--entry index.html] [--json]
  desktop-html monitor clear <monitor-id> [--json]
  desktop-html monitor reload <monitor-id> [--json]
  desktop-html monitor mode single-monitor|per-monitor|spanning [--json]
  desktop-html monitor span <skin-id> [--monitors DISPLAY1,DISPLAY2] [--entry index.html] [--json]
  desktop-html placement diagnostics [--json]
  desktop-html placement reapply [--json]
  desktop-html logs [--lines 200] [--json]
  desktop-html ping [--json]
  desktop-html open-settings [--json]
  desktop-html exit [--json]
  desktop-html startup status [--json]
  desktop-html startup on|off [--json]
  desktop-html config get [--json]
  desktop-html config patch <json-or-file> [--json]
  desktop-html config set <path> <json-value> [--json]
  desktop-html backup list [--json]
  desktop-html backup create config|skin|storage [--skin-id <id>] [--reason text] [--json]
  desktop-html backup restore <backup-id> [--json]
  desktop-html backup prune --keep <count> [--json]
  desktop-html safe-mode on|off [--json]
""");
        return 0;
    }

    private static string? GetOptionValue(string[] args, string optionName)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], optionName, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static int? TryParseOptionInt32(string[] args, string optionName)
    {
        var value = GetOptionValue(args, optionName);
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static int WriteError(string message, int exitCode)
    {
        Console.Error.WriteLine(message);
        return exitCode;
    }

    private static string ResolveExecutablePath()
    {
        return Environment.ProcessPath
            ?? System.Reflection.Assembly.GetEntryAssembly()?.Location
            ?? throw new InvalidOperationException("Could not determine the desktop-html executable path.");
    }
}
