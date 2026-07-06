using Velopack;
using Velopack.Sources;

namespace DesktopHtml.App;

/// <summary>
/// Velopack-backed self-update against the project's GitHub releases.
/// Only active when running from an installed build; no-ops in dev builds.
/// </summary>
public sealed class UpdateService
{
    public const string RepoUrl = "https://github.com/cpc2/desktop.html";

    private readonly UpdateManager _manager = new(new GithubSource(RepoUrl, accessToken: null, prerelease: false));

    public bool IsInstalled => _manager.IsInstalled;

    public string? InstalledVersion => _manager.CurrentVersion?.ToString();

    public Task<UpdateInfo?> CheckForUpdatesAsync() => _manager.CheckForUpdatesAsync();

    public Task DownloadUpdatesAsync(UpdateInfo updateInfo) => _manager.DownloadUpdatesAsync(updateInfo);

    /// <summary>
    /// Registers the downloaded update to apply after this process exits, restarting the app.
    /// The caller is responsible for shutting down cleanly afterwards.
    /// </summary>
    public void StageUpdateForRestart(UpdateInfo updateInfo)
        => _manager.WaitExitThenApplyUpdates(updateInfo, silent: true, restart: true);
}
