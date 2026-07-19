# desktop.html Bridge API Reference

`window.desktop` is injected into every desktop.html skin. All methods return Promises. Failures reject with an `Error` that may include `code` and `details`.

desktop.html is full-trust. These APIs can open files, run programs, write files, and change local runtime config.

## Runtime

- `desktop.getRuntimeInfo()`
- `desktop.getVersion()`
- `desktop.getCapabilities()`
- `desktop.getConfig()`
- `desktop.setConfigPatch(patch)`
- `desktop.getBackups()`
- `desktop.createBackup({ kind, skinId, reason })`
- `desktop.restoreBackup(id)`
- `desktop.reload()`
- `desktop.reloadSkin()`
- `desktop.openSettings()`
- `desktop.openLogs()`
- `desktop.exit()`

Example:

```js
const info = await window.desktop.getRuntimeInfo();
await window.desktop.openSettings();
```

## Skin and Settings Management

These are available because settings is also HTML. Normal skins can call them, but should do so carefully.

- `desktop.getInstalledSkins()`
- `desktop.getInstalledSkinDetails()`
- `desktop.installSkinFromFolder(path, { overwrite })`
- `desktop.activateSkin(skinId, entry)`
- `desktop.assignMonitorSkin(monitorId, skinId, entry)`
- `desktop.clearMonitorSkin(monitorId)`
- `desktop.configureSpanning(skinId, { entry, monitors })`
- `desktop.setMonitorMode(mode)`
- `desktop.setStartupEnabled(enabled)`
- `desktop.getStartupStatus()`
- `desktop.openSkinFolder(skinId)`
- `desktop.deleteSkin(skinId)` — moves the skin folder and its stored data to the Recycle Bin; fails while the skin is active or assigned to a monitor/spanning mode

Valid monitor modes are `single-monitor`, `per-monitor`, and `spanning`.

## Backups

- `desktop.getBackups()`
- `desktop.createBackup({ kind, skinId, reason })`
- `desktop.restoreBackup(id)`

Backup kinds are `config`, `skin`, and `storage`. `skin` and `storage` backups require `skinId`.

Example:

```js
const backup = await window.desktop.createBackup({ kind: "config", reason: "before-edit" });
await window.desktop.restoreBackup(backup.id);
```

## Monitor Data

- `desktop.getMonitors()`
- `desktop.getCurrentMonitor()`
- `desktop.getVirtualDesktopBounds()`
- `desktop.getWorkArea()`

Monitor objects include `id`, `deviceName`, `bounds`, `workArea`, `isPrimary`, and `dpiScale`.

## Opening and Execution

- `desktop.openPath(path)`
- `desktop.openFile(path)`
- `desktop.openFolder(path)`
- `desktop.openUrl(url)`
- `desktop.openShortcut(path)`
- `desktop.revealInExplorer(path)`
- `desktop.openWindowsSettings(uri)`
- `desktop.shellExecute(options)`
- `desktop.run(command, args, options)`
- `desktop.runCommandLine(commandLine, options)`
- `desktop.runPowerShell(scriptOrFile, options)`
- `desktop.runBatch(scriptOrFile, options)`

`shellExecute` options:

```js
await window.desktop.shellExecute({
  file: "notepad.exe",
  args: [],
  workingDirectory: null,
  verb: "open",
  showWindow: "normal",
  waitForExit: false
});
```

`showWindow` can be `normal`, `hidden`, `minimized`, or `maximized`.

For launchers, prefer:

- URLs: `openUrl(url)`
- Folders: `openFolder(path)`
- Files: `openFile(path)`
- `.lnk` shortcuts: `openShortcut(path)`

## File System

- `desktop.exists(path)`
- `desktop.readText(path)`
- `desktop.writeText(path, content)`
- `desktop.readJson(path)`
- `desktop.writeJson(path, value)`
- `desktop.listDirectory(path)`
- `desktop.listDesktopItems()`
- `desktop.createDirectory(path)`
- `desktop.deletePath(path, options)`
- `desktop.movePath(source, destination)`
- `desktop.copyPath(source, destination)`
- `desktop.writeFileBase64(path, dataBase64, options)`

These are full-trust local file APIs. Avoid destructive actions unless the UI makes the action obvious.

`listDesktopItems()` returns `{ desktopPath, publicDesktopPath, items }`, where `items` merges the current user's Desktop and the Public Desktop (`C:\Users\Public\Desktop`) and excludes shell metadata such as `desktop.ini`. Use it for launcher skins that want the live Windows desktop contents.

Example:

```js
function launchDesktopItem(item) {
  if (item.isDirectory) {
    return window.desktop.openFolder(item.fullPath);
  }
  if (item.name.toLowerCase().endsWith(".lnk")) {
    return window.desktop.openShortcut(item.fullPath);
  }
  if (item.name.toLowerCase().endsWith(".url")) {
    return window.desktop.openPath(item.fullPath);
  }
  return window.desktop.openFile(item.fullPath);
}

const { items } = await window.desktop.listDesktopItems();
```

