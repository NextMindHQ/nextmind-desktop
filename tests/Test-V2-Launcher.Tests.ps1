$ErrorActionPreference = 'Stop'

$repo = Split-Path $PSScriptRoot -Parent
$launcher = Join-Path $repo 'TEST-V2.cmd'

function Invoke-Launcher([string[]] $Arguments, [string] $PathOverride = $env:PATH) {
    $before = @(Get-Process -Name NextMindDesktop -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $env:ComSpec
    $startInfo.Arguments = '/d /c "' + $launcher + '" ' + ($Arguments -join ' ')
    $startInfo.WorkingDirectory = $repo
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Environment['PATH'] = $PathOverride
    $process = [System.Diagnostics.Process]::Start($startInfo)
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    $after = @(Get-Process -Name NextMindDesktop -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
    [pscustomobject]@{
        ExitCode = $process.ExitCode
        Output = $stdout + $stderr
        NewProcessIds = @($after | Where-Object { $_ -notin $before })
    }
}

function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) {
        throw $Message
    }
}

if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) {
    throw "Launcher does not exist: $launcher"
}

$createdConfigs = @()
$fakeBin = $null
try {
    $success = Invoke-Launcher @('--verify-only')
    Assert-True ($success.ExitCode -eq 0) "Verify-only failed with exit $($success.ExitCode):`n$($success.Output)"
    Assert-True ($success.NewProcessIds.Count -eq 0) 'Verify-only started a NextMindDesktop process.'
    Assert-True ($success.Output -match 'VERIFICATION PASSED - V2 was not started\.') 'Verify-only success marker is missing.'
    Assert-True ($success.Output -match 'Running instance check: (detected|none detected)\.') 'Read-only running-instance check is missing.'
    $match = [regex]::Match($success.Output, '(?m)^Isolated config: (.+)\r?$')
    Assert-True $match.Success 'Verify-only did not report its isolated config directory.'
    $createdConfig = [IO.Path]::GetFullPath($match.Groups[1].Value.Trim())
    $createdConfigs += $createdConfig
    $tempRoot = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
    Assert-True ($createdConfig.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) "Config is not below TEMP: $createdConfig"
    Assert-True (Test-Path -LiteralPath $createdConfig -PathType Container) "Config directory was not created: $createdConfig"

    $second = Invoke-Launcher @('--verify-only')
    Assert-True ($second.ExitCode -eq 0) "Second verify-only failed with exit $($second.ExitCode):`n$($second.Output)"
    Assert-True ($second.NewProcessIds.Count -eq 0) 'Second verify-only started a NextMindDesktop process.'
    $secondMatch = [regex]::Match($second.Output, '(?m)^Isolated config: (.+)\r?$')
    Assert-True $secondMatch.Success 'Second verify-only did not report its isolated config directory.'
    $secondConfig = [IO.Path]::GetFullPath($secondMatch.Groups[1].Value.Trim())
    $createdConfigs += $secondConfig
    Assert-True (-not $secondConfig.Equals($createdConfig, [StringComparison]::OrdinalIgnoreCase)) 'Two runs reused the same config directory.'

    $fakeBin = Join-Path $env:TEMP ('NextMind-TestV2-FakeDotnet-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $fakeBin | Out-Null
    Set-Content -LiteralPath (Join-Path $fakeBin 'dotnet.cmd') -Encoding Ascii -Value '@exit /b 42'
    $failure = Invoke-Launcher @('--verify-only') ($fakeBin + [IO.Path]::PathSeparator + $env:PATH)
    Assert-True ($failure.ExitCode -ne 0) 'A failed Release build returned exit code 0.'
    Assert-True ($failure.NewProcessIds.Count -eq 0) 'Build failure started a NextMindDesktop process.'
    Assert-True ($failure.Output -match 'Release build failed\. V2 was not started\.') "Readable build-failure message is missing. Output:`n$($failure.Output)"

    Write-Host 'PASS: TEST-V2 launcher verification and build-failure gate.'
}
finally {
    foreach ($path in @($createdConfigs + $fakeBin)) {
        if ([string]::IsNullOrWhiteSpace($path)) { continue }
        $full = [IO.Path]::GetFullPath($path)
        $tempRoot = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
        if ($full.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $full)) {
            Remove-Item -LiteralPath $full -Recurse -Force
        }
    }
}
