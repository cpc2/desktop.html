namespace DesktopHtml.Core.Placement;

public sealed record ShellSnapshot(
    string? ProgmanHandle,
    string? ShellDllDefViewHandle,
    string? WallpaperWorkerWHandle,
    int WorkerWCount,
    string Signature)
{
    public bool IsReady => ProgmanHandle is not null && ShellDllDefViewHandle is not null;

    public bool HasChanged(ShellSnapshot? previous) =>
        previous is null || !string.Equals(Signature, previous.Signature, StringComparison.Ordinal);

    public static ShellSnapshot Create(
        string? progmanHandle,
        string? shellDllDefViewHandle,
        string? wallpaperWorkerWHandle,
        int workerWCount)
    {
        var normalizedWorkerWCount = Math.Max(0, workerWCount);
        var signature = string.Join(
            "|",
            Normalize(progmanHandle),
            Normalize(shellDllDefViewHandle),
            Normalize(wallpaperWorkerWHandle),
            normalizedWorkerWCount.ToString(System.Globalization.CultureInfo.InvariantCulture));

        return new ShellSnapshot(
            Normalize(progmanHandle),
            Normalize(shellDllDefViewHandle),
            Normalize(wallpaperWorkerWHandle),
            normalizedWorkerWCount,
            signature);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
