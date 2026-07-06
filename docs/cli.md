# desktop.html CLI Reference

The app executable also acts as the CLI:

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe <command>
```

Most commands support `--json` for agent-friendly output.

## Common Skin Workflow

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe skin validate .\my-skin --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe skin install .\my-skin --force --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe skin activate my.skin --entry index.html --json
```

If desktop.html is already running:

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe skin reload --json
```

## Skin Authoring Toolkit

These commands help create and iterate on skins from the CLI.

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe skin scaffold my.skin --template classic --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe skin scaffold my.rabbits --template rabbits --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe skin validate .\my-skin --strict --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe skin dev .\my-skin --entry index.html --watch --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe skin pack .\my-skin --out .\dist --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe skin install .\dist\my.skin-0.1.0.zip --force --json
```

Implemented behavior:

- `skin scaffold`: generates valid starter skins from blank, classic, launcher, dashboard, or rabbits templates.
- `skin validate --strict`: checks missing referenced assets, obvious local JavaScript syntax errors, and warns on private absolute paths or remote assets.
- `skin dev`: validates, installs, activates, and reloads a skin folder. With `--watch`, it repeats on file changes until Ctrl+C.
- `skin pack`: validates and zips a skin into `<id>-<version>.zip` (`--out` accepts a folder or a `.zip` path; `--force` overwrites).
- `skin install` accepts either a skin folder or a `.zip` package produced by `skin pack`.

Still planned:

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe skin screenshot .\my-skin --entry index.html --size 1920x1080 --state desktop --out .\screenshots --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe skin audit .\my-skin --accessibility --json
```

- `--mock-bridge`: provide fake runtime, monitor, storage, clipboard, desktop item, and known folder responses.
- `--no-execution`: log file, URL, app, and raw execution requests instead of running them.
- `skin screenshot`: render deterministic PNGs for common viewport and UI states.
- `skin audit --accessibility`: check keyboard reachability, focus states, accessible names, contrast, and reduced-motion issues.

## Status and Diagnostics

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe status --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe monitor list --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe placement diagnostics --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe logs --lines 100 --json
```

`placement diagnostics --json` is the best non-visual check that host windows exist and placement is working.

## Monitor Assignment

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe monitor assign "\\.\DISPLAY1" my.skin --entry index.html --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe monitor clear "\\.\DISPLAY1" --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe monitor mode single-monitor --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe monitor span my.skin --monitors "\\.\DISPLAY1,\\.\DISPLAY2" --entry index.html --json
```

## Runtime Host Commands

These require desktop.html to be running:

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe ping --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe open-settings --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe skin reload --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe placement reapply --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe exit --json
```

If no host is running, host-only commands return:

```text
Could not connect to a running desktop.html process. Is desktop.html open?
```

## Config and Startup

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe config get --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe config set app.safeMode false --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe startup status --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe startup on --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe startup off --json
```

## Updates

Installed builds (from the GitHub releases installer) update themselves automatically: the app checks GitHub releases shortly after startup, downloads any newer version, and restarts to apply it. `update check` reports the current state without applying anything:

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe update check --json
```

Dev builds (anything not installed by the Velopack installer) report `"installed": false` and are never auto-updated.

## Backups and Recovery

Backups live under `%AppData%\desktop-html\backups` as plain folders with a `manifest.json` and payload files.

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe backup list --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe backup create config --reason manual --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe backup create skin --skin-id my.skin --reason before-overwrite --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe backup create storage --skin-id my.skin --reason before-clear --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe backup restore <backup-id> --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\desktop-html.exe backup prune --keep 20 --json
```

`config patch`, `config set`, safe-mode toggles, startup toggles, monitor assignment changes, forced skin overwrites, and storage clears create backups automatically when there is existing data to protect.
