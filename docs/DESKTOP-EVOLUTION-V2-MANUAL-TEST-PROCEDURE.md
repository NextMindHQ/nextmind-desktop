# NextMind Desktop Evolution V2 — isolated manual test procedure

Status: prepared 2026-10-31. This procedure does not replace the installed application, does not use its live configuration, and does not change its autostart entry.

## Safety boundary

- Do not run `tools/start.ps1`: it publishes into `%LOCALAPPDATA%\NextMind\Desktop\app` and would replace the installed test target.
- Do not point `NEXTMIND_DESKTOP_CONFIG_DIR` at `%LOCALAPPDATA%\NextMind\Desktop`.
- Do not use Task Manager **End task**, `Stop-Process`, or `taskkill`. Exit the running application through its own UI so pending configuration is flushed.
- Keep `NEXTMIND_ENABLE_MANAGED_MOVES` unset. Do not test managed moves against the real Desktop.
- Use only disposable files for drag/drop tests. Adding a normal item is a reference, but the test should still avoid private or irreplaceable data.
- The installed and V2 builds cannot run together: both intentionally use `Local\NextMind.Desktop.SingleInstance.v1`.

## Readiness gate

Manual testing may start when all of the following are true:

1. No zone/widget edit or drag is in progress.
2. The current application has been closed with **tray icon → Exit** or **Control Center → Exit NextMind Desktop**.
3. This read-only check returns no process:

   ```powershell
   Get-Process -Name NextMindDesktop -ErrorAction SilentlyContinue
   ```

4. `NEXTMIND_DESKTOP_CONFIG_DIR`, `NEXTMIND_FAKE_USER_DESKTOP`, and `NEXTMIND_ENABLE_MANAGED_MOVES` are not already set in the test shell.
5. At least 30 minutes are available for the functional pass; allow another 15 minutes for the performance samples.

The normal Exit path closes the search/control windows, flushes zone/profile/widget state, disposes the tray and releases the mutex. Wait a few seconds after Exit and use the read-only process check above. If the process remains, stop and investigate; do not force it closed as part of this procedure.

## Fresh isolated V2 run

Open a new PowerShell window and run:

```powershell
$repo = 'C:\Users\zoltk\Documents\ChatGPT\NextMind Desktop'
$testRoot = Join-Path $env:TEMP ("NextMindDesktop-V2-Manual-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $testRoot | Out-Null

Remove-Item Env:NEXTMIND_FAKE_USER_DESKTOP -ErrorAction SilentlyContinue
Remove-Item Env:NEXTMIND_ENABLE_MANAGED_MOVES -ErrorAction SilentlyContinue
$env:NEXTMIND_DESKTOP_CONFIG_DIR = $testRoot

$v2Exe = Join-Path $repo 'src\NextMind.Desktop.App\bin\Release\net8.0-windows10.0.19041.0\NextMindDesktop.exe'
Start-Process -FilePath $v2Exe
```

Expected evidence:

- Test data appears only under `$testRoot` (`config.json`, `config.json.bak`, `logs`, and any journal data).
- The installed directory `%LOCALAPPDATA%\NextMind\Desktop` is unchanged.
- The redirected run logs that autostart is not touched automatically.
- The real Desktop is reported only as a known folder; managed moves remain disabled.

Keep `$testRoot` after the test until the results have been reviewed. It is the disposable evidence set.

## Safe migration test

Migration must use a copy, never the live config directory. Perform this after the installed process has exited normally.

```powershell
$liveRoot = Join-Path $env:LOCALAPPDATA 'NextMind\Desktop'
$migrationRoot = Join-Path $env:TEMP ("NextMindDesktop-V2-Migration-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $migrationRoot | Out-Null

$liveHashBefore = Get-FileHash -Algorithm SHA256 (Join-Path $liveRoot 'config.json')
Copy-Item -LiteralPath (Join-Path $liveRoot 'config.json') -Destination $migrationRoot
if (Test-Path -LiteralPath (Join-Path $liveRoot 'config.json.bak')) {
    Copy-Item -LiteralPath (Join-Path $liveRoot 'config.json.bak') -Destination $migrationRoot
}

$env:NEXTMIND_DESKTOP_CONFIG_DIR = $migrationRoot
Start-Process -FilePath $v2Exe
```

Verify the copied configuration opens, V3 data becomes schema V4 with a `Default` profile, zones/items remain present, and V2 state persists after a normal V2 exit and restart. Then verify the live file was not changed:

```powershell
$liveHashAfter = Get-FileHash -Algorithm SHA256 (Join-Path $liveRoot 'config.json')
$liveHashBefore.Hash -eq $liveHashAfter.Hash
```

Expected result: `True`. Retain both hash objects and the copied migration directory as evidence.

## Functional checklist

### UI and layout

