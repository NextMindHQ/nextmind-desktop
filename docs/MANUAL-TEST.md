# Manual test scenarios

Run these in a throw-away Windows user or VM, or with throw-away files only — never with data you cannot lose.

Start: `powershell -ExecutionPolicy Bypass -File tools\start.ps1` (publishes to `%LOCALAPPDATA%\NextMind\Desktop\app` and starts; the first start enables "Start with Windows").

> Scenarios 6–9 below (search, placement, menus, restart) need no installed copy: run them on the **isolated test instance**
> (`tools\test-ui.ps1`, see [DEVELOPMENT.md](DEVELOPMENT.md)), which never touches your settings or autostart entry.

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

## 6. Search in a zone (v0.2)
Use *Win+D* to see the zones. Seed a zone with 10+ items (`tools\test-ui.ps1 -Demo` or `-Stress`).
1. Click the zone's magnifier, type part of a name: only matching items stay, live, in the zone's own order. Mixed case and Polish letters match (`lodz` finds *Łódź*).
2. Type two words: both must match. Type something that matches nothing: *No matches for …*.
3. **↓ / ↑** move the selection through the results; **Enter** opens the selected result (or the first match).
4. **Esc** clears the text; **Esc** again closes the bar and every item is back. The **✕** in the field clears the text in one click.
5. Click the magnifier on a *collapsed* zone: it expands and the box takes the keyboard. Collapse the zone while the bar is open: the bar closes and everything is visible again.
6. Switch to another application while the bar is open: it does not take the keyboard back; click the box to type again.
7. With a filter active you cannot reorder by dragging; drop a file on the zone: the full list returns and the new item is visible.
8. Large zones (600 and 2000 items, `-Stress`): typing stays smooth.
9. Nothing in the config, the order or on disk changed after searching.

## 7. Placement and expanding (v0.2)
1. A new zone appears in a free spot, not on top of another. Fill the screen: the last one is still visible.
2. Drag a zone close to another: the edges snap (with a small gap); hold **Alt**: no snapping. Drop it deep onto another zone: it stays there (overlap is allowed).
3. Collapse a zone that has another zone directly below it, then expand it: it opens **upward** and does not cover the one below. Collapse it again: the title bar returns to its place.
4. Expand a collapsed zone sitting at the bottom edge: it stays on the screen.

## 8. Menus (v0.2)
1. Right-click a title bar and an item next to a neighbouring zone: the menu does not cover the neighbour when there is room elsewhere.
2. *Sort by* and *Move to Zone*: hover opens the submenu; →, ← and Esc work; the submenu opens on the side that covers the least of other zones and stays on the screen
   (check next to a neighbour, at the right edge, at the bottom edge and with a long zone list, `-ManyZones`).
3. A menu closes when you click elsewhere, switch to another application or press Alt+Tab.

## 9. Restart keeps the layout (v0.2)
1. Collapse a zone near the bottom edge of the screen, move another zone slightly (this forces a save), then restart the test instance on the same config (`-Reuse`).
2. The collapsed zone is exactly where it was; expanding it picks a direction that keeps it on the screen. Repeat the restart: still the same.

## Reset
Tray → untick Start with Windows, Exit, then delete `%LOCALAPPDATA%\NextMind\Desktop` (config, logs, the published app).
**Careful:** if Managed Desktop Moves was ever on, `ManagedItems` inside it holds files that were moved off your Desktop. Return them first (item menu → *Return to Desktop*)
and check that the folder is empty before deleting. With throw-away test files only, this does not matter.
