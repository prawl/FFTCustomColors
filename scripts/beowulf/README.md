# Beowulf Theme Scripts

This directory contains scripts for creating the Temple Knight theme for Beowulf in Final Fantasy Tactics.

## Beowulf Palette Mapping - CRITICAL DISCOVERY

### Color Test Results
Using the simple color test (create_simple_color_test.py), we mapped Beowulf's palette:

**Test Colors Applied:**
- **Indices 0-2**: BLACK (shadow/outline)
- **Indices 3-6**: RED (test color)
- **Indices 7-10**: GREEN (test color)
- **Indices 11-15**: BLUE (test color)

**What Actually Appeared In-Game:**
- **BLUE (indices 11-15)**: Face, hair, neck tie/collar
- **GREEN (indices 7-10)**: Main chest armor, cape, front of boots, AND pixels near the eyes!
- **RED (indices 3-6)**: Secondary armor pieces, legs/pants under armor, gloves, shoulder outlines
- **BLACK (indices 0-2)**: Shadow outlines (as expected)

### Critical Issue: Shared Palette Indices
**IMPORTANT**: Beowulf's sprite has a unique problem - indices 7-10 (GREEN in test) are used for BOTH:
1. Main armor and cape
2. Pixels right beside his left eye

This means making indices 7-10 dark colors creates a "black eye" effect on Beowulf's face. Any themes must keep indices 7-10 relatively light/neutral to avoid this visual issue.

## Scripts

### 1. extract_original_colors.py
Extracts and displays the original Beowulf palette colors. Use this to understand the base colors.

```bash
python extract_original_colors.py
```

### 2. create_simple_color_test.py
Creates a simple 4-color test sprite to verify palette mapping:
- RED for indices 3-6 (armor)
- GREEN for indices 7-10 (secondary elements)
- BLUE for indices 11-15 (cape/details)

```bash
python create_simple_color_test.py
```

### 3. create_temple_knight.py
Creates the Temple Knight theme for Beowulf featuring:
- Silver/platinum armor befitting a holy warrior
- Purple accents representing his magical abilities
- Deep purple cape for a regal appearance

```bash
python create_temple_knight.py
```

## Temple Knight Theme

The Temple Knight theme transforms Beowulf into a regal magical swordsman:

**Color Scheme:**
- **Armor** (indices 3-6): Silver/platinum gradients
- **Accents** (indices 7-10): Purple gradients for magical emphasis
- **Cape** (indices 11-12): Deep purple for nobility
- **Skin** (indices 13-15): Original skin tones preserved
- **Shadows** (indices 0-2): Original shadows preserved

This creates a cohesive look that reflects Beowulf's role as a Temple Knight with magical sword techniques and beast taming abilities.

## Important Notes

### Testing Process
1. First run `create_simple_color_test.py` to verify palette mapping
2. Deploy and test the color-coded version to confirm which indices control what
3. Once mapping is confirmed, run `create_temple_knight.py` for the final theme

### Theme Testing
1. Run `create_temple_knight.py` to generate the theme
2. Deploy with `BuildLinked.ps1`
3. Launch game through Reloaded-II
4. Use F2 to cycle to the Temple Knight theme

## File Structure
Themes are created in:
```
ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_beowulf_test/
ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_beowulf_temple_knight/
```

Contains:
- `battle_beio_spr.bin` - Beowulf's sprite with theme applied