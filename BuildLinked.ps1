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

# Clean existing installation
$modPath = "$env:RELOADEDIIMODS/FFTColorCustomizer"
if (Test-Path $modPath) {
    Write-Host "Removing existing mod installation..." -ForegroundColor Yellow
    Remove-Item "$modPath/*" -Force -Recurse -ErrorAction SilentlyContinue
}

# Build and publish with IL trimming for smaller size
Write-Host "Publishing to Reloaded-II mods folder..." -ForegroundColor Cyan
dotnet publish "./ColorMod/FFTColorCustomizer.csproj" -c Release -o "$modPath" /p:OutputPath="./bin/Release" /p:ReloadedILLink="true"

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

            # Copy generic job themed folders (e.g., sprites_crimson_red, sprites_lucavi)
            Write-Host "Copying generic job themed sprites..." -ForegroundColor Cyan
            $genericThemeFolders = Get-ChildItem "ColorMod/FFTIVC/data/enhanced/fftpack/unit/" -Directory |
                Where-Object { $_.Name -match "^sprites_[^_]+$" -or $_.Name -match "^sprites_(crimson_red|lucavi|northern_sky|southern_sky|amethyst|celestial|corpse_brigade|emerald_dragon|frost_knight|golden_templar|blood_moon|volcanic|ocean_depths|royal_purple|phoenix_flame|rose_gold|silver_knight|shadow_assassin)" }

            foreach ($folder in $genericThemeFolders) {
                $destFolder = "$spritePath/$($folder.Name)"
                New-Item -ItemType Directory -Force -Path $destFolder | Out-Null
                Copy-Item "$($folder.FullName)/*.bin" $destFolder -Force
                Write-Host "  Copied $($folder.Name)" -ForegroundColor Gray
            }

            $genericFolderCount = $genericThemeFolders.Count
            Write-Host "Copied $genericFolderCount generic job theme folders" -ForegroundColor Green
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

    # Copy Data JSON files (StoryCharacters.json and JobClasses.json)
    Write-Host "Copying data files..."
    $dataSource = "$PSScriptRoot/ColorMod/Data"
    $dataDest = "$modPath/Data"

    if (Test-Path $dataSource) {
        New-Item -ItemType Directory -Force -Path $dataDest | Out-Null
        Copy-Item "$dataSource/*.json" -Destination $dataDest -Force
        $dataCount = (Get-ChildItem "$dataDest/*.json").Count
        Write-Host "Copied $dataCount data files"
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
