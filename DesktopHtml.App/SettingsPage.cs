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
      --bg: #0f1115;
      --panel: #151920;
      --panel-2: #1b2029;
      --line: rgba(255, 255, 255, 0.08);
      --line-strong: rgba(255, 255, 255, 0.16);
      --ink: #e8eaed;
      --muted: #99a1ab;
      --faint: #6b7280;
      --accent: #6cc7b5;
      --accent-dim: rgba(108, 199, 181, 0.14);
      --danger: #e07a7a;
      --danger-dim: rgba(224, 122, 122, 0.12);
      --warn: #d9c46f;
      --radius: 10px;
    }

    * { box-sizing: border-box; }

    html, body {
      height: 100%;
      margin: 0;
    }

    body {
      display: flex;
      background: var(--bg);
      color: var(--ink);
      font-family: "Segoe UI Variable Text", "Segoe UI", system-ui, sans-serif;
      font-size: 14px;
      line-height: 1.45;
    }

    /* ---------------- sidebar ---------------- */

    .sidebar {
      flex: none;
      width: 216px;
      display: flex;
      flex-direction: column;
      gap: 4px;
      padding: 20px 12px;
      background: var(--panel);
      border-right: 1px solid var(--line);
    }

    .brand {
      padding: 4px 10px 16px;
    }

    .brand h1 {
      margin: 0;
      font-size: 15px;
      font-weight: 600;
      letter-spacing: 0.01em;
    }

    .brand span {
      font-size: 12px;
      color: var(--faint);
    }

    .nav-btn {
      display: flex;
      align-items: center;
      gap: 10px;
      width: 100%;
      border: none;
      border-radius: 8px;
      background: none;
      color: var(--muted);
      font: inherit;
      text-align: left;
      padding: 9px 12px;
      cursor: pointer;
    }

    .nav-btn:hover { background: rgba(255, 255, 255, 0.04); color: var(--ink); }

    .nav-btn.active {
      background: var(--accent-dim);
      color: var(--accent);
      font-weight: 600;
    }

    .nav-btn .glyph {
      width: 16px;
      text-align: center;
      font-size: 13px;
      opacity: 0.85;
    }

    .sidebar-footer {
      margin-top: auto;
      display: grid;
      gap: 8px;
      padding: 12px 10px 0;
      border-top: 1px solid var(--line);
      font-size: 12px;
      color: var(--faint);
    }

    .safe-badge {
      display: none;
      padding: 5px 8px;
      border-radius: 6px;
      background: var(--danger-dim);
      color: var(--danger);
      font-weight: 600;
    }

    .safe-badge.on { display: block; }

    /* ---------------- content ---------------- */

    .content {
      flex: 1;
      min-width: 0;
      display: flex;
      flex-direction: column;
    }

    .topbar {
      flex: none;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      padding: 16px 28px;
      border-bottom: 1px solid var(--line);
      background: var(--panel);
    }

    .topbar h2 {
      margin: 0;
      font-size: 17px;
      font-weight: 600;
    }

    .topbar .actions { display: flex; gap: 8px; }

    .scroll {
      flex: 1;
      overflow-y: auto;
      padding: 24px 28px 40px;
    }

    .scroll::-webkit-scrollbar { width: 10px; }
    .scroll::-webkit-scrollbar-thumb {
      background: rgba(255, 255, 255, 0.12);
      border-radius: 5px;
      border: 2px solid var(--bg);
    }

    .section { display: none; max-width: 880px; margin: 0 auto; }
    .section.active { display: block; }

    .section > * + * { margin-top: 16px; }

    /* ---------------- primitives ---------------- */

    .card {
      background: var(--panel);
      border: 1px solid var(--line);
      border-radius: var(--radius);
      padding: 18px 20px;
    }

    .card > h3 {
      margin: 0 0 4px;
      font-size: 14px;
      font-weight: 600;
    }

    .card > .desc {
      margin: 0 0 14px;
      font-size: 12.5px;
      color: var(--muted);
    }

    .muted { color: var(--muted); }
    .small { font-size: 12px; }

    button {
      border: 1px solid var(--line-strong);
      border-radius: 8px;
      background: var(--panel-2);
      color: var(--ink);
      font: inherit;
      font-size: 13px;
      padding: 7px 14px;
      cursor: pointer;
      transition: background 0.1s, border-color 0.1s;
    }

    button:hover { border-color: rgba(255, 255, 255, 0.3); }

    button.primary {
      background: var(--accent-dim);
      border-color: rgba(108, 199, 181, 0.5);
      color: var(--accent);
      font-weight: 600;
    }

    button.primary:hover { border-color: var(--accent); }

    button.danger {
      background: none;
      border-color: rgba(224, 122, 122, 0.45);
      color: var(--danger);
    }

    button.danger:hover { background: var(--danger-dim); border-color: var(--danger); }

    button.ghost { background: none; border-color: transparent; color: var(--muted); }
    button.ghost:hover { color: var(--ink); border-color: var(--line-strong); }

    button:disabled { opacity: 0.45; cursor: default; }

    input[type="text"], select {
      border: 1px solid var(--line-strong);
      border-radius: 8px;
      background: var(--bg);
      color: var(--ink);
      font: inherit;
      font-size: 13px;
      padding: 7px 10px;
      min-width: 0;
    }

    input[type="text"]:focus-visible, select:focus-visible, button:focus-visible {
      outline: 1.5px solid var(--accent);
      outline-offset: 1px;
    }

    label.field {
      display: grid;
      gap: 5px;
      font-size: 12px;
      color: var(--muted);
    }

    .row { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
    .row.end { justify-content: flex-end; }
    .grow { flex: 1; min-width: 160px; }

    /* ---------------- switches ---------------- */

    .switch-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
      padding: 12px 0;
    }

    .switch-row + .switch-row { border-top: 1px solid var(--line); }

    .switch-row .label { font-weight: 500; }
    .switch-row .sub { font-size: 12px; color: var(--muted); }

    input.switch {
      appearance: none;
      flex: none;
      width: 40px;
      height: 22px;
      margin: 0;
      border-radius: 11px;
      background: var(--panel-2);
      border: 1px solid var(--line-strong);
      position: relative;
      cursor: pointer;
      transition: background 0.15s, border-color 0.15s;
    }

    input.switch::before {
      content: "";
      position: absolute;
      top: 2px;
      left: 2px;
      width: 16px;
      height: 16px;
      border-radius: 50%;
      background: var(--muted);
      transition: transform 0.15s, background 0.15s;
    }

    input.switch:checked {
      background: var(--accent-dim);
      border-color: var(--accent);
    }

    input.switch:checked::before {
      transform: translateX(18px);
      background: var(--accent);
    }

    /* ---------------- segmented control ---------------- */

    .segmented {
      display: inline-flex;
      border: 1px solid var(--line-strong);
      border-radius: 8px;
      overflow: hidden;
    }

    .segmented button {
      border: none;
      border-radius: 0;
      background: none;
      color: var(--muted);
      padding: 7px 16px;
    }

    .segmented button + button { border-left: 1px solid var(--line); }

    .segmented button.active {
      background: var(--accent-dim);
      color: var(--accent);
      font-weight: 600;
    }

    /* ---------------- status grid ---------------- */

    .stat-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
      gap: 1px;
      background: var(--line);
      border: 1px solid var(--line);
      border-radius: var(--radius);
      overflow: hidden;
    }

    .stat {
      background: var(--panel);
      padding: 12px 16px;
      min-width: 0;
    }

    .stat span {
      display: block;
      font-size: 11px;
      text-transform: uppercase;
      letter-spacing: 0.06em;
      color: var(--faint);
      margin-bottom: 3px;
    }

    .stat strong {
      display: block;
      font-size: 13px;
      font-weight: 600;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    /* ---------------- item lists (skins, monitors, backups) ---------------- */

    .item {
      display: flex;
      align-items: center;
      gap: 14px;
      padding: 13px 16px;
      background: var(--panel);
      border: 1px solid var(--line);
      border-radius: var(--radius);
    }

    .item + .item { margin-top: 8px; }

    .item .info { flex: 1; min-width: 0; }

    .item .title {
      font-weight: 600;
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .item .sub {
      font-size: 12px;
      color: var(--muted);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .badge {
      flex: none;
      font-size: 10.5px;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: var(--accent);
      background: var(--accent-dim);
      border-radius: 20px;
      padding: 2px 9px;
    }

    .badge.neutral { color: var(--muted); background: rgba(255, 255, 255, 0.06); }

    .warning {
      display: flex;
      gap: 10px;
      align-items: baseline;
      border: 1px solid rgba(217, 196, 111, 0.35);
      background: rgba(217, 196, 111, 0.07);
      border-radius: var(--radius);
      padding: 10px 14px;
      font-size: 12.5px;
      color: var(--muted);
    }

    .warning::before { content: "!"; font-weight: 700; color: var(--warn); }

    .empty {
      padding: 22px;
      text-align: center;
      color: var(--faint);
      font-size: 13px;
      border: 1px dashed var(--line-strong);
      border-radius: var(--radius);
    }

    /* ---------------- logs ---------------- */

    pre.logbox {
      margin: 0;
      padding: 14px 16px;
      background: #0b0d10;
      border: 1px solid var(--line);
      border-radius: var(--radius);
      font-family: "Cascadia Mono", Consolas, monospace;
      font-size: 11.5px;
      line-height: 1.55;
      color: var(--muted);
      overflow: auto;
      max-height: calc(100vh - 220px);
      white-space: pre-wrap;
      word-break: break-all;
    }

    /* ---------------- toast ---------------- */

    #toast {
      position: fixed;
      right: 20px;
      bottom: 20px;
      z-index: 50;
      max-width: 380px;
      padding: 11px 16px;
      border-radius: 8px;
      background: var(--panel-2);
      border: 1px solid var(--accent);
      color: var(--ink);
      font-size: 13px;
      box-shadow: 0 6px 24px rgba(0, 0, 0, 0.4);
      opacity: 0;
      transform: translateY(8px);
      pointer-events: none;
      transition: opacity 0.18s, transform 0.18s;
    }

    #toast.show { opacity: 1; transform: none; }
    #toast.error { border-color: var(--danger); }
  </style>
</head>
<body>
  <nav class="sidebar">
    <div class="brand">
      <h1>desktop.html</h1>
      <span id="versionLabel">&mdash;</span>
    </div>
    <button class="nav-btn active" data-tab="overview" type="button"><span class="glyph">&#9632;</span>Overview</button>
    <button class="nav-btn" data-tab="skins" type="button"><span class="glyph">&#10064;</span>Skins</button>
    <button class="nav-btn" data-tab="monitors" type="button"><span class="glyph">&#9707;</span>Monitors</button>
    <button class="nav-btn" data-tab="backups" type="button"><span class="glyph">&#8634;</span>Backups</button>
    <button class="nav-btn" data-tab="logs" type="button"><span class="glyph">&#8801;</span>Logs</button>
    <div class="sidebar-footer">
      <span class="safe-badge" id="safeBadge">Safe mode is on</span>
      <span id="configPath" style="overflow:hidden;text-overflow:ellipsis;white-space:nowrap;"></span>
    </div>
  </nav>

  <div class="content">
    <div class="topbar">
      <h2 id="sectionTitle">Overview</h2>
      <div class="actions">
        <button id="reloadSkin" type="button">Reload skin</button>
        <button id="refresh" class="ghost" type="button">Refresh</button>
      </div>
    </div>

    <div class="scroll">
      <!-- ============ OVERVIEW ============ -->
      <div class="section active" id="tab-overview">
        <div class="stat-grid" id="statusGrid"></div>

        <div class="card">
          <h3>Monitor mode</h3>
          <p class="desc">How skins are laid out across displays.</p>
          <div class="segmented" id="modeSegment">
            <button data-mode="single-monitor" type="button">Single</button>
            <button data-mode="per-monitor" type="button">Per monitor</button>
            <button data-mode="spanning" type="button">Spanning</button>
          </div>
        </div>

        <div class="card">
          <h3>Behavior</h3>
          <div class="switch-row">
            <div>
              <div class="label">Start with Windows</div>
              <div class="sub">Launch the desktop runtime when you sign in.</div>
            </div>
            <input class="switch" type="checkbox" id="startupToggle">
          </div>
          <div class="switch-row">
            <div>
              <div class="label">Pause on fullscreen apps</div>
              <div class="sub">Suspend skin rendering while a fullscreen app is active.</div>
            </div>
            <input class="switch" type="checkbox" id="fullscreenToggle">
          </div>
          <div class="switch-row">
            <div>
              <div class="label">Pause on battery</div>
              <div class="sub">Suspend skin rendering while running on battery power.</div>
            </div>
            <input class="switch" type="checkbox" id="batteryToggle">
          </div>
        </div>

        <div class="card">
          <h3>Safe mode</h3>
          <p class="desc">Closes all desktop skin windows while keeping settings and the tray available. Use this if a skin misbehaves.</p>
          <button id="safeToggle" class="danger" type="button">Enable safe mode</button>
        </div>
      </div>

      <!-- ============ SKINS ============ -->
      <div class="section" id="tab-skins">
        <div class="warning">Skins are full-trust local programs: they can run commands and open files through the bridge. Only install skins from folders you trust.</div>

        <div class="card">
          <h3>Install from folder</h3>
          <div class="row">
            <input id="installPath" class="grow" type="text" placeholder="C:\path\to\skin" spellcheck="false">
            <label class="row small muted" style="gap:6px;">
              <input id="installOverwrite" type="checkbox"> Overwrite
            </label>
            <button id="installSkin" class="primary" type="button">Install</button>
          </div>
        </div>

        <div id="skinList"></div>
      </div>

      <!-- ============ MONITORS ============ -->
      <div class="section" id="tab-monitors">
        <div id="monitorList"></div>

        <div class="card">
          <h3>Spanning</h3>
          <p class="desc">One skin stretched across several monitors. Leave all monitors unchecked to span everything.</p>
          <div class="row">
            <label class="field grow">Skin<select id="spanSkin"></select></label>
            <label class="field grow">Entry<select id="spanEntry"></select></label>
          </div>
          <div class="row" id="spanMonitors" style="margin-top:10px;"></div>
          <div class="row end" style="margin-top:14px;">
            <button id="applySpan" class="primary" type="button">Use spanning mode</button>
          </div>
        </div>
      </div>

      <!-- ============ BACKUPS ============ -->
      <div class="section" id="tab-backups">
        <div class="row">
          <p class="muted small grow" style="margin:0;">Automatic backups are created before risky operations. The current target is always backed up before a restore.</p>
          <button id="createConfigBackup" type="button">Create config backup</button>
        </div>
        <div id="backupList"></div>
      </div>

      <!-- ============ LOGS ============ -->
      <div class="section" id="tab-logs">
        <div class="row end">
          <button id="openLogs" class="ghost" type="button">Open log folder</button>
        </div>
        <pre class="logbox" id="logBox">Loading...</pre>
      </div>
    </div>
  </div>

  <div id="toast" role="status" aria-live="polite"></div>

  <script>
    const state = { runtime: null, config: null, monitors: [], skins: [], backups: [] };

    const $ = id => document.getElementById(id);

    /* ---------------- toast + actions ---------------- */

    function toast(text, isError) {
      const el = $("toast");
      el.textContent = text;
      el.classList.toggle("error", !!isError);
      el.classList.add("show");
      clearTimeout(toast._t);
      toast._t = setTimeout(() => el.classList.remove("show"), 2600);
    }

    async function run(label, action) {
      try {
        await action();
        await refresh();
        toast(`${label} complete.`);
      } catch (error) {
        await refresh().catch(() => {});
        toast(error?.message || `${label} failed.`, true);
      }
    }

    /* ---------------- tabs ---------------- */

    const tabTitles = {
      overview: "Overview",
      skins: "Skins",
      monitors: "Monitors",
      backups: "Backups",
      logs: "Logs"
    };

    function showTab(tab) {
      for (const btn of document.querySelectorAll(".nav-btn")) {
        btn.classList.toggle("active", btn.dataset.tab === tab);
      }
      for (const section of document.querySelectorAll(".section")) {
        section.classList.toggle("active", section.id === `tab-${tab}`);
      }
      $("sectionTitle").textContent = tabTitles[tab] || tab;
      sessionStorage.setItem("settings-tab", tab);
    }

    for (const btn of document.querySelectorAll(".nav-btn")) {
      btn.addEventListener("click", () => showTab(btn.dataset.tab));
    }

    /* ---------------- shared helpers ---------------- */

    function entriesFor(skinId) {
      const skin = state.skins.find(item => item.id === skinId);
      if (!skin) return ["index.html"];
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
        option.textContent = skin.name;
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

    function el(tag, className, text) {
      const node = document.createElement(tag);
      if (className) node.className = className;
      if (text != null) node.textContent = text;
      return node;
    }

    /* ---------------- overview ---------------- */

    function stat(name, value) {
      const item = el("div", "stat");
      const label = el("span", null, name);
      const strong = el("strong", null, value ?? "—");
      strong.title = value ?? "";
      item.append(label, strong);
      return item;
    }

    function renderOverview() {
      const grid = $("statusGrid");
      grid.textContent = "";
      grid.append(
        stat("Version", state.runtime.appVersion),
        stat("Active skin", state.config.skins.activeSkinId || "(none)"),
        stat("Entry", state.config.skins.entry),
        stat("Mode", state.config.skins.activeMode),
        stat("Placement", state.config.desktop.placementMode),
        stat("Monitors", String(state.monitors.length))
      );

      for (const btn of document.querySelectorAll("#modeSegment button")) {
        btn.classList.toggle("active", btn.dataset.mode === state.config.skins.activeMode);
      }

      $("startupToggle").checked = !!state.config.app.startWithWindows;
      $("fullscreenToggle").checked = !!state.config.performance.pauseWhenFullscreenAppActive;
      $("batteryToggle").checked = !!state.config.performance.pauseWhenOnBattery;

      const safe = !!state.config.app.safeMode;
      $("safeToggle").textContent = safe ? "Disable safe mode" : "Enable safe mode";
      $("safeToggle").classList.toggle("danger", !safe);
      $("safeToggle").classList.toggle("primary", safe);
      $("safeBadge").classList.toggle("on", safe);

      $("versionLabel").textContent = `v${state.runtime.appVersion}`;
      $("configPath").textContent = state.runtime.configFile || "";
      $("configPath").title = state.runtime.configFile || "";
    }

    /* ---------------- skins ---------------- */

    function renderSkins() {
      const list = $("skinList");
      list.textContent = "";

      if (!state.skins.length) {
        list.append(el("div", "empty", "No skins installed yet."));
        return;
      }

      const activeId = state.config.skins.activeSkinId;
      const sorted = [...state.skins].sort((a, b) =>
        (b.id === activeId) - (a.id === activeId) || a.name.localeCompare(b.name));

      for (const skin of sorted) {
        const item = el("div", "item");
        const info = el("div", "info");
        const title = el("div", "title", `${skin.name} `);
        title.append(el("span", "muted small", `v${skin.version}`));
        if (skin.id === activeId) title.append(el("span", "badge", "Active"));
        const perms = skin.permissions || {};
        if (perms.network) title.append(el("span", "badge neutral", "net"));
        if (perms.rawExecution) title.append(el("span", "badge neutral", "exec"));
        const storage = skin.storageBytes > 0
          ? ` · storage ${skin.storageBytes >= 1048576
              ? (skin.storageBytes / 1048576).toFixed(1) + " MB"
              : Math.max(1, Math.round(skin.storageBytes / 1024)) + " KB"}`
          : "";
        const sub = el("div", "sub", `${skin.id} · ${skin.skinPath}${storage}`);
        sub.title = skin.skinPath;
        info.append(title, sub);

        const entrySelect = document.createElement("select");
        fillEntrySelect(entrySelect, skin.id,
          skin.id === activeId ? state.config.skins.entry : (skin.entry || "index.html"));

        const activateBtn = el("button", skin.id === activeId ? "" : "primary",
          skin.id === activeId ? "Reactivate" : "Activate");
        activateBtn.type = "button";
        activateBtn.addEventListener("click", () => {
          if (!confirm(`Activate "${skin.name}"? Skins run with full trust.`)) return;
          run("Activate skin", () => window.desktop.activateSkin(skin.id, entrySelect.value));
        });

        const folderBtn = el("button", "ghost", "Folder");
        folderBtn.type = "button";
        folderBtn.addEventListener("click", () => window.desktop.openSkinFolder(skin.id));

        item.append(info, entrySelect, activateBtn, folderBtn);
        list.append(item);
      }
    }

    /* ---------------- monitors ---------------- */

    function renderMonitors() {
      const list = $("monitorList");
      list.textContent = "";
      $("spanMonitors").textContent = "";

      for (const monitor of state.monitors) {
        const assignment = state.config.skins.perMonitor?.[monitor.id] || {};
        const item = el("div", "item");
        const info = el("div", "info");
        const title = el("div", "title", monitor.id);
        if (monitor.isPrimary) title.append(el("span", "badge neutral", "Primary"));
        const geometry = `${monitor.workArea.width}×${monitor.workArea.height} at ${monitor.workArea.left},${monitor.workArea.top}`;
        const assigned = assignment.skinId ? ` · assigned: ${assignment.skinId}` : "";
        info.append(title, el("div", "sub", geometry + assigned));

        const skinSelect = document.createElement("select");
        const entrySelect = document.createElement("select");
        fillSkinSelect(skinSelect, assignment.skinId || state.config.skins.activeSkinId);
        fillEntrySelect(entrySelect, skinSelect.value, assignment.entry || state.config.skins.entry);
        skinSelect.addEventListener("change", () => fillEntrySelect(entrySelect, skinSelect.value, entrySelect.value));

        const assignBtn = el("button", "primary", "Assign");
        assignBtn.type = "button";
        assignBtn.addEventListener("click", () => run("Assign monitor", () =>
          window.desktop.assignMonitorSkin(monitor.id, skinSelect.value, entrySelect.value)));

        const clearBtn = el("button", "ghost", "Clear");
        clearBtn.type = "button";
        clearBtn.addEventListener("click", () => run("Clear monitor", () =>
          window.desktop.clearMonitorSkin(monitor.id)));

        item.append(info, skinSelect, entrySelect, assignBtn, clearBtn);
        list.append(item);

        const checkLabel = el("label", "row small muted");
        checkLabel.style.gap = "6px";
        const checkbox = document.createElement("input");
        checkbox.type = "checkbox";
        checkbox.value = monitor.id;
        checkbox.checked = !!state.config.skins.spanning.monitors?.includes(monitor.id);
        checkLabel.append(checkbox, monitor.id);
        $("spanMonitors").append(checkLabel);
      }

      const selected = state.config.skins.spanning.skinId || state.config.skins.activeSkinId || state.skins[0]?.id;
      fillSkinSelect($("spanSkin"), selected);
      fillEntrySelect($("spanEntry"), $("spanSkin").value, state.config.skins.spanning.entry || state.config.skins.entry);
    }

    /* ---------------- backups ---------------- */

    function renderBackups() {
      const list = $("backupList");
      list.textContent = "";

      const backups = state.backups.slice(0, 25);
      if (!backups.length) {
        list.append(el("div", "empty", "No backups yet."));
        return;
      }

      for (const backup of backups) {
        const item = el("div", "item");
        const info = el("div", "info");
        const title = el("div", "title", new Date(backup.createdUtc).toLocaleString());
        title.append(el("span", "badge neutral", backup.kind));
        info.append(title, el("div", "sub", `${backup.reason} · ${backup.relativeTargetPath}`));

        const restoreBtn = el("button", "danger", "Restore");
        restoreBtn.type = "button";
        restoreBtn.addEventListener("click", () => {
          if (!confirm("Restore this backup? The current target will be backed up first, then replaced.")) return;
          run("Restore backup", () => window.desktop.restoreBackup(backup.id));
        });

        item.append(info, restoreBtn);
        list.append(item);
      }
    }

    /* ---------------- refresh ---------------- */

    async function refresh() {
      // Each call settles independently so one failure cannot blank the
      // whole window; sections render with whatever data is available.
      const results = await Promise.allSettled([
        window.desktop.getRuntimeInfo(),
        window.desktop.getConfig(),
        window.desktop.getMonitors(),
        window.desktop.getInstalledSkinDetails(),
        window.desktop.getBackups(),
        window.desktop.getLogs({ maxLines: 120 })
      ]);

      const [runtime, config, monitors, skins, backups, logs] = results;
      const failed = [];
      const take = (result, name, assign) => {
        if (result.status === "fulfilled") assign(result.value);
        else failed.push(name);
      };

      take(runtime, "runtime", v => { state.runtime = v; });
      take(config, "config", v => { state.config = v; });
      take(monitors, "monitors", v => { state.monitors = v; });
      take(skins, "skins", v => { state.skins = v; });
      take(backups, "backups", v => { state.backups = v; });

      if (state.runtime && state.config) renderOverview();
      if (state.config) {
        renderSkins();
        renderMonitors();
        renderBackups();
      }
      if (logs.status === "fulfilled") {
        $("logBox").textContent = logs.value.join("\n") || "(no log entries)";
      }

      if (failed.length) {
        throw new Error(`Failed to load: ${failed.join(", ")}`);
      }
    }

    /* ---------------- static wiring ---------------- */

    $("refresh").addEventListener("click", () =>
      refresh().then(() => toast("Refreshed.")).catch(e => toast(e?.message || "Refresh failed.", true)));
    $("reloadSkin").addEventListener("click", () => run("Reload skin", () => window.desktop.reloadSkin()));
    $("openLogs").addEventListener("click", () => window.desktop.openLogs());

    $("startupToggle").addEventListener("change", e =>
      run(e.target.checked ? "Enable startup" : "Disable startup", () =>
        window.desktop.setStartupEnabled(e.target.checked)));
    $("fullscreenToggle").addEventListener("change", e =>
      run("Update fullscreen pause", () =>
        window.desktop.setConfigPatch({ performance: { pauseWhenFullscreenAppActive: e.target.checked } })));
    $("batteryToggle").addEventListener("change", e =>
      run("Update battery pause", () =>
        window.desktop.setConfigPatch({ performance: { pauseWhenOnBattery: e.target.checked } })));

    $("safeToggle").addEventListener("click", () => {
      const next = !state.config.app.safeMode;
      const prompt = next
        ? "Enable safe mode? Desktop skin windows will close and settings will stay available."
        : "Disable safe mode and allow desktop skins again?";
      if (!confirm(prompt)) return;
      run(next ? "Enable safe mode" : "Disable safe mode", () =>
        window.desktop.setConfigPatch({ app: { safeMode: next } }));
    });

    for (const btn of document.querySelectorAll("#modeSegment button")) {
      btn.addEventListener("click", () =>
        run(`Set ${btn.textContent.toLowerCase()} mode`, () => window.desktop.setMonitorMode(btn.dataset.mode)));
    }

    $("spanSkin").addEventListener("change", () =>
      fillEntrySelect($("spanEntry"), $("spanSkin").value, $("spanEntry").value));

    $("installSkin").addEventListener("click", () => {
      const path = $("installPath").value.trim();
      if (!path) {
        toast("Enter a skin folder path first.", true);
        return;
      }
      if (!confirm("Install this full-trust skin folder?")) return;
      run("Install skin", () => window.desktop.installSkinFromFolder(path, { overwrite: $("installOverwrite").checked }));
    });

    $("applySpan").addEventListener("click", () => {
      const monitors = [...$("spanMonitors").querySelectorAll("input:checked")].map(input => input.value);
      run("Configure spanning", () => window.desktop.configureSpanning($("spanSkin").value, {
        entry: $("spanEntry").value,
        monitors
      }));
    });

    $("createConfigBackup").addEventListener("click", () =>
      run("Create config backup", () => window.desktop.createBackup({ kind: "config", reason: "settings-manual" })));

    /* ---------------- boot ---------------- */

    showTab(sessionStorage.getItem("settings-tab") || "overview");
    refresh().catch(error => {
      $("sectionTitle").textContent = "Settings failed to load";
      toast(error?.message || "Settings failed to load.", true);
    });
  </script>
</body>
</html>
""";
}
