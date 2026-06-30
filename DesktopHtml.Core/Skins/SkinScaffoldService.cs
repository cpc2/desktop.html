using System.Text.Json;

namespace DesktopHtml.Core.Skins;

public sealed record SkinScaffoldResult(
    string SkinId,
    string Template,
    string Directory,
    IReadOnlyList<string> Files);

public sealed class SkinScaffoldService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public async Task<SkinScaffoldResult> ScaffoldAsync(
        string skinId,
        string targetDirectory,
        string template,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        var normalizedId = NormalizeSkinId(skinId);
        var normalizedTemplate = NormalizeTemplate(template);
        var fullDirectory = Path.GetFullPath(targetDirectory);

        if (Directory.Exists(fullDirectory) && Directory.EnumerateFileSystemEntries(fullDirectory).Any() && !overwrite)
        {
            throw new InvalidOperationException($"Target skin directory already exists and is not empty: {fullDirectory}");
        }

        Directory.CreateDirectory(fullDirectory);

        var files = CreateTemplateFiles(normalizedId, normalizedTemplate);
        foreach (var file in files)
        {
            var path = Path.Combine(fullDirectory, file.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, file.Value, cancellationToken).ConfigureAwait(false);
        }

        return new SkinScaffoldResult(normalizedId, normalizedTemplate, fullDirectory, files.Keys.ToArray());
    }

    private static Dictionary<string, string> CreateTemplateFiles(string skinId, string template)
    {
        var name = ToDisplayName(skinId);
        var manifest = JsonSerializer.Serialize(new SkinManifest
        {
            Id = skinId,
            Name = name,
            Version = "0.1.0",
            Author = "Unknown",
            Description = $"{name} desktop.html skin.",
            Entry = "index.html",
            Entries = new Dictionary<string, string> { ["main"] = "index.html" },
            MinimumDesktopHtmlVersion = "0.1.0",
            Permissions = new SkinPermissions
            {
                FullTrust = true,
                Network = true,
                RawExecution = true
            }
        }, JsonOptions);

        if (template == "rabbits")
        {
            return new Dictionary<string, string>
            {
                ["manifest.json"] = manifest + Environment.NewLine,
                ["index.html"] = CreateRabbitsHtml(name),
                ["style.css"] = CreateRabbitsCss(),
                ["script.js"] = CreateRabbitsScript(),
                ["rabbits-theme.js"] = CreateRabbitsThemeHelper()
            };
        }

        return new Dictionary<string, string>
        {
            ["manifest.json"] = manifest + Environment.NewLine,
            ["index.html"] = CreateHtml(name, template),
            ["style.css"] = CreateCss(template),
            ["script.js"] = CreateScript(template)
        };
    }

    private static string CreateRabbitsHtml(string name) =>
        $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{{name}}</title>
  <link rel="stylesheet" href="style.css">
</head>
<body>
  <main class="desktop">
    <section class="hero">
      <p class="eyebrow">optional Rabbits theme support</p>
      <h1>{{name}}</h1>
      <p id="status">Loading theme support...</p>
      <div class="actions">
        <button type="button" data-theme="night">Night</button>
        <button type="button" data-theme="day">Day</button>
        <button type="button" data-action="import">Import SVG</button>
        <button type="button" data-action="reset">Reset</button>
      </div>
    </section>
    <section class="drop-zone" id="dropZone">
      <p>Drop a Hundred Rabbits compatible .svg theme here.</p>
      <div class="swatches" id="swatches" aria-label="Active palette"></div>
    </section>
    <section class="workspace">
      <button type="button" data-action="settings">Settings</button>
      <button type="button" data-action="logs">Logs</button>
    </section>
  </main>
  <script src="rabbits-theme.js"></script>
  <script src="script.js"></script>
</body>
</html>
""";

    private static string CreateRabbitsCss() =>
        """
:root {
  color-scheme: dark;
  font-family: "Segoe UI", system-ui, sans-serif;
  --background: #17151f;
  --f_high: #f5f0ff;
  --f_med: #c8c0d8;
  --f_low: #8a8299;
  --f_inv: #17151f;
  --b_high: #fff3c7;
  --b_med: #352d46;
  --b_low: #211d2d;
  --b_inv: #d97178;
}

