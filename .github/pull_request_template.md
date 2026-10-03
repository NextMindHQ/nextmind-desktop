## What and why

<!-- Short description of the change and the motivation. Link the related issue. -->

## Checklist

- [ ] `dotnet build -c Release` has 0 warnings / 0 errors
- [ ] `dotnet test -c Release` passes
- [ ] Tests added or updated
- [ ] No test touches a real Desktop, Documents, Pictures or OneDrive (filesystem tests use `TempSandbox`)
- [ ] No new delete call on user data; drops still report `Link`/`Copy`, never `Move`
- [ ] No private data (usernames, local paths, file names) in code, docs, logs or screenshots
- [ ] No new network access or telemetry
