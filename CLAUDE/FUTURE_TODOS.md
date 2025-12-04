# FFT Color Mod - Future TODOs
<!-- Tasks to tackle AFTER v0.1-alpha is working -->

## ✅ Completed (December 2024)
- [x] Analyzed FFTGenericJobs approach
- [x] Build scripts (BuildLinked.ps1, Publish.ps1)
- [x] GitHub Actions CI/CD
- [x] 52 passing tests (up from 29)
- [x] PaletteDetector logic
- [x] Hotkey system (F1/F2)
- [x] All 4 chapter detection
- [x] Added required packages
- [x] Created SignatureScanner with tests
- [x] Added CreateSpriteLoadHook with TDD
- [x] Tested mod in Reloaded-II (loads but no Start())
- [x] Wired up pattern found handler in Mod.cs

## 🎯 POST-MVP Tasks

### Account Setup
- [ ] Register on mod sites (prawl or Pax username):
  - FFHacktics forum (priority)
  - Nexus Mods
  - GameBanana

### Version 0.2.0-beta
- [ ] Fix bugs from alpha feedback
- [ ] Add Blue color (F1)
- [ ] Better screenshots
- [ ] Post to one mod site

### Version 1.0.0
- [ ] All colors working
- [ ] Full documentation
- [ ] Release on all mod sites

## 🔧 Code Architecture (from FFTGenericJobs)

### Build System
- [ ] Create `Reloaded.Checks.targets` for RELOADEDIIMODS validation
- [ ] Create `Reloaded.Trimming.targets` for IL trimming
- [ ] Update FFTColorMod.csproj to import targets

### Template Pattern
- [ ] Create `Template/` folder with boilerplate
- [ ] Move entry logic to `Startup.cs`
- [ ] Create `ModContext.cs` for dependency injection

### Configuration System
- [ ] Create `Configuration/` folder
- [ ] Add `Configurable.cs` base class
- [ ] Support hot-reload of settings
- [ ] User-customizable colors

## 🔬 Research & Discovery

### Signature Scanning
- [ ] Install x64dbg debugger
- [ ] Find sprite loading signatures in FFT_enhanced.exe
- [ ] Identify palette application signatures
- [ ] Document patterns in `Signatures.cs`
- [ ] Test signature reliability

### Advanced Hooking
- [ ] Create `SigScanner.cs` class
- [ ] Implement `HookManager.cs`
- [ ] Add comprehensive logging
- [ ] Test with known patterns

## 🎨 Feature Enhancements

### Color Schemes
- [ ] Blue scheme (F1)
- [ ] Green scheme (F3)
- [ ] Purple scheme (F4)
- [ ] Custom schemes via config
- [ ] Color cycling with single key

### User Interface
- [ ] Reloaded-II config UI
- [ ] Color preview
- [ ] Per-character colors
- [ ] Save/load presets

### Advanced Features
- [ ] Enemy colors
- [ ] Job-specific schemes
- [ ] Team colors
- [ ] Randomizer mode
- [ ] Color-blind schemes

## 📝 Documentation

### Testing
- [ ] Tests for signature scanning
- [ ] Hook management tests
- [ ] Integration tests
- [ ] Performance benchmarks
- [ ] Compatibility tests

### Documentation
- [ ] Document signatures
- [ ] Hooking guide
- [ ] Troubleshooting
- [ ] Video demo

## 📸 Marketing & Presentation

### Screenshots
- [ ] Before/after comparisons
- [ ] Each color scheme
- [ ] All chapters
- [ ] Battle scenes
- [ ] Animated GIF

### Mod Pages
- [ ] Write description (100-200 words)
- [ ] Feature list
- [ ] Installation guide
- [ ] FAQ section
- [ ] Thumbnail/banner (600x338px)

### Distribution
- [ ] Reloaded-II Database
- [ ] Nexus Mods
- [ ] GameBanana
- [ ] ModDB
- [ ] FFHacktics forum
- [ ] YouTube video
- [ ] NuGet package

## 🏆 Code Quality

### Benefits to Implement
- Build validation
- IL Trimming (50-70% size reduction)
- Debug launcher
- Template pattern
- Hot-reload config
- ModContext DI

### Performance
- [ ] Cache processed sprites
- [ ] Profile with dotnet tools
- [ ] Optimize hook points

## 💡 Ideas & Research

### Technical
- [ ] GPU shader interception
- [ ] Universal Redirector fallback
- [ ] Memory-mapped files

### Community
- [ ] User feedback collection
- [ ] Feature requests
- [ ] Mod compatibility matrix

---

**Note**: These are all post-MVP tasks. Focus on getting v0.1-alpha working first with just RED color!