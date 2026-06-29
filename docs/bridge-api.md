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
- `desktop.createDirectory(path)`
- `desktop.deletePath(path, options)`
- `desktop.movePath(source, destination)`
- `desktop.copyPath(source, destination)`

These are full-trust local file APIs. Avoid destructive actions unless the UI makes the action obvious.

## Skin Storage

- `desktop.storage.get(key)`
- `desktop.storage.set(key, value)`
- `desktop.storage.remove(key)`
- `desktop.storage.clear()`
- `desktop.storage.getAll()`

Storage is scoped by skin id.

## Clipboard

- `desktop.clipboard.readText()`
- `desktop.clipboard.writeText(text)`

## Diagnostics

- `desktop.log(level, message, data)`
- `desktop.getLogs({ maxLines })`
- `desktop.getLastError()`

Example:

```js
await window.desktop.log("info", "Tile launched", { id: "notepad" });
const logs = await window.desktop.getLogs({ maxLines: 50 });
```
