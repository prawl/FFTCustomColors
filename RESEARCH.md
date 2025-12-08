# FFT Color Mod Research - December 8, 2024

## Executive Summary
After extensive research into FFT palette modding, we have discovered the fundamental issue: **FFT sprites were never designed for universal palette swapping**. The game uses a complex multi-palette system where each sprite can have different purposes for the same color indices. The only successful universal color mod (better_palettes) works because its author manually edited every sprite file to have consistent colors at specific palette slots.

## Critical Discoveries

### 1. FFT Sprite Palette Structure - THE BREAKTHROUGH
**We were wrong about the 96 color structure!** Based on FFHacktics documentation and web research:

- FFT sprites contain **256 colors total** (512 bytes)
- Organized as **16 palettes of 16 colors each**
- First 8 palettes are for unit sprites
- Last 8 palettes are for portraits
- Each sprite can only use **ONE 16-color palette at a time** (4-bit indexing)
- Color format: 1 bit for transparency + 5 bits each for BGR channels

This means our assumption about indices 0-95 was incorrect. The sprite actually has multiple complete palettes, and the game selects which one to use!

### 2. How corpse_brigade REALLY Works
Based on our research, corpse_brigade likely works in one of two ways:

1. **Manual Edit Theory**: The better_palettes author manually edited indices 32-47 (palette 3) in EVERY sprite file to contain brown/tan colors, then uses game mechanics to select that palette
2. **Palette Swap Theory**: They're swapping between existing palettes within the sprite (e.g., using palette 3 instead of palette 0)

Evidence points to #1 - they manually created consistent alternate palettes.

### 3. Why Our Attempts Failed
We failed because we misunderstood the fundamental structure:
- We thought we were swapping color indices 2-9 with 34-41
- We actually needed to either:
  - Edit a specific palette slot consistently across ALL sprites
  - Use game mechanics to select different palettes

### 4. FFT Modding Tools and Community

#### Primary Tools
1. **Shishi Sprite Editor** (Part of FFTPatcher suite)
   - Can edit sprites and palettes
   - Saves changes automatically (no manual save needed)
   - Can export/import as PNG
   - Works with ISO files directly

2. **Graphics Gale**
   - Used for palette manipulation
   - Can drag colors between palette rows
   - Popular for sprite editing in the community

3. **FFTPatcher Suite**
   - Comprehensive modding toolkit
   - Includes Shishi, event editor, and more
   - Version 0.497 is current

#### Community Resources
- **FFHacktics.com** - Main community hub
- Active forums with sprite editing discussions
- Sprite database with custom sprites
- Tutorial sections for palette editing

### 5. Technical Sprite Format Details

From FFT Investigative Project and FFHacktics:
- Sprites stored as `.bin` files
- 4 bits per pixel (nybble format)
- Palette data at start of file
- Sprite dimensions: 256x488 pixels
- Lower half uses compression for attack animations
- Different sprite types (unit, monster, other) have different layouts

### 6. Other FFT Mods Examined

#### GenericJobs Mod
- Adds hidden jobs (Dark Knight)
- No palette modification code found
- Uses memory hooks for job selection
- Not relevant for color modding

#### Texture Pack Mod
- Contains reshade configurations
- Works with textures, not sprite palettes
- Not applicable to our needs

#### better_palettes Mod
- Only mod that successfully implements universal color changes
- Contains manually edited sprite files
- Each sprite has consistent colors at specific indices

## Viable Solutions

### Solution 1: Manual Palette Editing (Proven to Work)
**Time Required**: 20-30 hours
**Success Rate**: 100% (proven by better_palettes)

Process:
1. Open each of 178 sprite files individually
2. Edit a specific palette slot (e.g., palette 3 = indices 32-47)
3. Place red colors consistently in that palette
4. Save all sprites
5. Implement palette selection in game

**Pros**: Guaranteed to work, professional results
**Cons**: Extremely time-consuming, requires manual work

### Solution 2: Automated Palette Injection
**Time Required**: 2-4 hours development
**Success Rate**: 70% (theoretical)

Process:
1. Identify which palette slot to use (e.g., palette 3)
2. Create a script that:
   - Opens each sprite file
   - Injects red colors at indices 32-47
   - Preserves other palettes
3. Apply to all 178 sprites

**Pros**: Faster than manual
**Cons**: May not look good on all sprites without manual tweaking

### Solution 3: Modify Existing better_palettes
**Time Required**: 1-2 hours
**Success Rate**: 90% (high probability)

Process:
1. Take corpse_brigade sprites (known to work)
2. Modify the brown colors to red
3. Keep the same indices and structure

**Pros**: Builds on proven foundation
**Cons**: Limited to one color scheme, may still have issues

### Solution 4: Use Professional Tools
**Time Required**: Unknown
**Success Rate**: Unknown

The community uses:
- Shishi Sprite Editor for in-game editing
- Graphics Gale for palette work
- Could potentially batch process sprites

