# Theme Editor Design Document

## Overview

Built-in theme editor for creating custom color themes for generic job classes with real-time preview.

## Architecture

### Palette Structure
- 16 palettes × 16 colors × 2 bytes = 512 bytes (first 512 bytes of BIN)
- Palette 0: Player colors (what we modify)
- Palettes 1-4: Enemy colors (preserve)

### Section Mapping Format
```json
{
  "job": "Knight_Male",
  "sprite": "battle_knight_m_spr.bin",
  "sections": [
    { "name": "Cape", "displayName": "Cape", "indices": [3, 4, 5], "roles": ["shadow", "base", "highlight"] }
  ]
}
```
Location: `ColorMod/Data/SectionMappings/[Job]_[Gender].json`

### Auto-Shade Algorithm (HSL)
```
Shadow:    L = L * 0.65, S = min(S * 1.1, 1.0)
Base:      Original color
Highlight: L = min(L * 1.35, 0.95), S = S * 0.85
```

### File Storage
```
%RELOADEDIIMODS%/FFTColorCustomizer/
  ├── UserThemes.json
  └── UserThemes/[Job]/[theme_name]/[sprite].bin
```

### Theme Sharing Format
```json
{ "version": 1, "name": "Ocean Blue", "job": "Knight_Male", "palette": "base64-512-bytes", "sections": { "Cape": "#0047AB" } }
```

---

## Implementation TODO

### Phase 1: Core Palette Engine ✅
- [x] `PaletteModifier.cs` - LoadTemplate, SetPaletteColor, GetPreview, Reset, ApplySectionColor, SaveToFile
- [x] `HslColor.cs` - RGB↔HSL conversion, auto-shade generation
- [x] `SectionMapping.cs` - model classes and loader

### Phase 2: Theme Editor UI (In Progress)
- [x] Add "Theme Editor" tab to ConfigurationForm
- [x] ThemeEditorPanel - template dropdown, job filtering, mapping loader
- [x] Sprite preview panel with rotation arrows
- [x] HslColorPicker component - H/S/L sliders, properties, events
- [x] Dynamic section color pickers from mapping
- [x] Theme name input, Save/Reset/Cancel buttons
- [x] Integrate preview with PaletteModifier (real-time updates)
- [x] Per-section Reset button
- [ ] Color preview swatch in HslColorPicker
- [ ] Hex code display/input
- [ ] Copy/Paste buttons

### Phase 3: Theme Persistence
- [ ] `UserThemeService.cs` - SaveTheme, LoadTheme, DeleteTheme, GetUserThemes
- [ ] UserThemes.json registry
- [ ] Theme name validation (duplicates, reserved names, allowed chars)
- [ ] Wire Save button

### Phase 4: Theme System Integration
- [ ] Modify `ThemeService.cs` to discover UserThemes
- [ ] Add user themes to dropdowns (after separator)
- [ ] Theme resolution: user → built-in → original

### Phase 5: Import/Export
- [ ] Export button (JSON to clipboard)
- [ ] Import from Clipboard (validate, populate editor, handle conflicts)

### Phase 6: My Themes Management
- [ ] "My Themes" tab - grouped by job, Export/Delete buttons
- [ ] Storage indicator, soft warning at 50+ themes

### Phase 7: Additional Mappings
- [ ] Knight, Archer, Monk, Thief, Chemist, White Mage (Male/Female)

---

## Key Decisions

- **HSL over HSV**: More intuitive lightness control
- **Themes immutable**: Edit via export → import → save as new
- **No hard storage limit**: Soft warning at 50 themes (~2MB)
- **User themes priority**: User themes override built-in if same name

## Diagnostic Tool

Run `python scripts/diagnostic_sprite.py input.bin output.bin` to create rainbow-palette sprite for section mapping research.
