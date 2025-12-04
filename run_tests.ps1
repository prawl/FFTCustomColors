# FFT Color Mod - Test Runner
# This script ensures tests run with the exact correct command every time

Write-Host "Restoring all project packages..." -ForegroundColor Yellow
dotnet restore FFTColorMod.csproj
dotnet restore FFTColorMod.Tests.csproj

Write-Host "Building main project..." -ForegroundColor Yellow
dotnet build FFTColorMod.csproj --no-restore -c Debug

Write-Host "Building test project..." -ForegroundColor Yellow
dotnet build FFTColorMod.Tests.csproj --no-restore -c Debug

# Check if the test DLL actually exists
$TestDLL = "bin\Debug\net8.0-windows\FFTColorMod.Tests.dll"
if (-not (Test-Path $TestDLL)) {
    Write-Host "ERROR: Test DLL not found at $TestDLL" -ForegroundColor Red
    Write-Host "Trying to rebuild test project explicitly..." -ForegroundColor Red
    dotnet build FFTColorMod.Tests.csproj -c Debug

    if (-not (Test-Path $TestDLL)) {
        Write-Host "FATAL: Test DLL still not found after rebuild!" -ForegroundColor Red
        exit 1
    }
}

Write-Host "Running tests..." -ForegroundColor Green
dotnet test $TestDLL --verbosity minimal

Write-Host "`nTest run complete!" -ForegroundColor Cyan