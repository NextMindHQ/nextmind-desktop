[CmdletBinding()]
param(
    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'NextMind\Desktop'),
    [string]$Payload = (Join-Path $PSScriptRoot 'payload'),
    [string]$BackupRoot = (Join-Path $env:LOCALAPPDATA 'NextMind\Desktop-Backups'),
    [switch]$TestMode,
    [switch]$SkipProcessCheck
)

$ErrorActionPreference = 'Stop'
if ($SkipProcessCheck -and -not $TestMode) { throw '-SkipProcessCheck is allowed only with -TestMode.' }
if (-not $SkipProcessCheck -and (Get-Process -Name NextMindDesktop -ErrorAction SilentlyContinue)) {
    throw 'NextMind Desktop is running. Close it normally from the tray (Exit), then run INSTALL-V3.cmd again. No process was stopped.'
}

$manifestPath = Join-Path $PSScriptRoot 'RELEASE-MANIFEST.json'
if (-not (Test-Path -LiteralPath $manifestPath)) { throw "Missing release manifest: $manifestPath" }
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
if ($manifest.Version -ne '3.0.0') { throw "Unexpected package version: $($manifest.Version)" }
if (-not (Test-Path -LiteralPath (Join-Path $Payload 'NextMindDesktop.exe'))) { throw 'Payload is incomplete.' }

foreach ($entry in $manifest.PayloadFiles) {
    $file = Join-Path $Payload $entry.Relative
    if (-not (Test-Path -LiteralPath $file)) { throw "Payload file missing: $($entry.Relative)" }
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $file).Hash -ne $entry.SHA256) { throw "Payload hash mismatch: $($entry.Relative)" }
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backup = Join-Path $BackupRoot "pre-v3-$stamp"
New-Item -ItemType Directory -Force -Path $backup | Out-Null
if (Test-Path -LiteralPath $InstallRoot) {
    Copy-Item -LiteralPath $InstallRoot -Destination (Join-Path $backup 'Desktop') -Recurse -Force
    $sourceFiles = Get-ChildItem -LiteralPath $InstallRoot -File -Recurse
    $backupFiles = @()
    foreach ($source in $sourceFiles) {
        $relative = [IO.Path]::GetRelativePath($InstallRoot, $source.FullName)
        $copy = Join-Path (Join-Path $backup 'Desktop') $relative
        $sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $source.FullName).Hash
        if ($sourceHash -ne (Get-FileHash -Algorithm SHA256 -LiteralPath $copy).Hash) {
            throw "Backup verification failed: $relative"
        }
        $backupFiles += [ordered]@{ Relative = $relative; Bytes = $source.Length; SHA256 = $sourceHash }
    }
    [ordered]@{ Created = (Get-Date).ToString('o'); Source = $InstallRoot; Files = $backupFiles } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $backup 'backup-manifest.json') -Encoding UTF8
}

New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null
$stagedApp = Join-Path $InstallRoot "app.new-$stamp"
Copy-Item -LiteralPath $Payload -Destination $stagedApp -Recurse -Force
$currentApp = Join-Path $InstallRoot 'app'
$previousApp = Join-Path $backup 'previous-app'
if (Test-Path -LiteralPath $currentApp) { Move-Item -LiteralPath $currentApp -Destination $previousApp }
Move-Item -LiteralPath $stagedApp -Destination $currentApp

$receipt = [ordered]@{
    Version = '3.0.0'
    InstalledAt = (Get-Date).ToString('o')
    InstallRoot = $InstallRoot
    Backup = $backup
    PreservedData = @('config.json','config.json.bak','ManagedItems','journal','logs')
}
$receipt | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $InstallRoot 'install-v3-receipt.json') -Encoding UTF8

if (-not $TestMode) {
    $exe = Join-Path $currentApp 'NextMindDesktop.exe'
    $programs = [Environment]::GetFolderPath('Programs')
    $shortcut = Join-Path $programs 'NextMind Desktop.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($shortcut)
    $link.TargetPath = $exe
    $link.WorkingDirectory = $currentApp
    $link.IconLocation = "$exe,0"
    $link.Save()

    $uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NextMindDesktop'
    New-Item -Path $uninstallKey -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name DisplayName -Value 'NextMind Desktop' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name DisplayVersion -Value '3.0.0' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name Publisher -Value 'NextMind' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name InstallLocation -Value $InstallRoot -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name UninstallString -Value ('"{0}"' -f (Join-Path $InstallRoot 'UNINSTALL-V3.cmd')) -PropertyType String -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UNINSTALL-V3.cmd') -Destination $InstallRoot -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UNINSTALL-V3.ps1') -Destination $InstallRoot -Force
}

Write-Host "Installed NextMind Desktop 3.0.0 to: $currentApp"
Write-Host "Verified rollback backup: $backup"
Write-Host 'Existing configuration and user data were not overwritten.'
Write-Host 'Start NextMind Desktop normally from the Start menu or its stable executable.'
