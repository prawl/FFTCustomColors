# BuildLinked.ps1 - local build + deploy of paxtrick.fft.colorcustomizer into Reloaded-II.
#
# Local-dev counterpart to Publish.ps1 (which builds the production release zip).
# The shared pipeline half (build, shared asset staging, the manifest-driven
# verification) lives in tools/pipeline.ps1, dot-sourced by both scripts so the
# copies cannot drift. This file keeps the deploy-specific half: mods-folder
# resolution, the UserThemes preserve/restore round-trip, the selective sprite
# staging (the release zip robocopies the whole FFTIVC tree instead), the g2d /
# Ramza tex handling, and the User config seed.
#
# Deploys ALL generic + story themes.

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "         DEV BUILD SCRIPT                   " -ForegroundColor Cyan
Write-Host "   Deploys ALL generic + story themes      " -ForegroundColor Yellow
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

. "$PSScriptRoot\tools\pipeline.ps1"

Write-Host "Building FFT Color Mod (DEV MODE)..." -ForegroundColor Green

# Clean ALL FFTColorCustomizer installations to prevent conflicts
Write-Host "Cleaning up all FFTColorCustomizer installations..." -ForegroundColor Yellow
$modsDir = "$env:RELOADEDIIMODS"

# Remove any versioned folders (FFTColorCustomizer_v*)
Get-ChildItem "$modsDir" -Filter "FFTColorCustomizer_v*" -Directory | ForEach-Object {
    Write-Host "  Removing versioned installation: $($_.Name)" -ForegroundColor Yellow
    Remove-Item $_.FullName -Force -Recurse -ErrorAction SilentlyContinue
}

# Deploy to the ACTIVE ModId folder (tools/pipeline.ps1's $ColorModId; must match
# ModConfig.json "ModId") so the copy the game actually loads gets updated.
$modPath = "$modsDir/$ColorModId"

# Remove the legacy "FFTColorCustomizer" shadow folder (same ModId -> Reloaded loads the wrong copy)
if ("$modsDir/$LegacyShadowFolderName" -ne $modPath -and (Test-Path "$modsDir/$LegacyShadowFolderName")) {
    Write-Host "  Removing legacy shadow folder (shares ModId): $LegacyShadowFolderName" -ForegroundColor Yellow
    Remove-Item "$modsDir/$LegacyShadowFolderName" -Force -Recurse -ErrorAction SilentlyContinue
}

# Clean existing dev installation (preserve user themes)
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

    # Remove everything except the Vortex marker (so Vortex doesn't treat the folder
    # as orphaned) and logs/ (so a deploy never destroys the run evidence the log
    # rotation exists to preserve; the sibling mods preserve their flight/ the same
    # way). NOT -Exclude: with -Recurse it spares a directory itself but still wipes
    # the directory's CONTENTS (proven empirically), so filter the top level instead.
    Get-ChildItem $modPath -Force |
        Where-Object { $_.Name -ne "__folder_managed_by_vortex" -and $_.Name -ne "logs" } |
        Remove-Item -Force -Recurse -ErrorAction SilentlyContinue

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

# The contract-test gate (tools/pipeline.ps1, CC-16): a malformed ledger or a
# logging-contract violation refuses to deploy, same as the sibling mods.
try {
    Invoke-UnitTestGate -FailVerb DEPLOY
} catch {
    Write-Host "$_" -ForegroundColor Red
    Pop-Location
    exit 1
}

# Build and publish with IL trimming for smaller size (tools/pipeline.ps1)
Write-Host "Publishing to Reloaded-II mods folder..." -ForegroundColor Cyan
try {
    Invoke-ColorModBuild -OutDir $modPath -Dev
} catch {
    Write-Host "Build failed! Check the output above for errors." -ForegroundColor Red
    Pop-Location
    exit 1
}

# TLDR: Copy ModConfig.json so Reloaded recognizes the mod
Write-Host "Copying ModConfig.json..." -ForegroundColor Cyan
Copy-Item "ColorMod/ModConfig.json" "$modPath/ModConfig.json" -Force

# SQLite native DLL to the mod root + the mod icon (shared, tools/pipeline.ps1)
Copy-SqliteNativeToRoot -Dest $modPath
Copy-ModIcon -Dest $modPath

