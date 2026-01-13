# FFT Color Mod - DEV Build & Deploy Script
# Builds the mod with IL trimming and deploys ALL themes
# Now includes all generic and story character themes

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "         DEV BUILD SCRIPT                   " -ForegroundColor Cyan
Write-Host "   Deploys ALL generic + story themes      " -ForegroundColor Yellow
Write-Host "   (Full theme deployment enabled)         " -ForegroundColor Yellow
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

Write-Host "Building FFT Color Mod (DEV MODE)..." -ForegroundColor Green

# Clean ALL FFTColorCustomizer installations to prevent conflicts
Write-Host "Cleaning up all FFTColorCustomizer installations..." -ForegroundColor Yellow
$modsDir = "$env:RELOADEDIIMODS"

# Remove any versioned folders (FFTColorCustomizer_v*)
Get-ChildItem "$modsDir" -Filter "FFTColorCustomizer_v*" -Directory | ForEach-Object {
    Write-Host "  Removing versioned installation: $($_.Name)" -ForegroundColor Yellow
    Remove-Item $_.FullName -Force -Recurse -ErrorAction SilentlyContinue
}

# Clean existing dev installation (preserve user themes)
$modPath = "$modsDir/FFTColorCustomizer"
if (Test-Path $modPath) {
    Write-Host "  Cleaning existing dev installation (preserving user themes)..." -ForegroundColor Yellow

    # Backup UserThemes folder and registry if they exist
    $userThemesPath = "$modPath/UserThemes"
    $userThemesJson = "$modPath/UserThemes.json"
    $tempBackupPath = "$env:TEMP/FFTColorCustomizer_UserThemes_Backup"

    if (Test-Path $userThemesPath) {
        Write-Host "  Backing up UserThemes folder..." -ForegroundColor Cyan
        if (Test-Path $tempBackupPath) { Remove-Item $tempBackupPath -Force -Recurse }
        Copy-Item $userThemesPath $tempBackupPath -Recurse -Force
    }
    if (Test-Path $userThemesJson) {
        Write-Host "  Backing up UserThemes.json..." -ForegroundColor Cyan
        Copy-Item $userThemesJson "$tempBackupPath/UserThemes.json" -Force
    }

    # Remove everything
    Remove-Item "$modPath/*" -Force -Recurse -ErrorAction SilentlyContinue

    # Restore UserThemes if backup exists
    if (Test-Path $tempBackupPath) {
        Write-Host "  Restoring user themes..." -ForegroundColor Green
        if (Test-Path "$tempBackupPath/UserThemes.json") {
            Copy-Item "$tempBackupPath/UserThemes.json" $userThemesJson -Force
        }
        # Copy UserThemes folder contents (excluding the backup json we stored there)
        $userThemesFolders = Get-ChildItem $tempBackupPath -Directory
        if ($userThemesFolders.Count -gt 0) {
            New-Item -ItemType Directory -Force -Path $userThemesPath | Out-Null
            foreach ($folder in $userThemesFolders) {
                Copy-Item $folder.FullName "$userThemesPath/$($folder.Name)" -Recurse -Force
            }
            Write-Host "  Restored $($userThemesFolders.Count) user theme job(s)" -ForegroundColor Green
        }
        # Clean up temp backup
        Remove-Item $tempBackupPath -Force -Recurse -ErrorAction SilentlyContinue
    }
}

# Build and publish with IL trimming for smaller size
Write-Host "Publishing to Reloaded-II mods folder..." -ForegroundColor Cyan
dotnet publish "./ColorMod/FFTColorCustomizer.csproj" -c Release -o "$modPath" /p:OutputPath="./bin/Release" /p:ReloadedILLink="true"

