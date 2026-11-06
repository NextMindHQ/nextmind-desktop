# NextMind Desktop V3 Future Workspace Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a functional, persisted, lightweight Future Workspace UI on top of the existing NextMind Desktop V2 architecture.

**Architecture:** Add schema-v5 global appearance preferences, apply them through a small WPF theme adapter, and restyle all existing windows with one shared resource dictionary. Preserve managers, window behavior, safety gates, and layout mechanics. Finish with an isolated current-source launcher and evidence-based documentation.

**Tech Stack:** .NET 8, WPF/XAML, xUnit, Windows CMD, PowerShell integration test

**Spec:** `docs/superpowers/specs/2026-10-31-desktop-v3-future-workspace-design.md`

## Global Constraints

- Keep WPF and the existing V2 architecture; add no external packages.
- Preserve every V2 feature and Managed Items safety guarantee.
- Do not modify the installed app, live config, autostart, or running process.
- No continuous decorative animation, polling, shader, 3D engine, telemetry, cloud, global hotkey, or disk indexer.
- Do not merge, push, publish, or call the normal installer/start script.
- Manual/visual/performance claims require evidence; otherwise mark `NOT TESTED`.

## Review Focus

- Unknown/corrupt v5 appearance values normalize without making text unreadable.
- Resource-saver mode disables decorative motion/glow without hiding focus or semantic state.
- Dynamic theme resources remain available to every window and do not break window construction.
- Control Center retains all V2 operations at its minimum supported size.
- V3 launcher cannot run an old output after a failed build and never points at the installed config.

---

### Task 1: Schema-v5 personalization

**Files:**
- Modify: `src/NextMind.Desktop.Core/Config/AppConfig.cs`
- Modify: `src/NextMind.Desktop.Core/Config/ConfigValidator.cs`
- Test: `tests/NextMind.Desktop.Tests/AppearancePreferencesTests.cs`

**Interfaces:**
- Produces: `AppearancePreferencesConfig`, `AppearanceAccent`, and `AppConfig.Appearance`
- Consumed by: Task 2 theme adapter and Task 3 Control Center

- [ ] Write failing tests for defaults, v4→v5 migration, numeric clamping, unknown accent fallback, serialization round-trip, and independence from workspace-profile switching.
- [ ] Run focused tests and confirm RED because schema v5/appearance types do not exist.
- [ ] Implement schema v5 and normalization with no WPF dependency.
- [ ] Run focused tests and full suite; commit.

### Task 2: Shared Future design system and runtime theme

**Files:**
- Create: `src/NextMind.Desktop.App/Themes/FutureTheme.xaml`
- Create: `src/NextMind.Desktop.App/FutureTheme.cs`
- Modify: `src/NextMind.Desktop.App/App.xaml`
- Modify: `src/NextMind.Desktop.App/App.xaml.cs`
- Test: `tests/NextMind.Desktop.Tests/FutureThemeTests.cs`

**Interfaces:**
- Consumes: `AppearancePreferencesConfig`
- Produces: shared resource keys and `FutureTheme.Apply(AppearancePreferencesConfig)`
- Consumed by: Tasks 3 and 4 XAML

- [ ] Write failing STA integration tests for required component resource keys and applied accent/opacity/motion/resource-saver values.
- [ ] Confirm RED because the dictionary/adapter do not exist.
- [ ] Implement one merged dictionary and bounded dynamic-resource updates; no repeating animation.
- [ ] Apply theme after config load and run focused/full tests; commit.

### Task 3: Futuristic Control Center and personalization workflow

**Files:**
- Modify: `src/NextMind.Desktop.App/ControlCenterWindow.xaml`
- Modify: `src/NextMind.Desktop.App/ControlCenterWindow.xaml.cs`
- Modify: `src/NextMind.Desktop.App/App.xaml.cs`
- Test: `tests/NextMind.Desktop.Tests/ControlCenterAppearanceTests.cs`

**Interfaces:**
- Consumes: Task 1 appearance model and Task 2 resources/apply adapter
- Produces: visible controls for glow, opacity, accent, animations, resource saver, apply, and reset

- [ ] Write failing STA tests that construct the window with real managers/config and verify named appearance controls map to a saved normalized model and reset defaults.
- [ ] Confirm RED because the controls/callback do not exist.
- [ ] Rebuild the window as responsive scrollable cards while retaining every V2 event path.
- [ ] Implement apply/reset callbacks, runtime theme refresh, and persistence.
- [ ] Run focused/full tests and commit.

### Task 4: Zones, widgets, search, dialogs, and interaction polish

**Files:**
- Modify: `src/NextMind.Desktop.App/ZoneWindow.xaml`
- Modify: `src/NextMind.Desktop.App/WidgetWindow.xaml`
- Modify: `src/NextMind.Desktop.App/SearchWindow.xaml`
- Modify: `src/NextMind.Desktop.App/NoteEditorWindow.xaml`
- Modify: `src/NextMind.Desktop.App/PromptWindow.xaml`
- Modify: `src/NextMind.Desktop.App/ChoiceWindow.xaml`
- Modify only if required: corresponding code-behind files
- Test: extend WPF construction/search interaction tests

**Interfaces:**
- Consumes: Task 2 resource keys; existing managers/events remain unchanged
- Produces: coherent Future Workspace presentation across all existing UI surfaces

- [ ] Write failing integration assertions for category display, shared component styles, empty states, and preserved item-only search double click.
- [ ] Confirm RED for new named visual elements/resources.
- [ ] Restyle each surface using shared resources; add only event-driven, zero-duration-capable transitions.
- [ ] Run WPF-focused tests, full suite, Release build, and commit.

### Task 5: TEST-V3 launcher, documentation, and final evidence

**Files:**
- Create: `TEST-V3.cmd`
- Create: `README-TEST-V3.md`
- Create: `tests/Test-V3-Launcher.Tests.ps1`
- Create: `docs/DESKTOP-V3-DESIGN-SYSTEM.md`
- Create: `docs/DESKTOP-V3-IMPLEMENTATION.md`
- Create: `docs/DESKTOP-V3-TEST-RESULTS.md`

**Interfaces:**
- Consumes: current V3 source/build and Task 2 theme marker
- Produces: one-click isolated launcher plus owner-facing implementation/test record

- [ ] Write a failing real launcher test for `--verify-only`, two unique config roots, failing-build gate, current V3 marker, and no new process.
- [ ] Confirm RED because `TEST-V3.cmd` does not exist.
- [ ] Implement the self-contained launcher by adapting the verified V2 safety pattern to V3 markers/paths.
- [ ] Write the non-technical README and evidence documents; explicitly record missing concept image and manual/performance `NOT TESTED` items.
- [ ] Run launcher test, full automated suite, Release build, `git diff --check`, forbidden-operation audit, and commit.