# Copy Config.json only if it doesn't exist (don't overwrite user's config)
if (Test-Path "ColorMod/Config.json") {
    if (!(Test-Path "$modPath/Config.json")) {
        Write-Host "Copying default Config.json..." -ForegroundColor Cyan
        Copy-Item "ColorMod/Config.json" "$modPath/Config.json" -Force
    } else {
        Write-Host "Config.json already exists, preserving user settings..." -ForegroundColor Yellow
    }
}

# Seed the Reloaded User config directory (Reloaded\User\Mods\<ModId>, derived from
# the mods folder). Seed-only: an existing user config is NEVER overwritten - it is
# the user's live settings, and the Ramza tex deploy below reads it back.
# (The old version of this block referenced undefined $gamePath/$scriptDir variables
# and a wrong folder name, so it silently never ran; this is the working form.)
$userConfigDir = Join-Path (Split-Path $modsDir -Parent) "User\Mods\$ColorModId"
Write-Host "Seeding User config directory..." -ForegroundColor Cyan
New-Item -ItemType Directory -Path $userConfigDir -Force | Out-Null
foreach ($cfgName in @("Config.json", "ModUserConfig.json")) {
    $cfgSource = "$PSScriptRoot\ColorMod\$cfgName"
    $cfgDest = Join-Path $userConfigDir $cfgName
    if ((Test-Path $cfgSource) -and -not (Test-Path $cfgDest)) {
        Copy-Item $cfgSource -Destination $cfgDest -Force
        Write-Host "  Seeded $cfgName in User directory" -ForegroundColor Green
    }
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

        # Also deploy sprites_original as a FOLDER. The theme editor loads each character's
        # original template from unit/sprites_original/<sprite>.bin (e.g. the new Chocobo
        # preview reads battle_cyoko_spr.bin from here). Flattening into unit/ alone isn't enough.
        $spritesOriginalDest = "$spritePath/sprites_original"
        New-Item -ItemType Directory -Force -Path $spritesOriginalDest | Out-Null
        Copy-Item "ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_original/*.bin" $spritesOriginalDest -Force
        Write-Host "Copied sprites_original folder ($spriteCount theme-editor templates incl. Chocobo)" -ForegroundColor Green

        # Copy story character themed folders (e.g., sprites_cloud_sephiroth_black)
        Write-Host "Copying story character themed sprites..." -ForegroundColor Cyan
        $storyCharacterFolders = Get-ChildItem "ColorMod/FFTIVC/data/enhanced/fftpack/unit/" -Directory |
            Where-Object { $_.Name -match "sprites_(cloud|agrias|orlandeau|rapha|marach|reis|mustadio|meliadoul|beowulf|construct8)_" }

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

            # Check user's config and copy appropriate Ramza tex files (the same
            # User config dir seeded above; per-user live settings win)
            $userConfigJson = Join-Path $userConfigDir "Config.json"
            if (Test-Path $userConfigJson) {
                $userConfig = Get-Content $userConfigJson | ConvertFrom-Json
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

# Ramza theme TEX files, Data JSONs + charclut.nxd, preview Images (shared, tools/pipeline.ps1)
Copy-RamzaThemeAssets -Dest $modPath
Copy-DataAssets -Dest $modPath

# Copy preview images (theme editor previews; separate from the Images HD sheets)
Write-Host "Copying preview images..."
$previewSource = "$PSScriptRoot/ColorMod/Resources/Previews"
$previewDest = "$modPath/Resources/Previews"

if (Test-Path $previewSource) {
    New-Item -ItemType Directory -Force -Path $previewDest | Out-Null
    Copy-Item "$previewSource/*.png" -Destination $previewDest -Force
    $previewCount = (Get-ChildItem "$previewDest/*.png").Count
    Write-Host "Copied $previewCount preview images"
}

Copy-ImageAssets -Dest $modPath

# Theme directories are no longer deployed - they're read from git repo

# TLDR: Verify deployment succeeded (fail loud on missing pieces; no silent drift).
# Manifest-driven (tools/package_manifest.json via tools/pipeline.ps1) - the SAME
# manifest tools/analyze.py gates the release zip against (CC-10), plus the
# monster-family asset sweep. This replaces the old inline required-file lists,
# so deploy and package verification can never drift apart.
Write-Host "`nVerifying deployment..." -ForegroundColor Cyan
$verificationErrors = @()
$verificationErrors += @(Test-ManifestTree -Root $modPath)
$verificationErrors += @(Test-MonsterFamilyAssets -Root $modPath)

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

# Restore Working Directory
Pop-Location
