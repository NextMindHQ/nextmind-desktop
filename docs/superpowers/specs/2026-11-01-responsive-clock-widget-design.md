# Responsive Clock & Date Widget Design

## Intent

Make the V3 Clock & Date widget use its available content area: compact and readable at the minimum size, visibly larger when expanded, and stable while the user drags a resize grip.

## Constraints

- Keep the existing WPF window, fixed title bar, holographic resources, opacity, colors, and minute-aligned clock update.
- Add no dependency, polling loop, timer, process, configuration field, or schema migration.
- Do not touch the installed application or user configuration.
- Work only on `forge/desktop-v3-future-workspace`; no merge, push, or release.

## Approaches

1. **A single WPF `Viewbox`.** Very small change and naturally smooth, but all three text levels scale identically; at the minimum widget size `LOCAL TIME` and the date become too small.
2. **Responsive font policy driven by the WPF content size (chosen).** Calculate time, date, label, and spacing from available device-independent pixels, with readable minimums and conservative width/height budgets. A down-only `Viewbox` around the localized date prevents long date strings from clipping.
3. **Breakpoint templates.** Readable at a few fixed sizes, but produces jumps during resize and duplicates markup.

Approach 2 best matches smooth resizing, readable compact mode, and deterministic DPI testing without adding timers or dependencies.

## Behavior

- The fixed 30 DIP title bar never scales.
- `ClockWidgetLayout.Calculate(widthDip, heightDip)` returns bounded font sizes and spacing for the content surface.
- `WidgetWindow` applies the layout on WPF `SizeChanged`; this is event-driven and performs no persistence.
- A long localized date may scale down within its own row but is not clipped or ellipsized.
- DPI behavior is based on logical WPF units: physically equivalent sizes at different DPI produce the same typography.

## Verification

- Unit tests cover compact, default, and large content sizes, monotonic growth, layout bounds, invalid/tiny input, and equivalent physical geometry at 96/144/192 DPI.
- A WPF integration test verifies that resizing the real widget changes the rendered clock typography while leaving the title bar font unchanged.
- Run the full test suite and Release build; do not launch the application.
