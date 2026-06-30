# desktop.html Skin Authoring Guide

This guide is for humans and coding agents creating local desktop.html skins.

desktop.html skins are trusted local HTML apps. A skin is just a folder with a `manifest.json` file and at least one HTML entrypoint. The runtime does not know what an icon, launcher, panel, shelf, or widget is. Your HTML owns the whole visual model.

## Minimal Folder

```text
my-skin/
  manifest.json
  index.html
  style.css
  script.js
  assets/
```

## Manifest

`manifest.json` must be valid JSON. Comments and trailing commas are accepted by the validator, but plain JSON is preferred for portability.

```json
{
  "schemaVersion": 1,
  "id": "example.my-skin",
  "name": "My Skin",
  "version": "0.1.0",
  "author": "Unknown",
  "description": "A local desktop.html skin.",
  "entry": "index.html",
  "entries": {
    "main": "index.html"
  },
  "minimumDesktopHtmlVersion": "0.1.0",
  "permissions": {
    "fullTrust": true,
    "network": true,
    "rawExecution": true
  }
}
```

Required fields:

- `schemaVersion`: positive integer.
- `id`: stable skin id, usually reverse-domain or `author.skin-name`.
- `name`: display name.
- `entry`: relative path to the default HTML entrypoint.

Important validation rules:

- Entrypoints must be relative paths inside the skin folder.
- Entrypoints cannot be absolute paths.
- Entrypoints cannot contain `..`.
- Every declared entrypoint file must exist.

## HTML and JavaScript

The runtime injects `window.desktop` before your scripts run. Bridge calls are async and return Promises.

```html
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>My Skin</title>
  <link rel="stylesheet" href="./style.css">
</head>
<body>
  <button id="notepad" type="button">Open Notepad</button>
  <script src="./script.js"></script>
</body>
</html>
```

```js
async function boot() {
  if (!window.desktop) {
    document.body.textContent = "window.desktop is not available.";
    return;
  }

  const runtime = await window.desktop.getRuntimeInfo();
  console.log(runtime.appVersion);
}

document.getElementById("notepad").addEventListener("dblclick", async () => {
  await window.desktop.shellExecute({ file: "notepad.exe", showWindow: "normal" });
});

boot().catch(error => {
  console.error(error);
});
```

## Recommended UX Patterns

- Use `button` elements for launchable items so keyboard focus and click handling are sane.
- Prefer one explicit launch action, such as double-click or a visible Launch button.
- Avoid re-rendering or replacing a clicked element between `click` and `dblclick`; it can interrupt double-click recognition.
- Store skin-local preferences with `window.desktop.storage`.
- Keep text within bounds and make layouts responsive to monitor size.
- Treat every bridge call as fallible and show friendly errors.

## Local Storage

Skin storage is isolated by skin id and stored under AppData.

```js
const count = Number(await window.desktop.storage.get("count") || 0);
await window.desktop.storage.set("count", count + 1);
await window.desktop.storage.remove("count");
await window.desktop.storage.clear();
```

## Optional Hundred Rabbits Themes

Skins may opt into the Hundred Rabbits 9-color SVG theme convention by including a local `rabbits-theme.js` helper. This is skin-side only; the runtime does not enforce or inject global theme behavior.

Scaffold a starter:

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin scaffold my.rabbits --template rabbits --json
```

The helper exposes:

```js
const theme = window.DesktopHtmlRabbitsTheme.createController({
  target: document.documentElement,
  storageKey: "rabbits-theme",
  defaultTheme,
  onLoad: activeTheme => render(activeTheme)
});

await theme.start();
await theme.load(svgTextOrThemeObject);
theme.openFilePicker();
theme.enableDropImport(document.body);
```

Themes must provide `background`, `f_high`, `f_med`, `f_low`, `f_inv`, `b_high`, `b_med`, `b_low`, and `b_inv`. The helper injects compatible variables such as `--background` and namespaced aliases such as `--rabbits-background`, persists with `window.desktop.storage` when available, and parses imported SVG as text without injecting the SVG markup.

## Multi-Monitor Behavior

Use monitor APIs instead of hardcoding display geometry.

```js
const current = await window.desktop.getCurrentMonitor();
const monitors = await window.desktop.getMonitors();
const virtualBounds = await window.desktop.getVirtualDesktopBounds();
```

In per-monitor mode, each host WebView can receive its own current monitor. In spanning mode, the skin may be hosted over a virtual canvas.

## Validate and Install

From the repository root:

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin validate .\path\to\my-skin --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin install .\path\to\my-skin --force --json
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin activate example.my-skin --entry index.html --json
```

If desktop.html is already running:

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin reload --json
```

## Agent Checklist

Before handing a skin back:

- `manifest.json` has a stable unique `id`.
- `entry` points to an existing HTML file.
- All CSS/JS/assets are relative to the skin folder.
- No private absolute paths are hardcoded unless the user explicitly asked for a local-only personal skin.
- Launch buttons call the bridge only from clear user actions.
- `skin validate` passes.
- The skin is installed with `skin install --force`.
