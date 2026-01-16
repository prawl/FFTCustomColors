# Hair Highlight / Skin Color Separation Fix

## Problem Statement

User reported that on the Squire (and potentially other jobs), the hair highlight is linked to the skin color. When changing skin tone, the light patch on the hair also changes because both use the same palette index (index 15).

**User comment:** "the hair highlight issue is still present. From what I've discovered, the light patch of hair is linked to the skin color. is there a way you can make the hair color not attached to the Skin toggles?"

## Root Cause

The original FFT sprites reuse palette indices for different visual elements. For Squire Male:
- **SkinColor section**: indices `[15, 14]` (base, shadow)
- **BootsAndHair section**: indices `[12, 13, 11]` (base, shadow, outline)

The hair highlight pixels in the head region are painted using index 15 (same as skin base), so they change color whenever skin color changes.

## Solution Approach

**Modify the sprite pixel data** to remap hair highlight pixels from index 15 to index 12 (or another hair index). This separates the hair highlight from the skin color at the pixel level.

### Why This Works
- The mod currently only swaps palette colors, not pixel data
- By changing which palette index the hair highlight pixels use, they will follow the Hair color instead of Skin color
- This is a one-time modification to the sprite files

## Progress

### Phase 1: Hair Highlight Fix (COMPLETED - TESTED WORKING)

Successfully separated hair highlight from skin color:
- [x] Analyzed sprite pixel data to find hair highlight pixels using index 15
- [x] Found clear gap at Y=10-11 separating hair (Y<10) from face (Y>=12)
- [x] Remapped 30 hair highlight pixels from index 15 → index 12
- [x] **TESTED IN-GAME: WORKING** - Skin color no longer affects hair highlight

### Phase 2: Separate Hair from Boots/Gloves (ATTEMPTED - BLOCKED)

Attempted to give Hair its own slider separate from Boots/Gloves:

**Analysis Results:**
- Hair region (Y < 15): ~228 pixels using indices 11, 12, 13
- Boots/Gloves region (Y >= 25): ~395 pixels using same indices
- Need 2-3 unused palette indices for a separate Hair section

**Attempted Solution:**
- Tried repurposing indices 1 and 2 (appeared low-usage in analysis)
- Index 1: 406 pixels, Index 2: 44 pixels
- Remapped hair pixels from 12→1 and 13→2

**Result: FAILED**
- Indices 1 and 2 control the **shadow outline around the character**
- Changing these indices breaks the character's visual appearance
- Reverted changes

### Current State
- Sprite reverted to original (with hair highlight still linked to BootsAndHair via index 12)
- JSON mapping reverted to original BootsAndHair section
- Need to find truly unused indices or accept shared Hair/Boots/Gloves section

## Palette Index Usage (Squire Male)

```
Index  0: 7399 pixels - Transparent/background
Index  1:  406 pixels - SHADOW OUTLINE (cannot repurpose)
Index  2:   44 pixels - SHADOW OUTLINE (cannot repurpose)
Index  3:  175 pixels - Armor outline
Index  4:  182 pixels - Armor shadow
Index  5:  208 pixels - Armor base
Index  6:   45 pixels - Belt shadow
Index  7:   17 pixels - Belt base
Index  8:  267 pixels - Hat outline
Index  9:  173 pixels - Hat shadow
Index 10:  241 pixels - Hat base
Index 11:  288 pixels - BootsAndHair outline
Index 12:  364 pixels - BootsAndHair base (includes hair after fix)
Index 13:  342 pixels - BootsAndHair shadow
Index 14:   50 pixels - Skin shadow
Index 15:   39 pixels - Skin base (was 69, now 39 after hair highlight fix)
```

**All 16 palette indices are in use.** There are no unused indices to repurpose for a separate Hair section without breaking other visual elements.

## Options Going Forward

### Option 1: Accept Shared Hair/Boots/Gloves (Current State)
- Hair highlight is separated from skin (working)
- Hair still shares color with boots/gloves
- Simplest solution, no visual artifacts

### Option 2: Merge Low-Usage Indices
- Index 7 (17 pixels) could potentially be merged into index 6 (both are Belt)
- This would free up one index, but we need 2-3 for a full Hair section
- Risk: May cause visual differences in belt shading

### Option 3: 2-Color Hair Section
- Find one freeable index for Hair base
- Share outline (index 11) between Hair and Boots/Gloves
- Hair would only have base + shared outline (no dedicated shadow)
- Less color control but achieves separation

