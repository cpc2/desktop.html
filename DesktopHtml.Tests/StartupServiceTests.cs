using DesktopHtml.Core.Startup;

namespace DesktopHtml.Tests;

public sealed class StartupServiceTests
{
    [Fact]
    public void BuildRunValue_QuotesAbsoluteExecutablePath()
    {
        var path = Path.Combine(Path.GetTempPath(), "desktop-html test", "desktop-html.exe");

        var value = StartupService.BuildRunValue(path);

        Assert.StartsWith("\"", value);
        Assert.EndsWith("\"", value);
        Assert.Contains("desktop-html.exe", value);
    }

    [Fact]
    public void BuildRunValue_RejectsBlankPath()
    {
        Assert.Throws<ArgumentException>(() => StartupService.BuildRunValue(""));
    }

    [Theory]
    [InlineData(new byte[] { 0x03, 0, 0, 0, 0xEC, 0xF5, 0x29, 0x1C, 0x98, 0x2E, 0xDD, 0x01 }, true)]
    [InlineData(new byte[] { 0x01, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, false)]
    [InlineData(new byte[] { 0x06, 0, 0, 0 }, false)]
    [InlineData(new byte[0], false)]
    public void IsApprovalDisabled_ReadsLowBitOfFirstByte(byte[] value, bool expected)
    {
        Assert.Equal(expected, StartupService.IsApprovalDisabled(value));
    }

    [Fact]
    public void IsApprovalDisabled_TreatsMissingValueAsEnabled()
    {
        Assert.False(StartupService.IsApprovalDisabled(null));
    }
}
