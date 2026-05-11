# Build a contact sheet of all wldface_NNN_08_uitx.bmp portraits with ID labels.
# Lets a human visually scan to identify a character by number.

param(
    [string]$Src = "C:\Users\ptyRa\OneDrive\Desktop\Extracted Game Files\extracted_portraits_tex",
    [string]$Out = "C:\Users\ptyRa\Dev\FFTColorCustomizer\scripts\_tex_decoded\wldface_contact_sheet.png",
    [int]$ThumbW = 80,
    [int]$ThumbH = 80,
    [int]$Cols = 10
)

Add-Type -AssemblyName System.Drawing

$files = Get-ChildItem $Src -Filter "wldface_*_08_uitx.bmp" | Sort-Object Name
Write-Host "Found $($files.Count) wldface _08 portraits"

$rows = [Math]::Ceiling($files.Count / $Cols)
$labelH = 16
$sheet = New-Object System.Drawing.Bitmap ($ThumbW * $Cols), ($rows * ($ThumbH + $labelH))
$g = [System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.Color]::FromArgb(30, 30, 30))
$font = New-Object System.Drawing.Font "Consolas", 9, ([System.Drawing.FontStyle]::Bold)

for ($i = 0; $i -lt $files.Count; $i++) {
    $row = [int]($i / $Cols)
    $col = $i % $Cols
    $x = $col * $ThumbW
    $y = $row * ($ThumbH + $labelH)

    $b = [System.Drawing.Bitmap]::FromFile($files[$i].FullName)
    $g.DrawImage($b, $x, $y, $ThumbW, $ThumbH)
    $b.Dispose()

    if ($files[$i].BaseName -match '_(\d+)_') {
        $id = $matches[1]
        $g.DrawString($id, $font, [System.Drawing.Brushes]::Yellow, ($x + 4), ($y + $ThumbH))
    }
}

$g.Dispose()
$outDir = Split-Path $Out -Parent
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }
$sheet.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$sheet.Dispose()
Write-Host "Contact sheet: $Out"
