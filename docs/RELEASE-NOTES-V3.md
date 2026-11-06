# NextMind Desktop 3.0.0 release notes

V3 upgrades the existing desktop in place while preserving configuration, profiles, zones, widget content, geometry, managed items and the user's autostart preference. The installation path remains `%LOCALAPPDATA%\NextMind\Desktop\app`.

The installer does not terminate NextMind Desktop. If any instance is running, it stops before changing files and asks the user to close the application normally.

The Portable package is self-contained for Windows x64. It stores its isolated configuration in `%LOCALAPPDATA%\NextMind\Desktop-Portable` and never adds itself to autostart.