### Option 4: Runtime Palette Manipulation
- Don't remap pixels, instead dynamically swap palette colors
- More complex, but doesn't require sprite modifications
- Could allow per-frame color changes

## Technical Notes

### BIN File Structure
```
Bytes 0-511:    16 palettes × 16 colors × 2 bytes (BGR555 format)
Bytes 512+:     Pixel data (4-bit indexed, 2 pixels per byte)
                256 pixels wide sprite sheet
                Each sprite is 32×40 pixels
```

### Pixel Index Calculation
```csharp
int pixelIndex = (sheetY * 256) + sheetX;
int byteIndex = 512 + (pixelIndex / 2);
// Low nibble if pixelIndex is even, high nibble if odd
```

### Key Files
- `ColorMod/Utilities/SpritePixelRemapper.cs` - Main utility class
- `Tests/Utilities/SpritePixelRemapperTests.cs` - Analysis and generation tests
- `ColorMod/Data/SectionMappings/Squire_Male.json` - Section mapping

### Generated Files (for reference)
- `docs/hair_highlight_fix/` - Visualizations and fixed sprite from Phase 1
- `docs/hair_separation/` - Failed attempt at full separation (Phase 2)

## Lessons Learned

1. **Index 1 and 2 are critical** - They control shadow outlines, not just minor details
2. **All 16 indices are purposefully used** in FFT sprites - no "free" slots
3. **Y-position cutoff works well** for separating head from body regions
4. **Hair highlight fix (Phase 1) is solid** - Can be applied to other jobs

## Critical Finding: Pixel Remapping Does NOT Work In-Game

**Tested 2024:** Pixel data modifications in FFTPack .bin files do NOT affect in-game rendering.

### Test Results
1. **UI Config Preview**: Reads pixel data from FFTPack .bin files ✓
2. **In-Game Rendering**: Does NOT read pixel data from FFTPack .bin files ✗

### Proof
- Set ALL pixels to index 0 (transparent) in FFTPack .bin file
- **UI Config**: Sprite disappeared (pixel data read from .bin)
- **In-Game**: Sprite displayed normally (pixel data from elsewhere)

### Architecture
- **FFTPack .bin files**: Only PALETTE data is used by the game
- **Pixel/texture data**: Comes from g2d.dat or original game archives
- **G2D modding**: Requires different file format (not same as FFTPack .bin)

### Conclusion
**The hair highlight fix is NOT possible through FFTPack palette modding alone.**

Potential future solutions:
1. Modify g2d.dat files directly (requires understanding g2d file format)
2. Contact FFTIVC mod loader author about pixel data interception
3. Accept the limitation: hair highlight will always follow skin color

## Next Steps

1. ~~Apply Phase 1 fix (hair highlight → index 12) to other affected jobs~~ NOT POSSIBLE via FFTPack
2. Document this limitation in user-facing documentation
3. ~~Consider g2d.dat modding as future enhancement~~ **NOW PURSUING - SEE BELOW**

---

## Phase 3: TEX File Modding (G2D System) - IN PROGRESS

### Breakthrough Discovery (2026-01-12)

TEX files in the g2d folder CAN modify generic job sprites, not just Ramza!

### Test: Pink Armor Experiment

**Goal:** Verify if TEX files control generic job battle sprites

**Process:**
1. Modified ALL Squire Male BMPs (924, 925, 980-993) with pink armor palette
2. Used FFT Sprite Toolkit to generate TEX files
3. Placed TEX files in mod's g2d folder
4. Game loaded TEX files (confirmed in logs)

**Result:**
- Squire DID change colors ✓
- BUT appeared brown, NOT pink

### Key Finding: TEX Files Use Index Remapping, Not RGB Colors

The TEX file system does NOT store arbitrary RGB colors. Instead:
- TEX files remap palette indices to a **fixed game master palette**
- When we put pink RGB(255,0,255) in the BMP, the toolkit found the **nearest existing color**
- The nearest color was brown, so the Squire appeared brown

**This aligns with how Ramza themes work:**
- We're not adding new colors to the game
- We're pointing indices to different existing colors in the master palette

### Why This Is Good News for Hair Highlight Fix

The hair highlight fix doesn't need new colors - it just needs to change which INDEX pixels use:
- Current: Hair highlight pixels use index 15 (skin color)
- Goal: Hair highlight pixels use index 12 (boots/hair color)
- Both indices already exist in the palette