if ($LASTEXITCODE -eq 0) {
    # TLDR: Copy ModConfig.json so Reloaded recognizes the mod
    Write-Host "Copying ModConfig.json..." -ForegroundColor Cyan
    Copy-Item "ColorMod/ModConfig.json" "$modPath/ModConfig.json" -Force

    # Copy mod icon from Images directory
    if (Test-Path "ColorMod/Images/thunder_god.png") {
        Write-Host "Copying mod icon (thunder_god.png)..." -ForegroundColor Cyan
        Copy-Item "ColorMod/Images/thunder_god.png" "$modPath/Preview.png" -Force
    } elseif (Test-Path "ColorMod/Preview.png") {
        Write-Host "Copying Preview.png (mod icon)..." -ForegroundColor Cyan
        Copy-Item "ColorMod/Preview.png" "$modPath/Preview.png" -Force
    }

    # Copy Config.json only if it doesn't exist (don't overwrite user's config)
    if (Test-Path "ColorMod/Config.json") {
        if (!(Test-Path "$modPath/Config.json")) {
            Write-Host "Copying default Config.json..." -ForegroundColor Cyan
            Copy-Item "ColorMod/Config.json" "$modPath/Config.json" -Force
        } else {
            Write-Host "Config.json already exists, preserving user settings..." -ForegroundColor Yellow
        }
    }

    # Create User config directory and copy configs there
    $userConfigPath = "$gamePath\Reloaded\User\Mods\FFTColorCustomizer"
    Write-Host "Creating User config directory..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $userConfigPath -Force | Out-Null

    Write-Host "Copying configuration files to User directory..." -ForegroundColor Cyan
    if (Test-Path "$scriptDir\ColorMod\Config.json") {
        Copy-Item "$scriptDir\ColorMod\Config.json" -Destination "$userConfigPath\Config.json" -Force
        Write-Host "  Copied Config.json to User directory" -ForegroundColor Green
    }
    if (Test-Path "$scriptDir\ColorMod\ModUserConfig.json") {
        Copy-Item "$scriptDir\ColorMod\ModUserConfig.json" -Destination "$userConfigPath\ModUserConfig.json" -Force
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

        # Copy original sprite files to fftpack/unit directory
        $spritePath = "$modPath/FFTIVC/data/enhanced/fftpack/unit"
        if (Test-Path "ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_original") {
            Write-Host "Copying original sprite files..." -ForegroundColor Cyan
            New-Item -ItemType Directory -Force -Path $spritePath | Out-Null
            Copy-Item "ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_original/*.bin" $spritePath -Force
            $spriteCount = (Get-ChildItem "ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_original/*.bin" | Measure-Object).Count
            Write-Host "Copied $spriteCount original sprite files to fftpack/unit" -ForegroundColor Green

            # Copy story character themed folders (e.g., sprites_cloud_sephiroth_black)
            Write-Host "Copying story character themed sprites..." -ForegroundColor Cyan
            $storyCharacterFolders = Get-ChildItem "ColorMod/FFTIVC/data/enhanced/fftpack/unit/" -Directory |
                Where-Object { $_.Name -match "sprites_(cloud|agrias|orlandeau|rapha|marach|reis|mustadio|meliadoul|beowulf)_" }

            foreach ($folder in $storyCharacterFolders) {
                $destFolder = "$spritePath/$($folder.Name)"
                New-Item -ItemType Directory -Force -Path $destFolder | Out-Null
                Copy-Item "$($folder.FullName)/*.bin" $destFolder -Force
                Write-Host "  Copied $($folder.Name)" -ForegroundColor Gray
            }

            $storyFolderCount = $storyCharacterFolders.Count
            Write-Host "Copied $storyFolderCount story character theme folders" -ForegroundColor Green

            # Copy system/ffto/g2d tex files if they exist
            $g2dSourcePath = "ColorMod/RamzaThemes"
            $genericG2dPath = "ColorMod/FFTIVC/data/enhanced/system/ffto/g2d"

            Write-Host "Setting up G2D directory..." -ForegroundColor Cyan
            $g2dDestPath = "$modPath/FFTIVC/data/enhanced/system/ffto/g2d"
            New-Item -ItemType Directory -Force -Path $g2dDestPath | Out-Null

            # Copy generic job TEX files (hair highlight fix) from FFTIVC g2d folder
            if (Test-Path $genericG2dPath) {
                $genericTexFiles = Get-ChildItem "$genericG2dPath/*.bin" -File -ErrorAction SilentlyContinue
                if ($genericTexFiles) {
                    $genericTexFiles | Copy-Item -Destination $g2dDestPath -Force
                    $genericTexCount = ($genericTexFiles | Measure-Object).Count
                    Write-Host "Copied $genericTexCount generic job G2D tex files (hair fix)" -ForegroundColor Green
                }
            }

            if (Test-Path $g2dSourcePath) {
                # Don't copy Ramza tex files (830-835) to root - let game use built-in for original theme
                # Only copy other tex files if they exist
                $nonRamzaFiles = Get-ChildItem "$g2dSourcePath/*.bin" -File | Where-Object {
                    $_.Name -notmatch "tex_83[0-5]\.bin"
                }
                if ($nonRamzaFiles) {
                    $nonRamzaFiles | Copy-Item -Destination $g2dDestPath -Force
                    $texCount = ($nonRamzaFiles | Measure-Object).Count
                    Write-Host "Copied $texCount non-Ramza G2D tex files" -ForegroundColor Green
                } else {
                    Write-Host "No tex files to copy (using game built-in for Ramza)" -ForegroundColor Gray
                }

                # Check user's config and copy appropriate Ramza tex files
                $userConfigPath = "C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\Reloaded\User\Mods\paxtrick.fft.colorcustomizer\Config.json"
                if (Test-Path $userConfigPath) {
                    $userConfig = Get-Content $userConfigPath | ConvertFrom-Json
                    $ramzaTheme = $userConfig.RamzaChapter1

                    if ($ramzaTheme -and $ramzaTheme -ne "original") {
                        Write-Host "  Deploying Ramza tex files for theme: $ramzaTheme" -ForegroundColor Cyan

                        # Copy tex files from source theme directory
                        $themeTexPath = "$g2dSourcePath/$ramzaTheme"
                        if (Test-Path $themeTexPath) {
                            Copy-Item "$themeTexPath/tex_83*.bin" $g2dDestPath -Force
                            $texCount = (Get-ChildItem "$g2dDestPath/tex_83*.bin" | Measure-Object).Count
                            Write-Host "  Copied $texCount Ramza tex files for $ramzaTheme theme" -ForegroundColor Green
                        }
                    } else {
                        Write-Host "  Ramza set to original theme - no tex files deployed" -ForegroundColor Gray
                    }
                } else {
                    Write-Host "  No user config found - tex themes will be managed at runtime" -ForegroundColor Yellow
                }
            }

            # Copy generic job themed folders (e.g., sprites_crimson_red, sprites_lucavi)
            Write-Host "Copying generic job themed sprites..." -ForegroundColor Cyan
            $genericThemeFolders = Get-ChildItem "ColorMod/FFTIVC/data/enhanced/fftpack/unit/" -Directory |
                Where-Object { $_.Name -match "^sprites_[^_]+$" -or $_.Name -match "^sprites_(crimson_red|lucavi|northern_sky|southern_sky|amethyst|celestial|corpse_brigade|emerald_dragon|frost_knight|golden_templar|blood_moon|volcanic|ocean_depths|royal_purple|phoenix_flame|rose_gold|silver_knight|shadow_assassin)" }

            # Copy job-specific themed folders (e.g., sprites_knight_h78)
            Write-Host "Copying job-specific themed sprites..." -ForegroundColor Cyan
            $jobSpecificFolders = Get-ChildItem "ColorMod/FFTIVC/data/enhanced/fftpack/unit/" -Directory |
                Where-Object { $_.Name -match "^sprites_(knight|squire|monk|whitemage|blackmage|timemage|summoner|thief|mediator|mystic|geomancer|dragoon|samurai|ninja|calculator|bard|dancer|mime|archer|chemist)_" -and $_.Name -notmatch "(agrias|cloud|orlandeau|rapha|marach|reis|mustadio|meliadoul|beowulf)" }

            foreach ($folder in $genericThemeFolders) {
                $destFolder = "$spritePath/$($folder.Name)"
                New-Item -ItemType Directory -Force -Path $destFolder | Out-Null
                Copy-Item "$($folder.FullName)/*.bin" $destFolder -Force
                Write-Host "  Copied $($folder.Name)" -ForegroundColor Gray
            }

            $genericFolderCount = $genericThemeFolders.Count
            Write-Host "Copied $genericFolderCount generic job theme folders" -ForegroundColor Green

            foreach ($folder in $jobSpecificFolders) {
                $destFolder = "$spritePath/$($folder.Name)"
                New-Item -ItemType Directory -Force -Path $destFolder | Out-Null
                Copy-Item "$($folder.FullName)/*.bin" $destFolder -Force
                Write-Host "  Copied $($folder.Name)" -ForegroundColor Gray
            }

            $jobSpecificCount = $jobSpecificFolders.Count
            Write-Host "Copied $jobSpecificCount job-specific theme folders" -ForegroundColor Green
        }

        # Copy WotL (unit_psp) sprite folders for Dark Knight and Onion Knight
        $unitPspSource = "ColorMod/FFTIVC/data/enhanced/fftpack/unit_psp"
        if (Test-Path $unitPspSource) {
            Write-Host "Copying WotL (unit_psp) sprites..." -ForegroundColor Cyan
            $unitPspDest = "$modPath/FFTIVC/data/enhanced/fftpack/unit_psp"

            $wotlFolders = Get-ChildItem $unitPspSource -Directory
            foreach ($folder in $wotlFolders) {
                $destFolder = "$unitPspDest/$($folder.Name)"
                New-Item -ItemType Directory -Force -Path $destFolder | Out-Null
                Copy-Item "$($folder.FullName)/*.bin" $destFolder -Force
                Write-Host "  Copied $($folder.Name)" -ForegroundColor Gray
            }

            $wotlFolderCount = $wotlFolders.Count
            Write-Host "Copied $wotlFolderCount WotL theme folders to unit_psp" -ForegroundColor Green
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

    # Copy Ramza theme tex files to RamzaThemes folder (outside game scan path)
    Write-Host "Copying Ramza theme tex files to RamzaThemes folder..." -ForegroundColor Cyan
    $ramzaThemesSource = "$PSScriptRoot/ColorMod/RamzaThemes"
    $ramzaThemesDest = "$modPath/RamzaThemes"

    if (Test-Path $ramzaThemesSource) {
        # Define all valid Ramza themes (only keep the main ones, not duplicates)
        $validRamzaThemes = @(
            "dark_knight",      # The fixed dark knight theme
            "white_heretic",    # The perfect white armor theme
            "crimson_blade"     # The new red/crimson theme
        )

        $copiedCount = 0
        foreach ($themeName in $validRamzaThemes) {
            $themePath = "$ramzaThemesSource/$themeName"
            if (Test-Path $themePath) {
                # Check if theme has all 6 required Ramza TEX files
                $ramzaTexFiles = @()
                foreach ($num in 830..835) {
                    $texFile = "$themePath/tex_$num.bin"
                    if (Test-Path $texFile) {
                        $ramzaTexFiles += $texFile
                    }
                }

                if ($ramzaTexFiles.Count -eq 6) {
                    $themeDest = "$ramzaThemesDest/$themeName"
                    New-Item -ItemType Directory -Force -Path $themeDest | Out-Null
                    foreach ($texFile in $ramzaTexFiles) {
                        Copy-Item $texFile -Destination $themeDest -Force
                    }
                    Write-Host "  Copied Ramza tex files for $themeName theme" -ForegroundColor Green
                    $copiedCount++
                } elseif ($ramzaTexFiles.Count -gt 0) {
                    Write-Host "  Warning: $themeName has incomplete tex files (found $($ramzaTexFiles.Count)/6)" -ForegroundColor Yellow
                }
            }
        }

        if ($copiedCount -gt 0) {
            Write-Host "Successfully deployed $copiedCount Ramza themes to RamzaThemes folder" -ForegroundColor Green
        } else {
            Write-Host "No valid Ramza themes found to deploy" -ForegroundColor Yellow
        }
    }

    # Copy Data JSON files (StoryCharacters.json, JobClasses.json, SectionMappings/)
    Write-Host "Copying data files..."
    $dataSource = "$PSScriptRoot/ColorMod/Data"
    $dataDest = "$modPath/Data"

    if (Test-Path $dataSource) {
        # Copy entire Data directory structure including subdirectories
        Copy-Item $dataSource -Destination $modPath -Recurse -Force
        $dataCount = (Get-ChildItem "$dataDest" -Recurse -Filter "*.json").Count
        Write-Host "Copied $dataCount data files (including subdirectories)"
    }

    # Copy preview images
    Write-Host "Copying preview images..."
    $previewSource = "$PSScriptRoot/ColorMod/Resources/Previews"
    $previewDest = "$modPath/Resources/Previews"

    if (Test-Path $previewSource) {
        New-Item -ItemType Directory -Force -Path $previewDest | Out-Null
        Copy-Item "$previewSource/*.png" -Destination $previewDest -Force
        $previewCount = (Get-ChildItem "$previewDest/*.png").Count
        Write-Host "Copied $previewCount preview images"
    }

    # Copy sprite sheet images for Ramza chapters
    Write-Host "Copying Ramza sprite sheet images..." -ForegroundColor Cyan
    $imagesSource = "$PSScriptRoot/ColorMod/Images"
    $imagesDest = "$modPath/Images"

    if (Test-Path $imagesSource) {
        # Copy the entire Images directory structure including all image files
        Write-Host "  Creating Images directory structure..." -ForegroundColor Gray
        New-Item -ItemType Directory -Force -Path $imagesDest | Out-Null

        # Copy all Ramza chapter folders with their theme subfolders and sprite sheets
        $ramzaFolders = Get-ChildItem "$imagesSource" -Directory | Where-Object { $_.Name -match "^Ramza" }

        foreach ($folder in $ramzaFolders) {
            $destFolder = "$imagesDest/$($folder.Name)"
            Write-Host "  Copying $($folder.Name)..." -ForegroundColor Gray

            # Copy the entire folder structure including all subdirectories and image files
            Copy-Item $folder.FullName $destFolder -Recurse -Force

            # Count image files copied (both PNG and BMP)
            $bmpCount = (Get-ChildItem "$destFolder" -Recurse -Filter "*.bmp" | Measure-Object).Count
            $pngCount = (Get-ChildItem "$destFolder" -Recurse -Filter "*.png" | Measure-Object).Count
            Write-Host "    Copied $bmpCount BMP sprite sheets and $pngCount PNG images" -ForegroundColor Green
        }

        # Copy any PNG files in the root Images directory (like thunder_god.png for the mod icon)
        Copy-Item "$imagesSource/*.png" $imagesDest -Force -ErrorAction SilentlyContinue

        $totalBmpCount = (Get-ChildItem "$imagesDest" -Recurse -Filter "*.bmp" | Measure-Object).Count
        $totalPngCount = (Get-ChildItem "$imagesDest" -Recurse -Filter "*.png" | Measure-Object).Count
        Write-Host "Total images copied: $totalBmpCount BMP files and $totalPngCount PNG files" -ForegroundColor Green
    }

    # Theme directories are no longer deployed - they're read from git repo

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
