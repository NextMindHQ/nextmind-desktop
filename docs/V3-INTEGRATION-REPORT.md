# NextMind Desktop V3 — Integration Report

Date: 2026-11-06. Branch: `codex/v3-public-integration`. Base: public `main` at `22eef1686888cf0e0df2290931fa8289c663616c`.

## Result

The integration branch was created from the public history and the V3 work was imported in reviewed functional groups. Public history was not replaced or rebased. No push, merge to `main`, tag or GitHub Release was performed.

## Preserved public-main functionality

- Local zones, references and guarded Managed Desktop Moves.
- Per-zone search, including its no-match empty state.
- Free-slot placement, edge snapping, smart expansion and layout restore.
- Obstruction-aware context menus, submenu placement and dismissal behavior.
- Public CI workflows, Apache-2.0 license, security policy, threat/data-safety documentation and UI verification launcher.

## Integrated V3 functionality

- Workspace profiles and schema V5 migration.
- Assisted layout history and undo.
- Local global search for commands, zones, widgets and Start-menu applications.
- Clock & Date and Quick Note widgets with profile-scoped persistence.
- Future Workspace theme, shared chrome, responsive widgets and live appearance preview.
- V3 Control Center, five presentation PNGs, GitHub README, changelog and release notes.
- Manifest-verified Setup package with backup/rollback and self-contained Portable package with an isolated data root.

## Manual conflict decisions

- Zone windows retain public per-zone filtering while using the V3 visual system and empty-state treatment.
- Public geometry/menu algorithms remain authoritative where the V3 line had no equivalent behavior.
- V3 profiles reload existing zone windows without discarding public zone state.
- Portable mode disables installed-edition autostart integration but is not labelled as a developer test mode.
- Redirected test configuration and V3 test builds use isolated mutex and activation channels; normal production behavior remains unchanged.

## Verification

- Public baseline: 364/364 tests passed before integration.
- Integrated suite: 432/432 tests passed, with no skipped tests.
- Release build: passed with 0 errors and 0 warnings.
- V2 and V3 verification launchers: passed without modifying the installed configuration.
- Setup install and rollback: passed against synthetic application and user-data roots; configuration, managed items and logs retained their SHA-256 values.
- Portable: passed from the extracted release archive; self-contained runtime present, isolated data written only to the redirected temporary root, live installed configuration and autostart state unchanged.
- Package hygiene: no PDB, logs, configuration, test marker, backups or user data in either archive.
- Packaging licenses: both archives contain Apache-2.0; Portable additionally contains the .NET redistribution license and third-party notices.
- Installer compatibility: the actual Setup scripts pass under Windows PowerShell 5.1, and custom-root uninstall is confined to the root containing its receipt.

The reproducible package builder writes the exact source commit to `RELEASE-MANIFEST.json` and generates `artifacts/release-v3/dist/SHA256SUMS.txt`. Those ignored build outputs must be regenerated from the eventual `main` release commit; hashes are therefore deliberately not committed to this report.

## Release readiness

The local candidate is ready for publication review. The remaining gates are intentionally external: explicit approval to integrate, a normal merge to the then-current public `main`, green GitHub CI, regeneration of both archives from that exact commit, and verification of the public tag and downloadable Release assets.
