using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopHtml.Core.Ipc;
using DesktopHtml.Core.Logging;

namespace DesktopHtml.App;

public sealed class RuntimeCommandServer : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IDesktopHostActions _hostActions;
    private readonly LogService _logService;
    private readonly CancellationTokenSource _stop = new();
    private Task? _serverTask;

    public RuntimeCommandServer(IDesktopHostActions hostActions, LogService logService)
    {
        _hostActions = hostActions;
        _logService = logService;
    }

    public void Start()
    {
        _serverTask ??= Task.Run(() => RunAsync(_stop.Token));
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        _ = _logService.InfoAsync("ipc", "Runtime command server started.");

        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                RuntimeIpcDefaults.PipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                await HandleConnectionAsync(pipe, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                await _logService.ErrorAsync("ipc", "Runtime command server error.", new
                {
                    error = ex.Message,
                    type = ex.GetType().Name
                }).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(pipe, leaveOpen: true);
        await using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };

        var requestLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        var request = string.IsNullOrWhiteSpace(requestLine)
            ? null
            : JsonSerializer.Deserialize<RuntimeCommandRequest>(requestLine, JsonOptions);

        if (request is null || string.IsNullOrWhiteSpace(request.Id))
        {
            await WriteResponseAsync(writer, new RuntimeCommandResponse
            {
                Ok = false,
                Error = new RuntimeCommandError("INVALID_REQUEST", "IPC request is invalid.")
            }).ConfigureAwait(false);
            return;
        }

        await _logService.InfoAsync("ipc", "Runtime command received.", new { request.Command })
            .ConfigureAwait(false);

        RuntimeCommandResponse response;
        try
        {
            var result = await ExecuteAsync(request).ConfigureAwait(false);
            response = new RuntimeCommandResponse
            {
                Id = request.Id,
                Ok = true,
                Result = result
            };
        }
        catch (Exception ex)
        {
            response = new RuntimeCommandResponse
            {
                Id = request.Id,
                Ok = false,
                Error = new RuntimeCommandError("COMMAND_FAILED", ex.Message)
            };
        }

        await WriteResponseAsync(writer, response).ConfigureAwait(false);
    }

    private async Task<JsonNode?> ExecuteAsync(RuntimeCommandRequest request)
    {
        switch (request.Command)
        {
            case "ping":
                return JsonSerializer.SerializeToNode(new { running = true }, JsonOptions);
            case "reloadSkin":
                await _hostActions.ReloadSkinAsync().ConfigureAwait(false);
                return JsonSerializer.SerializeToNode(new { reloaded = true }, JsonOptions);
            case "reloadMonitorSkin":
                var monitorId = GetRequiredString(request.Params, "monitorId");
                await _hostActions.ReloadMonitorSkinAsync(monitorId).ConfigureAwait(false);
                return JsonSerializer.SerializeToNode(new { reloaded = true, monitorId }, JsonOptions);
            case "openSettings":
                await _hostActions.OpenSettingsAsync().ConfigureAwait(false);
                return JsonSerializer.SerializeToNode(new { opened = true }, JsonOptions);
            case "placementDiagnostics":
                var diagnostics = await _hostActions.GetPlacementDiagnosticsAsync().ConfigureAwait(false);
                return JsonSerializer.SerializeToNode(diagnostics, JsonOptions);
            case "placementReapply":
                var reapplyResult = await _hostActions.ReapplyPlacementAsync("cli").ConfigureAwait(false);
                return JsonSerializer.SerializeToNode(reapplyResult, JsonOptions);
            case "exit":
                _ = _hostActions.ExitAsync();
                return JsonSerializer.SerializeToNode(new { exiting = true }, JsonOptions);
            default:
                throw new InvalidOperationException($"Unknown runtime command '{request.Command}'.");
        }
    }

    private static string GetRequiredString(JsonObject? parameters, string propertyName)
    {
        if (parameters is null
            || !parameters.TryGetPropertyValue(propertyName, out var value)
            || value is null)
        {
            throw new InvalidOperationException($"Runtime command requires parameter '{propertyName}'.");
        }

        return value.GetValue<string>();
    }

    private static Task WriteResponseAsync(StreamWriter writer, RuntimeCommandResponse response) =>
        writer.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions));

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        if (_serverTask is not null)
        {
            try
            {
                await _serverTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _stop.Dispose();
    }
}
