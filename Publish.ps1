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
    [string]$OutputPath = "C:\Users\ptyRa\Downloads"
)

## => Configuration <= ##
$ProjectPath = "ColorMod/FFTColorCustomizer.csproj"
$BuildOutputPath = "Publish/Release"
$TempBuildPath = "Publish/TempBuild"
$ModConfigPath = "$BuildOutputPath/ModConfig.json"

## => Functions <= ##
function Write-Status {
    param($Message, $Color = "Green")
    Write-Host "`n==> $Message" -ForegroundColor $Color
}

function Write-Error {
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
        Write-Error "Build failed!"
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
            Write-Error "Failed to copy FFTIVC folder!"
        }

        # Count sprite files
        $binFiles = Get-ChildItem -Path $destFFTIVC -Filter "*.bin" -Recurse
        Write-Host "  -> Copied $($binFiles.Count) sprite files" -ForegroundColor Green
    } else {
        Write-Error "FFTIVC folder not found at: $sourceFFTIVC"
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
            Write-Error "No version specified and ModConfig.json not found!"
        }
    }

    Write-Status "Updating version to $NewVersion..." "Cyan"

    if (Test-Path $ModConfigPath) {
        $config = Get-Content $ModConfigPath | ConvertFrom-Json
        $config.ModVersion = $NewVersion
        $config | ConvertTo-Json -Depth 10 | Set-Content $ModConfigPath
        Write-Host "  -> Updated ModConfig.json version to $NewVersion"
    } else {
        Write-Error "ModConfig.json not found at: $ModConfigPath"
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
        $files = Get-ChildItem -Path $BuildOutputPath -Filter $pattern -Recurse -ErrorAction SilentlyContinue
        if ($files) {
            Write-Host "  -> Removing $($files.Count) $pattern files..."
            $files | Remove-Item -Force -ErrorAction SilentlyContinue
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

    # Create ZIP using .NET compression
    Write-Host "  -> Compressing to: $packagePath"

    Add-Type -Assembly System.IO.Compression.FileSystem

    try {
        [System.IO.Compression.ZipFile]::CreateFromDirectory(
            $BuildOutputPath,
            $packagePath,
            [System.IO.Compression.CompressionLevel]::Optimal,
            $false
        )

        $packageInfo = Get-Item $packagePath
        $sizeMB = [math]::Round($packageInfo.Length / 1MB, 2)

        Write-Host "  -> Package created successfully!" -ForegroundColor Green
        Write-Host "  -> Size: $sizeMB MB" -ForegroundColor Cyan
        Write-Host "  -> Location: $packagePath" -ForegroundColor Cyan
    }
    catch {
        Write-Error "Failed to create ZIP package: $_"
    }

    return $packagePath
}

function Verify-Package {
    param([string]$PackagePath)

    Write-Status "Verifying package contents..." "Cyan"

    Add-Type -Assembly System.IO.Compression.FileSystem

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
            "Data"
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
        Write-Error "Failed to verify package: $_"
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

    # Step 7: Verify
    Verify-Package -PackagePath $packagePath

    Write-Status "Publishing completed successfully!" "Green"
    Write-Host "`n=====================================" -ForegroundColor Magenta
    Write-Host "Package ready at: $packagePath" -ForegroundColor Yellow
    Write-Host "Version: $finalVersion" -ForegroundColor Yellow
    Write-Host "=====================================" -ForegroundColor Magenta
}
catch {
    Write-Error "An unexpected error occurred: $_"
}
finally {
    # Restore original directory
    Pop-Location
    Set-Location $originalLocation
}