#!/usr/bin/env pwsh
# Setup Production Mode - Restores all themes for release (F1/F2 should be disabled in code)

Write-Host "Setting up Production Mode with all themes..." -ForegroundColor Cyan

$modPath = $PSScriptRoot
$gameThemesPath = Join-Path $modPath "FFTIVC\data\enhanced\fftpack\unit"
$backupPath = Join-Path $modPath "ColorSchemes"

# Create game directory if it doesn't exist
if (-not (Test-Path $gameThemesPath)) {
    New-Item -ItemType Directory -Path $gameThemesPath -Force | Out-Null
    Write-Host "Created game themes directory: $gameThemesPath" -ForegroundColor Green
}

# Check if backup directory exists
if (-not (Test-Path $backupPath)) {
    Write-Host "No backup directory found. All themes might already be in place." -ForegroundColor Yellow
} else {
    # Get all themes from backup
    $backupThemes = Get-ChildItem -Path $backupPath -Directory -Filter "sprites_*" -ErrorAction SilentlyContinue

    if ($backupThemes.Count -eq 0) {
        Write-Host "No themes found in backup directory." -ForegroundColor Yellow
    } else {
        Write-Host "Found $($backupThemes.Count) themes in backup" -ForegroundColor Gray

        # Move all themes back to game directory (except test themes)
        $restoredCount = 0
        $skippedTestThemes = 0

        foreach ($theme in $backupThemes) {
            # Skip test themes in production
            if ($theme.Name -like "sprites_test_*") {
                Write-Host "Skipping test theme: $($theme.Name)" -ForegroundColor Yellow
                $skippedTestThemes++
                continue
            }

            $sourcePath = $theme.FullName
            $destPath = Join-Path $gameThemesPath $theme.Name

            # Remove destination if it exists
            if (Test-Path $destPath) {
                Remove-Item -Path $destPath -Recurse -Force
            }

            Write-Host "Restoring $($theme.Name)..." -ForegroundColor Gray
            Move-Item -Path $sourcePath -Destination $destPath -Force
            $restoredCount++
        }

        Write-Host "Restored $restoredCount production themes" -ForegroundColor Green
        if ($skippedTestThemes -gt 0) {
            Write-Host "Skipped $skippedTestThemes test themes" -ForegroundColor Yellow
        }
    }
}

# Also clean up any test themes that might be in the game directory
$gameThemes = Get-ChildItem -Path $gameThemesPath -Directory -Filter "sprites_test_*" -ErrorAction SilentlyContinue
if ($gameThemes.Count -gt 0) {
    Write-Host "`nRemoving test themes from production..." -ForegroundColor Yellow
    foreach ($testTheme in $gameThemes) {
        # Move test themes back to ColorSchemes for safekeeping
        $destPath = Join-Path $backupPath $testTheme.Name
        if (Test-Path $destPath) {
            Remove-Item -Path $destPath -Recurse -Force
        }
        Move-Item -Path $testTheme.FullName -Destination $destPath -Force
        Write-Host "Moved $($testTheme.Name) back to ColorSchemes" -ForegroundColor Gray
    }
}

# Verify final state
$finalThemes = Get-ChildItem -Path $gameThemesPath -Directory -Filter "sprites_*" -ErrorAction SilentlyContinue
Write-Host "`nProduction mode setup complete!" -ForegroundColor Green
Write-Host "Active themes ($($finalThemes.Count)):" -ForegroundColor Cyan

# List themes, highlighting if we have the expected 20
$themeList = @()
foreach ($theme in $finalThemes) {
    $themeName = $theme.Name -replace "sprites_", ""
    $themeList += $themeName
}

# Sort with original first, then alphabetically
$sortedThemes = @("original") + ($themeList | Where-Object { $_ -ne "original" } | Sort-Object)

foreach ($theme in $sortedThemes) {
    Write-Host "  - $theme" -ForegroundColor White
}

if ($finalThemes.Count -eq 20) {
    Write-Host "`nAll 20 production themes are ready!" -ForegroundColor Green
} else {
    Write-Host "`nWarning: Expected 20 themes but found $($finalThemes.Count)" -ForegroundColor Yellow
}

Write-Host "`nREMINDER: Disable F1/F2 hotkeys in the code for production!" -ForegroundColor Red
Write-Host "Users will need to restart the game after changing themes in Reloaded config." -ForegroundColor Gray