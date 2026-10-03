# Stops NextMind Desktop. Prefer tray -> Exit (it flushes the config immediately); the config is also saved
# 400 ms after every change, so this is safe too.
$p = Get-Process NextMindDesktop -ErrorAction SilentlyContinue
if ($p) { $p | Stop-Process; Write-Host 'Stopped.' } else { Write-Host 'Not running.' }