- [ ] Tray icon and Control Center open; zones remain below normal windows and above Desktop icons.
- [ ] Create, rename, hide, show, collapse, expand, move, resize, and delete a zone; deletion confirmation is clear and references are not deleted.
- [ ] Snap-to-zone activates near an edge and respects the configured gap.
- [ ] Disabling snap leaves the zone at the released coordinates.
- [ ] Grid snap aligns final X/Y to the selected grid size.
- [ ] Overlap remains allowed and shows only the amber warning.
- [ ] Undo Move restores exactly the latest title-bar move and then disables.
- [ ] Auto-arrange packs visible zones without resizing or leaving a monitor work area.
- [ ] If a second monitor/different DPI is available: restart, disconnect/reconnect, and confirm zones/widgets clamp into a remaining work area.

### Workspace profiles

- [ ] Create `Work`; changes made there do not alter `Default` after switching back.
- [ ] Rename a profile and verify the name survives restart.
- [ ] Duplicate a profile; referenced items and widgets are independent copies.
- [ ] A managed item is not duplicated into a second profile owner (use only the separate fake-Desktop procedure if this case must be exercised).
- [ ] Delete a non-active profile after confirmation.
- [ ] Deleting the last profile is refused.
- [ ] Deleting a profile that owns managed items is refused.
- [ ] Active profile, layouts, names, item references, and widget state survive a normal exit/restart.

### Search

- [ ] Open from Control Center and with `Ctrl+K`.
- [ ] Results update while typing; zone names, commands, widgets, and Start-menu applications are found.
- [ ] Up/Down changes selection; Enter executes once; Escape executes nothing.
- [ ] An impossible query shows **No results**.
- [ ] Accent-insensitive query works, e.g. `zazolc` matches `Zażółć`.
- [ ] Double-click directly on a result executes that result exactly once.
- [ ] With a result selected, double-click the empty area below the result rows: nothing executes and the window remains open.

### Widgets

- [ ] Add Clock & Date and Quick Note from Control Center and search.
- [ ] Move, resize, collapse, expand, hide, and show each widget.
- [ ] Quick Note preserves Unicode, multiple lines, an empty note, and text longer than 80 characters.
- [ ] Hiding/showing a note preserves its text.
- [ ] Profile switches restore profile-specific widget state.
- [ ] Normal exit/restart preserves geometry, collapsed/hidden state, and note contents.
- [ ] Clock changes at the next minute boundary without continuous visual updates.

### Configuration and migration

- [ ] Fresh isolated run creates schema V4 data only in `$testRoot`.
- [ ] Migration copy creates `Default` and preserves every V3 zone/item.
- [ ] Live config SHA-256 is identical before and after the migration test.
- [ ] Corrupt only a copied test `config.json`; `.bak`/default recovery works and the bad file is retained as `config.corrupt-*.json`.
- [ ] Make only the disposable test directory read-only; the app remains running and does not damage the previous generation.
- [ ] The installed autostart registry entry is unchanged.

## Performance checklist

Use a separate fresh test root for each scenario: one zone; five zones; five zones plus Clock & Date plus Quick Note; 20 profile switches.

- [ ] Record the `Cold start: first zone rendered ... ms` line from the scenario log.
- [ ] Immediately after startup and after 60 seconds idle, record:

  ```powershell
  Get-Process -Name NextMindDesktop | Select-Object Id,CPU,WorkingSet64,PrivateMemorySize64,@{Name='ThreadCount';Expression={$_.Threads.Count}},HandleCount
  ```

- [ ] Confirm the CPU value does not grow materially during the 60-second idle window.
- [ ] Confirm working/private memory does not grow continuously after repeated profile switches.
- [ ] Record search-open latency subjectively and confirm typing remains responsive with the real Start-menu catalog.
- [ ] Record machine, Windows build, display count/scaling, and whether this was a cold or warm filesystem cache.

## End the V2 session and restore the installed build

1. Exit V2 through the tray or Control Center; do not force-terminate it.
2. Confirm the process is gone with `Get-Process`.
3. In the test shell, clear only the test variables:

   ```powershell
   Remove-Item Env:NEXTMIND_DESKTOP_CONFIG_DIR -ErrorAction SilentlyContinue
   Remove-Item Env:NEXTMIND_FAKE_USER_DESKTOP -ErrorAction SilentlyContinue
   Remove-Item Env:NEXTMIND_ENABLE_MANAGED_MOVES -ErrorAction SilentlyContinue
   ```

4. Start the already installed executable normally:

   ```powershell
   Start-Process -FilePath (Join-Path $env:LOCALAPPDATA 'NextMind\Desktop\app\NextMindDesktop.exe')
   ```

5. Confirm the original zones/configuration return. Do not delete the test roots until the test record has been accepted.

## Optional managed-move test (separate approval)

This is deliberately outside the normal pass. It requires a disposable fake Desktop outside Desktop/Documents/Pictures/OneDrive, plus all three environment variables. It must never point at the real Desktop. Run it only after explicit approval and with disposable files.
