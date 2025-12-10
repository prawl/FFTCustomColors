# FFT Color Mod - TODOs

## Current Task - Config-Based Sprite Customization (COMPLETED!)
### Status: Reloaded-II Configuration System Fully Implemented
- [X] Created Config.cs with all job/gender mappings (battle_knight_m, battle_yumi_w, etc.)
- [X] Implemented GetColorForSprite method mapping sprite names to config properties
- [X] Created ConfigurationManager for loading/saving JSON configs
- [X] Created ConfigBasedSpriteManager for per-job sprite management
- [X] Integrated config system with Mod.cs (InterceptFilePath uses job-based colors)
- [X] Created ReloadedConfig, ModConfig, and ReloadedConfigManager classes
- [X] Created Configurator class implementing IConfigurable<Config>
- [X] Updated Mod class to implement IConfigurable<Config>
- [X] **COMPLETED: All ConfigurableTests passing (10 tests)** ✅
- [X] Implemented IConfigurable interface correctly (ConfigName and Save properties)
- [X] Configuration persistence working (saves and loads correctly)
- [X] ConfigurationUpdated event system working
- [X] GetJobColor and GetAllJobColors methods working
- [ ] **NEXT: Test with actual Reloaded-II mod loader UI**
- [ ] Add dropdown selections for color schemes in Reloaded-II config
- [ ] Document configuration usage in README

### Key Implementation Details:
- **Job Sprite Patterns**: battle_knight_, battle_yumi_ (archer), battle_item_ (chemist), battle_monk_, battle_siro_ (white mage), battle_kuro_ (black mage), battle_thief_, battle_ninja_, battle_mina_ (squire), battle_toki_ (time mage), battle_syou_ (summoner), battle_samu_ (samurai), battle_ryu_ (dragoon), battle_fusui_ (geomancer), battle_onmyo_ (mystic/oracle), battle_waju_ (mediator), battle_odori_ (dancer), battle_gin_ (bard), battle_mono_ (mime), battle_san_ (calculator)
- **Config Path**: Uses environment variable FFT_CONFIG_PATH or defaults to modPath/config.json
- **Dual System**: InterceptFilePath checks if sprite is job-based, uses ConfigBasedSpriteManager for jobs, falls back to old SpriteFileManager for non-job sprites (like RAMZA.SPR)
- **TDD Approach**: Writing tests first, then minimal implementation to pass

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
- [X] Refactor all classes for better organization and clarity
- [X] Fix test flakiness issues (tests randomly failing)
- [X] Add extensive documentation (XML comments, README sections, API docs)

## Core User Requirements (Full Vision)
- [ ] **Character Customization**: Customize color of every playable character
- [ ] **Targeted Customization**: Separate color control for armor vs hair
- [X] **Preset Palettes**: Select from faction color schemes (Northern Sky, Southern Sky, etc.)
- [X] **Save Persistence**: Colors persist between game saves and sessions
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
