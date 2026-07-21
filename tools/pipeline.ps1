# tools/pipeline.ps1 - the shared pipeline prefix for BuildLinked.ps1 (dev deploy)
# and Publish.ps1 (release zip). Dot-source it; everything here lands in the
# caller's scope. Mirrors the sibling FFTLivingWeapons split: one copy of the
# build + shared asset staging + verification, two callers, no drift.
#
# The required-file/floor manifest is tools/package_manifest.json (CC-10) - the
# SAME file tools/analyze.py gates the release zip against. Test-ManifestTree
# below is its disk-side mirror, so BuildLinked's deploy verification and the
# Publish zip gates can never check different lists.

# Repo root, resolved from this file's own location so everything works no
# matter what cwd the caller happens to be in when it dot-sources us.
$PipelineRepoRoot = Split-Path -Parent $PSScriptRoot

# The active Reloaded ModId (must match ColorMod/ModConfig.json "ModId"). A folder
# literally named "FFTColorCustomizer" shares this ModId and silently shadows the
# real install, so both scripts treat that name as a legacy shadow to remove.
$ColorModId = "paxtrick.fft.colorcustomizer"
$LegacyShadowFolderName = "FFTColorCustomizer"

# The three shippable Ramza battle TEX themes (RamzaThemes/<name>/tex_830..835.bin).
# One list for both the dev deploy and the release stage; the manifest floor
# (RamzaThemes/* min 18 = 3 themes x 6 tex) fails red if one goes missing.
$RamzaThemeNames = @("dark_knight", "white_heretic", "crimson_blade")

$PackageManifestPath = Join-Path $PSScriptRoot "package_manifest.json"

function Get-PackageManifest {
    if (-not (Test-Path $PackageManifestPath)) {
        throw "package manifest not found: $PackageManifestPath"
    }
    return Get-Content $PackageManifestPath -Raw | ConvertFrom-Json
}

