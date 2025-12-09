# FFT Color Mod

Custom color palettes for Final Fantasy Tactics (Steam) using professionally edited sprites from better_palettes.

## Features

- **F1 Hotkey**: Cycle through 20 unique color schemes in-game
- **Persistent**: Color choices persist between game sessions (file-based swapping)
- **38 Sprites Modified**: All major job classes included
- **Custom Themes**: 14 new color schemes beyond the original 6

## Installation

1. Install [Reloaded-II mod loader](https://github.com/Reloaded-II/Reloaded-II)
2. Download FFColorMod from releases
3. Extract to: `Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\Reloaded\Mods\`
4. Launch Reloaded-II and enable "FFT Color Mod"
5. Start the game through Reloaded-II

## Usage

Press **F1** in-game to cycle through color schemes. Changes apply immediately.

## Building from Source

### Requirements
- .NET SDK 8.0+
- Windows (Git Bash recommended for scripts)

### Quick Deploy
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

#### 1. create_sprite_theme.py - Full Palette Transformations
Create cohesive color themes by transforming entire palettes:
```bash
# Hue shift to create new color schemes
python create_sprite_theme.py --source corpse_brigade --name ocean_blue --hue-shift 180

# Adjust brightness/saturation
python create_sprite_theme.py --source original --name dark_knight --brightness -30 --saturation +20

# Transform specific clothing items (experimental)
python create_sprite_theme.py --source original --name black_leather \
  --transform-items "boots,gloves" --item-color "#1A1A1A"
```

Features:
- Automatic skin tone preservation
- HSV color adjustments
- Hue shifting for complete recolors
- Clothing item detection (boots, gloves, armor, etc.)

#### 2. create_sprite_theme_indices.py - Precise Palette Control
Target specific palette indices for surgical color changes:
```bash
# Change specific palette indices
python create_sprite_theme_indices.py --source original --name custom \
  --custom-indices "13,39,61" --custom-color "#000000"

# Predefined item groups
python create_sprite_theme_indices.py --source original --name black_boots \
  --boots-color "#1A1A1A" --gloves-color "#1A1A1A"
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

#### Known Index Groups (Knight Sprite)
- **Boots**: Indices 13, 39, 61, 40 (dark browns)
- **Gloves/Hair Mix**: Indices 11, 59, 27 (medium browns - shared between items)
- **Hair/Leather Mix**: Indices 14, 28, 44, 60, 12 (lighter browns/tans)

**Important**: Many palette indices are shared across multiple body parts. Changing boots might also affect belts, and changing gloves often affects hair. This is a limitation of the palette-based sprite system.

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