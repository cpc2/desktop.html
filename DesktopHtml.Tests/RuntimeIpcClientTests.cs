using DesktopHtml.Core.Ipc;

namespace DesktopHtml.Tests;

public sealed class RuntimeIpcClientTests
{
    [Fact]
    public async Task SendAsync_WhenServerIsMissing_ReturnsClearError()
    {
        var client = new RuntimeIpcClient($"desktop-html-test-missing-{Guid.NewGuid():N}");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SendAsync("ping", timeout: TimeSpan.FromMilliseconds(50)));

        Assert.Contains("running desktop.html process", ex.Message);
    }

    [Fact]
    public void RuntimeCommandRequest_DefaultId_IsPopulated()
    {
        var request = new RuntimeCommandRequest { Command = "ping" };

        Assert.False(string.IsNullOrWhiteSpace(request.Id));
        Assert.Equal("ping", request.Command);
    }
}
