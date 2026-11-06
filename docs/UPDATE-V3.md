# Safe update to V3

1. Close NextMind Desktop normally from its tray icon: **Exit**. Do not use End task.
2. Extract `NextMindDesktop-3.0.0-Setup.zip` to a temporary folder.
3. Double-click `INSTALL-V3.cmd`.
4. The installer verifies every payload file, creates and verifies a dated full backup, then replaces only the `app` directory.
5. Start NextMind Desktop from the Start menu. Confirm profiles, zones, widget content and positions.

The installer preserves `config.json`, its backups, `ManagedItems`, `journal` and `logs`. Its receipt records the exact rollback backup path.
