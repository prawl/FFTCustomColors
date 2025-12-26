# Creating New Ramza Color Themes for FFTColorCustomizer

## Overview
This guide documents the process for creating new color themes for Ramza in Final Fantasy Tactics: The Ivalice Chronicles using the FFTColorCustomizer mod.

## Key Discovery
Ramza's color themes require **TEX files** (texture/UV mapping files), not just palette files. The FFTColorCustomizer mod uses a different system for Ramza than for other characters.

## Required Files
- **TEX Files**: tex_830.bin through tex_839.bin (10 files total)
  - These are the actual texture files that define Ramza's colors
  - Different numbered files correspond to different Ramza chapters/appearances

## Ramza Sprite Color Reference (Complete Analysis)

### Chapter-Specific Color Mappings

#### Chapter 1 Ramza
**What Each Color Controls:**
- **Armor (Blues)** → Main armor (chest/arms)
  - RGB(48, 72, 104) - Main armor
  - RGB(56, 96, 136) - Light armor parts
  - RGB(40, 56, 80) - Dark armor shadows
  - RGB(80, 128, 184) - Bright armor highlights

- **Hair/Skin (Browns)** → Hair/face/under armor shirt/knee centers/back stripes
  - RGB(72, 48, 40) - Dark brown hair
  - RGB(104, 72, 24) - Medium brown
  - RGB(104, 64, 32) - Brown shadows
  - RGB(160, 104, 40) - Light brown
  - RGB(144, 80, 40) - Medium brown

- **Accessories (Golds)** → Gloves/legs/feet
  - RGB(216, 160, 72) - Gold trim
  - RGB(200, 136, 80) - Dark gold
  - RGB(232, 192, 128) - Light gold

- **Outlines (Grays)** → Outlines (hands/feet/legs/eyebrows)
  - RGB(40, 40, 32) - Dark gray/metal
  - RGB(224, 224, 216) - Light gray/silver

#### Chapter 2 Ramza
**What Each Color Controls:**
- **Armor (Purples)** → Main armor (chest/arms)
  - RGB(48, 40, 80) - Dark purple armor
  - RGB(88, 64, 120) - Medium purple armor
  - RGB(128, 96, 200) - Light purple armor

- **Main Features (Browns/Tans)** → Hair/face/gloves
  - RGB(216, 160, 72) - Gold/tan
  - RGB(200, 136, 80) - Dark gold/tan
  - RGB(232, 192, 128) - Light gold/tan
  - RGB(176, 160, 136) - Tan
  - RGB(184, 120, 40) - Brown
  - RGB(72, 64, 48) - Dark brown

- **Accents (Other Browns)** → Accent colors (hair/gloves/legs)
  - RGB(72, 40, 8) - Dark brown accent
  - RGB(112, 88, 24) - Brown accent

- **Outlines (Grays)** → Body outlines (shoulders/arms/legs/feet)
  - RGB(40, 40, 32) - Dark gray
  - RGB(224, 216, 192) - Light gray

#### Chapter 3/4 Ramza
**What Each Color Controls:**
- **Armor (Teals)** → Shoulders/arms/hands/legs/boots
  - RGB(32, 64, 88) - Dark teal armor
  - RGB(40, 96, 120) - Medium teal armor
  - RGB(64, 136, 152) - Light teal armor

- **Main Features (Browns)** → Hair/face/chest under armor
  - RGB(64, 56, 56) - Main brown
  - RGB(72, 40, 8) - Dark brown hair
  - RGB(104, 72, 24) - Medium brown
  - RGB(112, 96, 80) - Under armor brown
  - RGB(128, 56, 8) - Dark tan

- **Accents (Light Browns)** → Hair tips/pelvic under armor/thong/ankle stripes
  - RGB(184, 120, 40) - Hair tips
  - RGB(200, 136, 80) - Under armor accent
  - RGB(232, 192, 128) - Light accent
  - RGB(176, 160, 136) - Pale accent
  - RGB(216, 160, 72) - Bright accent

- **Outlines (Grays)** → Outlines (hands/legs/feet)
  - RGB(40, 40, 32) - Dark gray
  - RGB(224, 224, 216) - Light gray

## Step-by-Step Process

### 1. Extract Original Sprites
Use the FFT Sprite Modding Toolkit from Nexus Mods:
```
1. Install the toolkit
2. Run the extraction process
3. Sprites will be extracted to: C:/Users/[username]/AppData/Local/FFTSpriteToolkit/working/extracted_sprites/
4. Look for files named: 830_Ramuza_Ch1_hd.png through 835_Ramuza_Ch4_hd.png
```

**IMPORTANT**: You also need the original BMP sprite sheets from:
```
C:/Users/[username]/OneDrive/Desktop/FFT_Original_Sprites/
- 830_Ramuza_Ch1_hd.bmp through 835_Ramuza_Ch4_hd.bmp
```

