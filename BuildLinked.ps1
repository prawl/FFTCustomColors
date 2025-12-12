# FFT Color Mod - DEV Build & Deploy Script
# Builds the mod with IL trimming and deploys LIMITED themes for testing
# For production builds with ALL themes, use BuildLinked.Production.ps1

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "         DEV BUILD SCRIPT                   " -ForegroundColor Cyan
Write-Host "   Deploys 5 generic + Orlandeau themes    " -ForegroundColor Yellow
Write-Host "   (Limited to prevent F1/F2 crashes)      " -ForegroundColor Yellow
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

Write-Host "Building FFT Color Mod (DEV MODE)..." -ForegroundColor Green

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

    # TLDR: Copy the FFTIVC directory structure but NOT the sprites_* directories
    if (Test-Path "ColorMod/FFTIVC") {
        Write-Host "Copying color variant PAC files and sprites..." -ForegroundColor Cyan
        # Don't copy the entire directory - we'll copy sprites selectively below

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

            # TLDR: Copy color variant directories from ColorMod/FFTIVC
            Write-Host "Copying color variant directories (DEV MODE)..." -ForegroundColor Cyan

            # Core themes to deploy (limit to 5 to prevent F1/F2 crashes in DEV)
            $coreThemes = @(
                "sprites_original",
                "sprites_corpse_brigade",
                "sprites_lucavi",
                "sprites_northern_sky",
                "sprites_smoke"
            )
            Write-Host "  DEV: Limiting to 5 generic themes for stability" -ForegroundColor Yellow

            # Get all Orlandeau themes (these are separate from generic themes)
            $orlandeauThemes = Get-ChildItem "ColorMod/FFTIVC/data/enhanced/fftpack/unit" -Directory -Filter "sprites_orlandeau_*" -ErrorAction SilentlyContinue |
                ForEach-Object { $_.Name }

            if ($orlandeauThemes.Count -gt 0) {
                Write-Host "  DEV: Including $($orlandeauThemes.Count) Orlandeau theme(s)" -ForegroundColor Magenta
            }

            # Combine core themes and Orlandeau themes
            # Only copy the core themes for stability (5 generic + all Orlandeau)
            $colorVariants = Get-ChildItem "ColorMod/FFTIVC/data/enhanced/fftpack/unit" -Directory -Filter "sprites_*" -ErrorAction SilentlyContinue |
                Where-Object { $coreThemes -contains $_.Name -or $_.Name -like "sprites_orlandeau_*" } |
                ForEach-Object { $_.Name }

            foreach ($variant in $colorVariants) {
                # Skip sprites_original - those files are already in the base unit/ directory
                if ($variant -eq "sprites_original") {
                    Write-Host "  Skipping sprites_original (already in base directory)" -ForegroundColor Gray
                    continue
                }

                $sourcePath = "ColorMod/FFTIVC/data/enhanced/fftpack/unit/$variant"
                $destPath = "$spritePath/$variant"

                # Copy theme directory and ensure it has ALL sprites
                if (Test-Path $sourcePath) {
                    Write-Host "  Processing $variant..." -ForegroundColor Cyan

                    # Create destination directory
                    New-Item -ItemType Directory -Force -Path $destPath | Out-Null

                    # First, copy ALL base sprites from SOURCE (excluding story characters) to ensure complete set
                    # Use the source directory for base sprites, not the deployment directory
                    $sourceBasePath = "ColorMod/FFTIVC/data/enhanced/fftpack/unit"
                    $baseSprites = Get-ChildItem "$sourceBasePath/*.bin" -ErrorAction SilentlyContinue |
                        Where-Object { $_.Name -notmatch "aguri|kanba|oru|musu|dily|hime|aruma|rafa|mara|cloud|beio|reze" }

                    $baseCount = $baseSprites.Count
                    if ($variant -like "sprites_orlandeau_*") {
                        # For Orlandeau themes, include all base sprites plus Orlandeau
                        $baseSprites = Get-ChildItem "$sourceBasePath/*.bin" -ErrorAction SilentlyContinue |
                            Where-Object { $_.Name -notmatch "aguri|kanba|musu|dily|hime|aruma|rafa|mara|cloud|beio|reze" -or $_.Name -match "oru" }
                        $baseCount = $baseSprites.Count
                    }

                    # Copy all base sprites first
                    if ($baseSprites.Count -gt 0) {
                        Copy-Item $baseSprites.FullName $destPath -Force
                    }

                    # Then overlay the modified sprites from the source directory
                    $modifiedCount = 0
                    $modifiedSprites = Get-ChildItem "$sourcePath/*.bin" -ErrorAction SilentlyContinue
                    if ($modifiedSprites.Count -gt 0) {
                        Copy-Item "$sourcePath/*.bin" $destPath -Force
                        $modifiedCount = $modifiedSprites.Count
                    }

                    $totalCount = (Get-ChildItem "$destPath/*.bin" -ErrorAction SilentlyContinue | Measure-Object).Count

                    if ($variant -like "sprites_orlandeau_*") {
                        Write-Host "    [Orlandeau] ${variant}: $modifiedCount modified, $totalCount total sprites" -ForegroundColor Magenta
                    } else {
                        Write-Host "    [Generic] ${variant}: $modifiedCount modified, $totalCount total sprites" -ForegroundColor Green
                    }

                    # Verify expected count
                    # 121 generic sprites for generic themes
                    # 124 sprites for Orlandeau themes (121 generic + 3 Orlandeau variants: oru, goru, voru)
                    $expectedCount = if ($variant -like "sprites_orlandeau_*") { 124 } else { 121 }
                    if ($totalCount -ne $expectedCount) {
                        Write-Host "    WARNING: Expected $expectedCount sprites but found $totalCount" -ForegroundColor Yellow
                    }
                } else {
                    Write-Host "  Variant directory $variant not found in source" -ForegroundColor Yellow
                }
            }

            # Check for and remove any empty sprite directories
            Write-Host "`nChecking for empty sprite directories..." -ForegroundColor Yellow
            $allDirs = Get-ChildItem "$spritePath" -Directory -Filter "sprites_*" -ErrorAction SilentlyContinue
            $emptyDirs = @()
            foreach ($dir in $allDirs) {
                $fileCount = (Get-ChildItem "$($dir.FullName)/*.bin" -ErrorAction SilentlyContinue | Measure-Object).Count
                if ($fileCount -eq 0) {
                    $emptyDirs += $dir.FullName
                    Write-Host "  Found empty directory: $($dir.Name)" -ForegroundColor Red
                }
            }

            if ($emptyDirs.Count -gt 0) {
                Write-Host "  Removing $($emptyDirs.Count) empty directories..." -ForegroundColor Yellow
                foreach ($emptyDir in $emptyDirs) {
                    Remove-Item $emptyDir -Force -Recurse -ErrorAction SilentlyContinue
                    Write-Host "    Deleted: $(Split-Path $emptyDir -Leaf)" -ForegroundColor Gray
                }
            } else {
                Write-Host "  No empty directories found" -ForegroundColor Green
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

    # Check expected color variant directories based on what we tried to copy
    Write-Host "`nVerifying theme directories..." -ForegroundColor Cyan

    # Expected themes in DEV mode
    $expectedGenericThemes = @("sprites_corpse_brigade", "sprites_lucavi", "sprites_northern_sky", "sprites_smoke")
    $expectedOrlandeauThemes = Get-ChildItem "ColorMod/FFTIVC/data/enhanced/fftpack/unit" -Directory -Filter "sprites_orlandeau_*" -ErrorAction SilentlyContinue |
        ForEach-Object { $_.Name }

    # Check generic themes (should have 121 sprites each - all generics, no story characters)
    $expectedGenericCount = 121
    foreach ($theme in $expectedGenericThemes) {
        $themeDir = "$mainSpriteDir/$theme"
        if (!(Test-Path $themeDir)) {
            $verificationErrors += "Generic theme directory missing: $theme"
        } else {
            $spriteCount = (Get-ChildItem "$themeDir/*.bin" -ErrorAction SilentlyContinue | Measure-Object).Count
            if ($spriteCount -eq 0) {
                $verificationErrors += "Generic theme $theme has no sprite files"
            } elseif ($spriteCount -ne $expectedGenericCount) {
                $verificationErrors += "Generic theme $theme has $spriteCount sprites (expected $expectedGenericCount)"
                Write-Host "  [WARN] Generic theme $theme has $spriteCount sprites (expected $expectedGenericCount)" -ForegroundColor Yellow
            } else {
                Write-Host "  [OK] Generic theme $theme has $spriteCount sprites" -ForegroundColor Green
            }
        }
    }

    # Check Orlandeau themes (should have 124 sprites each - all generics + 3 Orlandeau variants)
    $expectedOrlandeauCount = 124
    foreach ($theme in $expectedOrlandeauThemes) {
        $themeDir = "$mainSpriteDir/$theme"
        if (!(Test-Path $themeDir)) {
            $verificationErrors += "Orlandeau theme directory missing: $theme"
        } else {
            $spriteCount = (Get-ChildItem "$themeDir/*.bin" -ErrorAction SilentlyContinue | Measure-Object).Count
            if ($spriteCount -eq 0) {
                $verificationErrors += "Orlandeau theme $theme has no sprite files"
            } elseif ($spriteCount -ne $expectedOrlandeauCount) {
                $verificationErrors += "Orlandeau theme $theme has $spriteCount sprites (expected $expectedOrlandeauCount)"
                Write-Host "  [WARN] Orlandeau theme $theme has $spriteCount sprites (expected $expectedOrlandeauCount)" -ForegroundColor Yellow
            } else {
                Write-Host "  [OK] Orlandeau theme $theme has $spriteCount sprites" -ForegroundColor Magenta
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