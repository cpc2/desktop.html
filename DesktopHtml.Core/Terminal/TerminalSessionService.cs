using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using DesktopHtml.Core.Logging;

namespace DesktopHtml.Core.Terminal;

public sealed class TerminalStartOptions
{
    public string? SessionId { get; set; }
    public string Command { get; set; } = "wsl.exe";
    public IReadOnlyList<string> Args { get; set; } = [];
    public string? WorkingDirectory { get; set; }

    /// <summary>Attach the process to a ConPTY pseudoconsole so it sees a real
    /// TTY (prompts, colors, cursor addressing, resize). Output is a VT byte
    /// stream, so the renderer must handle full escape sequences.</summary>
    public bool Pty { get; set; }

    public int Cols { get; set; } = 80;
    public int Rows { get; set; } = 24;
}

/// <summary>
/// Owns bridge-launched terminal processes in two flavors: plain redirected
/// stdio (default) and ConPTY pseudoconsole (<see cref="TerminalStartOptions.Pty"/>).
/// Sessions are page-scoped — call <see cref="KillAll"/> when the page
/// navigates away so orphaned shells do not accumulate across skin reloads.
/// </summary>
public sealed class TerminalSessionService : IDisposable
{
    private readonly ConcurrentDictionary<string, Session> _sessions = new();
    private readonly LogService? _log;

    public TerminalSessionService(LogService? log = null)
    {
        _log = log;
    }

    /// <summary>Starts a terminal session and begins streaming its output.</summary>
    /// <returns>The session id.</returns>
    public string Start(TerminalStartOptions options, Action<string, string> onOutput, Action<string, int> onExit)
    {
        var id = string.IsNullOrWhiteSpace(options.SessionId) ? Guid.NewGuid().ToString("n") : options.SessionId!;

        return options.Pty
            ? StartPty(id, options, onOutput, onExit)
            : StartStdio(id, options, onOutput, onExit);
    }

    /// <summary>Resizes a pty session. Returns false for unknown or stdio sessions.</summary>
    public bool Resize(string sessionId, int cols, int rows)
    {
        if (!_sessions.TryGetValue(sessionId, out var session) || session.Resize is null)
        {
            return false;
        }

        try
        {
            session.Resize(cols, rows);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Writes input to a session's stdin. Returns false if the session is gone.</summary>
    public bool Write(string sessionId, string text)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return false;
        }

        try
        {
            session.WriteInput(text);
            return true;
        }
        catch
        {
            // Stream closed under us; the exit watcher will clean up.
            return false;
        }
    }

    /// <summary>Kills every live session (page navigated away or host closing).</summary>
    public void KillAll()
    {
        foreach (var session in _sessions.Values)
        {
            try
            {
                session.Kill();
            }
            catch
            {
            }
        }
    }

    public void Dispose() => KillAll();

    private string StartStdio(string id, TerminalStartOptions options, Action<string, string> onOutput, Action<string, int> onExit)
    {
        var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = options.Command,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = options.WorkingDirectory ?? ""
        };

        foreach (var arg in options.Args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            _ = _log?.ErrorAsync("terminal", $"Failed to start terminal process '{options.Command}': {ex.Message}");
            process.Dispose();
            throw;
        }

        _ = _log?.InfoAsync("terminal", $"Terminal session started (stdio). SessionId: {id}, PID: {process.Id}, Command: {options.Command}");

        _sessions[id] = new Session(
            id,
            text =>
            {
                process.StandardInput.Write(text);
                process.StandardInput.Flush();
            },
            () =>
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            },
            Resize: null);

        _ = Task.Run(() => StreamCharsAsync(id, process.StandardOutput, onOutput));
        _ = Task.Run(() => StreamCharsAsync(id, process.StandardError, onOutput));
        _ = Task.Run(async () =>
        {
            var exitCode = -1;
            try
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
                exitCode = process.ExitCode;
                _ = _log?.InfoAsync("terminal", $"Terminal session exited. SessionId: {id}, ExitCode: {exitCode}");
            }
            catch (Exception ex)
            {
                _ = _log?.ErrorAsync("terminal", $"Error waiting for terminal exit. SessionId: {id}: {ex.Message}");
            }
            finally
            {
                _sessions.TryRemove(id, out _);
                InvokeExit(onExit, id, exitCode);
            }
        });

        return id;
    }

    private string StartPty(string id, TerminalStartOptions options, Action<string, string> onOutput, Action<string, int> onExit)
    {
        PseudoConsoleProcess pty;
        try
        {
            pty = PseudoConsoleProcess.Start(options.Command, options.Args, options.WorkingDirectory, options.Cols, options.Rows);
        }
        catch (Exception ex)
        {
            _ = _log?.ErrorAsync("terminal", $"Failed to start pty session '{options.Command}': {ex.Message}");
            throw;
        }

        _ = _log?.InfoAsync("terminal", $"Terminal session started (pty {options.Cols}x{options.Rows}). SessionId: {id}, PID: {pty.ProcessId}, Command: {options.Command}");

        _sessions[id] = new Session(
            id,
            text =>
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                pty.Input.Write(bytes, 0, bytes.Length);
                pty.Input.Flush();
            },
            pty.Kill,
            (cols, rows) => pty.Resize(cols, rows));

        _ = Task.Run(() => StreamBytesAsync(id, pty.Output, onOutput));
        _ = Task.Run(async () =>
        {
            var exitCode = -1;
            try
            {
                exitCode = await pty.WaitForExitAsync().ConfigureAwait(false);
                _ = _log?.InfoAsync("terminal", $"Terminal session exited. SessionId: {id}, ExitCode: {exitCode}");
            }
            catch (Exception ex)
            {
                _ = _log?.ErrorAsync("terminal", $"Error waiting for pty exit. SessionId: {id}: {ex.Message}");
            }
            finally
            {
                _sessions.TryRemove(id, out _);

                // Give the output reader a moment to drain the final bytes
                // before the pseudoconsole is torn down.
                await Task.Delay(150).ConfigureAwait(false);
                pty.Dispose();
                InvokeExit(onExit, id, exitCode);
            }
        });

        return id;
    }

    private static void InvokeExit(Action<string, int> onExit, string id, int exitCode)
    {
        try
        {
            onExit(id, exitCode);
        }
        catch
        {
        }
    }

    private static async Task StreamCharsAsync(string id, StreamReader reader, Action<string, string> onOutput)
    {
        var buffer = new char[4096];
        try
        {
            // Read to EOF rather than checking HasExited so buffered output
            // written just before exit is not dropped.
            while (true)
            {
                var read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                onOutput(id, new string(buffer, 0, read));
            }
        }
        catch
        {
            // Reader torn down with the process; nothing to do.
        }
    }

    private static async Task StreamBytesAsync(string id, Stream stream, Action<string, string> onOutput)
    {
        var buffer = new byte[4096];
        var chars = new char[8192];
        var decoder = Encoding.UTF8.GetDecoder();
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(buffer).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                var charCount = decoder.GetChars(buffer, 0, read, chars, 0);
                if (charCount > 0)
                {
                    onOutput(id, new string(chars, 0, charCount));
                }
            }
        }
        catch
        {
            // Pipe closed with the pseudoconsole; nothing to do.
        }
    }

    private sealed record Session(string Id, Action<string> WriteInput, Action Kill, Action<int, int>? Resize);
}