* {
  box-sizing: border-box;
}

html,
body,
.desktop {
  width: 100%;
  height: 100%;
  margin: 0;
}

body {
  overflow: hidden;
  background: var(--background);
  color: var(--f_high);
}

.desktop {
  display: grid;
  grid-template-columns: minmax(280px, 420px) 1fr;
  grid-template-rows: auto 1fr;
  gap: 16px;
  padding: 24px;
}

.hero,
.drop-zone,
.workspace {
  border: 1px solid color-mix(in srgb, var(--f_low), transparent 55%);
  border-radius: 8px;
  background: color-mix(in srgb, var(--b_low), transparent 10%);
  padding: 18px;
}

.drop-zone {
  grid-row: span 2;
  display: grid;
  align-content: space-between;
  gap: 18px;
  outline: 2px dashed transparent;
  outline-offset: -8px;
}

.drop-zone.is-dragging {
  outline-color: var(--b_inv);
}

.workspace {
  display: flex;
  align-items: end;
  gap: 10px;
}

.eyebrow {
  margin: 0 0 8px;
  color: var(--b_high);
  font-size: 12px;
  text-transform: uppercase;
}

h1,
p {
  margin: 0 0 12px;
}

h1 {
  font-size: 34px;
}

p {
  color: var(--f_med);
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

button {
  border: 1px solid color-mix(in srgb, var(--f_low), transparent 45%);
  border-radius: 6px;
  background: var(--b_med);
  color: var(--f_high);
  padding: 8px 12px;
  font: inherit;
}

button:hover,
button:focus-visible {
  border-color: var(--b_inv);
  outline: none;
}

.swatches {
  display: grid;
  grid-template-columns: repeat(9, minmax(20px, 1fr));
  gap: 6px;
}

.swatch {
  min-height: 72px;
  border: 1px solid color-mix(in srgb, var(--f_low), transparent 50%);
  border-radius: 6px;
}
""";

    private static string CreateRabbitsScript() =>
        """
const status = document.getElementById("status");
const swatches = document.getElementById("swatches");
const dropZone = document.getElementById("dropZone");

const palettes = {
  night: {
    background: "#17151f",
    f_high: "#f5f0ff",
    f_med: "#c8c0d8",
    f_low: "#8a8299",
    f_inv: "#17151f",
    b_high: "#fff3c7",
    b_med: "#352d46",
    b_low: "#211d2d",
    b_inv: "#d97178"
  },
  day: {
    background: "#f3efe5",
    f_high: "#1d2830",
    f_med: "#59636b",
    f_low: "#8b9398",
    f_inv: "#fff8ed",
    b_high: "#163a5f",
    b_med: "#d8e5e8",
    b_low: "#fff8ed",
    b_inv: "#c85d4d"
  }
};

function renderSwatches(theme) {
  swatches.innerHTML = "";
  for (const [key, value] of Object.entries(theme)) {
    const swatch = document.createElement("div");
    swatch.className = "swatch";
    swatch.title = `${key}: ${value}`;
    swatch.style.background = value;
    swatches.appendChild(swatch);
  }
}

const theme = window.DesktopHtmlRabbitsTheme.createController({
  defaultTheme: palettes.night,
  onLoad: active => {
    renderSwatches(active);
    status.textContent = "Theme loaded. Drop a compatible SVG to replace it.";
  },
  onError: error => {
    status.textContent = error?.message || "Theme import failed.";
  }
});

document.addEventListener("click", async event => {
  const themeName = event.target?.dataset?.theme;
  const action = event.target?.dataset?.action;

  try {
    if (themeName) {
      await theme.load(palettes[themeName]);
    } else if (action === "import") {
      theme.openFilePicker();
    } else if (action === "reset") {
      await theme.reset();
    } else if (action === "settings" && window.desktop) {
      await window.desktop.openSettings();
    } else if (action === "logs" && window.desktop) {
      await window.desktop.openLogs();
    }
  } catch (error) {
    status.textContent = error?.message || "Theme action failed.";
  }
});

theme.enableDropImport(dropZone);
theme.start().catch(error => {
  status.textContent = error?.message || "Theme support failed to start.";
});
""";

    private static string CreateRabbitsThemeHelper() =>
        """
(function () {
  "use strict";

  const keys = [
    "background",
    "f_high",
    "f_med",
    "f_low",
    "f_inv",
    "b_high",
    "b_med",
    "b_low",
    "b_inv"
  ];

  const fallbackTheme = {
    background: "#17151f",
    f_high: "#f5f0ff",
    f_med: "#c8c0d8",
    f_low: "#8a8299",
    f_inv: "#17151f",
    b_high: "#fff3c7",
    b_med: "#352d46",
    b_low: "#211d2d",
    b_inv: "#d97178"
  };

  function isColor(value) {
    return /^#?([0-9a-f]{3}|[0-9a-f]{6})$/i.test(value || "");
  }

  function normalizeTheme(input) {
    const theme = {};
    for (const key of keys) {
      const value = input?.[key];
      if (!isColor(value)) {
        throw new Error(`Invalid Rabbits theme: ${key} must be a hex color.`);
      }
      theme[key] = value.startsWith("#") ? value : `#${value}`;
    }
    return theme;
  }

  function parseSvgTheme(text) {
    const documentXml = new DOMParser().parseFromString(text, "text/xml");
    const theme = {};
    for (const key of keys) {
      const element = documentXml.getElementById(key);
      theme[key] = element?.getAttribute("fill");
    }
    return normalizeTheme(theme);
  }

  function parseInput(input) {
    if (typeof input === "string") {
      const text = input.trim();
      if (text.startsWith("{")) {
        return normalizeTheme(JSON.parse(text));
      }
      return parseSvgTheme(text);
    }
    return normalizeTheme(input);
  }

  function createStorage(storageKey) {
    return {
      async get() {
        if (window.desktop?.storage) {
          return window.desktop.storage.get(storageKey);
        }
        return localStorage.getItem(storageKey);
      },
      async set(value) {
        const text = JSON.stringify(value);
        if (window.desktop?.storage) {
          await window.desktop.storage.set(storageKey, text);
        } else {
          localStorage.setItem(storageKey, text);
        }
      }
    };
  }

  function applyTheme(target, theme) {
    for (const [key, value] of Object.entries(theme)) {
      target.style.setProperty(`--${key}`, value);
      target.style.setProperty(`--rabbits-${key.replace(/_/g, "-")}`, value);
    }
  }

  function createController(options = {}) {
    const target = options.target || document.documentElement;
    const storageKey = options.storageKey || "rabbits-theme";
    const defaultTheme = normalizeTheme(options.defaultTheme || fallbackTheme);
    const storage = createStorage(storageKey);
    let activeTheme = defaultTheme;

    async function load(input) {
      const nextTheme = parseInput(input);
      applyTheme(target, nextTheme);
      activeTheme = nextTheme;
      await storage.set(nextTheme);
      if (options.onLoad) {
        options.onLoad(nextTheme);
      }
      return nextTheme;
    }

    async function start() {
      const saved = await storage.get();
      if (saved) {
        return load(saved);
      }
      return load(defaultTheme);
    }

    function openFilePicker() {
      const input = document.createElement("input");
      input.type = "file";
      input.accept = ".svg,image/svg+xml,application/json";
      input.addEventListener("change", () => {
        const file = input.files?.[0];
        if (file) {
          readFile(file).then(load).catch(handleError);
        }
      });
      input.click();
    }

    function enableDropImport(element = window) {
      element.addEventListener("dragover", event => {
        event.preventDefault();
        element.classList?.add("is-dragging");
      });
      element.addEventListener("dragleave", () => {
        element.classList?.remove("is-dragging");
      });
      element.addEventListener("drop", event => {
        event.preventDefault();
        element.classList?.remove("is-dragging");
        const file = event.dataTransfer?.files?.[0];
        if (file) {
          readFile(file).then(load).catch(handleError);
        }
      });
    }

    function handleError(error) {
      if (options.onError) {
        options.onError(error);
      } else {
        console.error(error);
      }
    }

    return {
      start,
      load,
      reset: () => load(defaultTheme),
      openFilePicker,
      enableDropImport,
      getTheme: () => ({ ...activeTheme })
    };
  }

  function readFile(file) {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(String(reader.result || ""));
      reader.onerror = () => reject(reader.error || new Error("Could not read theme file."));
      reader.readAsText(file, "UTF-8");
    });
  }

  window.DesktopHtmlRabbitsTheme = { createController };
})();
""";

    private static string CreateHtml(string name, string template) =>
        $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{{name}}</title>
  <link rel="stylesheet" href="style.css">
</head>
<body data-template="{{template}}">
  <main class="desktop">
    <section class="panel">
      <p class="eyebrow">desktop.html skin</p>
      <h1>{{name}}</h1>
      <p id="status">Loading runtime...</p>
      <div class="actions">
        <button type="button" data-action="settings">Settings</button>
        <button type="button" data-action="logs">Logs</button>
      </div>
    </section>
    <section class="workspace" aria-label="Workspace"></section>
  </main>
  <script src="script.js"></script>
</body>
</html>
""";

    private static string CreateCss(string template)
    {
        var accent = template switch
        {
            "classic" => "#8fd14f",
            "launcher" => "#7db8ff",
            "dashboard" => "#f5c26b",
            _ => "#75d6c9"
        };

        return $$"""
:root {
  color-scheme: dark;
  font-family: "Segoe UI", system-ui, sans-serif;
  background: #101316;
  color: #f7f3e8;
}

* {
  box-sizing: border-box;
}

html,
body,
.desktop {
  width: 100%;
  height: 100%;
  margin: 0;
}

body {
  overflow: hidden;
  background: #101316;
}

.desktop {
  display: grid;
  grid-template-columns: minmax(260px, 360px) 1fr;
  gap: 18px;
  padding: 24px;
}

.panel,
.workspace {
  border: 1px solid rgba(255,255,255,.14);
  border-radius: 8px;
  background: rgba(255,255,255,.055);
  padding: 18px;
}

.eyebrow {
  margin: 0 0 8px;
  color: {{accent}};
  text-transform: uppercase;
  font-size: 12px;
}

h1,
p {
  margin: 0 0 12px;
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

button {
  border: 1px solid rgba(255,255,255,.18);
  border-radius: 6px;
  background: rgba(255,255,255,.08);
  color: inherit;
  padding: 8px 11px;
  font: inherit;
}

button:focus-visible {
  outline: 2px solid {{accent}};
  outline-offset: 2px;
}
""";
    }

    private static string CreateScript(string template) =>
        $$"""
const status = document.getElementById("status");

async function refresh() {
  if (!window.desktop) {
    status.textContent = "Mock preview: bridge unavailable.";
    return;
  }

  const info = await window.desktop.getRuntimeInfo();
  status.textContent = `running desktop.html ${info.appVersion} with the {{template}} template.`;
}

document.addEventListener("click", event => {
  const action = event.target?.dataset?.action;
  if (!action || !window.desktop) {
    return;
  }

  if (action === "settings") {
    window.desktop.openSettings();
  } else if (action === "logs") {
    window.desktop.openLogs();
  }
});

refresh().catch(error => {
  status.textContent = error?.message || "Skin failed to initialize.";
});
""";

    private static string NormalizeSkinId(string skinId)
    {
        if (string.IsNullOrWhiteSpace(skinId))
        {
            throw new InvalidOperationException("Skin id is required.");
        }

        var normalized = skinId.Trim().ToLowerInvariant();
        if (normalized.Any(character => !(char.IsLetterOrDigit(character) || character is '.' or '-' or '_')))
        {
            throw new InvalidOperationException("Skin id may only contain letters, numbers, dots, dashes, and underscores.");
        }

        return normalized;
    }

    private static string NormalizeTemplate(string template)
    {
        var normalized = string.IsNullOrWhiteSpace(template) ? "blank" : template.Trim().ToLowerInvariant();
        if (normalized is not ("blank" or "classic" or "launcher" or "dashboard" or "rabbits"))
        {
            throw new InvalidOperationException("Template must be one of: blank, classic, launcher, dashboard, rabbits.");
        }

        return normalized;
    }

    private static string ToDisplayName(string skinId) =>
        string.Join(' ', skinId.Split('.', '-', '_')
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
