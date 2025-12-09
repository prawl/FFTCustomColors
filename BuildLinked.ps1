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
dotnet publish "./ColorMod/FFTColorMod.csproj" -c Release -o "$modPath" /p:OutputPath="./bin/Release" /p:ReloadedILLink="true"

if ($LASTEXITCODE -eq 0) {
    # TLDR: Copy ModConfig.json so Reloaded recognizes the mod
    Write-Host "Copying ModConfig.json..." -ForegroundColor Cyan
    Copy-Item "ColorMod/ModConfig.json" "$modPath/ModConfig.json" -Force

    # Copy Preview.png if it exists
    if (Test-Path "ColorMod/Preview.png") {
        Copy-Item "ColorMod/Preview.png" "$modPath/Preview.png" -Force
    }

    # TLDR: Copy the FFTIVC directory with all color PAC files and sprites
    if (Test-Path "ColorMod/FFTIVC") {
        Write-Host "Copying color variant PAC files and sprites..." -ForegroundColor Cyan
        Copy-Item "ColorMod/FFTIVC" "$modPath" -Recurse -Force

        # Also copy to data/enhanced for the new switching mechanism
        $enhancedPath = "$modPath/data/enhanced"
        if (Test-Path "ColorMod/FFTIVC/data/enhanced") {
            New-Item -ItemType Directory -Force -Path $enhancedPath | Out-Null
            Copy-Item "ColorMod/FFTIVC/data/enhanced/*.pac" $enhancedPath -Force
            Write-Host "Copied $(Get-ChildItem ColorMod/FFTIVC/data/enhanced/*.pac | Measure-Object).Count PAC files" -ForegroundColor Green
        }

        # Copy individual sprite files to fftpack/unit directory
        $spritePath = "$modPath/FFTIVC/data/enhanced/fftpack/unit"
        if (Test-Path "ColorMod/FFTIVC/data/enhanced/fftpack/unit") {
            Write-Host "Copying individual sprite files..." -ForegroundColor Cyan
            New-Item -ItemType Directory -Force -Path $spritePath | Out-Null
            Copy-Item "ColorMod/FFTIVC/data/enhanced/fftpack/unit/*.bin" $spritePath -Force
            $spriteCount = (Get-ChildItem "ColorMod/FFTIVC/data/enhanced/fftpack/unit/*.bin" | Measure-Object).Count
            Write-Host "Copied $spriteCount sprite files to fftpack/unit" -ForegroundColor Green

            # TLDR: Copy better_palettes color variant directories
            Write-Host "Copying color variant directories..." -ForegroundColor Cyan
            # Auto-discover all sprite variant directories
            $colorVariants = Get-ChildItem "ColorMod/FFTIVC/data/enhanced/fftpack/unit" -Directory -Filter "sprites_*" |
                ForEach-Object { $_.Name }

            $emptyDirs = @()
            foreach ($variant in $colorVariants) {
                $sourcePath = "ColorMod/FFTIVC/data/enhanced/fftpack/unit/$variant"
                $destPath = "$spritePath/$variant"

                # Create variant directory
                New-Item -ItemType Directory -Force -Path $destPath | Out-Null

                # Only copy the modified sprites from each variant directory
                if (Test-Path $sourcePath) {
                    $variantSprites = Get-ChildItem "$sourcePath/*.bin" -ErrorAction SilentlyContinue
                    if ($variantSprites.Count -gt 0) {
                        Copy-Item "$sourcePath/*.bin" $destPath -Force
                        $variantCount = (Get-ChildItem "$destPath/*.bin" -ErrorAction SilentlyContinue | Measure-Object).Count
                        Write-Host "  Variant $variant has $variantCount modified sprite(s)" -ForegroundColor Green
                    } else {
                        Write-Host "  Variant $variant has no modified sprites - will be deleted" -ForegroundColor Yellow
                        $emptyDirs += $destPath
                    }
                } else {
                    Write-Host "  Variant directory $variant not found in source" -ForegroundColor Yellow
                }
            }

            # Delete empty sprite directories
            if ($emptyDirs.Count -gt 0) {
                Write-Host "`nCleaning up empty sprite directories..." -ForegroundColor Yellow
                foreach ($emptyDir in $emptyDirs) {
                    Remove-Item $emptyDir -Force -Recurse -ErrorAction SilentlyContinue
                    Write-Host "  Deleted: $(Split-Path $emptyDir -Leaf)" -ForegroundColor Gray
                }
            }
        }
    }

    # TLDR: Verify deployment succeeded
    Write-Host "`nVerifying deployment..." -ForegroundColor Cyan
    $verificationErrors = @()

    # Check main sprite directory
    $mainSpriteDir = "$modPath/FFTIVC/data/enhanced/fftpack/unit"
    if (!(Test-Path $mainSpriteDir)) {
        $verificationErrors += "Main sprite directory missing: $mainSpriteDir"
    } else {
        $mainSpriteCount = (Get-ChildItem "$mainSpriteDir/*.bin" -ErrorAction SilentlyContinue | Measure-Object).Count
        if ($mainSpriteCount -eq 0) {
            $verificationErrors += "No sprite files in main directory: $mainSpriteDir"
        } else {
            Write-Host "  [OK] Main sprite directory has $mainSpriteCount files" -ForegroundColor Green
        }
    }

    # Check each color variant directory
    # Only check directories that exist (empty ones were deleted)
    # Auto-discover all sprite variant directories (excluding sprites_original and sprites_default)
    $colorVariants = Get-ChildItem "$mainSpriteDir" -Directory -Filter "sprites_*" -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ne "sprites_original" -and $_.Name -ne "sprites_default" } |
        ForEach-Object { $_.Name }
    foreach ($variant in $colorVariants) {
        $variantDir = "$mainSpriteDir/$variant"
        # Only verify directories that exist (empty ones should have been deleted)
        if (Test-Path $variantDir) {
            $variantSpriteCount = (Get-ChildItem "$variantDir/*.bin" -ErrorAction SilentlyContinue | Measure-Object).Count
            if ($variantSpriteCount -eq 0) {
                # This shouldn't happen since we delete empty directories
                $verificationErrors += "Variant $variant exists but has no sprite files (should have been deleted)"
            } else {
                Write-Host "  [OK] Variant $variant has $variantSpriteCount modified sprite(s)" -ForegroundColor Green
            }
        }
    }

    # Check ModConfig.json
    if (!(Test-Path "$modPath/ModConfig.json")) {
        $verificationErrors += "ModConfig.json missing"
    } else {
        Write-Host "  [OK] ModConfig.json present" -ForegroundColor Green
    }

    # Report results
    if ($verificationErrors.Count -eq 0) {
        Write-Host "`nBuild successful! Mod installed to: $modPath" -ForegroundColor Green
        Write-Host "All sprite files verified and ready." -ForegroundColor Green
        Write-Host "You can now enable the mod in Reloaded-II" -ForegroundColor Green
    } else {
        Write-Host "`nBUILD VERIFICATION FAILED!" -ForegroundColor Red
        Write-Host "The following errors were detected:" -ForegroundColor Red
        foreach ($verifyError in $verificationErrors) {
            Write-Host "  X $verifyError" -ForegroundColor Red
        }
        Write-Host "`nPlease check your source files and try again." -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "Build failed! Check the output above for errors." -ForegroundColor Red
}

# Restore Working Directory
Pop-Location