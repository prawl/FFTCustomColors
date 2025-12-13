# Generic Sprite Palette Mapping System

This directory contains tools for analyzing and testing the color palettes of generic job sprites in Final Fantasy Tactics. These scripts help identify which palette indices control different sprite elements (armor, cape, trim, etc.) to enable precise theme creation.

## 🎯 Purpose

Generic job sprites (Knight, Archer, Monk, etc.) share similar palette structures but have different visual elements. Understanding the palette mapping allows for:

- **Precise theme creation** - Know exactly which indices to modify
- **Consistent styling** - Apply themes across all generic jobs
- **Avoid unintended changes** - Keep hair/skin colors intact
- **Efficient workflow** - Focus modifications on relevant indices

## 📁 Files

### `extract_original_colors.py`
Extracts and displays the original palette from generic sprite files.

**Features:**
- Shows RGB values for each palette index (0-15)
- Displays both 16-bit and 8-bit color values
- Analyzes multiple generic sprites at once
- Provides hex color codes for reference

**Usage:**
```bash
python extract_original_colors.py
```

**Output:**
- Console display of all palette colors
- Analysis of common patterns across sprites
- Recommendations for testing strategy

### `create_simple_color_test.py`
Creates test sprites with 4 distinct color groups to identify major sprite regions.

**Features:**
- Applies 4 color groups to identify major elements
- Tests multiple generic job sprites
- Creates reference documentation
- Focuses on practical theme creation needs

**Color Groups:**
- **Indices 0-2:** BLACK/GRAY (shadows/outlines)
- **Indices 3-6:** RED (main armor/clothing)
- **Indices 7-10:** GREEN (secondary elements/undergarments)
- **Indices 11-15:** BLUE (cape/additional details)

**Usage:**
```bash
python create_simple_color_test.py
```

**Output:**
- Test sprites in `sprites_generic_test/` directory
- `COLOR_REFERENCE.txt` file for documenting observations

### `create_detailed_color_test.py`
Creates test sprites with unique colors for each palette index for granular analysis.

**Features:**
- 16 distinct colors for precise index identification
- Focuses on key sprites (Knight, Archer, Monk, Priest)
- Detailed reference file with observation templates
- Comprehensive analysis framework

**Usage:**
```bash
python create_detailed_color_test.py
```

**Output:**
- Test sprites in `sprites_generic_detailed_test/` directory
- `DETAILED_COLOR_REFERENCE.txt` with observation templates

## 🔬 Testing Workflow

### Phase 1: Extract Original Colors
```bash
python extract_original_colors.py
```
- Understand the base palette structure
- Identify existing color ranges
- Plan testing approach

### Phase 2: Simple Color Test
```bash
python create_simple_color_test.py
```
- Deploy with `BuildLinked.ps1`
- Test in-game with generic job units
- Use F2 to cycle to test theme
- Document which parts show RED, GREEN, BLUE

### Phase 3: Detailed Analysis
```bash
python create_detailed_color_test.py
```
- Use detailed test for precise identification
- Document each index's function
- Create comprehensive mapping

### Phase 4: Theme Creation
- Use findings to create targeted themes
- Focus modifications on identified indices
- Test themes across multiple job sprites

## 🎨 Palette Index Guidelines

### Recommended Testing Priority
1. **battle_knight_m_spr.bin** - Clearest armor distinction
2. **battle_archer_m_spr.bin** - Different armor style
3. **battle_monk_m_spr.bin** - Minimal armor, more cloth
4. **battle_priest_m_spr.bin** - Robes instead of armor

### Expected Palette Structure
Based on analysis of existing themes and sprite patterns:

#### Shadow/Outline Indices (Usually 0-2)
- **Index 0:** Deep black outlines
- **Index 1:** Dark shadow areas
- **Index 2:** Medium shadow transitions

#### Main Armor/Clothing (Usually 3-6)
- **Index 3:** Primary armor color
- **Index 4:** Armor highlights/edges
- **Index 5:** Armor accents/details
- **Index 6:** Armor trim/borders

#### Secondary Elements (Usually 7-10)
- **Index 7:** Undergarments/cloth base
- **Index 8:** Cloth highlights
- **Index 9:** Fabric trim/accents
- **Index 10:** Secondary details

#### Cape/Accessories (Usually 11-15)
- **Index 11:** Cape/cloak base color
- **Index 12:** Cape shadows/folds
- **Index 13:** Metal buckles/hardware
- **Index 14:** Cape trim/edges
- **Index 15:** Cape highlights/accents