If we can edit the TEX file's pixel data directly, we can remap indices without any color changes.

### TEX File Analysis

**File:** `tex_992.bin` (Squire Male)
**Size:** 131,072 bytes (128 KB)
**Location:** `FFTIVC/data/enhanced/system/ffto/g2d/`

**Sprite BMP files that map to Squire Male TEX:**
- 924, 925 (possibly event/cutscene versions)
- 980-993 (palette variants for different team colors)

### TEX File Format (REVERSE ENGINEERED!)

**Structure:**
```
Bytes 0x000-0x7FF: Header (2048 bytes of zeros)
Bytes 0x800+:     Pixel data (4-bit indexed, 2 pixels per byte)
                  High nibble = first pixel, low nibble = second pixel
                  Sprite sheet: 512x512 pixels
                  Individual sprites: 32x40 pixels
```

**Analysis Results (tex_992.bin - Squire Male):**
- File size: 131,072 bytes (128 KB)
- Pixel data: 129,024 bytes (258,048 pixels at 4-bit indexed)
- Index 15 pixels found: 1,322 total
  - Hair region (localY < 12): 660 pixels
  - Face region (localY >= 12): 662 pixels

### Direct TEX Edit Solution (WORKING!)

Created `scripts/fix_hair_highlight_tex.py` that:
1. [x] Reads TEX file pixel data starting at offset 0x800
2. [x] Calculates sprite-local Y position for each pixel
3. [x] Remaps index 15 → index 12 only in hair region (localY < 12)
4. [x] Preserves face region pixels (keeps index 15)

**Test Run:**
```
Total index 15 pixels found: 1322
Hair region pixels remapped (15->12): 660
Face region pixels kept (index 15): 662
Bytes modified: 527 (some bytes have both nibbles changed)
```

### Next Step: Test In-Game

1. [ ] Deploy fixed tex_992.bin to mod's g2d folder
2. [ ] Test in-game to confirm hair highlight follows BootsAndHair instead of Skin

### Scripts Created

- `scripts/fix_hair_highlight_bmp.py` - Remaps hair pixels in BMP (for toolkit approach)
- `scripts/fix_hair_highlight_tex.py` - **DIRECT TEX EDIT** (bypasses toolkit!)
- `scripts/make_pink_test.py` - Creates pink armor test BMP
- `scripts/make_all_pink.py` - Makes all Squire Male BMPs pink for testing

### Log Evidence

TEX files are loaded by the mod loader:
```
[fftivc.utility.modloader] G2D: fftivc.custom.highlight_fixed mapping G2D file 992 from ...\tex_992.bin
[fftivc.utility.modloader] Hooked CFILE_DAT::Decode for g2d.dat @ 0x14FA4E5CB
```

---

## Phase 4: Working Hair Highlight Fix (2026-01-12)

### SUCCESS: Hair Highlight Separated from Skin!

**The hair highlight fix is WORKING.** Hair highlight pixels now follow the BootsAndHair color instead of Skin.

### Critical Bug Fix: Header Offset

The original script was not accounting for the 0x800 (2048 byte) header in TEX files:

**Before (BROKEN):**
```python
# Processed from byte 0, wrong Y calculation
for byte_offset in range(len(data)):
    pixel_idx = byte_offset * 2  # WRONG - includes header bytes
```

**After (WORKING):**
```python
HEADER_SIZE = 0x800  # TEX files have 2KB header
for byte_offset in range(HEADER_SIZE, len(data)):
    pixel_data_offset = byte_offset - HEADER_SIZE
    pixel_idx = pixel_data_offset * 2  # Correct - relative to pixel data
```

Without this fix, Y positions were calculated 8 rows too high (2048 bytes = 4096 pixels = 8 rows at 512 width).

### Test Results (v6)

Using clean original from `original_squire` mod:
```
TEX file: tex_992_clean_original.bin
Size: 131072 bytes
Header size: 2048 bytes (0x800)
Pixel data: 129024 bytes
Hair region: localY < 12

Total index 15 pixels: 805
Hair region remapped (15->12): 359
Face region kept (index 15): 446
```

**In-Game Result:**
- ✅ Hair highlight follows BootsAndHair color (not skin)
- ✅ Face highlights still follow Skin color
- ⚠️ A few edge pixels still showing through with bright colors
- ❌ Armor shows brown/original color instead of themed color

