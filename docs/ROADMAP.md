# topweb / desktop.html — Agent Roadmap

This document is for AI agents (and humans) working on this repository. It describes the
project's philosophy, what should be built next, and what needs tidying. Treat it as the
prioritized backlog; update it when you complete or obsolete an item.

## Project philosophy — read this first

1. **Skins are trusted, powerful, and thin.** A skin is local HTML/CSS/JS running full-trust
   in a WebView2 wallpaper window. Skins may run commands, call APIs, and touch the file
   system — that is the point. But the *host* should provide the hard primitives (terminal
   sessions, HTTP, icons, file watching) so skins stay mostly UI. When a skin has to
   hand-roll infrastructure (see the ANSI parser in `samples/glyph/script.js`), that is a
   signal the host or a shared library should absorb it.
2. **Power-user friendly.** Keyboard-first, CLI-complete (`desktop-html.exe` should be able
   to do everything the settings UI can), JSON-flagged output, scriptable.
3. **Low resource usage.** This runs 24/7 as the user's desktop. Prefer push (events) over
   poll, one-shot probes over timers, host-side caches over per-skin work. Never add a
   permanent polling loop to the host without a strong reason.
4. **Docs are load-bearing.** Skins are frequently written by AI agents from
   `docs/agent-skin-prompt.md`, `docs/bridge-api.md`, and `docs/skin-authoring.md`. Any
   bridge change that isn't documented there effectively doesn't exist. Updating these docs
   is part of the definition of done for every bridge change.

## Architecture map

- `DesktopHtml.App` — WPF/WebView2 host, tray, CLI, settings window, and
  `DesktopBridgeDispatcher.cs` (routes `window.desktop.*` calls).
- `DesktopHtml.Core` — services (FileSystem, Execution, Skins, Monitors, Placement,
  Storage, Config, Logging). Testable, no UI dependencies.
- `DesktopHtml.Tests` — xUnit tests (currently mostly `FileSystemService`).
- `samples/*` — skins; `glyph` is the flagship/reference skin.
- `docs/*` — the contract that skin-writing agents build against.

## P0 — Tidying / debt (do these before or alongside new features)

- [x] **Document the newer bridge methods.** `httpFetch`, terminal sessions, `getIcon`,
      `watch`/`unwatch`, system stats, `notify`, and the full event table are now in
      `docs/bridge-api.md`.
- [x] **Refactor `DesktopBridgeDispatcher.cs` back into Core services.** Done:
      `Terminal/TerminalSessionService`, `Network/HttpFetchService`,
      `FileSystem/FileSystemWatchService`, `SystemInfo/SystemStatsService` in Core with
      tests; `IconService` in App (needs WPF imaging); shortcut resolution unified in
      `FileSystem/ShortcutResolver`. Terminal sessions are page-scoped and killed on
      skin reload (previously old WSL shells leaked across reloads).
- [x] **Add a `.gitattributes`** — done; run `git add --renormalize .` once to settle
      existing files.
- [x] **Surface the manifest `permissions` block.** Schema documented in
      `docs/skin-authoring.md`; `skin validate` prints declarations; the settings skin
      list shows net/exec badges. Still declarative-only by design.
- [ ] **Scrub machine-specific paths from samples before public release.** Glyph is
      clean. The remaining hits are *generated* launcher data files (`desktop-nexus`,
      `aurora`, `nocturne`, `meridian`, `nerv` desktop-items/data files) that
      intentionally snapshot this machine's desktop — regenerate or delete them at
      release time rather than hand-editing.
- [x] **Test coverage for launcher-critical paths:** `listDesktopItems` merge/dedupe,
      terminal session lifecycle (stdio + pty), `.url` shortcut resolution, zip
      install round-trip. (.lnk resolution keeps its existing COM-dependent test.)
- [x] **`skin validate --strict` no longer false-positives on JavaScript.** Asset-existence
      scanning now runs only on `.html`/`.css` (JS builds paths dynamically); `.js` keeps
      the private-path and syntax checks. Vendored minified libs stop tripping it.
- [x] **Fix flaky test:** `RestoreAsync_ReplacesExistingSkinFolder` — root cause was
      `Directory.Move` racing Windows' asynchronous directory teardown right after
      `Directory.Delete`; `BackupService.RestoreDirectory` now retries the rename
      briefly.

## P1 — Host primitives (highest value new work)

- [x] **ConPTY-backed terminal sessions.** `pty: true` on `terminal.start` (Windows
      pseudoconsole) plus `terminal.resize`; stdio mode remains the default. Gotcha
      encoded in `PseudoConsoleProcess`: STARTF_USESTDHANDLES with NULL handles is
      required or the child bypasses the pty when the host has redirected stdio.
      Glyph now uses ConPTY through the shared xterm.js widget (`samples/_lib/`);
      the `script(1)` hack and the hand-rolled ANSI parser are gone.
