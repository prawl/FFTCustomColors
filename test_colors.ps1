# PowerShell script to test generic color generation
cd "C:\Users\ptyRa\Dev\FFT_Color_Mod"

# Use reflection to call the test method
Add-Type -Path ".\bin\Release\net8.0-windows\FFTColorMod.dll"

$generator = New-Object FFTColorMod.SpriteColorGeneratorV2
$spriteFile = "input_sprites\battle_knight_m_spr.bin"
$outputDir = "test_output"

Write-Host "Testing Generic Color Generation" -ForegroundColor Yellow
Write-Host "================================" -ForegroundColor Yellow
Write-Host "Input: $spriteFile"
Write-Host "Output: $outputDir"

# Generate color variants
$generator.ProcessSingleSprite($spriteFile, $outputDir)

# Read and compare files
$original = [System.IO.File]::ReadAllBytes("$outputDir\sprites_original\battle_knight_m_spr.bin")
$red = [System.IO.File]::ReadAllBytes("$outputDir\sprites_red\battle_knight_m_spr.bin")
$blue = [System.IO.File]::ReadAllBytes("$outputDir\sprites_blue\battle_knight_m_spr.bin")

Write-Host "`nFirst 48 bytes comparison (16 colors in BGR format):" -ForegroundColor Cyan
Write-Host "Original: $([BitConverter]::ToString($original, 0, 48))"
Write-Host "Red:      $([BitConverter]::ToString($red, 0, 48))" -ForegroundColor Red
Write-Host "Blue:     $([BitConverter]::ToString($blue, 0, 48))" -ForegroundColor Blue

# Check for differences
$redDifferent = $false
$blueDifferent = $false

for ($i = 0; $i -lt 96; $i++) {
    if ($original[$i] -ne $red[$i]) {
        $redDifferent = $true
        Write-Host "`nRed differs at byte $i`: $($original[$i]) -> $($red[$i])" -ForegroundColor Red
        break
    }
}

for ($i = 0; $i -lt 96; $i++) {
    if ($original[$i] -ne $blue[$i]) {
        $blueDifferent = $true
        Write-Host "Blue differs at byte $i`: $($original[$i]) -> $($blue[$i])" -ForegroundColor Blue
        break
    }
}

if ($redDifferent -and $blueDifferent) {
    Write-Host "`n✅ SUCCESS: Color variants are working!" -ForegroundColor Green
} else {
    Write-Host "`n❌ FAILED: Color variants not working" -ForegroundColor Red
}