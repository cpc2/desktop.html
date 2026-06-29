namespace DesktopHtml.App;

public static class SettingsPage
{
    public const string Html = """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>desktop.html settings</title>
  <style>
    :root {
      color-scheme: dark;
      font-family: "Segoe UI", system-ui, sans-serif;
      background: #101316;
      color: #f7f3e8;
    }

    * {
      box-sizing: border-box;
    }

    body {
      margin: 0;
      padding: 22px;
      background: #101316;
    }

    main {
      max-width: 1120px;
      margin: 0 auto;
      display: grid;
      gap: 18px;
    }

    header,
    .row,
    .toolbar {
      display: flex;
      flex-wrap: wrap;
      gap: 10px;
      align-items: center;
    }

    header {
      justify-content: space-between;
    }

    h1,
    h2,
    h3,
    p {
      margin: 0;
      letter-spacing: 0;
    }

    h1 {
      font-size: 28px;
    }

    h2 {
      font-size: 17px;
      color: #75d6c9;
    }

    h3 {
      font-size: 15px;
    }

    section {
      display: grid;
      gap: 10px;
      border-top: 1px solid rgba(255,255,255,.12);
      padding-top: 16px;
    }

    label {
      display: grid;
      gap: 5px;
      color: #a8b4b2;
      font-size: 13px;
    }

    input,
    select {
      min-width: 180px;
      border: 1px solid rgba(255,255,255,.18);
      border-radius: 6px;
      background: rgba(255,255,255,.07);
      color: #f7f3e8;
      padding: 8px 10px;
      font: inherit;
    }

    input[type="checkbox"] {
      min-width: 0;
      width: 16px;
      height: 16px;
      padding: 0;
    }

    button {
      border: 1px solid rgba(255,255,255,.18);
      border-radius: 6px;
      background: rgba(255,255,255,.08);
      color: #f7f3e8;
      padding: 8px 11px;
      font: inherit;
    }

    button:hover,
    button:focus-visible,
    input:focus-visible,
    select:focus-visible {
      border-color: rgba(117,214,201,.8);
      outline: none;
    }

    button.primary {
      background: rgba(117,214,201,.18);
      border-color: rgba(117,214,201,.55);
    }

    button.danger {
      border-color: rgba(231,116,116,.6);
    }

    button:disabled {
      opacity: .48;
    }

    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
      gap: 12px;
    }

    .card {
      display: grid;
      gap: 10px;
      border: 1px solid rgba(255,255,255,.12);
      border-radius: 8px;
      padding: 12px;
      background: rgba(255,255,255,.045);
    }

    .status {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(190px, 1fr));
      gap: 8px;
    }

    .stat {
      display: grid;
      gap: 3px;
      border-left: 2px solid rgba(117,214,201,.6);
      padding-left: 9px;
    }

    .stat span,
    .muted {
      color: #a8b4b2;
      font-size: 13px;
    }

    .warning {
      border-left: 3px solid #d9c46f;
      padding: 9px 11px;
      background: rgba(217,196,111,.1);
      color: #f7f3e8;
    }

    pre,
    .list {
      margin: 0;
      padding: 12px;
      border-radius: 6px;
      background: rgba(0,0,0,.28);
      overflow: auto;
      white-space: pre-wrap;
    }

    .message {
      min-height: 22px;
      color: #d9c46f;
    }
  </style>
</head>
<body>
  <main>
    <header>
      <div>
        <h1>desktop.html settings</h1>
        <p class="muted" id="subtitle">Loading runtime...</p>
      </div>
      <div class="toolbar">
        <button id="reload" type="button">Reload skin</button>
        <button id="openLogs" type="button">Open logs</button>
        <button id="refresh" type="button">Refresh</button>
      </div>
    </header>

    <p class="message" id="message"></p>

    <section>
      <h2>Status</h2>
      <div class="status" id="status"></div>
    </section>

    <section>
      <h2>Runtime controls</h2>
      <div class="toolbar">
        <button id="modeSingle" type="button">Single monitor</button>
        <button id="modePer" type="button">Per monitor</button>
        <button id="modeSpan" type="button">Spanning</button>
        <button id="startup" type="button">Toggle startup</button>
        <button id="pauseFullscreen" type="button">Toggle fullscreen pause</button>
        <button id="pauseBattery" type="button">Toggle battery pause</button>
        <button id="safe" class="danger" type="button">Enable safe mode</button>
      </div>
    </section>

    <section>
      <h2>Skins</h2>
      <p class="warning">Skins are full-trust local programs. They can run commands and open files through the bridge. Only install skins from folders you trust.</p>
      <div class="grid">
        <div class="card">
          <h3>Install from folder</h3>
          <label>
            Folder path
            <input id="installPath" type="text" placeholder="C:\path\to\skin">
          </label>
          <label class="row">
            <input id="installOverwrite" type="checkbox">
            Overwrite if already installed
          </label>
          <button id="installSkin" class="primary" type="button">Install skin</button>
        </div>

        <div class="card">
          <h3>Activate skin</h3>
          <label>
            Skin
            <select id="skinSelect"></select>
          </label>
          <label>
            Entry
            <select id="entrySelect"></select>
          </label>
          <div class="toolbar">
            <button id="activateSkin" class="primary" type="button">Activate</button>
            <button id="openSkinFolder" type="button">Open folder</button>
          </div>
        </div>
      </div>
      <div class="list" id="skins"></div>
    </section>

    <section>
      <h2>Monitor assignment</h2>
      <div class="grid" id="monitors"></div>
    </section>

    <section>
      <h2>Spanning mode</h2>
      <div class="card">
        <label>
          Skin
          <select id="spanSkin"></select>
        </label>
        <label>
          Entry
          <select id="spanEntry"></select>
        </label>
        <div id="spanMonitors" class="toolbar"></div>
        <button id="applySpan" class="primary" type="button">Use spanning mode</button>
        <p class="muted">Leaving every monitor unchecked spans all current monitors.</p>
      </div>
    </section>

    <section>
      <h2>Backup restore</h2>
      <div class="toolbar">
        <button id="createConfigBackup" type="button">Create config backup</button>
        <label>
          Backup
          <select id="backupSelect"></select>
        </label>
        <button id="restoreBackup" class="danger" type="button">Restore selected backup</button>
      </div>
      <pre id="backups"></pre>
    </section>

    <section>
      <h2>Recent logs</h2>
      <pre id="logs"></pre>
    </section>
  </main>

  <script>
    const state = {
      runtime: null,
      config: null,
      monitors: [],
      skins: [],
      backups: []
    };

    const ids = [
      "subtitle", "message", "status", "skinSelect", "entrySelect", "skins",
      "monitors", "spanSkin", "spanEntry", "spanMonitors", "logs",
      "installPath", "installOverwrite", "backupSelect", "backups"
    ];
    const ui = Object.fromEntries(ids.map(id => [id, document.getElementById(id)]));

    function setMessage(text) {
      ui.message.textContent = text || "";
    }

    async function run(label, action) {
      try {
        setMessage(`${label}...`);
        await action();
        await refresh();
        setMessage(`${label} complete.`);
      } catch (error) {
        setMessage(error?.message || `${label} failed.`);
      }
    }

    function stat(name, value) {
      const item = document.createElement("div");
      item.className = "stat";
      item.innerHTML = `<span></span><strong></strong>`;
      item.querySelector("span").textContent = name;
      item.querySelector("strong").textContent = value ?? "";
      return item;
    }

    function entriesFor(skinId) {
      const skin = state.skins.find(item => item.id === skinId);
      if (!skin) {
        return ["index.html"];
      }

      const entries = new Set([skin.entry || "index.html"]);
      for (const [name, path] of Object.entries(skin.entries || {})) {
        entries.add(name);
        entries.add(path);
      }

      return [...entries].filter(Boolean);
    }

    function fillSkinSelect(select, selectedSkinId) {
      select.textContent = "";
      for (const skin of state.skins) {
        const option = document.createElement("option");
        option.value = skin.id;
        option.textContent = `${skin.name} (${skin.id})`;
        option.selected = skin.id === selectedSkinId;
        select.append(option);
      }
    }

    function fillEntrySelect(select, skinId, selectedEntry) {
      select.textContent = "";
      for (const entry of entriesFor(skinId)) {
        const option = document.createElement("option");
        option.value = entry;
        option.textContent = entry;
        option.selected = entry === selectedEntry;
        select.append(option);
      }
    }

    function renderStatus() {
      ui.status.textContent = "";
      const startup = state.config.app.startWithWindows ? "enabled" : "disabled";
      ui.status.append(
        stat("Version", state.runtime.appVersion),
        stat("Mode", state.config.skins.activeMode),
        stat("Active skin", state.config.skins.activeSkinId || "(none)"),
        stat("Entry", state.config.skins.entry),
        stat("Startup", startup),
        stat("Safe mode", String(state.config.app.safeMode)),
        stat("Placement", state.config.desktop.placementMode),
        stat("Config", state.runtime.configFile)
      );
      document.getElementById("safe").textContent = state.config.app.safeMode ? "Disable safe mode" : "Enable safe mode";
    }

    function renderSkins() {
      const selected = state.config.skins.activeSkinId || state.skins[0]?.id || "";
      fillSkinSelect(ui.skinSelect, selected);
      fillEntrySelect(ui.entrySelect, ui.skinSelect.value, state.config.skins.entry);
      fillSkinSelect(ui.spanSkin, state.config.skins.spanning.skinId || selected);
      fillEntrySelect(ui.spanEntry, ui.spanSkin.value, state.config.skins.spanning.entry || state.config.skins.entry);

      ui.skins.textContent = state.skins.length
        ? state.skins.map(skin => `${skin.id} - ${skin.name} ${skin.version}\n  ${skin.skinPath}`).join("\n")
        : "(none)";
    }

    function renderMonitors() {
      ui.monitors.textContent = "";
      ui.spanMonitors.textContent = "";

      for (const monitor of state.monitors) {
        const assignment = state.config.skins.perMonitor?.[monitor.id] || {};
        const card = document.createElement("div");
        card.className = "card";
        card.innerHTML = `
          <h3></h3>
          <p class="muted"></p>
          <label>Skin<select class="monitor-skin"></select></label>
          <label>Entry<select class="monitor-entry"></select></label>
          <div class="toolbar">
            <button class="assign primary" type="button">Assign</button>
            <button class="clear" type="button">Clear</button>
          </div>
        `;
        card.querySelector("h3").textContent = monitor.id + (monitor.isPrimary ? " primary" : "");
        card.querySelector("p").textContent = `${monitor.workArea.width}x${monitor.workArea.height}+${monitor.workArea.left}+${monitor.workArea.top}`;
        const skinSelect = card.querySelector(".monitor-skin");
        const entrySelect = card.querySelector(".monitor-entry");
        fillSkinSelect(skinSelect, assignment.skinId || state.config.skins.activeSkinId);
        fillEntrySelect(entrySelect, skinSelect.value, assignment.entry || state.config.skins.entry);
        skinSelect.addEventListener("change", () => fillEntrySelect(entrySelect, skinSelect.value, entrySelect.value));
        card.querySelector(".assign").addEventListener("click", () => run("Assign monitor", () =>
          window.desktop.assignMonitorSkin(monitor.id, skinSelect.value, entrySelect.value)));
        card.querySelector(".clear").addEventListener("click", () => run("Clear monitor", () =>
          window.desktop.clearMonitorSkin(monitor.id)));
        ui.monitors.append(card);

        const checkLabel = document.createElement("label");
        checkLabel.className = "row";
        const checkbox = document.createElement("input");
        checkbox.type = "checkbox";
        checkbox.value = monitor.id;
        checkbox.checked = state.config.skins.spanning.monitors?.includes(monitor.id);
        checkLabel.append(checkbox, monitor.id);
        ui.spanMonitors.append(checkLabel);
      }
    }

    function renderBackups() {
      ui.backupSelect.textContent = "";
      for (const backup of state.backups.slice(0, 20)) {
        const option = document.createElement("option");
        option.value = backup.id;
        option.textContent = `${backup.kind} - ${new Date(backup.createdUtc).toLocaleString()} - ${backup.reason}`;
        ui.backupSelect.append(option);
      }

      ui.backups.textContent = state.backups.length
        ? state.backups.slice(0, 20).map(backup =>
          `${backup.id}\n  ${backup.kind} ${backup.reason}\n  ${backup.relativeTargetPath}`).join("\n\n")
        : "(no backups yet)";
    }

    async function refresh() {
      const [runtime, config, monitors, skins, backups, logs] = await Promise.all([
        window.desktop.getRuntimeInfo(),
        window.desktop.getConfig(),
        window.desktop.getMonitors(),
        window.desktop.getInstalledSkinDetails(),
        window.desktop.getBackups(),
        window.desktop.getLogs({ maxLines: 80 })
      ]);

      state.runtime = runtime;
      state.config = config;
      state.monitors = monitors;
      state.skins = skins;
      state.backups = backups;
      ui.subtitle.textContent = `Running ${runtime.appVersion}`;
      renderStatus();
      renderSkins();
      renderMonitors();
      renderBackups();
      ui.logs.textContent = logs.join("\n");
    }

    document.getElementById("refresh").addEventListener("click", () => run("Refresh", async () => {}));
    document.getElementById("reload").addEventListener("click", () => run("Reload skin", () => window.desktop.reloadSkin()));
    document.getElementById("openLogs").addEventListener("click", () => window.desktop.openLogs());
    document.getElementById("startup").addEventListener("click", () => run("Toggle startup", () =>
      window.desktop.setStartupEnabled(!state.config.app.startWithWindows)));
    document.getElementById("pauseFullscreen").addEventListener("click", () => run("Toggle fullscreen pause", () =>
      window.desktop.setConfigPatch({ performance: { pauseWhenFullscreenAppActive: !state.config.performance.pauseWhenFullscreenAppActive } })));
    document.getElementById("pauseBattery").addEventListener("click", () => run("Toggle battery pause", () =>
      window.desktop.setConfigPatch({ performance: { pauseWhenOnBattery: !state.config.performance.pauseWhenOnBattery } })));
    document.getElementById("safe").addEventListener("click", () => {
      const next = !state.config.app.safeMode;
      const prompt = next
        ? "Enable safe mode? Desktop skin windows will close and settings will stay available."
        : "Disable safe mode and allow desktop skins again?";
      if (!confirm(prompt)) {
        return;
      }

      run(next ? "Enable safe mode" : "Disable safe mode", () => window.desktop.setConfigPatch({ app: { safeMode: next } }));
    });

    document.getElementById("modeSingle").addEventListener("click", () => run("Set single-monitor mode", () =>
      window.desktop.setMonitorMode("single-monitor")));
    document.getElementById("modePer").addEventListener("click", () => run("Set per-monitor mode", () =>
      window.desktop.setMonitorMode("per-monitor")));
    document.getElementById("modeSpan").addEventListener("click", () => run("Set spanning mode", () =>
      window.desktop.setMonitorMode("spanning")));

    ui.skinSelect.addEventListener("change", () => fillEntrySelect(ui.entrySelect, ui.skinSelect.value, ui.entrySelect.value));
    ui.spanSkin.addEventListener("change", () => fillEntrySelect(ui.spanEntry, ui.spanSkin.value, ui.spanEntry.value));

    document.getElementById("installSkin").addEventListener("click", () => {
      const path = ui.installPath.value.trim();
      if (!path) {
        setMessage("Enter a skin folder path first.");
        return;
      }

      if (!confirm("Install this full-trust skin folder?")) {
        return;
      }

      run("Install skin", () => window.desktop.installSkinFromFolder(path, { overwrite: ui.installOverwrite.checked }));
    });

    document.getElementById("activateSkin").addEventListener("click", () => {
      if (!confirm("Activate this full-trust skin?")) {
        return;
      }

      run("Activate skin", () => window.desktop.activateSkin(ui.skinSelect.value, ui.entrySelect.value));
    });
    document.getElementById("openSkinFolder").addEventListener("click", () =>
      window.desktop.openSkinFolder(ui.skinSelect.value));
    document.getElementById("applySpan").addEventListener("click", () => {
      const monitors = [...ui.spanMonitors.querySelectorAll("input:checked")].map(input => input.value);
      run("Configure spanning", () => window.desktop.configureSpanning(ui.spanSkin.value, {
        entry: ui.spanEntry.value,
        monitors
      }));
    });
    document.getElementById("createConfigBackup").addEventListener("click", () =>
      run("Create config backup", () => window.desktop.createBackup({ kind: "config", reason: "settings-manual" })));
    document.getElementById("restoreBackup").addEventListener("click", () => {
      const backupId = ui.backupSelect.value;
      if (!backupId) {
        setMessage("No backup selected.");
        return;
      }

      if (!confirm("Restore this backup? The current target will be backed up first, then replaced.")) {
        return;
      }

      run("Restore backup", () => window.desktop.restoreBackup(backupId));
    });

    refresh().catch(error => {
      ui.subtitle.textContent = error?.message || "Settings failed to load.";
    });
  </script>
</body>
</html>
""";
}
