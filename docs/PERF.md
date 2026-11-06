# M1 performance check

Machine: MSI Katana GF76 (i7-11800H, 16 logical cores, 16 GB), Windows 11 build 26300. Release build, framework-dependent
(.NET 8.0 Desktop Runtime), primary display at 100 % scale. Measured 2026-10-03 with `perf.ps1`-style probes
(idle = no interaction; CPU from `Process.TotalProcessorTime` deltas over the window).

| Scenario | Cold start* | CPU idle | Working set | Private bytes | Threads / handles |
|---|---|---|---|---|---|
| 1 zone | 0.68 s | 0 ms over 60 s (0 %) | 98 MB | 42 MB | 8 / 366 |
| 5 zones | 0.53 s | 0 ms over 45 s (0 %) | 104 MB | 46 MB | 11 / 376 |

\* process launch → first zone window visible, measured from outside. In-app log figure ("first zone rendered") 0.4–0.6 s warm,
1.2 s on the first run after a build (JIT/disk cache). Debug build: 0.5–1.2 s.

Notes
* The working set includes shared pages of the WPF framework; private bytes (~42–46 MB) are the app's own cost.
  Each extra zone costs ≈1 MB. Nothing grows while idle.
* Idle CPU is exactly 0 because there are no timers or polling: persistence uses a single re-armed one-shot timer,
  system events arrive through window messages, and no animation runs.
* Not measured: GPU, battery impact, behaviour with many items/icons (M2+).
* Revisit trigger from ADR-1: private bytes > ~80 MB with one zone, or idle CPU > 0.1 %. Neither is close.

## Evolution V2 measurement status — 2026-10-31

Runtime before/after numbers are **NOT TESTED** in this session. The machine already had the installed NextMind Desktop running from `%LOCALAPPDATA%\NextMind\Desktop\app`, so the intentional global single-instance mutex rejected the V2 artifact before startup. The existing process was not terminated because it belongs to the user.

What was measured:

| Check | Result |
|---|---|
| Release compilation | PASS, 0 warnings / 0 errors |
| Automated suite | PASS, 297 tests in about 1 second |
| Published artifact | framework-dependent win-x64, ZIP 6,923,206 bytes |

Architectural performance notes:

* Search builds an in-memory catalog only when the search window opens; it does not index the disk or run a background service.
* Layout calculations are pure and execute only after a move or explicit auto-arrange.
* Persistence remains a re-armed one-shot timer; no idle polling was introduced.
* The clock uses one manager-level, minute-aligned one-shot timer and stops it when no visible clock exists.
* CPU/RAM monitoring was intentionally not implemented because periodic sampling would violate the no-polling constraint.

For a valid comparison, close the installed instance, launch the V2 artifact with a throw-away config, and repeat the original `PERF.md` protocol for: one zone; five zones; five zones + clock + note; 20 repeated profile switches. Record cold start, 60-second idle CPU, working set, private bytes, thread/handle counts and any growth.
