# Release checklist (maintainers)

A checklist for preparing a NextMind Desktop release. Nothing here publishes anything by itself: pushing, tagging and publishing are separate,
deliberate maintainer actions.

## 1. Code and tests
- [ ] Working tree clean; the release branch is up to date and reviewed
- [ ] `dotnet build -c Release --no-incremental`: **0 warnings, 0 errors** (the whole solution, including the App project)
- [ ] `dotnet test -c Release`: all tests pass; note the test count in the changelog if it changed
- [ ] CI is green on the release commit

## 2. Manual checks (isolated test instance only, see [DEVELOPMENT.md](DEVELOPMENT.md))
Run them with `tools\test-ui.ps1`; never against an installed copy or real files.
- [ ] **Zones:** create, rename, hide, delete; new zones appear in a free spot; drag with snapping (and with Alt: no snapping); overlapping on purpose works
- [ ] **Collapse / expand:** opens downward, or upward when it would cover other zones or leave the screen; collapsing returns the title bar to where it was
- [ ] **Collapsed zone near the bottom edge:** move it there, force a save, restart with `-Reuse`; it stays exactly where it was, then expand and collapse it
- [ ] **Search:** magnifier in every zone (also collapsed); live filtering, Polish letters, several words; ↓ / ↑, Enter, Esc, ✕; no match message; collapsing closes the bar and shows everything
- [ ] **Menus:** zone and item menus close on a click elsewhere, after switching application and with Alt+Tab; "Sort by" and "Move to Zone" do not cover neighbouring zones; check next to a neighbour, at the right edge, at the bottom edge and with a long zone list
- [ ] **Show desktop (Win+D)** shows and hides the zones; zones never float above ordinary windows
- [ ] **Restart:** layout, collapsed state and items survive an app restart; Explorer restart recovers the zones and the tray icon
- [ ] **Large zones:** a 600- and a 2000-item zone stay responsive while searching
- [ ] Optional: another display scale and a second monitor (zones stay on screen, menus on the same monitor)

## 3. Privacy and safety review
- [ ] Review the full diff since the last release; search it for user names, e-mail addresses, absolute user paths, tokens and keys: **no hits**
- [ ] No test data, logs or configuration files in the repository
- [ ] No new network access, telemetry, delete or move calls on user data (the guard tests cover this)
- [ ] Screenshots: only synthetic demo content, no wallpaper, desktop icons, taskbar, other applications or personal names; PNGs carry no text/metadata chunks

## 4. Documentation
- [ ] README: features and "Using Zones" match the release; remove the "new in the next release" markers; download links and file names point to the new version
- [ ] CHANGELOG: move the *Unreleased* entries under the new version heading with its date; update the compare links
- [ ] Screenshots current ([SCREENSHOT-PLAN.md](SCREENSHOT-PLAN.md))
- [ ] Known limitations updated; contributing and development guides still accurate

## 5. Version and packaging
- [ ] Set the version in `Directory.Build.props` (single source) and rebuild
- [ ] Publish the release ZIP the same way as the previous release; include the SHA-256 checksum file
- [ ] Verify the ZIP on a clean machine or VM: it starts, creates a first zone, the tray icon appears, "Start with Windows" can be toggled and removed
- [ ] Release notes: highlights from the changelog, the SmartScreen note (the executable is not code-signed), the checksum and how to verify it

## 6. Publishing (maintainer decision)
- [ ] Merge the release branch; tag `vX.Y.Z`; publish the release with the ZIP and checksum
- [ ] After publishing: download the asset, verify the checksum, `git clone` into an empty folder, `dotnet build -c Release` and `dotnet test -c Release` from the clone
- [ ] Check the repository front page: badges, README images, links

## 7. If something goes wrong
- [ ] Unpublish or mark the release as a pre-release, fix on a new branch, release a patch version; never re-tag a published version
