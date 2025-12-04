# FFT Color Mod - Extract and Process Sprites Script

$gamePath = "C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles"
$outputPath = "C:\Users\ptyRa\Dev\FFT_Color_Mod\FFTIVC\data"

Write-Host "FFT Color Mod - Sprite Extraction and Processing" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

# Create output directories
$directories = @("sprites_red", "sprites_blue", "sprites_green", "sprites_purple", "sprites_original")
foreach ($dir in $directories) {
    $fullPath = Join-Path $outputPath $dir
    if (!(Test-Path $fullPath)) {
        New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
        Write-Host "Created directory: $dir" -ForegroundColor Green
    }
}

# Try extracting from smaller PAC files
$pacFiles = @(
    "$gamePath\data\classic\0000.pac",
    "$gamePath\data\classic\0002.pac",
    "$gamePath\data\classic\0004.pac",
    "$gamePath\data\classic\0005.pac"
)

$tempExtractPath = "C:\Users\ptyRa\Dev\FFT_Color_Mod\temp_sprites"
if (!(Test-Path $tempExtractPath)) {
    New-Item -ItemType Directory -Path $tempExtractPath -Force | Out-Null
}

foreach ($pacFile in $pacFiles) {
    if (Test-Path $pacFile) {
        $fileName = Split-Path $pacFile -Leaf
        Write-Host "`nProcessing $fileName..." -ForegroundColor Yellow

        # Extract sprites from this PAC file
        & dotnet run --project FFTColorMod.csproj -- extract-single "$pacFile" "$tempExtractPath"

        if ($?) {
            Write-Host "Successfully extracted from $fileName" -ForegroundColor Green

            # Process each extracted sprite
            $sprites = Get-ChildItem "$tempExtractPath\*.SPR" -ErrorAction SilentlyContinue

            if ($sprites) {
                Write-Host "Found $($sprites.Count) sprites, generating color variants..." -ForegroundColor Cyan

                foreach ($sprite in $sprites) {
                    & dotnet run --project FFTColorMod.csproj -- process "$($sprite.FullName)" "$outputPath"
                }

                Write-Host "Color variants generated!" -ForegroundColor Green
            }
            else {
                Write-Host "No sprites found in $fileName" -ForegroundColor Yellow
            }
        }
        else {
            Write-Host "Failed to extract from $fileName" -ForegroundColor Red
        }
    }
}

# Clean up temp directory
if (Test-Path $tempExtractPath) {
    Remove-Item $tempExtractPath -Recurse -Force
}

Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "Extraction and processing complete!" -ForegroundColor Green
Write-Host "Color variant sprites are ready in: $outputPath" -ForegroundColor Green