### 2. Create Your Custom Single Sprite (60x74 pixels)
First, create or modify a single Ramza sprite with your desired colors:
- Extract a single sprite from position (62, 2) in the sprite sheet
- Modify colors as desired
- Save as a 60x74 PNG (or 240x296 if 4x scaled)

### 3. Build Color Mapping from Single Sprite to Full Sheet
**This is the critical step for perfect color matching:**

```python
from PIL import Image

def extract_sprite_from_sheet(sheet_path, position=(62, 2)):
    """Extract a single sprite from a sprite sheet."""
    sheet = Image.open(sheet_path).convert('RGBA')
    sprite = sheet.crop((position[0], position[1], position[0] + 60, position[1] + 74))
    return sprite

def build_color_mapping_from_sprites(original_sprite, perfect_sprite):
    """Build exact color mapping by comparing two sprites pixel by pixel."""

    # Resize perfect sprite if needed (from 240x296 to 60x74)
    if perfect_sprite.size == (240, 296):
        perfect_sprite = perfect_sprite.resize((60, 74), Image.NEAREST)

    color_map = {}
    orig_pixels = original_sprite.load()
    perf_pixels = perfect_sprite.load()

    for y in range(74):
        for x in range(60):
            orig_r, orig_g, orig_b, orig_a = orig_pixels[x, y]
            perf_r, perf_g, perf_b, perf_a = perf_pixels[x, y]

            if orig_a > 0 and perf_a > 0:
                orig_color = (orig_r, orig_g, orig_b)
                perf_color = (perf_r, perf_g, perf_b)

                # Store the mapping
                if orig_color not in color_map:
                    color_map[orig_color] = perf_color

    return color_map
```

### 4. Apply Color Mapping to Full Sprite Sheet
Apply the exact pixel-by-pixel color mapping to the entire sprite sheet:

```python
def apply_perfect_mapping(original_sheet_path, perfect_sprite_path, output_path):
    """Apply the perfect color mapping to entire sprite sheet."""

    # Extract original sprite from sheet
    original_sprite = extract_sprite_from_sheet(original_sheet_path)

    # Load perfect sprite
    perfect_sprite = Image.open(perfect_sprite_path).convert('RGBA')

    # Build the color mapping
    color_map = build_color_mapping_from_sprites(original_sprite, perfect_sprite)

    # Apply to full sheet
    sheet = Image.open(original_sheet_path).convert('RGBA')
    pixels = sheet.load()
    width, height = sheet.size

    for y in range(height):
        for x in range(width):
            r, g, b, a = pixels[x, y]

            if a == 0:
                continue

            current_color = (r, g, b)
            if current_color in color_map:
                new_color = color_map[current_color]
                pixels[x, y] = (*new_color, a)

    sheet.save(output_path, 'PNG')
    return sheet
```

### 5. Process All Chapter Sprite Sheets
Apply the mapping to all 6 sprite sheets (2 per chapter):

```python
# Example for white_heretic theme
perfect_sprites = {
    1: "MBG_073_Contrast_3.png",           # Chapter 1 white sprite
    2: "Ramza_Ch23_WhiteArmor_NoDots.png", # Chapter 2-3 white sprite
    4: "Ramza_Ch4_WhiteArmor_BlackUnder.png" # Chapter 4 white sprite
}

sheets = [
    ("830_Ramuza_Ch1_hd.bmp", "830_Ramuza_Ch1_white_heretic.png", 1),
    ("831_Ramuza_Ch1_hd.bmp", "831_Ramuza_Ch1_white_heretic.png", 1),
    ("832_Ramuza_Ch23_hd.bmp", "832_Ramuza_Ch23_white_heretic.png", 2),
    ("833_Ramuza_Ch23_hd.bmp", "833_Ramuza_Ch23_white_heretic.png", 2),
    ("834_Ramuza_Ch4_hd.bmp", "834_Ramuza_Ch4_white_heretic.png", 4),
    ("835_Ramuza_Ch4_hd.bmp", "835_Ramuza_Ch4_white_heretic.png", 4),
]

for original_path, output_path, chapter in sheets:
    apply_perfect_mapping(original_path, perfect_sprites[chapter], output_path)
```

### 6. Convert Modified Sprite Sheets to Game Format
The FFT Sprite Toolkit should handle this conversion:
1. Place the 6 modified PNG sprite sheets in the toolkit's working directory
2. Name them exactly as the originals (830_Ramuza_Ch1_hd.png, etc.)
3. Use the toolkit's "Create Mod Package" feature
4. The toolkit will generate the necessary TEX files

### 7. Locate Generated Files
After the toolkit creates the mod package, find:
- TEX files in: `[ModName]/FFTIVC/data/enhanced/system/ffto/g2d/`
- Files needed: tex_830.bin through tex_839.bin

