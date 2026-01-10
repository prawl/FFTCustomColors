# Generic Jobs Mod Research: Dark Knight and Onion Knight Integration

## Overview

This document provides research findings on incorporating the Dark Knight and Onion Knight from the **GenericJobs mod** into FFTColorCustomizer. The reference implementation from the **Better Palettes mod** demonstrates how to properly support these War of the Lions (WotL) exclusive jobs.

---

## Mod Locations Analyzed

### GenericJobs Mod
- **Path:** `C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\Reloaded\Mods\GenericJobs-34-0-0-6-1763372807 (1)`
- **ModId:** `ffttic.jobs.genericjobs`
- **Author:** cipherxof
- **Version:** 0.0.7
- **Repository:** https://github.com/cipherxof/FFTGenericJobs
- **Dependencies:** `fftivc.utility.modloader`, `Reloaded.Memory.SigScan.ReloadedII`, `reloaded.sharedlib.hooks`

### Better Palettes Mod
- **Path:** `C:\Users\ptyRa\Downloads\better_palettes_extracted\better_palettes`
- **ModId:** `fftivc.custom.better_palettes`
- **Author:** Daytona
- **Version:** 2.0.11
- **NexusMods:** https://www.nexusmods.com/finalfantasytacticstheivalicechronicles/mods/43

---

## Key Discovery: PSP-Exclusive Sprite Path

### Critical Difference from Standard Jobs

Dark Knight and Onion Knight use a **completely different sprite path** than standard generic jobs:

| Job Type | Sprite Path | Naming Convention |
|----------|-------------|-------------------|
| Standard Jobs | `fftpack/unit/` | `battle_[job]_[m/w]_spr.bin` |
| WotL Jobs | `fftpack/unit_psp/` | `spr_dst_bchr_[job]_[m/w]_spr.bin` |

### Original Game Files Location

The base game stores WotL sprites in:
```
data/enhanced/0002/0002/fftpack/unit_psp/
```

Files present:
- `spr_dst_bchr_ankoku_m_spr.bin` - Dark Knight (Male)
- `spr_dst_bchr_ankoku_w_spr.bin` - Dark Knight (Female)
- `spr_dst_bchr_tama_m_spr.bin` - Onion Knight (Male)
- `spr_dst_bchr_tama_w_spr.bin` - Onion Knight (Female)

Plus additional WotL characters:
- `spr_dst_bchr_algakuma_m_spr.bin` - Algazanth
- `spr_dst_bchr_arles_m_spr.bin` - Argath
- `spr_dst_bchr_bulechange_m_spr.bin` - Balthier
- `spr_dst_bchr_bulemonda_m_spr.bin` - Beowulf
- `spr_dst_bchr_kaito_m_spr.bin` - Luso
- `spr_dst_bchr_sword_m_spr.bin` - Dark Knight (Story character - different from generic)
- `spr_dst_bchr_valuhurea_m_spr.bin` - Valmafra

---

## Job Internal Names (Japanese Romanization)

| Display Name | Internal Name | Notes |
|--------------|---------------|-------|
| Dark Knight | `ankoku` | "ankoku" = darkness/dark |
| Onion Knight | `tama` | "tama" = sphere/ball (reference to onion shape) |

Compare with existing mappings:
| Display Name | Internal Name |
|--------------|---------------|
| Squire | `mina` |
| Chemist | `item` |
| Calculator | `san` |
| Samurai | `samu` |
| Dragoon | `ryu` (dragon) |
| Geomancer | `fusui` |
| Time Mage | `toki` |

---

## GenericJobs Mod Structure

### What GenericJobs Does

The GenericJobs mod **enables** Dark Knight and Onion Knight as generic jobs. It modifies:

1. **NXD Data Files** (`data/enhanced/nxd/`)
   - `ability.*.nxd` - Job abilities definitions
   - `charshape.nxd` - Character shape/body definitions
   - `generaljob.nxd` - Job tree and unlock requirements
   - `job.*.nxd` - Job stats and properties
   - `jobcommand.*.nxd` - Job command lists
   - `uijobabilityhelp.*.nxd` - UI help text

2. **UI Face Textures** (`data/enhanced/ui/ffto/common/face/texture/`)
   - `wldface_159_08_uitx.tex` - Dark Knight Male face
   - `wldface_160_08_uitx.tex` - Dark Knight Female face
   - `wldface_161_08_uitx.tex` - Onion Knight Male face
   - `wldface_162_08_uitx.tex` - Onion Knight Female face

3. **Job Icons** (`data/enhanced/ui/ffto/icon/`)
   - `job/texture/j_160_uitx.tex` - Job selection icon
   - `job_comm/texture/jc_160_uitx.tex` - Command icon
   - `job_visual/texture/jv_21_f_uitx.tex` and `jv_21_m_uitx.tex` - Visual icons

