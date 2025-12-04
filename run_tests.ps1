# FFT Color Mod - Test Runner
# Reliable test execution with proper build order

Write-Host "Cleaning build artifacts..." -ForegroundColor Yellow
Remove-Item -Path "bin", "obj" -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Restoring and building projects..." -ForegroundColor Yellow
# Use the single test command which handles everything properly
dotnet test FFTColorMod.Tests.csproj --verbosity minimal

if ($LASTEXITCODE -ne 0) {
    Write-Host "Tests failed!" -ForegroundColor Red
    exit 1
}

Write-Host "`nTest run complete!" -ForegroundColor Cyan