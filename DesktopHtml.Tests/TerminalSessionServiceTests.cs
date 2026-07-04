using System.Text;
using DesktopHtml.Core.Terminal;

namespace DesktopHtml.Tests;

public sealed class TerminalSessionServiceTests
{
    [Fact]
    public async Task Start_StreamsOutputAndRaisesExit()
    {
        using var service = new TerminalSessionService();
        var output = new StringBuilder();
        var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        var sessionId = service.Start(
            new TerminalStartOptions
            {
                Command = "cmd.exe",
                Args = ["/d", "/c", "echo terminal-test-output"]
            },
            (_, text) => { lock (output) { output.Append(text); } },
            (_, exitCode) => exited.TrySetResult(exitCode));

        Assert.False(string.IsNullOrWhiteSpace(sessionId));

        var exitCode = await exited.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(0, exitCode);

        // Output callbacks race the exit callback slightly; give them a moment.
        await Task.Delay(200);
        lock (output)
        {
            Assert.Contains("terminal-test-output", output.ToString());
        }
    }

    [Fact]
    public async Task Write_FeedsStdin()
    {
        using var service = new TerminalSessionService();
        var output = new StringBuilder();
        var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        var sessionId = service.Start(
            new TerminalStartOptions
            {
                SessionId = "stdin-test",
                Command = "cmd.exe",
                Args = ["/d", "/q", "/k"]
            },
            (_, text) => { lock (output) { output.Append(text); } },
            (_, exitCode) => exited.TrySetResult(exitCode));

        Assert.True(service.Write(sessionId, "echo stdin-round-trip\r\nexit\r\n"));

        await exited.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await Task.Delay(200);
        lock (output)
        {
            Assert.Contains("stdin-round-trip", output.ToString());
        }
    }

    [Fact]
    public async Task Pty_ProvidesRealConsoleAndResize()
    {
        using var service = new TerminalSessionService();
        var output = new StringBuilder();
        var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        var sessionId = service.Start(
            new TerminalStartOptions
            {
                Command = "cmd.exe",
                Args = ["/d", "/q", "/k"],
                Pty = true,
                Cols = 100,
                Rows = 30
            },
            (_, text) => { lock (output) { output.Append(text); } },
            (_, exitCode) => exited.TrySetResult(exitCode));

        Assert.True(service.Resize(sessionId, 120, 40));
        Assert.True(service.Write(sessionId, "echo pty-round-trip\r"));

        // Wait for the echoed output before exiting.
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            lock (output)
            {
                if (output.ToString().Contains("pty-round-trip"))
                {
                    break;
                }
            }

            await Task.Delay(100);
        }

        lock (output)
        {
            Assert.Contains("pty-round-trip", output.ToString());
        }

        service.Write(sessionId, "exit\r");
        await exited.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void Write_ReturnsFalseForUnknownSession()
    {
        using var service = new TerminalSessionService();
        Assert.False(service.Write("missing", "hello"));
    }

    [Fact]
    public void Resize_ReturnsFalseForStdioSession()
    {
        using var service = new TerminalSessionService();
        var sessionId = service.Start(
            new TerminalStartOptions { Command = "cmd.exe", Args = ["/d", "/q", "/k"] },
            (_, _) => { },
            (_, _) => { });

        Assert.False(service.Resize(sessionId, 100, 30));
        service.KillAll();
    }
}
