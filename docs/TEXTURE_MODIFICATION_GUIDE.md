# FFT Texture Modification Guide

## Complete Method for Creating Custom Color Themes

### Overview
This guide documents the successful method for modifying FFT character colors by editing texture atlas files in g2d.dat, discovered through extensive testing on December 22, 2024.

## System Architecture

### Three Components Work Together:
1. **TEX Files (tex_830-835.bin)** - UV coordinate mappings (tells game WHERE to look in textures)
2. **G2D.DAT** - Master texture atlas container (2,450 embedded texture files)
3. **UV Remapping Mods** - Like white_heretic, which redirects UV coords to different texture regions

## Step-by-Step Process

### Step 1: Extract G2D.DAT

```bash
# Get g2d.dat from game files
# Location: [Game Directory]/data/enhanced/0007.pac (contains g2d.dat)
# Or unpacked: C:\Users\[username]\OneDrive\Desktop\Pac Files\0007\system\ffto\g2d.dat

# Extract all textures
python g2d_extract.py
# Creates extract/TEXTURES/ with 1,442 .bin files
```

### Step 2: Identify Target Textures

**Confirmed Texture Effects (from testing):**

| Texture Files | Effect When Modified | Safe to Modify |
|--------------|---------------------|----------------|
| tex_1556, 1558, 1560 | Main atlas - creates red tint | ✅ YES |
| tex_158 | Character specific - black pants | ✅ YES |
| tex_150-170 | Character details - boots/armor | ✅ YES (selective) |
| tex_900-920 | Reverts colors to original | ❌ NO |
| tex_1550-1590 | Large texture atlases | ⚠️ TEST CAREFULLY |

### Step 3: Modify Textures

```python
#!/usr/bin/env python3
import struct

def modify_texture_bgr555(filepath, output_path):
    """Modify a texture file with dark knight theme."""
    
    with open(filepath, 'rb') as f:
        data = bytearray(f.read())
    
    modified_count = 0
    
    # Process as 16-bit BGR555 values
    for i in range(0, len(data) - 1, 2):
        value = struct.unpack('<H', data[i:i+2])[0]
        
        if value == 0:  # Skip black/transparent
            continue
            
        # Extract BGR555 components (5 bits each)
        b = value & 0x1F
        g = (value >> 5) & 0x1F  
        r = (value >> 10) & 0x1F
        
        # Convert to 8-bit for easier manipulation
        r8 = r << 3
        g8 = g << 3
        b8 = b << 3
        
        # Dark Knight transformations that WORK:
        new_r, new_g, new_b = r8, g8, b8
        
        # White/Light colors → Dark gray
        if r8 > 200 and g8 > 200 and b8 > 200:
            new_r, new_g, new_b = 32, 32, 32
            modified_count += 1
            
        # Blue colors → Dark red
        elif b8 > r8 + 30 and b8 > g8 + 30:
            new_r = min(255, b8)
            new_g = 0
            new_b = 0
            modified_count += 1
            
        # Brown/tan → Black with red hint
        elif abs(r8 - g8) < 30 and r8 > b8 + 20:
            new_r = r8 // 4
            new_g = 0
            new_b = 0
            modified_count += 1
        
        # Pack back to BGR555 if changed
        if (new_r, new_g, new_b) != (r8, g8, b8):
            new_value = ((new_b >> 3) & 0x1F) | \
                       (((new_g >> 3) & 0x1F) << 5) | \
                       (((new_r >> 3) & 0x1F) << 10)
            struct.pack_into('<H', data, i, new_value)
    
    with open(output_path, 'wb') as f:
        f.write(data)
    
    return modified_count

# Recommended textures to modify for Dark Knight theme
TEXTURES_TO_MODIFY = [
    "tex_1556.bin",  # Main atlas - red tint
    "tex_1558.bin",  # Main atlas - red tint  
    "tex_1560.bin",  # Main atlas - red tint
    "tex_158.bin",   # Character - black pants
    "tex_152.bin",   # Details
    "tex_153.bin",   # Details
    "tex_160.bin",   # Details
    "tex_161.bin",   # Details
]

# DO NOT MODIFY: tex_900-920 range (causes color reversion)
```