### Known Issues

#### Issue 1: Edge Pixels Showing Through
Some pixels at the boundary between hair and face regions still show skin color when BootsAndHair is set to a bright color. May need to:
- Adjust Y threshold from 12 to 14
- Use neighbor-based detection for edge cases

#### Issue 2: Armor Colors Not Applying
When using the TEX file fix, armor shows brown (original) instead of themed colors.

**Theory:** The game loads BOTH file types:
- `tex_*.bin` (from g2d/) - Contains pixel index data
- `battle_*_spr.bin` (from fftpack/unit/) - Contains palette data + sprite layout

The theme system may work by modifying the `_spr.bin` palette, but when we deploy a custom TEX file, it may not be picking up the themed palette.

**Log Evidence:**
```
[G2D] Accessing file 992
[FFTPack] Accessing modded file 140 -> unit/battle_mina_m_spr.bin
```

Both files are loaded for the same sprite - one provides pixel indices, the other provides palette.

### File Format Summary

**TEX File (`tex_992.bin`):**
```
Offset 0x000-0x7FF: Header (2048 bytes, all zeros)
Offset 0x800+:      4-bit indexed pixel data
                    High nibble = first pixel
                    Low nibble = second pixel
                    512x504 effective pixels (129,024 bytes)
```

**SPR File (`battle_mina_m_spr.bin`):**
```
Offset 0x000-0x1FF: Palette data (multiple palettes, RGB555)
Offset 0x200-0x3FF: Padding/metadata
Offset 0x400+:      4-bit indexed pixel data (different layout than TEX)
                    ~43KB total (compressed or subset of sprites)
```

### Scripts Updated

- `scripts/fix_hair_highlight_4bit.py` - **UPDATED** with HEADER_SIZE = 0x800
- `scripts/tex_to_png.py` - Converts TEX to PNG for preview without game
- `scripts/create_index_test_tex.py` - Makes index 15 transparent for visualization

### Next Steps

1. [x] Investigate why armor colors don't apply with TEX file - **SEE PHASE 5**
2. [ ] Try adjusting Y threshold to catch remaining edge pixels
3. [ ] Apply fix to other jobs (Knight, Monk, etc.)
4. [ ] Integrate into build pipeline for automatic deployment

---

## Phase 5: Modloader Palette Limitation (2026-01-12)

### BLOCKED: Custom TEX Files Break Theme Palette

**Critical Discovery:** Deploying ANY custom TEX file via the modloader causes palette index misalignment. This is a fundamental limitation of how the modloader handles G2D files.

### Investigation Summary

Tested multiple approaches to fix armor colors when deploying custom TEX:

| Approach | Result |
|----------|--------|
| TEX only (hair fix) | Hair fixed ✓, Armor brown (original) ✗ |
| TEX + embedded palette in header | Hair fixed ✓, Armor brown ✗ |
| TEX + separate pal_992.bin file | Hair fixed ✓, Armor brown ✗ |
| TEX + tex_993 (both files) | Hair fixed ✓, Armor shows HAT color ✗ |
| Unmodified original TEX | Armor shows HAT color ✗ |
| No TEX (baseline) | Themes work ✓, Hair highlight broken ✓ |

### Root Cause Analysis

**Normal operation (no custom TEX):**
- Pixels: from built-in g2d.dat
- Palette: from SPR file (themed colors work ✓)

**With custom TEX deployed:**
- Pixels: from our TEX file (hair fix works ✓)
- Palette: from built-in g2d.dat (ignores SPR palette ✗)

The modloader processes custom G2D/TEX files differently than built-in ones. When a custom TEX is present, the game uses a hardcoded/built-in palette instead of reading from the themed SPR file.

### Key Evidence

1. **Even unmodified TEX causes wrong colors** - Deploying the exact same tex_992.bin from `original_squire` mod causes armor to show hat color
2. **Palette index shift observed** - Armor (indices 3,4,5) displays with hat colors (indices 8,9,10) = ~5 index offset
3. **Game loads both files** but doesn't merge them correctly:
   ```
   [G2D] Accessing file 992
   [G2D] Accessing file 993
   [FFTPack] Accessing modded file 140 -> unit/battle_mina_m_spr.bin
   ```

### Why Ramza Themes Work

Ramza themes use `RamzaColorTransformer` which **bakes actual RGB colors** into the TEX file. The colors are transformed and embedded, not dependent on external palette lookup.

