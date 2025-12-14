# FFT Color Mod

Custom color palettes for Final Fantasy Tactics (Steam) using professionally edited sprites from better_palettes.

## Features

- **20 Unique Color Schemes**: Original plus 19 custom themes
- **Per-Job Configuration**: Set different colors for each job class via Reloaded-II config
- **38 Sprites Modified**: All major job classes included
- **Persistent Settings**: Configuration persists between game sessions

## Installation

1. Install [Reloaded-II mod loader](https://github.com/Reloaded-II/Reloaded-II)
2. Download FFColorMod from releases
3. Extract to: `Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\Reloaded\Mods\`
4. Launch Reloaded-II and enable "FFT Color Mod"
5. Start the game through Reloaded-II

## Usage

### Configuration Menu (Persistent Settings)
The mod now includes full integration with Reloaded-II's configuration system, allowing you to set persistent color schemes for each job class:

1. **Open Reloaded-II** and select "FFT Color Mod"
2. **Click Configure** to open the configuration window
3. **Select color schemes** for each job class using the dropdown menus:
   - Each job (Squire, Knight, Monk, etc.) can have its own color scheme
   - Both male and female variants can be configured separately
   - Choose from 21 available color schemes including Original, Corpse Brigade, Lucavi, and custom themes
4. **Save your configuration** - changes persist between game sessions
5. **Launch the game** - your configured colors will be applied automatically

**Configuration Features**:
- **Per-Job Customization**: Set different colors for each job class
- **Gender Variants**: Configure male and female sprites separately
- **Persistent Settings**: Your choices are saved to `Reloaded\User\Mods\ptyra.fft.colormod\Config.json`
- **Real-time Updates**: Changes made in the configuration menu apply immediately when saved
- **F2 Hotkey Behavior**: F2 still cycles colors in-game, but these changes are session-only and don't overwrite your saved configuration

## Development

### Requirements
- .NET SDK 8.0+
- Windows (Git Bash recommended for scripts)
- Visual Studio 2022 or VS Code
- Reloaded-II mod loader installed

### Build Scripts

The mod includes two build scripts for different deployment scenarios:

#### BuildLinked.ps1 (Development)
- **Purpose**: Quick iteration and testing
- **Themes Deployed**: 5 generic themes + all Orlandeau themes
- **Generic Themes Limited**: original, corpse_brigade, lucavi, northern_sky, southern_sky
- **Why Limited**: Prevents F1/F2 cycling crashes during development
- **Usage**: `.\BuildLinked.ps1`

#### BuildLinked.Production.ps1 (Production)
- **Purpose**: Full release builds with all themes available
- **Themes Deployed**: ALL available themes (all generic + all story characters)
- **Theme Loading**: Only themes specified in Config.json are loaded into memory
- **Benefit**: All themes on disk but only configured ones use memory
- **Confirmation**: Requires "yes" confirmation to prevent accidental production builds
- **Warning**: Shows warnings if too many themes might cause stability issues
- **Usage**: `.\BuildLinked.Production.ps1`

**Important Distinction**:
- **Deployment** (BuildLinked scripts): Controls which theme files are copied to the mod folder
- **Loading** (Config.json): Controls which deployed themes are actually loaded into memory
- **Production Advantage**: All themes deployed but only configured themes consume resources

**Note**: The DevMode.ps1 and ProductionMode.ps1 scripts control CONFIG settings (which features are enabled), while BuildLinked scripts control DEPLOYMENT (which themes are included).

### Dynamic Sprite Loading System
The mod uses a dynamic sprite loading system to prevent crashes and optimize memory usage:

**How It Works:**
1. All 20 color themes are stored in the `ColorSchemes/` directory
2. At game startup, only the sprites configured in Reloaded-II are copied to `data/`
3. The game only loads what's actually being used (prevents crashes)
4. No manual file management needed - everything is automatic!

**Dev Mode Auto-Detection:**
- The system automatically detects dev mode when the 5 core themes are in `data/`
- In dev mode, DynamicSpriteLoader is disabled to preserve F1/F2 functionality
- Configured themes outside the dev set will be skipped with a console message
- Test themes (`sprites_test_*`) are always preserved

### Development Mode Setup
For testing with F1/F2 hotkeys, use development mode:

```powershell
# Set up dev mode (5 themes for F1/F2 testing)
./SetupDev.ps1

# When ready for production (dynamic loading only)
./SetupProduction.ps1
```

**Dev Mode:**
- Keeps 5 core themes in `data/` for F1/F2 testing (original, corpse_brigade, lucavi, northern_sky, southern_sky)
- Stores all other themes in `ColorSchemes/`
- Supports `sprites_test_*` themes for development
- DynamicSpriteLoader auto-detects and preserves this setup
- F1/F2 hotkeys work perfectly without interference
- Prevents crashes during hotkey testing

**Production Mode:**
- All themes stored in `ColorSchemes/`
- `data/` directory kept empty
- DynamicSpriteLoader copies only configured themes at startup
- No crashes, minimal memory usage
- All 20 themes available through config

### Build & Deploy
```powershell
# Builds and deploys directly to Reloaded-II mods folder
./BuildLinked.ps1
```

### Run Tests
```bash
./RunTests.sh
```

### Project Structure
```
FFColorMod/
├── ColorMod/
│   ├── FFTIVC/                 # Sprite files and color variants
│   │   └── data/enhanced/
│   │       └── fftpack/unit/   # Original sprites + variant folders
│   ├── Utilities/              # Core functionality
│   │   ├── HotkeyHandler.cs   # F1 key detection
│   │   └── SpriteFileManager.cs # Sprite file swapping
│   ├── Mod.cs                  # Main mod entry point
│   └── ModConfig.json          # Reloaded-II configuration
├── Tests/                      # Unit tests (77 tests)
├── BuildLinked.ps1            # Build & deploy script
└── RunTests.sh                # Test runner script
```

## Color Schemes (21 Total - Auto-Detected)

The mod automatically detects all color schemes in the `sprites_*` directories.

### Original Themes
| Scheme | Description | Theme |
|--------|-------------|-------|
| Original | Default FFT colors | Classic brown/beige |
| Corpse Brigade | Blue armor variant | Cool blue tones |
| Lucavi | Dark demon theme | Deep purple |
| Northern Sky | Holy knight colors | Light silver/gold |
| Smoke | Stealthy gray | Dark gray/black |
| Southern Sky | Warm desert theme | Orange/red tones |

### Custom Created Themes
| Scheme | Description | Theme |
|--------|-------------|-------|
| Aaron | Aquatic warrior | Teal/cyan oceanic tones |
| Crimson Red | Deep red armor | Red/crimson tones |
| Royal Purple | Regal purple armor | Purple with gold accents |
| Phoenix Flame | Fire-themed armor | Orange/yellow flames |
| Frost Knight | Ice warrior | Icy blue with silver |
| Silver Knight | Metallic armor | Pure silver/white |
| Shadow Assassin | Stealth armor | Pure black |
| Emerald Dragon | Nature warrior | Deep green with gold |
| Rose Gold | Elegant armor | Pink/copper metallic |
| Ocean Depths | Sea warrior | Deep ocean blue |
| Golden Templar | Holy warrior | Dark armor, golden cape |
| Blood Moon | Dark warrior | Red and black combo |
| Celestial | Cosmic knight | White with blue accents |
| Volcanic | Lava warrior | Black with orange |
| Amethyst | Mystic armor | Purple/violet gradient |

## Technical Implementation

- **Method**: Physical file swapping (not memory patching)
- **Persistence**: Swapped files remain until changed (no save system needed)
- **Performance**: Instant switching with no game restart required
- **Compatibility**: Works with FFTIVC utility modloader

## Creating Custom Themes

### Theme Creation Scripts

#### create_cohesive_theme.py - NEW! Proper Cape Edge & Accent Handling (Recommended)
Creates truly cohesive themes with correct cape edges and shadows:
```bash
# Create a two-tone theme with proper cape shading
python create_cohesive_theme.py --name "cobalt_crusader" --primary "#0047AB" --accent "#FFD700"

# Single color theme with auto-generated darker shades
python create_cohesive_theme.py --name "crimson_knight" --primary "#DC143C"

# Test with single sprite first
python create_cohesive_theme.py --name "test_theme" --primary "#0047AB" --accent "#FFD700" --single "battle_knight_m_spr.bin"
```

This script properly handles:
- Main cape color at indices 6-10 in palette 0
- Cape edges at index 7 across palettes 0-3 (25% darker)
- Cape shadows at index 9 across palettes 0-3 (50% darker)
- Buckles/clasps at indices 3-5 with accent color

#### create_sprite_theme.py - Legacy Palette Control
Create themes with primary and accent colors (older method):
```bash
# Two-tone theme with primary and accent colors
python create_sprite_theme.py --source original --name royal_blue \
  --primary-color "#0047AB" --accent-color "#FFD700"

# Single color theme (no accent)
python create_sprite_theme.py --source original --name crimson \
  --primary-color "#DC143C"

# Custom indices for specific modifications
python create_sprite_theme.py --source original --name custom \
  --custom-indices "3,4,5" --custom-color "#FFD700"
```

#### 3. analyze_palette.py - Palette Analysis Tool
Analyze sprite palettes to understand color usage:
```bash
# Analyze a sprite's color palette
python analyze_palette.py ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_original/battle_knight_m_spr.bin

# Compare two sprites
python analyze_palette.py sprite1.bin --compare sprite2.bin
```

### Palette Index Mapping

FFT sprites use palette-based coloring where different body parts reference specific color indices:

#### CONFIRMED WORKING INDICES - Tested & Verified 2024-12-09
**These indices change visible armor WITHOUT affecting hair:**

**ACCENT COLORS (clasp, buckle, trim):**
- **Indices 3-5**: These control accent elements like belt buckles, clasps, and trim

**PRIMARY COLORS (cape, clothing, armor):**
- **Indices 6-9**: Main clothing and cape colors
- **Indices 20-31**: Additional armor elements
- **Indices 35-47**: Extended armor pieces (skip 44)
- **Indices 51-62**: More armor palette

**Creating Two-Tone Themes:**
```python
# Accent indices (gold trim, silver buckles, etc.)
accent_indices = "3,4,5"

# Primary armor indices (main color of cape, clothes, armor)
primary_indices = "6,7,8,9,20,21,22,23,24,25,26,27,28,29,30,31,35,36,37,38,39,40,41,42,43,45,46,47,51,52,53,54,55,56,57,58,59,60,61,62"
```

#### Hair Indices - NEVER MODIFY
- **Range 10-19**: ALL indices in this range affect hair (10, 11, 12, 13, 14, 15, 16, 17, 18, 19)
- **Specifically index 13**: Commonly mistaken for boots, but actually affects hair
- **Index 44**: Skip this one (between 43 and 45)

#### Discovery Method
These indices were discovered by analyzing existing working themes (corpse_brigade, lucavi, etc.) and identifying which indices they modify. All working themes change these same indices to create their color variations.

**Important**: This is why the original themes also change hair - they modify indices 10-19. Our new themes skip this range entirely to preserve hair color.

### Creating Your Own Theme

1. **Extract base sprites** (already included in sprites_original)
2. **Choose transformation method**:
   - Use `create_sprite_theme.py` for complete color themes
   - Use `create_sprite_theme_indices.py` for specific item colors
3. **Test your theme** using BuildLinked.ps1
4. **Iterate** based on in-game appearance

### CRITICAL: How FFT Color System Actually Works

**Sprite Format**:
- 256 colors total (512 bytes) organized as 16 palettes of 16 colors each
- Palettes 0-7: Unit sprites
- Palettes 8-15: Portraits
- Each palette: 2 bytes per color (XBBBBBGGGGGRRRRR format)

**🚨 CRITICAL DISCOVERY (2024-12-09): Cape Color Mapping Breakthrough**

After extensive testing with color-coded sprites, we've definitively mapped the cape structure:

**Cape Components & Their Indices:**
- **Main Cape Body**: Indices 6-10 in palette 0 (primary cape color)
- **Cape Edge/Trim**: Index 7 in palettes 0-3 (the literal border/edge of the cape)
- **Cape Accent/Shadows**: Index 9 in palettes 0-3 (darker areas for depth/shadows)
- **Additional Details**: Indices 12-15 in palettes 0-3 (extra armor/cape details)
- **Buckles/Clasps**: Indices 3-5 in palette 0 (metal accents, trim pieces)

**Why Previous Attempts Failed:**
- Working themes (corpse_brigade, lucavi, etc.) actually **swap entire palettes** rather than modifying individual colors
- Example: corpse_brigade swaps palette 0 ↔ palette 3, lucavi swaps palette 0 ↔ palette 4
- Cape edges/accents appear because they use colors from MULTIPLE palettes simultaneously
- **Solution**: Must modify index 7 and 9 across palettes 0-3 for complete cape coverage

**Proper Color Relationships for Cohesive Themes:**
- **Cape Edge** (index 7): Should be ~25% darker than main cape color
- **Cape Accent/Shadow** (index 9): Should be ~50% darker than main cape color
- **Buckles/Clasps** (indices 3-5): Use accent color for two-tone themes

**How Color Mods Work**:
1. The mod physically swaps entire sprite files when you press F1
2. Each color scheme (corpse_brigade, lucavi, etc.) has its own set of 38 sprite files
3. These sprites have modified palette data baked into them
4. The game loads these modified sprites directly - no runtime palette swapping

**Creating New Color Schemes**:
- Must modify the actual color values in the sprite's palette data (first 512 bytes)
- Convert colors across ALL sprite palettes (0-7) for complete coverage
- Different armor pieces may use different palettes within the same sprite
- Test with battle_knight_m_spr.bin first before batch processing

**Known Issue - Partial Color Conversion**:
- Current crimson_red conversion only affects certain armor pieces (pants/lower armor)
- Cape and upper armor remain original colors
- This occurs because different armor pieces reference different colors within the palettes
- Solution: Need more aggressive color conversion that catches all blue/green/gray shades

## Troubleshooting

- **Mod not loading**: Ensure Reloaded-II is properly installed and FFT is launched through it
- **F1 not working**: Check if another mod is using F1 hotkey
- **Missing sprites**: Run BuildLinked.ps1 to verify deployment

## Development Status

- ✅ 38 job sprites with 20 color variants each
- ✅ Male and female variants for all classes
- ✅ Custom color creation system developed
- ⬜ Enemy unit color variants (planned)
- ⬜ Configuration UI (planned)

## Credits

- **Sprites**: better_palettes mod team (professional color editing)
- **Framework**: Reloaded-II by Sewer56
- **Testing**: XUnit with FluentAssertions

## License

For personal use only. Final Fantasy Tactics © Square Enix.