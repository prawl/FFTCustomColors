# Overlay a 64x80 frame grid (matching SpriteSheetExtractor's frame size) and label cells
# by their (col, row) coordinates. Highlights current SW (col 1, row 0) and NW (col 3, row 0).
param(
    [Parameter(Mandatory=$true)][string]$SrcPath,
    [Parameter(Mandatory=$true)][string]$OutPath,
    [int]$CellW = 64,
    [int]$CellH = 80
)
Add-Type -AssemblyName System.Drawing
$src = [System.Drawing.Bitmap]::FromFile($SrcPath)
$bmp = New-Object System.Drawing.Bitmap $src.Width, $src.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.DrawImage($src, 0, 0, $src.Width, $src.Height)

$grid = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(200, 0, 255, 255)), 1
$font = New-Object System.Drawing.Font "Consolas", 10, ([System.Drawing.FontStyle]::Bold)
$swPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::Lime), 2
$nwPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::Magenta), 2

$cols = [Math]::Floor($src.Width / $CellW)
$rows = [Math]::Floor($src.Height / $CellH)

# Draw grid lines and (col,row) labels
for ($c = 0; $c -le $cols; $c++) { $g.DrawLine($grid, ($c * $CellW), 0, ($c * $CellW), $src.Height) }
for ($r = 0; $r -le $rows; $r++) { $g.DrawLine($grid, 0, ($r * $CellH), $src.Width, ($r * $CellH)) }
for ($r = 0; $r -lt $rows; $r++) {
    for ($c = 0; $c -lt $cols; $c++) {
        $label = "$c,$r"
        $x = $c * $CellW + 2
        $y = $r * $CellH + 2
        $g.DrawString($label, $font, [System.Drawing.Brushes]::Black, ($x + 1), ($y + 1))
        $g.DrawString($label, $font, [System.Drawing.Brushes]::Yellow, $x, $y)
    }
}

# Highlight current SW (col 1, row 0) in green and NW (col 3, row 0) in magenta
$g.DrawRectangle($swPen, $CellW, 0, $CellW, $CellH)
$g.DrawString("SW", $font, [System.Drawing.Brushes]::Lime, ($CellW + 22), 40)
$g.DrawRectangle($nwPen, ($CellW * 3), 0, $CellW, $CellH)
$g.DrawString("NW", $font, [System.Drawing.Brushes]::Magenta, ($CellW * 3 + 22), 40)

$g.Dispose(); $src.Dispose()
$bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Saved $OutPath ($CellW x $CellH cells, $cols cols x $rows rows)"
