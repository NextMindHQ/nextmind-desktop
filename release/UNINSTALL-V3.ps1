[CmdletBinding()]
param(
    [string]$InstallRoot,
    [switch]$TestMode,
    [switch]$SkipProcessCheck
)
$ErrorActionPreference = 'Stop'
if ($SkipProcessCheck -and -not $TestMode) { throw '-SkipProcessCheck is allowed only with -TestMode.' }
if (-not $SkipProcessCheck -and (Get-Process -Name NextMindDesktop -ErrorAction SilentlyContinue)) {
    throw 'NextMind Desktop is running. Close it normally from the tray (Exit), then retry.'
}
$resolvedInstallRoot = if ([string]::IsNullOrWhiteSpace($InstallRoot)) { $PSScriptRoot } else { $InstallRoot }
$root = [IO.Path]::GetFullPath($resolvedInstallRoot)
$receipt = Join-Path $root 'install-v3-receipt.json'
if (-not $TestMode -and -not (Test-Path -LiteralPath $receipt)) {
    throw "Refusing to uninstall without an installation receipt: $receipt"
}
$app = Join-Path $root 'app'
if (Test-Path -LiteralPath $app) { Remove-Item -LiteralPath $app -Recurse -Force }
if (-not $TestMode) {
    $shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'NextMind Desktop.lnk'
    if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut -Force }
    Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name NextMindDesktop -ErrorAction SilentlyContinue
    Remove-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NextMindDesktop' -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host "Application files removed. User data was intentionally preserved at: $root"
