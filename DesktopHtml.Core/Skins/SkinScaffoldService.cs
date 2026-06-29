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

        return new Dictionary<string, string>
        {
            ["manifest.json"] = manifest + Environment.NewLine,
            ["index.html"] = CreateHtml(name, template),
            ["style.css"] = CreateCss(template),
            ["script.js"] = CreateScript(template)
        };
    }

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
        if (normalized is not ("blank" or "classic" or "launcher" or "dashboard"))
        {
            throw new InvalidOperationException("Template must be one of: blank, classic, launcher, dashboard.");
        }

        return normalized;
    }

    private static string ToDisplayName(string skinId) =>
        string.Join(' ', skinId.Split('.', '-', '_')
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
