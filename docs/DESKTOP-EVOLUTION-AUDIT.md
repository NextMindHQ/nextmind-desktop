# NextMind Desktop Evolution V2 — audit

Date: 2026-10-31  
Baseline: `ada93f6` (`main`), 274/274 automated tests passing in Release.

## Executive summary

NextMind Desktop is a local-only Windows 10/11 x64 application built with C# 12, .NET 8, WPF and a thin Win32 shell layer. The repository has unusually strong data-safety foundations: Core is Windows-independent, user-data moves are journaled and opt-in, configuration writes are atomic with backup/corruption recovery, and destructive APIs are guarded by tests.

The current product already has movable/resizable/collapsible desktop-level zones, per-monitor physical-pixel geometry, DPI-aware sizing, event-driven persistence, local file references, guarded managed items, sorting/reordering, tray controls, and a small Control Center. Evolution V2 should extend these mechanisms rather than introduce a competing desktop model.

## Architecture and current behavior

- `NextMind.Desktop.Core`: versioned config, validation/migrations, atomic persistence, geometry, managed-item journal and filesystem guards. No Windows API.
- `NextMind.Desktop.Shell`: known folders, monitors/DPI, Explorer desktop anchoring, tray, icons, startup and single-instance support.
- `NextMind.Desktop.App`: WPF zone windows, `ZoneManager`, Control Center and prompts.
- One top-level WPF window is used per zone. Geometry is stored in physical virtual-screen pixels and clamped to the current monitor work areas.
- State changes re-arm a one-shot 400 ms save timer; there is no idle polling. Exit/session shutdown flushes state.
- `WM_DISPLAYCHANGE` reclamps zones; `OnDpiChanged` reloads icon sizes and keeps geometry in physical pixels.

## Capability matrix

| Area | Status | Evidence / gap |
|---|---|---|
| Drag zones | ISTNIEJE | Native `DragMove()` from title bar; position persisted. |
| Resize zones | ISTNIEJE | Edge/corner `Thumb` grips; min sizes; physical-pixel snapshot. |
| Restore after restart | ISTNIEJE | Atomic `ConfigStore`, backup/temp recovery, schema migrations. |
| Multi-monitor / DPI | CZĘŚCIOWO | Physical-pixel model, work-area clamp, DPI reload and display-change handling exist; mixed-DPI manual scenario remains unverified. |
| Snap to elements | BRAK | No peer-edge snapping. |
| Optional grid / spacing | BRAK | No layout preferences. |
| Collision warning | BRAK | Overlap is allowed but not detected or signaled. |
| Auto-arrange | BRAK | New zones only cascade by 36 px. |
| Undo move | BRAK | No movement history. |
| Global search | BRAK | Search was explicitly outside V0.1 scope. |
| Keyboard search flow | BRAK | No search dialog, Enter/Escape flow or Ctrl+K. |
| Zone items | ISTNIEJE | Reference/managed items, icons, sorting, manual reorder, move between zones. |
| Widget system | BRAK | Zones are not typed widgets; no widget catalog or note/clock model. |
| Workspace profiles | BRAK | One global list of zones. |
| Visual consistency | CZĘŚCIOWO | Coherent dark zone/Control Center styling; limited focus states and no V2 settings surface. |
| Keyboard accessibility | WYMAGA NAPRAWY | Prompts have keyboard behavior, but zone and Control Center actions are mainly pointer-driven. |
| Corrupt config recovery | ISTNIEJE | Main/temp/backup recovery, quarantine, read-only protection for newer schemas. |
| Save failure behavior | ISTNIEJE | Previous config remains untouched; failures logged; app stays running. |
| Idle CPU / memory measurements | ISTNIEJE (stare) | 2026-10-03: 0% idle CPU, ~42–46 MB private bytes, 0.53–0.68 s cold start; must be remeasured for V2. |
| Automated tests | ISTNIEJE | 274 xUnit tests at baseline; strong Core and safety coverage. WPF interaction tests are absent. |
| Manual Windows verification | CZĘŚCIOWO | Real-system V0.1 flow documented; mixed DPI/virtual desktops and V2 flows are not verified. |

## Constraints and technical debt

1. The desktop z-order integration depends on documented Win32 calls applied to an undocumented Explorer window hierarchy; a visible fallback exists.
2. `ZoneManager` and `ZoneWindow.xaml.cs` are already large; V2 logic should live in focused Core services and small App coordinators.
3. Automated tests intentionally avoid driving the real Desktop. WPF/manual interactions need an explicit NOT TESTED state when the host cannot run them reliably.
4. A live CPU/RAM widget would require periodic sampling and conflict with the repository's no-polling rule. It is excluded from this iteration unless the architecture changes explicitly.
5. Existing config schema is v3. V2 data must migrate forward without losing the current zones, items or safety preferences.

## Recommended V2 slice

- Add a pure Core layout engine for peer-edge/grid snapping, overlap detection, deterministic packing and one-step movement undo.
- Add versioned workspace profiles while preserving the current active zone list and migration behavior.
- Add a local search catalog for Desktop commands, zones, installed Start-menu shortcuts and available widgets; no disk-wide indexing.
- Add a small widget catalog with clock/date and quick note first. Reuse the same geometry/persistence conventions as zones.
- Expand the Control Center into the discoverable surface for search, profiles, layout settings, widgets and auto-arrange.

## Baseline evidence

Command: `dotnet test -c Release --nologo --verbosity minimal`  
Result: PASS — 274 passed, 0 failed, 0 skipped.

