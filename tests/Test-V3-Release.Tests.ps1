$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$stage = Join-Path $repo 'artifacts\release-v3\staging'
$setup = Join-Path $stage 'setup-package'
$portable = Join-Path $stage 'portable'
$controlCenter = Join-Path $repo 'src\NextMind.Desktop.App\ControlCenterWindow.xaml'
function Require([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }

Require (-not (Select-String -LiteralPath $controlCenter -SimpleMatch 'Live Preview' -Quiet)) 'Production Control Center contains a preview-only label.'
Require (Test-Path -LiteralPath (Join-Path $setup 'RELEASE-MANIFEST.json')) 'Setup manifest is missing.'
Require (Test-Path -LiteralPath (Join-Path $setup 'LICENSE')) 'Setup package is missing the Apache-2.0 license.'
Require (Test-Path -LiteralPath (Join-Path $portable 'LICENSE')) 'Portable package is missing the Apache-2.0 license.'
Require (-not (Select-String -LiteralPath (Join-Path $setup 'INSTALL-V3.ps1') -SimpleMatch '[IO.Path]::GetRelativePath' -Quiet)) 'Installer uses an API unavailable in Windows PowerShell 5.1.'
$manifest = Get-Content -Raw -LiteralPath (Join-Path $setup 'RELEASE-MANIFEST.json') | ConvertFrom-Json
Require ($manifest.Version -eq '3.0.0') 'Setup version is not 3.0.0.'
foreach ($entry in $manifest.PayloadFiles) {
    $file = Join-Path (Join-Path $setup 'payload') $entry.Relative
    Require (Test-Path -LiteralPath $file) "Missing payload file: $($entry.Relative)"
    Require ((Get-FileHash -Algorithm SHA256 -LiteralPath $file).Hash -eq $entry.SHA256) "Payload hash mismatch: $($entry.Relative)"
}

Require (-not (Test-Path -LiteralPath (Join-Path $setup 'payload\V3-RUNTIME-MARKER.txt'))) 'Production payload contains the V3 test marker.'
Require (-not (Get-ChildItem -LiteralPath $setup -File -Recurse | Where-Object Extension -in '.pdb','.log')) 'Setup contains developer symbols or logs.'
Require (Test-Path -LiteralPath (Join-Path $portable 'NextMindDesktop.portable')) 'Portable marker is missing.'
Require (-not (Get-ChildItem -LiteralPath $portable -File -Recurse | Where-Object Extension -in '.pdb','.log')) 'Portable contains developer symbols or logs.'
Require ((Get-Item -LiteralPath (Join-Path $setup 'payload\NextMindDesktop.exe')).VersionInfo.FileVersion -eq '3.0.0.0') 'Setup executable version is wrong.'
Require ((Get-Item -LiteralPath (Join-Path $portable 'NextMindDesktop.exe')).VersionInfo.FileVersion -eq '3.0.0.0') 'Portable executable version is wrong.'
Write-Host 'PASS: V3 release package manifest, production hygiene, version and portable isolation marker.'