- [x] **File-system watcher events.** `desktop.watch`/`unwatch` with 300 ms host-side
      debounce and batched `fileSystemChanged` events. Glyph now watches both Desktop
      folders for live refresh.
- [x] **Visibility / power events.** `powerStatusChanged` (SystemEvents) and
      `desktopVisibilityChanged` (WinEvent hooks + debounced EnumWindows in
      `DesktopVisibilityService`; no polling). Glyph pauses its rain/donut/wave loop on
      both; other samples should copy that pattern.
- [x] **System stats API.** `getSystemStats` + `subscribeSystemStats` (≥ 1000 ms
      enforced) backed by Win32 counters. Glyph shows live CPU/RAM/NET bars in its
      status panel from the subscription.
- [x] **Host-side icon cache + jumbo icons.** Cached by path+size+mtime; sizes
      small/large/extralarge/jumbo via `SHGetImageList`.
- [x] **Notifications.** `desktop.notify(title, message)` via tray balloon. A proper
      WinRT toast (with actions) would need the windows10 TFM — revisit if balloons
      feel too limited.
- [x] **`httpFetch` hardening.** `timeoutMs` (30 s default, 120 s max), `maxResponseBytes`
      cap with `truncated` flag, `asBase64` binary bodies, `contentType` in result.
      Not done: request cancellation from the page (AbortSignal-style).

## P2 — Skin developer experience

- [x] **Shared skin-side library (terminal widget).** `samples/_lib/` ships
      `topweb-terminal.js` + vendored xterm.js/fit-addon (MIT): a full ConPTY terminal
      in ~10 skin lines (see glyph). Skins copy the files into their own `vendor/`
      folder — installs are self-contained. Possible future helpers: icon-cached file
      lists, debounced storage, `onEvent` subscription utilities.
- [x] **Dev watch mode.** Already existed: `skin dev <folder> --watch` (validate,
      install, activate, IPC reload on change). Documented in `docs/cli.md`.
- [x] **Skin packaging.** `skin pack <folder>` produces `<id>-<version>.zip`;
      `skin install <file.zip>` (and the settings/bridge install path) extracts,
      validates, and installs. Install-from-URL deliberately not implemented (trust
      prompt design needed first).
- [ ] **Keep `docs/agent-skin-prompt.md` in lockstep** with every new bridge capability —
      it is the template agents use to generate skins.

## Showcase skin

- **Halcyon** (`samples/halcyon`, id `topweb.halcyon`) — dusk-glass reference skin that
  exercises most of the bridge at once: jumbo-icon live launcher (`getIcon` "jumbo" +
  `watch`), lazy ConPTY PowerShell drawer (shared terminal widget), now-playing card with
  album art + controls + a global media hotkey (`media.*` + `registerHotkey`), system
  gauges (`subscribeSystemStats`), weather (`httpFetch`), and paste/drop-to-desktop. Fully
  event-driven — no rAF/poll loops; CSS aurora pauses on `desktopVisibilityChanged`/battery.
  Passes `skin validate --strict`.

## P3 — Nice-to-haves (only after the above)

- [x] Drag-and-drop from Explorer into skins — done via HTML5 drop events +
      `writeFileBase64` (see glyph); clipboard paste-to-desktop via
      `clipboard.saveToDirectory`. Remaining gap: files > ~64 MB (base64 over the
      bridge is heavy); a host-side native drop target or chunked write would fix it.
- [x] Now-playing / media info via SMTC: `media.getNowPlaying` (+album-art data URLs),
      `media.control`, `media.subscribe` → `mediaChanged` events. Required switching the
      App TFM to net8.0-windows10.0.19041.0 (output path changed accordingly in docs).
- [x] Global hotkeys: `registerHotkey(key, modifiers)` → `hotkeyPressed` events via
      RegisterHotKey + hidden message window; page-scoped, released on reload.
- [x] Per-skin storage inspection: `getInstalledSkinDetails` reports `storageBytes`,
      shown in the settings skin list. (Quotas deliberately not enforced.)
- [ ] Multi-skin layouts (different skin per monitor already works; consider zones/panels).

## Conventions for agents working here

- Business logic goes in `DesktopHtml.Core` with tests; `DesktopHtml.App` stays thin
  (UI, WebView2 wiring, dispatcher routing).
- Build/test: `dotnet build .\desktop-html.sln` and `dotnet test .\desktop-html.sln`.
- Every bridge method addition/change updates: `DesktopBridgeBootstrap.cs` (JS surface),
  `GetCapabilities()` in the dispatcher, `docs/bridge-api.md`, and — if skin-facing
  patterns change — `docs/skin-authoring.md` + `docs/agent-skin-prompt.md`.
- Skins must degrade gracefully when `window.desktop` is absent (they get previewed in
  plain browsers) and must not hardcode machine-specific paths.
- Respect the resource philosophy: no new permanent timers in the host; skins should pause
  animation work when it cannot be seen (use the visibility events once they exist).
