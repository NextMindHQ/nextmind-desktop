[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Backup,
    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'NextMind\Desktop'),
    [switch]$TestMode,
    [switch]$SkipProcessCheck
)
$ErrorActionPreference = 'Stop'
if ($SkipProcessCheck -and -not $TestMode) { throw '-SkipProcessCheck is allowed only with -TestMode.' }
if (-not $SkipProcessCheck -and (Get-Process -Name NextMindDesktop -ErrorAction SilentlyContinue)) {
    throw 'NextMind Desktop is running. Close it normally from the tray (Exit), then retry rollback.'
}
$snapshot = Join-Path $Backup 'Desktop'
if (-not (Test-Path -LiteralPath $snapshot)) { throw "Invalid backup: $Backup" }
$safety = "$InstallRoot.before-rollback-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
if (Test-Path -LiteralPath $InstallRoot) { Move-Item -LiteralPath $InstallRoot -Destination $safety }
Copy-Item -LiteralPath $snapshot -Destination $InstallRoot -Recurse -Force
Write-Host "Rollback restored from: $Backup"
Write-Host "Pre-rollback safety copy: $safety"
