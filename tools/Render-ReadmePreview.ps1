[CmdletBinding()]
param(
    [string]$Readme = (Join-Path (Split-Path $PSScriptRoot -Parent) 'README.md'),
    [string]$Output = (Join-Path (Split-Path $PSScriptRoot -Parent) '.tmp\readme-preview.html')
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$markdown = Get-Content -Raw -LiteralPath $Readme
$body = (ConvertFrom-Markdown -InputObject $markdown).Html
$base = ([Uri]((Resolve-Path -LiteralPath $repo).Path + [IO.Path]::DirectorySeparatorChar)).AbsoluteUri
$html = @"
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <base href="$base">
  <title>NextMind Desktop README preview</title>
  <style>
    :root { color-scheme: light; }
    * { box-sizing: border-box; }
    body { margin: 0; background: #f6f8fa; color: #1f2328; font: 16px/1.5 -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
    main { width: min(1012px, 100%); margin: 24px auto; padding: 40px 48px; background: #fff; border: 1px solid #d0d7de; border-radius: 8px; overflow-wrap: anywhere; }
    h1, h2, h3 { line-height: 1.25; }
    h1, h2 { padding-bottom: .3em; border-bottom: 1px solid #d8dee4; }
    h1 { font-size: 2em; } h2 { margin-top: 1.5em; font-size: 1.5em; } h3 { margin-top: 1.35em; }
    img { max-width: 100%; height: auto; }
    table { width: 100%; border-spacing: 0; border-collapse: collapse; display: block; overflow-x: auto; }
    td, th { padding: 8px 10px; border: 1px solid #d0d7de; vertical-align: top; }
    code { padding: .2em .4em; background: #eff1f3; border-radius: 5px; font: 85% ui-monospace, SFMono-Regular, Consolas, monospace; }
    pre { padding: 16px; overflow: auto; background: #f6f8fa; border-radius: 6px; }
    pre code { padding: 0; background: transparent; }
    blockquote { margin: 0; padding: 0 1em; color: #59636e; border-left: .25em solid #d0d7de; }
    a { color: #0969da; text-decoration: none; }
    details { margin: 12px 0; padding: 12px 16px; border: 1px solid #d0d7de; border-radius: 6px; }
    @media (max-width: 600px) { body { background: #fff; } main { margin: 0; padding: 22px 16px 40px; border: 0; border-radius: 0; } h1 { font-size: 1.75em; } h2 { font-size: 1.35em; } td, th { min-width: 260px; } }
  </style>
</head>
<body><main>$body</main></body>
</html>
"@
$directory = Split-Path $Output -Parent
New-Item -ItemType Directory -Force -Path $directory | Out-Null
Set-Content -LiteralPath $Output -Value $html -Encoding UTF8
Write-Output (Resolve-Path -LiteralPath $Output).Path
