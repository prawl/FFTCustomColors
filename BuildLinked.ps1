# FFT Color Mod - Quick Build & Deploy Script
# Builds the mod with IL trimming and deploys directly to Reloaded-II mods folder

# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

Write-Host "Building FFT Color Mod..." -ForegroundColor Green

# Clean existing installation
$modPath = "$env:RELOADEDIIMODS/FFT_Color_Mod"
if (Test-Path $modPath) {
    Write-Host "Removing existing mod installation..." -ForegroundColor Yellow
    Remove-Item "$modPath/*" -Force -Recurse -ErrorAction SilentlyContinue
}

# Build and publish with IL trimming for smaller size
Write-Host "Publishing to Reloaded-II mods folder..." -ForegroundColor Cyan
dotnet publish "./FFTColorMod.csproj" -c Release -o "$modPath" /p:OutputPath="./bin/Release" /p:ReloadedILLink="true"

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build successful! Mod installed to: $modPath" -ForegroundColor Green
    Write-Host "You can now enable the mod in Reloaded-II" -ForegroundColor Green
} else {
    Write-Host "Build failed! Check the output above for errors." -ForegroundColor Red
}

# Restore Working Directory
Pop-Location