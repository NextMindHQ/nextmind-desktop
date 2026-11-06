$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$launcher = Join-Path $repo 'TEST-V3.cmd'

function Invoke-Launcher([string] $pathOverride = $env:PATH) {
    $before = @(Get-Process -Name NextMindDesktop -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
    $info = [Diagnostics.ProcessStartInfo]::new($env:ComSpec, '/d /c "' + $launcher + '" --verify-only')
    $info.WorkingDirectory = $repo
    $info.UseShellExecute = $false
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.Environment['PATH'] = $pathOverride
    $process = [Diagnostics.Process]::Start($info)
    $output = $process.StandardOutput.ReadToEnd() + $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    $after = @(Get-Process -Name NextMindDesktop -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
    [pscustomobject]@{ ExitCode = $process.ExitCode; Output = $output; NewIds = @($after | Where-Object { $_ -notin $before }) }
}

function Require([bool] $condition, [string] $message) { if (-not $condition) { throw $message } }

if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) { throw "Launcher does not exist: $launcher" }
$configs = @(); $builds = @(); $fakeBin = $null
try {
    $first = Invoke-Launcher
    Require ($first.ExitCode -eq 0) "Verify-only failed:`n$($first.Output)"
    Require ($first.NewIds.Count -eq 0) 'Verify-only started V3.'
    Require ($first.Output -match 'V3 VERIFICATION PASSED - application was not started\.') 'V3 success marker missing.'
    Require ($first.Output -match 'FutureTheme\.xaml') 'Current V3 theme marker was not reported.'
    Require ($first.Output -match 'V3 runtime marker: NEXTMIND-DESKTOP-V3-LIVE-PREVIEW-2') 'Compiled V3 runtime marker missing.'
    $m1 = [regex]::Match($first.Output, '(?m)^Isolated config: (.+)\r?$'); Require $m1.Success 'Config path missing.'
    $configs += [IO.Path]::GetFullPath($m1.Groups[1].Value.Trim())
    $b1 = [regex]::Match($first.Output, '(?m)^Build V3: (.+)\r?$'); Require $b1.Success 'Build path missing.'
    $builds += [IO.Path]::GetFullPath($b1.Groups[1].Value.Trim())

    $second = Invoke-Launcher
    Require ($second.ExitCode -eq 0) "Second verify-only failed:`n$($second.Output)"
    $m2 = [regex]::Match($second.Output, '(?m)^Isolated config: (.+)\r?$'); Require $m2.Success 'Second config path missing.'
    $configs += [IO.Path]::GetFullPath($m2.Groups[1].Value.Trim())
    $b2 = [regex]::Match($second.Output, '(?m)^Build V3: (.+)\r?$'); Require $b2.Success 'Second build path missing.'
    $builds += [IO.Path]::GetFullPath($b2.Groups[1].Value.Trim())
    Require (-not $configs[0].Equals($configs[1], [StringComparison]::OrdinalIgnoreCase)) 'V3 runs reused config.'
    Require (-not $builds[0].Equals($builds[1], [StringComparison]::OrdinalIgnoreCase)) 'V3 runs reused a build directory.'

    $fakeBin = Join-Path $env:TEMP ('NextMind-V3-FakeDotnet-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $fakeBin | Out-Null
    Set-Content -LiteralPath (Join-Path $fakeBin 'dotnet.cmd') -Encoding Ascii -Value '@exit /b 42'
    $failure = Invoke-Launcher ($fakeBin + [IO.Path]::PathSeparator + $env:PATH)
    Require ($failure.ExitCode -ne 0) 'Failed V3 build returned success.'
    Require ($failure.NewIds.Count -eq 0) 'Failed V3 build started an application.'
    Require ($failure.Output -match 'Release build failed\. V3 was not started\.') 'Build failure message missing.'
    Write-Host 'PASS: TEST-V3 isolated launcher and failure gate.'
}
finally {
    $temp = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
    foreach ($path in @($configs + $fakeBin)) {
        if ($path) { $full = [IO.Path]::GetFullPath($path); if ($full.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $full)) { Remove-Item -LiteralPath $full -Recurse -Force } }
    }
}
