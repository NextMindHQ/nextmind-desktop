# Threat model and data safety

**Overriding goal: no operation may cause the loss of user data.** Safety takes priority over features. Everything below is designed to
fail closed: if an operation cannot be shown to be safe, it is refused with a message — never guessed.

## 1. Two kinds of zone items

| | **Reference** | **Managed** |
|---|---|---|
| What a drop does | stores only the path in the config | **moves** the item from the Desktop into managed storage |
| Source | any path | only an item lying **directly on the user's Desktop** |
| Changes files? | no | yes (one atomic rename on a single volume) |
| Desktop icon disappears? | no | yes |
| Risk | none (a missing target is shown as unavailable) | data loss on a bug or crash → requires the journal |

Classification is by exact parent folder (`DesktopClassifier`), with the Desktop paths asked from Windows through the Known Folder API at the time of
use; a redirected Desktop is honoured and no path is hard-coded.

| An item dropped from… | Result |
|---|---|
| the **user Desktop**, safety gate ON | managed item: assessed, then moved |
| the **user Desktop**, safety gate OFF (**default**) | reference only; the item stays on the Desktop |
| the **Public Desktop** | reference only |
| anywhere else (including a Desktop *subfolder* or the Desktop folder itself) | reference only — never moved |

### The safety gate
`AppConfig.ManagedDesktopMovesEnabled` (in `config.json`) is **`false` by default**, also after config migration. It must be switched on deliberately.
*Return to Desktop* works regardless of the gate, so items can always be taken back.

Things the product **never** does: move from the Public Desktop or from outside the user's Desktop, copy-and-delete across volumes, overwrite, or call a delete
API on user data.

## 2. Managed storage

`%LOCALAPPDATA%\NextMind\Desktop\ManagedItems\<item-id>\<original name>`

* One folder per item, named by the item's stable id: no name collisions in storage; renaming a zone, deleting a zone or moving an item between zones never relocates data.
* The relocation is `MoveFileExW` with **no flags**: a single atomic rename. Without `MOVEFILE_COPY_ALLOWED` it cannot degrade to copy+delete across drives; without
  `MOVEFILE_REPLACE_EXISTING` it cannot overwrite. If the Desktop and storage are on different volumes the move is refused.
* Why AppData rather than a hidden folder on the Desktop: it keeps the Desktop truly empty, is per-user, and (normally) on the same volume. The trade-off is that a tool that
  "cleans AppData" could remove it — which is why *Return to Desktop* exists, why zones holding managed items cannot be deleted without returning them, and why the
  feature is opt-in.
* *Show Managed Location* opens the item's location in Explorer. There is no delete command anywhere in the app.

## 3. What may be moved (`MoveAssessor`)

The assessment is read-only, bounded (20 000 entries / 1.5 s) and runs off the UI thread.

| Verdict | Items |
|---|---|
| **Allow** | `.lnk` / `.url` / `.website`; ordinary files ≤ 50 MB; folders < 256 MB, ≤ 5 000 entries, no project markers, fully scanned |
| **Confirm** (*Move into Zone / Add as Reference / Cancel*, default *Add as Reference*) | program files (`.exe .msi .bat .cmd .com .scr .ps1 .vbs .js .jar`); files > 50 MB; folders ≥ 256 MB or > 5 000 entries; project-like folders (`.git`, `node_modules`, `.venv`, `venv`, `bin`, `obj`, `.vs`, `__pycache__`, `.idea`, `.svn`, `.hg` anywhere inside); folders not fully scanned |
| **Reject** (message; offer *Add as Reference*) | symlinks, junctions and any reparse point (also inside a folder), cloud placeholders (offline / recall attributes), system or hidden items, names ending in `.` or a space, items already inside managed storage, missing items |

Reparse-point detection inside a very large folder is best-effort within the scan budget; a truncated scan always asks.

## 4. Transaction and journal

A write-ahead journal keeps one JSON file per in-flight operation in `%LOCALAPPDATA%\NextMind\Desktop\journal\<op-id>.json`, written durably
(temp file → flush → atomic replace; the previous generation is kept as `.bak`).
Lifecycle: `Prepared → Moving → Moved → ConfigCommitted → (complete: journal removed)`.

**Move in (Desktop → storage)**
1. Write `Prepared`. On failure nothing was touched.
2. Create `ManagedItems\<id>` and write `Moving`. On failure tidy the empty folder and drop the journal.
3. Rename. On failure the source is untouched; tidy and drop the journal.
4. Write `Moved` (non-fatal if this fails).
5. Add the item to the zone and **commit the config**. On failure rename back (journal dropped); if even that is impossible the item stays safely managed in memory and the journal is kept.
6. Write `ConfigCommitted` and remove the journal.

