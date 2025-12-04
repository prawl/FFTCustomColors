# FFT Color Mod - Test Runner
# This script ensures tests run with the exact correct command every time

Write-Host "Cleaning build artifacts..." -ForegroundColor Yellow
Remove-Item -Path "bin", "obj" -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Restoring test project packages..." -ForegroundColor Yellow
dotnet restore FFTColorMod.Tests.csproj

Write-Host "Building test project..." -ForegroundColor Yellow
dotnet build FFTColorMod.Tests.csproj --no-restore

Write-Host "Running tests..." -ForegroundColor Green
dotnet test FFTColorMod.Tests.csproj --verbosity minimal --no-build

Write-Host "`nTest run complete!" -ForegroundColor Cyan