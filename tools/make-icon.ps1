# Generates src/NextMind.Desktop.App/Assets/nextmind.ico (placeholder shark-fin mark; PNG-compressed multi-size ICO).
# Run with Windows PowerShell 5.1:  powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot '..\src\NextMind.Desktop.App\Assets\nextmind.ico'
$sizes = 16, 24, 32, 48, 64, 256

function New-IconPng([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $r = [single]($s * 0.24); $d = $r * 2; $w = [single]($s - 1)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $d, $d, 180, 90); $path.AddArc($w - $d, 0, $d, $d, 270, 90)
    $path.AddArc($w - $d, $w - $d, $d, $d, 0, 90); $path.AddArc(0, $w - $d, $d, $d, 90, 90); $path.CloseFigure()

    $rect = New-Object System.Drawing.RectangleF 0, 0, $s, $s
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 20, 28, 40)), ([System.Drawing.Color]::FromArgb(255, 15, 118, 130)), 60.0
    $g.FillPath($bg, $path)

    $fin = [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF ($s * 0.18), ($s * 0.74)),
        (New-Object System.Drawing.PointF ($s * 0.46), ($s * 0.20)),
        (New-Object System.Drawing.PointF ($s * 0.56), ($s * 0.52)),
        (New-Object System.Drawing.PointF ($s * 0.82), ($s * 0.74)))
    $g.FillPolygon((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 232, 246, 250))), $fin)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(200, 120, 220, 230)), ([single][Math]::Max(1.0, $s * 0.05))
    $g.DrawLine($pen, [single]($s * 0.12), [single]($s * 0.82), [single]($s * 0.88), [single]($s * 0.82))
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$pngs = foreach ($s in $sizes) { , (New-IconPng $s) }
$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ms
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $pngs[$i].Length
    $bw.Write([byte]$(if ($s -ge 256) { 0 } else { $s })); $bw.Write([byte]$(if ($s -ge 256) { 0 } else { $s }))
    $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$len); $bw.Write([uint32]$offset)
    $offset += $len
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush()
[System.IO.File]::WriteAllBytes((Resolve-Path (Split-Path $out)).Path + '\nextmind.ico', $ms.ToArray())
"Wrote $out ($($ms.Length) bytes)"
