# Overlay a fully-labeled PIXEL ruler on a monster HD sprite sheet so a human can read off the
# exact start coordinates of a crop. EVERY $Minor-px line is labeled with its pixel value on both
# axes (x labels vertical in the top margin, y labels in the left margin); $Major lines are brighter.
# Pixels are SOURCE pixels (the sheet is scaled $Scale x only for readability).
#
# Read off "x starts at <val>, y starts at <val>" + a cell size, and that maps straight to a
# FrameLayout(frameW, frameH, swCol, nwCol, row, offsetX=<x>, offsetY=<y>).
#
# Usage: powershell -File grid_overlay.ps1 -SrcPath <hd.bmp> -OutPath <out.png> [-Scale 2] [-Minor 16] [-Major 64]
param(
    [Parameter(Mandatory=$true)][string]$SrcPath,
    [Parameter(Mandatory=$true)][string]$OutPath,
    [int]$Scale = 2,
    [int]$Minor = 16,
    [int]$Major = 64
)
Add-Type -AssemblyName System.Drawing
$src = [System.Drawing.Bitmap]::FromFile($SrcPath)
$W = $src.Width; $H = $src.Height
$L = 40   # left margin (y labels)
$T = 26   # top margin (x labels)
$bmp = New-Object System.Drawing.Bitmap (($W * $Scale) + $L), (($H * $Scale) + $T), ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(255, 12, 12, 12))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g.DrawImage($src, $L, $T, ($W * $Scale), ($H * $Scale))

$minorPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(70, 0, 255, 255)), 1
$majorPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(200, 0, 255, 255)), 1
$fontMin = New-Object System.Drawing.Font "Consolas", 6
$fontMaj = New-Object System.Drawing.Font "Consolas", 7, ([System.Drawing.FontStyle]::Bold)
$yel = [System.Drawing.Brushes]::Yellow
$gry = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(150, 200, 200))

# Vertical lines + x labels (rotated, in top margin)
for ($x = 0; $x -le $W; $x += $Minor) {
    $isMaj = ($x % $Major -eq 0)
    $px = $L + $x * $Scale
    $g.DrawLine(($(if ($isMaj) { $majorPen } else { $minorPen })), $px, $T, $px, $T + $H * $Scale)
    $g.TranslateTransform($px, ($T - 2))
    $g.RotateTransform(-90)
    $g.DrawString("$x", $(if ($isMaj) { $fontMaj } else { $fontMin }), $(if ($isMaj) { $yel } else { $gry }), 0, -5)
    $g.ResetTransform()
}
# Horizontal lines + y labels (in left margin)
for ($y = 0; $y -le $H; $y += $Minor) {
    $isMaj = ($y % $Major -eq 0)
    $py = $T + $y * $Scale
    $g.DrawLine(($(if ($isMaj) { $majorPen } else { $minorPen })), $L, $py, $L + $W * $Scale, $py)
    $g.DrawString("$y", $(if ($isMaj) { $fontMaj } else { $fontMin }), $(if ($isMaj) { $yel } else { $gry }), 1, ($py - 6))
}

$g.Dispose(); $src.Dispose()
$bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Saved $OutPath ($W x $H @ ${Scale}x, every $Minor px labeled, major $Major px)"