**Return to Desktop (storage → Desktop)** is symmetric. The target name is never overwritten: a taken name gets ` (2)`, ` (3)` … before the extension
(for folders: after the whole name), and a race on the name simply picks the next one. If the config save fails, the item is renamed back into storage and re-listed at its old position.

**Recovery at start** (before zones show items) looks only at what exists on disk:

| Journal | Source | Destination | Action |
|---|---|---|---|
| move-in, Prepared/Moving | exists | missing | nothing moved: discard the journal, tidy the empty folder |
| move-in, Moving/Moved/Committed | any | **exists** | the data is in storage: ensure the config lists it (re-add to its zone, or the first zone, or a new "Recovered" zone), commit, drop the journal |
| move-in | missing | missing | location unknown ⇒ **do nothing**, keep the journal, tell the user |
| move-out | missing | exists on Desktop | the item is back: remove the stale config entry, tidy |
| move-out | exists | missing | it never left storage: keep it managed, drop the journal |
| either | both exist | | leave everything as is and report |
| unreadable journal | | | left untouched and reported |

Recovery is idempotent. If the config cannot be saved during recovery, the journal is kept for the next start.

## 5. Zones, ordering and sorting

* **Delete Zone** with managed items offers only *Return items to Desktop and delete Zone* or *Cancel*. All items are returned first; if any return fails the zone is **kept**. There is no option that deletes files; removing a reference never touches its source.
* Renaming a zone never touches data. Moving an item between zones (menu or drag) changes membership only.
* **Sorting** (per zone): Custom, Name A→Z / Z→A, Date Added newest / oldest, Type. The stored list is always the custom order; sorting only changes the display. Dragging in a sorted zone switches it to Custom and keeps the arrangement the user just made.

## 6. Edge cases covered by tests

Duplicate names (` (2)`, case-insensitive), read-only items (attributes preserved), locked files (message, no change), folders and nested folders,
reparse points (refused for managed, allowed as references), missing items (shown as unavailable; **never auto-removed**), Unicode and emoji in names,
long paths (> 260 characters, `longPathAware`), a Desktop path with spaces or non-ASCII characters, a damaged config (kept aside, previous generation used),
a config from a newer version (not overwritten), and injected failures at the main steps of the move, return and recovery paths.

## 7. Threat model (local application)

| Threat | Description | Mitigation |
|---|---|---|
| Data loss | crash or bug during a move; a `Move` drop effect | write-ahead journal and recovery, same-volume atomic rename only, no delete API on user data (static test), drop effect `Link`, fault-injection tests |
| Explorer deletes the source | the drop target answers `Move` and the source app deletes the original | drops always answer `Link`/`Copy` |
| Malicious or odd names | `..`, alternate data streams, reserved device names, trailing dots/spaces | only file names are used, special names are rejected, full paths are validated against the allowed root |
| Config tampering | a hand-edited config pointing a managed item outside storage | a managed item's location is derived from its id and stored name and validated against the storage root; the managed filesystem is scoped to storage + journal + the Desktop only |
| Only one class may rename | accidental use of the rename elsewhere | a static test requires that only `ManagedItemService` calls it |
| Launching items | *Open* calls the Shell on a user path | `UseShellExecute` with the path as data; no commands are composed from text |
| Privacy leakage | telemetry or network calls | no networking code and no telemetry; the only external processes started are the Shell and Explorer for opening items |
| Privilege escalation | needing administrator rights | none: HKCU, `%LOCALAPPDATA%`, the user's Desktop |
| Interfering with Explorer | injection or hooks | no injection, no shell extensions; a separate process |
| Two instances | two processes writing the same config | single-instance mutex and atomic writes |

The log (`%LOCALAPPDATA%\NextMind\Desktop\logs\desktop.log`) records operations and file *names* of dropped items, never file contents.

## 8. Out of scope for V0.1

Cross-volume moves, tracking files by file id, virtual Shell items, virtual desktops, multi-level undo, dragging a managed item *out* to the Desktop
(use *Return to Desktop*).

## 9. Testing rules

* Tests obtain a filesystem only from `TempSandbox` (a guarded temp directory). `GuardedFileSystem` refuses paths outside its root and any path under the real Desktop, Public Desktop, Documents, Pictures or OneDrive, so a misconfigured test fails loudly instead of touching real data.
* Destructive operations against a real Desktop are never part of any automated test.

### Development-only fake Desktop
For manual end-to-end UI testing without touching a real Desktop:

* `NEXTMIND_DESKTOP_CONFIG_DIR=<temp folder>` redirects the config, journal and logs.
* `NEXTMIND_FAKE_USER_DESKTOP=<temp folder>` makes a throw-away folder play the Desktop — accepted only when it lies outside every real user folder.
* `NEXTMIND_ENABLE_MANAGED_MOVES=1` then enables the gate **only for that fake Desktop**.
