# TEST-V2 One-Click Launcher Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a safe one-click launcher for the current V2 source build without starting V2 during verification.

**Architecture:** A self-contained batch launcher owns process waiting, build gating, unique config creation, scoped environment, and application launch. A standalone PowerShell integration test invokes its `--verify-only` interface and substitutes a failing `dotnet.cmd` through `PATH` to exercise the failure gate.

**Tech Stack:** Windows CMD, PowerShell test harness, .NET 8 SDK/MSBuild

**Spec:** `docs/superpowers/specs/2026-10-31-test-v2-launcher-design.md`

## Global Constraints

- Do not terminate the installed application.
- Do not write to `%LOCALAPPDATA%\NextMind\Desktop` or its configuration.
- Do not start V2 from automated verification.
- Do not merge, commit, push, publish, or create a release.

## Review Focus

- A failed build with an existing stale executable must never start that executable.
- Repository paths containing spaces must work.
- An already-running installed process must only be waited on, never terminated.
- Every run must select a new config directory below `%TEMP%`.
- Environment variables must remain scoped to the launcher process and its V2 child.

---

### Task 1: Launcher behavior and integration verification

**Files:**
- Create: `TEST-V2.cmd`
- Create: `README-TEST-V2.md`
- Create: `tests/Test-V2-Launcher.Tests.ps1`

**Interfaces:**
- Consumes: `src/NextMind.Desktop.App/NextMind.Desktop.App.csproj` and the fixed `SearchWindow.xaml`
- Produces: `TEST-V2.cmd [--verify-only]`, with exit code 0 only after successful validation/build

- [ ] **Step 1: Write the failing integration test**

Test the real batch entry point. Assert that `--verify-only` builds successfully, creates a unique path under `%TEMP%`, does not create a new NextMind process, and reports verification-only completion. Prepend a fixture `dotnet.cmd` returning failure and assert a non-zero exit, the explicit build-failure message, and no new process.

- [ ] **Step 2: Run the test to verify RED**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tests\Test-V2-Launcher.Tests.ps1`

Expected: FAIL because `TEST-V2.cmd` does not exist.

- [ ] **Step 3: Implement the minimal batch launcher**

Implement process polling without termination commands, fix/source validation, guarded Release build, unique `%TEMP%` config, scoped environment, `start /wait` of the exact build output, and `--verify-only` exit before launch.

- [ ] **Step 4: Add the non-technical README**

Document double-click use, normal Exit routes, isolated data location, the prohibition on toggling Start with Windows, return to the installed executable, and troubleshooting for build failure.

- [ ] **Step 5: Verify GREEN and regressions**

Run the launcher integration test, `dotnet test -c Release`, `dotnet build -c Release --no-restore`, `git diff --check`, and a forbidden-command/path audit. Do not invoke normal launcher mode.

