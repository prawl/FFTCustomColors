# Creating New Ramza Color Themes for FFTColorCustomizer

## Overview
This guide documents the process for creating new color themes for Ramza in Final Fantasy Tactics: The Ivalice Chronicles using the FFTColorCustomizer mod.

## Key Discovery
Ramza's color themes require **TEX files** (texture/UV mapping files), not just palette files. The FFTColorCustomizer mod uses a different system for Ramza than for other characters.

## Required Files
- **TEX Files**: tex_830.bin through tex_839.bin (10 files total)
  - These are the actual texture files that define Ramza's colors
  - Different numbered files correspond to different Ramza chapters/appearances

## Step-by-Step Process

### 1. Extract Original Sprites
Use the FFT Sprite Modding Toolkit from Nexus Mods:
```
1. Install the toolkit
2. Run the extraction process
3. Sprites will be extracted to: C:/Users/[username]/AppData/Local/FFTSpriteToolkit/working/extracted_sprites/
4. Look for files named: 830_Ramuza_Ch1_hd.png through 835_Ramuza_Ch4_hd.png
```

### 2. Modify Sprite Colors
Create a Python script to recolor the sprites:
```python
from PIL import Image

def recolor_sprite(input_path, output_path, color_map):
    img = Image.open(input_path)
    if img.mode != 'RGBA':
        img = img.convert('RGBA')

    pixels = img.load()
    width, height = img.size

    for y in range(height):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            old_color = (r, g, b)
            if old_color in color_map:
                new_color = color_map[old_color]
                pixels[x, y] = (*new_color, a)

    img.save(output_path)
```

Define your color mappings (example for blue theme):
```python
blue_knight_map = {
    # Browns (hair) -> Dark Blue
    (72, 48, 40): (20, 30, 80),
    (144, 80, 40): (30, 50, 120),
    # Add more color mappings as needed
}
```

### 3. Convert Modified Sprites to Game Format
The FFT Sprite Toolkit should handle this conversion:
1. Place modified PNG files in the toolkit's working directory
2. Use the toolkit's "Create Mod Package" feature
3. The toolkit will generate the necessary TEX and palette files

### 4. Locate Generated Files
After the toolkit creates the mod package, find:
- TEX files in: `[ModName]/FFTIVC/data/enhanced/system/ffto/g2d/`
- Files needed: tex_830.bin through tex_839.bin

### 5. Integrate into FFTColorCustomizer

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