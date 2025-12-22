# TEX File Format Research

## Overview
This document tracks our investigation into FFT's tex file format, specifically tex_830-835.bin files used for Ramza's character textures and colors.

## Key Files
- **tex_830.bin** - 131,072 bytes (128 KB)
- **tex_831.bin** - 118,784 bytes (116 KB)
- **tex_832.bin** - 131,072 bytes (128 KB)
- **tex_833.bin** - 118,784 bytes (116 KB)
- **tex_834.bin** - 131,072 bytes (128 KB)
- **tex_835.bin** - 118,784 bytes (116 KB)

## Initial Observations

### File Structure Pattern
- Files alternate between two sizes: 131,072 and 118,784 bytes
- Even-numbered files (830, 832, 834) are larger
- Odd-numbered files (831, 833, 835) are smaller

### Header Analysis
From hex dump of tex_830.bin:
- Bytes 0x0000-0x1370: Mostly zeros (header/metadata section?)
- Actual data starts around offset 0x1370

## Known Offsets (from RamzaTexGenerator)

### Hair Color
- **Offset 0x0E50-0x0E51**: Hair color (16-bit BGR555)
- Original brown: `0x4848` (RGB: 72, 64, 16)
- White modification changes this value

### Color Format
- 16-bit BGR555 format
- Bits 0-4: Blue (5 bits)
- Bits 5-9: Green (5 bits)
- Bits 10-14: Red (5 bits)
- Bit 15: Usually transparency/alpha (often 0)

## Sprite File Relationship

### battle_ramuza_spr.bin Structure
- Standard FFT sprite format
- First 512 bytes: 16 color palettes (16 colors × 16 palettes × 2 bytes)
- Remaining: 4-bit indexed pixel data

### How Tex Files Override Sprites
- Tex files appear to override palette/texture data at runtime
- Game loads sprite first, then applies tex modifications
- Multiple tex files (830-835) may handle different aspects:
  - Different equipment states?
  - Different animation frames?
  - Different body parts?

## Investigation Steps

### Step 1: Identify Color Regions
- [ ] Map all non-zero data regions in tex files
- [ ] Compare white_heretic vs original tex files
- [ ] Identify which offsets change between themes

### Step 2: Test Color Modifications
- [ ] Create test tex with known color changes
- [ ] Document which visual elements change
- [ ] Map offset ranges to visual components

### Step 3: Understand Multiple Tex Files
- [ ] Why 6 files for one character?
- [ ] What does each file control?
- [ ] How do they work together?

## Hypotheses to Test

### Hypothesis 1: Direct Palette Override
Tex files contain replacement palette data that directly overrides sprite palettes.

### Hypothesis 2: Texture Overlay
Tex files contain texture data that's overlaid on the base sprite.

### Hypothesis 3: Color Transformation Matrix
Tex files contain color transformation data (hue/saturation/brightness adjustments).

## Next Steps
1. Compare byte-by-byte differences between original and white_heretic tex files
2. Identify all color value locations
3. Test modifications at different offsets to see visual changes
4. Create mapping table of offsets to visual elements

---

## Research Log

### Session 1: Initial Investigation (Current)
- Identified tex file sizes and patterns
- Found hair color offset (0x0E50)
- Established 16-bit BGR555 color format
- Created initial hypotheses about file structure