## Key Discoveries for Perfect Color Matching

### The Problem
When creating custom Ramza sprites, the colors in your individual sprite PNG may not match the full sprite sheet due to:
- Color mapping issues
- Brightness differences
- Incorrect color detection

### The Solution: Pixel-Perfect Color Mapping
1. **Extract the exact sprite** from position (62, 2) in the original sprite sheet
2. **Compare pixel-by-pixel** with your custom sprite
3. **Build an exact color mapping table** (original RGB → new RGB)
4. **Apply this exact mapping** to every pixel in the entire sprite sheet

This ensures that the colors in the full sprite sheet match your custom sprite EXACTLY.

### Example Color Mappings (white_heretic theme)
```
Chapter 2-3 Purple to White:
- RGB(48, 40, 80) → RGB(105, 105, 105)   # Dark purple to dark gray
- RGB(88, 64, 120) → RGB(189, 189, 189)  # Medium purple to light gray
- RGB(128, 96, 200) → RGB(255, 255, 255) # Light purple to pure white
```

### 8. Integrate into FFTColorCustomizer

#### A. Add Theme to Source Files
1. Create theme directory:
   ```
   ColorMod/FFTIVC/data/enhanced/system/ffto/g2d/[theme_name]/
   ```

2. Copy TEX files (tex_830-839.bin) to this directory

3. Update `ColorMod/Data/StoryCharacters.json`:
   ```json
   {
     "name": "RamzaChapter1",
     "spriteNames": ["ramuza"],
     "defaultTheme": "original",
     "availableThemes": [
       "original",
       "white_heretic",
       "your_new_theme"  // Add your theme name here
     ]
   }
   ```
   Add the theme to all three Ramza entries (RamzaChapter1, RamzaChapter2, RamzaChapter34)

#### B. Build and Deploy
1. Run BuildLinked.ps1 to build the mod
2. The script needs to be updated to copy TEX files to RamzaThemes folder:
   - TEX files should go to: `[ModPath]/RamzaThemes/[theme_name]/`

#### C. Manual Deployment (if BuildLinked.ps1 doesn't copy files)
Copy TEX files directly to:
```
C:/Program Files (x86)/Steam/steamapps/common/FINAL FANTASY TACTICS - The Ivalice Chronicles/Reloaded/Mods/FFTColorCustomizer/RamzaThemes/[theme_name]/
```

### 6. Test in Game
1. Enable FFTColorCustomizer in Reloaded-II
2. Launch the game
3. Press F1 to open the mod configuration menu
4. Find RamzaChapter1/2/34 in the menu
5. Select your new theme from the dropdown
6. You may need to reload the game for changes to take effect

## File Structure Summary

### Source Structure (Development)
```
ColorMod/
├── Data/
│   └── StoryCharacters.json  (theme registration)
└── FFTIVC/
    └── data/
        └── enhanced/
            └── system/
                └── ffto/
                    └── g2d/
                        └── [theme_name]/
                            ├── tex_830.bin
                            ├── tex_831.bin
                            ├── ...
                            └── tex_839.bin
```

### Deployed Structure (In Mod)
```
FFTColorCustomizer/
├── Data/
│   └── StoryCharacters.json
└── RamzaThemes/
    └── [theme_name]/
        ├── tex_830.bin
        ├── tex_831.bin
        ├── ...
        └── tex_839.bin
```

## Important Notes

1. **TEX Files are Essential**: Palette files alone won't work for Ramza. You need the modified TEX files.

2. **File Numbering**:
   - tex_830-831: Chapter 1 Ramza
   - tex_832-833: Chapter 2-3 Ramza
   - tex_834-835: Chapter 4 Ramza
   - tex_836-839: Additional variations

3. **Color Quality**: Direct color replacement can look rough. For better results:
   - Use proper color theory for replacements
   - Consider hue/saturation adjustments instead of direct RGB swaps
   - Test different color combinations for visual appeal

4. **Testing**: Always test with a fresh game load after making changes. The mod may cache textures.

## Troubleshooting

- **Ramza appears unchanged**: Make sure TEX files are in RamzaThemes/[theme_name]/, not in fftpack/
- **Theme doesn't appear in menu**: Check that StoryCharacters.json was updated correctly
- **Colors look wrong**: The original color detection in your script may need adjustment
- **Game crashes**: TEX files may be corrupted or wrong format - regenerate from toolkit

## Working Example: Blue Knight Theme
Successfully tested configuration:
- 10 TEX files (tex_830-839.bin) extracted from customspritestest mod
- Placed in RamzaThemes/blue_knight/
- Added "blue_knight" to StoryCharacters.json availableThemes arrays
- Theme appears in F1 menu and successfully changes Ramza's colors to blue