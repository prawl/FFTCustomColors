# FFT Custom Colors Mod

A color modification mod for Final Fantasy Tactics (Steam version) that enables custom character color palettes through the Reloaded-II mod loader.

## Version History

### v0.3.0 (December 2025) - FFTGenericJobs Integration & New Strategy
- **BREAKTHROUGH**: Discovered working memory manipulation approach from FFTGenericJobs mod
- **Function hooking strategy** replaces file interception approach
- **Build automation** with PowerShell scripts (BuildLinked.ps1, Publish.ps1)
- **Signature scanning** for dynamic function discovery
- Ready for implementation with proven hooking approach

### v0.2.0 (December 2025) - Research Phase Complete
- **27 passing tests** with comprehensive TDD implementation
- **Complete Chapter 1-4 support** for all Ramza outfit variations
- **Memory manipulation research** revealed FFT's dynamic palette reloading
- **File interception approach** identified as initial solution

### v0.1.0 (Initial Development)
- Project setup with Reloaded-II framework
- TDD implementation with xUnit and FluentAssertions
- Color detection and replacement logic
- Hotkey system (F1/F2)

## Features

### Working
- Complete mod structure compatible with Reloaded-II
- 27 passing tests with TDD framework
- Chapter detection for all 4 Ramza outfits
- Color replacement logic (BGR format)
- Hotkey system (F1: Original, F2: Red)


### Planned
- [ ] Function hooking implementation using signature scanning
- [ ] Hook sprite/palette loading functions to modify colors at load time
- [ ] Additional color schemes (Blue, Green, Purple)
- [ ] Support for multiple character palettes
- [ ] Configuration UI in Reloaded-II

## Installation

1. Ensure you have Final Fantasy Tactics (Steam version) installed
2. Install Reloaded-II mod loader
3. Place this mod folder in: `[FFT Directory]\Reloaded\Mods\FFT_Color_Mod`
4. Enable the mod in Reloaded-II
5. Launch FFT through Vortex or Reloaded-II

## Development Workflow

### Git Workflow & Releases

#### Initial Setup
```bash
# Initialize git repository (if not done)
git init

# Add GitHub remote
git remote add origin https://github.com/ptyRa/FFT_Color_Mod.git

# Create initial commit
git add .
git commit -m "Initial commit"
git push -u origin main
```

#### Development Process
```bash
# Create feature branch for new work
git checkout -b feature/hook-implementation

# Make changes and commit
git add .
git commit -m "Add sprite loading hook"

# Push branch to GitHub
git push origin feature/hook-implementation

# Create Pull Request on GitHub for review
```

#### Creating a Release
```bash
# Ensure you're on main branch with latest changes
git checkout main
git pull origin main

# Create a version tag (triggers GitHub Actions)
git tag -a v0.4.0 -m "Add function hooking support"

# Push the tag to GitHub (this triggers automatic release)
git push origin v0.4.0

# GitHub Actions will automatically:
# - Build the mod
# - Run tests
# - Generate changelog
# - Create GitHub Release with downloads
```

#### Version Numbering (Semantic Versioning)
- **Major (1.x.x)**: Breaking changes, major features
- **Minor (x.1.x)**: New features, backwards compatible
- **Patch (x.x.1)**: Bug fixes, minor improvements
- **Pre-release**: v1.0.0-beta.1, v1.0.0-alpha.2

#### Automated CI/CD
Every push to GitHub triggers automated builds:
- **Push to main/develop**: Builds and tests, creates artifacts
- **Push tag (v*)**: Creates official release with changelog
- **Pull Request**: Runs tests and provides feedback

## Building from Source

### Requirements
- .NET SDK 8.0 or later
- Visual Studio 2022 or VS Code with C# extension
- Git for version control

### Build Steps

#### Quick Development Build
```powershell
# Quick build and deploy to Reloaded-II
.\BuildLinked.ps1
# This builds with IL trimming and deploys directly to your Reloaded-II mods folder
```

#### Release Build
```powershell
# Build for release distribution
.\Publish.ps1

# Build with Ready-to-Run optimization
.\Publish.ps1 -BuildR2R $true

# Create delta update package (requires previous GitHub release)
.\Publish.ps1 -MakeDelta $true -UseGitHubDelta $true
```

#### Manual Build
```bash
# Clone the repository
git clone https://github.com/ptyRa/FFT_Color_Mod
cd FFT_Color_Mod

# Run tests
dotnet test

# Build release version
dotnet build -c Release

# Output will be in bin/Release/
```

## Project Structure

```
FFT_Color_Mod/
├── Mod.cs                   # Main mod entry point
├── PaletteDetector.cs       # Color detection and replacement logic
├── MemoryScanner.cs         # Memory scanning for palettes
├── ModConfig.json           # Reloaded-II configuration
├── FFTColorMod.csproj       # Main project file
├── FFTColorMod.Tests.csproj # Test project
├── BuildLinked.ps1          # Quick build & deploy script
├── Publish.ps1              # Release packaging script
├── Tests/
│   ├── PaletteDetectorTests.cs  # Unit tests for color detection
│   └── MemoryScannerTests.cs    # Unit tests for memory scanning
├── README.md                # This file - user documentation
├── CLAUDE.md                # Development documentation
└── PLANNING.md              # Technical research & strategy
```

## Technical Details

- **Platform**: Windows (Steam version of FFT)
- **Framework**: .NET 8.0
- **Mod Loader**: Reloaded-II
- **Color Format**: BGR (Blue-Green-Red)
- **Testing**: xUnit with FluentAssertions

## Current Status

### Breakthrough Discovery ✅
- Analyzed successful **FFTGenericJobs** mod - found working memory manipulation approach
- **Function hooking with signature scanning** solves our palette reloading problem
- FFTGenericJobs successfully modifies FFT by hooking functions at startup
- 27 passing tests with complete TDD framework ready for implementation

### Next Steps
- Implement function hooking using Reloaded.Memory.SigScan
- Hook sprite/palette loading functions to modify colors at load time
- Apply existing color detection/replacement logic within hooked functions
- See CLAUDE.md for development details, PLANNING.md for technical research

## Contributing

This is currently a personal project, but suggestions and bug reports are welcome!

## Credits

- **Developer**: ptyRa
- **Tools Used**:
  - Reloaded-II by Sewer56
  - FFTPatcher Suite (reference only)
- **Community**: FFHacktics for FFT modding resources

## License

This mod is for personal use. Final Fantasy Tactics is property of Square Enix.

## Support

For issues or questions:
- Check the [FFHacktics Forum](https://ffhacktics.com/)
- Review CLAUDE.md for technical details


## FAQ

**Q: Does this work with other FFT mods?**
A: Currently untested, but should be compatible with most mods that don't alter sprite palettes.

**Q: Can I use this to change enemy colors?**
A: Not yet, but it's on the wishlist for future versions.

**Q: Will this affect my save files?**
A: No, this mod only changes visual appearance in memory, not save data.

**Q: How do I create custom color schemes?**
A: Currently requires manual code editing. UI-based customization is planned for future versions.