# Section Mappings Guide

Section mapping files define how palette indices map to customizable sprite sections.

## File Structure

```json
{
  "job": "JobName_Gender",
  "sprite": "battle_xxx_m_spr.bin",
  "sections": [
    {
      "name": "SectionName",
      "displayName": "Display Name",
      "indices": [5, 6, 4, 3],
      "roles": ["base", "highlight", "shadow", "outline"]
    }
  ]
}
```

## Available Roles (from HslColor.cs)

| Role | Effect | Use For |
|------|--------|---------|
| `highlight` | Lightest (L * 1.35) | Brightest shade in set |
| `base` | Primary color | Main/primary shade |
| `shadow` | Darker (L * 0.65) | Second darkest |
| `outline` | Darkest (L * 0.45) | Darkest edge pixels |
| `accent` | Light detail (L * 1.5) | Light accent colors |
| `accent_shadow` | Slightly dark accent (L * 1.25) | Still lighter than base! |

**Important:** `accent_shadow` is LIGHTER than base, not darker.

## Index Order in Sections

Indices are ordered by role assignment (first index gets first role):

Example: `[12, 11, 13, 10]` with `["highlight", "base", "shadow", "outline"]`
- Index 12 = highlight, Index 11 = base, Index 13 = shadow, Index 10 = outline

## Skipped Indices

- Eyes: usually index 2
- Skin tones: usually indices 14-15
- Character outline: usually index 1

---

# Story Character Theme Editor - Implementation Plan

## Overview

Extend the existing Theme Editor to support story characters (Agrias, Mustadio, etc.).

## Current Architecture

| Component | Location | Purpose |
|-----------|----------|---------|
| `ThemeEditorPanel.cs` | ThemeEditor/ | Main UI, template dropdown, color pickers |
| `UserThemeService.cs` | ThemeEditor/ | Save/load user themes, path: `UserThemes/[job]/[theme]/` |
| `PaletteModifier.cs` | ThemeEditor/ | Palette manipulation engine |
| `SectionMapping.cs` | ThemeEditor/ | Model + JSON loader |
| `StoryCharacters.json` | Data/ | Character definitions (names, sprites, themes) |

## Key Differences: Generic Jobs vs Story Characters

| Aspect | Generic Jobs | Story Characters |
|--------|--------------|------------------|
| Naming | `Knight_Male` | `Agrias`, `Mustadio` |
| Sprites | `battle_knight_m_spr.bin` | `battle_aguri_spr.bin` (no gender suffix) |
| Theme folders | `sprites_[theme]/` | `sprites_[char]_[theme]/` |
| Gender | Male/Female variants | Single variant per character |
| Count | 38 jobs | 12 characters |

## Implementation Phases

### Phase 1: Section Mappings for Story Characters (~4 hrs)
- Create `Data/SectionMappings/Story/` directory
- Add JSON files: `Agrias.json`, `Mustadio.json`, `Orlandeau.json`, etc.
- Same format as generic jobs, use verification process
- Characters to map: Agrias, Cloud, Mustadio, Orlandeau, Reis, Rapha, Marach, Beowulf, Meliadoul

### Phase 2: SectionMapping Loader Update (~1 hr)
- `SectionMapping.cs` - Add `LoadStoryCharacterMapping(string charName)`
- Look in `Data/SectionMappings/Story/[CharName].json`

### Phase 3: ThemeEditorPanel Updates (~3 hrs)
- Add character type toggle (Generic Jobs / Story Characters)
- Populate dropdown from `StoryCharacters.json` when Story mode selected
- Load correct mapping based on character name
- Handle sprite path differences for preview

### Phase 4: UserThemeService Extension (~2 hrs)
- Update path resolution for story characters:
  - Generic: `UserThemes/[Job]/[theme]/palette.bin`
  - Story: `UserThemes/Story/[Character]/[theme]/palette.bin`
- Update `SaveTheme`, `GetUserThemes`, `IsUserTheme`, `GetUserThemePalettePath`
- Update registry format in `UserThemes.json`

### Phase 5: Theme Application (~2 hrs)
- `ConfigBasedSpriteManager.ApplyUserTheme()` - handle story character paths
- Output to `sprites_[char]_[theme]/battle_[sprite]_spr.bin`
- Integrate with existing theme dropdown in config form

### Phase 6: Preview Images (~1 hr)
- Ensure story character BMPs exist in `Images/` for preview
- Update preview loader to find story character images

## File Changes Summary

| File | Change |
|------|--------|
| `Data/SectionMappings/Story/*.json` | NEW - 9-12 files |
| `ThemeEditor/SectionMapping.cs` | Add story character loader |
| `ThemeEditor/ThemeEditorPanel.cs` | Add character type toggle, update dropdown |
| `ThemeEditor/UserThemeService.cs` | Add story character path handling |
| `Utilities/ConfigBasedSpriteManager.cs` | Add story character sprite generation |

## Estimated Total: 13-16 hours (~2-3 days)

## Risks & Considerations

1. **Ramza is special** - Uses TEX files, may need separate handling or exclusion
2. **Multi-sprite characters** - Agrias has 2 sprites (aguri, kanba), Mustadio has 2 (musu, garu)
3. **Preview images** - Need BMPs for all story characters
4. **Testing** - Each character needs in-game verification
