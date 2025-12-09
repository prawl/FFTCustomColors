<#
.SYNOPSIS
    Builds and Publishes FFT Color Mod for Reloaded-II
.DESCRIPTION
    Windows script to Build and Publish the FFT Color Mod.
    By default, published items will be output to a directory called `Publish/ToUpload`.

.PARAMETER ProjectPath
    Path to the project to be built.
    Default: ColorMod/FFTColorMod.csproj

.PARAMETER PackageName
    Name of the package to be built.
    Default: FFT_Color_Mod

.PARAMETER PublishOutputDir
    Default: "Publish/ToUpload"
    Declares the directory for placing the output files.

.PARAMETER BuildR2R
    Default: $False
    Builds the mod using Ready to Run optimization for faster startup.

.PARAMETER MakeDelta
    Default: $False
    Set to true to create Delta packages for smaller updates.

.PARAMETER UseGitHubDelta
    Default: $False
    If true, sources the last version from GitHub for delta creation.

.PARAMETER GitHubUserName
    Default: ptyRa
    GitHub username for obtaining previous releases.

.PARAMETER GitHubRepoName
    Default: FFT_Color_Mod
    GitHub repository name.

.PARAMETER PublishGeneric
    Default: $True
    Publishes a generic package (ZIP file).

.EXAMPLE
  .\Publish.ps1
  # Simple build and publish

.EXAMPLE
  .\Publish.ps1 -BuildR2R $true
  # Build with Ready-to-Run optimization

.EXAMPLE
  .\Publish.ps1 -MakeDelta $true -UseGitHubDelta $true
  # Create delta update from GitHub release
#>

[cmdletbinding()]
param (
    $Build=$True,
    $BuildR2R=$False,
    $RemoveExe=$True,
    $UseScriptDirectory=$True,
    $MakeDelta=$False,
    $IsPrerelease=$False,

    ## => Project Config <= ##
    $ProjectPath = "ColorMod/FFTColorMod.csproj",
    $PackageName = "FFT_Color_Mod",
    $PublishOutputDir = "Publish/ToUpload",

    ## => Delta Config <= ##
    $UseGitHubDelta = $False,
    $GitHubUserName = "ptyRa",
    $GitHubRepoName = "FFT_Color_Mod",
    $GitHubInheritVersionFromTag = $True,

    ## => Publish Config <= ##
    $PublishGeneric = $True,
    $PublishNuGet = $False,
    $PublishGameBanana = $False
)

## => Directories <= ##
$publishBuildDirectory = "Publish/Builds/CurrentVersion"
$deltaDirectory = "Publish/Builds/LastVersion"
$PublishGenericDirectory = "$PublishOutputDir/Generic"
$TempDirectory = [System.IO.Path]::GetTempPath() + [System.IO.Path]::GetRandomFileName()
$TempDirectoryBuild = "$TempDirectory/build"

## => Tools <= ##
$reloadedToolsPath = "./Publish/Tools/Reloaded-Tools"
$updateToolsPath = "./Publish/Tools/Update-Tools"
$reloadedToolPath = "$reloadedToolsPath/Reloaded.Publisher.exe"
$updateToolPath = "$updateToolsPath/Sewer56.Update.Tool.dll"

## => Functions <= ##

function Write-Status {
    param($Message, $Color = "Green")
    Write-Host "`n==> $Message" -ForegroundColor $Color
}

function Get-Tools {
    $ProgressPreference = 'SilentlyContinue'

    if (-not(Test-Path -Path $reloadedToolsPath -PathType Any)) {
        Write-Status "Downloading Reloaded Tools..." "Yellow"
        Invoke-WebRequest -Uri "https://github.com/Reloaded-Project/Reloaded-II/releases/latest/download/Tools.zip" -OutFile "$TempDirectory/Tools.zip"
        Expand-Archive -LiteralPath "$TempDirectory/Tools.zip" -DestinationPath $reloadedToolsPath
        Remove-Item "$TempDirectory/Tools.zip" -ErrorAction SilentlyContinue
    }

    if ($MakeDelta -and -not(Test-Path -Path $updateToolsPath -PathType Any)) {
        Write-Status "Downloading Update Library Tools..." "Yellow"
        Invoke-WebRequest -Uri "https://github.com/Sewer56/Update/releases/latest/download/Sewer56.Update.Tool.zip" -OutFile "$TempDirectory/Sewer56.Update.Tool.zip"
        Expand-Archive -LiteralPath "$TempDirectory/Sewer56.Update.Tool.zip" -DestinationPath $updateToolsPath
        Remove-Item "$TempDirectory/Sewer56.Update.Tool.zip" -ErrorAction SilentlyContinue
    }
}

