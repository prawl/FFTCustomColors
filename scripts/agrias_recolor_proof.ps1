# End-to-end visual proof of TEX recoloring for Agrias.
# 1. Extract dominant unique BGR555 colors from vanilla tex_880
# 2. Build a color map (blue tunic -> red, orange hair -> green)
# 3. Apply by calling our new ModifyTexColorsWithMap-equivalent in PowerShell
#    (the C# method works on file paths; we mirror the logic inline so we can iterate)
# 4. Decode result to PNG for visual comparison

$vanillaTex = "C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\Reloaded\Mods\original_squire_v2\FFTIVC\data\enhanced\system\ffto\g2d\tex_880.bin"
$outBin     = "C:\Users\ptyRa\Dev\FFTColorCustomizer\scripts\_tex_decoded\agrias_recolored\tex_880.bin"
$outDir     = Split-Path $outBin -Parent
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

$bytes = [System.IO.File]::ReadAllBytes($vanillaTex)

function Bgr555ToRgb($v) {
    $b = (($v -band 0x1F) -shl 3)
    $g = ((($v -shr 5) -band 0x1F) -shl 3)
    $r = ((($v -shr 10) -band 0x1F) -shl 3)
    return @{R=$r; G=$g; B=$b}
}
function RgbToBgr555($r, $g, $b) {
    $r5 = ($r -shr 3) -band 0x1F
    $g5 = ($g -shr 3) -band 0x1F
    $b5 = ($b -shr 3) -band 0x1F
    return [int]($b5 -bor ($g5 -shl 5) -bor ($r5 -shl 10))
}

# Step 1: histogram of unique non-zero BGR555 values in vanilla TEX
$hist = @{}
for ($i = 0; $i -lt $bytes.Length - 1; $i += 2) {
    $v = [int]($bytes[$i] -bor ($bytes[$i+1] -shl 8))
    if ($v -eq 0) { continue }
    if ($hist.ContainsKey($v)) { $hist[$v]++ } else { $hist[$v] = 1 }
}
Write-Host "Unique non-zero colors in vanilla tex_880: $($hist.Count)"
Write-Host "Top 16 by pixel count:"
$top = $hist.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 16
foreach ($e in $top) {
    $c = Bgr555ToRgb $e.Key
    "  0x{0:X4} count={1,-6} -> RGB({2},{3},{4})" -f $e.Key,$e.Value,$c.R,$c.G,$c.B
}

# Step 2: hand-pick recolors based on visual identification.
# Agrias armor is blue tones; let's map blue family -> red family.
# Iterate ALL unique colors; for those with B > R+30 (blue dominant), shift to red dominant.
$map = @{}
foreach ($e in $hist.GetEnumerator()) {
    $c = Bgr555ToRgb $e.Key
    if ($c.B -gt ($c.R + 30)) {
        # Blue-dominant pixel: swap R and B to make it red-dominant
        $new = RgbToBgr555 $c.B $c.G $c.R
        $map[$e.Key] = $new
    }
}
Write-Host ""
Write-Host "Map entries (blue->red swap): $($map.Count)"

# Step 3: apply
$output = $bytes.Clone()
$changes = 0
for ($i = 0; $i -lt $output.Length - 1; $i += 2) {
    $v = [int]($output[$i] -bor ($output[$i+1] -shl 8))
    if ($map.ContainsKey($v)) {
        $nv = $map[$v]
        $output[$i]   = [byte]($nv -band 0xFF)
        $output[$i+1] = [byte](($nv -shr 8) -band 0xFF)
        $changes++
    }
}
[System.IO.File]::WriteAllBytes($outBin, $output)
Write-Host "Wrote $outBin ($changes pixels changed)"
Write-Host ""
Write-Host "Now decode original + recolored to PNG and view side by side..."
& "C:\Users\ptyRa\Dev\FFTColorCustomizer\scripts\decode_tex_to_png.ps1" -SrcDir $outDir -OutDir $outDir -Filter "tex_880.bin"
