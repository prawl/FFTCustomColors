# FFT Color Mod - Technical Research & Current State
<!-- KEEP UNDER 100 LINES -->

## 🎯 CURRENT STATUS (Dec 4, 2024)
**Working Implementation**: File-based color swapping with hotkeys (F1-F5)
- ✅ 124 tests passing with streaming PAC extraction
- ✅ StartEx bug FIXED - mod initializes properly (151ms load time)
- ✅ Successfully extracted and processed sprites from PAC files
- ✅ Generated color variant .diff.pac files for all color schemes
- ✅ Hotkey switching functional for generic job sprites
- ❌ **RAMZA CANNOT BE MODIFIED VIA FILE REPLACEMENT** (see critical discovery below)

## 🚨 CRITICAL DISCOVERY: RAMZA MODIFICATION BLOCKED

**Confirmed by Better Palettes mod author (Dec 4, 2024):**
- "unfortunately swapping ramza doesn't work because of deluxe/preorder"
- "preorder/deluxe seems to override it"
- Our corrupted Ramza sprites (22 bytes) didn't crash or affect the game
- Better Palettes has NO Ramza files - only modifies generic job sprites

**Implications:**
- Deluxe/Preorder DLC overrides base game Ramza sprites
- File replacement approach CANNOT modify Ramza
- Must use memory hooking to intercept palette loading
- Generic job sprites CAN be modified (confirmed working)

## 🚨 KEY DISCOVERIES

### 1. Sprite Palette Format (CRITICAL)
**Palette is in first 288 bytes of sprite files!**
- FFT sprite files store color palette data in bytes 0-287
- Sprite shape/pixel data stored after byte 288
- Proof: Copying first 288 bytes transfers colors between sprites
```bash
# Copy palette from source to target
dd if=source.bin of=target.bin bs=1 count=288 conv=notrunc
```

### 2. File Format & Location
- **Format**: `.bin` files (NOT .spr!)
- **Location**: `FFTIVC/data/enhanced/fftpack/unit/`
- **Naming**: `battle_[type]_spr.bin` (e.g., battle_ramza_spr.bin)
- **Extracted from**: 0002.pac and 0003.pac files

### 3. Implementation Approaches

#### Option A: File Replacement (IMPLEMENTED ✅)
**Current working solution**:
1. Extract sprites using FF16Tools/PacExtractor
2. Generate color variants using PaletteDetector
3. Create .diff.pac files for each color scheme
4. Hotkeys (F1-F5) trigger file path redirection
5. Game loads pre-modified sprites

**Status**: Working for battle_10m_spr.bin sprites

#### Option B: Memory Hooking (Future Enhancement)
Hook sprite loading functions to modify palettes during load:
```csharp
private nint LoadSpriteHook(nint spriteData, int size) {
    var result = _loadSpriteHook.OriginalFunction(spriteData, size);
    // Modify first 288 bytes only!
    if (_paletteDetector.DetectChapterOutfit(spriteData)) {
        _paletteDetector.ReplacePaletteColors(spriteData, _currentScheme, 288);
    }
    return result;
}
```

### 4. Why Direct Memory Modification Failed
**Root Cause**: FFT continuously reloads palettes from source files
- Memory modifications get overwritten immediately
- Need to intercept at load time OR replace source files
- File replacement approach avoids this issue entirely

### 5. Sprite Deployment (IMPORTANT)
**No PAC packing required when using Reloaded-II!** Unpacked sprites work directly:
- Place modified sprites in: `FFTIVC/data/enhanced/fftpack/unit/`
- Deploy with `.\BuildLinked.ps1` to copy to Reloaded-II mods folder
- Reloaded-II handles file redirection automatically

### 6. How to Apply Color to FFT Sprites (CONFIRMED WORKING)

**Critical Discovery**: The palette is stored in the first 288 bytes of the sprite file!

#### Working Method (Binary Palette Replacement):
```bash
# Extract palette from a working colored sprite (e.g., red knight from GenericJobs)
dd if="FFTIVC/data/enhanced/fftpack/unit/sprites_red/battle_knight_m_spr.bin" of="palette.bin" bs=1 count=288

# Apply this palette to target sprites
dd if="palette.bin" of="target_sprite.bin" bs=1 count=288 conv=notrunc
```