### ⚠️ Caution Indices
Some indices may control hair, skin, or other elements that should remain unchanged:
- Test thoroughly before including in themes
- Document any unintended effects
- Consider excluding problematic indices from themes

## 📊 Documentation Templates

### Simple Test Observations
After running simple color test, document:
- Which parts show **RED** (main armor)
- Which parts show **GREEN** (secondary elements)
- Which parts show **BLUE** (cape/details)
- Any unexpected color locations

### Detailed Test Analysis
For each sprite, document:
- **Helmet/Head armor:** Controlled by indices ___
- **Chest armor:** Controlled by indices ___
- **Leg armor:** Controlled by indices ___
- **Cape/cloak:** Controlled by indices ___
- **Undergarments:** Controlled by indices ___
- **Metal/buckles:** Controlled by indices ___

## 🎯 Theme Creation Guidelines

### Best Practices
1. **Start with Knight sprites** - Clearest element distinction
2. **Test across multiple jobs** - Ensure consistency
3. **Avoid hair/skin indices** - Focus on armor/clothing
4. **Use grouped modifications** - Change related indices together
5. **Test edge cases** - Female variants, different jobs

### Common Index Groups for Themes
Based on testing results, group indices for cohesive themes:
- **Primary group:** Main armor indices
- **Secondary group:** Trim/accent indices
- **Cape group:** Cloak/cape indices
- **Metal group:** Buckle/hardware indices

### Validation Process
1. Test theme with primary sprite (Knight_M)
2. Validate across other job sprites
3. Check for unintended changes (hair/skin)
4. Adjust index selection if needed
5. Document final index mapping

## 💡 Tips & Tricks

### Efficient Testing
- Use the simple test first to understand major regions
- Focus detailed testing on the most important sprites
- Document findings immediately while testing
- Take screenshots for reference

### Color Selection for Themes
- Use high contrast colors for testing
- Choose harmonious colors for final themes
- Consider the original sprite's color relationships
- Test themes in different lighting conditions

### Troubleshooting
- **Colors don't appear:** Check if indices are used by the sprite
- **Wrong parts colored:** Review index assignments
- **Theme looks broken:** Verify all related indices are included
- **Inconsistent across jobs:** Some jobs may use different structures

---

## 📊 ACTUAL TEST RESULTS

### Monk Sprite Analysis (Simple Test - 4 Color Groups)
**Date:** December 12, 2024
**Test:** sprites_generic_test with 4-color grouping

**Observed Color Mapping:**
- **BLACK/GRAY (Indices 0-2):** Shadows/outlines (working as expected)
- **RED (Indices 3-6):** Pants/lower body clothing
- **GREEN (Indices 7-10):** Headband, vest, back of shoes (accessories & secondary clothing)
- **BLUE (Indices 11-15):** Hair, face/skin, chest skin, arms, fists, front of shoes

### ⚠️ CRITICAL DISCOVERY - INDICES 11-15 CONTAIN HAIR AND SKIN!

**Important Finding:** Unlike expected, indices 11-15 on the Monk sprite control:
- Hair color
- Skin tone (face, chest, arms, fists)
- Some shoe details

**This means:**
- **DO NOT MODIFY indices 11-15** when creating armor themes for Monks
- These indices must preserve original values to maintain character appearance
- Focus theme modifications on indices 3-10 only

### Safe Modification Zones for Monk
- **Indices 0-2:** Can adjust for shadow/outline tweaks
- **Indices 3-6:** PRIMARY - Safe for pants/lower clothing themes
- **Indices 7-10:** SECONDARY - Safe for accessories/vest themes
- **Indices 11-15:** FORBIDDEN - Contains hair/skin (do not modify!)

---

### Thief Sprite Analysis (Simple Test - 4 Color Groups)
**Date:** December 12, 2024
**Test:** sprites_generic_test with 4-color grouping

**Observed Color Mapping:**
- **BLACK/GRAY (Indices 0-2):** Shadows/outlines (working as expected)
- **RED (Indices 3-6):** Hat, vest, arm bands (main clothing/accessories)
- **GREEN (Indices 7-10):** Pants, chest armor (secondary clothing/armor)
- **BLUE (Indices 11-15):** Face/skin, feather from hat, arms, fists, boots

### ⚠️ CONFIRMED - INDICES 11-15 ALSO CONTAIN SKIN FOR THIEF!

