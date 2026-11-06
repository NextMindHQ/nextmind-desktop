# NextMind Desktop V3 release design

## Decision

Ship version 3.0.0 by extending the existing per-user publish model. The installer keeps the stable `%LOCALAPPDATA%\NextMind\Desktop\app` path, preserves the rest of the data root, and refuses to continue while NextMind Desktop is running. It never terminates a process.

## Safety model

- Back up the complete existing `Desktop` data root and verify every file with SHA-256 before replacement.
- Stage and validate the new payload before touching the installation.
- Replace only `app`; never delete or overwrite `config.json`, its backups, `ManagedItems`, `journal`, or `logs`.
- Preserve the user's autostart preference and stable Run-key target.
- Keep the pre-V3 backup and provide an explicit rollback script.
- Test schema 3 to schema 5 migration on a copy of the real configuration and compare all profiles, zones, items, widgets, notes, geometry, layout and autostart fields.

## Portable model

Portable builds contain the .NET runtime and a `NextMindDesktop.portable` marker. The marker redirects data to `%LOCALAPPDATA%\NextMind\Desktop-Portable`, separated from the installed edition, and disables all autostart registry interaction. Data is not stored beside the ZIP because protected-folder safety intentionally prevents application writes under Documents/Desktop.

## Production hygiene

Remove the `V3 Live Preview 2` label from the UI and omit the V3 test marker from production output. `TEST-V3.cmd` explicitly opts into the marker at build time.

## Release artifacts

- `NextMindDesktop-3.0.0-Setup.zip`: payload, guarded installer, rollback/uninstall helpers and user documentation.
- `NextMindDesktop-3.0.0-Portable-win-x64.zip`: self-contained application, portable marker and instructions.

The setup archive is deliberately not an MSI: the repository has no existing MSI toolchain, while the current installation already uses a per-user published directory.
