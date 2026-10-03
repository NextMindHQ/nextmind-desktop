# Inspect / exercise "Start with Windows" WITHOUT restarting or logging off.
#   status    - what the per-user Run entry says and whether its target exists
#   simulate  - run the registered command line exactly as Windows does at logon (a second instance must exit by itself)
#   enable / disable - the same single registry value the tray checkbox writes/removes
param([ValidateSet('status', 'simulate', 'enable', 'disable')][string]$Action = 'status')

$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$name = 'NextMindDesktop'
$defaultExe = Join-Path $env:LOCALAPPDATA 'NextMind\Desktop\app\NextMindDesktop.exe'

function Get-Entry { (Get-ItemProperty -Path $key -Name $name -ErrorAction SilentlyContinue).$name }
function Parse-Exe($cmd) {
    if ([string]::IsNullOrWhiteSpace($cmd)) { return $null }
    $c = $cmd.Trim()
    if ($c.StartsWith('"')) { $e = $c.IndexOf('"', 1); if ($e -gt 1) { return $c.Substring(1, $e - 1) } else { return $null } }
    $i = $c.IndexOf('.exe', [StringComparison]::OrdinalIgnoreCase); if ($i -ge 0) { return $c.Substring(0, $i + 4) } else { return $null }
}

switch ($Action) {
    'status' {
        $cmd = Get-Entry
        if ($null -eq $cmd) { Write-Host "Start with Windows: OFF  (no '$name' value in HKCU Run)"; break }
        $exe = Parse-Exe $cmd
        Write-Host "Start with Windows: ON"
        Write-Host "  registry value : $cmd"
        if ($exe -and (Test-Path -LiteralPath $exe)) { Write-Host "  target exists  : yes ($exe)" } else { Write-Host "  target exists  : NO - entry is broken; the app repairs it the next time it starts" }
        Write-Host ("  app running    : " + [bool](Get-Process NextMindDesktop -ErrorAction SilentlyContinue))
    }
    'simulate' {
        $cmd = Get-Entry
        $exe = Parse-Exe $cmd
        if (-not $exe -or -not (Test-Path -LiteralPath $exe)) { Write-Host 'Nothing valid to run (see: status).'; break }
        $before = @(Get-Process NextMindDesktop -ErrorAction SilentlyContinue).Count
        Write-Host "Running the registered command like logon would: $cmd"
        Start-Process -FilePath $exe
        Start-Sleep -Seconds 4
        $after = @(Get-Process NextMindDesktop -ErrorAction SilentlyContinue).Count
        Write-Host "NextMindDesktop processes: before=$before after=$after (expected: exactly 1 after, whether or not it was already running)"
    }
    'enable' {
        if (-not (Test-Path -LiteralPath $defaultExe)) { Write-Host "Not installed yet: run tools\start.ps1 first ($defaultExe)."; break }
        Set-ItemProperty -Path $key -Name $name -Value ('"' + $defaultExe + '"') -Type String
        Write-Host 'Enabled.'
    }
    'disable' {
        Remove-ItemProperty -Path $key -Name $name -ErrorAction SilentlyContinue
        Write-Host 'Disabled.'
    }
}
