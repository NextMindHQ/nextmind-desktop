# Desktop V3 Appearance Live Preview Hotfix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Zapewnić czytelny selektor akcentu oraz natychmiastowy, odwracalny podgląd wyglądu ograniczony do stref i widgetów.

**Architecture:** Rozdzielić stałe zasoby `Future*` od dynamicznych `Workspace*`, dodać testowalną sesję podglądu i związać zdarzenia UI z podglądem bez zapisu. Warstwy treści widgetów/stref dostaną osobne tło, podczas gdy nagłówki i kontrolki pozostaną stałe.

**Tech Stack:** .NET 8, C# 12, WPF XAML, xUnit

**Spec:** `docs/superpowers/specs/2026-11-01-desktop-v3-appearance-live-preview-hotfix-design.md`

## Global Constraints

- Zachować konfigurację V5 i nie zapisywać podczas podglądu.
- Nie używać `Window.Opacity`, procesów pomocniczych, pollingu ani ciężkich animacji.
- Nie uruchamiać V3, nie modyfikować instalacji, nie wykonywać merge, push ani release.

## Review Focus

- Zamknięcie po Apply, następnie po kolejnej niezapisanej zmianie, przywraca ostatni Apply.
- Inicjalizacja kontrolek nie generuje przypadkowego podglądu ani zapisu.
- Resource Saver nadal zeruje glow i motion.
- Klawiatura pozwala wybrać każdy akcent, a focus jest widoczny.
- Niska opacity nie zmienia nagłówków, tekstu ani kontrolek.

---

### Task 1: Transactional appearance preview

**Files:**
- Create: `src/NextMind.Desktop.App/AppearancePreviewSession.cs`
- Modify: `src/NextMind.Desktop.App/AppearanceEditor.cs`
- Test: `tests/NextMind.Desktop.Tests/AppearancePreviewSessionTests.cs`

**Interfaces:**
- Produces: `Preview(AppearancePreferencesConfig)`, `Commit(AppearancePreferencesConfig)`, `Rollback()` and value-copy behavior.

- [ ] Write tests for preview without persistence, commit, reset preview, and rollback to the latest committed snapshot.
- [ ] Run the tests and verify RED because the session does not exist.
- [ ] Implement the minimal session and copy helper.
- [ ] Run targeted tests and verify GREEN.

### Task 2: Workspace-only theme resources and layered surfaces

**Files:**
- Modify: `src/NextMind.Desktop.App/FutureTheme.cs`
- Modify: `src/NextMind.Desktop.App/Themes/FutureTheme.xaml`
- Modify: `src/NextMind.Desktop.App/WidgetWindow.xaml`
- Modify: `src/NextMind.Desktop.App/ZoneWindow.xaml`
- Test: `tests/NextMind.Desktop.Tests/FutureThemeTests.cs`

**Interfaces:**
- Consumes: normalized `AppearancePreferencesConfig`.
- Produces: dynamic `WorkspaceAccent*`, `WorkspaceContentBrush`, and `WorkspacePanelEffect` resources used only by widget/zone surfaces.

- [ ] Add tests proving dynamic workspace resources change while fixed Control Center resources remain unchanged.
- [ ] Add XAML structure tests proving content-only backgrounds and fixed headers/controls.
- [ ] Run targeted tests and verify RED.
- [ ] Implement resource separation and layered surface bindings.
- [ ] Run targeted tests and verify GREEN.

### Task 3: Control Center live preview and accent selector

**Files:**
- Modify: `src/NextMind.Desktop.App/ControlCenterWindow.xaml`
- Modify: `src/NextMind.Desktop.App/ControlCenterWindow.xaml.cs`
- Modify: `src/NextMind.Desktop.App/App.xaml.cs`
- Test: `tests/NextMind.Desktop.Tests/ControlCenterAppearanceTests.cs`

**Interfaces:**
- Consumes: `AppearancePreviewSession` and `Workspace*` resources.
- Produces: immediate preview events, save-only Apply, rollback-on-close, and a dark accessible swatch selector.

- [ ] Add UI behavior/structure tests for values, dark swatches, focus, and handlers.
- [ ] Run targeted tests and verify RED.
- [ ] Wire slider/toggle/accent changes to preview; Apply commits; Reset previews; close rolls back.
- [ ] Run targeted tests and verify GREEN.

### Task 4: Regression verification and documentation

**Files:**
- Modify: `docs/DESKTOP-V3-TEST-RESULTS.md`
- Modify: `README-TEST-V3.md`

- [ ] Run the full test suite and Release build into isolated output paths.
- [ ] Run launcher verification without launching V3.
- [ ] Run `git diff --check` and audit forbidden operations.
- [ ] Record results and the one-click retest procedure.
