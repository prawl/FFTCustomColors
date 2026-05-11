# Render battle_tetsu_spr.bin's pixel area as a PNG using palette 0.
param([int]$Scale = 2)

Add-Type -AssemblyName System.Drawing

$bin = "C:\Users\ptyRa\AppData\Local\FFTSpriteToolkit\working\.FFTSpriteToolkit\data\enhanced\extracted\fftpack\unit\battle_tetsu_spr.bin"
$outDir = "C:\Users\ptyRa\Dev\FFTColorCustomizer\scripts\_tex_decoded\tetsu_render"
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

$bytes = [System.IO.File]::ReadAllBytes($bin)
Write-Host "Bin size: $($bytes.Length) bytes"

$palette = New-Object System.Drawing.Color[] 16
for ($i = 0; $i -lt 16; $i++) {
    $v = $bytes[$i*2] -bor ($bytes[$i*2+1] -shl 8)
    $r = (($v -band 0x1F) * 255 / 31) -as [int]
    $g = ((($v -shr 5) -band 0x1F) * 255 / 31) -as [int]
    $b = ((($v -shr 10) -band 0x1F) * 255 / 31) -as [int]
    $a = if ($i -eq 0) { 0 } else { 255 }
    $palette[$i] = [System.Drawing.Color]::FromArgb($a, $r, $g, $b)
}
Write-Host "Palette idx1=$($palette[1]) idx5=$($palette[5]) idx8=$($palette[8])"

$dataStart = 512
$pixelBytes = $bytes.Length - $dataStart
$width = 256
$bytesPerRow = [int]($width / 2)
$height = [int]($pixelBytes / $bytesPerRow)
Write-Host "Width=$width Height=$height BytesPerRow=$bytesPerRow"

$bmp = New-Object System.Drawing.Bitmap $width, $height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
for ($y = 0; $y -lt $height; $y++) {
    for ($x = 0; $x -lt $width; $x++) {
        $pixelIdx = $y * $width + $x
        $byteIdx = $dataStart + [int]($pixelIdx / 2)
        if ($byteIdx -ge $bytes.Length) { break }
        $pd = $bytes[$byteIdx]
        $colorIdx = if ($pixelIdx % 2 -eq 0) { $pd -band 0x0F } else { ($pd -shr 4) -band 0x0F }
        $bmp.SetPixel($x, $y, $palette[$colorIdx])
    }
}

$nativePath = Join-Path $outDir "tetsu_native.png"
$bmp.Save($nativePath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Saved native: $nativePath"

if ($Scale -gt 1) {
    $hdW = $width * $Scale
    $hdH = $height * $Scale
    $hd = New-Object System.Drawing.Bitmap $hdW, $hdH
    $g = [System.Drawing.Graphics]::FromImage($hd)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.DrawImage($bmp, 0, 0, $hdW, $hdH)
    $g.Dispose()
    $hdPath = Join-Path $outDir "tetsu_hd.png"
    $hd.Save($hdPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $hd.Dispose()
    Write-Host "Saved scaled: $hdPath"
}
$bmp.Dispose()
