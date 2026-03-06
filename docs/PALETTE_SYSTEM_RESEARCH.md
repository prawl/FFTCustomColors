# Palette System Research

Research findings on how FFT: The Ivalice Chronicles handles color palettes for generic job sprites and Ramza, with implications for per-unit color customization.

## Key Discovery: Two Separate Color Systems

The game uses **two completely different** systems for character colors:

| System | Used For | Mechanism | File |
|--------|----------|-----------|------|
| **CharCLUT (NXD)** | Ramza + special characters | Runtime palette lookup table | `charclut.nxd` |
| **Embedded BIN Palettes** | All generic jobs | Palette data baked into sprite BIN files | `battle_*_spr.bin` |

### Implication
Generic job colors **cannot** be changed via `charclut.nxd`. The charclut table only contains entries for Ramza (keys 1-3), one special character (key 254), and a debug entry (key 255). Generic jobs must be recolored by modifying the palette bytes within their sprite BIN files.

## charclut.nxd Contents

Extracted via FF16Tools.CLI and confirmed via Python sqlite3 analysis.

### Entries

| Key | Key2 | Description | CharaColorSkinId |
|-----|------|-------------|------------------|
| 1 | 0 | Ramza Ch1 - Vanilla (Blue armor) | 1 |
| 1 | 1 | Ramza Ch1 - Red variant | 1 |
| 1 | 2 | Ramza Ch1 - Gray variant | 1 |
| 1 | 3 | Ramza Ch1 - White variant | 1 |
| 2 | 0 | Ramza Ch2/3 - Vanilla (Purple armor) | 1 |
| 2 | 1 | Ramza Ch2/3 - Red variant | 1 |
| 2 | 2 | Ramza Ch2/3 - Gray variant | 1 |
| 2 | 3 | Ramza Ch2/3 - Gold variant | 1 |
| 3 | 0 | Ramza Ch4 - Vanilla (Teal armor) | 1 |
| 3 | 1 | Ramza Ch4 - Red variant | 1 |
| 3 | 2 | Ramza Ch4 - Gray variant | 1 |
| 3 | 3 | Ramza Ch4 - Gold variant | 1 |
| 254 | 0-1 | Special character (blue armor, both identical) | 0 |
| 255 | 0 | Debug/test (purple tones) | 0 |
| 255 | 1 | Debug/test (all black/zeroed) | 0 |

### Ramza CLUT Palette Index Mapping

Each CLUT entry contains 48 bytes (16 colors x 3 RGB channels):

| Index | Purpose | Consistent Across Variants |
|-------|---------|---------------------------|
| 0 | Transparent (always 0,0,0) | Yes |
| 1 | Dark outline | Yes |
| 2 | Light/white highlights | Yes |
| **3-5** | **Primary armor colors (dark to bright)** | **No - these change per variant** |
| 6-8 | Secondary armor / cape / accessories | Partially |
| 9-12 | Under armor / leather / brown tones | Varies by chapter |
| 13-15 | Skin/hair tones | Mostly consistent |

### CharShape Connection

The `CharShape` table (from `CharShape.layout`) contains a `charclut+Id` column that references charclut entries. This is how the game links character shapes to their CLUT palettes. The CharShape table would need to be modified to extend CLUT-based coloring to generic jobs (if that approach were pursued).

## Sprite BIN File Palette Structure

### File Layout
```
Bytes 0-511:    Palette data (16 palettes x 16 colors x 2 bytes per color)
Bytes 512+:     Sprite pixel data (4-bit indexed, 2 pixels per byte)
```

### Palette Format: BGR555
Each color is 2 bytes, little-endian:
```
Bit layout: [Low Byte] [High Byte]
Bits 0-4:   Red   (5 bits, 0-31)
Bits 5-9:   Green (5 bits, 0-31)
Bits 10-14: Blue  (5 bits, 0-31)
Bit 15:     Unused

Conversion to 8-bit: value_8bit = value_5bit * 255 / 31
Conversion to 5-bit: value_5bit = value_8bit * 31 / 255
```

### Palette Slot Usage

Every generic job sprite has the same palette slot pattern:

| Palette Slots | Purpose | Present In |
|---------------|---------|------------|
| **0-4** | **Sprite color variants (per-unit colors)** | All jobs |
| 5-7 | Extra variants (some jobs only) | Bard, Dancer, Calculator, etc. |
| **8-12** | **Portrait/skin tone variants** | All jobs |
| 13-15 | Unused (all zeros) | All jobs |

Confirmed across all 38 generic job sprites. Typical job has 10 active palettes (0-4 + 8-12). Some jobs have 11-13 due to extra slots in 5-7.

### Per-Unit Color Variants (Palettes 0-4)

The game assigns different palette indices to different units of the same job class, giving each unit a distinct appearance. This is the same system used in the original PSX FFT.

**Example: Knight Male (battle_knight_m_spr.bin)**

Indices 0-2 (transparent, outline, highlights) stay constant. Indices 3-15 change:

| Palette | Visual Description | Primary Armor | Cape/Secondary |
|---------|-------------------|---------------|----------------|
| 0 | Gold/Purple | Gold (indices 3-4) | Purple (5-6, 8-10) |
| 1 | Teal/Green | Teal (3-4) | Green (5-6), Gray-green (8-10) |
| 2 | Red/Gray | Red-brown (3-4) | Blue-gray (5-6), Warm gray (8-10) |
| 3 | Gold/Blue-Gray | Gold (3-4) | Blue-gray (5-6), Green (8-10) |
| 4 | Silver/Red | Silver (3-4) | Blue-gray (5-6), Red (8-10) |

