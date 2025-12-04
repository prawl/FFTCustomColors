# FFT Color Mod - TODO List
<!-- KEEP UNDER 100 LINES TOTAL -->

## ⚡ IMMEDIATE: Get RED Working with F2

**Goal**: v0.1-alpha in 1-2 weeks
**Status**: 45 tests passing ✅, experimental hooks deployed!

### This Week: Add Dependencies & Hook
- [x] ✅ Add required packages (DONE - already in csproj)
- [x] ✅ Update ModConfig.json dependencies (DONE)
- [x] ✅ Create SignatureScanner class with tests (DONE - 45 tests passing!)
- [x] ✅ Add ProcessSpriteData hook method (DONE)
- [x] ✅ Wire up PaletteDetector to SignatureScanner (DONE)
- [x] ✅ Add ColorScheme property for F2 switching (DONE)
- [x] ✅ Add experimental hook patterns (DONE)
- [x] ✅ Add logging for pattern discovery (DONE)
- [ ] 🔴 Find actual sprite loading signature with x64dbg
- [ ] 🔴 Test hook fires in game with console output

### Next Week: Red Color
- [ ] 🔴 Hook sprite loading function
- [ ] 🔴 Integrate existing PaletteDetector
- [ ] 🔴 Hard-code RED color only
- [ ] 🔴 Test with Chapter 1 Ramza
- [ ] 🔴 Add F2 hotkey toggle

### Release v0.1-alpha
- [ ] 🔴 One screenshot (before/after)
- [ ] 🔴 Basic README
- [ ] 🔴 Build with `.\Publish.ps1`
- [ ] 🔴 Tag as v0.1.0-alpha
- [ ] 🔴 Upload to GitHub
- [ ] 🔴 Post on FFHacktics

## 📖 Quick Context (for new sessions)

**Problem**: Direct memory edits fail - FFT reloads palettes
**Solution**: Hook sprite loading functions (like FFTGenericJobs does)
**Format**: BGR colors, 256 per palette
**Key Files**:
- PaletteDetector.cs (tested - detects all 4 chapters!)
- SignatureScanner.cs (hook infrastructure ready)
- run_tests.sh / run_tests.ps1 (use these to run tests!)

**Current Progress**:
- ✅ SignatureScanner with IReloadedHooks integration
- ✅ ProcessSpriteData hook method ready
- ✅ PaletteDetector wired up to scanner
- ✅ ColorScheme property for F1/F2 switching
- ✅ Experimental hook patterns ready for testing
- ✅ Logging system for pattern discovery
- 🔴 Need: Find actual sprite loading signature via testing

## 🔧 Hook Implementation Pattern

```csharp
// Find function signature
_startupScanner.AddMainModuleScan(
    "48 8B C4 48 89 58 ??",  // Byte pattern
    result => {
        if (result.Found) {
            _hooks.CreateHook<LoadSpriteDelegate>(
                LoadSpriteHook,
                gameBase + result.Offset
            ).Activate();
        }
    }
);

// Hook implementation
private nint LoadSpriteHook(nint spriteData, int size) {
    var result = _loadSpriteHook.OriginalFunction(spriteData, size);
    // Apply our PaletteDetector here!
    ModifyPaletteInMemory(spriteData);
    return result;
}
```

## ✅ Completed (December 3, 2025)
- [x] Analyzed FFTGenericJobs approach
- [x] Build scripts (BuildLinked.ps1, Publish.ps1)
- [x] GitHub Actions CI/CD
- [x] 29 passing tests
- [x] PaletteDetector logic
- [x] Hotkey system (F1/F2)
- [x] All 4 chapter detection

---
**See FUTURE_TODOS.md for post-MVP tasks**