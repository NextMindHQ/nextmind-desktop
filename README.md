# NextMind Desktop

**A lightweight, local-first desktop organizer for Windows.**

NextMind Desktop puts collapsible *zones* directly on your Windows desktop — above the wallpaper and icons, below every
application — so you can group shortcuts, files and folders without opening another window.

> _Screenshot placeholder: `docs/images/hero.png`. The screenshots will be added **before the first GitHub Release**, captured in a demo environment with sample files (see [docs/SCREENSHOT-PLAN.md](docs/SCREENSHOT-PLAN.md))._

> **Status: V0.1.0.** Source build only. **Managed Desktop Moves are an opt-in, safety-gated feature and are OFF by default** — see
> [How Managed Items Work](#how-managed-items-work).

## Why NextMind Desktop

The Windows desktop quickly turns into a pile of icons. NextMind Desktop gives it structure: you create named zones right on
the desktop, drop things into them, collapse the ones you don't need, and keep going. It is a small, quiet tray application —
no account, no cloud, no background polling.

It is an independent project with its own design and code. It is in the same general category as other desktop-organization
tools, but it is not a clone of any of them.

## Features

* **Desktop Zones** — named panels that live on the desktop itself
* **Collapse / expand** — double-click the title bar (a collapsed zone is just its title)
* **Move and resize** — drag the title bar, resize from the edges and corner grip; clamped to the visible monitor area
* **Drag & drop** — drop files, folders and shortcuts from Explorer or the Desktop onto a zone
* **Managed Desktop Items** *(opt-in)* — loose items from your Desktop can be moved into the zone's managed storage, so the Desktop really empties
* **References** — items from anywhere else are stored as references; the original is never touched
* **Manual ordering and sorting** — drag to reorder, or sort by name, date added or type (per zone)
* **Persistence** — positions, sizes, collapsed state, items and sort order survive restarts
* **Native Shell icons** — the same icons Explorer shows
* **Start with Windows** — on by default, toggled from the tray menu
* **Tray icon and Control Center** — create, show/hide and manage zones
* **Local-first** — no cloud requirement, no account

## How Managed Items Work

There are two kinds of items in a zone, decided by where the dropped item comes from:

| You drop… | Result |
|---|---|
| **A loose item from your Desktop** → Zone | Depends on the safety gate (below): **moved** into managed storage if it is ON, **referenced** if it is OFF. |
| **Any other file or folder** (anything not directly on your Desktop, including the Public Desktop and Desktop subfolders) → Zone | **Reference only.** The zone remembers the path. The original stays exactly where it is. |
| **Return to Desktop** (item menu) | Safely moves a managed item back to the Desktop. Works regardless of the gate. |

### Managed Desktop Moves: opt-in, safety-gated

In V0.1 the move feature is controlled by the `ManagedDesktopMovesEnabled` setting in `config.json`. It is **`false` by default**.

* **OFF (default):** a Desktop item dropped on a zone is added as a **reference only**. Nothing is moved; the item stays on the Desktop.
* **ON:** a Desktop item can be **moved into managed storage**, so the Desktop icon really disappears. A safety assessment runs first:
  ordinary shortcuts and small files and folders move directly; riskier items (programs, large files or folders, project-like folders,
  folders that could not be fully scanned) ask first, defaulting to *Add as Reference*; unsafe items (symlinks, junctions, cloud placeholders,
  system/hidden items) are refused and can only be added as references.

The Control Center shows whether the gate is on or off. Managed storage is `%LOCALAPPDATA%\NextMind\Desktop\ManagedItems`; moves are a
single atomic rename on one volume. Full details: [docs/THREAT-AND-DATA-SAFETY.md](docs/THREAT-AND-DATA-SAFETY.md).

## Safety

NextMind Desktop is designed so that a bug or a crash does not cost you files:

* **Write-ahead journal and recovery** — every managed move is journaled before it happens and recovered on the next start if the app died midway.
* **Collision handling** — if a name is already taken (for example when returning an item), the returned item gets a new name such as `NAME (2).lnk`. Nothing is silently overwritten.
* **Removing a reference never deletes the source** — "Remove from Zone" only forgets the path.
* **Delete Zone returns managed items** — the dialog offers *Return items to Desktop and delete Zone* or *Cancel*; there is no option that deletes your files.
* **No delete APIs on user data** — a test fails the build if product code gains a file-delete call.
* **Drops use link semantics** — Explorer is never told to "move" the source, so it cannot delete anything on its own.
* **Fail closed** — if an operation cannot be proven safe, it is refused with a message rather than guessed.

## Requirements

* Windows 10 version 2004 (build 19041) or later, or Windows 11 — **x64**
* To run: the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
* To build: the .NET 8 SDK (`global.json` pins an 8.0.4xx SDK and allows roll-forward to newer 8.0 feature bands)

## Installation

There is no installer yet. For now, build from source (below). Pre-built release archives may follow.

## Build from Source

```powershell
git clone https://github.com/NextMindHQ/nextmind-desktop.git
cd nextmind-desktop
dotnet restore
dotnet build -c Release
```

Run it from a stable per-user folder (the "Start with Windows" entry stores the path of the running executable):

```powershell
powershell -ExecutionPolicy Bypass -File tools\start.ps1   # publishes to %LOCALAPPDATA%\NextMind\Desktop\app and starts it
powershell -ExecutionPolicy Bypass -File tools\stop.ps1    # stops it (or use tray -> Exit)
```

**Uninstall:** untick *Start with Windows* in the tray menu, choose *Exit*, then delete `%LOCALAPPDATA%\NextMind\Desktop`.

## Development

```powershell
dotnet test
```

```
src/NextMind.Desktop.Core    config (versioned, atomic save, recovery), guarded filesystem, journal, managed items, geometry — no Windows API
src/NextMind.Desktop.Shell   Win32 interop: desktop layer, tray, message window, single instance, monitors, known folders, icons
src/NextMind.Desktop.App     WPF app: zone windows, zone manager, Control Center
tests/NextMind.Desktop.Tests xUnit; filesystem tests run only inside guarded temp directories
docs/                        architecture, safety model, manual test scenarios
tools/                       start / stop / autostart helper scripts, icon generator
```

See [CONTRIBUTING.md](CONTRIBUTING.md) before changing anything that touches the filesystem.
Further reading: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/THREAT-AND-DATA-SAFETY.md](docs/THREAT-AND-DATA-SAFETY.md),
[docs/MANUAL-TEST.md](docs/MANUAL-TEST.md).

