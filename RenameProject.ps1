# PowerShell script to rename FFT_Color_Mod to FFTColorCustomizer
Write-Host "Starting project rename from FFT_Color_Mod to FFTColorCustomizer..." -ForegroundColor Cyan

# Step 1: Update solution file content
Write-Host "`nStep 1: Updating solution file content..." -ForegroundColor Yellow
$slnContent = Get-Content "FFTColorMod.sln" -Raw
$slnContent = $slnContent -replace 'FFTColorMod', 'FFTColorCustomizer'
Set-Content -Path "FFTColorCustomizer.sln" -Value $slnContent
Write-Host "Created FFTColorCustomizer.sln" -ForegroundColor Green

# Step 2: Update project files
Write-Host "`nStep 2: Updating project files..." -ForegroundColor Yellow

# Update main project file
$projContent = Get-Content "ColorMod\FFTColorMod.csproj" -Raw
$projContent = $projContent -replace 'FFTColorMod', 'FFTColorCustomizer'
$projContent = $projContent -replace 'FFT_Color_Mod', 'FFTColorCustomizer'
Set-Content -Path "ColorMod\FFTColorCustomizer.csproj" -Value $projContent
Write-Host "Created ColorMod\FFTColorCustomizer.csproj" -ForegroundColor Green

# Update test project file
$testProjContent = Get-Content "FFTColorMod.Tests.csproj" -Raw
$testProjContent = $testProjContent -replace 'FFTColorMod', 'FFTColorCustomizer'
$testProjContent = $testProjContent -replace 'FFT_Color_Mod', 'FFTColorCustomizer'
Set-Content -Path "FFTColorCustomizer.Tests.csproj" -Value $testProjContent
Write-Host "Created FFTColorCustomizer.Tests.csproj" -ForegroundColor Green

# Step 3: Update all C# files to use new namespace
Write-Host "`nStep 3: Updating namespaces in all C# files..." -ForegroundColor Yellow
$csFiles = Get-ChildItem -Path . -Filter *.cs -Recurse
$count = 0
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw
    if ($content -match 'FFTColorMod') {
        $content = $content -replace 'namespace FFTColorMod', 'namespace FFTColorCustomizer'
        $content = $content -replace 'using FFTColorMod', 'using FFTColorCustomizer'
        $content = $content -replace 'nameof\(FFTColorMod\)', 'nameof(FFTColorCustomizer)'
        Set-Content -Path $file.FullName -Value $content
        $count++
    }
}
Write-Host "Updated $count C# files" -ForegroundColor Green

# Step 4: Update ModConfig.json
Write-Host "`nStep 4: Updating ModConfig.json..." -ForegroundColor Yellow
$modConfigPath = "ColorMod\ModConfig.json"
if (Test-Path $modConfigPath) {
    $modConfig = Get-Content $modConfigPath -Raw
    $modConfig = $modConfig -replace 'FFTColorMod\.dll', 'FFTColorCustomizer.dll'
    $modConfig = $modConfig -replace 'FFTColorMod', 'FFTColorCustomizer'
    $modConfig = $modConfig -replace 'FFT Custom Colors', 'FFT Color Customizer'
    $modConfig = $modConfig -replace 'ptyra\.fft\.colormod', 'ptyra.fft.colorcustomizer'
    $modConfig = $modConfig -replace 'FFT_Color_Mod', 'FFTColorCustomizer'
    Set-Content -Path $modConfigPath -Value $modConfig
    Write-Host "Updated ModConfig.json" -ForegroundColor Green
}

# Step 5: Update build scripts
Write-Host "`nStep 5: Updating build scripts..." -ForegroundColor Yellow
$buildScripts = @("BuildLinked.ps1", "BuildLocal.ps1", "BuildProduction.ps1")
foreach ($script in $buildScripts) {
    if (Test-Path $script) {
        $content = Get-Content $script -Raw
        $content = $content -replace 'FFT_Color_Mod', 'FFTColorCustomizer'
        $content = $content -replace 'FFTColorMod', 'FFTColorCustomizer'
        Set-Content -Path $script -Value $content
        Write-Host "Updated $script" -ForegroundColor Green
    }
}

# Step 6: Update any JSON data files
Write-Host "`nStep 6: Updating JSON data files..." -ForegroundColor Yellow
$jsonFiles = Get-ChildItem -Path . -Filter *.json -Recurse | Where-Object { $_.Name -ne "ModConfig.json" }
foreach ($file in $jsonFiles) {
    $content = Get-Content $file.FullName -Raw
    if ($content -match 'FFTColorMod' -or $content -match 'FFT_Color_Mod') {
        $content = $content -replace 'FFTColorMod', 'FFTColorCustomizer'
        $content = $content -replace 'FFT_Color_Mod', 'FFTColorCustomizer'
        Set-Content -Path $file.FullName -Value $content
        Write-Host "Updated $($file.Name)" -ForegroundColor Green
    }
}

# Step 7: Update README and documentation
Write-Host "`nStep 7: Updating documentation files..." -ForegroundColor Yellow
$mdFiles = Get-ChildItem -Path . -Filter *.md -Recurse
foreach ($file in $mdFiles) {
    $content = Get-Content $file.FullName -Raw
    if ($content -match 'FFT_Color_Mod' -or $content -match 'FFTColorMod') {
        $content = $content -replace 'FFT_Color_Mod', 'FFTColorCustomizer'
        $content = $content -replace 'FFTColorMod', 'FFTColorCustomizer'
        $content = $content -replace 'FFT Color Mod', 'FFT Color Customizer'
        Set-Content -Path $file.FullName -Value $content
        Write-Host "Updated $($file.Name)" -ForegroundColor Green
    }
}

# Step 8: Delete old project files
Write-Host "`nStep 8: Cleaning up old files..." -ForegroundColor Yellow
Remove-Item "FFTColorMod.sln" -Force -ErrorAction SilentlyContinue
Remove-Item "ColorMod\FFTColorMod.csproj" -Force -ErrorAction SilentlyContinue
Remove-Item "FFTColorMod.Tests.csproj" -Force -ErrorAction SilentlyContinue
Write-Host "Removed old project files" -ForegroundColor Green

Write-Host "`n============================================" -ForegroundColor Cyan
Write-Host "Project rename completed!" -ForegroundColor Green
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Close any open IDEs (Visual Studio, VS Code)" -ForegroundColor White
Write-Host "2. Rename the root folder from FFT_Color_Mod to FFTColorCustomizer" -ForegroundColor White
Write-Host "3. Update any external references or shortcuts" -ForegroundColor White
Write-Host "4. Run 'dotnet build' to verify everything works" -ForegroundColor White
Write-Host "============================================" -ForegroundColor Cyan