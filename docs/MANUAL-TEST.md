# Manual test scenarios

Run these in a throw-away Windows user or VM, or with throw-away files only — never with data you cannot lose.

Start: `powershell -ExecutionPolicy Bypass -File tools\start.ps1` (publishes to `%LOCALAPPDATA%\NextMind\Desktop\app` and starts; the first start enables "Start with Windows").

## 1. Title bar
1. Expanded: `🦈 NextMind ▼`, and `—  ×` at the right edge.
2. Click the ▼ (or double-click the title): collapses to `🦈 NextMind ▶` — the triangle is in the same place, only its direction changes.
3. Expand, drag the bottom-right grip much wider/narrower: the triangle never moves away from the name.
4. `—` collapses. `×` hides only that zone (no taskbar button, nothing in Alt+Tab).
5. Tray right-click → **Show / Hide Zones** brings hidden zones back.

## 2. Items (references only)
1. Drag a folder, a file, a `.lnk` and a `.exe` from Explorer/Desktop onto a zone. The cursor shows a *link* arrow. The originals stay where they were
   (the Desktop icon is still on the Desktop — this version never moves files).
2. Real Windows icons + names appear in a grid; make the zone wider and the number of columns grows.
3. Double-click: folder → Explorer, file → its app, shortcut → runs, program → starts.
4. Right-click an item → Open / Show in Explorer / **Remove from Zone** (the file is untouched, not in the Recycle Bin).
5. Drop the same item twice: it is selected, not duplicated.
6. Tray → Exit, start again: the zones, positions, sizes, collapsed state and items are back.
7. (Optional) Rename one referenced file in Explorer, restart the app: that item is shown dimmed with `?` (no crash); rename it back and double-click it: it opens again.

## 3. Zones
1. Tray → **Create Zone**: type a name (emoji works: Win + .  → 🎮), Create. Repeat for several zones.
2. Right-click a title bar → **Rename Zone**, **Hide Zone**, **Delete Zone**.
3. Delete Zone asks `Delete zone "…"? Files and folders will NOT be deleted.` Enter = Cancel. After Delete only the zone is gone; check the files.

## 4. Start with Windows — without restarting Windows
Tray menu shows `✓ Start with Windows` (ticked by default after the first run).
```powershell
powershell -ExecutionPolicy Bypass -File tools\autostart.ps1 status     # ON + the registered command + whether the target exists
```
1. Untick it in the tray → run `status` again: OFF. Tick it → ON again (no restart needed).
2. **Exit** in the tray, then `tools\autostart.ps1 status` → still ON (Exit does not disable autostart).
3. Simulate the logon start (second instance must not appear):
```powershell
powershell -ExecutionPolicy Bypass -File tools\autostart.ps1 simulate   # runs the registered command exactly like Windows; expects exactly 1 process after
```
   With the app closed this starts it with all zones; with it running, it just shows the zones and exits.
4. Break it on purpose (optional): `reg add HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v NextMindDesktop /d "C:\nope\x.exe" /f`, then start the app:
   the entry is repaired to the running exe (`status`).
5. Full test: next time you sign in / restart — the zones should appear by themselves.

## 5. Regression checks
Zone stays under normal windows and over the icons; "Show desktop" keeps it visible; Explorer restart (Task Manager → Restart) recovers it and the tray icon.

## Reset
Tray → untick Start with Windows, Exit, delete `%LOCALAPPDATA%\NextMind\Desktop` (config, logs, the published app).