## Known Limitations

* Dragging a managed item *out* to the Desktop is not implemented yet — use **Return to Desktop**.
* Managed storage lives under `%LOCALAPPDATA%`; a tool that cleans AppData could remove it. Keep that in mind before enabling managed moves.
* Keeping a zone above the desktop icons relies on undocumented Windows behaviour. There is a visible fallback, but future Windows updates could change it.
* Scanning very large folders for reparse points is bounded; when a scan is truncated the app always asks.
* New zones cascade by a small offset, so a second zone may partly overlap the first until moved.
* There is no Settings window yet; managed moves are enabled by editing `config.json`.
* Windows only, x64 only.

## Roadmap

Ideas for V0.2 — no dates, no promises:

* A proper installer and update path
* UX polish
* Accessibility
* Additional Shell integration
* Further performance work

## Privacy

NextMind Desktop is local-first. The source contains no networking code (no HTTP, socket or update-check calls) and no telemetry
or analytics. Configuration and the log are stored in `%LOCALAPPDATA%\NextMind\Desktop\`. The log can contain the names of
files you drop onto a zone — review it before attaching it to a bug report.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## Security

See [SECURITY.md](SECURITY.md).

## License

NextMind Desktop is licensed under the [Apache License 2.0](LICENSE). Copyright 2026 NextMind.

It is original work. Other desktop-organization tools were studied only as general inspiration for the problem space; no code was copied from them.

Please also read the [Code of Conduct](CODE_OF_CONDUCT.md).
