$ErrorActionPreference = 'Stop'
if (Get-Process -Name NextMindDesktop -ErrorAction SilentlyContinue) {
    throw 'NextMind Desktop is running. Close it normally from the tray (Exit), then retry.'
}
$root = Join-Path $env:LOCALAPPDATA 'NextMind\Desktop'
$app = Join-Path $root 'app'
if (Test-Path -LiteralPath $app) { Remove-Item -LiteralPath $app -Recurse -Force }
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'NextMind Desktop.lnk'
if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut -Force }
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name NextMindDesktop -ErrorAction SilentlyContinue
Remove-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NextMindDesktop' -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Application files removed. User data was intentionally preserved at: $root"