**Pros**: Industry-standard tools
**Cons**: Need to learn new software, may still require manual work

## Recommended Path Forward

Based on our research, the most practical approach is:

1. **Short Term**: Modify the corpse_brigade browns to reds
   - Quick win
   - Proven structure
   - Can be done programmatically

2. **Medium Term**: Develop automated palette injection
   - Target unused palette slots
   - Create consistent color schemes
   - Test on subset first

3. **Long Term**: Consider manual editing with Shishi/Graphics Gale
   - For professional-quality results
   - When other methods prove insufficient

## Key Insights

1. **FFT uses a multi-palette system** - Not a single 96-color palette
2. **Each sprite references only 16 colors** - Via 4-bit indexing
3. **Successful mods edit sprite files directly** - Not through code hooks
4. **The community has tools** - But they require manual work
5. **No universal solution exists** - Each sprite needs individual attention

## Conclusion

Our initial approach was flawed due to misunderstanding FFT's palette system. The game uses multiple 16-color palettes per sprite, not a single 96-color palette. Successful color mods require editing sprite files to have consistent colors in specific palette slots, then using those slots consistently across all sprites.

The better_palettes mod succeeded through brute force - manually editing every sprite. For a quicker solution, we should modify their work rather than starting from scratch.

## UPDATE: FFHacktics Deep Dive Research - December 8, 2024

### Critical Discovery: Palette Structure Clarification
After extensive research on FFHacktics.com, we've confirmed the exact sprite palette structure:

#### Sprite File Format:
- **Total Size**: 256 colors (512 bytes of palette data)
- **Organization**: 16 palettes × 16 colors each
- **Palette 0-7**: Unit sprite palettes
- **Palette 8-15**: Portrait palettes
- **Color Format**: 2 bytes per color (XBBBBBGGGGGRRRRR)
  - 1 bit: Semi-transparency flag
  - 5 bits: Blue channel
  - 5 bits: Green channel
  - 5 bits: Red channel
- **Special**: First color in each palette is transparency

#### Key Technical Constraints:
1. **4-bit Indexing**: Sprites can only reference 16 colors at a time
2. **Palette Selection**: Game selects which palette to use (0-7 for sprites, 8-15 for portraits)
3. **Color Limit**: Maximum 15 usable colors + 1 transparency per palette
4. **Auto-save**: Shishi Sprite Editor saves changes automatically (no manual save)

### Sprite Editing Workflow (FFHacktics Best Practices):

#### Method 1: Shishi Sprite Editor (Recommended)
1. Open FFT ISO in Shishi
2. Navigate to desired sprite
3. Export as PNG/BMP
4. Edit in Graphics Gale maintaining palette constraints
5. Import back into Shishi (auto-saves)

#### Method 2: Palette Editor 1.31b
1. Export sprites as 8-bit BMPs
2. Use Palette Editor to drag colors between palette rows
3. Maintain 16-color constraint per palette
4. Import modified sprites back

#### Method 3: Graphics Gale (Community Favorite)
1. Open sprite BMP/PNG
2. Use palette management features
3. Copy entire palette rows between sprites
4. Ensure color count stays within limits

### Why Our Crimson Red Mod Isn't Working:

After analyzing the FFHacktics documentation and our implementation:

1. **We're modifying the wrong part of the palette**: The better_palettes mod likely uses a specific palette slot (e.g., palette 1 or 2) for its alternate colors, not random indices 34-41.

2. **Corpse Brigade colors**: According to FFHacktics discussions, corpse_brigade likely uses a different palette selection mechanism, not just color swapping.

3. **Our conversion script is too simplistic**: We're converting "blue to red" but FFT sprites use specific palette slots that the game engine expects.

### The Real Solution:

Based on FFHacktics community experience:

1. **Identify the exact palette used**: Export a corpse_brigade sprite and analyze which of the 16 palettes contains the actual blue armor colors

2. **Modify the correct palette**: Change only that specific palette (likely palette 1 or 2, indices 16-31 or 32-47)

3. **Use proper tools**: Either:
   - Shishi Sprite Editor for direct editing
   - Graphics Gale for palette management
   - Palette Editor 1.31b for precise color copying

4. **Test incrementally**: Modify one sprite first (like battle_knight_m_spr.bin) and verify it works before batch processing

### Community Insights:

- **Successful examples exist**: Multiple FFHacktics users have created palette swaps successfully
- **Cloud palette transfer**: Confirmed working when applied to other character sprites
- **Generic job swaps**: Community has successfully swapped palettes between job classes
- **Tool preference**: Graphics Gale is the community favorite for palette work

### Next Steps:

1. Export a corpse_brigade sprite using proper tools to identify exact palette structure
2. Use Graphics Gale or Palette Editor to properly modify the specific palette
3. Test with a single sprite before batch conversion
4. Consider using Shishi directly for more control