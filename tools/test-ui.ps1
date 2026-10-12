<#
.SYNOPSIS
  Starts an ISOLATED test instance of NextMind Desktop for manual UI checks. Never touches the installed (production) instance.

.DESCRIPTION
  What makes it isolated:
    * runs the freshly built bin\Release exe, never the exe in %LOCALAPPDATA%\NextMind\Desktop\app
    * NEXTMIND_DESKTOP_CONFIG_DIR points to a new folder under %TEMP% (own config, logs, journal, managed storage)
    * the app, seeing that variable, uses its own single-instance mutex and activate message (".dev"), never reads or writes
      the HKCU Run key, shows "(TEST)" in the tray tooltip, and keeps managed Desktop moves OFF
    * the seed config uses synthetic zones that reference dummy files in the test folder
    * the script remembers the process it started and only ever stops that one (never by name)
  Before and after, it compares the production instance (process id + start time, found by exe path), the production Run-key
  value and the production config file with a baseline, and aborts if isolation cannot be guaranteed.

  Do NOT use tools\start.ps1 / tools\stop.ps1 for this: they act on every NextMindDesktop process and on the installed app.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\test-ui.ps1 -DryRun   # prepare + verify, start nothing
  powershell -ExecutionPolicy Bypass -File tools\test-ui.ps1           # start the test instance
  powershell -ExecutionPolicy Bypass -File tools\test-ui.ps1 -Stop     # stop ONLY the test instance and re-verify production
