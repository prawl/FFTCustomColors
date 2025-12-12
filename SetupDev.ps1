#!/usr/bin/env pwsh
# Setup Development Mode - Moves all themes to ColorSchemes for dynamic loading
# Keeps only 5 core themes in data/ for F1/F2 testing

Write-Host "Setting up Development Mode with dynamic sprite loading..." -ForegroundColor Cyan

$modPath = $PSScriptRoot
$gameThemesPath = Join-Path $modPath "FFTIVC\data\enhanced\fftpack\unit"
$colorSchemesPath = Join-Path $modPath "ColorSchemes"

# Create ColorSchemes directory if it doesn't exist
if (-not (Test-Path $colorSchemesPath)) {
    New-Item -ItemType Directory -Path $colorSchemesPath -Force | Out-Null
    Write-Host "Created ColorSchemes directory: $colorSchemesPath" -ForegroundColor Green
}

# Core themes to keep for development (always keep these)
$coreDevThemes = @(
    "sprites_original",
    "sprites_corpse_brigade",
    "sprites_lucavi",
    "sprites_northern_sky",
    "sprites_smoke"
)

# Also keep any test themes (themes starting with "sprites_test_")
# This allows you to add sprites_test_1, sprites_test_2, etc. for testing

# Get all sprite directories from data folder
$allThemes = Get-ChildItem -Path $gameThemesPath -Directory -Filter "sprites_*" -ErrorAction SilentlyContinue

Write-Host "Found $($allThemes.Count) themes in data directory" -ForegroundColor Gray

# Move ALL themes to ColorSchemes first
$movedCount = 0
foreach ($theme in $allThemes) {
    $sourcePath = $theme.FullName
    $destPath = Join-Path $colorSchemesPath $theme.Name

    # Skip if already exists in ColorSchemes
    if (Test-Path $destPath) {
        Write-Host "Theme $($theme.Name) already exists in ColorSchemes" -ForegroundColor Gray
        continue
    }

    Write-Host "Moving $($theme.Name) to ColorSchemes..." -ForegroundColor Gray
    Move-Item -Path $sourcePath -Destination $destPath -Force
    $movedCount++
}

if ($movedCount -gt 0) {
    Write-Host "Moved $movedCount themes to ColorSchemes" -ForegroundColor Green
}

# Now copy ONLY the dev themes back to data for F1/F2 testing
$colorSchemeThemes = Get-ChildItem -Path $colorSchemesPath -Directory -Filter "sprites_*" -ErrorAction SilentlyContinue
$copiedCount = 0

foreach ($theme in $colorSchemeThemes) {
    # Only copy core dev themes and test themes
    if ($coreDevThemes -contains $theme.Name -or $theme.Name -like "sprites_test_*") {
        $sourcePath = $theme.FullName
        $destPath = Join-Path $gameThemesPath $theme.Name

        # Skip if already exists
        if (Test-Path $destPath) {
            Write-Host "Dev theme $($theme.Name) already in data directory" -ForegroundColor Gray
            continue
        }

        Write-Host "Copying dev theme: $($theme.Name)" -ForegroundColor Gray
        Copy-Item -Path $sourcePath -Destination $destPath -Recurse -Force
        $copiedCount++
    }
}

if ($copiedCount -gt 0) {
    Write-Host "Copied $copiedCount dev themes to data directory" -ForegroundColor Green
}

# Verify final state
$remainingThemes = Get-ChildItem -Path $gameThemesPath -Directory -Filter "sprites_*" -ErrorAction SilentlyContinue
Write-Host "`nDevelopment mode setup complete!" -ForegroundColor Green
Write-Host "Active themes ($($remainingThemes.Count)):" -ForegroundColor Cyan
foreach ($theme in $remainingThemes) {
    Write-Host "  - $($theme.Name)" -ForegroundColor White
}

Write-Host "`nYou can now use F1/F2 to cycle through themes without crashing!" -ForegroundColor Yellow
Write-Host "Run SetupProduction.ps1 to restore all themes for release." -ForegroundColor Gray