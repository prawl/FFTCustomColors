# FFT Color Mod - PRODUCTION Build & Deploy Script
# Builds the mod with IL trimming and deploys ALL themes to Reloaded-II mods folder
# WARNING: This includes ALL themes which may cause F1/F2 crashes if too many are included

Write-Host "============================================" -ForegroundColor Magenta
Write-Host "       PRODUCTION BUILD SCRIPT              " -ForegroundColor Magenta
Write-Host "   This will deploy ALL available themes   " -ForegroundColor Yellow
Write-Host "============================================" -ForegroundColor Magenta
Write-Host ""

# Confirm production build
$confirmation = Read-Host "Are you sure you want to build for PRODUCTION? (yes/no)"
if ($confirmation -ne "yes") {
    Write-Host "Production build cancelled." -ForegroundColor Yellow
    exit 0
}

# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

Write-Host "Building FFT Color Mod for PRODUCTION..." -ForegroundColor Green

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

    # Copy Config.json for configuration settings
    if (Test-Path "ColorMod/Config.json") {
        Write-Host "Copying Config.json..." -ForegroundColor Cyan
        Copy-Item "ColorMod/Config.json" "$modPath/Config.json" -Force
    }

    # Create User config directory and copy configs there
    $userConfigPath = "$gamePath\Reloaded\User\Mods\FFT_Color_Mod"
    Write-Host "Creating User config directory..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $userConfigPath -Force | Out-Null

    Write-Host "Copying configuration files to User directory..." -ForegroundColor Cyan
    if (Test-Path "$scriptDir\Config.json") {
        Copy-Item "$scriptDir\Config.json" -Destination "$userConfigPath\Config.json" -Force
        Write-Host "  Copied Config.json to User directory" -ForegroundColor Green
    }
    if (Test-Path "$scriptDir\ModUserConfig.json") {
        Copy-Item "$scriptDir\ModUserConfig.json" -Destination "$userConfigPath\ModUserConfig.json" -Force
        Write-Host "  Copied ModUserConfig.json to User directory" -ForegroundColor Green
    }

    # Copy Preview.png if it exists
    if (Test-Path "ColorMod/Preview.png") {
        Copy-Item "ColorMod/Preview.png" "$modPath/Preview.png" -Force
    }

    # TLDR: Copy the FFTIVC directory structure
    if (Test-Path "ColorMod/FFTIVC") {
        Write-Host "Copying color variant PAC files and sprites..." -ForegroundColor Cyan

        # Copy to data/enhanced for the new switching mechanism
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

            # TLDR: Copy ALL color variant directories for PRODUCTION
            Write-Host "Copying ALL color variant directories (PRODUCTION MODE)..." -ForegroundColor Magenta

            # Get ALL sprite directories (both generic and story characters)
            $allVariants = Get-ChildItem "ColorMod/FFTIVC/data/enhanced/fftpack/unit" -Directory -Filter "sprites_*" -ErrorAction SilentlyContinue |
                ForEach-Object { $_.Name }

            $genericThemeCount = 0
            $orlandeauThemeCount = 0
            $otherStoryThemeCount = 0

            Write-Host "`nFound $($allVariants.Count) total theme directories to deploy:" -ForegroundColor Yellow

            $emptyDirs = @()
            foreach ($variant in $allVariants) {
                # Skip sprites_original - those files are already in the base unit/ directory
                if ($variant -eq "sprites_original") {
                    Write-Host "  Skipping sprites_original (already in base directory)" -ForegroundColor Gray
                    continue
                }

                # Count theme types
                if ($variant -like "sprites_orlandeau_*") {
                    $orlandeauThemeCount++
                } elseif ($variant -like "sprites_*_*") {
                    # Might be another story character in the future
                    $otherStoryThemeCount++
                } else {
                    $genericThemeCount++
                }

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

                        # Color code by type
                        if ($variant -like "sprites_orlandeau_*") {
                            Write-Host "  [Orlandeau] $variant has $variantCount modified sprite(s)" -ForegroundColor Magenta
                        } else {
                            Write-Host "  [Generic] $variant has $variantCount modified sprite(s)" -ForegroundColor Green
                        }
                    } else {
                        Write-Host "  Variant $variant has no modified sprites - will be deleted" -ForegroundColor Yellow
                        $emptyDirs += $destPath
                    }
                } else {
                    Write-Host "  Variant directory $variant not found in source" -ForegroundColor Yellow
                }
            }

            Write-Host "`nTheme Summary:" -ForegroundColor Cyan
            Write-Host "  Generic Themes: $genericThemeCount" -ForegroundColor Green
            Write-Host "  Orlandeau Themes: $orlandeauThemeCount" -ForegroundColor Magenta
            if ($otherStoryThemeCount -gt 0) {
                Write-Host "  Other Story Themes: $otherStoryThemeCount" -ForegroundColor Blue
            }

            # Delete empty sprite directories
            if ($emptyDirs.Count -gt 0) {
                Write-Host "`nCleaning up empty sprite directories..." -ForegroundColor Yellow
                foreach ($emptyDir in $emptyDirs) {
                    Remove-Item $emptyDir -Force -Recurse -ErrorAction SilentlyContinue
                    Write-Host "  Deleted: $(Split-Path $emptyDir -Leaf)" -ForegroundColor Gray
                }
            }

            # Warning about theme count
            if ($genericThemeCount -gt 10) {
                Write-Host "`nWARNING: $genericThemeCount generic themes deployed!" -ForegroundColor Red
                Write-Host "This may cause F1/F2 cycling issues or crashes." -ForegroundColor Red
                Write-Host "Consider limiting themes for stability." -ForegroundColor Yellow
            }
        }
    }

    # TLDR: Verify deployment succeeded
    Write-Host "`nVerifying PRODUCTION deployment..." -ForegroundColor Cyan
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
    $colorVariants = Get-ChildItem "$mainSpriteDir" -Directory -Filter "sprites_*" -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ne "sprites_original" -and $_.Name -ne "sprites_default" } |
        ForEach-Object { $_.Name }

    $deployedGeneric = 0
    $deployedOrlandeau = 0

    foreach ($variant in $colorVariants) {
        $variantDir = "$mainSpriteDir/$variant"
        if (Test-Path $variantDir) {
            $variantSpriteCount = (Get-ChildItem "$variantDir/*.bin" -ErrorAction SilentlyContinue | Measure-Object).Count
            if ($variantSpriteCount -eq 0) {
                $verificationErrors += "Variant $variant exists but has no sprite files (should have been deleted)"
            } else {
                if ($variant -like "sprites_orlandeau_*") {
                    $deployedOrlandeau++
                } else {
                    $deployedGeneric++
                }
            }
        }
    }

    Write-Host "  [OK] Deployed $deployedGeneric generic theme(s)" -ForegroundColor Green
    Write-Host "  [OK] Deployed $deployedOrlandeau Orlandeau theme(s)" -ForegroundColor Magenta

    # Check ModConfig.json
    if (!(Test-Path "$modPath/ModConfig.json")) {
        $verificationErrors += "ModConfig.json missing"
    } else {
        Write-Host "  [OK] ModConfig.json present" -ForegroundColor Green
    }

    # Report results
    if ($verificationErrors.Count -eq 0) {
        Write-Host "`n========================================" -ForegroundColor Green
        Write-Host "PRODUCTION BUILD SUCCESSFUL!" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "Mod installed to: $modPath" -ForegroundColor Green
        Write-Host "Deployed $deployedGeneric generic themes" -ForegroundColor Cyan
        Write-Host "Deployed $deployedOrlandeau Orlandeau themes" -ForegroundColor Magenta
        Write-Host "All sprite files verified and ready." -ForegroundColor Green
        Write-Host "You can now enable the mod in Reloaded-II" -ForegroundColor Green
    } else {
        Write-Host "`nPRODUCTION BUILD VERIFICATION FAILED!" -ForegroundColor Red
        Write-Host "The following errors were detected:" -ForegroundColor Red
        foreach ($verifyError in $verificationErrors) {
            Write-Host "  X $verifyError" -ForegroundColor Red
        }
        Write-Host "`nPlease check your source files and try again." -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "PRODUCTION build failed! Check the output above for errors." -ForegroundColor Red
}

# Restore Working Directory
Pop-Location