For generic jobs, TEX files use indexed colors that reference an external palette. When custom TEX is deployed, this palette lookup gets broken.

### TEX vs SPR Index Distribution

Analysis shows TEX and SPR use different index distributions for the same visual elements:

```
Index  TEX      SPR      Meaning
--------------------------------------------------
    3       0      341   Armor outline (TEX doesn't use!)
    4       0      299   Armor shadow (TEX doesn't use!)
    5     474      318   Armor base
    8    1011      444   Hat outline
    9    1137      273   Hat shadow
   10    1362      449   Hat base
```

TEX uses indices 8,9,10 where SPR uses 3,4,5 for similar pixel counts, suggesting different index mappings between the two formats.

### Attempted Solutions (All Failed)

1. **Embed palette in TEX header** - Game ignores header data (all zeros expected)
2. **Deploy separate pal_992.bin** - Game doesn't load palette files for tex_992
3. **Deploy both tex_992 + tex_993** - Made colors worse (hat color on armor)
4. **SPR pixel modification** - Game ignores SPR pixel data for rendering

### Conclusion

**The hair highlight fix via TEX is NOT compatible with the theme system.**

The modloader/game has a fundamental issue where custom TEX files bypass the normal palette loading from SPR files. Without changes to the modloader itself, we cannot have both:
- Hair highlight fix (requires TEX modification)
- Themed armor colors (requires SPR palette)

### Options Going Forward

#### Option 1: Accept Limitation (Current State)
- Hair highlight remains linked to skin color
- Theme system works fully
- No visual artifacts

#### Option 2: Contact Modloader Author
- Ask Nenkai (modloader author) why custom TEX causes palette issues
- May be a bug that can be fixed in the modloader
- GitHub: https://github.com/Nenkai

#### Option 3: Generate Themed TEX Files
- Like Ramza themes, bake actual colors into TEX for each theme
- Would require generating ~18+ TEX files (one per theme)
- Complex implementation but would solve both issues
- Would need to transform colors using similar approach to `RamzaColorTransformer`

#### Option 4: Runtime Palette Manipulation
- Intercept palette loading at runtime
- Apply hair highlight fix dynamically
- Most complex, requires deep game hooking

### Files Created During Investigation