## Skin Storage

- `desktop.storage.get(key)`
- `desktop.storage.set(key, value)`
- `desktop.storage.remove(key)`
- `desktop.storage.clear()`
- `desktop.storage.getAll()`

Storage is scoped by skin id.

`writeFileBase64(path, dataBase64, options)` writes binary content decoded from base64. Pass `{ unique: true }` to auto-rename (`name (2).ext`, `name (3).ext`, ...) instead of overwriting; the resolved path is returned as `{ path }`. Primary use case: saving files dragged from Explorer onto the skin:

```js
window.addEventListener("drop", async (e) => {
  e.preventDefault();
  for (const file of e.dataTransfer.files) {
    const dataBase64 = await new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(String(reader.result).split(",")[1] || "");
      reader.onerror = () => reject(reader.error);
      reader.readAsDataURL(file);
    });
    await window.desktop.writeFileBase64(`${desktopPath}\\${file.name}`, dataBase64, { unique: true });
  }
});
window.addEventListener("dragover", (e) => e.preventDefault());
```

## Clipboard

- `desktop.clipboard.readText()`
- `desktop.clipboard.writeText(text)`
- `desktop.clipboard.saveToDirectory(directory)`

`saveToDirectory(directory)` saves the current clipboard contents into a directory and returns `{ savedPaths }`. Copied files/folders (an Explorer file-drop list) are copied in; otherwise a clipboard image (e.g. a screenshot) is saved as `Pasted <timestamp>.png`. Names are auto-deduplicated, nothing is overwritten. Combine with a `paste` event listener to let users Ctrl+V screenshots or copied files straight onto the desktop:

```js
document.addEventListener("paste", async (e) => {
  e.preventDefault();
  const { savedPaths } = await window.desktop.clipboard.saveToDirectory(desktopPath);
});
```

## Network

- `desktop.httpFetch(url, options)`

Host-side HTTP so skins are not limited by page CORS. Options:

| Option | Default | Notes |
| --- | --- | --- |
| `method` | `"GET"` | Any HTTP method. |
| `headers` | — | Object of request headers. |
| `body` | — | Request body string (UTF-8). |
| `timeoutMs` | `30000` | Clamped to 1s–120s. |
| `maxResponseBytes` | 8 MB | Clamped to 1 KB–64 MB; `truncated` is set if the cap was hit. |
| `asBase64` | `false` | Return the body as `bodyBase64` for binary content. |

Returns `{ status, statusText, headers, contentType, body, bodyBase64, truncated }`.

```js
const res = await window.desktop.httpFetch("https://api.open-meteo.com/v1/forecast?...");
const data = JSON.parse(res.body);

const img = await window.desktop.httpFetch(imageUrl, { asBase64: true });
imgEl.src = `data:${img.contentType};base64,${img.bodyBase64}`;
```

## Terminal Sessions

- `desktop.startTerminalSession({ sessionId?, command?, args?, workingDirectory?, pty?, cols?, rows? })` → `{ sessionId }`
- `desktop.writeTerminalInput(sessionId, text)`
- `desktop.resizeTerminal(sessionId, cols, rows)` — pty sessions only

Starts a child process (default `wsl.exe`) and streams its output back as
events (see Events below). Sessions are killed automatically when the skin
reloads or the window closes, so shells do not leak across reloads.

Two modes:

- **Default (redirected stdio):** simple pipelines. Programs do not see a real
  TTY, so prompts/colors/TUIs may not work.
- **`pty: true` (ConPTY):** the child gets a real Windows pseudoconsole —
  prompts, colors, cursor addressing, and `resizeTerminal` all work for
  PowerShell, cmd, and WSL alike. The output is a full VT escape stream, so
  pair it with a real terminal renderer (e.g. a locally bundled xterm.js); a
  minimal color-only parser will show artifacts.

```js
window.desktop.onEvent(event => {
  if (event.type === "terminalOutput" && event.sessionId === id) term.write(event.text);
  if (event.type === "terminalExit" && event.sessionId === id) showExit(event.exitCode);
});
const { sessionId } = await window.desktop.startTerminalSession({
  command: "powershell.exe",
  pty: true,
  cols: 100,
  rows: 30
});
await window.desktop.writeTerminalInput(sessionId, "ls\r");
await window.desktop.resizeTerminal(sessionId, 120, 40);
```

## Icons

- `desktop.getIcon(path, size)` → PNG data URL or `null`

`size` is `"small"` (16px), `"large"` (32px, default), `"extralarge"` (48px), or
`"jumbo"` (256px). Passing a boolean is still supported (`true` = small) for
older skins. Icons are cached host-side by path + size + file modification time,
so repeated requests are free. `.lnk` shortcuts resolve to their target's icon.

## File Watching

