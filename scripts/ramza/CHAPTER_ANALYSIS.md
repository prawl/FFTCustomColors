# Ramza Chapter Sprite Analysis

## Overview
Analysis of Ramza's sprite evolution across FFT chapters, based on isolation tests.

## Chapter 1 (Files: 830, 831)
**Theme**: Young squire with blue leather armor

### Color Zones
- **Jacket**: Blue leather armor (b > r and b > g and b > 70)
- **Accessories**: Brown leather (gloves, boots, belt)
- **Under-layer**: Tan/beige shirt and pants
- **Hair**: Brown with highlights (preserved, Y < 35)

### Detected Pieces
- Jacket: ~450 pixels
- Gloves: Edges at X < 28 or X > 35 (Y: 35-50)
- Boots: Y >= 55
- Belt: Y: 45-55, darker browns
- Arm stripes: Center area (Y: 35-50)

## Chapter 2/3 (Files: 832, 833)
**Theme**: Mercenary with purple armor and metal plating

### Color Zones
- **Armor**: Purple/violet main armor (blues: 442 pixels)
- **Metal**: Gray/silver plating and chainmail (grays: 185 pixels)
- **Leather**: Brown accessories reduced (browns: 633 pixels)
- **Hair**: Same as Ch1 (preserved)

### Detected Pieces
- Main armor: Purple chest piece and shoulders
- Metal components: Shoulder guards, gauntlets
- Gloves: 24 pixels (much less than Ch1)
- Boots: 253 pixels (metal boots)
- Belt: 41 pixels
- Arm stripes: 131 pixels
- Under-layer: 75 pixels (less visible)

### Key Differences from Ch1
- Purple replaces blue for main armor
- Introduction of gray/silver metal elements
- Less exposed under-layer (more armor coverage)
- Metal gauntlets instead of leather gloves

## Chapter 4 (Files: 834, 835)
**Theme**: Knight with full teal/cyan plate armor

### Color Zones
- **Armor**: Teal/cyan full plate (blues: 593 pixels, most coverage)
- **Metal**: Extensive gray/silver (grays: 349 pixels)
- **Accents**: Orange/brown trim (browns: 309 pixels, least of all chapters)
- **Hair**: Same as previous (preserved)

### Detected Pieces
- Full plate armor: 593 pixels (highest armor coverage)
- Metal components: Extensive plating
- Boots: 46 pixels (full metal)
- Arm coverage: 120 pixels
- Under-layer: 8 pixels (minimal exposure)
- No separate gloves detected (integrated armor)

### Key Differences from Ch2/3
- Cyan/teal replaces purple
- Maximum armor coverage (593 vs 442 pixels)
- Most metal elements (349 vs 185 pixels)
- Least exposed browns/leather (309 vs 633 pixels)
- Orange accents on armor trim
- Fully integrated armor (no separate gloves)

## Sprite Position Constants (All Chapters)
```python
sprite_x = 62
sprite_y = 2
sprite_width = 60
sprite_height = 74
```

## Color Detection Patterns

### Universal Patterns
- **Blues**: Main armor color (varies by chapter)
- **Browns**: Leather/fabric elements
- **Grays**: Metal armor pieces
- **Skin tones**: Always preserved
- **Hair zone**: Y < 35 (never modified)

### Chapter-Specific RGB Profiles
```python
# Chapter 1: Blue leather
armor_detection = "b > r and b > g and b > 70"

# Chapter 2/3: Purple armor
# Similar detection but purple hues

# Chapter 4: Cyan/teal armor
# Stronger blue component with green tint
```

## Theme Generation Implications

### Chapter 1 Themes
- 3-zone system works perfectly
- Clear separation between jacket/under-layer/accessories
- Browns easily distinguishable by position

### Chapter 2/3 Themes
- Need to handle metal (gray) elements separately
- Purple armor base instead of blue
- Less under-layer visibility
- Metal gauntlets vs leather gloves

### Chapter 4 Themes
- Minimal brown/leather elements
- Orange accents need special handling
- Full armor coverage means less zone distinction
- May need 4-zone system: armor/metal/accents/minimal under-layer

## Recommendations for Theme Generators

1. **Chapter 2/3 Generator**
   - Add gray/metal color zone
   - Reduce under-layer modifications (less visible)
   - Handle purple base colors
   - Account for metal gauntlets

2. **Chapter 4 Generator**
   - Focus on armor and metal colors
   - Minimize accessory color changes (integrated armor)
   - Add orange accent color control
   - Consider armor trim as separate zone

3. **Universal Considerations**
   - Hair threshold (Y < 35) works for all chapters
   - Sprite dimensions are consistent
   - Position-based detection still valid
   - Each chapter needs unique color detection thresholds