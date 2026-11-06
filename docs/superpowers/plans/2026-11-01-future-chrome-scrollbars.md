# Future Workspace Chrome and Scrollbars Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace light native/WPF surfaces with V3 chrome while retaining all window, scrolling, keyboard, and resize behavior.

**Architecture:** Control Center uses WPF `WindowChrome` for native non-client behavior and a custom title row for visuals/commands. The shared theme provides explicit scrollbar, scroll-viewer, chrome-button, and resize-thumb templates that are applied only to the affected surfaces.

**Tech Stack:** .NET 8, C# 12, WPF, xUnit

**Spec:** `docs/superpowers/specs/2026-11-01-future-chrome-scrollbars-design.md`

## Global Constraints

- Preserve scrolling, mouse-wheel, thumb dragging, keyboard navigation, window resize, maximize/restore, multi-monitor, and per-monitor DPI behavior.
- No new libraries, timers, polling, processes, schema fields, or configuration writes.
- Do not launch or modify the installed application; no merge, push, or release.

## Review Focus

- Maximized Control Center must remain inside the monitor work area and restore correctly.
- Caption buttons must remain interactive inside the native caption hit-test region.
- Vertical and horizontal scrollbar orientations must use correct page commands and thumb minimums.
- ScrollViewer corner filler must never fall back to a light system brush.
- Collapsed widgets/zones must retain resize-grip visibility behavior and no default `Thumb` chrome.

---

### Task 1: Native Control Center chrome

**Files:**
- Create: `src/NextMind.Desktop.App/FutureWindowChrome.cs`
- Modify: `src/NextMind.Desktop.App/ControlCenterWindow.xaml`
- Modify: `src/NextMind.Desktop.App/ControlCenterWindow.xaml.cs`
- Create: `tests/NextMind.Desktop.Tests/FutureWindowChromeTests.cs`

- [x] Write failing STA tests for `FutureWindowChrome.Apply`, maximize/restore state, and fixed DPI-independent chrome metrics.
- [x] Confirm RED because the helper does not exist.
- [x] Implement `WindowChrome` configuration and the title-row system command handlers.
- [x] Confirm focused tests pass.

### Task 2: Shared dark scroll and resize controls

**Files:**
- Modify: `src/NextMind.Desktop.App/Themes/FutureTheme.xaml`
- Modify: `src/NextMind.Desktop.App/ControlCenterWindow.xaml`
- Modify: `src/NextMind.Desktop.App/WidgetWindow.xaml`
- Modify: `src/NextMind.Desktop.App/ZoneWindow.xaml`
- Modify: `tests/NextMind.Desktop.Tests/FutureThemeTests.cs`

- [x] Add failing WPF tests for required style keys, named scroll parts, dark corner, correct page commands, draggable thumb, and resize-thumb template.
- [x] Confirm RED because the shared templates do not exist.
- [x] Implement the shared styles and apply them to affected surfaces without removing existing handlers.
- [x] Confirm focused tests pass.

### Task 3: Verification and handoff

**Files:**
- Modify: `docs/DESKTOP-V3-TEST-RESULTS.md`

- [x] Run the full test suite and Release build.
- [x] Run `TEST-V3.cmd --verify-only` and confirm no app process starts.
- [x] Inspect the complete diff and verify installation/config/autostart code is untouched.
- [x] Commit locally on the V3 branch and provide the manual checklist without claiming visual PASS.
