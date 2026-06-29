namespace DesktopHtml.Core.Skins;

public sealed class SkinManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "0.1.0";
    public string Author { get; set; } = "Unknown";
    public string? Description { get; set; }
    public string Entry { get; set; } = "index.html";
    public Dictionary<string, string> Entries { get; set; } = new();
    public string? MinimumDesktopHtmlVersion { get; set; }
    public SkinPermissions Permissions { get; set; } = new();
}

public sealed class SkinPermissions
{
    public bool FullTrust { get; set; } = true;
    public bool Network { get; set; } = true;
    public bool RawExecution { get; set; } = true;
}
