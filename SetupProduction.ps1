#!/usr/bin/env pwsh
# Setup Production Mode - Ensures all themes are in ColorSchemes for dynamic loading
# Clears data directory so only configured themes are copied at runtime

Write-Host "Setting up Production Mode with dynamic sprite loading..." -ForegroundColor Cyan

$modPath = $PSScriptRoot
$gameThemesPath = Join-Path $modPath "FFTIVC\data\enhanced\fftpack\unit"
$colorSchemesPath = Join-Path $modPath "ColorSchemes"

# Create game directory if it doesn't exist
if (-not (Test-Path $gameThemesPath)) {
    New-Item -ItemType Directory -Path $gameThemesPath -Force | Out-Null
    Write-Host "Created game themes directory: $gameThemesPath" -ForegroundColor Green
}

# Create ColorSchemes directory if it doesn't exist
if (-not (Test-Path $colorSchemesPath)) {
    New-Item -ItemType Directory -Path $colorSchemesPath -Force | Out-Null
    Write-Host "Created ColorSchemes directory: $colorSchemesPath" -ForegroundColor Green
}

# Move ALL themes from data to ColorSchemes
$dataThemes = Get-ChildItem -Path $gameThemesPath -Directory -Filter "sprites_*" -ErrorAction SilentlyContinue

if ($dataThemes.Count -eq 0) {
    Write-Host "No themes found in data directory." -ForegroundColor Yellow
} else {
    Write-Host "Found $($dataThemes.Count) themes in data directory" -ForegroundColor Gray

    $movedCount = 0
    foreach ($theme in $dataThemes) {
        $sourcePath = $theme.FullName
        $destPath = Join-Path $colorSchemesPath $theme.Name

        # If already exists in ColorSchemes, remove from data
        if (Test-Path $destPath) {
            Write-Host "Removing duplicate $($theme.Name) from data..." -ForegroundColor Gray
            Remove-Item -Path $sourcePath -Recurse -Force
        } else {
            Write-Host "Moving $($theme.Name) to ColorSchemes..." -ForegroundColor Gray
            Move-Item -Path $sourcePath -Destination $destPath -Force
            $movedCount++
        }
    }

    if ($movedCount -gt 0) {
        Write-Host "Moved $movedCount themes to ColorSchemes" -ForegroundColor Green
    }
}

# Clean up data directory - remove all sprites_* directories
Write-Host "`nCleaning data directory for production mode..." -ForegroundColor Yellow
$remainingThemes = Get-ChildItem -Path $gameThemesPath -Directory -Filter "sprites_*" -ErrorAction SilentlyContinue
foreach ($theme in $remainingThemes) {
    Remove-Item -Path $theme.FullName -Recurse -Force
    Write-Host "Removed $($theme.Name) from data directory" -ForegroundColor Gray
}

# Verify ColorSchemes directory contains all themes
$colorSchemeThemes = Get-ChildItem -Path $colorSchemesPath -Directory -Filter "sprites_*" -ErrorAction SilentlyContinue
$dataThemeCount = (Get-ChildItem -Path $gameThemesPath -Directory -Filter "sprites_*" -ErrorAction SilentlyContinue).Count

Write-Host "`nProduction mode setup complete!" -ForegroundColor Green
Write-Host "`nColorSchemes directory: $($colorSchemeThemes.Count) themes" -ForegroundColor Cyan
Write-Host "Data directory: $dataThemeCount themes (should be 0 for production)" -ForegroundColor Cyan

# List themes in ColorSchemes
if ($colorSchemeThemes.Count -gt 0) {
    Write-Host "`nAvailable themes in ColorSchemes:" -ForegroundColor White
    $themeList = @()
    foreach ($theme in $colorSchemeThemes) {
        $themeName = $theme.Name -replace "sprites_", ""
        if ($theme.Name -notlike "sprites_test_*") {
            $themeList += $themeName
        }
    }

    # Sort with original first, then alphabetically
    $sortedThemes = @("original") + ($themeList | Where-Object { $_ -ne "original" } | Sort-Object)

    foreach ($theme in $sortedThemes) {
        Write-Host "  - $theme" -ForegroundColor Gray
    }
}

if ($colorSchemeThemes.Count -ge 20) {
    Write-Host "`nAll production themes are ready in ColorSchemes!" -ForegroundColor Green
    Write-Host "The DynamicSpriteLoader will copy only configured themes at runtime." -ForegroundColor Yellow
} else {
    Write-Host "`nWarning: Expected 20+ themes but found $($colorSchemeThemes.Count)" -ForegroundColor Yellow
}

Write-Host "`nREMINDER:" -ForegroundColor Red
Write-Host "- F1/F2 hotkeys should be disabled for production" -ForegroundColor Gray
Write-Host "- Users configure themes via Reloaded-II config" -ForegroundColor Gray
Write-Host "- Only configured themes will be loaded at game startup" -ForegroundColor Gray
Write-Host "- No game restart needed when changing config!" -ForegroundColor Green