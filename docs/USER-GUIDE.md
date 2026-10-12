# NextMind Desktop: user guide

How to use zones, search, menus, the tray icon and the Control Center. For installation, safety and uninstalling, see the [README](../README.md).

## Create and arrange zones

* **Create a zone:** right-click the tray icon → **Create Zone**, or use the Control Center (double-click the tray icon). Give it any name, emoji included.
  A new zone appears in a free spot on the desktop.
* **Place it where you want:** drag the title bar. There is no grid and nothing is arranged for you. While you drag, edges snap to neighbouring zones and to
  the screen edge; hold **Alt** to move without snapping. You may overlap zones on purpose. Positions are saved automatically and come back after a restart.
* **Resize** from the right and bottom edges or the corner grip.
* **Rename, hide or delete:** right-click the title bar (*Rename Zone*, *Hide Zone*, *Delete Zone*). Deleting a zone never deletes your files.

## Collapse and expand

Double-click the title bar, click the ▼ / ▶ arrow, or use the **—** button to collapse a zone to its title; it stays exactly where you left it, also after a restart.
Expanding opens the zone downward — or upward when that would cover other zones or leave the screen. Other zones are never moved, and collapsing a zone
that opened upward puts its title bar back where it was.

![An expanded zone with its items](images/expanded.png)

![Collapsed zones: just the title bar, the magnifier and the close button](images/collapsed.png)

## Add items

Drag files, folders and shortcuts onto a zone from Explorer or the Desktop (see [How it works](../README.md#how-it-works)). Double-click an item to open it; right-click it for
*Open*, *Show in Explorer*, *Move to Zone* and *Remove from Zone* (which only forgets the path).
Drag items inside a zone to reorder them, drag them onto another zone to move them there, or sort a zone from its title-bar menu (*Sort by* name, date added or type).

## Search in a zone

Click the magnifier in the zone's title bar (on a collapsed zone it expands first). Type to filter **that zone's** items as you type; clear the box to see them all again.

![Search in a zone: typing "list" leaves the two matching items](images/search.png)

* Matches the names of files, folders and shortcuts, **ignoring case and diacritics** (`lodz` finds *Łódź*); with several words, every word must match.
* The zone's own order is kept. Searching never changes the zone, your files or their order.
* If nothing matches you see *No matches for …*.

| Key | Action |
|---|---|
| **↓ / ↑** | Select the next / previous result |
| **Enter** | Open the selected result (or the first match) |
| **Esc** | Clear the text; press again to close the search bar |
| **✕** in the field | Clear the text |
| Click the magnifier again | Close the search bar |

While a filter is active, dragging items to reorder them is switched off; dropping something on the zone shows the whole list again first.
Zones are normally not "active" windows, so they never take the keyboard from your applications — the search box takes it only while you are typing and
gives it back when you close the bar, collapse the zone or switch to another window.

## Show desktop (Win+D)

Zones are part of the desktop layer: they sit above the wallpaper and icons but **below every application window**. *Show desktop* (**Win+D**) or minimizing windows
reveals them; opening an application covers them again. They never float over your applications.

## Menus

Right-click a title bar or an item. Menus and the *Sort by* / *Move to Zone* submenus open where they cover the least of neighbouring zones and stay on screen;
they close when you click elsewhere, switch to another application or press Alt+Tab.

## Tray icon and Control Center

The tray icon (right-click) offers **Open NextMind Desktop** (the Control Center), **Show / Hide Zones**, **Create Zone**, **Start with Windows** and **Exit**.
Double-click the icon to open the Control Center: it lists all zones with **Hide / Show** and **Rename** for each, **Create Zone**, the *Start with Windows* checkbox,
**Hide All Zones** / **Show All Zones**, and the current state of the Managed Desktop Moves safety gate. *Exit* closes the app now; it does not change *Start with Windows*.

## Good to know

* Search matches names only (not file contents or parent folders) and works inside one zone at a time; there is no global search yet.
* Very large zones (hundreds to 2000 items) take a few seconds to load at start and use noticeably more memory.
* Managing many zones (for example 30 or more) is not comfortable yet; a zone list with find-and-jump in the Control Center is planned.
* Before you uninstall, read the warning about `ManagedItems` in the [README](../README.md#quick-start--install).
