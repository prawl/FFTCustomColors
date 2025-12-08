# FFT Color Mod - TODOs

## Immediate Tasks
- [X] Process ALL sprites in unit folder (not just 10m) - DONE: Extracted all sprites from PAC files
- [X] Extract sprites from both 0002.pac and 0003.pac files - DONE: Both PAC files extracted
- [X] ~~Identify which sprite file is Ramza through gameplay testing~~ (BLOCKED: Ramza protected by DLC override)
- [X] Document sprite type mapping (10m/20m/40m/60m → character types) - DONE: Complete mappings in README.md
- [X] Optimize PaletteDetector to only process first 288 bytes - DONE: FindPalette and FindAllPalettes now limited to 288 bytes
- [X] Test color persistence across battles, cutscenes, formation screen - DONE: Tests verify persistence
- [X] Implement color persistence across game sessions - DONE: ColorPreferencesManager saves to %AppData%
- [X] Test compatibility with other mods - DONE: Compatible with Better Palettes and GenericJobs

## Code Quality & Structure (Lower Priority)
- [X] Update project structure to match GenericJobs repo (Root > ColorMod, .gitignore, .sln, LICENSE)
- [X] Fix scripts to use CamelCase naming convention
- [X] Update repository files to use CamelCase naming convention
- [ ] Refactor all classes for better organization and clarity
- [X] Fix test flakiness issues (tests randomly failing)
- [ ] Add extensive documentation (XML comments, README sections, API docs)

## Core User Requirements (Full Vision)
- [ ] **Character Customization**: Customize color of every playable character
- [ ] **Targeted Customization**: Separate color control for armor vs hair
- [ ] **Preset Palettes**: Select from faction color schemes (Northern Sky, Southern Sky, etc.)
- [ ] **Save Persistence**: Colors persist between game saves and sessions
- [ ] **In-Game UI**: Edit character colors directly in-game via UI menu
- [ ] **Unlockable Colors**: Rare colors as gameplay rewards (MVP feature)

## Advanced UI Enhancement (Difficulty: 6.5-7/10)
- [ ] Research formation screen menu structure with x64dbg
- [ ] Hook menu creation functions to inject "Color Palette" option
- [ ] Implement color selection submenu UI
- [ ] Add per-unit color tracking and persistence
- [ ] Handle menu navigation and state management
- [ ] Consider simpler config-based approach as stepping stone

## MVP Features (Dream Features)
### Community & Sharing
- [ ] **Team Uniform System** - One-click matching colors for entire party
- [ ] **Color Import/Export** - Share schemes via codes/files, community library
- [ ] **Screenshot Mode** - Temporary cinematic filters without permanent changes

### Dynamic & Reactive Colors
- [ ] **Color Evolution System** - Colors shift based on combat actions (fire=red, healing=blue)
- [ ] **Dynamic Battle Colors** - Status effects change colors (berserk=red, haste=blue)
- [ ] **Weather-Reactive Colors** - Rain darkens/adds shine, sun brightens, night desaturates
- [ ] **Battlefield Camouflage** - Auto-adjust to terrain (desert=sandy, snow=white)
- [ ] **Mirror Match Detection** - Auto-contrast colors vs same enemy job classes

### Progression & Unlocks
- [ ] **Achievement-Based Unlocks** - Special colors for challenges (solo run = "Lone Wolf Silver")
- [ ] **Seasonal/Holiday Themes** - Real-world date unlocks (holidays, FFT anniversary)
- [ ] **"Veteran Scars" System** - Battle-worn variations for experienced units
- [ ] **Legacy Color Inheritance** - Recruited units inherit traits from recruiter

### Gameplay Integration
- [ ] **Zodiac Color Compatibility** - Bonuses for zodiac-matched colors
- [ ] **Job-Based Color Templates** - Auto-apply thematic colors on job change
- [ ] **Color Mixing for Hybrid Jobs** - Blend colors when multi-classing
- [ ] **Nemesis System Colors** - Recurring enemies remember/comment on colors
- [ ] **Enemy Army Recoloring** - Visually distinct enemy factions

### Utility & Fun
- [ ] **Color History/Undo** - Track last 5-10 changes with quick undo/redo
- [ ] **Color Randomizer Mode** - Chaos mode randomizing each battle
- [ ] **Color-Based Ability Effects** - Visual effects vary by character color

## Future Enhancements
- [ ] **REQUIRED FOR RAMZA**: Find sprite loading signatures with x64dbg
- [ ] **REQUIRED FOR RAMZA**: Implement memory hooking to bypass DLC protection
- [ ] Add per-character color customization
- [ ] Create color-blind friendly schemes
- [ ] Add enemy color modifications
- [ ] Test if users without deluxe/preorder can modify Ramza via files


## Release Preparation
- [ ] Create before/after screenshots
- [ ] Write mod description (100-200 words)
- [ ] Create installation guide
- [ ] Package for Reloaded-II Database
- [ ] Register on FFHacktics forum
