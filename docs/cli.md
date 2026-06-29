# desktop.html CLI Reference

The app executable also acts as the CLI:

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe <command>
```

Most commands support `--json` for agent-friendly output.

## Common Skin Workflow

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin validate .\my-skin --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin install .\my-skin --force --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin activate my.skin --entry index.html --json
```

If desktop.html is already running:

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin reload --json
```

## Skin Authoring Toolkit

These commands help create and iterate on skins from the CLI.

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin scaffold my.skin --template classic --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin validate .\my-skin --strict --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin dev .\my-skin --entry index.html --watch --json
```

Implemented behavior:

- `skin scaffold`: generates valid starter skins from blank, classic, launcher, or dashboard templates.
- `skin validate --strict`: checks missing referenced assets, obvious local JavaScript syntax errors, and warns on private absolute paths or remote assets.
- `skin dev`: validates, installs, activates, and reloads a skin folder. With `--watch`, it repeats on file changes until Ctrl+C.

Still planned:

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin screenshot .\my-skin --entry index.html --size 1920x1080 --state desktop --out .\screenshots --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin audit .\my-skin --accessibility --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin pack .\my-skin --out .\dist --json
```

- `--mock-bridge`: provide fake runtime, monitor, storage, clipboard, desktop item, and known folder responses.
- `--no-execution`: log file, URL, app, and raw execution requests instead of running them.
- `skin screenshot`: render deterministic PNGs for common viewport and UI states.
- `skin audit --accessibility`: check keyboard reachability, focus states, accessible names, contrast, and reduced-motion issues.
- `skin pack`: validate and export a shareable skin package.

## Status and Diagnostics

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe status --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe monitor list --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe placement diagnostics --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe logs --lines 100 --json
```

`placement diagnostics --json` is the best non-visual check that host windows exist and placement is working.

## Monitor Assignment

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe monitor assign "\\.\DISPLAY1" my.skin --entry index.html --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe monitor clear "\\.\DISPLAY1" --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe monitor mode single-monitor --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe monitor span my.skin --monitors "\\.\DISPLAY1,\\.\DISPLAY2" --entry index.html --json
```

## Runtime Host Commands

These require desktop.html to be running:

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe ping --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe open-settings --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin reload --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe placement reapply --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe exit --json
```

If no host is running, host-only commands return:

```text
Could not connect to a running desktop.html process. Is desktop.html open?
```

## Config and Startup

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe config get --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe config set app.safeMode false --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe startup status --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe startup on --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe startup off --json
```

## Backups and Recovery

Backups live under `%AppData%\desktop-html\backups` as plain folders with a `manifest.json` and payload files.

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe backup list --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe backup create config --reason manual --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe backup create skin --skin-id my.skin --reason before-overwrite --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe backup create storage --skin-id my.skin --reason before-clear --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe backup restore <backup-id> --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe backup prune --keep 20 --json
```

`config patch`, `config set`, safe-mode toggles, startup toggles, monitor assignment changes, forced skin overwrites, and storage clears create backups automatically when there is existing data to protect.
