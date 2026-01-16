# NXD File Format Reference

Technical documentation for FFT: The Ivalice Chronicles NXD database files used for game data overrides.

## Overview

NXD (Next ExcelDB) is a binary database format used by Square Enix games including FFT: The Ivalice Chronicles. It stores structured game data that can be overridden by mods without modifying core game files.

## Key Discovery: charclut.nxd for Ramza Colors

The **Green Ramza** mod demonstrated that `charclut.nxd` (Character Color Lookup Table) is the simplest and most effective way to change Ramza's armor colors - far simpler than the TEX file approach documented in CREATING_RAMZA_THEMES_RESEARCH.md.

## File Structure

### Header
```
Offset  Size  Description
0x00    4     Magic: "NXDF" (4E 58 44 46)
0x04    4     Version: 1 (little-endian)
0x08    4     Entry count (number of TOC entries)
0x0C    ...   Table of Contents entries
```

### Table of Contents (TOC)
Each TOC entry is 8 bytes:
```
Offset  Size  Description
0x00    4     Data offset (relative to start of data section)
0x04    4     Data size
```

### Data Section
Contains the actual database rows, format depends on the specific NXD type.

## Tools

### FF16Tools.CLI
The primary tool for working with NXD files:

```bash
# Convert NXD directory to SQLite database
FF16Tools.CLI nxd-to-sqlite -i <nxd_directory> -o output.db -g fft

# Convert SQLite back to NXD files
FF16Tools.CLI sqlite-to-nxd -i input.db -o <output_directory> -g fft
```

**Location in project:** `tools/FF16Tools.CLI.exe`

### Layout Files
FF16Tools uses layout files to understand NXD schemas:
- Location: `tools/Nex/Layouts/ffto/`
- Relevant layouts:
  - `CharaColorSkin.layout` - Character color/skin definitions
  - `Job.layout` - Job definitions
  - `Ability.layout` - Ability definitions

## charclut.nxd (Character Color Lookup Table)

### Purpose
Defines color palettes for characters, allowing runtime palette swapping without modifying sprite data.

### Schema
| Column | Type | Description |
|--------|------|-------------|
| Key | INTEGER | Character/Chapter ID (1=Ch1, 2=Ch2/3, 3=Ch4) |
| Key2 | INTEGER | Palette variant (0=Vanilla, 1-3=DLC/alternates) |
| DLCFlags | INTEGER | DLC requirement flags |
| Comment | TEXT | Human-readable description |
| CLUTData | TEXT | JSON array of 48 RGB values (16 colors × 3 channels) |
| CharaColorSkinId | INTEGER | Reference to CharaColorSkin table |
| UnkBool14 | INTEGER | Unknown boolean flag |

### CLUTData Format
48 integers representing 16 RGB colors:
```
[R0, G0, B0, R1, G1, B1, R2, G2, B2, ... R15, G15, B15]
```

### Ramza Chapter Mappings
| Key | Key2 | Description |
|-----|------|-------------|
| 1 | 0 | Chapter 1 - Vanilla |
| 1 | 1 | Chapter 1 - Red variant |
| 1 | 2 | Chapter 1 - Gray variant |
| 1 | 3 | Chapter 1 - White variant |
| 2 | 0 | Chapter 2/3 - Vanilla (Purple) |
| 2 | 1-3 | Chapter 2/3 - Alternate variants |
| 3 | 0 | Chapter 4 - Vanilla (Teal) |
| 3 | 1-3 | Chapter 4 - Alternate variants |
| 254 | 0-1 | Unknown (possibly special character) |
| 255 | 0-1 | Unknown (possibly debug/test) |

### Palette Index Mapping (Ramza)
| Index | Purpose | Safe to Edit |
|-------|---------|--------------|
| 0 | Transparent | No |
| 1 | Dark outline | No |
| 2 | Light/white highlights | Caution |
| **3-6** | **Armor colors (dark to bright)** | **Yes** |
| 7-8 | Boots/gloves/accessories | Yes |
| 9-12 | Under armor / brown tones | Caution |
| 13-15 | Hair/skin tones | No |

### Example: Green Ramza Transformation

Original Chapter 1 armor (Blue):
```
Index 3: RGB( 48,  72, 104)  - Dark blue
Index 4: RGB( 56,  96, 136)  - Medium blue
Index 5: RGB( 80, 128, 184)  - Light blue
Index 6: RGB(~similar)       - Bright blue
```

Green Ramza modification:
```
Index 3: RGB( 48,  56,  40)  - Dark green
Index 4: RGB( 64,  80,  40)  - Medium green
Index 5: RGB( 88, 104,  40)  - Light green
Index 6: RGB(112, 128,  48)  - Bright green
```

The transformation shifts the dominant channel from Blue to Green.

## overrideentrydata.nxd (Unit Overrides)

### Purpose
Overrides unit data including sprites, jobs, equipment, and colors. Used by Black Boco mod.

