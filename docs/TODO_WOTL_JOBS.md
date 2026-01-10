# TODO: Implement Dark Knight and Onion Knight Support

## Overview
Add support for theming the War of the Lions exclusive jobs (Dark Knight and Onion Knight) that become available when users install the GenericJobs mod.

---

## Phase 1: Core Registry and Configuration ✅ COMPLETE

### 1.1 Update GenericCharacterRegistry.cs
- [x] Add Dark Knight Male registration (`ankoku_m`)
- [x] Add Dark Knight Female registration (`ankoku_w`)
- [x] Add Onion Knight Male registration (`tama_m`)
- [x] Add Onion Knight Female registration (`tama_w`)
- [x] Use new category "WotL Jobs" to group them separately

### 1.2 Update Config.cs
- [x] Add `DarkKnightMale` to JobMetadata dictionary
- [x] Add `DarkKnightFemale` to JobMetadata dictionary
- [x] Add `OnionKnightMale` to JobMetadata dictionary
- [x] Add `OnionKnightFemale` to JobMetadata dictionary
- [x] Add `DarkKnight_Male` property with getter/setter
- [x] Add `DarkKnight_Female` property with getter/setter
- [x] Add `OnionKnight_Male` property with getter/setter
- [x] Add `OnionKnight_Female` property with getter/setter

### 1.3 Update Config.json
- [x] Add default entries for the 4 new jobs (set to "original")

---

## Phase 2: Sprite File Handling ✅ COMPLETE

### 2.1 Update ConfigBasedSpriteManager.cs
- [x] Add sprite filename mappings:
  - `DarkKnight_Male` → `spr_dst_bchr_ankoku_m_spr.bin`
  - `DarkKnight_Female` → `spr_dst_bchr_ankoku_w_spr.bin`
  - `OnionKnight_Male` → `spr_dst_bchr_tama_m_spr.bin`
  - `OnionKnight_Female` → `spr_dst_bchr_tama_w_spr.bin`

### 2.2 Update ConfigurationManagerAdapter.cs
- [x] Add sprite-to-job mappings for `ankoku_m`, `ankoku_w`, `tama_m`, `tama_w`

### 2.3 Update DynamicSpriteLoader.cs
- [x] Add logic to detect WotL jobs (`IsWotLJob` method)
- [x] Route WotL sprites to `fftpack/unit_psp/` instead of `fftpack/unit/` (`GetUnitDirectory` method)

### 2.4 Update SpriteFileManager.cs
- [x] Handle the different output path for WotL sprites (`GetTargetUnitDirectory` method)

---

## Phase 3: GenericJobs Mod Detection ✅ COMPLETE

