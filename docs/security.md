# desktop.html Security and Trust Model

desktop.html skins are full-trust local programs written in HTML, CSS, and JavaScript.

Installing a skin is equivalent to running local software from that skin author. A skin can:

- Open files, folders, URLs, and shortcuts.
- Run programs and scripts.
- Read and write local files through bridge APIs.
- Access network resources using browser APIs.
- Store data under `%AppData%\desktop-html`.

## Agent Rules

When generating a skin:

- Do not hide raw execution behind surprising UI.
- Do not run commands on page load.
- Only run bridge launch/execution calls after a clear user action.
- Avoid destructive file operations unless the user explicitly requested them.
- Prefer visible launch buttons, double-click launchers, or context actions.
- Make any local absolute paths obvious in the UI or docs.

## Third-Party Skins

Only install skins from sources you trust. The settings UI shows a warning before installing or activating skins, but v1 does not sandbox skins.

## Safe Mode

Safe mode disables active skin windows and opens settings only. It is useful if a skin is broken or hostile.

CLI:

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\desktop-html.exe safe-mode on
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\desktop-html.exe safe-mode off
```