function Test-ManifestTree {
    # Disk-side mirror of tools/analyze.py's zip gate (CC-10), driven by the SAME
    # tools/package_manifest.json: required entries, JSON parse checks, per-prefix
    # file-count floors, and the sprites_* theme-dir floor - evaluated against a
    # deployed/staged directory instead of a zip. Returns an array of error
    # strings; empty means pass.
    param([Parameter(Mandatory = $true)][string]$Root)

    $manifest = Get-PackageManifest
    $errs = @()

    $rootFull = (Get-Item $Root).FullName
    $files = @(Get-ChildItem $rootFull -Recurse -File | ForEach-Object {
        $_.FullName.Substring($rootFull.Length + 1) -replace '\\', '/'
    })

    foreach ($req in $manifest.required_entries) {
        if ($files -notcontains $req) { $errs += "required entry missing: $req" }
    }

    foreach ($jpath in $manifest.parse_json) {
        $full = Join-Path $rootFull ($jpath -replace '/', '\')
        if (Test-Path $full) {
            try { Get-Content $full -Raw | ConvertFrom-Json | Out-Null }
            catch { $errs += "JSON does not parse: $jpath ($($_.Exception.Message))" }
        }
    }

    foreach ($floor in $manifest.floors) {
        $count = @($files | Where-Object { $_.StartsWith($floor.prefix) -and $_.EndsWith($floor.suffix) }).Count
        if ($count -lt $floor.min) {
            $errs += "floor violated: $count files at $($floor.prefix)*$($floor.suffix) (need >= $($floor.min): $($floor.desc))"
        } else {
            Write-Host "  [OK] $count files at $($floor.prefix)*$($floor.suffix) (floor $($floor.min))" -ForegroundColor Green
        }
    }

    $unitPrefix = "FFTIVC/data/enhanced/fftpack/unit/"
    $themeDirs = @($files |
        Where-Object { $_.StartsWith($unitPrefix + "sprites_") } |
        ForEach-Object { ($_.Substring($unitPrefix.Length) -split '/')[0] } |
        Sort-Object -Unique)
    if ($themeDirs.Count -lt $manifest.min_theme_dirs) {
        $errs += "only $($themeDirs.Count) sprites_* theme dirs under unit/ (need >= $($manifest.min_theme_dirs))"
    } else {
        Write-Host "  [OK] $($themeDirs.Count) sprites_* theme dirs (floor $($manifest.min_theme_dirs))" -ForegroundColor Green
    }

    return $errs
}

function Test-MonsterFamilyAssets {
    # Verify EVERY monster family's assets are present (section mapping + HD preview
    # BMP + original sprite bin). Driven by the deployed Data/SectionMappings/Monster
    # mappings, so new families are covered automatically and a missing asset fails
    # red (no silent drift). Returns an array of error strings; empty means pass.
    param([Parameter(Mandatory = $true)][string]$Root)

    $errs = @()
    $monsterMapDir = Join-Path $Root "Data\SectionMappings\Monster"
    if (-not (Test-Path $monsterMapDir)) {
        return @("Monster section-mapping dir missing: $monsterMapDir")
    }

    $monsterMaps = @(Get-ChildItem "$monsterMapDir\*.json" -File)
    Write-Host "Verifying $($monsterMaps.Count) monster families..." -ForegroundColor Cyan
    foreach ($map in $monsterMaps) {
        $family = [System.IO.Path]::GetFileNameWithoutExtension($map.Name)
        $sprite = (Get-Content $map.FullName -Raw | ConvertFrom-Json).sprite
        # A mapping with no sprite field must fail red, not pass: interpolating
        # $null builds a path ending in "sprites_original\", and Test-Path on that
        # resolves the DIRECTORY as true (silently green on malformed input).
        if ([string]::IsNullOrWhiteSpace([string]$sprite)) {
            $errs += "$family mapping has no sprite field: Data/SectionMappings/Monster/$($map.Name)"
            continue
        }
        $bmp = Get-ChildItem "$Root\Images\$family\original\*.bmp" -File -ErrorAction SilentlyContinue | Select-Object -First 1
        $binPath = "$Root\FFTIVC\data\enhanced\fftpack\unit\sprites_original\$sprite"
        if (-not $bmp) { $errs += "$family HD preview BMP missing: Images/$family/original/*.bmp" }
        if (-not (Test-Path $binPath)) { $errs += "$family original sprite missing: $binPath" }
        if ($bmp -and (Test-Path $binPath)) { Write-Host "  [OK] $family ($sprite + $($bmp.Name))" -ForegroundColor Green }
    }
    return $errs
}

function Invoke-UnitTestGate {
    # The contract-test gate (CC-16): the work-ledger enforcement (TodoContractTests)
    # and the logging contract (LogContractTests) run as a hard gate before any
    # deploy or package, exactly as the sibling FFT mods run theirs. FILTERED to the
    # two contract suites so the dev loop does not pay for the full 1200-test suite;
    # CI still runs everything. ONE canonical flag set, so a gate that passes
    # locally passed under the same conditions everywhere.
    param(
        [Parameter(Mandatory = $true)][ValidateSet('DEPLOY', 'PACKAGE')]
        [string]$FailVerb
    )

    Write-Host "Running the contract-test gate (Todo + Log contracts)..." -ForegroundColor Cyan
    & dotnet test "$PipelineRepoRoot\FFTColorCustomizer.Tests.csproj" --nologo -p:WarningLevel=0 `
        --filter "FullyQualifiedName~TodoContractTests|FullyQualifiedName~LogContractTests"
    if ($LASTEXITCODE -ne 0) {
        throw "REFUSING TO ${FailVerb}: the contract tests failed (see above)."
    }
    Write-Host "  -> Contract gate green." -ForegroundColor Green
}

function Invoke-ColorModBuild {
    # Build the mod DLL (+ dependency tree) into $OutDir. Two flavors, one function:
    #   -Dev     the BuildLinked deploy build (Reloaded IL trimming for size)
    #   default  the release build (clean + restore first, framework-dependent)
    param(
        [Parameter(Mandatory = $true)][string]$OutDir,
        [switch]$Dev,
        [string]$TempBuildPath = "Publish/TempBuild"
    )

    $proj = "$PipelineRepoRoot\ColorMod\FFTColorCustomizer.csproj"
    if ($Dev) {
        dotnet publish $proj -c Release -o $OutDir /p:OutputPath="./bin/Release" /p:ReloadedILLink="true"
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)." }
    } else {
        Write-Host "  -> Cleaning solution..."
        dotnet clean $proj -c Release | Out-Null
        Write-Host "  -> Restoring NuGet packages..."
        dotnet restore $proj | Out-Null
        Write-Host "  -> Building Release configuration..."
        dotnet publish $proj -c Release --self-contained false -o $OutDir /p:OutputPath="$TempBuildPath"
        if ($LASTEXITCODE -ne 0) { throw "Build failed!" }
    }
}

function Copy-ModIcon {
    # Preview.png is the ModIcon. ColorMod/Preview.png wins; thunder_god.png is the
    # dev fallback (the dev script historically copied thunder_god first and then
    # overwrote it with Preview.png, so that IS the old effective dev behavior).
    # -RequirePreview (the release flavor) refuses the fallback: the old release
    # flow had none, so a deleted Preview.png failed the zip gate loudly instead
    # of silently shipping a substitute icon - keep it that way.
    param(
        [Parameter(Mandatory = $true)][string]$Dest,
        [switch]$RequirePreview
    )

    if (Test-Path "$PipelineRepoRoot\ColorMod\Preview.png") {
        Write-Host "  -> Copying Preview.png (mod icon)..." -ForegroundColor Cyan
        Copy-Item "$PipelineRepoRoot\ColorMod\Preview.png" "$Dest\Preview.png" -Force
    } elseif ($RequirePreview) {
        throw "ColorMod/Preview.png missing (the release ModIcon; refusing to substitute a fallback)."
    } elseif (Test-Path "$PipelineRepoRoot\ColorMod\Images\thunder_god.png") {
        Write-Host "  -> Copying mod icon (thunder_god.png)..." -ForegroundColor Cyan
        Copy-Item "$PipelineRepoRoot\ColorMod\Images\thunder_god.png" "$Dest\Preview.png" -Force
    }
}

function Copy-SqliteNativeToRoot {
    # SQLite native library must ALSO sit at the mod root: Reloaded-II loads the mod
    # in a context where runtimes/<rid>/native probing alone is not reliable, so the
    # pinvoke falls back to same-directory resolution. BuildLinked has done this for
    # dev deploys since c686d852; the release zip never did, which crashed Ramza
    # theme saves for every zip user (CC-10). Missing input is a hard failure for
    # BOTH flavors now - a build output without the native DLL is a broken mod.
    param([Parameter(Mandatory = $true)][string]$Dest)

    $sqliteNative = "$Dest\runtimes\win-x64\native\e_sqlite3.dll"
    if (-not (Test-Path $sqliteNative)) {
        throw "runtimes/win-x64/native/e_sqlite3.dll missing from build output at $Dest!"
    }
    Copy-Item $sqliteNative "$Dest\e_sqlite3.dll" -Force
    Write-Host "  -> Copied e_sqlite3.dll to mod root (Reloaded-II native resolution)" -ForegroundColor Green
}

function Copy-DataAssets {
    # Data/ (StoryCharacters.json, JobClasses.json, SectionMappings/, nxd sources)
    # plus the charclut.nxd deploy into the FFTIVC nxd path (Ramza color customization).
    param([Parameter(Mandatory = $true)][string]$Dest)

    $dataSource = "$PipelineRepoRoot\ColorMod\Data"
    if (Test-Path $dataSource) {
        Write-Host "  -> Copying Data folder..." -ForegroundColor Cyan
        Copy-Item $dataSource -Destination $Dest -Recurse -Force
        $dataCount = (Get-ChildItem "$Dest\Data" -Recurse -Filter "*.json").Count
        Write-Host "  -> Copied $dataCount data files (including subdirectories)"
    } else {
        Write-Host "  -> Warning: Data folder not found at: $dataSource" -ForegroundColor Yellow
    }

    $nxdSource = "$PipelineRepoRoot\ColorMod\Data\nxd\charclut.nxd"
    $nxdDestDir = "$Dest\FFTIVC\data\enhanced\nxd"
    if (Test-Path $nxdSource) {
        Write-Host "  -> Deploying charclut.nxd for Ramza customization..." -ForegroundColor Cyan
        New-Item -ItemType Directory -Force -Path $nxdDestDir | Out-Null
        Copy-Item $nxdSource -Destination "$nxdDestDir\charclut.nxd" -Force
        Write-Host "  -> Deployed charclut.nxd" -ForegroundColor Green
    } else {
        Write-Host "  -> Warning: charclut.nxd not found at $nxdSource" -ForegroundColor Yellow
    }
}

function Copy-ImageAssets {
    # Images/ - HD BMP sprite-sheet previews per character folder + root PNGs
    # (like thunder_god.png). The theme editor reads these at runtime.
    param([Parameter(Mandatory = $true)][string]$Dest)

    $imagesSource = "$PipelineRepoRoot\ColorMod\Images"
    if (-not (Test-Path $imagesSource)) { return }

    Write-Host "  -> Copying Images folder with HD sprite previews..." -ForegroundColor Cyan
    $imagesDest = "$Dest\Images"
    New-Item -ItemType Directory -Force -Path $imagesDest | Out-Null

    foreach ($folder in Get-ChildItem $imagesSource -Directory) {
        Copy-Item $folder.FullName "$imagesDest\$($folder.Name)" -Recurse -Force
    }
    Copy-Item "$imagesSource\*.png" $imagesDest -Force -ErrorAction SilentlyContinue

    $totalBmpCount = (Get-ChildItem $imagesDest -Recurse -Filter "*.bmp" -ErrorAction SilentlyContinue | Measure-Object).Count
    $totalPngCount = (Get-ChildItem $imagesDest -Recurse -Filter "*.png" -ErrorAction SilentlyContinue | Measure-Object).Count
    Write-Host "  -> Copied $totalBmpCount BMP sprite sheets and $totalPngCount PNG images" -ForegroundColor Green
}

function Copy-RamzaThemeAssets {
    # RamzaThemes/<theme>/tex_830..835.bin - the Ramza battle TEX themes, staged
    # OUTSIDE the game scan path (the runtime swaps them in). A theme ships only
    # when all 6 TEX files are present (the dev script's completeness check, now
    # applied to both flavors; an incomplete theme is a loud warning, not a ship).
    param([Parameter(Mandatory = $true)][string]$Dest)

    $ramzaThemesSource = "$PipelineRepoRoot\ColorMod\RamzaThemes"
    if (-not (Test-Path $ramzaThemesSource)) { return }

    Write-Host "  -> Copying Ramza tex themes to RamzaThemes folder..." -ForegroundColor Cyan
    $ramzaThemesDest = "$Dest\RamzaThemes"
    $copiedCount = 0
    foreach ($themeName in $RamzaThemeNames) {
        $themePath = "$ramzaThemesSource\$themeName"
        if (-not (Test-Path $themePath)) { continue }
        $ramzaTexFiles = @()
        foreach ($num in 830..835) {
            $texFile = "$themePath\tex_$num.bin"
            if (Test-Path $texFile) { $ramzaTexFiles += $texFile }
        }
        if ($ramzaTexFiles.Count -eq 6) {
            $themeDest = "$ramzaThemesDest\$themeName"
            New-Item -ItemType Directory -Force -Path $themeDest | Out-Null
            foreach ($texFile in $ramzaTexFiles) { Copy-Item $texFile -Destination $themeDest -Force }
            Write-Host "    -> Copied 6 tex files for $themeName theme" -ForegroundColor Green
            $copiedCount++
        } elseif ($ramzaTexFiles.Count -gt 0) {
            Write-Host "    -> Warning: $themeName has incomplete tex files (found $($ramzaTexFiles.Count)/6)" -ForegroundColor Yellow
        }
    }
    if ($copiedCount -eq 0) {
        Write-Host "  -> No valid Ramza themes found to deploy" -ForegroundColor Yellow
    }
}
