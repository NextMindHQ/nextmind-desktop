# NextMind Desktop

**A lightweight, local-first desktop organizer for Windows.**

[![Latest release](https://img.shields.io/github/v/release/NextMindHQ/nextmind-desktop?label=release)](https://github.com/NextMindHQ/nextmind-desktop/releases/latest)
[![Platform: Windows x64](https://img.shields.io/badge/platform-Windows%20x64-0078D6)](#requirements)
[![License: Apache-2.0](https://img.shields.io/github/license/NextMindHQ/nextmind-desktop)](LICENSE)
[![CI](https://github.com/NextMindHQ/nextmind-desktop/actions/workflows/ci.yml/badge.svg)](https://github.com/NextMindHQ/nextmind-desktop/actions/workflows/ci.yml)

NextMind Desktop puts collapsible **Zones** directly on your Windows desktop — above the wallpaper and icons, below every
application — so you can group shortcuts, files and folders without opening another window. It is a small tray application:
no account, no cloud, no telemetry.

**[⬇ Download the latest release](https://github.com/NextMindHQ/nextmind-desktop/releases/latest)**

> _Screenshots are coming soon (`docs/images/`). See [docs/SCREENSHOT-PLAN.md](docs/SCREENSHOT-PLAN.md) for what they will show._

## Download / Quick Start

1. Download **`NextMind-Desktop-v0.1.0-win-x64.zip`** from the [latest release](https://github.com/NextMindHQ/nextmind-desktop/releases/latest).
2. Extract it to a folder you want to keep (the app registers that location for *Start with Windows*).
3. Run **`NextMindDesktop.exe`**.

A first Zone appears on your desktop and a tray icon is added. Drop files, folders or shortcuts onto the Zone.

* **Self-contained:** v0.1.0 includes everything it needs. **You do not need to install .NET.**
* **Not code-signed yet:** Windows SmartScreen may warn about an unrecognized app. If you trust the download, choose *More info* → *Run anyway*.
  You can check the file first: the release includes a `.sha256` file, or run
  `Get-FileHash .\NextMind-Desktop-v0.1.0-win-x64.zip -Algorithm SHA256` in PowerShell and compare it with the value on the release page.
* **Start with Windows is on by default** on the first run. Untick it in the tray menu if you don't want it.
* **Uninstall:** untick *Start with Windows* in the tray menu, choose *Exit*, then delete the extracted folder and `%LOCALAPPDATA%\NextMind\Desktop`.

## Features

* **Desktop Zones** — named panels that live on the desktop itself
* **Collapse / expand** — double-click the title bar (a collapsed zone is just its title)
* **Move and resize** — drag the title bar, resize from the edges and corner grip; zones stay within the visible monitor area
* **Drag & drop** — drop files, folders and shortcuts from Explorer or the Desktop onto a Zone
* **Sorting and manual ordering** — drag to reorder, or sort per zone by name, date added or type
* **Native Windows icons** — the same icons Explorer shows
* **Start with Windows** — toggled from the tray menu
* **Tray icon and Control Center** — create, show/hide and manage zones
* **Persistence** — positions, sizes, collapsed state, items and sort order survive restarts
* **Local-first** — no account, no cloud, no telemetry; the source contains no networking code
* **Managed Desktop Moves** — *opt-in, off by default*; see [below](#managed-desktop-moves--safety)

## How it works

Every item in a Zone is one of two kinds, decided by where it comes from:

| You drop… | Result |
|---|---|
| **Anything that is not directly on your Desktop** (other folders, drives, the Public Desktop, Desktop subfolders) | **Reference.** The Zone remembers the path; the original is never touched. "Remove from Zone" only forgets the path. |
| **A loose item from your Desktop** | Depends on the Managed Desktop Moves setting: **reference** while it is OFF (default), **moved** into managed storage when it is ON. |
| **Return to Desktop** (item menu) | Safely moves a managed item back to the Desktop. |

Your settings and the log live in `%LOCALAPPDATA%\NextMind\Desktop\` (`config.json`, `logs\desktop.log`). The log can contain the names of
files you drop onto a Zone — review it before attaching it to a bug report.

## Managed Desktop Moves / Safety

**Managed Desktop Moves are an opt-in, safety-gated feature in v0.1.0, and they are OFF by default.**
The setting is `managedDesktopMovesEnabled` in `%LOCALAPPDATA%\NextMind\Desktop\config.json` (`false` by default). The Control Center shows its
current state. There is no Settings window yet, so enabling it means editing the file while the app is closed.

* **OFF (default):** a Desktop item dropped on a Zone is added as a **reference only**. Nothing is moved; the item stays on the Desktop.
* **ON:** a Desktop item can be **moved into managed storage** (`%LOCALAPPDATA%\NextMind\Desktop\ManagedItems`), so the Desktop icon really disappears.
  A safety assessment runs first. Ordinary shortcuts and small files and folders move directly; riskier items (programs, large files or folders,
  project-like folders, folders that could not be fully scanned) ask first, defaulting to *Add as Reference*; unsafe items (symlinks, junctions, cloud
  placeholders, system or hidden items) are refused and can only be added as references.

Safeguards, whether or not the feature is on:

* **Write-ahead journal and recovery** — every managed move is journaled first and recovered on the next start if the app stopped midway.
* **Atomic, same-volume moves only** — a single rename; never copy-and-delete, never overwrite.
* **Collision handling** — a taken name gets a new one such as `NAME (2).lnk`; nothing is silently overwritten.
* **Delete Zone returns managed items** — the dialog offers *Return items to Desktop and delete Zone* or *Cancel*; no option deletes your files.
* **No delete APIs on user data** — a test fails the build if product code gains a file-delete call.
* **Drops use link semantics** — Explorer is never told to "move" the source, so it cannot delete anything by itself.
* **Fail closed** — if an operation cannot be shown to be safe, it is refused with a message.

Full details: [docs/THREAT-AND-DATA-SAFETY.md](docs/THREAT-AND-DATA-SAFETY.md).

**Known limitations (v0.1.0)**

* Dragging a managed item *out* to the Desktop is not implemented yet — use **Return to Desktop**.
* Managed storage lives under `%LOCALAPPDATA%`; a tool that cleans AppData could remove it. Keep this in mind before enabling managed moves.
* Keeping a Zone above the desktop icons relies on undocumented Windows behaviour. There is a visible fallback, but a future Windows update could change it.
* Scanning very large folders for reparse points is bounded; when a scan is truncated the app always asks.
* New Zones cascade by a small offset, so a second Zone may partly overlap the first until moved.
* No Settings window yet. Windows x64 only. The executable is not code-signed.

## Requirements

**Using NextMind Desktop**

* Windows 10 version 2004 (build 19041) or later, or Windows 11 — **x64**
* Nothing else: the v0.1.0 release ZIP is self-contained (no .NET installation needed)

**Building from source**

* The .NET 8 SDK (`global.json` pins an 8.0.4xx SDK and allows roll-forward to newer 8.0 feature bands)
* Git. Visual Studio 2022, Rider or VS Code are optional.

## Build from Source / Development

```powershell
git clone https://github.com/NextMindHQ/nextmind-desktop.git
cd nextmind-desktop
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

To run your build from a stable per-user folder (the "Start with Windows" entry stores the path of the running executable):

```powershell
powershell -ExecutionPolicy Bypass -File tools\start.ps1   # publishes to %LOCALAPPDATA%\NextMind\Desktop\app and starts it
powershell -ExecutionPolicy Bypass -File tools\stop.ps1    # stops it (or use tray -> Exit)
```

Project layout:

```
src/NextMind.Desktop.Core    config (versioned, atomic save, recovery), guarded filesystem, journal, managed items, geometry — no Windows API
src/NextMind.Desktop.Shell   Win32 interop: desktop layer, tray, message window, single instance, monitors, known folders, icons
src/NextMind.Desktop.App     WPF app: zone windows, zone manager, Control Center
tests/NextMind.Desktop.Tests xUnit; filesystem tests run only inside guarded temp directories
docs/                        architecture, safety model, manual test scenarios
tools/                       start / stop / autostart helper scripts, icon generator
```

Further reading: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/THREAT-AND-DATA-SAFETY.md](docs/THREAT-AND-DATA-SAFETY.md),
[docs/MANUAL-TEST.md](docs/MANUAL-TEST.md).

**Roadmap** — ideas for V0.2, no dates and no promises: an installer and update path, UX polish, accessibility, additional Shell integration, further performance work.

## Contributing

Contributions are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) first — especially the filesystem safety rules (never test destructive
operations against a real Desktop) — and the [Code of Conduct](CODE_OF_CONDUCT.md).

## Security

Please report vulnerabilities privately through GitHub (**Security** tab → **Report a vulnerability**). See [SECURITY.md](SECURITY.md).

## License

NextMind Desktop is licensed under the [Apache License 2.0](LICENSE). Copyright 2026 NextMind.

It is original work. Other desktop-organization tools were studied only as general inspiration for the problem space; no code was copied from them.
