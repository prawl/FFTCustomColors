# FFT Color Mod Scripts

This directory contains Python scripts for creating and managing color themes for Final Fantasy Tactics sprites.

## Scripts Overview

### 1. create_cohesive_theme.py - Recommended Theme Creator
Creates truly cohesive themes with correct cape edges and shadows.

**Usage:**
```bash
# Create a two-tone theme with proper cape shading
python create_cohesive_theme.py --name "cobalt_crusader" --primary "#0047AB" --accent "#FFD700"

# Single color theme with auto-generated darker shades
python create_cohesive_theme.py --name "crimson_knight" --primary "#DC143C"

# Test with single sprite first
python create_cohesive_theme.py --name "test_theme" --primary "#0047AB" --accent "#FFD700" --single "battle_knight_m_spr.bin"
```

**Features:**
- Properly handles main cape color at indices 6-10 in palette 0
- Cape edges at index 7 across palettes 0-3 (25% darker)
- Cape shadows at index 9 across palettes 0-3 (50% darker)
- Buckles/clasps at indices 3-5 with accent color

### 2. create_sprite_theme.py - Alternative Theme Creator
Create themes with primary and accent colors using a different approach.

**Usage:**
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

### 3. apply_story_characters.py - Story Character Management
Apply themes to story character sprites or revert them to original colors.

**Usage:**
```bash
# Copy story characters to all themes
python apply_story_characters.py

# Revert working characters to original colors
python apply_story_characters.py revert

# Show help
python apply_story_characters.py help
```

**Working Story Characters:**
- Orlandeau (`battle_oru_spr.bin`)
- Malak (`battle_mara_spr.bin`)
- Reis human (`battle_reze_spr.bin`)
- Reis dragon (`battle_reze_d_spr.bin`)
- Agrias (`battle_aguri_spr.bin`, `battle_kanba_spr.bin`)
- Beowulf (`battle_beio_spr.bin`)

## Critical Technical Information

### FFT Sprite Palette Structure

FFT sprites use a complex palette system discovered through extensive research:

**Sprite Format:**
- **256 colors total** (512 bytes) organized as 16 palettes of 16 colors each
- Palettes 0-7: Unit sprites
- Palettes 8-15: Portraits
- Each palette: 2 bytes per color (XBBBBBGGGGGRRRRR format)
- Color format: 1 bit for transparency + 5 bits each for BGR channels

### CONFIRMED WORKING PALETTE INDICES

After extensive testing (2024-12-09), these indices change visible armor WITHOUT affecting hair:

**ACCENT COLORS (clasp, buckle, trim):**
- **Indices 3-5**: Belt buckles, clasps, and trim

**PRIMARY COLORS (cape, clothing, armor):**
- **Indices 6-9**: Main clothing and cape colors
- **Indices 20-31**: Additional armor elements
- **Indices 35-47**: Extended armor pieces (skip 44)
- **Indices 51-62**: More armor palette

**NEVER MODIFY - Hair Indices:**
- **Range 10-19**: ALL indices in this range affect hair
- **Index 44**: Skip this one (between 43 and 45)

### Cape Color Mapping Breakthrough (2024-12-09)

After color-coded sprite testing, we definitively mapped the cape structure:

**Cape Components & Their Indices:**
- **Main Cape Body**: Indices 6-10 in palette 0 (primary cape color)
- **Cape Edge/Trim**: Index 7 in palettes 0-3 (the literal border/edge of the cape)
- **Cape Accent/Shadows**: Index 9 in palettes 0-3 (darker areas for depth/shadows)
- **Additional Details**: Indices 12-15 in palettes 0-3 (extra armor/cape details)
- **Buckles/Clasps**: Indices 3-5 in palette 0 (metal accents, trim pieces)

**Why Previous Attempts Failed:**
- Working themes (corpse_brigade, lucavi) actually **swap entire palettes** rather than modifying individual colors
- Cape edges/accents appear because they use colors from MULTIPLE palettes simultaneously
- **Solution**: Must modify index 7 and 9 across palettes 0-3 for complete cape coverage

**Proper Color Relationships for Cohesive Themes:**
- **Cape Edge** (index 7): Should be ~25% darker than main cape color
- **Cape Accent/Shadow** (index 9): Should be ~50% darker than main cape color
- **Buckles/Clasps** (indices 3-5): Use accent color for two-tone themes

