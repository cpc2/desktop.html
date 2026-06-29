using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DesktopHtml.Core.Ipc;

public sealed class RuntimeIpcClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _pipeName;

    public RuntimeIpcClient(string pipeName = RuntimeIpcDefaults.PipeName)
    {
        _pipeName = pipeName;
    }

    public async Task<RuntimeCommandResponse> SendAsync(
        string command,
        JsonObject? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var request = new RuntimeCommandRequest
        {
            Command = command,
            Params = parameters
        };

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout ?? TimeSpan.FromSeconds(2));

            await using var pipe = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            await pipe.ConnectAsync(timeoutCts.Token).ConfigureAwait(false);

            await using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, leaveOpen: true);

            await writer.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions)).ConfigureAwait(false);
            var responseLine = await reader.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(responseLine))
            {
                throw new InvalidOperationException("The running desktop.html process closed the IPC connection without responding.");
            }

            var response = JsonSerializer.Deserialize<RuntimeCommandResponse>(responseLine, JsonOptions)
                ?? throw new InvalidOperationException("The running desktop.html process returned an invalid IPC response.");

            return response;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("Could not connect to a running desktop.html process. Is desktop.html open?");
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException("Could not communicate with the running desktop.html process.", ex);
        }
        catch (TimeoutException ex)
        {
            throw new InvalidOperationException("Timed out while communicating with the running desktop.html process.", ex);
        }
    }
}
