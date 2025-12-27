<#
.SYNOPSIS
    Builds and Publishes FFT Color Customizer Mod for Reloaded-II
.DESCRIPTION
    Complete build and packaging script for production release.
    Builds the mod, copies all required assets including sprite themes,
    and creates a ready-to-distribute ZIP package.
.PARAMETER Version
    Version number for the mod (e.g., "1.0.5")
    Default: Reads from Publish/Release/ModConfig.json
.PARAMETER OutputPath
    Where to save the final ZIP file
    Default: "C:\Users\ptyRa\Downloads"
.EXAMPLE
    .\Publish.ps1
    # Build with version from ModConfig.json
.EXAMPLE
    .\Publish.ps1 -Version "1.0.6"
    # Build with custom version number
#>

[cmdletbinding()]
param (
    [string]$Version = "",
    [string]$OutputPath = ""
)

## => Configuration <= ##
$ProjectPath = "ColorMod/FFTColorCustomizer.csproj"
$BuildOutputPath = "Publish/Release"
$TempBuildPath = "Publish/TempBuild"
$ModConfigPath = "$BuildOutputPath/ModConfig.json"

# Set default output path based on environment
if (-not $OutputPath) {
    if ($env:GITHUB_ACTIONS) {
        # GitHub Actions - output to workspace
        $OutputPath = "."
    }
    else {
        # Local build - output to Downloads
        $OutputPath = "C:\Users\ptyRa\Downloads"
    }
}

## => Functions <= ##
function Write-Status {
    param($Message, $Color = "Green")
    Write-Host "`n==> $Message" -ForegroundColor $Color
}

function Write-ErrorMessage {
    param($Message)
    Write-Host "`n[ERROR] $Message" -ForegroundColor Red
    exit 1
}

