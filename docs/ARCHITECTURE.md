# Architecture

NextMind Desktop is a small, event-driven Windows tray application written in C# 12 / .NET 8 with WPF and a thin Win32 layer.
This document describes the structure and the main design decisions. The data-safety model is in
[THREAT-AND-DATA-SAFETY.md](THREAT-AND-DATA-SAFETY.md).

## Projects

```
NextMind.Desktop.sln
src/
  NextMind.Desktop.Core    config, filesystem abstraction and guards, journal, managed items, sorting, geometry — no Windows API
  NextMind.Desktop.Shell   Win32 / COM interop: desktop layer, tray, message window, single instance, monitors, known folders, icons, startup registry
  NextMind.Desktop.App     WPF: zone windows, zone manager, Control Center, dialogs, app startup
tests/
  NextMind.Desktop.Tests   xUnit; filesystem tests run only inside guarded temp directories
```

Dependencies: `App → Core, Shell`; `Shell → Core`; `Core → nothing`. **Core has no Windows API, Shell is the only place with P/Invoke,
and App holds no policy** (decisions about what may be moved live in Core and are unit-tested).

`Directory.Build.props` sets nullable reference types, warnings-as-errors for product projects, and the single product version.

## Design decisions

### Stack: .NET 8, WPF, thin P/Invoke layer
WPF gives full control of the window handle, OLE drag & drop, and fast iteration. The cost is a larger working set than raw Win32;
measured idle cost on a development machine was about 40–50 MB private bytes and 0 % CPU. Because Core and Shell are independent of the UI, the
renderer could be replaced without touching the safety-critical logic.

### Zone windows
Each zone is a small top-level borderless window (`WS_EX_TOOLWINDOW`, no taskbar button, not activated by clicks). Zones sit directly above the
desktop icon host and below normal windows, so "Show desktop" keeps them visible. This relies on the Progman / WorkerW window structure, which is
**undocumented Windows behaviour**; it is isolated in `DesktopLayer`, re-validated after Explorer restarts (`TaskbarCreated`), and has a fallback
(bottom of the z-order). There is no full-screen overlay and no code injection into Explorer.

### Event-driven, no polling
No cyclic timers and no polling. System events (display change, DPI change, Explorer restart) arrive through window messages. Persistence uses a single
re-armed one-shot timer (about 400 ms after a change) plus a flush on exit.

### Configuration
* `%LOCALAPPDATA%\NextMind\Desktop\config.json` (+ `config.json.bak`), versioned schema (`SchemaVersion`), System.Text.Json.
* Atomic save: write a temp file, flush, replace. On read: validate; if the file is unusable, fall back to the previous generation; a damaged file is kept
  aside rather than overwritten blindly.
* Migrations are an explicit chain (`ConfigMigrator`). A config written by a newer version is not overwritten.
* Items have stable ids (not paths). Zone geometry is stored in physical pixels with the DPI it was saved at, and clamped to the visible monitors on restore.
* Full Unicode, including emoji in zone names.

### Filesystem abstraction
All file operations go through `IFileSystem`. Implementations: `RealFileSystem` (production), `GuardedFileSystem` / `ScopedFileSystem` (refuse any path outside
an allowed root, and refuse protected user folders), and temp-directory fixtures in tests. `RealFileSystem` implements the move as `MoveFileExW` with **no flags**,
i.e. a plain same-volume rename that can neither copy+delete across drives nor overwrite.

### Managed items and the journal
Moving a Desktop item into managed storage is a write-ahead-journaled transaction with startup recovery. See
[THREAT-AND-DATA-SAFETY.md](THREAT-AND-DATA-SAFETY.md).

### Icons
Native Shell icons (`IShellItemImageFactory`, `SHGetFileInfo` fallback), loaded off the UI thread, cached, and released immediately after conversion to frozen
bitmaps. A missing item shows a placeholder, never a crash and never an automatic removal.

### Drag & drop
Drops are accepted through OLE and always answered with the **`Link`** effect (never `Move`), so Explorer cannot delete the source by itself. Any physical
move is performed by the app, through the journal. Reordering inside a zone uses a private in-process data format that cannot be mistaken for a file drop.

### Process lifecycle
* **Single instance:** a named mutex; a second launch wakes the first (it shows its zones) and exits.
* **Tray:** a Win32 notification icon on a hidden message window (also used for `TaskbarCreated` and display messages) — no WinForms dependency.
* **Start with Windows:** the per-user `HKCU\…\Run` value `NextMindDesktop`. `AppConfig.AutostartEnabled` is the source of truth (default ON); at each start the
  registry value is made to match it (created, repaired to the running executable, or removed). Only the tray checkbox changes the preference.
* **Explorer restart:** zones are re-anchored and the tray icon is re-added.
* **Displays and DPI:** Per-Monitor V2; zones are re-clamped on display or DPI change.

### Privacy and permissions
No network access, no telemetry, no shell extensions, no services, no administrator rights. State lives in `%LOCALAPPDATA%\NextMind\Desktop\` and in the
per-user Run key.

## Testing

xUnit tests cover config (serialization, migration, atomic save, corruption), geometry, zone catalog and items, sorting, the move assessor, managed move / return /
recovery (including injected faults), startup logic, and static guards:

* `NoDestructiveOperationsTests` — product code may not contain delete calls on user data.
* `SafetyGuardTests` / `TempSandbox` — tests can only use a guarded temp directory; real Desktop, Documents, Pictures and OneDrive paths are refused.

## Known constraints

* Windows 10 2004+ / Windows 11, x64 only.
* Zone z-order depends on undocumented shell behaviour (with a fallback).
* Cross-volume moves, tracking files by file id, virtual Shell items and virtual desktops are out of scope for V0.1.
