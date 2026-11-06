# Desktop Evolution V2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add assisted freeform layout, local search, workspace profiles and lightweight widgets to the existing NextMind Desktop architecture.

**Architecture:** Pure deterministic mechanisms live in Core and are tested without Windows. Existing WPF managers remain the orchestration layer and Shell remains the only Win32 boundary. Config v4 migrates existing users without discarding zones or managed-item state.

**Tech Stack:** C# 12, .NET 8, WPF, xUnit, System.Text.Json; no new dependencies.

**Spec:** `docs/superpowers/specs/2026-10-31-desktop-evolution-v2-design.md`

## Global Constraints

- Keep the NextMind name and existing visual identity.
- No network, telemetry, external APIs, global installs, publication or push.
- Core has no Windows API; no cyclic idle polling.
- Never delete user data; preserve managed-item journal and guarded-filesystem semantics.
- Every product change follows RED → GREEN and Release verification.

## Review Focus

- Negative virtual-screen coordinates must remain valid through snapping and packing.
- Mixed-DPI transitions must not reinterpret stored physical pixels as DIPs.
- Switching profiles with managed items must preserve stable item ids/paths.
- Empty/unreadable Start-menu locations must leave built-in search usable.
- Removing the active/last profile or a note widget must not silently lose unrelated configuration.

---

### Task 1: Assisted layout engine and undo

**Files:**
- Create: `src/NextMind.Desktop.Core/Layout/DesktopLayoutEngine.cs`
- Create: `src/NextMind.Desktop.Core/Layout/LayoutUndoState.cs`
- Create: `tests/NextMind.Desktop.Tests/DesktopLayoutEngineTests.cs`
- Modify: `src/NextMind.Desktop.Core/Config/AppConfig.cs`
- Modify: `src/NextMind.Desktop.App/ZoneManager.cs`
- Modify: `src/NextMind.Desktop.App/ZoneWindow.xaml.cs`

**Interfaces:**
- Produces: `LayoutOptions`, `LayoutResult`, `DesktopLayoutEngine.Place(...)`, `DesktopLayoutEngine.AutoArrange(...)`, `LayoutUndoState`.

- [ ] Write tests for peer-edge snap, optional grid, gap, legal overlap diagnostics, deterministic multi-monitor packing and one-step undo.
- [ ] Run `dotnet test --filter DesktopLayoutEngineTests` and confirm RED because the types do not exist.
- [ ] Implement the pure Core engine and configuration defaults.
- [ ] Run the focused tests and confirm PASS.
- [ ] Integrate post-drag placement, collision visual state, auto-arrange and undo into the existing zone flow.
- [ ] Run all Release tests and commit.

### Task 2: Workspace profiles and schema v4 migration

**Files:**
- Create: `src/NextMind.Desktop.Core/Config/WorkspaceProfiles.cs`
- Create: `tests/NextMind.Desktop.Tests/WorkspaceProfileTests.cs`
- Modify: `src/NextMind.Desktop.Core/Config/AppConfig.cs`
- Modify: `src/NextMind.Desktop.Core/Config/ConfigMigrator.cs`
- Modify: `src/NextMind.Desktop.Core/Config/ConfigValidator.cs`
- Modify: `src/NextMind.Desktop.App/ZoneManager.cs`
- Modify: `src/NextMind.Desktop.App/ControlCenterWindow.xaml*`

**Interfaces:**
- Consumes: current zone snapshots and layout options from Task 1.
- Produces: `WorkspaceProfileConfig`, `WorkspaceProfiles.Create/Rename/Duplicate/Switch/Delete`.

- [ ] Write lifecycle, deep-clone, last-profile guard and v3 migration tests.
- [ ] Run focused tests and confirm RED.
- [ ] Implement schema/model/migration/validation and pure lifecycle service.
- [ ] Run focused tests and confirm PASS.
- [ ] Add Control Center profile actions with confirmation and safe window reload.
- [ ] Run all Release tests and commit.

### Task 3: Local global search

**Files:**
- Create: `src/NextMind.Desktop.Core/Search/SearchIndex.cs`
- Create: `tests/NextMind.Desktop.Tests/SearchIndexTests.cs`
- Create: `src/NextMind.Desktop.App/SearchWindow.xaml*`
- Modify: `src/NextMind.Desktop.App/App.xaml.cs`
- Modify: `src/NextMind.Desktop.App/ControlCenterWindow.xaml*`

**Interfaces:**
- Consumes: active zones and widget definitions.
- Produces: `SearchEntry`, `SearchIndex.Query(string, int)`, keyboard-driven SearchWindow.

- [ ] Write tests for empty query, token normalization, ranking, result limits and no-result behavior.
- [ ] Run focused tests and confirm RED.
- [ ] Implement deterministic local matching.
- [ ] Run focused tests and confirm PASS.
- [ ] Add built-in/zone/widget/Start-menu catalog assembly and magnifier/Ctrl+K UI.
- [ ] Run all Release tests and commit.

### Task 4: Clock and quick-note widgets

**Files:**
- Create: `src/NextMind.Desktop.Core/Widgets/WidgetCatalog.cs`
- Create: `tests/NextMind.Desktop.Tests/WidgetCatalogTests.cs`
- Create: `src/NextMind.Desktop.App/WidgetWindow.xaml*`
- Create: `src/NextMind.Desktop.App/WidgetManager.cs`
- Modify: `src/NextMind.Desktop.Core/Config/AppConfig.cs`
- Modify: `src/NextMind.Desktop.App/App.xaml.cs`
- Modify: `src/NextMind.Desktop.App/ControlCenterWindow.xaml*`

**Interfaces:**
- Consumes: physical-pixel geometry, desktop anchoring and active profile lifecycle.
- Produces: `WidgetConfig`, `WidgetDefinition`, add/remove/minimize/move/resize/persist flows.

- [ ] Write tests for catalog definitions, unique ids, note persistence and profile clone behavior.
- [ ] Run focused tests and confirm RED.
- [ ] Implement Core widget model/catalog and validation.
- [ ] Run focused tests and confirm PASS.
- [ ] Implement WPF widget windows/managers, minute-aligned one-shot clock and note editing.
- [ ] Run all Release tests and commit.

### Task 5: Polish, performance, documentation and artifact

**Files:**
- Modify: `docs/DESKTOP-EVOLUTION-PROGRESS.md`
- Create: `docs/DESKTOP-EVOLUTION-RESULTS.md`
- Modify: `docs/MANUAL-TEST.md`
- Modify: `docs/PERF.md`
- Modify: `README.md`

**Interfaces:**
- Consumes: all previous tasks.

- [ ] Run formatting/diff checks, Release tests and Release build.
- [ ] Run reproducible startup/idle measurements when the Windows UI environment permits; otherwise mark NOT TESTED.
- [ ] Exercise the manual checklist; never convert an unperformed scenario into PASS.
- [ ] Publish a local, framework-dependent x64 test artifact through the existing project process.
- [ ] Record DONE/PARTIAL/BLOCKED/NOT TESTED status, known limits and artifact path.

