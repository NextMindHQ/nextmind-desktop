# NextMind Desktop V3 — Future Workspace design

## Intent

Transform the existing WPF desktop utility into a coherent 2045-style workspace without changing its safety model, framework, layout engine, or local-only architecture. V3 must feel authored rather than themed: deep glass surfaces, restrained electric-blue/cyan light, compact HUD details, clear hierarchy, and strong keyboard/focus behavior.

The owner supplied a detailed written direction but no local concept image. Exact composition is therefore not inferred; the written specification is authoritative and the missing reference is reported.

## Goals

- One reusable WPF design system shared by every window.
- A responsive Control Center that exposes every V2 function plus persisted appearance controls.
- Futuristic zones, widgets, search, prompts, and menus while retaining their existing behavior.
- Schema-v5 personalization: glow, panel opacity, accent, animations, and resource-saver mode.
- Lightweight state-driven motion only; zero continuous decorative rendering or polling.
- An isolated `TEST-V3.cmd` that builds current source and never touches the installed app or live config.

## Non-goals and anti-goals

- No rewrite, new UI framework, 3D engine, shader package, cloud service, telemetry, global hotkey, disk indexer, or second layout engine.
- No hiding operational or safety controls for visual cleanliness.
- No change to drag/drop or Managed Items semantics.
- No automatic stop of the installed application, merge, push, release, or write to its config/autostart.

## Approaches considered

1. **Native WPF resource system (selected).** A merged `FutureTheme.xaml` supplies named brushes, templates, and keyed component styles. A small `FutureTheme` adapter applies persisted values to dynamic resources. This retains the current framework and gives runtime personalization without dependencies.
2. **Third-party WPF theme library.** Faster initial coverage but visually generic, adds dependency and migration risk, and conflicts with the authored design-system requirement.
3. **Custom-drawn/shader shell.** Highest visual ceiling but violates the lightweight/performance goals and would force a partial rewrite.

## Visual system

- Canvas: `#080C14`; elevated surface: graphite/navy glass around `#E6172230`; secondary surface: `#CC101925`.
- Default accent: electric blue `#38BDF8`; secondary cyan `#22D3C5`.
- Primary text: cool white `#EEF6FF`; secondary text: `#91A4B8`.
- Semantic warning and error retain amber/red and never become decoration.
- Corners: 8–14 px depending on scale; one-pixel keyline plus a restrained accent edge.
- Typography remains local Segoe UI Variable/Segoe UI; uppercase micro-labels and generous tracking create HUD hierarchy without bundled fonts.
- Components are keyed styles: holographic panel, primary/secondary/danger button, text box, toggle/check box, slider, menu, tooltip, header, search result, widget container, and zone item.

## Personalization model

`AppearancePreferencesConfig` is global rather than profile-specific: profile switching changes workspace content, while the visual system stays consistent. It stores:

- `GlowIntensity` (`0..1`, default `0.55`)
- `PanelOpacity` (`0.55..1`, default `0.90`)
- `AccentColor` (curated stable ID: `electric-blue`, `cyan`, `violet`, `amber`)
- `AnimationsEnabled` (default `true`)
- `ResourceSaverMode` (default `false`)

Schema v4 migrates to v5 by defaults. Validation clamps numeric values and falls back from unknown accent IDs. Resource-saver mode forces motion duration to zero and removes decorative glow while keeping borders, contrast, and all behavior.

## Runtime architecture

- `FutureTheme.xaml` contains static component templates and dynamic-resource references.
- `FutureTheme.Apply(AppearancePreferencesConfig)` updates a bounded set of application resources; existing windows update through `DynamicResource`.
- Short entrance/hover/focus transitions are event-driven. There are no animation clocks that repeat indefinitely.
- The existing minute-aligned clock timer remains unchanged.
- Control Center edits a copy and applies it through an app-level callback that validates, applies, and persists the config.

## Window changes

- **Control Center:** resizable, scrollable two-column card layout when space allows; single-column fallback through wrapping. Branded header, active-profile treatment, operational sections, appearance card, and a visible system/safety footer.
- **Widgets:** shared glass container and header; larger clock hierarchy with HUD micro-labels; note text remains scrollable and edited through the existing editor.
- **Zones:** shared glass shell, accent drag state, stronger empty state, clearer title/chrome and item focus/selection.
- **Search:** command-center shell, category chips from `SearchEntryKind`, accent selection, empty/help states, and preserved item-only double click.
- **Dialogs/editor:** same theme resources so modal flows do not fall back to stock Windows styling.

## Testing and evidence

- Core tests cover schema-v5 migration, normalization, serialization, defaults, reset, and profile independence.
- WPF integration tests load the shared resource dictionary, verify required component keys, and confirm resource-saver mapping.
- Existing 298 tests remain the regression floor.
- `TEST-V3.cmd --verify-only` is exercised against a successful real Release build and a fake failing `dotnet.cmd`; neither path may start V3.
- Visual/manual, DPI, live drag/drop, and performance measurements remain `NOT TESTED` until the owner closes the installed instance and launches the isolated V3 build.

