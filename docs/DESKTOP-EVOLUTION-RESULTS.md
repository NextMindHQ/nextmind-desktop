# NextMind Desktop Evolution V2 — results

Date: 2026-10-31  
Branch: `forge/desktop-evolution-v2`  
Baseline: `ada93f6`  
Audit: `docs/DESKTOP-EVOLUTION-AUDIT.md`

## What was found

The existing V0.1 product was already a sound .NET 8/WPF desktop-zone application with physical-pixel multi-monitor geometry, atomic versioned config, backup/corruption recovery, safe local file references, guarded managed Desktop items, native icons, tray controls and 274 automated tests. Search, profiles, widgets, snapping, collision feedback, auto-arrange and movement undo were absent.

## DONE

### Assisted freeform layout

- Zone-to-zone edge snapping with configurable gap and threshold.
- Optional grid snapping; free placement remains the default behavior.
- Collision detection with non-blocking amber border feedback.
- Deterministic auto-arrange across monitor work areas, including negative coordinates.
- One-step undo for the latest title-bar move.
- Layout preferences persist through the existing atomic config store.

### Local global search

- Magnifier in Control Center and `Ctrl+K` shortcut.
- Live local results for NextMind commands, active zones, widget actions and `.lnk` entries from user/common Start Menu.
- Up/Down, Enter and Escape behavior plus explicit no-results state.
- Case/diacritic-insensitive token matching with deterministic ranking.
- No full-disk scan, document access, network or background indexer.

### Workspace profiles

- Create, rename, switch, duplicate and confirm-delete.
- Schema v3→v4 migration creates a Default profile without losing active zones/items.
- Per-profile zones, layout settings and widgets use deep copies.
- The last profile cannot be deleted. Profiles containing managed items cannot be deleted.
- Duplicates intentionally exclude managed items, preventing two configuration owners for one physical object.

### Widgets

- Clock & Date using one minute-aligned, self-rearmed one-shot timer.
- Quick Note stored locally in config with Unicode, a dedicated multiline editor and a 10,000-character bound.
- Move, resize, collapse/expand and persistence.
- × hides a widget without deleting its data; Control Center/search restore hidden widgets.
- Widget layouts and notes are profile-specific.

### Safety and compatibility

- No new NuGet dependencies, network access, telemetry, external APIs, services or global installs.
- Existing managed-item journal and filesystem safety policy are unchanged.
- Core remains free of Windows APIs; Shell remains the Win32 boundary.
- Existing config recovery and newer-schema read-only behavior remain.

## Verification

| Check | Result |
|---|---|
| Baseline Release tests | PASS — 274/274 |
| Final Release tests | PASS — 297/297, 0 failed, 0 skipped |
| Release build | PASS — 0 warnings, 0 errors |
| Diff whitespace check | PASS |
| Config migration/corruption tests | PASS |
| Static destructive-operation guards | PASS as part of full suite |
| Manual V2 UI flow | NOT TESTED — installed instance held global mutex; user process was not stopped |
| Mixed-DPI / monitor disconnect | NOT TESTED |
| V2 runtime CPU/RAM/cold start | NOT TESTED; historical V0.1 baseline remains in `docs/PERF.md` |

## PARTIAL

- UX/accessibility polish: keyboard search is implemented and Control Center is expanded, but a real UI accessibility/focus pass was blocked by the existing instance.
- Performance comparison: architecture remains event-driven, but no V2 runtime numbers are claimed.
- CPU/RAM status widget: deliberately omitted because continuous sampling conflicts with the repository's hard no-polling rule.

## Known limitations

- Auto-arrange currently applies to visible zones; widget placement remains manual.
- Undo covers one zone move, not resize, profile switch or auto-arrange.
- Search enumerates Start-menu shortcuts on demand and does not resolve arbitrary AppX registrations.
- Profile duplication omits managed items by design; references and widgets are duplicated.
- The Explorer desktop-layer behavior remains undocumented Windows behavior with the existing visible fallback.

## Local test artifact

- Directory: `artifacts/NextMindDesktop-v2-test/`
- ZIP: `artifacts/NextMindDesktop-v2-test.zip`
- Runtime: framework-dependent .NET 8, `win-x64`
- Entry point: `NextMindDesktop.exe`
- ZIP SHA-256: `879EE80E31430359F1EA5442FCC3FEC97B111F601574DAA1F676C21CDA1CDE6F`

The artifact was prepared locally and was not published or pushed.

## How to use the new features

1. Open Control Center by double-clicking the tray icon.
2. Configure snapping/grid/gap under **Layout**; use **Auto-arrange** and **Undo Move** there.
3. Click **Search** or press `Ctrl+K` while Control Center is active.
4. Create/switch profiles under **Workspace Profiles**.
5. Add Clock & Date or Quick Note under **Widgets**. The widget × hides it; use **Show** in Control Center or search to restore it.

## Owner decisions still needed

1. Whether a future CPU/RAM widget justifies relaxing the no-polling rule, and at what sampling interval/power budget.
2. Whether undo should expand to a multi-operation history spanning resize/auto-arrange.
3. Whether managed items should be shared across profiles through a redesigned global ownership registry; current duplication safely excludes them.
4. Whether to make `Ctrl+K` a system-wide hotkey. The current shortcut is scoped to Control Center and cannot collide with other applications.
