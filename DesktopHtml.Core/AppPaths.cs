namespace DesktopHtml.Core;

public sealed record AppPaths(
    string Root,
    string ConfigFile,
    string StateFile,
    string LogsDirectory,
    string SkinsDirectory,
    string StorageDirectory,
    string BackupsDirectory,
    string WebViewUserDataDirectory)
{
    public static AppPaths CreateDefault()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "desktop-html");

        return new AppPaths(
            root,
            Path.Combine(root, "config.json"),
            Path.Combine(root, "state.json"),
            Path.Combine(root, "logs"),
            Path.Combine(root, "skins"),
            Path.Combine(root, "storage"),
            Path.Combine(root, "backups"),
            Path.Combine(root, "webview2"));
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(SkinsDirectory);
        Directory.CreateDirectory(StorageDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(WebViewUserDataDirectory);
    }
}
