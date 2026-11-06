# Future Workspace Chrome and Scrollbars Design

## Intent

Remove the remaining light Windows/WPF surfaces from V3 Control Center, zones, and widgets while preserving native window behavior, scrolling, and resize functionality.

## Root causes

- Control Center uses the shared colors but retains the default Windows non-client frame, so Windows draws the light caption.
- The V3 theme has no `ScrollBar` or `ScrollViewer` template, so WPF falls back to the OS theme, including its light corner filler.
- `WidgetWindow` declares a grip style but does not apply it; its resize `Thumb` controls therefore retain the default light template.
- `ZoneWindow` already supplies transparent resize grips, but the `ListBox`-owned `ScrollViewer` still uses the default WPF template.

## Approaches

1. **Hide the bars and disable native chrome.** Rejected because it removes required scrolling and resize behavior.
2. **Handle non-client hit testing and resizing manually.** Rejected because it duplicates fragile Windows behavior across DPI and monitors.
3. **WPF `WindowChrome` plus shared control templates (chosen).** Use `WindowChrome` for native caption/resize behavior and focused WPF templates for scrollbar, scroll viewer, chrome buttons, and resize thumbs.

## Design

- `FutureWindowChrome.Apply(Window)` configures `WindowStyle=None`, `ResizeMode=CanResize`, a 44 DIP caption, 7 DIP resize border, no glass, and native caption behavior.
- Control Center adds a 44 DIP dark title row. Its three keyboard-focusable buttons invoke `SystemCommands` for minimize, maximize/restore, and close. The caption surface itself remains native draggable chrome.
- `FuturisticScrollBarStyle` uses a thin transparent track, dark page-click regions, and an accented thumb. Its named repeat buttons retain page commands and the thumb remains draggable.
- `FuturisticScrollViewerStyle` preserves `PART_ScrollContentPresenter` and both named scrollbars, and paints the intersection corner dark.
- The keyed scroll-viewer style is applied only to Control Center, Quick Note, and the zone list to limit regression scope.
- `FuturisticResizeThumbStyle` replaces the default system `Thumb` visuals; widget grips use it and keep their existing drag handlers/cursors.

## Non-goals and constraints

- Do not change appearance persistence, V5 configuration, autostart, active installation, or user data.
- Do not add dependencies, polling, timers, or custom Win32 non-client handling.
- No merge, push, or release.
- Automated tests validate structure and behavior contracts; visual acceptance remains manual.
