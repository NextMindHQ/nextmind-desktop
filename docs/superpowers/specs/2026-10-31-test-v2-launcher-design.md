# TEST-V2 one-click launcher — design

## Intent

Provide a non-technical tester with one file to double-click. The launcher must run the current V2 source build with the search double-click fix while isolating all test configuration from the installed NextMind Desktop.

## Selected approach

A self-contained Windows batch file, `TEST-V2.cmd`, is the selected approach. It uses only Windows CMD facilities plus the repository's existing .NET SDK dependency. A PowerShell companion was rejected because the user does not want to enter PowerShell commands; a prebuilt artifact was rejected because it could silently become stale.

## Behavior

- In normal mode, detect `NextMindDesktop.exe`. If present, explain the two normal Exit routes and poll until it exits; never terminate it.
- Verify that the source tree contains the search-result item double-click fix.
- Run `dotnet build` for the App project in Release. A non-zero exit stops the launcher before any executable is started, even if an old binary exists.
- Create a unique directory below `%TEMP%\NextMindDesktop-V2-Test` for every invocation.
- Use `setlocal` and set `NEXTMIND_DESKTOP_CONFIG_DIR` only for the child V2 process. Explicitly clear the fake-Desktop and managed-move variables.
- Run the build output from `src\NextMind.Desktop.App\bin\Release\net8.0-windows10.0.19041.0`, never the old ZIP and never `%LOCALAPPDATA%\NextMind\Desktop\app`.
- Wait for V2 to exit and display the installed executable path for returning to the normal version.
- Support `--verify-only`: perform source/build/path/isolation validation, create the disposable directory, and exit before starting V2. This is the non-invasive verification interface.

## Safety constraints

No merge, commit, push, release, process termination, installation overwrite, live-config write, registry write, managed move, or automatic relaunch of the installed version. A redirected config causes application startup to skip automatic autostart registration. The README warns the tester not to toggle the Start with Windows option manually during V2 testing.

