# Ramza Texture Color Guide

This document contains verified color mappings for Ramza's sprite sheets across all chapters. These colors have been tested and confirmed to target specific sprite elements without affecting face, hair, or skin.

## Quick Reference

### Chapter 2/3 (Purple Armor) - Most Detailed

| Element | Color RGB | Hex | Pixels | Notes |
|---------|-----------|-----|--------|-------|
| **Armor - Dark** | (48, 40, 80) | #302850 | 9,610 | Main armor shadow |
| **Armor - Medium** | (88, 64, 120) | #584078 | 6,224 | Main armor body |
| **Armor - Light** | (128, 96, 200) | #8060c8 | 2,604 | Armor highlights |
| **Dark Accent** | (40, 40, 32) | #282820 | 8,360 | Shadow/outline |
| **Boots - Outline** | (72, 64, 48) | #484030 | 4,496 | Darkest boot edge |
| **Boots - Fill** | (112, 96, 80) | #706050 | 4,316 | Mid-tone boot |
| **Boots - Primary** | (176, 160, 136) | #b0a088 | 2,641 | Main boot/glove center |
| **Boots - Highlight** | (224, 216, 192) | #e0d8c0 | 296 | Tiny center highlight |
| **Under Armor - Dark** | (72, 40, 8) | #482808 | 3,912 | Under armor shadow |
| **Under Armor - Light** | (128, 56, 8) | #803808 | 869 | Under armor highlight |

**Total Boots/Gloves:** 11,749 pixels (all 4 colors)
**Total Under Armor:** 4,781 pixels (both colors)

---

## Detailed Color Mappings

### Chapter 1 (Blue Armor)

#### Armor Colors (SAFE TO EDIT)
| Element | RGB | Pixels |
|---------|-----|--------|
| Main armor blue | (48, 72, 104) | 6,164 |
| Light armor blue | (56, 96, 136) | 5,421 |
| Dark armor blue | (40, 56, 80) | 5,217 |
| Bright armor blue | (80, 128, 184) | 3,473 |
| Dark accent | (40, 40, 32) | 3,462 |

#### Boots/Gloves Colors (SAFE TO EDIT)
| Element | RGB | Pixels | Role |
|---------|-----|--------|------|
| Boots outer | (72, 48, 40) | 5,757 | Outer accent/edge |
| Boots accent | (104, 64, 32) | 3,552 | Mid-tone accent |
| Boots main | (144, 80, 40) | 2,910 | Main boot color |
| Boots highlight | (224, 224, 216) | 41 | Tiny highlight |

**Total Boots/Gloves:** 12,260 pixels (all 4 colors)

**Design Note:** Chapter 1 boots use warm brown tones. The hierarchy is:
1. Main (144, 80, 40) - brightest, most prominent
2. Accent (104, 64, 32) - mid-tone
3. Outer (72, 48, 40) - darkest edge
4. Highlight (224, 224, 216) - tiny accent spot

---

### Chapter 2/3 (Purple Armor)

#### Armor Colors (SAFE TO EDIT)
| Element | RGB | Pixels |
|---------|-----|--------|
| Dark purple armor | (48, 40, 80) | 9,610 |
| Medium purple armor | (88, 64, 120) | 6,224 |
| Light purple armor | (128, 96, 200) | 2,604 |
| Dark accent | (40, 40, 32) | 8,360 |

#### Boots/Gloves Colors (SAFE TO EDIT)
| Element | RGB | Pixels | Role |
|---------|-----|--------|------|
| Boots outline | (72, 64, 48) | 4,496 | Darkest edge/outline |
| Boots fill | (112, 96, 80) | 4,316 | Mid-tone fill |
| Boots primary | (176, 160, 136) | 2,641 | Main center color |
| Boots highlight | (224, 216, 192) | 296 | Tiny center highlight spot |

**Design Note:** When creating boot themes, treat (176, 160, 136) as the PRIMARY color (most visible), with the others as supporting shades. The hierarchy is:
1. Primary (176, 160, 136) - brightest, most prominent
2. Fill (112, 96, 80) - mid-tone
3. Outline (72, 64, 48) - darkest edge
4. Highlight (224, 216, 192) - tiny accent spot

#### Under Armor Colors (SAFE TO EDIT)
| Element | RGB | Pixels |
|---------|-----|--------|
| Dark under armor | (72, 40, 8) | 3,912 |
| Light under armor | (128, 56, 8) | 869 |

---

### Chapter 4 (Teal Armor)

#### Armor Colors (SAFE TO EDIT)
| Element | RGB | Pixels |
|---------|-----|--------|
| Dark teal armor | (32, 64, 88) | 8,761 |
| Medium teal armor | (40, 96, 120) | 6,350 |
| Light teal armor | (64, 136, 152) | 3,272 |
| Dark accent | (40, 40, 32) | 6,941 |

#### Chest/Upper Body Colors (SAFE TO EDIT)
| Element | RGB | Pixels | Notes |
|---------|-----|--------|-------|
| Chest top portion | (64, 56, 56) | 6,424 | Upper chest area |

