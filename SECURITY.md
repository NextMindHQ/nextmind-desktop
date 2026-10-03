# Security Policy

## Supported versions

| Version | Supported |
|---|---|
| V0.1.x | Yes |

Earlier or pre-release builds are not supported.

## Reporting a vulnerability

Please **do not open a public issue** for a security problem.

Report it privately through GitHub: on the repository's **Security** tab choose **Report a vulnerability**
(GitHub private vulnerability reporting). Include:

* what you found and its impact (for example: possible data loss, unintended file move or overwrite, path-confinement bypass, privilege issue);
* the NextMind Desktop version and Windows version;
* minimal steps to reproduce, ideally using throw-away test files.

We will acknowledge the report as soon as we can and keep you updated while it is investigated. This is a small open-source
project, so we cannot promise fixed response times.

## Please do not publish private data

When reporting anything — in a private report or a normal issue — remove personal information first: usernames, full local
paths, file and folder names from your real Desktop, and screenshots of your real desktop. The app's log can contain names of
files you dropped onto a zone.

## Filesystem and Shell bugs: take extra care

NextMind Desktop can move files between your Desktop and its managed storage. If you think you hit a bug that moved, renamed or
lost a file:

* **Stop using the managed-items feature** and do not retry on real data.
* Do not delete `%LOCALAPPDATA%\NextMind\Desktop` — it contains the config, the journal and managed storage that recovery uses.
* Reproduce with throw-away files (for example shortcuts to `notepad.exe`), never with files you cannot afford to lose.
* Report the steps and, if possible, the relevant part of `logs\desktop.log` with private data removed.

Managed Desktop moves are disabled by default (`ManagedDesktopMovesEnabled` in `config.json`).

## Scope

In scope: the application code in this repository, its filesystem safety guarantees, its Shell integration, and its helper scripts.
Out of scope: vulnerabilities in Windows, .NET or other third-party software (report those to their vendors).
