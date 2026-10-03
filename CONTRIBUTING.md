# Contributing to NextMind Desktop

Thanks for your interest! NextMind Desktop moves and organizes people's real files, so the most important contribution rule is
about **filesystem safety** (below). Please read it first.

## Requirements

* Windows 10 2004+ or Windows 11, x64 (the app and most tests use Windows APIs)
* .NET 8 SDK (`global.json` pins an 8.0.4xx SDK; newer 8.0 feature bands are accepted)
* Git; any editor (Visual Studio 2022, Rider or VS Code)

## Clone, build, test

```powershell
git clone https://github.com/NextMindHQ/nextmind-desktop.git
cd nextmind-desktop
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

Product projects build with **warnings as errors**; the build must stay at 0 warnings and 0 errors, and all tests must pass.

## Filesystem safety rules (hard rules)

1. **Never run destructive operations against your real Desktop, Documents, Pictures or OneDrive** — not in tests, not while debugging, not "just once".
2. **Filesystem tests must use temp or fake roots.** Obtain a filesystem only from `TempSandbox` (`tests/NextMind.Desktop.Tests/Support`). `GuardedFileSystem` refuses to operate outside its root or under protected user folders and fails the test loudly.
3. **Product code must not delete user data.** The only delete in `src/` is the app's own temporary config file. `NoDestructiveOperationsTests` fails if another delete call appears. If you believe a change needs one, open an issue first.
4. **Drag & drop must report `Link` or `Copy`, never `Move`** as the drop effect — otherwise Explorer deletes the source itself.
5. **Resolve folders through the Known Folder APIs;** never hard-code `C:\Users\<name>\Desktop`.
6. **Fail closed:** if an operation cannot be shown safe, refuse it with a clear message. Never overwrite silently; resolve name collisions by renaming.
7. To exercise the UI end-to-end without touching your real Desktop, point `NEXTMIND_DESKTOP_CONFIG_DIR` at a temp folder and set `NEXTMIND_FAKE_USER_DESKTOP` to a throw-away folder outside any real user folder (see [docs/THREAT-AND-DATA-SAFETY.md](docs/THREAT-AND-DATA-SAFETY.md), "Development-only fake Desktop").

## Architecture

* `Core` has no Windows API. `Shell` is the only place with P/Invoke. `App` (WPF) holds no policy.
* Anything relying on undocumented Windows behaviour must be marked as such in code and have a fallback.
* No polling and no cyclic timers. No network access, no telemetry, no shell extensions, no code injection into Explorer.

## Style

* Follow `.editorconfig`; C# 12, nullable reference types on, file-scoped namespaces.
* Match the surrounding code: naming, comment density, idiom. Comment the *why*, not the *what*.
* Keep changes focused; don't reformat unrelated code.

## Pull request workflow

1. Open an issue first for anything non-trivial (especially anything touching filesystem operations).
2. Fork, create a topic branch, make small focused commits.
3. Add or update tests; run `dotnet build -c Release` and `dotnet test -c Release` locally.
4. Open a PR using the template. CI (Windows build and tests) must pass.
5. A maintainer reviews; changes that touch moves, journal, recovery or deletion get extra scrutiny.

## Reporting bugs and security issues

Bugs: use the issue templates, and **remove private data** (paths, file names, usernames) from logs and screenshots.
Vulnerabilities: do **not** open a public issue — see [SECURITY.md](SECURITY.md).
