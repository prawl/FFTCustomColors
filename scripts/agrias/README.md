# Agrias Theme Scripts

This directory contains scripts for analyzing and creating themes for Agrias Oaks in Final Fantasy Tactics.

## Agrias Dual Sprite System - CRITICAL INFORMATION

### Unique Challenge: Two Sprite Files
Unlike most characters who have a single sprite file, Agrias has **TWO separate sprite files**:
- **battle_aguri_spr.bin** - Main Agrias (storyline appearance)
- **battle_kanba_spr.bin** - Guest Agrias (different appearance/context)

This dual sprite system presents unique challenges and opportunities for theme creation.

### Palette Mapping Strategy
Before creating any themes for Agrias, it's **CRITICAL** to:
1. Extract and compare palettes from both sprite files
2. Determine if they share identical color schemes
3. Test color mapping on both sprites to identify any differences
4. Create themes that work well for both variants

## Scripts

### 1. extract_original_colors.py
Extracts and displays the original palette colors from **both** Agrias sprite files, then compares them to determine compatibility.

```bash
python extract_original_colors.py
```

**What it shows:**
- Original palette for Main Agrias (battle_aguri_spr.bin)
- Original palette for Guest Agrias (battle_kanba_spr.bin)
- Side-by-side comparison showing which indices match
- Compatibility assessment for single theme vs separate themes

### 2. create_simple_color_test.py
Creates a simple 4-color test for **both** Agrias sprites to verify palette mapping:
- BLACK for indices 0-2 (shadow/outline)
- RED for indices 3-6 (main armor)
- GREEN for indices 7-10 (secondary elements)
- BLUE for indices 11-15 (cape/details)

```bash
python create_simple_color_test.py
```

**Critical Testing:**
- Deploy and test with **BOTH** Agrias variants in-game
- Note any differences in how colors appear on each sprite
- Document which parts show each test color
- Pay special attention to face/skin areas for unexpected mapping

## Analysis Workflow

### Phase 1: Palette Discovery
1. Run `extract_original_colors.py` to compare base palettes
2. Determine if sprites share identical color schemes
3. Document any palette differences between variants

### Phase 2: Color Mapping
1. Run `create_simple_color_test.py` to generate test sprites
2. Deploy with `BuildLinked.ps1`
3. Test in-game with both Agrias variants
4. Document color mapping for each sprite:
   - Which indices control armor vs undergarments
   - Face/skin tone mapping
   - Cape and accessory colors
   - Any unique elements per sprite variant

### Phase 3: Theme Development
Based on mapping results:
- **If palettes are identical:** Create single theme for both sprites
- **If palettes differ:** Create optimized theme that works for both, or separate themes

## Expected Palette Regions

**Initial Hypothesis (to be tested):**
- **Indices 0-2**: Shadow/outline colors (likely consistent across variants)
- **Indices 3-6**: Primary armor colors (main design focus)
- **Indices 7-10**: Secondary elements/undergarments (may vary between sprites)
- **Indices 11-15**: Cape, accessories, skin tones (potential differences)

## Testing Process

### In-Game Validation
1. **Deploy Test Theme:**
   ```bash
   python create_simple_color_test.py
   BuildLinked.ps1
   ```

2. **Test Both Variants:**
   - Use F2 to cycle to Agrias test theme
   - Test with main story Agrias
   - Test with guest Agrias (if available in save)
   - Document color appearances for each

3. **Document Findings:**
   - Create mapping notes for each sprite variant
   - Note any shared vs different color usage
   - Identify optimal color schemes that work for both

## File Structure
Test themes are created in:
```
ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_agrias_test/
```

Contains:
- `battle_aguri_spr.bin` - Main Agrias test sprite
- `battle_kanba_spr.bin` - Guest Agrias test sprite
- `COLOR_REFERENCE.txt` - Testing reference guide

## Important Notes

### Dual Sprite Considerations
- **Theme Compatibility**: Any theme must consider how it appears on BOTH sprite variants
- **Testing Scope**: Always test themes with both sprites when possible
- **Color Selection**: Choose colors that work well regardless of sprite differences
- **Documentation**: Keep detailed records of how each sprite responds to color changes

### Common Issues to Watch For
- **Face Color Bleeding**: Some palette indices may unexpectedly affect facial features
- **Asymmetric Mapping**: One sprite might use indices differently than the other
- **Outfit Variations**: Guest vs main Agrias may have different costume elements
- **Skin Tone Consistency**: Ensure skin tones remain natural across both sprites

## Next Steps
After completing the palette mapping phase:
1. Create dedicated theme scripts (e.g., `create_holy_knight.py`)
2. Develop themes optimized for Agrias's dual sprite system
3. Test themes thoroughly with both sprite variants
4. Document any sprite-specific considerations for future theme development

---

**CRITICAL REMINDER**: Agrias's dual sprite system requires more thorough testing than single-sprite characters. Always validate themes with both sprite variants before considering them complete.