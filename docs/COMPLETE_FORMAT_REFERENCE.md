# FFT: The Ivalice Chronicles -- Complete File Format Reference

> Comprehensive reverse-engineering documentation covering every file format used in the FFTColorCustomizer mod and the FFT:TIC remaster's data systems.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Sprite BIN Format](#2-sprite-bin-format)
3. [TEX File Format](#3-tex-file-format)
4. [NXD Database Format](#4-nxd-database-format)
5. [fftpack Virtual Filesystem](#5-fftpack-virtual-filesystem)
6. [Color Pipeline: Unit to Screen](#6-color-pipeline-unit-to-screen)
7. [File Swapping Mechanisms](#7-file-swapping-mechanisms)
8. [Open Questions & Untested Hypotheses](#8-open-questions--untested-hypotheses)

---

## 1. Architecture Overview

FFT:TIC uses **two completely independent color systems** that never overlap:

| System | Used For | Color Storage | Format |
|--------|----------|---------------|--------|
| **Embedded BIN Palettes** | All generic jobs + story characters (non-Ramza) | First 512 bytes of `.bin` sprite files | BGR555, 16 palettes x 16 colors |
| **CharCLUT (NXD)** | Ramza + special characters | `charclut.nxd` database entries | 8-bit RGB triplets, 48 bytes per entry |

The game engine hardcodes which system to use per unit. Generic jobs always read from BIN palettes. Ramza always goes through the CharShape -> CharCLUT NXD chain.

---

## 2. Sprite BIN Format

### 2.1 File Structure

Sprite BIN files have **no header and no magic bytes**. The file begins directly with data:

```
Offset    Size         Section
────────────────────────────────────────
0x000     512 bytes    Palette Data (16 palettes x 16 colors x 2 bytes)
0x200     Variable     Pixel Data (4bpp, 256px wide sprite sheet)
```

Total file sizes range from ~42,953 bytes (Cloud) to ~47,285 bytes (White Mage Female).

### 2.2 Palette Section (0x000 - 0x1FF)

**16 palettes**, each **32 bytes** (16 colors x 2 bytes BGR555):

| Palette | Offset | Purpose |
|---------|--------|---------|
| 0 | `0x000` | **Player units** (your party) |
| 1 | `0x020` | Enemy/NPC variant 1 |
| 2 | `0x040` | Enemy/NPC variant 2 |
| 3 | `0x060` | Enemy/NPC variant 3 |
| 4 | `0x080` | Enemy/NPC variant 4 |
| 5-7 | `0x0A0` | Usually empty (all zeros) |
| 8 | `0x100` | Portrait/status palette (non-transparent color 0) |
| 9 | `0x120` | Portrait variant |
| 10 | `0x140` | Portrait variant |
| 11 | `0x160` | Portrait variant |
| 12 | `0x180` | Portrait variant |
| 13-15 | `0x1A0` | Usually empty (all zeros) |

The game selects palette 0 for player units and palettes 1-4 for enemies. This selection is **hardcoded in the game engine** -- no data file controls it.

### 2.3 BGR555 Color Format

Each color is a **16-bit little-endian** value:

```
Byte layout: [low_byte] [high_byte]
16-bit value = low_byte | (high_byte << 8)

Bit layout:
  15      14-10     9-5      4-0
  ┌──┬──────────┬─────────┬─────────┐
  │ X│  Blue(5) │Green(5) │ Red(5)  │
  └──┴──────────┴─────────┴─────────┘
```

**Decoding (BGR555 -> RGB888):**
```csharp
ushort bgr555 = (ushort)(data[offset] | (data[offset + 1] << 8));
int r = (bgr555 & 0x1F) * 255 / 31;
int g = ((bgr555 >> 5) & 0x1F) * 255 / 31;
int b = ((bgr555 >> 10) & 0x1F) * 255 / 31;
```

**Encoding (RGB888 -> BGR555):**
```csharp
ushort bgr555 = (ushort)((r * 31 / 255) | ((g * 31 / 255) << 5) | ((b * 31 / 255) << 10));
```

**Special values:**
- `0x0000` = Transparent (color index 0 in battle sprites)
- `0x8000` = Black with bit 15 set (sometimes used)

### 2.4 Palette Color Index Semantics

Each 16-color palette has consistent semantic roles across all jobs:

| Index | Role | Safe to Modify? |
|-------|------|:---------------:|
| 0 | Transparent (always 0x0000) | No |
| 1 | Dark outline (near-black) | No |
| 2 | White/light highlights | Caution |
| **3-6** | **Primary armor colors (dark -> bright)** | **Yes** |
| **7-10** | **Secondary costume (cape, cloth, accessories)** | **Yes** |
| 11-12 | Boots, leather, hair, gold accents | Yes |
| 13 | Skin shadow | No |
| 14 | Skin midtone | No |
| 15 | Skin highlight | No |

Palettes 0-4 share identical skin tones (indices 13-15) but differ in costume colors (indices 3-10).

### 2.5 Pixel Data Section (0x200+)

**Format:** 4 bits per pixel (4bpp), uncompressed, linear layout.

- Sheet width: **256 pixels** (fixed)
- Sheet height: variable (typically 332-365 rows depending on animation count)
- Bytes per row: **128** (256 pixels / 2 pixels per byte)

**Pixel packing (low nibble first):**
```
For pixel at (x, y) on the 256-wide sheet:
  byteOffset = 0x200 + (y * 128) + (x / 2)

  if x is even:  colorIndex = byte & 0x0F        // Low nibble
  if x is odd:   colorIndex = (byte >> 4) & 0x0F  // High nibble
```

### 2.6 Sprite Sheet Layout

Individual sprites are **32px wide x 40px tall**, tiled 8 across the 256px sheet:

```
 x=0      x=32     x=64     x=96     x=128    x=160    x=192    x=224
┌────────┬────────┬────────┬────────┬────────┬────────┬────────┬────────┐
│   W    │   SW   │   S    │   NW   │   N    │ Anim 1 │ Anim 2 │ Anim 3 │  Row 0 (y=0-39)
├────────┼────────┼────────┼────────┼────────┼────────┼────────┼────────┤
│  Walk  │  Walk  │  Walk  │  Walk  │  Walk  │  Walk  │  Walk  │  Walk  │  Row 1 (y=40-79)
├────────┼────────┼────────┼────────┼────────┼────────┼────────┼────────┤
│   ...more animation rows...                                           │
└────────┴────────┴────────┴────────┴────────┴────────┴────────┴────────┘
```

East-facing sprites (E, NE, SE) are created by **horizontally mirroring** west-side sprites at runtime.

---

## 3. TEX File Format

TEX files store higher-resolution sprite/portrait textures used for Ramza and story characters.

### 3.1 Two TEX Sub-Formats

| Type | TEX IDs | Pixel Data | Size | Notes |
|------|---------|------------|------|-------|
| **4-bit TEX** | 830-1145 | 4bpp indexed | Variable | Battle sprites, 512px wide |
| **8-bit TEX** | 1552+ | 8bpp indexed | 262,144 bytes | Portraits, palette from separate `pal_*.bin` |

### 3.2 4-bit TEX Structure (Ramza battle sprites)

```
Offset    Size          Section
────────────────────────────────────────
0x000     2,048 bytes   Header (all zeros)
0x800     Variable      Pixel data (4bpp indexed, 512px wide)
```

**Key parameters:**
- Header: 0x800 (2,048) bytes of zeros
- Sheet width: **512 pixels**
- Individual sprites: **32 x 40 pixels**
- Pixel packing: **high nibble = first pixel, low nibble = second pixel** (opposite of BIN!)
- Even TEX IDs (830, 832, ...): Standing poses, 131,072 bytes (504 rows)
- Odd TEX IDs (831, 833, ...): Animation frames, 118,784 bytes (456 rows)

**Pixel extraction:**
```python
# TEX uses HIGH nibble first (opposite of BIN files!)
high_nibble = (byte >> 4) & 0x0F   # First pixel (even offset)
low_nibble = byte & 0x0F            # Second pixel (odd offset)
```

### 3.3 TEX Color Source

**Critical insight:** TEX files contain only **palette index references** (0-15), not actual colors. The actual RGB colors come from:
- **charclut.nxd** at runtime (for Ramza) -- the game's CharCLUT system maps indices to colors
- The TEX file itself has no embedded palette

This means changing Ramza's colors requires modifying `charclut.nxd`, not the TEX pixel data. The pixel data only needs changing when remapping which *semantic slot* a pixel references (e.g., moving hair highlights from skin index 15 to hair index 12).

### 3.4 YOX Compression

Some TEX files use **YOX compression** (zlib/deflate):

```
Offset 0x400: Check for "YOX\0" signature (4 bytes)
If YOX compressed:
  - Skip to offset 0x410 (YOX header is 16 bytes)
  - Skip 2-byte zlib header
  - Decompress using deflate
  - Decompressed size: 131,072 bytes (standard TEX)
```

The `TexFileModifier` in the codebase handles transparent decompression.

### 3.5 Ramza TEX File Inventory

Ramza uses 6 TEX files (3 chapters x 2 per chapter):

| TEX ID | Chapter | Content | Bytes |
|--------|---------|---------|-------|
| tex_830.bin | Ch1 | Standing poses | 131,072 |
| tex_831.bin | Ch1 | Animation frames | 118,784 |
| tex_832.bin | Ch2/3 | Standing poses | 131,072 |
| tex_833.bin | Ch2/3 | Animation frames | 118,784 |
| tex_834.bin | Ch4 | Standing poses | 131,072 |
| tex_835.bin | Ch4 | Animation frames | 118,784 |

### 3.6 Hair Highlight Fix

TEX files for generic jobs (tex_1000 through tex_1013) have a known issue where hair highlight pixels use palette index 15 (skin) instead of index 12 (hair/boots). The `fix_hair_highlight_tex.py` script remaps these:

```
Hair region: localY < 12 (top portion of each 32x40 sprite)
Remap: index 15 -> index 12 within hair region only
```

---

## 4. NXD Database Format

NXD ("Next ExcelDB") is Square Enix's binary database format, shared between FFT:TIC and FFXVI.

### 4.1 File Header

```
Offset  Size  Value      Description
──────────────────────────────────────
0x00    4     "NXDF"     Magic bytes (4E 58 44 46)
0x04    4     1          Version (uint32 LE)
0x08    4     varies     Flags/entry count
```

### 4.2 Table Types

| Type | Key Structure | Example |
|------|---------------|---------|
| SingleKeyed | One primary key | CharShape |
| DoubleKeyed | Primary key + secondary key | CharCLUT, OverrideEntryData |

### 4.3 charclut.nxd Structure (1,657 bytes)

**Schema (from CharCLUT.layout):**
```
table_name:     CharCLUT
table_type:     DoubleKeyed
columns:
  DLCFlags          int
  Comment           string
  CLUTData          byte[]     // Always 48 bytes (16 colors x 3 RGB)
  CharaColorSkinId  int        // Reference to CharaColorSkin table
  UnkBool14         byte       // Enable/disable flag
  // <3 bytes padding>
```

**Key Groups:**

| Key | Key2 Count | Description |
|-----|-----------|-------------|
| 1 | 4 (0-3) | Ramza Chapter 1 variants |
| 2 | 4 (0-3) | Ramza Chapter 2/3 variants |
| 3 | 4 (0-3) | Ramza Chapter 4 variants |
| 254 | 2 (0-1) | Generic blue soldier (CharaColorSkinId=0) |
| 255 | 2 (0-1) | Debug/test purple palette |

### 4.4 CLUTData Payload Map

CLUTData begins at **offset 0x379**, entries are **48 bytes** each, spaced **0x30 apart**:

```
Offset   Key  Key2  Description
─────────────────────────────────────────
0x379    1    0     Chapter 1 - Vanilla Blue
0x3A9    1    1     Chapter 1 - Red
0x3D9    1    2     Chapter 1 - Gray
0x409    1    3     Chapter 1 - White
0x439    2    0     Chapter 2/3 - Vanilla Purple
0x469    2    1     Chapter 2/3 - Red
0x499    2    2     Chapter 2/3 - Gray
0x4C9    2    3     Chapter 2/3 - Gold
0x4F9    3    0     Chapter 4 - Vanilla Teal
0x529    3    1     Chapter 4 - Red
0x559    3    2     Chapter 4 - Gray
0x589    3    3     Chapter 4 - Gold
0x5B9    254  0     Special (blue armor)
0x5E9    254  1     Special (identical)
0x619    255  0     Debug (purple)
0x649    255  1     Debug (all zeros)
```

### 4.5 CLUTData Color Format

Each 48-byte block stores **16 colors as 8-bit RGB triplets**:

```
[R0, G0, B0, R1, G1, B1, R2, G2, B2, ... R15, G15, B15]
```

**Example -- Chapter 1 Vanilla Blue (offset 0x379):**
```
00 00 00 = Index 0:  RGB(0,0,0)        Transparent
28 20 20 = Index 1:  RGB(40,32,32)     Dark outline
E0 D8 D0 = Index 2:  RGB(224,216,208)  Light highlights
28 38 48 = Index 3:  RGB(40,56,72)     Dark blue armor
30 48 68 = Index 4:  RGB(48,72,104)    Medium blue armor
38 60 80 = Index 5:  RGB(56,96,128)    Light blue armor
50 80 B8 = Index 6:  RGB(80,128,184)   Bright blue armor
48 30 28 = Index 7:  RGB(72,48,40)     Boots/leather
60 38 28 = Index 8:  RGB(96,56,40)     Boots mid
90 50 28 = Index 9:  RGB(144,80,40)    Leather/brown
70 40 28 = Index 10: RGB(112,64,40)    Leather mid
B8 78 28 = Index 11: RGB(184,120,40)   Gold accent
D8 98 48 = Index 12: RGB(216,152,72)   Gold highlight
A0 68 28 = Index 13: RGB(160,104,40)   Skin shadow
C8 88 50 = Index 14: RGB(200,136,80)   Skin mid
E8 C0 80 = Index 15: RGB(232,192,128)  Skin highlight
```

### 4.6 NXD Rendering Pipeline

```
Unit Instance
  └─> Spriteset (byte field on unit)
        └─> CharShape (charshape.nxd, SingleKeyed)
              └─> charclut+Id field
                    └─> CharCLUT (charclut.nxd, DoubleKeyed)
                          ├─> CLUTData (48 bytes = actual colors)
                          ├─> CharaColorSkinId -> CharaColorSkin table
                          └─> UnkBool14 (enable/disable)
```

### 4.7 Format Conversion: NXD RGB <-> BIN BGR555

```csharp
// BGR555 (BIN) to RGB888 (NXD CLUTData)
int r8 = (bgr555 & 0x1F) * 255 / 31;
int g8 = ((bgr555 >> 5) & 0x1F) * 255 / 31;
int b8 = ((bgr555 >> 10) & 0x1F) * 255 / 31;

// RGB888 (NXD CLUTData) to BGR555 (BIN)
ushort bgr555 = (ushort)((r8 * 31 / 255) | ((g8 * 31 / 255) << 5) | ((b8 * 31 / 255) << 10));
```

### 4.8 Related NXD Tables

| Table | Type | Purpose |
|-------|------|---------|
| CharShape | SingleKeyed | Maps Spriteset -> charclut+Id |
| CharaColorSkin | - | Conditional palette with UserSituationId |
| CharShapeLUTParam | - | Rendering LUT float parameters |
| OverrideEntryData | DoubleKeyed | Per-unit overrides (54+ columns, remaster's ENTD) |

### 4.9 External Tools

- **FF16Tools.CLI** (`tools/FF16Tools.CLI.exe`): Converts NXD <-> SQLite with `-g fft` flag
- **Nex Layout Files** (`tools/Nex/Layouts/ffto/`): 100+ schema definitions for all NXD tables
- Documentation: https://nenkai.github.io/ffxvi-modding/resources/formats/nxd/

---

## 5. fftpack Virtual Filesystem

### 5.1 How It Works

`fftpack.bin` is the original PlayStation archive containing all game assets. In FFT:TIC, the FFTIVC modloader **virtualizes** it into a directory tree. The mod never reads fftpack.bin directly -- it places files at virtual paths that the modloader intercepts.

### 5.2 Directory Structure

```
[ModDir]/FFTIVC/data/enhanced/
├── fftpack/
│   ├── unit/                              # Standard sprites (SPR BIN files)
│   │   ├── sprites_original/              # 57 original backup files
│   │   ├── sprites_lucavi/                # Theme directories (188 total)
│   │   ├── sprites_corpse_brigade/
│   │   ├── sprites_[theme_name]/          # Global themes (all 38 jobs)
│   │   ├── sprites_[job]_[theme]/         # Job-specific themes (one job only)
│   │   └── sprites_[character]_[theme]/   # Story character themes
│   └── unit_psp/                          # WotL-exclusive sprites
│       ├── sprites_original/              # 4 WotL originals
│       └── sprites_[theme]/               # WotL themes
├── system/ffto/g2d/                       # TEX files
│   └── tex_1000.bin ... tex_1013.bin      # 28 generic job TEX files
└── nxd/
    └── charclut.nxd                       # Character CLUT overrides

[ModDir]/RamzaThemes/                      # Ramza TEX themes (outside game path)
├── dark_knight/   tex_830.bin - tex_835.bin
├── white_heretic/ tex_830.bin - tex_835.bin
└── crimson_blade/ tex_830.bin - tex_835.bin
```

### 5.3 Sprite File Naming

**Generic jobs:** `battle_[japanese_name]_[m/w]_spr.bin`

| Job | Internal Name | Job | Internal Name |
|-----|--------------|-----|--------------|
| Squire | mina | Samurai | samu |
| Chemist | item | Dragoon | ryu |
| Knight | knight | Geomancer | fusui |
| Archer | yumi | Mystic | onmyo |
| Monk | monk | Mediator | waju |
| White Mage | siro | Calculator | san |
| Black Mage | kuro | Bard | gin (M only) |
| Time Mage | toki | Dancer | odori (F only) |
| Summoner | syou | Mime | mono |
| Thief | thief | Ninja | ninja |

**WotL jobs** (unit_psp/): `spr_dst_bchr_[name]_[m/w]_spr.bin`
- Dark Knight: `ankoku`, Onion Knight: `tama`

**Story characters:** `battle_[name]_spr.bin` (single gender)
- Agrias: `aguri`, Orlandeau: `oru`, Mustadio: `musu`, Cloud: `cloud`
- Ramza: `ramuza` (Ch1), `ramuza2` (Ch2/3), `ramuza3` (Ch4)

### 5.4 Theme Directory Patterns

| Pattern | Scope | Example |
|---------|-------|---------|
| `sprites_[theme]` | All 38 generic jobs | `sprites_lucavi/` |
| `sprites_[job]_[theme]` | Single job only | `sprites_knight_h78/` |
| `sprites_[char]_[theme]` | Story character | `sprites_agrias_demon_hunter/` |

---

## 6. Color Pipeline: Unit to Screen

### 6.1 Generic Jobs (BIN Palette System)

```
Battle starts
  └─> Game loads battle_[job]_[m/w]_spr.bin
        └─> Reads palette section (bytes 0x000-0x1FF)
              ├─> Player unit: selects Palette 0
              └─> Enemy unit: selects Palette 1, 2, 3, or 4
                    └─> Pixel data (4bpp) indexes into selected 16-color palette
                          └─> BGR555 decoded to display colors
```

**Mod intervention point:** Replace the entire BIN file (pre-built theme) or just the first 512 bytes (custom palette).

### 6.2 Ramza (CharCLUT NXD System)

```
Battle starts
  └─> Game identifies Ramza's Spriteset
        └─> Looks up CharShape (charshape.nxd)
              └─> Gets charclut+Id value
                    └─> Reads CharCLUT entry (charclut.nxd, Key=chapter, Key2=variant)
                          └─> CLUTData (48 bytes RGB) applied to TEX pixel indices
                                └─> Index 0-15 mapped to actual RGB colors
```

**Mod intervention point:** Patch CLUTData bytes in charclut.nxd at hardcoded offsets.

### 6.3 Color System Comparison

| Aspect | BIN Palettes | NXD CharCLUT |
|--------|-------------|--------------|
| Color format | BGR555 (2 bytes/color) | RGB888 (3 bytes/color) |
| Colors per entry | 16 | 16 |
| Palette variants | 5 battle + 5 portrait | 4 per chapter |
| Total palette bytes | 512 (entire section) | 48 (CLUTData only) |
| Pixel format | 4bpp, low nibble first | 4bpp, high nibble first |
| Applies to | All generic jobs, story chars | Ramza only (keys 1-3) |
| Runtime modifiable | No (cached on load) | Via NXD file replacement |

---

## 7. File Swapping Mechanisms

The mod uses three complementary approaches:

### 7.1 Mechanism A: File Copy at Startup

`SpriteFileCopier.cs` / `ConfigBasedSpriteManager.cs`

On config load/change, copies themed BIN from `sprites_[theme]/` to the active `unit/` directory. Falls back to path redirection if file is locked.

### 7.2 Mechanism B: Runtime Path Interception

`FileInterceptor.cs` -> `SpriteFileInterceptor.cs`

When the game requests `unit/battle_knight_m_spr.bin`, the mod intercepts and redirects to `unit/sprites_dark_knight/battle_knight_m_spr.bin`. Two-tier lookup: per-job config first, then global theme fallback.

### 7.3 Mechanism C: NXD Database Override

`RamzaNxdService.cs` -> `NxdPatcher.cs`

For Ramza only: patches `charclut.nxd` with modified CLUTData at hardcoded byte offsets. Deployed to `FFTIVC/data/enhanced/nxd/charclut.nxd`.

### 7.4 User Theme Application

`UserThemeApplicator.ApplyPaletteToSprite()`:
```csharp
// Replace first 512 bytes of original sprite with user's custom palette
Array.Copy(userPalette, 0, originalSprite, 0, 512);
```

User themes stored as 512-byte `palette.bin` files under `UserThemes/{job}/{themeName}/`.

### 7.5 Reloaded-II Dependencies

| Component | Purpose |
|-----------|---------|
| `fftivc.utility.modloader` | Virtual filesystem overlay |
| `Reloaded.Memory.SigScan.ReloadedII` | Memory signature scanning |
| `reloaded.sharedlib.hooks` | Function hooking |

---

## 8. Open Questions & Untested Hypotheses

### 8.1 Enemy Palette Selection Algorithm
How does the game pick palette 1 vs 2 vs 3 vs 4 for different enemy units of the same job? Possible factors: unit ID, recruitment order, party slot, deterministic hash. **Unknown.**

### 8.2 CharCLUT for Generic Jobs
Can `charshape.nxd` be modified to assign `charclut+Id` values to generic jobs, routing them through the CharCLUT system instead of embedded BIN palettes? This would enable per-unit color control without full sprite replacement. **Untested -- documented as highest priority experiment.**

### 8.3 Keys 254/255 in CharCLUT
These entries have `CharaColorSkinId=0` (not character-specific), suggesting the CLUT system was designed for broader use than just Ramza. **Purpose unknown.**

### 8.4 OverrideEntryData Binary Payloads
The remaster's ENTD replacement (`overrideentrydata.nxd`) has 54+ columns including Spriteset overrides and undocumented binary payloads that may encode color data. **Not fully analyzed.**

### 8.5 Runtime Palette Caching
The game caches palette data in memory. Modifying BIN files on disk does not force a redraw. Users must trigger a palette reload by entering party formation and hovering over the affected unit. **No known way to force runtime reload.**

### 8.6 NXD Header Internals
Bytes 0x0C through 0x378 of charclut.nxd contain key group descriptors, per-row metadata, and relative offset tables. The codebase bypasses full parsing by using hardcoded offsets. **Partially mapped but not fully reverse-engineered.**

---

## Appendix A: Key Source Files

| File | Role |
|------|------|
| `ColorMod/Utilities/BinSpriteExtractor.cs` | BGR555 palette reading, 4bpp pixel extraction |
| `ColorMod/ThemeEditor/PaletteModifier.cs` | BGR555 palette writing and modification |
| `ColorMod/Utilities/UserThemeApplicator.cs` | 512-byte palette injection into BIN files |
| `ColorMod/Utilities/TexFileModifier.cs` | TEX decompression (YOX) and RGB555 modification |
| `ColorMod/Services/NxdPatcher.cs` | Binary patching of charclut.nxd at hardcoded offsets |
| `ColorMod/Services/RamzaBinToNxdBridge.cs` | BGR555 <-> RGB888 conversion bridge |
| `ColorMod/Services/RamzaNxdService.cs` | Per-chapter Ramza NXD patching orchestration |
| `ColorMod/Services/RamzaBuiltInThemePalettes.cs` | Hardcoded original + theme palette values |
| `ColorMod/Utilities/SpriteFileManager.cs` | File swapping and path interception |
| `ColorMod/Utilities/SpriteFileInterceptor.cs` | Runtime path redirection |
| `ColorMod/Services/FFTIVCPathResolver.cs` | Versioned mod path resolution |
| `ColorMod/Data/SectionMappings/*.json` | Per-job palette index -> visual section maps |
| `scripts/fix_hair_highlight_tex.py` | TEX hair highlight pixel remapping tool |
| `scripts/fix_enemy_palettes.py` | Restores zeroed enemy palettes in themed sprites |
| `tools/FF16Tools.CLI.exe` | NXD <-> SQLite conversion tool |
| `tools/Nex/Layouts/ffto/*.layout` | NXD table schema definitions (100+ files) |

## Appendix B: Quick Reference Card

```
┌─────────────────────────────────────────────────────────────┐
│                  FFT:TIC FORMAT QUICK REF                    │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  SPRITE BIN                    TEX FILE                      │
│  ──────────                    ────────                      │
│  No header/magic               0x800 zero header             │
│  0x000: 512B palettes          0x800: pixel data             │
│  0x200: 4bpp pixels            4bpp indexed, 512px wide      │
│  256px wide sheet              High nibble first              │
│  Low nibble first              May be YOX compressed         │
│  BGR555 LE colors              No embedded palette           │
│                                Colors from charclut.nxd      │
│                                                              │
│  NXD (charclut.nxd)           VIRTUAL FILESYSTEM            │
│  ──────────────────           ──────────────────             │
│  Magic: "NXDF"                FFTIVC/data/enhanced/          │
│  Version: 1                     fftpack/unit/     (sprites)  │
│  CLUTData @ 0x379               fftpack/unit_psp/ (WotL)     │
│  48 bytes per entry             system/ffto/g2d/  (TEX)      │
│  16 colors x 3 RGB bytes       nxd/              (NXD)       │
│  0x30 spacing between                                        │
│                                                              │
│  COLOR FORMATS                                               │
│  ─────────────                                               │
│  BIN: BGR555 (2 bytes, 5-5-5 bits, LE)                      │
│  NXD: RGB888 (3 bytes, 8-8-8 bits)                          │
│  TEX: indices only (colors from NXD)                         │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```