**Important Finding:** Similar to Monk, indices 11-15 on the Thief sprite control:
- Skin tone (face, arms, fists)
- Hat feather decoration
- Boot colors

**This confirms a pattern:**
- Jobs with exposed skin use indices 11-15 for skin tones
- These indices must be preserved to maintain character appearance

### Safe Modification Zones for Thief
- **Indices 0-2:** Can adjust for shadow/outline tweaks
- **Indices 3-6:** PRIMARY - Safe for hat/vest/armbands themes
- **Indices 7-10:** SECONDARY - Safe for pants/chest armor themes
- **Indices 11-15:** MIXED - Contains skin (preserve) but also boots/feather (can modify carefully)

---

### Knight Sprite Analysis (Simple Test - 4 Color Groups)
**Date:** December 12, 2024
**Test:** sprites_generic_test with 4-color grouping

**Observed Color Mapping:**
- **BLACK/GRAY (Indices 0-2):** Shadows/outlines (working as expected)
- **RED (Indices 3-6):** Chest armor, emblem outline, some under armor sections
- **GREEN (Indices 7-10):** Cape, under armor between boots and chest armor
- **BLUE (Indices 11-15):** Hair, head/helmet, fists/gloves, boots, chest emblem

### ⚠️ DIFFERENT PATTERN - KNIGHT USES INDICES 11-15 FOR HAIR + ARMOR!

**Important Finding:** Unlike Monk/Thief, Knight's indices 11-15 control:
- Hair (still present like other jobs)
- Helmet/head armor
- Gloves/gauntlets
- Boots
- Chest emblem detail

**Key Observations:**
- Knights still have hair in indices 11-15 (must be careful)
- But also have armor pieces in these indices (boots, gloves, emblem)
- Cape is in GREEN group (indices 7-10)
- Main chest armor is in RED group (indices 3-6)

### Safe Modification Zones for Knight
- **Indices 0-2:** Can adjust for shadow/outline tweaks
- **Indices 3-6:** PRIMARY - Main chest armor (safe to modify)
- **Indices 7-10:** SECONDARY - Cape and under armor (safe to modify)
- **Indices 11-15:** MIXED - Contains hair BUT also armor pieces (modify carefully, preserve hair colors)

---

## 🎯 SUMMARY OF FINDINGS

### Overall Pattern Discovery
Based on testing Knight, Monk, and Thief sprites:

**Indices 0-2: SHADOWS/OUTLINES**
- Consistent across all jobs
- Safe to modify for darker/lighter outlines

**Indices 3-6: PRIMARY ARMOR/CLOTHING**
- Knight: Main chest armor, emblem outline
- Monk: Pants/lower body clothing
- Thief: Hat, vest, arm bands
- **SAFE TO MODIFY** for all jobs

**Indices 7-10: SECONDARY ELEMENTS**
- Knight: Cape, under armor
- Monk: Headband, vest, shoe backs
- Thief: Pants, chest armor
- **SAFE TO MODIFY** for all jobs

**Indices 11-15: MIXED CONTENT - CAUTION REQUIRED**
- ALL JOBS: Contains hair colors (must preserve)
- Knight: Also has helmet, gloves, boots, emblem
- Monk: Skin tones (face, chest, arms) + shoe fronts
- Thief: Skin tones (face, arms) + boots, hat feather

### Recommended Theme Creation Strategy

1. **Universal Safe Zone (Indices 3-10):**
   - Modify these freely for armor/clothing themes
   - Will work consistently across all generic jobs

2. **Conditional Zone (Indices 11-15):**
   - For Knights: Can partially modify (avoid hair indices)
   - For Monk/Thief: Avoid entirely (preserves skin tones)
   - Consider job-specific theme variants if needed

3. **Best Practice:**
   - Create themes using only indices 3-10 for maximum compatibility
   - This ensures themes work across all jobs without affecting hair/skin
   - Use indices 0-2 sparingly for shadow adjustments

## 🔗 Related Files

- `/scripts/beowulf/` - Similar analysis for Beowulf sprites
- `CLAUDE/RESEARCH.md` - General research on sprite palette structures
- Theme creation scripts in main project directory

## 📝 Contributing

When documenting your findings:
1. Update this README with confirmed index mappings
2. Add sprite-specific notes for exceptions
3. Document any new patterns discovered
4. Share successful theme index combinations

---

**Last Updated:** December 2024
**Primary Test Sprite:** battle_knight_m_spr.bin
**Focus:** Generic job sprite palette mapping for theme creation