### 3.1 Create GenericJobsDetector Service
- [x] Create new file `Services/GenericJobsDetector.cs`
- [x] Implement detection by checking for `GenericJobs*` folder in Reloaded Mods directory
- [x] Cache detection result (don't check filesystem repeatedly)
- [x] Expose `IsGenericJobsInstalled` property
- [x] Default to "not detected" if check fails (safe fallback)

### 3.2 Register Service
- [ ] Add `GenericJobsDetector` to `ServiceContainer.cs` (Optional - can be used directly)
- [ ] Initialize during mod startup (Optional - can be instantiated as needed)

---

## Phase 4: UI Configuration ✅ COMPLETE

### 4.1 Create New "WotL Jobs" Section
- [x] Add collapsible "WotL Jobs (Requires GenericJobs Mod)" section to ConfigurationForm
- [x] Section order: Generic Characters | Story Characters | **WotL Jobs** | Theme Editor | My Themes
- [x] Create dedicated controls list for WotL jobs

### 4.2 Update ConfigurationForm.Data.cs
- [x] Add `LoadWotLJobs` method to populate WotL section
- [x] Add row for Dark Knight (Male)
- [x] Add row for Dark Knight (Female)
- [x] Add row for Onion Knight (Male)
- [x] Add row for Onion Knight (Female)
- [x] Add reset logic for WotL jobs in `ResetAllCharacters`

### 4.3 Update ConfigurationForm.cs
- [x] Add `_wotlJobsCollapsed` and `_wotlJobsControls` fields
- [x] Add `ToggleWotLJobsVisibility` method

### 4.4 Update CharacterRowBuilder.cs
- [x] Add optional `controlsList` parameter to `AddGenericCharacterRow` for WotL jobs

### 4.5 Mod Status Indicator (Future Enhancement)
- [ ] Show mod detection status at top of WotL section (optional enhancement)
- [ ] If mod NOT detected, show warning banner (optional enhancement)

---

## Phase 5: Theme Assets

### 5.1 Create Base Theme Directories
- [ ] Create `ColorSchemes/sprites_darkknight_original/` (or similar structure)
- [ ] Create `ColorSchemes/sprites_onionknight_original/`

### 5.2 Obtain/Create Sprite Files
- [ ] Extract original Dark Knight sprites from game (`0002/0002/fftpack/unit_psp/`)
- [ ] Extract original Onion Knight sprites from game
- [ ] Create at least one alternate theme for testing

### 5.3 Create Preview Images
- [ ] Generate `darkknight_male_original.png` preview
- [ ] Generate `darkknight_female_original.png` preview
- [ ] Generate `onionknight_male_original.png` preview
- [ ] Generate `onionknight_female_original.png` preview
- [ ] Add previews as embedded resources

---

## Phase 6: Data Files ✅ COMPLETE

### 6.1 Create WotLClasses.json
- [x] Add Dark Knight Male job definition
- [x] Add Dark Knight Female job definition
- [x] Add Onion Knight Male job definition
- [x] Add Onion Knight Female job definition

### 6.2 Update JobClassDefinitionService.cs
- [x] Add logic to load WotLClasses.json alongside JobClasses.json

### 6.3 Create Section Mappings (Optional - for Theme Editor)
- [ ] Create `DarkKnight_Male.json` section mapping
- [ ] Create `DarkKnight_Female.json` section mapping
- [ ] Create `OnionKnight_Male.json` section mapping
- [ ] Create `OnionKnight_Female.json` section mapping

---

## Phase 7: Testing

### 7.1 Unit Tests ✅ COMPLETE
- [x] Add tests for WotL job registration in GenericCharacterRegistry
- [x] Add tests for sprite path resolution (unit_psp vs unit) - DynamicSpriteLoaderTests, SpriteFileManagerTests
- [x] Add tests for config property persistence - ConfigTests
- [x] Add tests for GenericJobsDetector service - GenericJobsDetectorTests
- [x] Add tests for ConfigurationManagerAdapter WotL mappings
- [x] Add tests for JobClassDefinitionService WotL loading

### 7.2 Integration Testing
- [ ] Install GenericJobs mod
- [ ] Verify Dark Knight appears in FFTColorCustomizer UI
- [ ] Verify Onion Knight appears in FFTColorCustomizer UI
- [ ] Test theme switching for Dark Knight Male
- [ ] Test theme switching for Dark Knight Female
- [ ] Test theme switching for Onion Knight Male
- [ ] Test theme switching for Onion Knight Female
- [ ] Verify sprites deploy to correct `unit_psp` folder
- [ ] Verify in-game appearance matches selected theme
- [ ] Test "original" theme restores default sprites

### 7.3 Edge Cases
- [ ] Test behavior when GenericJobs mod is NOT installed (warning should show)
- [ ] Test config migration from older versions without WotL jobs
- [ ] Test theme discovery for WotL jobs
- [ ] Verify dropdowns still work when mod not detected (graceful degradation)

---

## Phase 8: Documentation

### 8.1 Update CLAUDE.md ✅ COMPLETE
- [x] Add WotL job name mappings to special job names section
- [x] Document `unit_psp` path requirement

### 8.2 User Documentation ✅ COMPLETE
- [x] Note GenericJobs mod dependency
- [x] Document how to create themes for WotL jobs
- [x] Update any README or user guides

---

## Optional Enhancements (Future)

### Face Texture Support
- [ ] Support face texture theming (wldface_159-162)
- [ ] Handle faction-based face variants (_08 through _12 suffixes)

### Mobile Textures
- [ ] Research tex_*.bin files for world map sprites
- [ ] Implement world map character theming if applicable

---

## File Change Summary

| File | Type of Change |
|------|----------------|
| `GenericCharacterRegistry.cs` | Add 4 job registrations |
| `Config.cs` | Add metadata + 4 properties |
| `Config.json` | Add 4 default entries |
| `ConfigBasedSpriteManager.cs` | Add 4 filename mappings |
| `ConfigurationManagerAdapter.cs` | Add 4 sprite-to-job mappings |
| `DynamicSpriteLoader.cs` | Add unit_psp path logic |
| `SpriteFileManager.cs` | Support unit_psp directory |
| `ConfigurationForm.cs` | Add new "WotL Characters" tab |
| `ConfigurationForm.Data.cs` | Add WotL tab population method |
| `CharacterRowBuilder.cs` | Add preview mappings + status icons |
| `JobClasses.json` | Add 4 job definitions |
| `CLAUDE.md` | Document new mappings |
| **NEW:** `Services/GenericJobsDetector.cs` | Mod detection service |
| `ServiceContainer.cs` | Register GenericJobsDetector |

---

## Dependencies

**Required for users:**
- GenericJobs mod (`ffttic.jobs.genericjobs`) v0.0.7+

**No new code dependencies for FFTColorCustomizer itself.**

---

## Estimated Scope

- **Registry/Config changes:** ~50-100 lines
- **Sprite path handling:** ~20-30 lines
- **GenericJobsDetector service:** ~40-60 lines
- **UI additions (new tab + status indicators):** ~100-150 lines
- **Data files:** ~50 lines JSON
- **Tests:** ~80-120 lines

Total: **~350-500 lines of code** plus asset files
