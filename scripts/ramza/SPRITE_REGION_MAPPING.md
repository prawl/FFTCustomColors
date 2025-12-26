# Sprite Region Mapping Documentation

## Overview
This document describes how to pinpoint specific armor pieces and clothing regions on Ramza's Chapter 1 sprite using position-based detection rather than just color-based detection.

## Sprite Sheet Coordinates

### Base Sprite Location (SW-facing Ramza)
```
sprite_x = 62   # X position in sprite sheet
sprite_y = 2    # Y position in sprite sheet
sprite_width = 60
sprite_height = 74
```

These coordinates extract the southwest-facing Ramza sprite from the full 512x512 sprite sheet.

## Y-Position Regions (Relative to Sprite Top)

### Critical Y-Thresholds
```python
HAIR_Y_MAX = 35         # Hair/head area (Y < 35)
GLOVES_BOOTS_Y_MIN = 35 # Gloves/boots start (Y >= 35)
```

### Detailed Y-Position Breakdown

| Y Range | Region | Contains |
|---------|--------|----------|
| 0-20 | Head Top | Hair (top portion) |
| 20-35 | Head Bottom | Hair (sides), face, collar |
| 35-45 | Arms/Hands | Gloves, arm stripes |
| 45-55 | Waist | Belt, open jacket bottom |
| 55-74 | Legs/Feet | Boots, leg stripes |

## Color Detection by Region

### Zone 1: Jacket (Blue Armor)
- **Color Detection**: `b > r and b > g and b > 70`
- **Position**: All Y positions (full height)
- **Shading Variations**:
  - Light blue (b > 120): Main armor surface
  - Medium blue (b > 90): Armor accents
  - Dark blue (b <= 90): Armor shadows

### Zone 2: Under-layer (Shirt/Pants)
- **Color Detection**: Browns/tans with specific conditions
- **Primary Detection**: `r > g and g >= b and r - b > 15`
- **Secondary Detection**: `r > g and g > b and 100 < r < 220 and 60 < g < 180`
- **Position Filtering**:
  - Y: 35-50 (arm stripes)
  - Y: 50-55 (open jacket area)
  - Y: 55-74 (pants/leg area)
- **Intensity**: Generally lighter browns (r >= 100)

### Zone 3: Accessories (Gloves/Boots/Belt)
- **Color Detection**: Same brown detection as Zone 2
- **Position-Based Identification**:
  ```python
  # Gloves (Y: 35-50)
  if 35 <= rel_y < 50:
      if rel_x < 15 or rel_x > 45:  # Edges of arms
          # This is a glove

  # Belt (Y: 45-55)
  elif 45 <= rel_y < 55:
      if r < 100:  # Darker browns
          # This is belt

  # Boots (Y: 55+)
  elif rel_y >= 55:
      if r < 100:  # Darker browns
          # This is boots
  ```

## X-Position Considerations

### Arm/Glove Detection
- **Left arm/glove**: X: 5-20
- **Right arm/glove**: X: 40-55
- **Center body**: X: 20-40

### Boot Detection
- **Left boot**: X < 30
- **Right boot**: X >= 30

## Color Preservation Rules

### Always Preserve (Never Change)
```python
preserve_colors = [
    (216, 160, 72),   # Main skin tone
    (200, 136, 80),   # Medium skin
    (232, 192, 128),  # Light skin
    (224, 224, 216),  # Eyes
    (184, 120, 40),   # Hair highlight
]
```

### Hair Protection
- **Rule**: Any brown color where Y < 35 is considered hair
- **Implementation**: Skip color changes for browns in this region

## Implementation Pattern

```python
def process_pixel(x, y, r, g, b, a):
    # Get relative position
    rel_x = x - sprite_x
    rel_y = y - sprite_y

    # Skip if outside sprite bounds
    if not (0 <= rel_x < sprite_width and 0 <= rel_y < sprite_height):
        return

    # Skip transparent
    if a == 0:
        return

    # Skip preserved colors (skin/eyes)
    if (r, g, b) in preserve_colors:
        return

    # Skip hair area for browns
    if rel_y < HAIR_Y_MAX and is_brown(r, g, b):
        return

    # Process by zone
    if is_blue(r, g, b):
        # Zone 1: Jacket
        apply_jacket_color()
    elif is_brown(r, g, b):
        if is_glove_position(rel_x, rel_y):
            # Zone 3: Gloves
            apply_accessory_color()
        elif is_boot_position(rel_y):
            # Zone 3: Boots
            apply_accessory_color()
        elif is_belt_position(rel_y, r):
            # Zone 3: Belt
            apply_accessory_color()
        else:
            # Zone 2: Under-layer
            apply_underlayer_color()
```

## Testing Methodology

### Grid-Based Testing
Divide the sprite into a 3x4 grid to identify which grid cells contain which armor pieces:
- Top row (Y: 0-18): Head/shoulders
- Second row (Y: 18-36): Upper body/arms
- Third row (Y: 36-54): Lower body/hands
- Bottom row (Y: 54-74): Legs/feet

### Color Intensity Testing
Use different colors for different brown intensities:
- Dark browns (r < 100): Usually accessories
- Medium browns (100 <= r < 150): Mixed use
- Light browns (r >= 150): Usually under-layer

### Position-Specific Testing
Create tests that color pixels differently based on exact Y position:
```python
if rel_y < 20:
    color = RED  # Head area
elif rel_y < 35:
    color = GREEN  # Lower head/collar
elif rel_y < 50:
    color = BLUE  # Arms/hands
else:
    color = YELLOW  # Legs/boots
```

## Key Discoveries

1. **Hair vs Gloves**: Both use similar brown colors, but hair is Y < 35
2. **Arm Stripes**: Extended browns that run along arms (Y: 35-50, center X)
3. **Open Jacket**: The tan/brown showing at bottom of jacket (Y: 45-55)
4. **Belt**: Darker browns at waist level (Y: 45-55, r < 100)
5. **Boot vs Pants**: Boots are darker browns at Y > 55

## Three-Zone Color System

### Final Implementation
1. **Zone 1 (Jacket)**: All blue pixels regardless of position
2. **Zone 2 (Under-layer)**: Lighter browns/tans in arm, waist, leg areas
3. **Zone 3 (Accessories)**: Darker browns at glove, belt, boot positions

### Benefits
- Hair remains original color
- Each clothing layer independently controllable
- Position-based detection prevents color bleeding
- Works across all Ramza chapter variants