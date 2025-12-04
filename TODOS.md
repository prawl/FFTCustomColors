# FFT Color Mod - TODO List
<!-- KEEP UNDER 100 LINES TOTAL -->

## ⚡ IMMEDIATE: Get RED Working with F2

**Goal**: v0.1-alpha in 1-2 weeks
**Status**: 45 tests passing ✅, mod loads in Reloaded-II!

### ✅ CONFIRMED WORKING (Dec 3, 2024):
- Mod loads successfully in Reloaded-II
- F1/F2 hotkeys respond correctly
- Memory scanning finds palettes (5 found in test)
- Chapter detection works (found Ch1 & Ch2)
- Memory writes succeed (WriteProcessMemory=True)
- Color values change in memory (80 40 60 → 30 30 80)

### 🔴 PROBLEM: Colors don't persist visually
**Why**: FFT reloads palettes from files constantly
**Solution**: Need to hook sprite loading functions

### This Week: Add Dependencies & Hook
- [x] ✅ Add required packages (DONE)
- [x] ✅ Create SignatureScanner with tests (45 tests passing!)
- [x] ✅ Add ProcessSpriteData hook method (DONE)
- [x] ✅ Test mod in Reloaded-II (WORKS!)
- [ ] 🔴 Fix: Start() method not being called by Reloaded
- [ ] 🔴 Find actual sprite loading signature with x64dbg
- [ ] 🔴 Hook sprite loading to modify DURING load

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

**Test Results (Dec 3, 2024)**:
- ✅ Memory modification works (5 palettes found & modified)
- ✅ Chapter detection accurate (Ch1 & Ch2 identified)
- ✅ Hotkeys work (F1/F2 switching)
- 🔴 Visual changes don't persist (need hooks)
- 🔴 Start() not called (need to fix Reloaded integration)
- 🔴 Need actual sprite loading signatures

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