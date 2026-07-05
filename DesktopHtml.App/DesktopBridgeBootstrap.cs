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
  const listeners = new Set();

  // Messages posted while the navigation is still settling are silently
  // dropped by WebView2 in both directions, which loses bridge calls made
  // while the document is parsing (e.g. an inline script's initial data
  // load). Queue outbound messages and ping the host with "bridgeHello"
  // until it acknowledges with a "bridgeReady" event; only then flush.
  let bridgeReady = false;
  const outbox = [];

  function send(payload) {
    if (bridgeReady) {
      chrome.webview.postMessage(payload);
    } else {
      outbox.push(payload);
    }
  }

  function markReady() {
    if (bridgeReady) {
      return;
    }
    bridgeReady = true;
    clearInterval(helloTimer);
    while (outbox.length) {
      chrome.webview.postMessage(outbox.shift());
    }
  }

  const helloStarted = Date.now();
  const helloTimer = setInterval(() => {
    if (Date.now() - helloStarted > 15000) {
      // No ack — assume an old host without the handshake and post directly.
      markReady();
      return;
    }
    chrome.webview.postMessage({ bridgeHello: true });
  }, 100);
  chrome.webview.postMessage({ bridgeHello: true });

  function rejectFromError(error) {
    const nativeError = new Error(error?.message || "Native bridge call failed.");
    nativeError.code = error?.code || "BRIDGE_ERROR";
    nativeError.details = error?.details || null;
    return nativeError;
  }

  chrome.webview.addEventListener("message", event => {
    const message = event.data;
    if (message && message.type === "bridgeReady") {
      markReady();
      return;
    }
    if (message && message.type) {
      listeners.forEach(cb => {
        try { cb(message); } catch (e) { console.error(e); }
      });
      return;
    }
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
    send({ id, method, params });

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
    getIcon: (path, sizeOrSmall = "large") => {
      const size = typeof sizeOrSmall === "string" ? sizeOrSmall : (sizeOrSmall ? "small" : "large");
      return call("getIcon", { path, size });
    },
    writeText: (path, content) => call("writeText", { path, content }),
    writeFileBase64: (path, dataBase64, options = {}) => call("writeFileBase64", { ...options, path, dataBase64 }),
    readJson: path => call("readJson", { path }),
    writeJson: (path, value) => call("writeJson", { path, value }),
    listDirectory: path => call("listDirectory", { path }),
    listDesktopItems: () => call("listDesktopItems"),
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
      writeText: text => call("clipboard.writeText", { text }),
      saveToDirectory: directory => call("clipboard.saveToDirectory", { directory })
    },
    getLogs: (options = {}) => call("getLogs", options),
    getLastError: () => call("getLastError"),
    onEvent: callback => {
      listeners.add(callback);
      return () => listeners.delete(callback);
    },
    httpFetch: (url, options = {}) => call("httpFetch", { url, ...options }),
    startTerminalSession: options => call("terminal.start", options || {}),
    writeTerminalInput: (sessionId, text) => call("terminal.write", { sessionId, text }),
    resizeTerminal: (sessionId, cols, rows) => call("terminal.resize", { sessionId, cols, rows }),
    watch: (path, options = {}) => call("watch", { path, ...options }),
    unwatch: watchId => call("unwatch", { watchId }),
    getSystemStats: () => call("getSystemStats"),
    subscribeSystemStats: (intervalMs = 2000) => call("subscribeSystemStats", { intervalMs }),
    unsubscribeSystemStats: () => call("unsubscribeSystemStats"),
    notify: (title, message = "") => call("notify", { title, message }),
    media: {
      getNowPlaying: (options = {}) => call("media.getNowPlaying", options),
      control: action => call("media.control", { action }),
      subscribe: () => call("media.subscribe"),
      unsubscribe: () => call("media.unsubscribe")
    },
    registerHotkey: (key, modifiers = []) => call("registerHotkey", { key, modifiers }),
    unregisterHotkey: hotkeyId => call("unregisterHotkey", { hotkeyId })
  };
})();
""";
}
