# desktop.html Config Reference

desktop.html stores runtime config at:

```text
%AppData%\desktop-html\config.json
```

The CLI is the preferred way for agents to inspect and patch config.

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe config get --json
```

## Shape

```json
{
  "schemaVersion": 1,
  "app": {
    "startWithWindows": false,
    "showTrayIcon": true,
    "safeMode": false,
    "logLevel": "info"
  },
  "desktop": {
    "placementMode": "behind-normal-windows",
    "fallbackPlacementMode": "behind-normal-windows",
    "avoidTaskbar": true,
    "showInAltTab": false,
    "showInTaskbar": false
  },
  "performance": {
    "pauseWhenFullscreenAppActive": true,
    "pauseWhenOnBattery": false,
    "targetFrameRate": null
  },
  "skins": {
    "activeMode": "single-monitor",
    "activeSkinId": "desktop-html.sample.launcher",
    "entry": "index.html",
    "perMonitor": {},
    "spanning": {
      "skinId": "desktop-html.sample.launcher",
      "entry": "index.html",
      "monitors": []
    }
  }
}
```

## Notes for Agents

- Prefer skin CLI commands over directly editing config.
- Use `skin activate` for single-monitor mode.
- Use `monitor assign` for per-monitor mode.
- Use `monitor span` for spanning mode.
- Use `config set app.safeMode false --json` to leave safe mode from CLI.
- `config patch`, `config set`, safe-mode toggles, startup toggles, and monitor assignment changes automatically create config backups when an existing config file is present.
- Use `backup list --json` and `backup restore <backup-id> --json` to inspect or restore backups under `%AppData%\desktop-html\backups`.
- Avoid writing personal paths into sample skins unless the skin is explicitly personal/local-only.