### Cross-Theme Comparison

When comparing the **same palette slot** across different mod themes (e.g., palette 0 of knight across amethyst, blood_moon, celestial, corpse_brigade), the following indices change:

- **Indices 0-2**: Never change (transparent, outline, highlights)
- **Indices 3-4**: Rarely change (sigil/emblem colors)
- **Indices 5-10**: **Always change** (primary and secondary armor colors)
- **Indices 11-15**: Sometimes change (accessories, skin, hair)

This confirms that theme creators primarily modify **indices 5-10** for armor recoloring, while keeping character identity markers (indices 0-4, 11-15) mostly intact.

## Existing Codebase Infrastructure

The mod already has all the plumbing needed for palette-based customization:

### Palette Reading
- `BinSpriteExtractor.ReadPalette()` - Reads BGR555 palettes from BIN files
- Handles palette bounds checking, all-black palette fallback

### Palette Writing
- `UserThemeApplicator.ApplyPaletteToSprite()` - Replaces bytes 0-511 in a BIN file with new palette data
- `PaletteModifier.SetPaletteColor()` - Modifies individual BGR555 colors at specific indices

### Section Mappings (Partial)
JSON-based mappings of palette indices to visual sections exist for 3 jobs:
- Knight Male: Cape [9,10,8,7], Underarmor/Sigil [5,6,4,3], Hair/Boots/Gloves [11,12,13]
- Squire Female: HeadbandArmsBoots [4,5,3,7,6], ChestArmor [10,9,8]
- Chemist Female: HoodArms [9,10,8], Dress [5,6,7,4], HairPouchBracersBoots [11,12,13]

Remaining 35 jobs need section mappings completed. See `docs/SPRITE_INDEX_MAPPINGS.md`.

## Per-Unit Customization Feasibility

### The Opportunity
Since the game already uses palettes 0-4 for per-unit color variation, we can:

1. Take the base sprite BIN for a job
2. Modify specific palette slots (0-4) with custom colors per unit
3. Write the modified BIN to the deployment directory
4. Each unit displays its assigned palette variant

### Constraints

**5 variants maximum per job**: Palettes 0-4 give us 5 distinct color schemes per job+gender combination. This covers most gameplay scenarios (rarely >5 units of the same job+gender).

**Game caches palette data in memory**: Simply updating the BIN file on disk does not force the game to redraw. The user must trigger a palette reload by entering party formation and hovering over the affected unit. The game reads the palette into memory on unit interaction, not continuously from disk.

**Palette assignment logic unknown**: How the game decides which unit gets palette 0 vs 1 vs 2 etc. is not yet determined. Possible factors:
- Unit ID / recruitment order
- Party slot position
- Deterministic hash

**Testing needed**: Recruit 3-4 units of the same job+gender, note which color variant each gets, rearrange in party roster, re-enter battle to see if colors follow the unit or shift based on position.

### Architecture for Per-Unit Colors

```
User picks colors for "Knight Variant 1" through "Knight Variant 5"
    |
    v
PaletteModifier writes BGR555 values to palette slots 0-4
    |
    v
UserThemeApplicator replaces bytes 0-511 in battle_knight_m_spr.bin
    |
    v
Modified BIN deployed to FFTIVC/data/enhanced/fftpack/unit/
    |
    v
Game loads palette on next unit interaction in party formation
```

### Alternative Approach: CharShape + CharCLUT Extension

A more ambitious approach would be to:
1. Add new entries to `charclut.nxd` for generic job palette IDs
2. Modify `charshape.nxd` to point generic jobs at these new CLUT entries
3. Let the game's existing CLUT pipeline handle the recoloring

This would bypass the BIN palette system entirely but requires:
- `charshape.nxd` (not currently in the repo - would need extraction from game files)
- Understanding of how CharShape IDs map to generic units
- Risk of conflicts with the existing sprite-swap system

The BIN palette approach is simpler and uses proven infrastructure.

## Tools Reference

### FF16Tools.CLI
Location: `tools/FF16Tools.CLI.exe` (Windows PE executable)

```bash
# Convert NXD to SQLite
FF16Tools.CLI.exe nxd-to-sqlite -i <nxd_directory> -o output.db -g fft

# Convert SQLite back to NXD
FF16Tools.CLI.exe sqlite-to-nxd -i input.db -o <output_directory> -g fft
```

### SQLite Database
Location: `tools/charclut.sqlite` (pre-converted for analysis)

Query with Python:
```python
import sqlite3, json
conn = sqlite3.connect('tools/charclut.sqlite')
cursor = conn.cursor()
cursor.execute('SELECT Key, Key2, CLUTData FROM CharCLUT ORDER BY Key, Key2')
for row in cursor.fetchall():
    key, key2, clut_json = row
    colors = json.loads(clut_json)
    # colors is a flat array: [R0, G0, B0, R1, G1, B1, ... R15, G15, B15]
```

## Research History

- **Sessions 15-18**: Discovered NXD system via Black Boco mod analysis
- **Session 23**: Found charclut.nxd loading in game logs
- **January 2026**: Discovered Green Ramza's charclut.nxd approach
- **March 2026**: Confirmed charclut.nxd contains only Ramza + special characters (no generic jobs). Discovered 5-slot per-unit palette system in sprite BIN files. Identified feasibility of per-unit color customization via palette slot modification.

---

*Documentation created March 6, 2026*
*Based on analysis of charclut.sqlite, sprite BIN file hex dumps, and cross-theme palette comparison*