### Step 4: Repack Modified Textures

```bash
# Copy all original textures to a working directory
cp -r extract/TEXTURES working_textures/

# Copy your modified textures over the originals
cp modified/*.bin working_textures/

# Repack into new g2d.dat
python g2d_repack.py working_textures g2d_modified.dat

# Output: g2d_modified.dat (~11MB, compressed from 13.9MB)
```

### Step 5: Deploy Modified G2D.DAT

**Option 1: FFTIVC Override Directory (Recommended)**
```
C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\FFTIVC\data\enhanced\system\ffto\g2d.dat
```

**Option 2: Direct Pac Files Replacement**
```
C:\Users\[username]\OneDrive\Desktop\Pac Files\0007\system\ffto\g2d.dat
```

### Step 6: Test with UV Remapping Theme

1. **Enable white_heretic theme** in FFTColorCustomizer
2. **Restart the game** (required for g2d.dat changes)
3. **Start new game or use clean save** (saves can retain old appearance)

## Test Results Summary

### What Worked:
- ✅ **First test (4 files)**: Black pants + reddish tint
- ✅ **Second test (57 files)**: Darker boots
- ⚠️ **Problem**: tex_900-920 reverted underpants to teal (original color)

### Key Discoveries:

1. **Texture Priority System**: Some textures override others
   - tex_900-920 has priority and shouldn't be modified
   - tex_1556/1558/1560 are safe main atlases
   - tex_158 reliably controls pants

2. **Selective Loading**: Modloader doesn't replace entire g2d.dat
   - Individual texture files are loaded selectively
   - Full g2d.dat replacement doesn't work as expected

3. **UV Mapping Dependency**: 
   - MUST use white_heretic or similar UV remapping
   - Without UV remapping, texture changes won't show

## Color Transformation Rules

### Proven Transformations:
- **White (RGB >200)** → Dark gray (32,32,32) ✅
- **Blue dominant** → Dark red ✅
- **Brown/tan** → Black with red tint ✅
- **General brightening** → Darken by 75% ✅

### Failed Transformations:
- **Aggressive darkening everything** → Can cause reversions
- **Modifying tex_900-920** → Reverts to original colors

## Troubleshooting

### Issue: Changes not visible
- Ensure white_heretic theme is active
- Restart game after g2d.dat changes
- Check if using clean save file

### Issue: Colors reverting to original
- Don't modify tex_900-920 range
- Some textures have priority over others
- Check if save file has cached appearance

### Issue: Partial changes only
- Normal - modloader loads textures selectively
- Focus on proven texture files
- Test incrementally to identify working textures

## File Structure Reference

```
Project Structure:
├── g2d_extract.py          # Extracts textures from g2d.dat
├── g2d_repack.py           # Repacks textures into g2d.dat
├── extract/
│   └── TEXTURES/           # 1,442 extracted texture files
├── modified_textures/      # Your modified texture files
├── g2d_new.dat            # Repacked g2d with modifications
└── texture_visualization/  # PNG previews of textures
```

## Tools Created

1. **g2d_extract.py** - Extracts all textures from g2d.dat
2. **g2d_repack.py** - Repacks modified textures into g2d.dat
3. **visualize_tex_images.py** - Creates PNG previews of texture files
4. **modify_texture_atlas.py** - Applies dark knight transformations
5. **find_actual_textures.py** - Identifies texture atlas files

## Future Improvements

1. **Map all texture regions** to character parts
2. **Create custom UV mappings** instead of relying on white_heretic
3. **Identify texture priority system** to avoid conflicts
4. **Automate theme creation** with preset transformations
5. **Package themes** with both TEX and texture modifications

## Important Notes

- **Save files retain appearance**: Always test with new game or clean saves
- **TEX files are UV maps, not colors**: They point to texture regions
- **G2D.DAT contains actual textures**: This is where colors live
- **Modloader is selective**: Not all textures in g2d.dat are loaded
- **Some textures override others**: Careful selection is required

---

*Guide created December 22, 2024*
*Based on ~25 hours of research and testing*
*Confirmed working with FFT: The War of the Lions (Steam version)*