### What GenericJobs Does NOT Include

**No sprite files!** The mod relies on the base game's existing WotL sprites in `unit_psp/`.

---

## Better Palettes Implementation Reference

### Sprite File Structure

Better Palettes deploys themed sprites to:
```
FFTIVC/data/enhanced/fftpack/unit_psp/
  spr_dst_bchr_ankoku_m_spr.bin  (~44KB)
  spr_dst_bchr_ankoku_w_spr.bin  (~44KB)
  spr_dst_bchr_tama_m_spr.bin    (~44KB)
  spr_dst_bchr_tama_w_spr.bin    (~44KB)
```

### Theme Organization

Better Palettes uses a config folder structure:
```
config_files/units/wotl/
  ├── male/
  │   ├── Dark_Knight/
  │   │   ├── Default/
  │   │   │   ├── spr_dst_bchr_ankoku_m_spr.bin
  │   │   │   ├── wldface_159_08_uitx.tex
  │   │   │   ├── wldface_159_09_uitx.tex
  │   │   │   └── ... (face variants)
  │   │   ├── Corpse_Brigade/
  │   │   ├── Northern_Sky/
  │   │   ├── Southern_Sky/
  │   │   └── Lucavi/
  │   └── Onion_Knight/
  │       └── (same structure)
  └── female/
      ├── Dark_Knight/
      │   └── (same structure)
      └── Onion_Knight/
          └── (same structure)
```

### Face Texture Mapping

| Job | Gender | Face ID | Files |
|-----|--------|---------|-------|
| Dark Knight | Male | 159 | wldface_159_08 through wldface_159_12 |
| Dark Knight | Female | 160 | wldface_160_08 through wldface_160_12 |
| Onion Knight | Male | 161 | wldface_161_08 through wldface_161_12 |
| Onion Knight | Female | 162 | wldface_162_08 through wldface_162_12 |

The suffix `_08` through `_12` represents different faction/palette variants.

---

## Implementation Plan for FFTColorCustomizer

### 1. Add WotL Jobs to GenericCharacterRegistry

Add to `ColorMod/Registry/GenericCharacterRegistry.cs`:

```csharp
// War of the Lions Jobs (requires GenericJobs mod)

// Dark Knights (ankoku - WotL)
RegisterCharacter("DarkKnight_Male", "Dark Knight (Male)", "WotL Jobs",
    "Color scheme for all male dark knights", "DarkKnightMale", new[] { "ankoku_m" });
RegisterCharacter("DarkKnight_Female", "Dark Knight (Female)", "WotL Jobs",
    "Color scheme for all female dark knights", "DarkKnightFemale", new[] { "ankoku_w" });

// Onion Knights (tama - WotL)
RegisterCharacter("OnionKnight_Male", "Onion Knight (Male)", "WotL Jobs",
    "Color scheme for all male onion knights", "OnionKnightMale", new[] { "tama_m" });
RegisterCharacter("OnionKnight_Female", "Onion Knight (Female)", "WotL Jobs",
    "Color scheme for all female onion knights", "OnionKnightFemale", new[] { "tama_w" });
```

### 2. Add Config Properties

Add to `ColorMod/Configuration/Config.cs`:

```csharp
// WotL Jobs
public string DarkKnight_Male
{
    get => GetJobTheme("DarkKnight_Male");
    set => SetJobTheme("DarkKnight_Male", value);
}

public string DarkKnight_Female
{
    get => GetJobTheme("DarkKnight_Female");
    set => SetJobTheme("DarkKnight_Female", value);
}

public string OnionKnight_Male
{
    get => GetJobTheme("OnionKnight_Male");
    set => SetJobTheme("OnionKnight_Male", value);
}

public string OnionKnight_Female
{
    get => GetJobTheme("OnionKnight_Female");
    set => SetJobTheme("OnionKnight_Female", value);
}
```

### 3. Update Sprite Resolution

The key difference is the **output path**. Add to `ConfigBasedSpriteManager`:

```csharp
// WotL Jobs - use unit_psp path
"DarkKnight_Male" => "spr_dst_bchr_ankoku_m_spr.bin",
"DarkKnight_Female" => "spr_dst_bchr_ankoku_w_spr.bin",
"OnionKnight_Male" => "spr_dst_bchr_tama_m_spr.bin",
"OnionKnight_Female" => "spr_dst_bchr_tama_w_spr.bin",
```

### 4. Create New Sprite Path Handler

The mod needs to deploy WotL sprites to `fftpack/unit_psp/` instead of `fftpack/unit/`:

