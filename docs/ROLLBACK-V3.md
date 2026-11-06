# Restore the previous version

1. Close NextMind Desktop normally from the tray.
2. Locate the `Backup` path in `%LOCALAPPDATA%\NextMind\Desktop\install-v3-receipt.json`.
3. From the extracted Setup package, run PowerShell script `ROLLBACK-V3.ps1 -Backup "<that path>"`.
4. The script first creates a safety copy of the current state, then restores the full pre-V3 snapshot.

Do not delete the dated backup until V3 has been used successfully for an appropriate period.
