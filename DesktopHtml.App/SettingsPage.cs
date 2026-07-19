using System.IO;

namespace DesktopHtml.App;

/// <summary>Serves the settings UI from SettingsPage.html, which is embedded
/// into the assembly so the published app stays a single self-contained exe.</summary>
public static class SettingsPage
{
    public static string Html { get; } = LoadHtml();

    private static string LoadHtml()
    {
        using var stream = typeof(SettingsPage).Assembly.GetManifestResourceStream("DesktopHtml.App.SettingsPage.html")
            ?? throw new InvalidOperationException("Embedded resource 'DesktopHtml.App.SettingsPage.html' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