### Schema (54 columns)
Key columns:
| Column | Type | Description |
|--------|------|-------------|
| Key | INTEGER | Unit ID |
| Key2 | INTEGER | Variant/instance |
| Spriteset | INTEGER | Sprite set reference |
| MainJob | INTEGER | Primary job ID |
| JobUnlock | INTEGER | Job unlock flags |
| Head/Body/Accessory | INTEGER | Equipment slots |
| Level/Bravery/Faith | INTEGER | Unit stats |

### Known Unit IDs (from Black Boco)
| Key | Description |
|-----|-------------|
| 83 | Boco (story chocobo) |
| 93 | Boco variant |
| 105 | Boco variant |
| 300-500 | Generic enemies |

### Binary Payload
The overrideentrydata.nxd contains embedded binary data beyond the database structure that holds actual sprite/color modifications. This payload:
- Is preserved but not exposed by FF16Tools
- Has an unknown format
- Cannot be safely modified without understanding its structure

## Deployment

### Correct Path Structure
```
[Mod]/FFTIVC/data/enhanced/nxd/[filename].nxd
```

**Critical:** Must be under `FFTIVC/` prefix, not just `data/enhanced/nxd/`.

### How the Game Loads NXD Overrides
1. Game loads base NXD files from game data
2. Modloader checks for override NXD files in mod directories
3. Override entries replace/add to base data
4. Changes take effect immediately (no restart needed for some)

### Save File Interaction
**Warning:** Some NXD changes may be baked into save files:
- Save files created with mods may retain mod data
- Disabling mods doesn't always revert save file data
- Test with NEW GAME to verify mod changes

## Comparison: NXD vs TEX Approaches

| Aspect | charclut.nxd | TEX Files |
|--------|--------------|-----------|
| **File size** | ~1.6 KB | ~700 KB (6 files) |
| **Complexity** | Simple palette values | UV coordinate mapping |
| **Tools needed** | FF16Tools | FFT Sprite Toolkit |
| **Edit method** | SQLite database | Binary sprite sheets |
| **Best for** | Color/hue changes | Complete visual redesign |
| **Deployment** | Single file | Multiple files per chapter |

## Creating Custom Ramza Colors via charclut.nxd

### Step 1: Extract Original
```bash
# Get original charclut.nxd from game files or existing mod
# Convert to SQLite for editing
cd tools
./FF16Tools.CLI.exe nxd-to-sqlite -i <path_to_nxd_dir> -o charclut.db -g fft
```

### Step 2: Edit Colors
```python
import sqlite3
import json

conn = sqlite3.connect('charclut.db')
cursor = conn.cursor()

# Get Chapter 1 vanilla palette
cursor.execute("SELECT CLUTData FROM CharCLUT WHERE Key=1 AND Key2=0")
clut_data = json.loads(cursor.fetchone()[0])

# Modify armor colors (indices 3-6, each is R,G,B = 3 values)
# Index 3 starts at position 9 (3*3)
clut_data[9:12] = [255, 0, 0]    # Index 3 = Red
clut_data[12:15] = [200, 0, 0]   # Index 4 = Darker red
clut_data[15:18] = [150, 0, 0]   # Index 5 = Even darker
clut_data[18:21] = [100, 0, 0]   # Index 6 = Darkest

# Update database
cursor.execute(
    "UPDATE CharCLUT SET CLUTData=? WHERE Key=1 AND Key2=0",
    (json.dumps(clut_data),)
)
conn.commit()
```

### Step 3: Convert Back to NXD
```bash
./FF16Tools.CLI.exe sqlite-to-nxd -i charclut.db -o output_nxd -g fft
```

### Step 4: Deploy
Copy the generated `charclut.nxd` to:
```
[YourMod]/FFTIVC/data/enhanced/nxd/charclut.nxd
```

## Other Relevant NXD Files

| File | Purpose |
|------|---------|
| `characolorskin.nxd` | Character color/skin definitions |
| `chara.nxd` | Character base definitions |
| `charaname.nxd` | Character name strings |
| `job.nxd` | Job definitions |
| `ability.nxd` | Ability definitions |
| `battle.nxd` | Battle parameters |

## Known Limitations

1. **Binary payload in overrideentrydata.nxd** - Cannot be easily edited
2. **Layout files required** - FF16Tools needs correct layout schemas
3. **Save file persistence** - Some changes bake into saves
4. **Limited documentation** - Format is reverse-engineered

## Research History

- **Session 15-18:** Discovered NXD system via Black Boco mod analysis
- **Session 23:** Found charclut.nxd loading in game logs
- **January 2026:** Discovered Green Ramza's charclut.nxd approach

## References

- FF16Tools: https://github.com/Nenkai/FF16Tools
- FFHacktics community documentation
- Green Ramza mod (Nexus Mods ID: 68)
- Black Boco mod analysis (documented in CREATING_RAMZA_THEMES_RESEARCH.md)

---

*Documentation created January 16, 2026*
*Based on analysis of Green Ramza mod and FF16Tools*
