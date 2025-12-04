# FFT Color Mod - TODO List
<!-- KEEP UNDER 100 LINES TOTAL -->

## ⚡ NEW APPROACH: File-Based Color Swapping (Proven Method)

**Goal**: v0.1 release using file replacement approach
**Status**: PaletteDetector working (34 tests passing ✅)

### WHY FILE-BASED:
- WotL Characters mod proves it works
- No reverse engineering needed
- Can build TODAY with existing code

## 📋 IMPLEMENTATION TASKS

### Phase 1: Extract & Generate Sprites
- [ ] **Extract sprite files from FFT**
  - Location: Steam\...\FINAL FANTASY TACTICS\pack\*.pac files
  - Target: .SPR files with embedded palettes

- [ ] **Create SpriteColorGenerator tool**
  - Read all .SPR files from input directory
  - Use PaletteDetector.DetectChapterOutfit()
  - Generate 5 variants using ReplacePaletteColors()
  - Save to FFTIVC/data/sprites_[color]/

- [ ] **Generate all color variants**
  - Blue (F1), Red (F2), Green (F3), Purple (F4), Original (F5)
  - ~500 sprites × 5 colors = 2500 files

### Phase 2: Mod Integration
- [ ] **Add fftivc.utility.modloader dependency**
  ```json
  "ModDependencies": ["fftivc.utility.modloader", "reloaded.sharedlib.hooks"]
  ```

- [ ] **Implement file redirection logic**
  - Handle F1-F5 hotkeys
  - Switch active sprite folder
  - Use modloader's file redirection

- [ ] **Test hotkey switching**
  - F1-F5 instantly swap colors
  - Works without battle restart

### Phase 3: Polish & Release
- [ ] **Create installer** with pre-generated sprites
- [ ] **Documentation** (installation, hotkeys)
- [ ] **Test all 4 chapter outfits**

## 🔧 Technical Details

### File Structure:
```
FFT_Color_Mod/
├── FFTIVC/data/
│   ├── sprites_blue/
│   ├── sprites_red/
│   ├── sprites_green/
│   ├── sprites_purple/
│   └── sprites_original/
├── FFTColorMod.dll
└── ModConfig.json
```

### Key Components:
- **PaletteDetector.cs** - Detects & replaces colors (working!)
- **SpriteColorGenerator.cs** - Batch-processes sprites
- **Mod.cs** - Hotkeys & file redirection
- **fftivc.utility.modloader** - File interception

### Success Criteria:
✅ F1-F5 instantly change ALL sprite colors
✅ Works in battles, cutscenes, menus
✅ Compatible with other mods

---
**See PLANNING.md for technical details and research**