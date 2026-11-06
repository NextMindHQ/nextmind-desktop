[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $repo 'artifacts\release-v3'
$stage = Join-Path $artifacts 'staging'
$setup = Join-Path $stage 'setup-package'
$setupPayload = Join-Path $setup 'payload'
$portable = Join-Path $stage 'portable'
$dist = Join-Path $artifacts 'dist'
$project = Join-Path $repo 'src\NextMind.Desktop.App\NextMind.Desktop.App.csproj'

if (-not $artifacts.StartsWith((Join-Path $repo 'artifacts'), [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to build outside the repository artifacts directory.'
}

if (Test-Path -LiteralPath $artifacts) {
    Remove-Item -LiteralPath $artifacts -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $setupPayload, $portable, $dist | Out-Null

& dotnet publish $project -c $Configuration -r win-x64 --self-contained false -p:NextMindV3TestBuild=false -o $setupPayload
if ($LASTEXITCODE -ne 0) { throw 'Framework-dependent publish failed.' }

& dotnet publish $project -c $Configuration -r win-x64 --self-contained true -p:NextMindV3TestBuild=false -o $portable
if ($LASTEXITCODE -ne 0) { throw 'Self-contained portable publish failed.' }

Get-ChildItem -LiteralPath $setupPayload, $portable -Recurse -File |
    Where-Object { $_.Extension -in '.pdb', '.log' -or $_.Name -eq 'V3-RUNTIME-MARKER.txt' } |
    Remove-Item -Force

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'INSTALL-V3.cmd') -Destination $setup
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'INSTALL-V3.ps1') -Destination $setup
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UNINSTALL-V3.cmd') -Destination $setup
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UNINSTALL-V3.ps1') -Destination $setup
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ROLLBACK-V3.ps1') -Destination $setup
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README-INSTALL.txt') -Destination $setup
Copy-Item -LiteralPath (Join-Path $repo 'docs\CHANGELOG-V3.md') -Destination $setup
Copy-Item -LiteralPath (Join-Path $repo 'docs\RELEASE-NOTES-V3.md') -Destination $setup
Copy-Item -LiteralPath (Join-Path $repo 'docs\ROLLBACK-V3.md') -Destination $setup
Copy-Item -LiteralPath (Join-Path $repo 'docs\UPDATE-V3.md') -Destination $setup

$payloadFiles = @(
    Get-ChildItem -LiteralPath $setupPayload -Recurse -File |
        Sort-Object FullName |
        ForEach-Object {
            if (-not $_.FullName.StartsWith($setupPayload, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Payload file escaped its staging directory: $($_.FullName)"
            }
            [ordered]@{
                Relative = $_.FullName.Substring($setupPayload.Length).TrimStart([IO.Path]::DirectorySeparatorChar)
                Bytes = $_.Length
                SHA256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash
            }
        }
)

$commit = (& git -C $repo rev-parse HEAD).Trim()
[ordered]@{
    Product = 'NextMind Desktop'
    Version = '3.0.0'
    Commit = $commit
    Created = (Get-Date).ToString('o')
    PayloadFiles = $payloadFiles
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $setup 'RELEASE-MANIFEST.json') -Encoding UTF8

Set-Content -LiteralPath (Join-Path $portable 'NextMindDesktop.portable') -Value 'NextMind Desktop Portable 3.0.0' -Encoding ASCII
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README-PORTABLE.txt') -Destination $portable

$setupZip = Join-Path $dist 'NextMindDesktop-3.0.0-Setup.zip'
$portableZip = Join-Path $dist 'NextMindDesktop-3.0.0-Portable-win-x64.zip'
Compress-Archive -Path (Join-Path $setup '*') -DestinationPath $setupZip -CompressionLevel Optimal
Compress-Archive -Path (Join-Path $portable '*') -DestinationPath $portableZip -CompressionLevel Optimal

$hashes = @(
    Get-Item -LiteralPath $setupZip, $portableZip |
        ForEach-Object { '{0}  {1}' -f (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash, $_.Name }
)
$hashes | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ASCII

Write-Host "Built: $setupZip"
Write-Host "Built: $portableZip"
$hashes | ForEach-Object { Write-Host $_ }
