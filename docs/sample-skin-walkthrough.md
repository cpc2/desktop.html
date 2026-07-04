# Sample Skin Walkthrough

Use `samples/phase11-control-room` as the simplest skin to copy.

## 1. Copy the Sample

```powershell
Copy-Item .\samples\phase11-control-room .\samples\my-skin -Recurse
```

Then edit:

- `manifest.json`
- `index.html`
- `style.css`
- `script.js`

## 2. Change the Manifest

Use a unique id:

```json
{
  "schemaVersion": 1,
  "id": "example.my-skin",
  "name": "My Skin",
  "version": "0.1.0",
  "author": "Unknown",
  "entry": "index.html",
  "entries": {
    "main": "index.html"
  },
  "permissions": {
    "fullTrust": true,
    "network": true,
    "rawExecution": true
  }
}
```

## 3. Add Bridge Calls

Open settings:

```js
await window.desktop.openSettings();
```

Open a website:

```js
await window.desktop.openUrl("https://example.com");
```

Launch an app:

```js
await window.desktop.shellExecute({
  file: "notepad.exe",
  showWindow: "normal"
});
```

Store skin state:

```js
await window.desktop.storage.set("mode", "compact");
const mode = await window.desktop.storage.get("mode");
```

## 4. Validate and Install

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\desktop-html.exe skin validate .\samples\my-skin --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\desktop-html.exe skin install .\samples\my-skin --force --json
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\desktop-html.exe skin activate example.my-skin --entry index.html --json
```

## 5. Reload While Running

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\desktop-html.exe skin reload --json
```

If this fails with a no-host error, start desktop.html first:

```powershell
.\DesktopHtml.App\bin\Debug\net8.0-windows10.0.19041.0\desktop-html.exe
```