#### Straps/Thigh Colors (SAFE TO EDIT) - Covers Boots, Chest, Legs, and Thighs
| Element | RGB | Pixels | Notes |
|---------|-----|--------|-------|
| Straps - Fill | (112, 96, 80) | 2,018 | All straps on boots/chest/legs + thigh area |
| Straps - Highlight | (176, 160, 136) | 1,283 | Strap highlights on boots/chest/legs + thigh area |
| Thigh - Highlight | (224, 224, 216) | 198 | Small thigh/rear highlight accent |

**Design Note:** In Chapter 4, the strap colors `(112, 96, 80)` and `(176, 160, 136)` cover ALL straps across the sprite - including boot straps, chest straps, leg straps, and thigh areas. The `(224, 224, 216)` is a small highlight on the thighs. These should be styled together for visual consistency.

#### Under Armor Colors (SAFE TO EDIT)
| Element | RGB | Pixels |
|---------|-----|--------|
| Under armor dark | (72, 40, 8) | 3,854 |
| Under armor light | (128, 56, 8) | 2,122 |

---

## DO NOT EDIT - Protected Colors

These colors are shared with skin, hair, and gold trim. Changing them will affect Ramza's face and appearance.

| Element | RGB | Hex | Pixels (Ch2/3) |
|---------|-----|-----|----------------|
| Background | (0, 0, 0) | #000000 | 204,786 |
| Skin/Gold primary | (216, 160, 72) | #d8a048 | 4,646 |
| Hair/Skin | (184, 120, 40) | #b87828 | 2,759 |
| Gold accent | (200, 136, 80) | #c88850 | 2,556 |
| Hair brown | (112, 88, 24) | #705818 | 2,345 |
| Skin highlight | (232, 192, 128) | #e8c080 | 1,724 |
| Hair tips (Ch4) | (104, 72, 24) | #684818 | 2,480 |
| Hair (Ch1) | (104, 72, 24) | #684818 | 4,118 |
| Hair (Ch1) | (160, 104, 40) | #a06828 | 3,064 |

---

## Professional Theme Design Guidelines

### Boot/Glove Palettes (Natural Colors)

These are recommended natural leather/cloth colors for boots:

| Style | Outline | Fill | Primary | Highlight |
|-------|---------|------|---------|-----------|
| **Dark Leather** | (56, 42, 32) | (88, 68, 52) | (120, 96, 72) | (152, 128, 104) |
| **Brown Leather** | (72, 56, 40) | (104, 84, 64) | (136, 112, 88) | (168, 144, 120) |
| **Tan Leather** | (96, 76, 56) | (128, 104, 80) | (160, 136, 112) | (192, 172, 152) |
| **Gray Cloth** | (64, 64, 60) | (96, 96, 88) | (128, 128, 120) | (168, 168, 160) |
| **Dark Gray** | (48, 48, 44) | (72, 72, 68) | (100, 100, 96) | (136, 136, 132) |
| **Warm Brown** | (80, 56, 40) | (112, 88, 64) | (144, 120, 96) | (184, 160, 136) |

### Under Armor Guidelines

Under armor should complement the main armor color:
- Use contrasting colors for visual interest
- Keep values similar to maintain readability
- Consider the overall color harmony

### Color Harmony Examples

| Theme | Armor | Under Armor | Boots |
|-------|-------|-------------|-------|
| Golden Knight | Warm golds | Crimson red | Dark leather |
| Silver Knight | Cool silvers | Navy blue | Dark gray |
| Frost Warden | Ice blues | White/cream | Gray cloth |
| Shadow Knight | Obsidian black | Blood red | Very dark brown |

---

## Source Files

- **Chapter 1:** `ColorMod/Images/RamzaChapter1/original/830_Ramuza_Ch1.bmp`
- **Chapter 2/3:** `ColorMod/Images/RamzaChapter23/original/832_Ramuza_Ch23.bmp`
- **Chapter 4:** `ColorMod/Images/RamzaChapter4/original/834_Ramuza_Ch4.bmp`

## Test Scripts

Located in `scripts/ramza/`:
- `analyze_colors.py` - Analyze all colors in sprites
- `test_under_armor_only.py` - Test under armor targeting
- `test_boots_only.py` - Test boots/gloves targeting (2 colors)
- `test_complete_boots.py` - Test all 4 boot colors
- `test_boot_center.py` - Identify boot center colors
- `test_ch4_accessories.py` - Test Chapter 4 chest straps and accessories
- `generate_armor_themes.py` - Main theme generator

---

## Version History

- **v1.2** (2024-12-27): Complete Chapter 4 color identification
  - Confirmed thigh highlight (224, 224, 216) - 198 pixels
  - Added hair tips (104, 72, 24) to protected colors list
  - Complete Chapter 4 editable colors now documented

- **v1.1** (2024-12-27): Added verified Chapter 4 strap colors
  - Confirmed chest top color (64, 56, 56) - 6,424 pixels
  - Confirmed strap colors (112, 96, 80) and (176, 160, 136) cover boots/chest/legs/thighs
  - Key finding: Chapter 4 strap colors are shared across boots, chest, leg straps, and thigh areas

- **v1.0** (2024-12-27): Initial documentation with verified Chapter 2/3 colors
  - Confirmed 4 boot/glove colors (11,749 pixels total)
  - Confirmed 2 under armor colors (4,781 pixels total)
  - Documented protected skin/hair colors
