# Desktop Evolution V2 design

## Intent

Evolve the existing local Windows desktop organizer into a freely configurable workspace without weakening its data-safety, local-only and event-driven guarantees.

## Goals

- Free placement with optional assistance: peer-edge snapping, optional grid, configurable gap, collision feedback, deterministic auto-arrange and one-step move undo.
- Fast local search for commands, zones, widgets and Start-menu applications.
- Persisted quick-note and clock/date widgets using the same geometry conventions as zones.
- Named local workspace profiles with create, rename, duplicate, switch and guarded delete.
- Backward-compatible migration from schema v3 with no loss of zones or managed-item state.
- Tests for every pure mechanism and explicit manual verification status for native UI/system behavior.

## Non-goals

- Disk-wide indexing, document-content search, cloud sync, accounts, telemetry or external APIs.
- A live CPU/RAM widget in this iteration: periodic sampling conflicts with the no-polling architecture.
- Rewriting the visual language, changing the NextMind identity or replacing the existing zone/item system.
- Publishing, pushing or installing globally.

## Constraints

- Windows 10 2004+ / Windows 11 x64; .NET 8; C# 12; WPF.
- Core remains free of Windows API; P/Invoke stays in Shell.
- No destructive user-data operations and no change to managed-item safety semantics.
- No cyclic idle timers. A clock may use a self-rearmed one-shot update aligned to the next minute.
- Existing config recovery and newer-schema read-only behavior remain intact.

## Approaches considered

1. **Extend current managers and config (chosen).** Add focused Core services and thin WPF coordinators. Lowest migration risk and preserves all current safety work.
2. Build a second canvas/dashboard next to zones. Rejected because it creates competing placement, persistence and widget systems.
3. Replace per-element WPF windows with a full-screen overlay. Rejected because it changes z-order behavior, hit testing and performance risk far beyond the requested evolution.

## Design

### Layout

`DesktopLayoutEngine` operates only on `RectPx`. It computes a proposed position from a freely dragged rectangle, optional grid size, configurable gap and peer rectangles. Snapping is a soft correction within a fixed threshold; overlaps remain legal and are returned as diagnostics. Auto-arrange packs rectangles deterministically inside current work areas without resizing them. The App records the pre-drag rectangle and applies one-step undo through a bounded `LayoutUndoState`.

### Profiles

Schema v4 adds `WorkspaceProfileConfig` and an active-profile id. The active runtime still exposes one list of zones to minimize churn; profile switching snapshots the current list and replaces it with a deep clone of the target. The migrated Default profile owns all v3 zones. Managed items retain stable ids and paths. The final remaining profile cannot be deleted.

### Search

Core provides normalized token matching and deterministic ranking. The App assembles a catalog from built-in commands, zones, widget definitions and `.lnk` files in the user/common Start Menu. Search never scans arbitrary user directories. A WPF search window updates as text changes, supports Up/Down, Enter and Escape, and is opened from a magnifier button and Ctrl+K in Control Center.

### Widgets

Schema v4 adds typed widget configs with bounds, collapsed state and a small JSON-safe content payload. V2 ships `Clock` and `QuickNote`. Widget windows reuse monitor clamping, desktop anchoring, move/resize/collapse and persistence patterns. Removing a widget removes only its configuration; quick-note text is not treated as a file and no user data is deleted.

### UI

The Control Center becomes the main discoverability surface: search, active profile, layout preferences, auto-arrange/undo, zone list and widget catalog. Existing colors and typography remain. Buttons receive consistent focus/hover behavior and destructive profile deletion requires confirmation.

### Failure behavior

Invalid profile/widget data is repaired by validation where safe; newer schemas remain read-only. Failed saves keep the previous config. If Start-menu enumeration fails, built-in results still work. If no monitor data is available, geometry remains unchanged as today.

## Verification

- Core xUnit tests cover snapping, collision, packing, undo, search ranking, profile lifecycle, schema migration, widget persistence and corruption recovery.
- Release build/test plus static safety tests.
- Manual checklist for drag visuals, keyboard search, profile switching, restart, DPI/display changes and widget interaction; unavailable cases are marked NOT TESTED.

