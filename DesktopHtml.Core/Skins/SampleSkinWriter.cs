namespace DesktopHtml.Core.Skins;

public sealed class SampleSkinWriter
{
    private readonly AppPaths _paths;

    public SampleSkinWriter(AppPaths paths)
    {
        _paths = paths;
    }

    public Task EnsureInstalledAsync(CancellationToken cancellationToken = default)
    {
        using var mutex = new Mutex(false, @"Local\DesktopHtmlSampleSkinInstall");
        mutex.WaitOne();
        try
        {
            var skinDirectory = Path.Combine(_paths.SkinsDirectory, SampleSkinConstants.Id);
            var manifestFile = Path.Combine(skinDirectory, "manifest.json");

            if (File.Exists(manifestFile)
                && File.Exists(Path.Combine(skinDirectory, "index.html"))
                && File.Exists(Path.Combine(skinDirectory, "style.css"))
                && File.Exists(Path.Combine(skinDirectory, "script.js")))
            {
                return Task.CompletedTask;
            }

            Directory.CreateDirectory(skinDirectory);
            cancellationToken.ThrowIfCancellationRequested();
            File.WriteAllText(manifestFile, ManifestJson);
            File.WriteAllText(Path.Combine(skinDirectory, "index.html"), IndexHtml);
            File.WriteAllText(Path.Combine(skinDirectory, "style.css"), StyleCss);
            File.WriteAllText(Path.Combine(skinDirectory, "script.js"), ScriptJs);
            return Task.CompletedTask;
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    private const string ManifestJson = """
{
  "schemaVersion": 1,
  "id": "desktop-html.sample.launcher",
  "name": "desktop.html Sample Launcher",
  "version": "0.1.0",
  "author": "desktop.html",
  "description": "A minimal trusted local skin that proves WebView2 and the desktop bridge.",
  "entry": "index.html",
  "entries": {
    "main": "index.html"
  },
  "minimumDesktopHtmlVersion": "0.1.0",
  "permissions": {
    "fullTrust": true,
    "network": true,
    "rawExecution": true
  }
}
""";

    private const string IndexHtml = """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>desktop.html sample launcher</title>
  <link rel="stylesheet" href="./style.css">
</head>
<body>
  <main class="surface">
    <section class="panel">
      <p class="eyebrow">desktop.html</p>
      <h1>Desktop bridge online</h1>
      <p id="runtime">Loading runtime...</p>
    </section>

    <button class="launcher" id="notepad" type="button">
      <span class="icon">N</span>
      <span>Notepad</span>
    </button>
  </main>

  <script src="./script.js"></script>
</body>
</html>
""";

    private const string StyleCss = """
html,
body {
  width: 100%;
  height: 100%;
  margin: 0;
}

body {
  background:
    radial-gradient(circle at 20% 20%, rgba(79, 189, 186, 0.18), transparent 28rem),
    linear-gradient(135deg, #101316 0%, #181d20 48%, #111416 100%);
  color: #f7f3e8;
  font-family: "Segoe UI", system-ui, sans-serif;
  overflow: hidden;
}

.surface {
  position: relative;
  width: 100%;
  height: 100%;
}

.panel {
  position: absolute;
  left: 56px;
  top: 48px;
  max-width: 520px;
}

.eyebrow {
  margin: 0 0 10px;
  color: #75d6c9;
  font-size: 13px;
  font-weight: 700;
  letter-spacing: 0;
  text-transform: uppercase;
}

h1 {
  margin: 0 0 12px;
  font-size: 34px;
  font-weight: 650;
  letter-spacing: 0;
}

p {
  margin: 0;
  color: #cfc8b8;
  font-size: 15px;
  line-height: 1.5;
}

.launcher {
  position: absolute;
  left: 64px;
  top: 190px;
  width: 106px;
  height: 116px;
  display: grid;
  grid-template-rows: 64px 1fr;
  place-items: center;
  border: 1px solid rgba(255, 255, 255, 0.14);
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.08);
  color: #f7f3e8;
  font: inherit;
  cursor: default;
}

.launcher:hover,
.launcher:focus-visible {
  border-color: rgba(117, 214, 201, 0.72);
  background: rgba(117, 214, 201, 0.13);
  outline: none;
}

.icon {
  width: 52px;
  height: 52px;
  display: grid;
  place-items: center;
  border-radius: 8px;
  background: #d9c46f;
  color: #171816;
  font-size: 28px;
  font-weight: 800;
}
""";

    private const string ScriptJs = """
const runtime = document.getElementById("runtime");
const notepad = document.getElementById("notepad");

async function boot() {
  if (!window.desktop) {
    runtime.textContent = "window.desktop is not available.";
    return;
  }

  const info = await window.desktop.getRuntimeInfo();
  runtime.textContent = `Running ${info.appVersion}; active skin: ${info.activeSkinId}`;
}

notepad.addEventListener("dblclick", async () => {
  await window.desktop.shellExecute({
    file: "notepad.exe",
    args: [],
    verb: "open",
    showWindow: "normal"
  });
});

boot().catch(error => {
  runtime.textContent = error?.message || "Runtime check failed.";
});
""";
}