function Build {
    Write-Status "Building FFT Color Mod..." "Cyan"

    # Clean build directory
    Remove-Item $publishBuildDirectory -Recurse -ErrorAction SilentlyContinue
    New-Item $publishBuildDirectory -ItemType Directory -ErrorAction SilentlyContinue | Out-Null

    # Restore and clean
    dotnet restore $ProjectPath
    dotnet clean $ProjectPath

    if ($BuildR2R) {
        Write-Status "Building with Ready-to-Run optimization..." "Cyan"
        dotnet publish $ProjectPath -c Release -r win-x86 --self-contained false -o "$publishBuildDirectory/x86" /p:PublishReadyToRun=true /p:OutputPath="$TempDirectoryBuild/x86"
        dotnet publish $ProjectPath -c Release -r win-x64 --self-contained false -o "$publishBuildDirectory/x64" /p:PublishReadyToRun=true /p:OutputPath="$TempDirectoryBuild/x64"

        # Move shared files
        Move-Item -Path "$publishBuildDirectory/x86/ModConfig.json" -Destination "$publishBuildDirectory/ModConfig.json" -ErrorAction SilentlyContinue
        Move-Item -Path "$publishBuildDirectory/x86/Preview.png" -Destination "$publishBuildDirectory/Preview.png" -ErrorAction SilentlyContinue
        Remove-Item "$publishBuildDirectory/x64/Preview.png" -ErrorAction SilentlyContinue
        Remove-Item "$publishBuildDirectory/x64/ModConfig.json" -ErrorAction SilentlyContinue
    }
    else {
        Write-Status "Building standard release..." "Cyan"
        dotnet publish $ProjectPath -c Release --self-contained false -o "$publishBuildDirectory" /p:OutputPath="$TempDirectoryBuild"
    }

    # Cleanup
    Remove-Item $TempDirectoryBuild -Recurse -ErrorAction SilentlyContinue
    if ($RemoveExe) {
        Get-ChildItem $publishBuildDirectory -Include *.exe -Recurse | Remove-Item -Force -Recurse
    }
    Get-ChildItem $publishBuildDirectory -Include *.pdb -Recurse | Remove-Item -Force -Recurse
    Get-ChildItem $publishBuildDirectory -Include *.xml -Recurse | Remove-Item -Force -Recurse

    # Remove test-related files
    Get-ChildItem $publishBuildDirectory -Include *Test* -Recurse | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue
    Get-ChildItem $publishBuildDirectory -Include xunit* -Recurse | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue
}

function Get-Last-Version {
    if ($UseGitHubDelta) {
        Write-Status "Downloading previous version from GitHub..." "Yellow"
        Remove-Item $deltaDirectory -Recurse -ErrorAction SilentlyContinue
        New-Item $deltaDirectory -ItemType Directory -ErrorAction SilentlyContinue | Out-Null

        $arguments = "DownloadPackage --extract --outputpath `"$deltaDirectory`" --allowprereleases `"$IsPrerelease`""
        $arguments += " --source GitHub --githubusername `"$GitHubUserName`" --githubrepositoryname `"$GitHubRepoName`" --githubinheritversionfromtag `"$GitHubInheritVersionFromTag`""

        Invoke-Expression "dotnet `"$updateToolPath`" $arguments"
    }
}

function Publish-Generic {
    Write-Status "Creating generic package..." "Green"

    Remove-Item $PublishGenericDirectory -Recurse -ErrorAction SilentlyContinue
    New-Item $PublishGenericDirectory -ItemType Directory -ErrorAction SilentlyContinue | Out-Null

    $arguments = "--modfolder `"$publishBuildDirectory`" --packagename `"$PackageName`" --outputfolder `"$PublishGenericDirectory`" --publishtarget Default"

    if ($MakeDelta -and $UseGitHubDelta) {
        $arguments += " --olderversionfolders `"$deltaDirectory`""
    }

    $command = "$reloadedToolPath $arguments"
    Invoke-Expression $command
}

function Cleanup {
    Remove-Item $PublishOutputDir -Recurse -ErrorAction SilentlyContinue
    Remove-Item $deltaDirectory -Recurse -ErrorAction SilentlyContinue
}

## => Main Script <= ##

# Set working directory
if ($UseScriptDirectory) {
    Split-Path $MyInvocation.MyCommand.Path | Push-Location
    [Environment]::CurrentDirectory = $PWD
}

# Convert parameters
$Build = [bool]::Parse($Build)
$BuildR2R = [bool]::Parse($BuildR2R)
$RemoveExe = [bool]::Parse($RemoveExe)
$MakeDelta = [bool]::Parse($MakeDelta)
$UseGitHubDelta = [bool]::Parse($UseGitHubDelta)
$PublishGeneric = [bool]::Parse($PublishGeneric)

# Create temp directory
New-Item $TempDirectory -ItemType Directory -ErrorAction SilentlyContinue | Out-Null

Write-Host "`n=====================================" -ForegroundColor Cyan
Write-Host "     FFT Color Mod - Publisher      " -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# Execute build steps
Cleanup
Get-Tools

if ($MakeDelta -and $UseGitHubDelta) {
    Get-Last-Version
}

if ($Build) {
    Build
}

if ($PublishGeneric) {
    Publish-Generic
}

# Cleanup temp folder
Remove-Item $TempDirectory -Recurse -ErrorAction SilentlyContinue

# Final message
Write-Status "Publishing complete!" "Green"
Write-Host "`nPackages created in: $PublishOutputDir" -ForegroundColor Yellow
Write-Host "You can now upload these packages to GitHub Releases or other platforms." -ForegroundColor Yellow

# Restore directory
if ($UseScriptDirectory) {
    Pop-Location
}