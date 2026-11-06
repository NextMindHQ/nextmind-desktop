# Portable V3

Extract `NextMindDesktop-3.0.0-Portable-win-x64.zip`, then run `NextMindDesktop.exe`.

This edition includes the .NET runtime, does not register autostart, and does not use the installed edition's configuration. Its data lives at `%LOCALAPPDATA%\NextMind\Desktop-Portable`.

“Portable” describes the application package, not data stored beside the executable. This deliberate choice prevents unsafe writes when the archive is extracted under Desktop or Documents.
