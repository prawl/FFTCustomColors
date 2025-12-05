# PowerShell script to generate color variants for ALL sprites
# This will process all 178 sprites in the input_sprites folder

Write-Host "FFT Color Mod - Generate All Color Variants" -ForegroundColor Yellow
Write-Host "===========================================" -ForegroundColor Yellow

$inputPath = "input_sprites"
$outputPath = "FFTIVC\data\enhanced\fftpack\unit"

# Check if input directory exists
if (!(Test-Path $inputPath)) {
    Write-Host "Error: Input directory not found: $inputPath" -ForegroundColor Red
    exit 1
}

# Get all sprite files
$sprites = Get-ChildItem "$inputPath\*.bin"

Write-Host "Found $($sprites.Count) sprites to process" -ForegroundColor Cyan
Write-Host ""

# Process sprites using Program.cs process command
Write-Host "Processing all sprites..." -ForegroundColor Green
& dotnet run --project FFTColorMod.csproj -- process $inputPath $outputPath

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ SUCCESS: All sprites processed!" -ForegroundColor Green

    # Count generated files
    $redCount = (Get-ChildItem "$outputPath\sprites_red\*.bin" 2>$null).Count
    $blueCount = (Get-ChildItem "$outputPath\sprites_blue\*.bin" 2>$null).Count
    $greenCount = (Get-ChildItem "$outputPath\sprites_green\*.bin" 2>$null).Count
    $originalCount = (Get-ChildItem "$outputPath\sprites_original\*.bin" 2>$null).Count

    Write-Host ""
    Write-Host "Generated files:" -ForegroundColor Cyan
    Write-Host "  Red variants:      $redCount" -ForegroundColor Red
    Write-Host "  Blue variants:     $blueCount" -ForegroundColor Blue
    Write-Host "  Green variants:    $greenCount" -ForegroundColor Green
    Write-Host "  Original copies:   $originalCount" -ForegroundColor Gray

    Write-Host ""
    Write-Host "Color variants are now ready for use!" -ForegroundColor Green
    Write-Host "Use number keys 1-4 in-game to switch colors:" -ForegroundColor Yellow
    Write-Host "  1 = Red" -ForegroundColor Red
    Write-Host "  2 = Blue" -ForegroundColor Blue
    Write-Host "  3 = Green" -ForegroundColor Green
    Write-Host "  4 = Original" -ForegroundColor Gray
} else {
    Write-Host "❌ FAILED: Error processing sprites" -ForegroundColor Red
    exit 1
}