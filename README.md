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
- FF16Tools GUI app for extracting sprites from PAC files (required for sprite modification)

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

### Testing Sprite Color Changes

#### Extract Sprites from Game
```bash
# Use FF16Tools GUI to extract sprites from PAC files
# PAC files location:
# C:/Program Files (x86)/Steam/steamapps/common/FINAL FANTASY TACTICS - The Ivalice Chronicles/data/enhanced/0002.pac
```

#### Process Single Sprite for Testing
```bash
# Extract sprites first (using FF16Tools GUI)
# Copy sprites to input_sprites folder

# Process a single sprite to test colors
dotnet run --project FFTColorMod.csproj -- process input_sprites/battle_knight_m_spr.bin test_output

# This creates color variants in:
# test_output/sprites_red/
# test_output/sprites_blue/
# test_output/sprites_green/
# test_output/sprites_purple/
# test_output/sprites_original/
```

#### Deploy and Test In-Game
```bash
# Deploy mod to Reloaded-II
powershell -File BuildLinked.ps1

# Launch FFT through Reloaded-II
# Enable FFT_Color_Mod
# Press F2 in-game to switch to red color
# Knights should now appear with red armor
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
├── input_sprites/           # Extracted FFT sprites (included for testing)
├── Tests/
│   ├── PaletteDetectorTests.cs  # Unit tests for color detection
│   └── MemoryScannerTests.cs    # Unit tests for memory scanning
├── README.md                # This file - user documentation
├── CLAUDE.md                # Development documentation
└── PLANNING.md              # Technical research & strategy
```

**Note**: The `input_sprites/` directory contains 179 extracted FFT sprite files (about 7MB total) and is included in version control to enable immediate testing and development without requiring FF16Tools extraction setup.

## Technical Details

- **Platform**: Windows (Steam version of FFT)
- **Framework**: .NET 8.0
- **Mod Loader**: Reloaded-II
- **Color Format**: BGR (Blue-Green-Red)
- **Testing**: xUnit with FluentAssertions
- **Sprite Format**: First 288 bytes contain palette data

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

## Sprite Mappings

### File Locations
- **0002.pac**: Contains all job sprites and generic units
- **0003.pac**: Contains additional sprites and portraits

### Generic Unit Sprites (NPCs/Enemies)
| File | Description | Size |
|------|-------------|------|
| battle_10m_spr.bin | Generic Male Unit Type 1 | 37,377 bytes |
| battle_10w_spr.bin | Generic Female Unit Type 1 | 37,377 bytes |
| battle_20m_spr.bin | Generic Male Unit Type 2 | 37,377 bytes |
| battle_20w_spr.bin | Generic Female Unit Type 2 | 37,377 bytes |
| battle_40m_spr.bin | Generic Male Unit Type 3 | 37,377 bytes |
| battle_40w_spr.bin | Generic Female Unit Type 3 | 37,377 bytes |
| battle_60m_spr.bin | Generic Male Unit Type 4 | 37,377 bytes |
| battle_60w_spr.bin | Generic Female Unit Type 4 | 37,377 bytes |

### Male Job Classes
| Job Class | Sprite File | Portrait Base | Japanese Name |
|-----------|-------------|---------------|---------------|
| Squire | battle_mina_m_spr.bin | wldface_096 | mina (見習い) |
| Chemist | battle_item_m_spr.bin | wldface_098 | item (アイテム士) |
| Knight | battle_knight_m_spr.bin | wldface_100 | knight (ナイト) |
| Archer | battle_yumi_m_spr.bin | wldface_102 | yumi (弓使い) |
| Monk | battle_monk_m_spr.bin | wldface_104 | monk (モンク) |
| White Mage | battle_siro_m_spr.bin | wldface_106 | siro (白魔道士) |
| Black Mage | battle_kuro_m_spr.bin | wldface_108 | kuro (黒魔道士) |
| Time Mage | battle_toki_m_spr.bin | wldface_110 | toki (時魔道士) |
| Summoner | battle_syou_m_spr.bin | wldface_112 | syou (召喚士) |
| Thief | battle_thief_m_spr.bin | wldface_114 | thief (シーフ) |
| Mediator | battle_waju_m_spr.bin | wldface_116 | waju (話術士) |
| Oracle | battle_onmyo_m_spr.bin | wldface_118 | onmyo (陰陽士) |
| Geomancer | battle_fusui_m_spr.bin | wldface_120 | fusui (風水士) |
| Dragoon | battle_ryu_m_spr.bin | wldface_122 | ryu (竜騎士) |
| Samurai | battle_samu_m_spr.bin | wldface_124 | samu (侍) |
| Ninja | battle_ninja_m_spr.bin | wldface_126 | ninja (忍者) |
| Calculator | battle_san_m_spr.bin | wldface_128 | san (算術士) |
| Bard | battle_gin_m_spr.bin | wldface_130 | gin (吟遊詩人) |
| Mime | battle_mono_m_spr.bin | wldface_132 | mono (ものまね士) |

