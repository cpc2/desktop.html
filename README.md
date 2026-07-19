I should preface this by saying this is "vibe coded", I haven't touched any code myself. It also allows you to run any command from the skin, so use this at your own risk. I had this done for my personal use because I think a desktop works best as a fully customizable website. Codex wrote the rest of this readme so I don't swear by it, but it should explain more in detail how it works and how to set it up.

---

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
- Current-user Windows startup registration through `desktop-html startup on|off`.

## Install

Download the latest `DesktopHtml-win-Setup.exe` from the [releases page](https://github.com/cpc2/desktop.html/releases) and run it. The installer adds a Start Menu entry, and the app keeps itself up to date automatically by checking GitHub releases on startup (the .NET 8 Desktop Runtime is installed automatically if missing).

Releases are cut by pushing a version tag (for example `v0.2.0`); a GitHub Actions workflow builds, packages with [Velopack](https://velopack.io), and publishes the release.

## Build

```powershell
dotnet build .\desktop-html.sln
dotnet test .\desktop-html.sln
```

## Documentation

- [Project website](https://cpc2.github.io/desktop.html/)
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
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe
```

The prototype intentionally uses a console-capable executable so CLI output works:

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe status --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe monitor list --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe monitor assign "\\.\DISPLAY1" desktop-html.sample.launcher --entry index.html --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe monitor clear "\\.\DISPLAY1" --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe monitor reload "\\.\DISPLAY1" --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe monitor span desktop-html.sample.launcher --monitors "\\.\DISPLAY1,\\.\DISPLAY2" --entry index.html --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe monitor mode single-monitor --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe placement diagnostics --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe placement reapply --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe logs --lines 50 --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe startup status --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe skin list --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe skin validate "$env:APPDATA\desktop-html\skins\desktop-html.sample.launcher" --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe skin install C:\path\to\skin --force --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe skin activate desktop-html.sample.launcher --entry index.html --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe skin reload --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe open-settings --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe config get --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe config set app.safeMode false --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe update check --json
```

## Trust Model

desktop.html skins are local programs written in HTML, CSS, and JavaScript. They can open files, run commands, start applications, and access network resources through the desktop bridge. Only install skins from sources you trust.

## License

desktop.html is licensed under GPL-3.0-or-later.
