# Responsive Clock & Date Widget Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the V3 clock typography respond smoothly and safely to widget size and DPI.

**Architecture:** A pure layout calculator derives clock typography from the content area's WPF device-independent size. `WidgetWindow` applies the result on `SizeChanged`; the existing header, theme resources, clock timer, persistence, and window chrome remain unchanged.

**Tech Stack:** .NET 8, C# 12, WPF, xUnit

**Spec:** `docs/superpowers/specs/2026-11-01-responsive-clock-widget-design.md`

## Global Constraints

- No new libraries, timers, processes, configuration fields, or schema changes.
- Do not launch the app or modify the installed application/user configuration.
- No merge, push, or release.

## Review Focus

- Extremely small restored geometry must remain finite and must not create negative font sizes.
- Large geometry must grow but remain bounded and preserve margins.
- Equivalent logical sizes at 96, 144, and 192 DPI must yield equivalent typography.
- Long localized dates must shrink to fit rather than clip.
- Resizing clock content must not resize title-bar typography or change holographic resources.

---

### Task 1: Responsive layout policy

**Files:**
- Create: `src/NextMind.Desktop.App/ClockWidgetLayout.cs`
- Create: `tests/NextMind.Desktop.Tests/ClockWidgetLayoutTests.cs`

**Interfaces:**
- Produces: `ClockWidgetLayout.Calculate(double widthDip, double heightDip) -> ClockWidgetMetrics`
- Produces: metrics for time, date, label, top/date spacing, and the content height budget.

- [x] Write table-driven failing tests for compact, default, large, tiny, and DPI-equivalent sizes.
- [x] Run the focused tests and confirm failure because the layout API does not exist.
- [x] Implement the smallest bounded, monotonic layout calculation that satisfies the tests.
- [x] Run focused tests and confirm all pass.

### Task 2: WPF integration

**Files:**
- Modify: `src/NextMind.Desktop.App/WidgetWindow.xaml`
- Modify: `src/NextMind.Desktop.App/WidgetWindow.xaml.cs`
- Modify: `tests/NextMind.Desktop.Tests/ClockWidgetLayoutTests.cs`

**Interfaces:**
- Consumes: `ClockWidgetLayout.Calculate` from Task 1.
- Produces: immediate typography updates from the clock content area's `SizeChanged` event.

- [x] Add a failing STA integration test that arranges the real clock content at compact and large sizes; the title bar remains outside the responsive component.
- [x] Run the focused test and confirm the responsive component does not yet exist.
- [x] Name the clock label/date-fit elements, connect `SizeChanged`, apply metrics, and wrap the localized date in a down-only fit container.
- [x] Run the focused tests and confirm all pass.

### Task 3: Verification and handoff

**Files:**
- Modify if needed: `docs/DESKTOP-V3-TEST-RESULTS.md`

- [x] Run the full test suite.
- [x] Build the solution in Release mode with warnings treated as errors.
- [x] Run `git diff --check`, inspect the diff, and confirm no launcher/config/install behavior changed.
- [x] Report automated evidence and the manual `TEST-V3.cmd` procedure without claiming visual PASS.