- `desktop.watch(path, { recursive?, watchId? })` → `{ watchId }`
- `desktop.unwatch(watchId)`

Watches a directory and pushes debounced, batched `fileSystemChanged` events
(~300 ms after activity settles, max 200 changes per batch). Prefer this over
polling `listDirectory`/`listDesktopItems`. Watches are removed automatically on
skin reload.

```js
window.desktop.onEvent(async event => {
  if (event.type === "fileSystemChanged" && event.watchId === desktopWatch) {
    await refreshLauncher(); // changes: [{ changeType, fullPath, oldPath }]
  }
});
const { desktopPath } = await window.desktop.getRuntimeInfo();
const { watchId: desktopWatch } = await window.desktop.watch(desktopPath);
```

## System Stats

- `desktop.getSystemStats()` — one-shot snapshot
- `desktop.subscribeSystemStats(intervalMs)` — pushes `systemStats` events; the host clamps the interval to ≥ 1000 ms
- `desktop.unsubscribeSystemStats()`

Snapshot shape: `{ cpuPercent, memory: { usedMb, totalMb, percent }, disks: [{ drive, freeGb, totalGb }], battery: { hasBattery, percent, onAc }, network: { receivedBytesPerSec, sentBytesPerSec } }`.
CPU and network are deltas between samples, so the first reading reports zero.
Use this instead of spawning PowerShell in a loop — it reads Win32 counters
directly and costs microseconds. One subscription per page; resubscribing
replaces the previous interval.

## Notifications

- `desktop.notify(title, message)`

Shows a Windows tray balloon notification. Useful for surfacing the result of
long-running commands or watcher hits while the desktop is covered.

## Media (Now Playing)

- `desktop.media.getNowPlaying({ thumbnail? })` — current system media session or `null`
- `desktop.media.control(action)` — `play`, `pause`, `playPause`, `next`, `previous`, `stop`
- `desktop.media.subscribe()` / `desktop.media.unsubscribe()` — push `mediaChanged` events

Backed by the Windows System Media Transport Controls (what the volume flyout
shows), so it works with Spotify, browsers, media players, etc. Snapshot shape:
`{ title, artist, album, sourceApp, status, positionSeconds, durationSeconds, thumbnail }`.
Pass `{ thumbnail: true }` to get album art as a data URL (omitted from pushed
events — fetch it on change if you need it).

```js
window.desktop.onEvent(async event => {
  if (event.type === "mediaChanged") render(event.nowPlaying);
});
await window.desktop.media.subscribe();
playPauseBtn.onclick = () => window.desktop.media.control("playPause");
```

## Global Hotkeys

- `desktop.registerHotkey(key, modifiers)` → `{ hotkeyId }`
- `desktop.unregisterHotkey(hotkeyId)`

System-wide hotkeys that fire `hotkeyPressed` events even while other apps are
focused. `key` is a letter, digit, `F1`–`F24`, or a named key (`space`,
`enter`, `escape`, arrows, ...); `modifiers` is an array of `ctrl`, `alt`,
`shift`, `win`. Registration throws if another application owns the
combination. Hotkeys are released automatically on skin reload.

```js
const { hotkeyId } = await window.desktop.registerHotkey("F9", ["ctrl", "alt"]);
window.desktop.onEvent(event => {
  if (event.type === "hotkeyPressed" && event.hotkeyId === hotkeyId) toggleOverlay();
});
```

## Events

`desktop.onEvent(callback)` subscribes to host-pushed events and returns an
unsubscribe function. Every event object has a `type`:

| Type | Payload | Source |
| --- | --- | --- |
| `terminalOutput` | `sessionId`, `text` | Terminal sessions |
| `terminalExit` | `sessionId`, `exitCode` | Terminal sessions |
| `fileSystemChanged` | `watchId`, `changes[]` | `desktop.watch` |
| `systemStats` | `stats` | `subscribeSystemStats` |
| `powerStatusChanged` | `onBattery`, `batteryPercent` | Automatic (AC plugged/unplugged) |
| `desktopVisibilityChanged` | `visible` | Automatic (a maximized/fullscreen window covers this monitor, or uncovers it) |
| `mediaChanged` | `nowPlaying` | `media.subscribe` |
| `hotkeyPressed` | `hotkeyId` | `registerHotkey` |

Well-behaved skins pause heavy animation work when `desktopVisibilityChanged`
reports `visible: false` or `powerStatusChanged` reports battery power — nobody
can see the pixels you are burning CPU on:

```js
let paused = false;
window.desktop.onEvent(event => {
  if (event.type === "desktopVisibilityChanged") paused = !event.visible;
});
```

## Diagnostics

- `desktop.log(level, message, data)`
- `desktop.getLogs({ maxLines })`
- `desktop.getLastError()`

Example:

```js
await window.desktop.log("info", "Tile launched", { id: "notepad" });
const logs = await window.desktop.getLogs({ maxLines: 50 });
```
