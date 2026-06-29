using DesktopHtml.Core.Configuration;
using DesktopHtml.Core.Logging;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace DesktopHtml.App;

public sealed class TrayService : IDisposable
{
    private readonly IDesktopHostActions _hostActions;
    private readonly DesktopHtmlConfig _config;
    private readonly LogService _logService;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _safeModeItem;
    private bool _disposed;

    public TrayService(IDesktopHostActions hostActions, DesktopHtmlConfig config, LogService logService)
    {
        _hostActions = hostActions;
        _config = config;
        _logService = logService;

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Settings", null, async (_, _) => await RunMenuActionAsync("openSettings", _hostActions.OpenSettingsAsync));
        menu.Items.Add("Reload Skin", null, async (_, _) => await RunMenuActionAsync("reloadSkin", _hostActions.ReloadSkinAsync));
        _safeModeItem = new Forms.ToolStripMenuItem("Safe Mode")
        {
            Checked = _config.App.SafeMode,
            CheckOnClick = true
        };
        _safeModeItem.Click += async (_, _) => await ToggleSafeModeAsync();
        menu.Items.Add(_safeModeItem);
        menu.Items.Add("Open Logs", null, async (_, _) => await RunMenuActionAsync("openLogs", _hostActions.OpenLogsAsync));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, async (_, _) => await RunMenuActionAsync("exit", _hostActions.ExitAsync));

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Text = BuildTooltip(config),
            Visible = config.App.ShowTrayIcon
        };

        _notifyIcon.DoubleClick += async (_, _) => await RunMenuActionAsync("openSettings", _hostActions.OpenSettingsAsync);
    }

    private static string BuildTooltip(DesktopHtmlConfig config)
    {
        var skin = string.IsNullOrWhiteSpace(config.Skins.ActiveSkinId)
            ? "No active skin"
            : config.Skins.ActiveSkinId;
        return $"desktop.html - {skin}"[..Math.Min(63, $"desktop.html - {skin}".Length)];
    }

    private async Task ToggleSafeModeAsync()
    {
        try
        {
            await _hostActions.SetSafeModeAsync(_safeModeItem.Checked).ConfigureAwait(false);
            await _logService.InfoAsync("tray", "Safe mode toggled.", new { enabled = _safeModeItem.Checked })
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _safeModeItem.Checked = !_safeModeItem.Checked;
            await _logService.ErrorAsync("tray", "Safe mode toggle failed.", new { error = ex.Message })
                .ConfigureAwait(false);
        }
    }

    private async Task RunMenuActionAsync(string action, Func<Task> callback)
    {
        try
        {
            await _logService.InfoAsync("tray", "Tray command started.", new { action }).ConfigureAwait(false);
            await callback().ConfigureAwait(false);
            await _logService.InfoAsync("tray", "Tray command completed.", new { action }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _logService.ErrorAsync("tray", "Tray command failed.", new { action, error = ex.Message })
                .ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _disposed = true;
    }
}