## Creating Custom Themes

### Method 1: Using create_cohesive_theme.py (Recommended)

```python
# Example: Create a royal purple theme with gold accents
python create_cohesive_theme.py --name "royal_guard" --primary "#6B46C1" --accent "#FFD700"

# The script will:
# 1. Apply purple to main armor (indices 6-10, 20-31, 35-47, 51-62)
# 2. Create darker purple for cape edges (25% darker at index 7)
# 3. Create shadow purple for cape depth (50% darker at index 9)
# 4. Apply gold to buckles and clasps (indices 3-5)
```

### Method 2: Using create_sprite_theme.py

```python
# Example: Create two-tone themes with specific index control
accent_indices = "3,4,5"  # Gold trim, silver buckles, etc.
primary_indices = "6,7,8,9,20,21,22,23,24,25,26,27,28,29,30,31,35,36,37,38,39,40,41,42,43,45,46,47,51,52,53,54,55,56,57,58,59,60,61,62"

python create_sprite_theme.py --source original --name "knight_commander" \
  --custom-indices $primary_indices --custom-color "#1E3A5F" \
  --custom-indices $accent_indices --custom-color "#C0C0C0"
```

## Important Technical Limitations

### DirectX 12 Sprite Refresh Issue
FFT uses DirectX 12 for rendering, which fundamentally affects how sprites update:
- Textures are uploaded to GPU memory via command lists
- Once in VRAM, textures remain cached until explicitly released
- CPU-side memory modifications don't affect GPU-cached textures
- **Result**: File swapping works for new sprites but not already-loaded ones

**User Workarounds:**
1. Change scenes (enter/exit battle, change maps)
2. Restart the game
3. Wait for unloaded sprites to appear with new colors

### Ramza Sprite DLC Protection
- Ramza's sprite appears to be DLC-locked
- File modifications trigger DLC validation failure
- Other story characters work fine with file swapping
- This is a known limitation without a current solution

## Theme Creation Best Practices

1. **Test with Single Sprite First**: Use the `--single` flag to test on one sprite before batch processing
2. **Preserve Hair Colors**: Never modify indices 10-19 to keep original hair colors
3. **Use Color Relationships**: Make cape edges 25% darker and shadows 50% darker than base color
4. **Two-Tone Themes**: Use accent colors sparingly on buckles/clasps for best visual impact
5. **Backup Original Sprites**: Always keep backups before batch modifications

## File Structure

```
ColorMod/FFTIVC/data/enhanced/fftpack/unit/
├── sprites_original/          # Base sprites (never modify)
├── sprites_corpse_brigade/    # Blue armor theme
├── sprites_lucavi/           # Dark demon theme
├── sprites_northern_sky/     # Holy knight theme
├── sprites_[custom]/         # Your custom themes
└── *.bin                     # Active sprites (swapped by F1/F2)
```

## Troubleshooting

### Common Issues

**Theme not appearing in game:**
- Ensure sprite files are in correct directory structure
- Check that all 38 job sprites are present in theme folder
- Verify file names match exactly (case-sensitive)

**Colors look wrong:**
- Different armor pieces use different palettes within the same sprite
- Some sprites may need manual adjustment after batch processing
- Use single sprite testing to identify issues

**Story characters not changing:**
- Run `apply_story_characters.py` to copy story sprites to themes
- Some characters (like Mustadio) have known issues
- Ramza is DLC-protected and won't work

## Development Notes

### Discovery Method
The working palette indices were discovered by:
1. Analyzing existing working themes (corpse_brigade, lucavi, etc.)
2. Identifying which indices they modify
3. Testing with color-coded sprites to map exact armor components
4. Confirming that indices 10-19 affect hair and should be avoided

### Memory Patching Results (December 2024)
Direct memory patching was attempted but causes game crashes due to:
- Memory access violations when scanning protected regions
- CLR errors when interacting with managed memory
- Process handle issues with OpenProcess permissions
- DirectX 12 architecture preventing runtime texture updates

File swapping remains the only reliable method for sprite color modification.

## Contributing

When creating new themes:
1. Use the provided scripts rather than manual editing
2. Test thoroughly with multiple job classes
3. Document any new discoveries about palette indices
4. Share successful theme configurations

## License

For personal use only. Final Fantasy Tactics © Square Enix.