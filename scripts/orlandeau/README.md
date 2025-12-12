# Orlandeau Theme Scripts

This directory contains scripts for creating the Thunder God theme for Orlandeau in Final Fantasy Tactics.

## Orlandeau Palette Mapping

Based on testing with color-coded sprites, Orlandeau's palette indices (0-15) map to:

- **Indices 0-2**: Shadows and outlines (preserved)
- **Indices 3-6**: Main armor, gloves, boots (electric blue)
- **Indices 7-10**: Belt, undergarments, and hair (gold)
- **Indices 11-15**: Cape and face/skin (preserved - brown cape, original skin)

## Scripts

### 1. extract_original_colors.py
Extracts and displays the original Orlandeau palette colors. Use this to understand the base colors.

```bash
python extract_original_colors.py
```

### 2. create_simple_color_test.py
Creates a simple 4-color test sprite to verify palette mapping. Used for development/testing.

```bash
python create_simple_color_test.py
```

### 3. create_thunder_god.py
Creates the Thunder God theme for Orlandeau - the definitive look featuring:
- Electric blue armor befitting the Thunder God
- Gold undergarments and accents representing divine power
- Original brown cape maintaining his nobility

```bash
python create_thunder_god.py
```

## Thunder God Theme

The Thunder God theme transforms Orlandeau into his legendary appearance:

**Color Scheme:**
- **Armor** (indices 3-6): Electric blue gradients from dark to light
- **Undergarments** (indices 7-10): Gold gradients for divine accents
- **Cape** (indices 11-14): Original brown preserved
- **Skin** (index 15): Original skin tone preserved
- **Shadows** (indices 0-2): Original shadows preserved

This creates a cohesive, powerful look that emphasizes Orlandeau's status as the Thunder God while maintaining his classic brown cape.

## Important Notes

### Palette Differences from Generic Sprites
Orlandeau's palette mapping is **different** from generic job sprites:
- Hair is in indices 7-10 (not 10-19 like generics)
- Cape uses indices 11-14
- Skin tone is at index 15

### Theme Testing
1. Run `create_thunder_god.py` to generate the theme
2. Deploy with `BuildLinked.ps1`
3. Launch game through Reloaded-II
4. The Thunder God theme is applied automatically on startup

## File Structure
Theme is created in:
```
ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_orlandeau_thunder_god/
```

Contains:
- `battle_oru_spr.bin` - Main Orlandeau sprite
- `battle_goru_spr.bin` - Guest Orlandeau sprite
- `battle_voru_spr.bin` - Variant Orlandeau sprite