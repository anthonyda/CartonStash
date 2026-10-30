<#
.SYNOPSIS
    Renders every app logo from src/CartonStash.App/Assets/AppIcon.svg (spec 007 T1).

.DESCRIPTION
    Uses headless Microsoft Edge to rasterize the SVG at each size (native rendering, transparent background), then
    System.Drawing to crop and to pack AppIcon.ico. Outputs are committed; rerun after changing AppIcon.svg.

    - AppIcon.ico                         16–256 px, for the exe, window and taskbar (unpackaged)
    - Square44x44Logo / Square150x150Logo / Wide310x150Logo / StoreLogo / SplashScreen PNGs for the MSIX manifest,
      at scale-100 and scale-200, plus Square44x44Logo.targetsize-* for crisp taskbar and Start list icons.
#>
[CmdletBinding()]
param(
    [string]$Assets = (Join-Path $PSScriptRoot '..\src\CartonStash.App\Assets')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$edge = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw 'Microsoft Edge is required to render the SVG.' }

$Assets = (Resolve-Path $Assets).Path
$svg = Get-Content (Join-Path $Assets 'AppIcon.svg') -Raw
$work = Join-Path ([IO.Path]::GetTempPath()) "cartonstash-logos-$PID"
New-Item -ItemType Directory -Force $work | Out-Null

# Renders the icon $iconSize px wide, centered on a transparent $width x $height canvas, and returns the PNG path.
function Render([int]$width, [int]$height, [int]$iconSize, [string]$name) {
    $markup = $svg -replace '<svg ', "<svg width=`"$iconSize`" height=`"$iconSize`" style=`"display:block`" "
    $html = "<html><body style='margin:0;background:transparent'><div style='width:${width}px;height:${height}px;display:flex;align-items:center;justify-content:center'>$markup</div></body></html>"
    $page = Join-Path $work "$name.html"
    Set-Content $page $html -Encoding utf8
    $shot = Join-Path $work "$name.shot.png"
    $canvas = [Math]::Max([Math]::Max($width, $height), 300)
    & $edge --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 --default-background-color=00000000 `
        "--window-size=$($canvas + 50),$($canvas + 50)" "--screenshot=$shot" "file:///$($page -replace '\\', '/')" 2>&1 | Out-Null
    # msedge.exe returns before the headless browser has written the file.
    for ($i = 0; $i -lt 80 -and -not (Test-Path $shot); $i++) { Start-Sleep -Milliseconds 250 }
    Start-Sleep -Milliseconds 300
    $source = [System.Drawing.Bitmap]::FromFile($shot)
    try {
        $frame = $source.Clone([System.Drawing.Rectangle]::new(0, 0, $width, $height), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $out = Join-Path $work "$name.png"
        $frame.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
        $frame.Dispose()
        return $out
    }
    finally { $source.Dispose() }
}

function Save([string]$from, [string]$fileName) { Copy-Item $from (Join-Path $Assets $fileName) -Force; "  $fileName" }

# MSIX logos (spec 007 T1). Square logos fill their tile; wide and splash center the icon.
foreach ($scale in 100, 200) {
    $f = $scale / 100
    Save (Render (44 * $f) (44 * $f) (44 * $f) "s44-$scale") "Square44x44Logo.scale-$scale.png"
    Save (Render (150 * $f) (150 * $f) (150 * $f) "s150-$scale") "Square150x150Logo.scale-$scale.png"
    Save (Render (310 * $f) (150 * $f) (100 * $f) "w310-$scale") "Wide310x150Logo.scale-$scale.png"
    Save (Render (50 * $f) (50 * $f) (50 * $f) "store-$scale") "StoreLogo.scale-$scale.png"
    Save (Render (620 * $f) (300 * $f) (160 * $f) "splash-$scale") "SplashScreen.scale-$scale.png"
}
foreach ($size in 16, 24, 32, 48, 256) {
    $png = Render $size $size $size "t$size"
    Save $png "Square44x44Logo.targetsize-$size.png"
    Save $png "Square44x44Logo.targetsize-${size}_altform-unplated.png"
}

# AppIcon.ico: PNG-compressed frames (Windows Vista+).
$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$frames = foreach ($n in $sizes) { [pscustomobject]@{ Size = $n; Bytes = [IO.File]::ReadAllBytes((Render $n $n $n "ico$n")) } }
$ms = New-Object IO.MemoryStream
$w = New-Object IO.BinaryWriter($ms)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$f.Bytes.Length); $w.Write([uint32]$offset)
    $offset += $f.Bytes.Length
}
foreach ($f in $frames) { $w.Write($f.Bytes) }
$w.Flush()
[IO.File]::WriteAllBytes((Join-Path $Assets 'AppIcon.ico'), $ms.ToArray())
"  AppIcon.ico ($($sizes -join ', ') px)"

Remove-Item $work -Recurse -Force