```csharp
private string GetSpriteOutputPath(string jobKey)
{
    var isWotLJob = jobKey.StartsWith("DarkKnight") || jobKey.StartsWith("OnionKnight");
    var subfolder = isWotLJob ? "unit_psp" : "unit";
    return Path.Combine(_basePath, "data", "enhanced", "fftpack", subfolder);
}
```

### 5. Theme Directory Structure

Create theme directories following this convention:
```
ColorSchemes/
  sprites_darkknight_corpse_brigade/
    spr_dst_bchr_ankoku_m_spr.bin
    spr_dst_bchr_ankoku_w_spr.bin
  sprites_darkknight_lucavi/
    spr_dst_bchr_ankoku_m_spr.bin
    spr_dst_bchr_ankoku_w_spr.bin
  sprites_onionknight_default/
    spr_dst_bchr_tama_m_spr.bin
    spr_dst_bchr_tama_w_spr.bin
```

### 6. Add UI Configuration

Add to `ConfigurationForm.Data.cs`:

```csharp
// WotL Jobs (requires GenericJobs mod)
AddJobRow(row++, "Dark Knight (Male)", _config.DarkKnight_Male, v => _config.DarkKnight_Male = v);
AddJobRow(row++, "Dark Knight (Female)", _config.DarkKnight_Female, v => _config.DarkKnight_Female = v);
AddJobRow(row++, "Onion Knight (Male)", _config.OnionKnight_Male, v => _config.OnionKnight_Male = v);
AddJobRow(row++, "Onion Knight (Female)", _config.OnionKnight_Female, v => _config.OnionKnight_Female = v);
```

---

## Face Texture Handling (Optional Enhancement)

To support faction-based face colors like Better Palettes:

1. **Face textures go to:** `data/enhanced/ui/ffto/common/face/texture/`
2. **TextureParts go to:** `data/enhanced/ui/ffto/common/face/textureparts/`

For each job + faction combo, you'd need:
- Dark Knight Male: `wldface_159_08_uitx.tex` (base) + variant suffixes
- Dark Knight Female: `wldface_160_08_uitx.tex` + variants
- Onion Knight Male: `wldface_161_08_uitx.tex` + variants
- Onion Knight Female: `wldface_162_08_uitx.tex` + variants

This is an advanced feature that could be added later.

---

## Mobile/Standard Texture Consideration

Better Palettes includes separate texture sets:
- `config_files/sprites/mobile/tex_*.bin` - Mobile UI textures
- `config_files/sprites/standard/tex_*.bin` - Standard textures

These appear to be world map character representations. Full implementation would require:
```
data/enhanced/system/ffto/g2d/
  tex_880.bin, tex_881.bin - Unknown assignments
  tex_914.bin, tex_915.bin - Unknown assignments
  tex_1020.bin, tex_1021.bin - Unknown assignments
  tex_1044.bin, tex_1045.bin - Unknown assignments
```

---

## Dependencies

For FFTColorCustomizer to support Dark Knight and Onion Knight:

1. **Required:** User must have GenericJobs mod installed and enabled
2. **Optional:** ModConfig.json could declare an optional dependency:
   ```json
   "OptionalDependencies": ["ffttic.jobs.genericjobs"]
   ```

3. **Detection:** The mod could check if the GenericJobs NXD files exist before showing WotL jobs in the UI

---

## Summary of Changes Needed

| File | Changes |
|------|---------|
| `GenericCharacterRegistry.cs` | Add DarkKnight and OnionKnight definitions |
| `Config.cs` | Add WotL job properties |
| `ConfigBasedSpriteManager.cs` | Add WotL sprite filename mappings |
| `ConfigurationManagerAdapter.cs` | Add sprite-to-job mappings |
| `ConfigurationForm.Data.cs` | Add WotL job UI rows |
| `DynamicSpriteLoader.cs` | Handle `unit_psp` path for WotL sprites |
| `SpriteFileManager.cs` | Support `unit_psp` output directory |

### New Files Needed

1. Theme sprite directories with WotL sprite files
2. Preview images for Dark Knight and Onion Knight
3. Section mapping JSON files for WotL jobs (if supporting theme editor)

---

## File Size Reference

Standard job sprites: ~43-44KB
WotL job sprites: ~44KB (same format)

The file sizes are consistent, confirming these use the same SPR format.

---

## Testing Checklist

- [ ] GenericJobs mod installed and enabled
- [ ] Dark Knight available in job change menu
- [ ] Onion Knight available in job change menu
- [ ] Theme selection appears in FFTColorCustomizer UI
- [ ] Sprite swap applies correctly to `unit_psp` folder
- [ ] Character in-game appearance matches selected theme
- [ ] Original sprites restored when selecting "original" theme
