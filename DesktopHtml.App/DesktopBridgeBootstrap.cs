namespace DesktopHtml.App;

public static class DesktopBridgeBootstrap
{
    public const string Script = """
(() => {
  if (window.desktop || !window.chrome?.webview) {
    return;
  }

  let nextId = 1;
  const pending = new Map();

  function rejectFromError(error) {
    const nativeError = new Error(error?.message || "Native bridge call failed.");
    nativeError.code = error?.code || "BRIDGE_ERROR";
    nativeError.details = error?.details || null;
    return nativeError;
  }

  chrome.webview.addEventListener("message", event => {
    const message = event.data;
    if (!message?.id || !pending.has(message.id)) {
      return;
    }

    const call = pending.get(message.id);
    pending.delete(message.id);

    if (message.ok) {
      call.resolve(message.result ?? null);
    } else {
      call.reject(rejectFromError(message.error));
    }
  });

  function call(method, params = {}) {
    const id = `call-${Date.now()}-${nextId++}`;
    chrome.webview.postMessage({ id, method, params });

    return new Promise((resolve, reject) => {
      pending.set(id, { resolve, reject });
    });
  }

  window.desktop = {
    getRuntimeInfo: () => call("getRuntimeInfo"),
    getVersion: () => call("getVersion"),
    getCapabilities: () => call("getCapabilities"),
    getConfig: () => call("getConfig"),
    getInstalledSkins: () => call("getInstalledSkins"),
    getInstalledSkinDetails: () => call("getInstalledSkinDetails"),
    setConfigPatch: patch => call("setConfigPatch", { patch }),
    getBackups: () => call("getBackups"),
    createBackup: options => call("createBackup", options || {}),
    restoreBackup: id => call("restoreBackup", { id }),
    installSkinFromFolder: (path, options = {}) => call("installSkinFromFolder", { path, overwrite: !!options.overwrite }),
    activateSkin: (skinId, entry = null) => call("activateSkin", { skinId, entry }),
    assignMonitorSkin: (monitorId, skinId, entry = null) => call("assignMonitorSkin", { monitorId, skinId, entry }),
    clearMonitorSkin: monitorId => call("clearMonitorSkin", { monitorId }),
    configureSpanning: (skinId, options = {}) => call("configureSpanning", {
      skinId,
      entry: options.entry ?? null,
      monitors: options.monitors ?? []
    }),
    setMonitorMode: mode => call("setMonitorMode", { mode }),
    setStartupEnabled: enabled => call("setStartupEnabled", { enabled }),
    getStartupStatus: () => call("getStartupStatus"),
    openSkinFolder: skinId => call("openSkinFolder", { skinId }),
    reload: () => call("reload"),
    reloadSkin: () => call("reloadSkin"),
    openSettings: () => call("openSettings"),
    openLogs: () => call("openLogs"),
    exit: () => call("exit"),
    getMonitors: () => call("getMonitors"),
    getCurrentMonitor: () => call("getCurrentMonitor"),
    getVirtualDesktopBounds: () => call("getVirtualDesktopBounds"),
    getWorkArea: () => call("getWorkArea"),
    log: (level, message, data = null) => call("log", { level, message, data }),
    openPath: path => call("openPath", { path }),
    openFile: path => call("openFile", { path }),
    openFolder: path => call("openFolder", { path }),
    openUrl: url => call("openUrl", { url }),
    openShortcut: path => call("openShortcut", { path }),
    revealInExplorer: path => call("revealInExplorer", { path }),
    openWindowsSettings: uri => call("openWindowsSettings", { uri }),
    shellExecute: options => call("shellExecute", options || {}),
    run: (command, args = [], options = {}) => call("run", { ...options, command, args }),
    runCommandLine: (commandLine, options = {}) => call("runCommandLine", { ...options, commandLine }),
    runPowerShell: (scriptOrFile, options = {}) => call("runPowerShell", { ...options, scriptOrFile }),
    runBatch: (scriptOrFile, options = {}) => call("runBatch", { ...options, scriptOrFile }),
    exists: path => call("exists", { path }),
    readText: path => call("readText", { path }),
    writeText: (path, content) => call("writeText", { path, content }),
    readJson: path => call("readJson", { path }),
    writeJson: (path, value) => call("writeJson", { path, value }),
    listDirectory: path => call("listDirectory", { path }),
    createDirectory: path => call("createDirectory", { path }),
    deletePath: (path, options = {}) => call("deletePath", { path, options }),
    movePath: (source, destination) => call("movePath", { source, destination }),
    copyPath: (source, destination) => call("copyPath", { source, destination }),
    storage: {
      get: key => call("storage.get", { key }),
      set: (key, value) => call("storage.set", { key, value }),
      remove: key => call("storage.remove", { key }),
      clear: () => call("storage.clear"),
      getAll: () => call("storage.getAll")
    },
    clipboard: {
      readText: () => call("clipboard.readText"),
      writeText: text => call("clipboard.writeText", { text })
    },
    getLogs: (options = {}) => call("getLogs", options),
    getLastError: () => call("getLastError")
  };
})();
""";
}
