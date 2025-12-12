#!/usr/bin/env pwsh
# Setup Development Mode - Keeps only 5 themes to prevent crashes during F1/F2 testing

Write-Host "Setting up Development Mode with limited themes..." -ForegroundColor Cyan

$modPath = $PSScriptRoot
$gameThemesPath = Join-Path $modPath "FFTIVC\data\enhanced\fftpack\unit"
$backupPath = Join-Path $modPath "ColorSchemes"

# Create backup directory if it doesn't exist
if (-not (Test-Path $backupPath)) {
    New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
    Write-Host "Created backup directory: $backupPath" -ForegroundColor Green
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

# Get all sprite directories
$allThemes = Get-ChildItem -Path $gameThemesPath -Directory -Filter "sprites_*" -ErrorAction SilentlyContinue

if ($allThemes.Count -eq 0) {
    Write-Host "No themes found in game directory. They might already be in backup." -ForegroundColor Yellow

    # Try to restore dev themes from backup
    $backupThemes = Get-ChildItem -Path $backupPath -Directory -Filter "sprites_*" -ErrorAction SilentlyContinue
    foreach ($theme in $backupThemes) {
        # Keep core dev themes and any test themes
        if ($coreDevThemes -contains $theme.Name -or $theme.Name -like "sprites_test_*") {
            $sourcePath = $theme.FullName
            $destPath = Join-Path $gameThemesPath $theme.Name
            Write-Host "Restoring theme: $($theme.Name)" -ForegroundColor Gray
            Move-Item -Path $sourcePath -Destination $destPath -Force
        }
    }
} else {
    Write-Host "Found $($allThemes.Count) themes in game directory" -ForegroundColor Gray

    # Move non-dev themes to backup (keep core themes and test themes)
    $movedCount = 0
    foreach ($theme in $allThemes) {
        # Keep core dev themes and any test themes
        if ($coreDevThemes -notcontains $theme.Name -and $theme.Name -notlike "sprites_test_*") {
            $sourcePath = $theme.FullName
            $destPath = Join-Path $backupPath $theme.Name

            # Remove destination if it exists
            if (Test-Path $destPath) {
                Remove-Item -Path $destPath -Recurse -Force
            }

            Write-Host "Moving $($theme.Name) to backup..." -ForegroundColor Gray
            Move-Item -Path $sourcePath -Destination $destPath -Force
            $movedCount++
        }
    }

    Write-Host "Moved $movedCount themes to backup" -ForegroundColor Green
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