#### Important Notes:
- Palette is BGR format (Blue-Green-Red), not RGB
- First 288 bytes = 96 color entries × 3 bytes per color
- Direct byte replacement works better than algorithmic color shifting
- Use palettes from GenericJobs sprites (they have proper color variants)

#### DISCOVERED: "White/Silver" Effect
- Applying red knight palette to chemist sprites creates a beautiful white/silver appearance!
- This is an unintended cross-class palette swap that produces stunning results
- The red palette from knight (`battle_knight_m_spr.bin`) when applied to chemist sprites produces white/silver coloring
- This suggests different sprite types interpret the same palette data differently

#### Deployment Process for Binary Search:

**CRITICAL: Deploy unpacked sprites, NOT PAC files!**

1. **Extract correct red palette from GenericJobs:**
```bash
dd if="FFTIVC/data/enhanced/fftpack/unit/sprites_red/battle_knight_m_spr.bin" of="correct_red_palette.bin" bs=1 count=288 2>/dev/null
```

2. **Copy ONLY sprite files (exclude seq/shp/sp2 files that cause crashes):**
```bash
# Clean directory first
rm -rf FFTIVC/data/enhanced/fftpack/unit
mkdir -p FFTIVC/data/enhanced/fftpack/unit

# Copy only _spr.bin files and apply palette to desired subset
counter=0
for sprite in input_sprites/*_spr.bin; do
    filename=$(basename "$sprite")
    cp "$sprite" "FFTIVC/data/enhanced/fftpack/unit/$filename"
    counter=$((counter + 1))
    # Example: Apply red palette to second half (sprites 70-138)
    if [ $counter -gt 69 ]; then
        dd if="correct_red_palette.bin" of="FFTIVC/data/enhanced/fftpack/unit/$filename" bs=1 count=288 conv=notrunc 2>/dev/null
    fi
done
```

3. **Deploy with BuildLinked.ps1:**
```powershell
powershell -ExecutionPolicy Bypass -File BuildLinked.ps1
```

**Key Lessons Learned:**
- DO NOT create PAC files for deployment - Reloaded-II works with unpacked sprites
- MUST exclude non-sprite files (seq/shp/sp2) - these cause game crashes
- Use GenericJobs red knight palette - creates the white/silver effect
- BuildLinked.ps1 copies entire FFTIVC directory to Reloaded-II mods folder

## 📊 TECHNICAL IMPLEMENTATION

### Working Components
- **PacExtractor**: Streaming extraction of >2GB PAC files
- **PaletteDetector**: Finds and replaces colors in first 288 bytes
- **SpriteColorGenerator**: Batch processes sprites with variants
- **FileRedirector**: Manages hotkey → color scheme mapping
- **Startup.cs**: Proper StartEx pattern for Reloaded-II

### Sprite Types Discovered
- `10m/10w` - Type 10 male/female (possibly Squires)
- `20m/20w` - Type 20 male/female (possibly Knights)
- `40m/40w` - Type 40 male/female (other job class)
- `60m/60w` - Type 60 male/female (other job class)
- **Ramza**: Unknown - needs identification through gameplay

### File Structure
```
FFTIVC/data/enhanced/
├── 0002.blue.diff.pac    # Color variant PAC files
├── 0002.red.diff.pac
├── 0002.green.diff.pac
├── 0002.purple.diff.pac
└── fftpack/unit/sprites_[color]/
    └── battle_10m_spr.bin  # Individual sprite variants
```

## 🔄 NEXT STEPS

### Immediate: Scale Up Option A
1. **Process ALL sprites** in unit folder (not just 10m)
2. **Identify Ramza** through gameplay testing
3. **Optimize PaletteDetector** to focus on first 288 bytes only

### Future: Add Memory Hooking (Option B)
1. Find sprite loading signatures with x64dbg
2. Hook functions at startup using IStartupScanner
3. Apply palette changes during load (first 288 bytes)
4. Combine with file approach for maximum flexibility

## 📝 IMPORTANT NOTES
- Current approach (Option A) is WORKING and deployed
- Memory hooking would add real-time flexibility but isn't required
- Focus on identifying which sprite is Ramza's for targeted testing
- Consider optimizing to only process first 288 bytes for performance