function Clean-BuildDirectories {
    Write-Status "Cleaning build directories..." "Yellow"

    if (Test-Path $BuildOutputPath) {
        Remove-Item "$BuildOutputPath\*" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    } else {
        New-Item $BuildOutputPath -ItemType Directory -Force | Out-Null
    }

    if (Test-Path $TempBuildPath) {
        Remove-Item $TempBuildPath -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

function Clean-DevInstallations {
    Write-Status "Removing dev build to prevent conflicts..." "Yellow"

    # Remove the linked dev build if it exists
    $devModPath = "$env:RELOADEDIIMODS/FFTColorCustomizer"
    if (Test-Path $devModPath) {
        Write-Host "  -> Removing dev build from Reloaded-II mods folder..." -ForegroundColor Yellow
        Remove-Item $devModPath -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

function Build-Project {
    Write-Status "Building FFT Color Customizer in Release mode..." "Cyan"

    # Clean solution first
    Write-Host "  -> Cleaning solution..."
    dotnet clean $ProjectPath -c Release | Out-Null

    # Restore packages
    Write-Host "  -> Restoring NuGet packages..."
    dotnet restore $ProjectPath | Out-Null

    # Build in Release mode
    Write-Host "  -> Building Release configuration..."
    $buildResult = dotnet publish $ProjectPath `
        -c Release `
        --self-contained false `
        -o $BuildOutputPath `
        /p:OutputPath="$TempBuildPath"

    if ($LASTEXITCODE -ne 0) {
        Write-ErrorMessage "Build failed!"
    }

    Write-Host "  -> Build completed successfully!" -ForegroundColor Green
}

function Copy-ModAssets {
    Write-Status "Copying mod assets..." "Cyan"

    # Copy ModConfig.json from ColorMod if it exists there
    $sourceModConfig = "ColorMod/ModConfig.json"
    if (Test-Path $sourceModConfig) {
        Write-Host "  -> Copying ModConfig.json..."
        Copy-Item $sourceModConfig -Destination $BuildOutputPath -Force
    }

    # Copy Preview.png
    $sourcePreview = "ColorMod/Preview.png"
    if (Test-Path $sourcePreview) {
        Write-Host "  -> Copying Preview.png..."
        Copy-Item $sourcePreview -Destination $BuildOutputPath -Force
    }

    # Copy Data folder with StoryCharacters.json and JobClasses.json
    $sourceData = "ColorMod/Data"
    if (Test-Path $sourceData) {
        Write-Host "  -> Copying Data folder..."
        $destData = "$BuildOutputPath/Data"
        Copy-Item $sourceData -Destination $BuildOutputPath -Recurse -Force
        Write-Host "  -> Data folder copied" -ForegroundColor Green
    } else {
        Write-Host "  -> Warning: Data folder not found at: $sourceData" -ForegroundColor Yellow
    }

    # Copy FFTIVC folder with all sprite themes
    $sourceFFTIVC = "ColorMod/FFTIVC"
    if (Test-Path $sourceFFTIVC) {
        Write-Host "  -> Copying FFTIVC folder (this may take a moment)..."
        $destFFTIVC = "$BuildOutputPath/FFTIVC"

        # Use robocopy for efficient copying with progress
        $robocopyArgs = @(
            $sourceFFTIVC,
            $destFFTIVC,
            "/E",           # Copy subdirectories including empty ones
            "/NFL",         # No file list
            "/NDL",         # No directory list
            "/NJH",         # No job header
            "/NJS",         # No job summary
            "/NC",          # No class
            "/NS"           # No size
        )

        $result = robocopy @robocopyArgs

        if ($LASTEXITCODE -ge 8) {
            Write-ErrorMessage "Failed to copy FFTIVC folder!"
        }

        # Clean up unwanted files from g2d directory but keep it for modloader scanning
        $g2dPath = "$destFFTIVC/data/enhanced/system/ffto/g2d"
        if (Test-Path $g2dPath) {
            Write-Host "  -> Cleaning g2d directory (keeping for modloader)..."
            # Remove ALL theme directories - themes will be in RamzaThemes folder instead
            $allDirs = @("black_variant", "red_variant", "test_variant", "themes", "original_backup",
                         "white_heretic", "dark_knight", "crimson_blade")
            foreach ($dir in $allDirs) {
                $dirPath = "$g2dPath/$dir"
                if (Test-Path $dirPath) {
                    Remove-Item $dirPath -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            # Remove any loose tex files from development testing
            Get-ChildItem "$g2dPath/tex_*.bin" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
        }

        # Count sprite files
        $binFiles = Get-ChildItem -Path $destFFTIVC -Filter "*.bin" -Recurse
        Write-Host "  -> Copied $($binFiles.Count) sprite files" -ForegroundColor Green
    } else {
        Write-ErrorMessage "FFTIVC folder not found at: $sourceFFTIVC"
    }

    # Copy RamzaThemes folder with tex themes
    $ramzaThemesSource = "ColorMod/FFTIVC/data/enhanced/system/ffto/g2d"
    if (Test-Path $ramzaThemesSource) {
        Write-Host "  -> Copying Ramza tex themes to RamzaThemes folder..."
        $ramzaThemesDest = "$BuildOutputPath/RamzaThemes"

        # Copy white_heretic theme if it exists
        $whiteHereticPath = "$ramzaThemesSource/white_heretic"
        if (Test-Path $whiteHereticPath) {
            $whiteHereticDest = "$ramzaThemesDest/white_heretic"
            New-Item -ItemType Directory -Force -Path $whiteHereticDest | Out-Null
            Copy-Item "$whiteHereticPath/*.bin" -Destination $whiteHereticDest -Force
            $texCount = (Get-ChildItem "$whiteHereticDest/*.bin" | Measure-Object).Count
            Write-Host "    -> Copied $texCount tex files for white_heretic theme" -ForegroundColor Green
        }

        # Copy dark_knight theme if it exists
        $darkKnightPath = "$ramzaThemesSource/dark_knight"
        if (Test-Path $darkKnightPath) {
            $darkKnightDest = "$ramzaThemesDest/dark_knight"
            New-Item -ItemType Directory -Force -Path $darkKnightDest | Out-Null
            Copy-Item "$darkKnightPath/*.bin" -Destination $darkKnightDest -Force
            $texCount = (Get-ChildItem "$darkKnightDest/*.bin" | Measure-Object).Count
            Write-Host "    -> Copied $texCount tex files for dark_knight theme" -ForegroundColor Green
        }

        # Copy crimson_blade theme if it exists
        $crimsonBladePath = "$ramzaThemesSource/crimson_blade"
        if (Test-Path $crimsonBladePath) {
            $crimsonBladeDest = "$ramzaThemesDest/crimson_blade"
            New-Item -ItemType Directory -Force -Path $crimsonBladeDest | Out-Null
            Copy-Item "$crimsonBladePath/*.bin" -Destination $crimsonBladeDest -Force
            $texCount = (Get-ChildItem "$crimsonBladeDest/*.bin" | Measure-Object).Count
            Write-Host "    -> Copied $texCount tex files for crimson_blade theme" -ForegroundColor Green
        }
    }

    # Copy Images folder with Ramza sprite sheet previews
    $imagesSource = "ColorMod/Images"
    if (Test-Path $imagesSource) {
        Write-Host "  -> Copying Images folder with Ramza sprite previews..."
        $imagesDest = "$BuildOutputPath/Images"
        New-Item -ItemType Directory -Force -Path $imagesDest | Out-Null

        # Copy Ramza chapter folders with sprite sheets
        $ramzaFolders = Get-ChildItem "$imagesSource" -Directory | Where-Object { $_.Name -match "^Ramza" }
        foreach ($folder in $ramzaFolders) {
            $destFolder = "$imagesDest/$($folder.Name)"
            Copy-Item $folder.FullName $destFolder -Recurse -Force
        }

        # Copy any root PNG files (like thunder_god.png)
        Copy-Item "$imagesSource/*.png" $imagesDest -Force -ErrorAction SilentlyContinue

        $totalBmpCount = (Get-ChildItem "$imagesDest" -Recurse -Filter "*.bmp" -ErrorAction SilentlyContinue | Measure-Object).Count
        $totalPngCount = (Get-ChildItem "$imagesDest" -Recurse -Filter "*.png" -ErrorAction SilentlyContinue | Measure-Object).Count
        Write-Host "    -> Copied $totalBmpCount BMP sprite sheets and $totalPngCount PNG previews" -ForegroundColor Green
    }
}

function Update-Version {
    param([string]$NewVersion)

    if ([string]::IsNullOrEmpty($NewVersion)) {
        # Try to read version from existing ModConfig.json
        if (Test-Path $ModConfigPath) {
            $config = Get-Content $ModConfigPath | ConvertFrom-Json
            $currentVersion = $config.ModVersion
            Write-Host "  -> Using version from ModConfig.json: $currentVersion"
            return $currentVersion
        } else {
            Write-ErrorMessage "No version specified and ModConfig.json not found!"
        }
    }

    Write-Status "Updating version to $NewVersion..." "Cyan"

    if (Test-Path $ModConfigPath) {
        $config = Get-Content $ModConfigPath | ConvertFrom-Json
        $config.ModVersion = $NewVersion
        $config | ConvertTo-Json -Depth 10 | Set-Content $ModConfigPath
        Write-Host "  -> Updated ModConfig.json version to $NewVersion"
    } else {
        Write-ErrorMessage "ModConfig.json not found at: $ModConfigPath"
    }

    return $NewVersion
}

function Clean-BuildOutput {
    Write-Status "Cleaning build output..." "Yellow"

    # Remove unnecessary files
    $patterns = @(
        "*.pdb",
        "*.xml",
        "*.deps.json",
        "*.runtimeconfig.json",
        "*Test*",
        "xunit*",
        "*.exe"
    )

    foreach ($pattern in $patterns) {
        try {
            $files = @(Get-ChildItem -Path $BuildOutputPath -Filter $pattern -Recurse -ErrorAction SilentlyContinue)
            if ($files -and $files.Count -gt 0) {
                Write-Host "  -> Removing $($files.Count) $pattern files..."
                $files | ForEach-Object {
                    if ($_ -and (Test-Path $_.FullName)) {
                        Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
                    }
                }
            }
        }
        catch {
            # Silently continue if there's an issue with specific patterns
        }
    }

    # Clean up temp build directory
    if (Test-Path $TempBuildPath) {
        Remove-Item $TempBuildPath -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Create-Package {
    param([string]$ModVersion)

    Write-Status "Creating ZIP package..." "Green"

    $packageName = "FFTColorCustomizer_v$ModVersion.zip"
    $packagePath = Join-Path $OutputPath $packageName

    # Remove existing package if it exists
    if (Test-Path $packagePath) {
        Write-Host "  -> Removing existing package..."
        Remove-Item $packagePath -Force
    }

    # Ensure output directory exists
    if (-not (Test-Path $OutputPath)) {
        Write-Host "  -> Creating output directory: $OutputPath"
        New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
    }

    # Ensure build directory exists
    if (-not (Test-Path $BuildOutputPath)) {
        Write-ErrorMessage "Build output directory not found: $BuildOutputPath"
        return $null
    }

    # Create ZIP using .NET compression
    Write-Host "  -> Compressing to: $packagePath"

    # Load assembly properly
    try {
        Add-Type -Assembly System.IO.Compression.FileSystem -ErrorAction Stop
    }
    catch {
        Write-Host "  -> Loading compression assembly using alternative method..."
        [Reflection.Assembly]::LoadWithPartialName("System.IO.Compression.FileSystem") | Out-Null
    }

    try {
        # Convert paths to absolute
        $absoluteBuildPath = (Get-Item $BuildOutputPath).FullName
        $absolutePackagePath = [System.IO.Path]::GetFullPath($packagePath)

        Write-Host "  -> Source: $absoluteBuildPath"
        Write-Host "  -> Target: $absolutePackagePath"

        [System.IO.Compression.ZipFile]::CreateFromDirectory(
            $absoluteBuildPath,
            $absolutePackagePath,
            [System.IO.Compression.CompressionLevel]::Optimal,
            $false
        )

        if (Test-Path $absolutePackagePath) {
            $packageInfo = Get-Item $absolutePackagePath
            $sizeMB = [math]::Round($packageInfo.Length / 1MB, 2)

            Write-Host "  -> Package created successfully!" -ForegroundColor Green
            Write-Host "  -> Size: $sizeMB MB" -ForegroundColor Cyan
            Write-Host "  -> Location: $absolutePackagePath" -ForegroundColor Cyan
            return $absolutePackagePath
        }
        else {
            Write-ErrorMessage "Package was not created at: $absolutePackagePath"
            return $null
        }
    }
    catch {
        Write-Host "`n[ERROR] Failed to create ZIP package: $_" -ForegroundColor Red
        Write-Host "  -> Error details: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

function Verify-Package {
    param([string]$PackagePath)

    Write-Status "Verifying package contents..." "Cyan"

    if (-not $PackagePath -or -not (Test-Path $PackagePath)) {
        Write-Host "  -> Package not found for verification" -ForegroundColor Red
        return
    }

    Add-Type -Assembly System.IO.Compression.FileSystem -ErrorAction SilentlyContinue

    try {
        $zip = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)

        $requiredFiles = @(
            "ModConfig.json",
            "FFTColorCustomizer.dll",
            "Preview.png",
            "Data/StoryCharacters.json",
            "Data/JobClasses.json"
        )

        $requiredPaths = @(
            "FFTIVC/data/enhanced/fftpack/unit",
            "Data",
            "Images",
            "RamzaThemes"
        )

        foreach ($file in $requiredFiles) {
            $entry = $zip.Entries | Where-Object { $_.FullName -eq $file }
            if ($entry) {
                Write-Host "  ✓ Found: $file" -ForegroundColor Green
            } else {
                Write-Host "  ✗ Missing: $file" -ForegroundColor Red
            }
        }

        foreach ($path in $requiredPaths) {
            $entries = $zip.Entries | Where-Object { $_.FullName -like "$path/*" }
            if ($entries) {
                $binFiles = $entries | Where-Object { $_.Name -like "*.bin" }
                Write-Host "  ✓ Found: $path (with $($binFiles.Count) .bin files)" -ForegroundColor Green
            } else {
                Write-Host "  ✗ Missing: $path" -ForegroundColor Red
            }
        }

        $zip.Dispose()
    }
    catch {
        Write-Host "`n[ERROR] Failed to verify package: $_" -ForegroundColor Red
    }
}

## => Main Script <= ##

Write-Host "`n=====================================" -ForegroundColor Magenta
Write-Host "   FFT Color Customizer - Publisher  " -ForegroundColor Magenta
Write-Host "=====================================" -ForegroundColor Magenta

# Save current directory and change to script directory
$originalLocation = Get-Location
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

try {
    # Step 1: Clean
    Clean-BuildDirectories
    Clean-DevInstallations

    # Step 2: Build
    Build-Project

    # Step 3: Copy Assets
    Copy-ModAssets

    # Step 4: Update Version
    $finalVersion = Update-Version -NewVersion $Version

    # Step 5: Clean Output
    Clean-BuildOutput

    # Step 6: Create Package
    $packagePath = Create-Package -ModVersion $finalVersion

    if ($packagePath) {
        # Step 7: Verify
        Verify-Package -PackagePath $packagePath

        Write-Status "Publishing completed successfully!" "Green"
        Write-Host "`n=====================================" -ForegroundColor Magenta
        Write-Host "Package ready at: $packagePath" -ForegroundColor Yellow
        Write-Host "Version: $finalVersion" -ForegroundColor Yellow
        Write-Host "=====================================" -ForegroundColor Magenta
        $exitCode = 0
    }
    else {
        Write-Status "Publishing failed - package creation unsuccessful" "Red"
        $exitCode = 1
    }
}
catch {
    Write-Host "`n[ERROR] An unexpected error occurred: $_" -ForegroundColor Red
    Write-Host "Stack Trace: $($_.ScriptStackTrace)" -ForegroundColor Red
    $exitCode = 1
}
finally {
    # Restore original directory
    Pop-Location
    Set-Location $originalLocation
    exit $exitCode
}