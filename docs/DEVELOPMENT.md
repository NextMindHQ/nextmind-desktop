# Development guide: running a safe test instance

This guide is for contributors. It explains how to run and exercise NextMind Desktop **without touching an installed copy**, its settings,
its autostart entry or your real desktop files. Users do not need any of this; see the [README](../README.md).

## Why a separate test instance

NextMind Desktop is a single-instance tray application that manages real files and writes to a per-user "Start with Windows" registry entry.
A development build started the normal way would compete with an installed copy for the single-instance lock, signal it, and could rewrite its autostart
entry. The test instance avoids all of that.

> **Do not use `tools\start.ps1` or `tools\stop.ps1` for testing.** They publish to and stop the *installed* copy (and every `NextMindDesktop` process).
> They are for running your own build as your installed app, not for experiments.

## `tools\test-ui.ps1`

```powershell
powershell -ExecutionPolicy Bypass -File tools\test-ui.ps1 -DryRun   # prepare and verify, start nothing
powershell -ExecutionPolicy Bypass -File tools\test-ui.ps1           # start the isolated test instance
powershell -ExecutionPolicy Bypass -File tools\test-ui.ps1 -Stop     # stop ONLY the test instance
```

| Option | What it does |
|---|---|
| *(none)* | Builds the app (Release, into `bin\` only), seeds a fresh config under `%TEMP%\NextMindDesktop-UITest\run-<timestamp>` with synthetic zones and dummy files, starts the test exe. |
| `-DryRun` | Does everything except starting the app (builds, seeds, checks). |
| `-Stop` | Stops only the process the script started (recorded in its state file), then re-checks that an installed copy was not changed. |
| `-Reuse` | Restarts on the **previous** test config instead of a fresh seed. Use it to check *save, restart, save* behaviour: the app re-saves its own file, the restart must keep the layout. |
| `-Stress` | Adds two big zones (600 and 2000 dummy items) for performance and filtering checks. |
| `-ManyZones` | Adds 36 small collapsed zones, so that menus listing zones (e.g. "Move to Zone") get very long. An optional load scenario, never the default layout; do not combine with `-Stress`. |
| `-Demo` | A tidy, fully synthetic layout (neutral names, empty sample files and folders, shortcuts to Windows' own tools only) used for documentation screenshots. |
| `-Exe <path>` | Use a specific test exe. Refused if it lies inside an installed copy's folder. |

The test zones are titled `TEST …` and the tray icon tooltip shows `NextMind Desktop (TEST)`.

## What is isolated, and how

The script sets `NEXTMIND_DESKTOP_CONFIG_DIR` in the *child* process only. When the app sees that variable it runs in **development mode**:

* **Own config, log, journal and managed storage** under the redirected folder; the script refuses a folder outside `%TEMP%` or one that overlaps an installed copy's data.
* **Own single-instance lock and activate message** (a `.dev` variant). A test instance cannot be blocked by, or signal, an installed copy.
* **No autostart handling at all:** the registry is neither read nor written, and the tray / Control Center autostart entries are disabled.
* **Managed Desktop Moves stay off** when the real Desktop is in use, whatever the redirected config says. Test items are *references* to dummy files under `%TEMP%`.
* Extra tracing of submenu placement is logged only in development mode; an installed copy logs warnings only.

Before and after, the script compares the installed copy with a baseline (process id and start time found by executable path, the autostart value and the
settings file) and **stops the test instance and aborts** if isolation cannot be guaranteed. It also refuses an executable that is older than the sources:
the test project does not reference the App project, so a green `dotnet test` says nothing about the App binaries, and a stale exe would not contain
the isolation code.

If you drive the app from your own scripts, keep to the same rules: new empty config folder, no real files, stop only the process you started.

## Logs

`%TEMP%\NextMindDesktop-UITest\run-<timestamp>\config\logs\desktop.log` of the current run. Log lines hold class names and counts, not window titles.
Useful lines: `Development run (config redirected): autostart is disabled …`, `Managed Desktop Moves: DISABLED`, `Cold start: first zone rendered … ms`,
and (development mode only) `Submenu '…': candidate #… requested … actual … delta=(0,0); stable=True`.

## Manual UI testing tips

* Zones live **under** normal windows. Press **Win+D** (or minimize windows) to see them.
* Programmatic input (UI Automation, injected keys) can take the keyboard focus for a moment because the search box has to activate its zone.
  Do not run such checks while you work in other applications; keystrokes can end up in the test window.
* A "ghost" `(TEST)` tray icon may stay until the mouse passes over it after `-Stop`; the clean exit is tray → Exit on the `(TEST)` icon.
* WPF windows ignore messages posted to them from other processes; use real clicks for interactive checks.

## Screenshots

Documentation screenshots come from `-Demo`: capture the zone **windows only** (for example with `PrintWindow`), never the desktop, and compose them on a
neutral generated backdrop. See [SCREENSHOT-PLAN.md](SCREENSHOT-PLAN.md).

## Build and test

```powershell
dotnet build -c Release        # 0 warnings, 0 errors (warnings are errors for the product projects)
dotnet test -c Release         # all tests must pass
```

Build the whole solution (not only the test project) before using the test instance, or let `tools\test-ui.ps1` build the App for you.
