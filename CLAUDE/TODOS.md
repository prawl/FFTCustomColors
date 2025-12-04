# FFT Color Mod - TODO List
<!-- KEEP UNDER 100 LINES TOTAL -->

## 🎯 CURRENT STATUS: Working Color Mod!
**Achievement**: File-based color swapping with hotkeys (F1-F5) fully functional! 🎉
- **124 tests passing** with streaming PAC extraction
- **StartEx bug FIXED** - mod initializes properly
- **Color variants working** for battle_10m_spr.bin sprites
- **Hotkeys functional** - F1-F5 switch colors in-game

## ✅ COMPLETED FEATURES
- **Core Systems**: PaletteDetector, SpriteColorGenerator, FileRedirector
- **PAC Extraction**: Streaming support for >2GB files with PACK header
- **Color Schemes**: Blue/Red/Green/Purple/Original variants
- **Game Integration**: Hotkey monitoring, file path interception
- **Sprite Processing**: battle_10m_spr.bin with all color variants
- **Deployment**: .diff.pac files in FFTIVC/data/enhanced/

## 🔥 IMMEDIATE NEXT STEPS

### 1. Scale Up Sprite Coverage
**Goal**: Process ALL sprites for complete character coverage
```bash
# Extract all sprites from PAC files
./FF16Tools.CLI.exe unpack-all -i "0002.pac" -o sprites_0002 -g fft
./FF16Tools.CLI.exe unpack-all -i "0003.pac" -o sprites_0003 -g fft

# Process all extracted sprites
dotnet run -- process sprites_0002 processed_sprites
dotnet run -- process sprites_0003 processed_sprites
```

### 2. Identify Ramza's Sprite
**Testing needed to find which sprite is Ramza**:
- Start new game, note Ramza's appearance
- Press F2 (red) - which character changes?
- Check sprite types: 10m, 20m, 40m, 60m, etc.
- Document mapping: Character → Sprite file

### 3. Optimize for First 288 Bytes
```csharp
// Update PaletteDetector to only process palette region
public void ProcessPaletteOnly(byte[] spriteData) {
    // Only modify bytes 0-287 (palette data)
    var paletteRegion = spriteData.Take(288).ToArray();
    DetectAndReplace(paletteRegion);
    Array.Copy(paletteRegion, spriteData, 288);
}
```

## 📊 TESTING CHECKLIST
- [ ] Process all sprites in fftpack/unit/
- [ ] Identify main character sprites (Ramza, Delita, Agrias)
- [ ] Test in battles, cutscenes, formation screen
- [ ] Verify color persistence across scenes
- [ ] Test compatibility with other mods

## 🛠️ TECHNICAL STACK
**Working**: PaletteDetector, SpriteColorGenerator, PacExtractor, FileRedirector, Startup.cs
**Files**: 124 tests, streaming PAC support, .bin sprite format
**Deployment**: Reloaded-II mod, FFTIVC structure, .diff.pac files

## 📁 FILE STRUCTURE
```
FFT_Color_Mod/
├── CLAUDE/              # Documentation
├── FFTIVC/             # Game mod files
│   └── data/enhanced/
│       ├── 0002.[color].diff.pac
│       └── fftpack/unit/sprites_[color]/
├── Tests/              # 124 passing tests
└── Source/             # Core implementation
```

## 📝 DEPLOYMENT
```bash
dotnet build -c Release
.\BuildLinked.ps1  # Quick deploy to Reloaded
# Launch FFT with Reloaded-II → Enable mod → Press F1-F5 in-game
```

## 🎮 SUCCESS CRITERIA
- ✅ F1-F5 instantly change sprite colors
- ✅ Works in battles and cutscenes
- 🔄 Need: Full character coverage (not just 10m)
- 🔄 Need: Identify Ramza's specific sprite

---
**Status**: Core functionality complete, scaling up coverage