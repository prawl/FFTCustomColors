# Decode FFT IC tex_*.bin files (uncompressed RGB555) to PNG. Fast version (LockBits).
# Usage: ./decode_tex_to_png.ps1 -SrcDir <dir> -OutDir <dir> [-Filter "tex_*.bin"] [-Ids "830,831,..."]

param(
    [Parameter(Mandatory=$true)][string]$SrcDir,
    [Parameter(Mandatory=$true)][string]$OutDir,
    [string]$Filter = "tex_*.bin",
    [string]$Ids = ""
)

Add-Type -AssemblyName System.Drawing
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }

$idFilter = if ($Ids) { ($Ids -split ",") | ForEach-Object { [int]$_ } } else { $null }

$files = Get-ChildItem $SrcDir -Filter $Filter
if ($idFilter) {
    $files = $files | Where-Object {
        $id = [int]($_.BaseName -replace 'tex_','')
        $idFilter -contains $id
    }
}

foreach ($f in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
    $w = 256
    if ($bytes.Length -eq 131072) { $h = 256 }
    elseif ($bytes.Length -eq 118784) { $h = 232 }
    else { $h = [int](($bytes.Length / 2) / $w) }

    $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    $stride = $data.Stride
    $pixelBytes = New-Object byte[] ($stride * $h)

    for ($y = 0; $y -lt $h; $y++) {
        $rowSrc = $y * $w * 2
        $rowDst = $y * $stride
        for ($x = 0; $x -lt $w; $x++) {
            $si = $rowSrc + $x * 2
            if ($si + 1 -ge $bytes.Length) { break }
            $v = $bytes[$si] -bor ($bytes[$si+1] -shl 8)
            $r = (($v -band 0x1F) -shl 3)
            $g = ((($v -shr 5) -band 0x1F) -shl 3)
            $b = ((($v -shr 10) -band 0x1F) -shl 3)
            $a = if ($v -eq 0) { 0 } else { 255 }
            $di = $rowDst + $x * 4
            $pixelBytes[$di]   = [byte]$b
            $pixelBytes[$di+1] = [byte]$g
            $pixelBytes[$di+2] = [byte]$r
            $pixelBytes[$di+3] = [byte]$a
        }
    }

    [System.Runtime.InteropServices.Marshal]::Copy($pixelBytes, 0, $data.Scan0, $pixelBytes.Length)
    $bmp.UnlockBits($data)

    $outPath = Join-Path $OutDir ($f.BaseName + ".png")
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}
Write-Host "Decoded $($files.Count) files into $OutDir"
