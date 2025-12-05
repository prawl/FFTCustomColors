# FFT Color Mod - TODOs

## Immediate Tasks
- [ ] Process ALL sprites in unit folder (not just 10m)
- [ ] Extract sprites from both 0002.pac and 0003.pac files
- [X] ~~Identify which sprite file is Ramza through gameplay testing~~ (BLOCKED: Ramza protected by DLC override)
- [ ] Document sprite type mapping (10m/20m/40m/60m → character types)
- [ ] Optimize PaletteDetector to only process first 288 bytes
- [ ] Test color persistence across battles, cutscenes, formation screen
- [ ] Test compatibility with other mods

## Future Enhancements
- [ ] **REQUIRED FOR RAMZA**: Find sprite loading signatures with x64dbg
- [ ] **REQUIRED FOR RAMZA**: Implement memory hooking to bypass DLC protection
- [ ] Add per-character color customization
- [ ] Create color-blind friendly schemes
- [ ] Add enemy color modifications
- [ ] Test if users without deluxe/preorder can modify Ramza via files

## Code Quality & Structure (Lower Priority)
- [ ] Update project structure to match GenericJobs repo (Root > ColorMod, .gitignore, .sln, LICENSE)
- [ ] Fix scripts to use CamelCase naming convention
- [ ] Update repository files to use CamelCase naming convention
- [ ] Refactor all classes for better organization and clarity
- [ ] Fix test flakiness issues (tests randomly failing)
- [ ] Add extensive documentation (XML comments, README sections, API docs)

## Release Preparation
- [ ] Create before/after screenshots
- [ ] Write mod description (100-200 words)
- [ ] Create installation guide
- [ ] Package for Reloaded-II Database
- [ ] Register on FFHacktics forum