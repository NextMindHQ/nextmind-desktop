# Changelog

All notable changes to NextMind Desktop are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

Changes since v0.1.0. They are in development on the `feature/zones-collision` branch and are **not on `main` yet**. They will ship with the next release
(planned as v0.2.0); the version number is not changed yet.

### Added

* **Search in a zone.** Every zone has a magnifier in its title bar (collapsed zones too — they expand first). Type to filter that zone's items live,
  ignoring case and diacritics (Polish letters included); several words must all match. It searches the names of files, folders and shortcuts (not
  their content or parent folders) and never changes the zone's contents, order or your files.
  Keyboard: **↓ / ↑** select a result, **Enter** opens the selected result (or the first match), **Esc** clears the query and a second **Esc** closes the
  bar; a **✕** inside the field clears the query. Clearing it (or closing the bar) shows everything again.
* **New zones appear in a free spot** on the desktop instead of on top of existing ones.
* **Snapping while you drag a zone.** Edges align to neighbouring zones and to the screen edge. Hold **Alt** to move freely; overlapping zones on purpose
  is still allowed.
* **Smarter expanding.** An expanding zone opens downward, or upward when that covers fewer other zones or would leave the screen. Other zones are never moved,
  and collapsing a zone that opened upward returns its title bar to where it was.
* **Menus stay out of the way.** Zone and item context menus, and their submenus ("Sort by", "Move to Zone"), open where they cover the least of
  neighbouring zones and stay on screen — including very long "Move to Zone" lists.
* **Developer tooling:** an isolated test instance (`tools/test-ui.ps1`) that cannot touch an installed copy, its settings or its autostart entry.
  See [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md). Also a maintainer [release checklist](docs/RELEASE-CHECKLIST.md).

### Fixed

* **Context menus now close** when you click elsewhere, switch to another application or press Alt+Tab (they used to stay on screen).
* **A collapsed zone keeps its position after a restart.** A zone collapsed near the bottom edge of the screen used to move upward every time the app
  started; its remembered expanded height is preserved for the next expand.

### Changed

* Moving a zone, expanding it, opening its menus and searching it are covered by new automated tests (the suite grew from 274 to 364 tests).

## [0.1.0] — 2026-10-03

First public release (Windows x64).

* Collapsible **Zones** on the desktop, below every application and above the icons; tray icon and Control Center.
* Drag & drop of files, folders and shortcuts (reference items); sorting and manual ordering; native Windows icons.
* Optional, opt-in **Managed Desktop Moves** with a write-ahead journal and recovery (off by default).
* Start with Windows, single instance, Explorer-restart recovery, per-monitor DPI, positions and layout persisted.
* No account, no cloud, no telemetry.

[Unreleased]: https://github.com/NextMindHQ/nextmind-desktop/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/NextMindHQ/nextmind-desktop/releases/tag/v0.1.0