### Female Job Classes
| Job Class | Sprite File | Portrait Base | Japanese Name |
|-----------|-------------|---------------|---------------|
| Squire | battle_mina_w_spr.bin | wldface_097 | mina (見習い) |
| Chemist | battle_item_w_spr.bin | wldface_099 | item (アイテム士) |
| Knight | battle_knight_w_spr.bin | wldface_101 | knight (ナイト) |
| Archer | battle_yumi_w_spr.bin | wldface_103 | yumi (弓使い) |
| Monk | battle_monk_w_spr.bin | wldface_105 | monk (モンク) |
| White Mage | battle_siro_w_spr.bin | wldface_107 | siro (白魔道士) |
| Black Mage | battle_kuro_w_spr.bin | wldface_109 | kuro (黒魔道士) |
| Time Mage | battle_toki_w_spr.bin | wldface_111 | toki (時魔道士) |
| Summoner | battle_syou_w_spr.bin | wldface_113 | syou (召喚士) |
| Thief | battle_thief_w_spr.bin | wldface_115 | thief (シーフ) |
| Mediator | battle_waju_w_spr.bin | wldface_117 | waju (話術士) |
| Oracle | battle_onmyo_w_spr.bin | wldface_119 | onmyo (陰陽士) |
| Geomancer | battle_fusui_w_spr.bin | wldface_121 | fusui (風水士) |
| Dragoon | battle_ryu_w_spr.bin | wldface_123 | ryu (竜騎士) |
| Samurai | battle_samu_w_spr.bin | wldface_125 | samu (侍) |
| Ninja | battle_ninja_w_spr.bin | wldface_127 | ninja (忍者) |
| Calculator | battle_san_w_spr.bin | wldface_129 | san (算術士) |
| Dancer | battle_odori_w_spr.bin | wldface_131 | odori (踊り子) |
| Mime | battle_mono_w_spr.bin | wldface_133 | mono (ものまね士) |

### Unique/Special Characters
| Character | Sprite Files | Portrait Base |
|-----------|-------------|---------------|
| Agrias | battle_aguri_spr.bin, battle_kanba_spr.bin | wldface_052 |

### Color Variants (from better_palettes mod)
- **Default**: Standard colors
- **Azure**: Blue-tinted variant
- **Smoke**: Gray/dark variant
- **Lucavi**: Dark/evil variant
- **Northern_Sky**: Light/holy variant
- **Southern_Sky**: Warm-toned variant
- **Corpse_Brigade**: Rebel/bandit colors
- **Ginger**: Orange/red hair variant
- **Maid**: Special Chemist variant
- **Festive**: Colorful Time Mage variant
- **Gold_with_Blue_Cape**: Special Dragoon variant
- **Red_Bard**: Special Bard variant
- **Cobalt**: Blue Dragoon variant
- **Forest**: Green Ninja variant
- **Concept**: Special Agrias variant

### Sprite Technical Details
- Job sprites: ~43-47 KB each
- Generic units: 37,377 bytes exactly
- Portraits: ~30-75 KB each
- Color palette data: First 288 bytes of sprite files
- Each sprite supports multiple palettes for team colors

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