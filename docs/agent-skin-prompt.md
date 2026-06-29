# Agent Prompt for Creating a desktop.html skin

Use this when asking another coding agent to create a desktop.html skin.

```text
Create a desktop.html skin in this repository.

Read these docs first:
- docs/skin-authoring.md
- docs/bridge-api.md
- docs/security.md

Requirements:
- Create a new folder under samples/<skin-name>.
- Include manifest.json, index.html, style.css, and script.js.
- Use plain HTML/CSS/JavaScript, no framework unless explicitly requested.
- The manifest id must be unique and stable.
- Do not hardcode private absolute paths unless I explicitly request a personal local skin.
- Use window.desktop only after clear user actions for launching or raw execution.
- Make the UI visually polished, responsive, and usable as a desktop surface.
- Validate with:
  .\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin validate .\samples\<skin-name> --json
- Install with:
  .\DesktopHtml.App\bin\Debug\net8.0-windows\desktop-html.exe skin install .\samples\<skin-name> --force --json
- Do not claim manual visual acceptance unless it was actually checked.
```