#>
[CmdletBinding()]
param(
    [switch]$Build,
    [switch]$Stop,
    [switch]$DryRun,
    [string]$Exe
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$prodDir = Join-Path $env:LOCALAPPDATA 'NextMind\Desktop'
$prodExe = Join-Path $prodDir 'app\NextMindDesktop.exe'
$testBase = Join-Path $env:TEMP 'NextMindDesktop-UITest'
$stateFile = Join-Path $testBase 'state.json'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

function Abort([string]$message) { Write-Host "ABORT: $message" -ForegroundColor Red; exit 2 }
function Norm([string]$p) { [IO.Path]::GetFullPath($p).TrimEnd('\') }
function IsUnder([string]$child, [string]$parent) {
    $c = (Norm $child) + '\'; $p = (Norm $parent) + '\'
    return $c.StartsWith($p, [StringComparison]::OrdinalIgnoreCase)
}

function Get-ProdBaseline {
    $run = $null
    try { $run = (Get-ItemProperty $runKey -ErrorAction Stop).NextMindDesktop } catch { }
    $cfg = Join-Path $prodDir 'config.json'
    $hash = ''
    if (Test-Path $cfg) { $hash = (Get-FileHash $cfg -Algorithm SHA256).Hash }
    $prod = @()
    foreach ($p in (Get-Process NextMindDesktop -ErrorAction SilentlyContinue)) {
        $path = $null
        try { $path = $p.Path } catch { }
        if ($path -and ((Norm $path) -ieq (Norm $prodExe))) {
            $prod += [pscustomobject]@{ Pid = $p.Id; Start = $p.StartTime.ToString('o') }
        }
    }
    [pscustomobject]@{ RunValue = $run; ConfigHash = $hash; Prod = @($prod) }
}

function Test-ProdUnchanged($before) {
    $now = Get-ProdBaseline
    $ok = $true
    if ($now.RunValue -ne $before.RunValue) { Write-Host 'FAIL: HKCU Run value changed.' -ForegroundColor Red; $ok = $false }
    foreach ($b in @($before.Prod)) {
        $match = @($now.Prod | Where-Object { $_.Pid -eq $b.Pid -and $_.Start -eq $b.Start })
        if ($match.Count -eq 0) { Write-Host "FAIL: production process $($b.Pid) is gone or was replaced." -ForegroundColor Red; $ok = $false }
    }
    if ($now.ConfigHash -ne $before.ConfigHash) {
        Write-Host 'NOTE: production config.json differs from the baseline. The test instance cannot write it (separate config dir); this is normally your own use of the real app.' -ForegroundColor Yellow
    }
    if ($ok) { Write-Host "OK: production unchanged (Run value, $(@($now.Prod).Count) production process(es) intact)." -ForegroundColor Green }
    return $ok
}

# ---------------------------------------------------------------- stop mode
if ($Stop) {
    if (-not (Test-Path $stateFile)) { Abort 'No test instance recorded (state.json missing).' }
    $state = Get-Content $stateFile -Raw | ConvertFrom-Json
    $proc = Get-Process -Id $state.Pid -ErrorAction SilentlyContinue
    if ($proc) {
        $path = $null
        try { $path = $proc.Path } catch { }
        if (-not $path -or ((Norm $path) -ine (Norm $state.Exe))) { Abort "PID $($state.Pid) is no longer the recorded test exe; not touching it." }
        if (@($state.Baseline.Prod | Where-Object { $_.Pid -eq $state.Pid }).Count -gt 0) { Abort 'The recorded PID is a production process; not touching it.' }
        Stop-Process -Id $state.Pid
        Wait-Process -Id $state.Pid -Timeout 10 -ErrorAction SilentlyContinue
        Write-Host "Stopped test instance PID $($state.Pid). (A ghost tray icon may linger until the mouse passes over it; the cleaner exit is tray -> Exit on the '(TEST)' icon.)"
    } else {
        Write-Host 'Test instance is not running.'
    }
    $null = Test-ProdUnchanged $state.Baseline
    exit 0
}

# ---------------------------------------------------------------- start mode
if (Test-Path $stateFile) {
    $old = Get-Content $stateFile -Raw | ConvertFrom-Json
    $alive = Get-Process -Id $old.Pid -ErrorAction SilentlyContinue
    if ($alive) {
        $p = $null
        try { $p = $alive.Path } catch { }
        if ($p -and ((Norm $p) -ieq (Norm $old.Exe))) { Abort "A test instance is already running (PID $($old.Pid)). Use -Stop first." }
    }
}

if (-not $Exe) { $Exe = Join-Path $root 'src\NextMind.Desktop.App\bin\Release\net8.0-windows10.0.19041.0\NextMindDesktop.exe' }
# Always build (incremental, into bin\ only): the test project does not reference the App project, so a green `dotnet test`
# says nothing about the App binaries. A stale exe lacks the isolation code and would talk to the production instance.
Write-Host 'Building the App (Release, into bin\ only)...'
Push-Location $root
try { dotnet build src\NextMind.Desktop.App -c Release --nologo -v q | Out-Null; if ($LASTEXITCODE -ne 0) { Abort 'Build failed.' } } finally { Pop-Location }
if (-not (Test-Path $Exe)) { Abort "Test exe not found: $Exe" }
$Exe = Norm $Exe

# Refuse a binary older than the sources it should contain.
$exeTime = (Get-Item $Exe).LastWriteTime
$newest = Get-ChildItem (Join-Path $root 'src') -Recurse -Include *.cs, *.xaml -File |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($newest -and $newest.LastWriteTime -gt $exeTime) { Abort "The exe ($exeTime) is older than $($newest.Name) ($($newest.LastWriteTime)). Build did not pick up the sources." }
if (IsUnder $Exe $prodDir) { Abort "The exe lives inside the production folder ($prodDir). Refusing." }
if ((Norm $Exe) -ieq (Norm $prodExe)) { Abort 'The exe is the production exe. Refusing.' }

$baseline = Get-ProdBaseline
Write-Host "Production baseline: $(@($baseline.Prod).Count) process(es); Run value: $($baseline.RunValue)"

# --- isolated test folder (new, under %TEMP%, never inside the production folder)
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$runDir = Join-Path $testBase "run-$stamp"
$cfgDir = Join-Path $runDir 'config'
$filesDir = Join-Path $runDir 'files'
if (-not (IsUnder $runDir $env:TEMP)) { Abort 'Test folder is not under %TEMP%.' }
if ((IsUnder $runDir $prodDir) -or (IsUnder $prodDir $runDir) -or ((Norm $cfgDir) -ieq (Norm $prodDir))) { Abort 'Test config folder overlaps the production folder.' }
New-Item -ItemType Directory -Force -Path $cfgDir, $filesDir | Out-Null
1..14 | ForEach-Object { Set-Content -Path (Join-Path $filesDir ("Test file $_.txt")) -Value "NextMind Desktop UI test file $_" }
New-Item -ItemType Directory -Force -Path (Join-Path $filesDir 'Test folder') | Out-Null

# --- synthetic seed config (all geometry in physical pixels)
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
$wa = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$g = [System.Drawing.Graphics]::FromHwnd([IntPtr]::Zero); $dpi = [int]$g.DpiX; $g.Dispose()
$s = $dpi / 96.0
function Px([double]$dip) { [int][Math]::Round($dip * $s) }

$files = Get-ChildItem $filesDir
function NewItems([int]$count) {
    $list = @()
    $i = 0
    foreach ($f in ($files | Select-Object -First $count)) {
        $i++
        $list += [ordered]@{
            id = [guid]::NewGuid().ToString('D'); kind = 'reference'; path = $f.FullName; name = $f.BaseName
            addedAtUtc = (Get-Date).ToUniversalTime().AddMinutes(-$i).ToString('o'); isFolder = [bool]$f.PSIsContainer
        }
    }
    , $list
}
function NewZone([string]$title, [double]$xDip, [double]$yDip, [bool]$collapsed, [int]$items) {
    [ordered]@{
        id = [guid]::NewGuid().ToString('D'); title = $title
        x = $wa.X + (Px $xDip); y = $wa.Y + (Px $yDip); width = Px 320; height = Px 220
        collapsed = $collapsed; dpi = $dpi; hidden = $false; sortMode = 'custom'; items = (NewItems $items)
    }
}
$bottomY = ($wa.Height / $s) - 32 - 4 # collapsed title bar hugging the bottom edge of the work area
$zones = @(
    (NewZone 'TEST A basic' 40 260 $false 8),
    (NewZone 'TEST B expands up' 400 430 $true 6),
    (NewZone 'TEST C below B' 400 480 $false 5),
    (NewZone 'TEST D at bottom edge' 40 $bottomY $true 4),
    (NewZone 'TEST E overlap 1' 760 300 $false 6),
    (NewZone 'TEST F overlap 2' 800 370 $true 4)
)
$config = [ordered]@{
    schemaVersion = 3; zonesVisible = $true; autostartDecided = $true; autostartEnabled = $false
    managedDesktopMovesEnabled = $false; zones = $zones
}
$json = $config | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText((Join-Path $cfgDir 'config.json'), $json, (New-Object Text.UTF8Encoding($false)))
Write-Host "Seed config: $cfgDir\config.json ($($zones.Count) synthetic zones, dummy files in $filesDir)"

# --- warn when test zones would sit on top of the real zones (read-only look at the production config)
$prodCfg = Join-Path $prodDir 'config.json'
if (Test-Path $prodCfg) {
    try {
        $real = (Get-Content $prodCfg -Raw | ConvertFrom-Json).zones
        foreach ($t in $zones) {
            foreach ($r in $real) {
                $ox = [Math]::Min($t.x + $t.width, $r.x + $r.width) - [Math]::Max($t.x, $r.x)
                $oy = [Math]::Min($t.y + $t.height, $r.y + $r.height) - [Math]::Max($t.y, $r.y)
                if ($ox -gt 0 -and $oy -gt 0) { Write-Host "WARN: '$($t.title)' overlaps your real zone at the same place; hide your zones from the tray for the test or move the test zone." -ForegroundColor Yellow; break }
            }
        }
    } catch { Write-Host 'NOTE: could not read the production layout for the overlap warning.' -ForegroundColor Yellow }
}

if ($DryRun) {
    Write-Host "DRY RUN: nothing started. Exe: $Exe" -ForegroundColor Cyan
    $null = Test-ProdUnchanged $baseline
    exit 0
}

# --- start ONLY the test exe, with the isolation variables in this child's environment
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $Exe
$psi.WorkingDirectory = Split-Path $Exe -Parent
$psi.UseShellExecute = $false
$psi.EnvironmentVariables['NEXTMIND_DESKTOP_CONFIG_DIR'] = $cfgDir
foreach ($v in 'NEXTMIND_FAKE_USER_DESKTOP', 'NEXTMIND_ENABLE_MANAGED_MOVES') { if ($psi.EnvironmentVariables.ContainsKey($v)) { $psi.EnvironmentVariables.Remove($v) } }
$proc = [System.Diagnostics.Process]::Start($psi)
New-Item -ItemType Directory -Force -Path $testBase | Out-Null
[pscustomobject]@{ Pid = $proc.Id; Exe = $Exe; ConfigDir = $cfgDir; Started = (Get-Date).ToString('o'); Baseline = $baseline } |
    ConvertTo-Json -Depth 6 | Set-Content -Path $stateFile -Encoding UTF8
Write-Host "Started TEST instance PID $($proc.Id) from $Exe"

# --- verify the instance really is the isolated one (from its own log)
$logOk = $false
for ($i = 0; $i -lt 40 -and -not $logOk; $i++) {
    Start-Sleep -Milliseconds 500
    if ($proc.HasExited) { Write-Host "FAIL: test instance exited with code $($proc.ExitCode). Check $cfgDir\logs." -ForegroundColor Red; $null = Test-ProdUnchanged $baseline; exit 2 }
    $log = Get-ChildItem (Join-Path $cfgDir 'logs') -Filter *.log -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($log) { $text = Get-Content $log.FullName -Raw -ErrorAction SilentlyContinue; $logOk = $text -match 'Cold start' }
}
if (-not $logOk) {
    Write-Host 'FAIL: the test instance did not report a clean start within 20 s. Stopping it (only that process).' -ForegroundColor Red
    Stop-Process -Id $proc.Id -ErrorAction SilentlyContinue
    $null = Test-ProdUnchanged $baseline
    exit 2
}
if ($logOk) {
    $lines = $text -split "`r?`n" | Where-Object { $_ -match 'Config:|Managed Desktop Moves|Development run|Tray icon added|Cold start' }
    $lines | ForEach-Object { Write-Host "  log: $_" }
    if ($text -notmatch 'Development run') { Write-Host 'FAIL: the instance did not report a development run. Stopping it.' -ForegroundColor Red; Stop-Process -Id $proc.Id; exit 2 }
    if ($text -notmatch 'Managed Desktop Moves: DISABLED') { Write-Host 'FAIL: managed moves are not disabled. Stopping it.' -ForegroundColor Red; Stop-Process -Id $proc.Id; exit 2 }
}
$ok = Test-ProdUnchanged $baseline
if (-not $ok) { Write-Host 'Production changed while the test started: stopping the TEST instance only.' -ForegroundColor Red; Stop-Process -Id $proc.Id -ErrorAction SilentlyContinue; exit 2 }
Write-Host ''
Write-Host 'Test instance is running (tray tooltip "NextMind Desktop (TEST)"). Stop it with:'
Write-Host '  powershell -ExecutionPolicy Bypass -File tools\test-ui.ps1 -Stop'
