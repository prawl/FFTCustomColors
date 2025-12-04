# FFT Color Mod - TODO List
<!-- KEEP UNDER 100 LINES TOTAL -->

## ⚡ NEW APPROACH: File-Based Color Swapping (Proven Method)

**Goal**: v0.1 release using file replacement approach
**Status**: 89 tests passing ✅ - Core pipeline complete!

### WHY FILE-BASED:
- WotL Characters mod proves it works
- No reverse engineering needed
- Can build TODAY with existing code

## 📋 IMPLEMENTATION STATUS

### ✅ Phase 1: Core Components (COMPLETE)
- [x] **SpriteColorGenerator tool** - Batch processes sprites
- [x] **SpriteProcessingPipeline** - Full color swapping pipeline
- [x] **FileRedirector** - With GetRedirectedPath for color variants
- [x] **ModLoaderIntegration** - Hotkeys (F1,F2,F4,F7,F8,F9) working
- [x] **ColorScheme enum** - Blue/Red/Green/Purple/Original
- [x] **ProcessDirectory method** - Bulk sprite processing
- [x] **Test coverage** - 81 tests passing, test script fixed

### 🚧 Phase 2: Sprite Processing (IN PROGRESS)
- [x] **PacExtractor class** - TDD implementation started
  - Can open PAC files and validate paths
  - Methods for GetFileName, GetFileSize, ExtractFile
  - 89 tests passing with full TDD approach
- [ ] **Extract sprite files from FFT**
  - Location: Steam\...\FINAL FANTASY TACTICS\pack\*.pac files
  - Found PAC files in enhanced directory (0000.pac - 1GB+)
  - Target: .SPR files with embedded palettes
  - Need to implement actual PAC file reading

- [ ] **Generate all color variants**
  - Use SpriteProcessingPipeline on extracted sprites
  - ~500 sprites × 5 colors = 2500 files
  - Save to FFTIVC/data/sprites_[color]/

### ⏳ Phase 3: Mod Integration
- [x] **fftivc.utility.modloader dependency** - Already in ModConfig.json
- [ ] **Hook file redirection** - Connect FileRedirector to modloader
- [ ] **Implement F1-F5 hotkeys** - Switch active color scheme
- [ ] **Test hotkey switching** - Verify instant color swaps

### 📦 Phase 4: Polish & Release
- [ ] **Create installer** with pre-generated sprites
- [ ] **Documentation** (installation, hotkeys)
- [ ] **Test all 4 chapter outfits**

## 🔧 Technical Stack

### Working Components:
- **PaletteDetector.cs** - Detects & replaces colors (34 tests ✅)
- **SpriteColorGenerator.cs** - Batch-processes sprites
- **SpriteProcessingPipeline.cs** - Full pipeline with color swapping
- **FileRedirector.cs** - Color scheme management
- **ModLoaderIntegration.cs** - Hotkey handling and file redirection
- **PacExtractor.cs** - PAC file extraction (TDD implementation)
- **Test Scripts** - Reliable run_tests.sh/.ps1

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

### Success Criteria:
✅ F1-F5 instantly change ALL sprite colors
✅ Works in battles, cutscenes, menus
✅ Compatible with other mods

---
**See PLANNING.md for technical details and research**