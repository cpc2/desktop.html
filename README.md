# desktop.html

`desktop.html` is a Windows 11 desktop runtime that loads trusted local HTML/CSS/JavaScript skins into a WebView2-powered desktop surface.

This repository currently contains the first prototype slice:

- WPF/WebView2 host windows sized to monitor work areas, with single-monitor, per-monitor, and spanning modes.
- Conservative desktop placement service with no-taskbar/no-Alt-Tab Stage A placement, WorkerW/Progman opt-in experiment, fallback, diagnostics, and shell-change reapply.
- AppData config under `%AppData%/desktop-html/config.json`.
- Bundled sample skin installed under `%AppData%/desktop-html/skins/desktop-html.sample.launcher`.
- Promise-based `window.desktop` bridge bootstrap.
- Initial bridge methods: `getRuntimeInfo`, `getVersion`, `getCapabilities`, `getConfig`, `setConfigPatch`, monitor geometry APIs, `log`, path/url open helpers, `shellExecute`, `run`, `runCommandLine`, `runPowerShell`, file helpers, storage helpers, clipboard helpers, diagnostics helpers, and host commands such as `reloadSkin` and `exit`.
- CLI commands for status, placement diagnostics/reapply, monitor listing, monitor assignment, skin listing, skin validation, skin install, skin activation, version, and safe mode toggling.
- Tray menu controls for settings, skin reload, safe mode, logs, and exit.
- Internal HTML settings window for status, monitors, installed skins, skin install/activation, monitor assignment, spanning mode, startup/pause toggles, recent logs, reload, safe mode, and log access.
- Repo-local test skin at `samples/phase11-control-room`, installable through settings or CLI.
- Repo-local generated launcher skin at `samples/desktop-nexus`, built from the current Windows Desktop with extracted shell icons.
- Current-user Windows startup registration through `desktop-html startup on|off`.

## Build

```powershell
dotnet build .\desktop-html.sln
dotnet test .\desktop-html.sln
```

## Documentation

- [Skin authoring guide](docs/skin-authoring.md)
- [Bridge API reference](docs/bridge-api.md)
- [CLI reference](docs/cli.md)
- [Config reference](docs/config.md)
- [Security and trust model](docs/security.md)
- [Security notice](SECURITY.md)
- [Sample skin walkthrough](docs/sample-skin-walkthrough.md)
- [Agent skin prompt](docs/agent-skin-prompt.md)

## Run

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe
```

The prototype intentionally uses a console-capable executable so CLI output works:

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe status --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe monitor list --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe monitor assign "\\.\DISPLAY1" desktop-html.sample.launcher --entry index.html --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe monitor clear "\\.\DISPLAY1" --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe monitor reload "\\.\DISPLAY1" --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe monitor span desktop-html.sample.launcher --monitors "\\.\DISPLAY1,\\.\DISPLAY2" --entry index.html --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe monitor mode single-monitor --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe placement diagnostics --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe placement reapply --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe logs --lines 50 --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe startup status --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin list --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin validate "$env:APPDATA\desktop-html\skins\desktop-html.sample.launcher" --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin validate .\samples\phase11-control-room --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin install .\samples\phase11-control-room --force --json
.\samples\desktop-nexus\tools\generate-desktop-nexus.ps1
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin validate .\samples\desktop-nexus --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin install .\samples\desktop-nexus --force --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin install C:\path\to\skin --force --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin activate desktop-html.sample.launcher --entry index.html --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin reload --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe open-settings --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe config get --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe config set app.safeMode false --json
```

## Desktop Nexus Input Testing

`Desktop Nexus` includes a collapsible diagnostics overlay in the lower-left corner. Use **Diagnostics** to expand it and **Toggle launch** to switch between double-click and single-click launch modes.

For input reliability checks:

1. Start `desktop-html.exe` and activate `Desktop Nexus`.
2. Run `.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe placement diagnostics --json` and confirm `hostWindows` is not empty.
3. Click and double-click a few tiles while watching the overlay counters.
4. Compare behavior with normal Windows desktop icons visible.
5. Hide Windows desktop icons with Desktop → View → Show desktop icons, then repeat the same clicks.

## Trust Model

desktop.html skins are local programs written in HTML, CSS, and JavaScript. They can open files, run commands, start applications, and access network resources through the desktop bridge. Only install skins from sources you trust.
