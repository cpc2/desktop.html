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
}
