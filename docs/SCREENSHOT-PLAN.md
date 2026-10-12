# Screenshot and demo plan

Rule: **never publish a screenshot of a real desktop.** Only synthetic demo content, and only the zone windows themselves.

## Demo environment
* Use the isolated test instance with the demo layout: `powershell -ExecutionPolicy Bypass -File tools\test-ui.ps1 -Demo`
  (see [DEVELOPMENT.md](DEVELOPMENT.md)). It uses its own config and log, neutral zone names, empty sample files and folders, and shortcuts to Windows' own tools only.
* Capture the **zone windows only** (for example with `PrintWindow`), never the desktop, and compose them on a neutral, generated backdrop.
  No wallpaper, no desktop icons, no taskbar, no other application windows, no notifications.
* Zones on an ordinary desktop sit under application windows, so capturing windows directly (instead of the screen) also keeps everything else out of the image.
* Before publishing, check every image for user names, paths, window titles and personal names, and make sure the PNG has no text or metadata chunks.

## Shots
| File | Content | Status |
|---|---|---|
| `docs/images/hero.png` | Several expanded and collapsed zones on a neutral backdrop | included |
| `docs/images/collapsed.png` | Collapsed zones (title bar, magnifier, close) | included |
| `docs/images/expanded.png` | One expanded zone with items and native icons | included |
| `docs/images/search.png` | A zone with the search bar open and a filtered list | included |
| `docs/images/sort-menu.png` | The title-bar menu with the *Sort by* submenu open | planned (needs a real right-click; popups are captured by hand, never automatically) |
| `docs/images/control-center.png` | Control Center | planned |
| `docs/images/drag-to-zone.png` (or short GIF) | Dragging an item into a zone | planned |
