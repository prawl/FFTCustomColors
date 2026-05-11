# Overlay a coordinate grid on an image for cropping reference.
# Usage: overlay_grid.ps1 -SrcPath <path> -OutPath <path> [-Step 32]

param(
    [Parameter(Mandatory=$true)][string]$SrcPath,
    [Parameter(Mandatory=$true)][string]$OutPath,
    [int]$Step = 32
)

Add-Type -AssemblyName System.Drawing

$src = [System.Drawing.Bitmap]::FromFile($SrcPath)
$bmp = New-Object System.Drawing.Bitmap $src.Width, $src.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.DrawImage($src, 0, 0, $src.Width, $src.Height)

$gridPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(120, 0, 255, 255)), 1
$majorPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(200, 255, 255, 0)), 1
$font = New-Object System.Drawing.Font "Consolas", 9, ([System.Drawing.FontStyle]::Bold)
$brush = [System.Drawing.Brushes]::Yellow
$shadowBrush = [System.Drawing.Brushes]::Black

for ($x = 0; $x -le $src.Width; $x += $Step) {
    $pen = if ($x % 64 -eq 0) { $majorPen } else { $gridPen }
    $g.DrawLine($pen, $x, 0, $x, $src.Height)
    if ($x % 64 -eq 0 -and $x -gt 0) {
        $g.DrawString("$x", $font, $shadowBrush, ($x + 2), 1)
        $g.DrawString("$x", $font, $brush, ($x + 1), 0)
    }
}
for ($y = 0; $y -le $src.Height; $y += $Step) {
    $pen = if ($y % 64 -eq 0) { $majorPen } else { $gridPen }
    $g.DrawLine($pen, 0, $y, $src.Width, $y)
    if ($y % 64 -eq 0 -and $y -gt 0) {
        $g.DrawString("$y", $font, $shadowBrush, 2, ($y + 1))
        $g.DrawString("$y", $font, $brush, 1, ($y))
    }
}

$g.Dispose()
$src.Dispose()
$bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Saved gridded $OutPath ($($Step)px minor, 64px major)"