- `scripts/fix_hair_highlight_4bit.py` - TEX hair fix (working, but breaks themes)
- `scripts/fix_hair_highlight_spr.py` - SPR hair fix (doesn't affect rendering)
- `temp_tex_analysis/` - Various test files and analysis outputs

### Lessons Learned

1. **G2D and FFTPack are separate systems** - TEX provides pixels, SPR provides palette
2. **Custom TEX deployment changes palette behavior** - Modloader quirk
3. **Ramza themes work differently** - Color transformation, not palette swapping
4. **Index distributions differ between TEX and SPR** - Not 1:1 mapping
5. **Game loads file pairs** - 992+993, 834+835, etc. always together

---

## Phase 6: WORKING SOLUTION (2026-01-13)

### SUCCESS: Hair Highlight Fix WITH Custom Themes!

**The hair highlight fix is WORKING and theme colors are ALSO working!**

Previous attempts failed because we thought deploying custom TEX files would break the theme palette system. This was WRONG - deploying BOTH tex_992.bin AND tex_993.bin together allows themes to work correctly.

### Quick Start: Apply to Any Job

1. **Get TEX file numbers** from the mapping table below (e.g., Squire Female = 994, 995)
2. **Get TEX files** from toolkit output: `C:\Users\ptyRa\AppData\Local\FFTSpriteToolkit\working\.FFTSpriteToolkit\sprites_rgba\`
3. **Patch with script**: `python scripts/fix_hair_highlight_tex.py tex_994.bin tex_994.bin` (repeat for each file)
4. **Deploy BOTH files** to `ColorMod/FFTIVC/data/enhanced/system/ffto/g2d/`
5. **Copy to game folder**: `C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\Reloaded\Mods\FFTColorCustomizer\FFTIVC\data\enhanced\system\ffto\g2d\`
6. **Test in-game**

**Note**: You can also patch TEX files inline without the script - see Python code below.

---

### Technical Details

#### TEX File Format
```
Offset 0x000-0x7FF: Header (2048 bytes, all zeros)
Offset 0x800+:      4-bit indexed pixel data
                    High nibble = first pixel
                    Low nibble = second pixel
                    Sheet width: 512 pixels
                    Sprite height: 40 pixels
```

**Python patch script:**
```python
HEADER_SIZE = 0x800
SHEET_WIDTH = 512
SPRITE_HEIGHT = 40
HAIR_Y_THRESHOLD = 12  # Hair region is localY < 12

for num in ['992', '993']:
    tex_path = f'path/to/tex_{num}.bin'

    with open(tex_path, 'rb') as f:
        data = bytearray(f.read())

    for i in range(HEADER_SIZE, len(data)):
        byte_val = data[i]
        high = (byte_val >> 4) & 0x0F
        low = byte_val & 0x0F

        new_high, new_low = high, low
        pixel_offset = (i - HEADER_SIZE) * 2

        # High nibble
        if high == 15:
            y = pixel_offset // SHEET_WIDTH
            local_y = y % SPRITE_HEIGHT
            if local_y < HAIR_Y_THRESHOLD:
                new_high = 12

        # Low nibble
        if low == 15:
            y = (pixel_offset + 1) // SHEET_WIDTH
            local_y = y % SPRITE_HEIGHT
            if local_y < HAIR_Y_THRESHOLD:
                new_low = 12

        data[i] = (new_high << 4) | new_low

    with open(tex_path, 'wb') as f:
        f.write(data)
```

**CRITICAL**: Deploy BOTH TEX files in each pair together. The game loads them as pairs.

### Results

- ✅ Hair highlight no longer follows skin color
- ✅ Hair highlight follows BootsAndHair color instead
- ✅ Custom theme colors ARE working (not stuck on original)
- ⚠️ Minor issue: Some stray pixels (hands, gloves) showing skin color bleed

### Known Issue: Stray Pixels

The Y-threshold approach (localY < 12) catches some non-hair pixels that use index 15:
- Some pixels on hands/gloves in the top portion of sprites
- These show the BootsAndHair color instead of Skin color

**Potential fixes:**
1. Adjust Y threshold (try 10 or 11 instead of 12)
2. Use X+Y bounds to target only the head area more precisely
3. Manual pixel identification for edge cases

### Why This Works (Corrected Understanding)

Previous assumption was WRONG:
- ❌ "Custom TEX files break theme palette system"

Actual behavior:
- ✅ When BOTH tex_992 AND tex_993 are deployed, the game correctly uses SPR palette for theming
- ✅ The TEX files provide pixel indices, SPR files provide palette colors
- ✅ Both systems work together when properly deployed

### File Mapping (Generic Jobs)

| Job | TEX Files | SPR File |
|-----|-----------|----------|
| Squire Male | 992, 993 | battle_mina_m_spr.bin |
| Squire Female | 994, 995 | battle_mina_w_spr.bin |
| Chemist Male | 996, 997 | battle_item_m_spr.bin |
| Chemist Female | 998, 999 | battle_item_w_spr.bin |
| Knight Male | 1000, 1001 | battle_knight_m_spr.bin |
| Knight Female | 1002, 1003 | battle_knight_w_spr.bin |
| Archer Male | 1004, 1005 | battle_archer_m_spr.bin |
| Archer Female | 1006, 1007 | battle_archer_w_spr.bin |
| Monk Male | 1008, 1009 | battle_monk_m_spr.bin |
| Monk Female | 1010, 1011 | battle_monk_w_spr.bin |
| Priest Male | 1012, 1013 | battle_priest_m_spr.bin |
| Priest Female | 1014, 1015 | battle_priest_w_spr.bin |
| Black Mage Male | 1016, 1017 | battle_kuro_m_spr.bin |
| Black Mage Female | 1018, 1019 | battle_kuro_w_spr.bin |
| Time Mage Male | 1020, 1021 | battle_toki_m_spr.bin |
| Time Mage Female | 1022, 1023 | battle_toki_w_spr.bin |
| Summoner Male | 1024, 1025 | battle_sho_m_spr.bin |
| Summoner Female | 1026, 1027 | battle_sho_w_spr.bin |
| Thief Male | 1028, 1029 | battle_shi_m_spr.bin |

**Pattern**: TEX files come in pairs (N, N+1). Get source TEX files from the FFT Sprite Toolkit output folder.

### Next Steps

1. [ ] Fine-tune Y threshold to reduce stray pixel issue
2. [ ] Apply fix to other jobs (Knight, Monk, Archer, etc.)
3. [ ] Create automated pipeline to patch all generic job TEX files
4. [ ] Integrate into build process

---

## Phase 7: Config UI Preview Fix (2026-01-13)

### Problem: Preview Shows Old Highlight

After fixing the in-game hair highlight (Phase 6), the Config UI preview still showed the hair highlight using the skin color. This is because:

- **In-game rendering**: Uses TEX files (from g2d folder) for pixel indices
- **Config UI preview**: Uses SPR files (from fftpack/unit folder) via `BinSpriteExtractor.cs`

The TEX fix doesn't affect the preview because `BinSpriteExtractor` reads pixel data directly from SPR files.

### Solution: Patch SPR Files Too

Created `scripts/fix_hair_highlight_spr.py` to apply the same index 15 → 12 remapping to SPR files.

**SPR File Format (different from TEX!):**
```
Offset 0x000-0x1FF: Palette data (512 bytes, 16 palettes × 32 bytes)
Offset 0x200+:      4-bit indexed pixel data
                    Low nibble = first pixel (opposite of TEX!)
                    High nibble = second pixel
                    Sheet width: 256 pixels (vs 512 for TEX)
                    Sprite height: 40 pixels
```

**Key Difference from TEX:**
- TEX: High nibble = first pixel, low nibble = second pixel
- SPR: Low nibble = first pixel, high nibble = second pixel

### Usage

```bash
# Fix single SPR file
python scripts/fix_hair_highlight_spr.py single battle_mina_m_spr.bin

# Fix all theme variants of a sprite (recommended)
python scripts/fix_hair_highlight_spr.py all ColorMod/FFTIVC battle_mina_m_spr.bin
```

### Results (Squire Male)

Patched 21 theme variants (all themes in sprites_* folders):
```
sprites_amethyst: 306 pixels remapped, 221 kept
sprites_blood_moon: 306 pixels remapped, 221 kept
... (all 21 variants)
Total: 6,426 pixels remapped
```

### Complete Fix Pipeline

To fully fix hair highlight for any job:

1. **Fix TEX files** (for in-game):
   ```bash
   python scripts/fix_hair_highlight_tex.py tex_992.bin tex_992.bin
   python scripts/fix_hair_highlight_tex.py tex_993.bin tex_993.bin
   # Copy to ColorMod/FFTIVC/data/enhanced/system/ffto/g2d/
   ```

2. **Fix SPR files** (for Config UI preview):
   ```bash
   python scripts/fix_hair_highlight_spr.py all ColorMod/FFTIVC battle_mina_m_spr.bin
   python scripts/fix_hair_highlight_spr.py all Publish/Release/FFTIVC battle_mina_m_spr.bin
   ```

3. **Deploy**:
   ```bash
   powershell.exe -ExecutionPolicy Bypass -File ./BuildLinked.ps1
   ```

### Files Created

- `scripts/fix_hair_highlight_spr.py` - SPR file patching script (supports single file or all themes)

---

## Phase 8: Knight Female Fix - Advanced Techniques (2026-01-16)

### Problem: Simple Y-Threshold Not Sufficient

The Knight Female job required a more sophisticated approach than the simple `localY < 12` threshold used for Squire Male. Initial attempts with neighbor-based detection alone failed because:

1. **Hair highlight pixels have more skin neighbors than hair neighbors** - They're at the edge of the face region
2. **The highlight is visible during walking animation** - Affects tex_1002 (standing poses), not tex_1003 (animations) as initially assumed
3. **X-position thresholds didn't help** - Highlight pixels were spread across all local X values

### Key Discovery: Animation Uses First TEX File

When testing Knight Female walking animation:
- **tex_1002.bin** = Used for BOTH standing AND walking animations
- **tex_1003.bin** = Used for other animation frames (attacks, etc.)

Initial testing on tex_1003 showed "no change" because the walking animation actually uses tex_1002.

### Working Solution: Hybrid Approach

After extensive testing, the following algorithm successfully fixed Knight Female:

```python
# Two-tier approach:
# Tier 1: min 3 hair neighbors - always convert (normal rule)
# Tier 2: For localY <= 12 (top of sprite/head area) - convert if ANY hair neighbor

for y in range(height):
    for x in range(SHEET_WIDTH):
        idx = get_pixel_index(data, x, y)
        if idx not in REMAP:
            continue

        hair_n, skin_n = count_neighbors(data, x, y)
        local_y = y % SPRITE_HEIGHT

        # Tier 1: 3+ hair neighbors - always convert
        if hair_n >= 3:
            candidates.append((x, y, idx, REMAP[idx]))
        # Tier 2: Top of sprite (head area) - convert if any hair neighbor
        elif local_y <= 12 and hair_n >= 1:
            candidates.append((x, y, idx, REMAP[idx]))
```

**Result:** 3954 pixels remapped, hair highlight fixed, face preserved.

### Approaches Tested (Knight Female)

| Approach | Pixels | Result |
|----------|--------|--------|
| min 3 hair neighbors, hair >= skin | 3227 | Face OK, highlight visible |
| min 2 hair neighbors, hair >= skin | 3284 | No change |
| min 1 hair neighbors, hair >= skin | 3299 | No change |
| Strict min 4 hair neighbors | 3080 | No change |
| Strict min 3 hair neighbors | 3723 | Highlight cut in half |
| Strict min 2 hair neighbors | 4136 | Highlight gone, face affected |
| Two-tier (min 3 + min 2 with hair > skin) | 3755 | Highlight back |
| Two-tier (min 3 + min 2 with hair >= skin) | 3780 | Highlight back |
| Convert all skin for localX >= 26 | 4038 | More face red, highlight there |
| Convert all skin for localX <= 5 | 3931 | Less face red, highlight there |
| **min 3 + localY <= 12 with min 1** | **3954** | **SUCCESS** |

### Script Variations Created

Multiple fix scripts with different algorithms:

| Script | Algorithm | Use Case |
|--------|-----------|----------|
| `fix_hair_no_spread.py` | No spreading, configurable min neighbors + hair >= skin | Conservative approach |
| `fix_hair_any_neighbor.py` | Any hair neighbor triggers conversion, multiple passes | Most aggressive |
| `fix_hair_equal_neighbors.py` | hair >= skin with spreading | Aggressive with spreading |
| `fix_hair_by_neighbor_ratio.py` | hair > skin with spreading | Moderate with spreading |
| `fix_hair_highlight_strict.py` | Min neighbors only, no skin ratio check | Variable aggression |

### Key Learnings

1. **Different jobs need different approaches** - Squire Male worked with simple Y-threshold, Knight Female needed hybrid
2. **Test on the correct TEX file** - Walking animation may use the "standing" TEX file
3. **Verify deployed files** - Check that the game folder has the modified files with 0 remaining skin pixels
4. **Hair pixels can have more skin than hair neighbors** - Pure neighbor ratio doesn't work for edge cases
5. **Combining Y-threshold with neighbor detection works best** - Top of sprite (localY <= 12) + any hair neighbor

### Jobs Status

| Job | TEX Files | Status | Fix Used |
|-----|-----------|--------|----------|
| Squire Male | 992, 993 | ✅ Done | Simple Y < 12 |
| Squire Female | 994, 995 | ✅ Done | Simple Y < 12 |
| Chemist Male | 996, 997 | ✅ Done | Simple Y < 12 |
| Chemist Female | 998, 999 | ✅ Done | Simple Y < 12 |
| **Knight Female** | **1002, 1003** | **✅ Done** | **Hybrid (min 3 + localY <= 12)** |
| Knight Male | 1000, 1001 | ❌ Not Done | - |
| Archer Female | 1006, 1007 | ❌ Not Done | - |
| Monk Female | 1010, 1011 | ❌ Not Done | - |
| Summoner Female | 1026, 1027 | ❌ Not Done | - |
| Mystic Female | 1038, 1039 | ❌ Not Done | - |
| Ninja Female | 1054, 1055 | ❌ Not Done | - |
| Bard Male | 1060, 1061 | ❌ Not Done | - |

### Vanilla TEX File Location

Original unmodified TEX files can be found at:
```
C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\Reloaded\Mods\original_squire_v2
```

### Recommended Fix Algorithm for Remaining Jobs

Start with the Knight Female hybrid approach:

```python
# Tier 1: 3+ hair neighbors - always convert
if hair_n >= 3:
    convert()
# Tier 2: Top of sprite (head area) - convert if any hair neighbor
elif local_y <= 12 and hair_n >= 1:
    convert()
```

Adjust parameters as needed:
- If highlight still visible: Lower localY threshold or use min 2 in Tier 1
- If face affected: Raise localY threshold or add skin neighbor check
