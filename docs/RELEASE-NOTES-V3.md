# NextMind Desktop 3.0.0 — Release Notes (draft)

**Your desktop. Your rules.** V3 brings the Future Workspace experience to NextMind Desktop: a cohesive visual system, workspace profiles, local command search, widgets and live personalization while retaining the project's local-first safety model.

## Highlights

- A redesigned Control Center and shared V3 chrome for zones and widgets.
- Profiles let one installation hold multiple independent workspace arrangements.
- `Ctrl+K` searches commands, zones, widgets and Start-menu applications locally.
- Clock & Date and Quick Note widgets persist per profile.
- Appearance controls preview opacity, glow and accent immediately; Apply commits, and closing rolls back unapplied changes.
- Existing configuration is migrated to schema V5 without discarding zones, items, geometry, profiles or notes.

## Install or update

Download and extract `NextMindDesktop-3.0.0-Setup.zip`, exit NextMind Desktop normally, and run `INSTALL-V3.cmd`. The installer refuses to proceed while the app is running, validates the payload manifest, creates and verifies a complete pre-upgrade backup, then replaces only `%LOCALAPPDATA%\NextMind\Desktop\app`.

Configuration, profiles, zones, widget content, geometry, managed items, logs and the existing autostart preference remain in place. Rollback and uninstall scripts are included. The installed build requires the .NET 8 Desktop Runtime for Windows x64.

## Portable

`NextMindDesktop-3.0.0-Portable-win-x64.zip` is self-contained. Extract the complete archive and run `NextMindDesktop.exe`. Its marker selects `%LOCALAPPDATA%\NextMind\Desktop-Portable`; it does not use the installed edition's data root, create an uninstall entry or register autostart.

## Safety

NextMind Desktop remains local-only: no account, cloud service, telemetry or ads. File references stay in place. Managed Desktop Moves remain off by default, journalled and recoverable, and never overwrite an existing destination.

## Release status

These notes are a publication draft. The current candidate packages are audit inputs only and must be regenerated from the final integrated public-main commit before release; their final hashes will therefore be different.
