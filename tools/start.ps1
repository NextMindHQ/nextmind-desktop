# Publishes NextMind Desktop to a stable per-user folder and starts it from there.
#   %LOCALAPPDATA%\NextMind\Desktop\app\NextMindDesktop.exe
# A stable location matters because "Start with Windows" registers the path of the running executable;
# running from bin\ would break the autostart entry on the next rebuild/clean.
# Uninstall = stop the app, untick "Start with Windows" in the tray, delete %LOCALAPPDATA%\NextMind\Desktop.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$appDir = Join-Path $env:LOCALAPPDATA 'NextMind\Desktop\app'
$exe = Join-Path $appDir 'NextMindDesktop.exe'

Push-Location $root
try {
    $running = Get-Process NextMindDesktop -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host 'Stopping the running instance (its config is saved 400 ms after every change)...'
        $running | Stop-Process
        $running | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
    }

    Write-Host "Publishing to $appDir ..."
    dotnet publish src\NextMind.Desktop.App -c Release --no-self-contained -o $appDir --nologo -v q | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

    Start-Process $exe
    Write-Host "Started: $exe"
}
finally { Pop-Location }
