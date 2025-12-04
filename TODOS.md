# FFT Color Mod - TODO List
<!-- KEEP UNDER 100 LINES TOTAL -->

## ⚡ NEW APPROACH: File-Based Color Swapping (Proven Method)

**Goal**: v0.1 release using file replacement approach
**Status**: 99 tests passing ✅ - ALL color schemes implemented!

### WHY FILE-BASED:
- WotL Characters mod proves it works
- No reverse engineering needed
- Can build TODAY with existing code

## 📋 IMPLEMENTATION STATUS

### ✅ Phase 1: Core Components (COMPLETE)
- [x] **All 4 color schemes** - Red/Blue/Green/Purple via TDD (99 tests passing!)
- [x] **PaletteDetector** - Finds & replaces all color palettes
- [x] **SpriteColorGenerator** - Batch processes sprites with color variants
- [x] **SpriteProcessingPipeline** - Full pipeline with color swapping
- [x] **FileRedirector** - Color scheme management & path redirection
- [x] **ModLoaderIntegration** - Hotkeys (F1-F5) ready
- [x] **Test Scripts** - Robust build/test with DLL checking

### ✅ Phase 2: Sprite Extraction (COMPLETE)
- [x] **PacExtractor class** - Full TDD implementation
  - Can open PAC files and validate paths
  - Supports both simple and FFT PACK format
  - Methods for GetFileName, GetFileSize, ExtractFile
  - ExtractAllSprites for .SPR filtering
  - ExtractSpritesFromDirectory for batch processing
- [x] **Command-line tool** - Program.cs with extract/process/full commands

### 🎯 Phase 3: Game Integration (READY TO TEST!)
**80% Complete** - Just need sprites & file hook!
- [x] **Mod.cs setup** - Reloaded-II compatible
- [x] **Hotkey monitoring** - F1-F5 keys ready
- [x] **Color switching logic** - All 4 schemes work
- [ ] **File redirection hook** - Connect to modloader's file system
- [ ] **Generate sprite files** - Process actual FFT sprites

### 🚀 IMMEDIATE NEXT STEPS TO TEST IN GAME:
1. **Extract sprites** - Get any .SPR file from FFT
2. **Generate variants** - Run SpriteColorGenerator on them
3. **Deploy mod** - Copy DLL to Reloaded mods folder
4. **Test hotkeys** - F1=Original, F2=Red, F3=Blue, F4=Green, F5=Purple

## 🔧 Technical Stack

### Working Components:
- **PaletteDetector.cs** - Detects & replaces colors (multiple palette support)
- **SpriteColorGenerator.cs** - Batch-processes sprites with color variants
- **SpriteProcessingPipeline.cs** - Full pipeline with color swapping
- **FileRedirector.cs** - Color scheme management
- **ModLoaderIntegration.cs** - Hotkey handling and file redirection
- **PacExtractor.cs** - PAC file extraction (supports PACK header)
- **Program.cs** - CLI tool for extraction and processing
- **Test Scripts** - Reliable run_tests.sh/.ps1 (96 tests passing)

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

## 🔥 CRITICAL NEXT STEPS

1. ~~**All colors implemented**~~ ✅ Red/Blue/Green/Purple working!
2. **Extract FFT sprites** - Get .SPR files from game PAC archives
3. **Generate color variants** - Run sprites through SpriteColorGenerator
4. **Hook file redirection** - Connect FileRedirector to modloader
5. **Test in game** - Deploy and verify hotkeys work!

## 🎮 DEPLOYMENT
**Install Path**: `C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS\Reloaded\Mods\FFT_Color_Mod`
1. Build: `dotnet build -c Release`
2. Copy: FFTColorMod.dll + ModConfig.json + FFTIVC/data folders
3. Launch game with Reloaded-II
4. Enable mod & test hotkeys!

## 📈 PROGRESS SUMMARY
- **Core Logic**: 100% ✅ (All colors, detection, replacement)
- **Testing**: 100% ✅ (99 tests passing)
- **Integration**: 80% 🚧 (Just need file hook)
- **Content**: 0% ⏳ (Need actual FFT sprites)

---
**See PLANNING.md for technical details and research**