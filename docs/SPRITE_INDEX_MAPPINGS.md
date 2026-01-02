# Sprite Index Mappings

## Overview

This document tracks the palette index mappings for each job sprite, identifying which palette indices correspond to which visual sections (armor, cape, hair, etc.).

## TODO Tracking

### Completed ✅
- Squire Female (battle_mina_w_spr.bin)
- Chemist Female (battle_item_w_spr.bin)
- Knight Male (battle_knight_m_spr.bin)

### In Progress 🚧
- (None currently)

### Not Started ❌
- Squire Male (battle_mina_m_spr.bin)
- Knight Female (battle_knight_w_spr.bin)
- Archer Male (battle_archer_m_spr.bin)
- Archer Female (battle_archer_w_spr.bin)
- Monk Male (battle_monk_m_spr.bin)
- Monk Female (battle_monk_w_spr.bin)
- Thief Male (battle_thief_m_spr.bin)
- Thief Female (battle_thief_w_spr.bin)
- Chemist Male (battle_item_m_spr.bin)
- White Mage Male (battle_priest_m_spr.bin)
- White Mage Female (battle_priest_w_spr.bin)

## Mapping Discovery Process

1. Run diagnostic tool: `python scripts/diagnostic_sprite.py input.bin output.bin`
2. View generated PNG with rainbow palette to identify sections
3. Document palette indices for each visual section
4. Create section mapping JSON in `ColorMod/Data/SectionMappings/[Job]_[Gender].json`

## Section Mapping Format

```json
{
  "job": "Knight_Male",
  "sprite": "battle_knight_m_spr.bin",
  "sections": [
    {
      "name": "Cape",
      "displayName": "Cape",
      "indices": [10, 9, 8],
      "roles": ["base", "highlight", "shadow"]
    }
  ]
}
```

### Role Definitions

- **base**: Primary color for the section
- **highlight**: Lighter shade for highlights
- **shadow**: Darker shade for shadows
- **accent**: Secondary color for details
- **accent_highlight**: Lighter shade of accent color
- **accent_shadow**: Darker shade of accent color

## Completed Mappings

### Squire Female (battle_mina_w_spr.bin)
- **HeadbandArmsBoots**: indices [4, 5, 3, 7, 6] (base, highlight, shadow, accent, accent_shadow)
- **ChestArmor**: indices [10, 9, 8] (base, highlight, shadow)
- **Hair**: indices [13, 12, 11, 15, 14] - *Too complex, shares pixels with face*

### Squire Male (battle_mina_m_spr.bin)
*TODO: Document mappings*

### Knight Male (battle_knight_m_spr.bin)
- **Cape**: indices [9, 10, 8, 7] (base, highlight, shadow, accent_shadow)
  - *Note: Unusual color arrangement where index 9 (cyan) is base, 10 (purple) is highlight, 8 (dark blue) is shadow, 7 (light blue) is accent_shadow*
- **Underarmor and Sigil (on cape)**: indices [5, 6, 4, 3] (base, highlight, shadow, accent)
  - *Includes yellow sigil on cape (index 3)*
- **Hair, Boots, and Gloves**: indices [11, 12, 13] (base, highlight, shadow)
- **Diagnostic**: `scripts/output/knight_m_diagnostic.png`

### Knight Female (battle_knight_w_spr.bin)
*TODO: Document mappings*

### Archer Male (battle_archer_m_spr.bin)
*TODO: Document mappings*

### Archer Female (battle_archer_w_spr.bin)
*TODO: Document mappings*

### Monk Male (battle_monk_m_spr.bin)
*TODO: Document mappings*

### Monk Female (battle_monk_w_spr.bin)
*TODO: Document mappings*

### Thief Male (battle_thief_m_spr.bin)
*TODO: Document mappings*

### Thief Female (battle_thief_w_spr.bin)
*TODO: Document mappings*

### Chemist Male (battle_item_m_spr.bin)
*TODO: Document mappings*

### Chemist Female (battle_item_w_spr.bin)
- **HoodArms**: indices [9, 10, 8] (base, highlight, shadow)
- **Dress**: indices [5, 6, 7, 4] (base, highlight, accent, shadow)
- **HairPouchBracersBoots**: indices [11, 12, 13] (base, highlight, shadow)
- **Diagnostic**: `scripts/output/chemist_w_diagnostic.png`

### White Mage Male (battle_priest_m_spr.bin)
*TODO: Document mappings*

### White Mage Female (battle_priest_w_spr.bin)
*TODO: Document mappings*

## Mapping Guidelines

### Best Practices
1. **Group related visual elements**: Combine sections that should change color together (e.g., "Headband, Arms & Boots")
2. **Use descriptive display names**: User-facing names should be clear and specific
3. **Document complexities**: Note any sections that are too complex or share pixels with other elements
4. **Verify with diagnostic output**: Always validate mappings against the rainbow diagnostic PNG

### Common Pitfalls
- **Hair sections**: Often share pixels with face/skin tones - usually too complex to isolate
- **Metal trim**: May use same indices across multiple armor pieces
- **Overlapping sections**: Some indices may appear in multiple visual areas due to sprite reuse

## Diagnostic Output Location

All diagnostic sprites and PNGs are stored in `scripts/output/`:
- `[job]_[gender]_diagnostic.bin` - Sprite with rainbow palette
- `[job]_[gender]_diagnostic.png` - Visual preview for index identification

## Next Steps

1. Review diagnostic PNGs for remaining jobs
2. Identify distinct visual sections
3. Map palette indices to sections
4. Create/update JSON mapping files
5. Test in Theme Editor UI
