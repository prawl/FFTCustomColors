# FFT Color Mod - Quick Build & Deploy Script
# Builds the mod with IL trimming and deploys directly to Reloaded-II mods folder

# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

Write-Host "Building FFT Color Mod..." -ForegroundColor Green

# Clean existing installation
$modPath = "$env:RELOADEDIIMODS/FFT_Color_Mod"
if (Test-Path $modPath) {
    Write-Host "Removing existing mod installation..." -ForegroundColor Yellow
    Remove-Item "$modPath/*" -Force -Recurse -ErrorAction SilentlyContinue
}

# Build and publish with IL trimming for smaller size
Write-Host "Publishing to Reloaded-II mods folder..." -ForegroundColor Cyan
dotnet publish "./FFTColorMod.csproj" -c Release -o "$modPath" /p:OutputPath="./bin/Release" /p:ReloadedILLink="true"

if ($LASTEXITCODE -eq 0) {
    # TLDR: Copy ModConfig.json so Reloaded recognizes the mod
    Write-Host "Copying ModConfig.json..." -ForegroundColor Cyan
    Copy-Item "ModConfig.json" "$modPath/ModConfig.json" -Force

    # Copy Preview.png if it exists
    if (Test-Path "Preview.png") {
        Copy-Item "Preview.png" "$modPath/Preview.png" -Force
    }

    # TLDR: Copy the FFTIVC directory with all color PAC files and sprites
    if (Test-Path "FFTIVC") {
        Write-Host "Copying color variant PAC files and sprites..." -ForegroundColor Cyan
        Copy-Item "FFTIVC" "$modPath" -Recurse -Force

        # Also copy to data/enhanced for the new switching mechanism
        $enhancedPath = "$modPath/data/enhanced"
        if (Test-Path "FFTIVC/data/enhanced") {
            New-Item -ItemType Directory -Force -Path $enhancedPath | Out-Null
            Copy-Item "FFTIVC/data/enhanced/*.pac" $enhancedPath -Force
            Write-Host "Copied $(Get-ChildItem FFTIVC/data/enhanced/*.pac | Measure-Object).Count PAC files" -ForegroundColor Green
        }

        # Copy individual sprite files to fftpack/unit directory
        $spritePath = "$modPath/FFTIVC/data/enhanced/fftpack/unit"
        if (Test-Path "FFTIVC/data/enhanced/fftpack/unit") {
            Write-Host "Copying individual sprite files..." -ForegroundColor Cyan
            New-Item -ItemType Directory -Force -Path $spritePath | Out-Null
            Copy-Item "FFTIVC/data/enhanced/fftpack/unit/*.bin" $spritePath -Force
            $spriteCount = (Get-ChildItem "FFTIVC/data/enhanced/fftpack/unit/*.bin" | Measure-Object).Count
            Write-Host "Copied $spriteCount sprite files to fftpack/unit" -ForegroundColor Green

            # TLDR: Copy better_palettes color variant directories
            Write-Host "Copying color variant directories..." -ForegroundColor Cyan
            $colorVariants = @("sprites_original", "sprites_default", "sprites_corpse_brigade", "sprites_lucavi", "sprites_northern_sky", "sprites_smoke", "sprites_southern_sky")
            foreach ($variant in $colorVariants) {
                $sourcePath = "FFTIVC/data/enhanced/fftpack/unit/$variant"
                $destPath = "$spritePath/$variant"

                if (Test-Path $sourcePath) {
                    New-Item -ItemType Directory -Force -Path $destPath | Out-Null
                    Copy-Item "$sourcePath/*" $destPath -Force -Recurse
                    $variantCount = (Get-ChildItem "$sourcePath/*.bin" -ErrorAction SilentlyContinue | Measure-Object).Count
                    Write-Host "  Copied $variantCount files to $variant" -ForegroundColor Green
                }
            }
        }
    }

    Write-Host "Build successful! Mod installed to: $modPath" -ForegroundColor Green
    Write-Host "You can now enable the mod in Reloaded-II" -ForegroundColor Green
} else {
    Write-Host "Build failed! Check the output above for errors." -ForegroundColor Red
}

# Restore Working Directory
Pop-Location