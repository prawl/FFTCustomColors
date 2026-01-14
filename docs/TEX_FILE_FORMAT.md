# TEX File Format Reference

Technical documentation for FFT: The Ivalice Chronicles TEX files used for sprite rendering.

## Overview

TEX files contain the actual pixel data for battle sprites. They work alongside SPR files:
- **TEX files** (g2d system): Provide pixel indices for rendering
- **SPR files** (fftpack): Provide palette colors for theming

## File Structure

```
Offset 0x000-0x7FF: Header (2048 bytes, all zeros)
Offset 0x800+:      4-bit indexed pixel data
```

### Pixel Data Format

- **4-bit indexed**: 2 pixels per byte
- **High nibble**: First pixel (index 0-15)
- **Low nibble**: Second pixel (index 0-15)
- **Sheet width**: 512 pixels
- **Index 0**: Transparent

### Reading Pixels (Python)

```python
HEADER_SIZE = 0x800
SHEET_WIDTH = 512

def get_pixel_index(data, pixel_idx):
    byte_offset = HEADER_SIZE + (pixel_idx // 2)
    byte_val = data[byte_offset]
    if pixel_idx % 2 == 0:
        return (byte_val >> 4) & 0x0F  # High nibble
    else:
        return byte_val & 0x0F  # Low nibble

def set_pixel_index(data, pixel_idx, new_index):
    byte_offset = HEADER_SIZE + (pixel_idx // 2)
    byte_val = data[byte_offset]
    if pixel_idx % 2 == 0:
        data[byte_offset] = (new_index << 4) | (byte_val & 0x0F)
    else:
        data[byte_offset] = (byte_val & 0xF0) | new_index
```

## File Pairs

TEX files come in pairs (N, N+1):

| File | Size | Rows | Contents |
|------|------|------|----------|
| Even (N) | 131,072 bytes | 504 | **Standing poses** - all static character poses |
| Odd (N+1) | 118,784 bytes | 456 | **Animation frames** - movement/action in-between frames |

**Important**: Both files must be deployed together for changes to work correctly.

## Sprite Layout

- **Sprite height**: 40 pixels per sprite row
- **Local Y calculation**: `local_y = (pixel_y) % 40`
- **Sprite row**: `sprite_row = pixel_y // 40`

### Y-Region Guidelines (approximate)

| Local Y | Region | Typical Content |
|---------|--------|-----------------|
| 0-10 | Hair | Top of head, hair |
| 10-15 | Face | Eyes, face details |
| 15-25 | Torso | Upper body, arms |
| 25-40 | Legs | Lower body, feet |

*Note: Exact boundaries vary by sprite and pose.*

## TEX File Mappings

### Generic Jobs (992-1145)

| Job | Male TEX | Female TEX | SPR Name |
|-----|----------|------------|----------|
| Squire | 992, 993 | 994, 995 | battle_mina_[m/w]_spr.bin |
| Chemist | 996, 997 | 998, 999 | battle_item_[m/w]_spr.bin |
| Knight | 1000, 1001 | 1002, 1003 | battle_knight_[m/w]_spr.bin |
| Archer | 1004, 1005 | 1006, 1007 | battle_archer_[m/w]_spr.bin |
| Monk | 1008, 1009 | 1010, 1011 | battle_monk_[m/w]_spr.bin |
| Priest | 1012, 1013 | 1014, 1015 | battle_priest_[m/w]_spr.bin |
| Black Mage | 1016, 1017 | 1018, 1019 | battle_kuro_[m/w]_spr.bin |
| Time Mage | 1020, 1021 | 1022, 1023 | battle_toki_[m/w]_spr.bin |
| Summoner | 1024, 1025 | 1026, 1027 | battle_sho_[m/w]_spr.bin |
| Thief | 1028, 1029 | 1030, 1031 | battle_shi_[m/w]_spr.bin |

### Story Characters (830-920)

| Character | TEX Files | Notes |
|-----------|-----------|-------|
| Ramza Chapter 1 | 830, 831 | |
| Ramza Chapter 2/3 | 832, 833 | |
| Ramza Chapter 4 | 834, 835 | |
| Orlandeau | 854, 855 | |
| Reis | 858, 859 | |
| Rapha | 870, 871 | |
| Marach | 872, 873 | |
| Agrias | 880, 881, 914, 915 | 4 files (2 sprite variants) |
| Beowulf | 882, 883 | |
| Mustadio | 888, 889 | |
| Meliadoul | 905, 906 | |
| Cloud | 910, 911 | |

### Unknown Ranges

| Range | Possible Contents |
|-------|-------------------|
| 836-853 | Unknown story characters or monsters |
| 860-869 | Unknown |
| 1146-1551 | Unknown (gap before 1552+) |
| 1552+ | Unknown (pal_*.bin files also exist here) |

## Pixel Manipulation Techniques

### Remove Visual Elements

Remap indices to 0 (transparent):

```python
# Remove cape (indices 7, 8, 9, 10)
CAPE_INDICES = {7, 8, 9, 10}
for i in range(HEADER_SIZE, len(data)):
    high = (data[i] >> 4) & 0x0F
    low = data[i] & 0x0F
    if high in CAPE_INDICES:
        high = 0
    if low in CAPE_INDICES:
        low = 0
    data[i] = (high << 4) | low
```

### Y-Threshold Remapping

Change pixels only in specific regions:

```python
# Make character bald (remove hair in head region only)
HAIR_INDICES = {11, 12, 13}
HAIR_Y_THRESHOLD = 15

for i in range(HEADER_SIZE, len(data)):
    pixel_offset = (i - HEADER_SIZE) * 2
    y = pixel_offset // SHEET_WIDTH
    local_y = y % SPRITE_HEIGHT

    if local_y < HAIR_Y_THRESHOLD:
        high = (data[i] >> 4) & 0x0F
        low = data[i] & 0x0F
        if high in HAIR_INDICES:
            high = 0  # transparent
        if low in HAIR_INDICES:
            low = 0
        data[i] = (high << 4) | low
```

### Neighbor-Based Detection

Find pixels surrounded by specific indices:

```python
def count_neighbors(data, pixel_idx, target_indices):
    x = pixel_idx % SHEET_WIDTH
    y = pixel_idx // SHEET_WIDTH
    count = 0
    for dy in [-1, 0, 1]:
        for dx in [-1, 0, 1]:
            if dx == 0 and dy == 0:
                continue
            neighbor_idx = (y + dy) * SHEET_WIDTH + (x + dx)
            if get_pixel_index(data, neighbor_idx) in target_indices:
                count += 1
    return count
```

## Possible Modifications

### Tested & Working

- ✅ **Remove cape** - Remap cape indices to transparent
- ✅ **Make bald** - Remove hair in head region using Y-threshold
- ✅ **Hair highlight fix** - Remap skin indices to hair in hair region

### Theoretical (Untested)

- **Silhouette mode** - Remap all non-zero indices to single color
- **Outline only** - Keep only outline indices, make rest transparent
- **Swap sections** - Remap one section's indices to another's
- **Remove helmet/hat** - Similar to cape removal
- **Invisible body** - Keep only head and hands visible

## Scripts Reference

| Script | Purpose |
|--------|---------|
| `fix_hair_skin_indices_tex.py` | Remap skin to hair by Y-threshold |
| `fix_stray_skin_to_hair.py` | Remap skin pixels surrounded by hair |
| `remove_cape.py` | Remove cape by making indices transparent |
| `make_bald.py` | Remove hair in head region |
| `visualize_tex_indices.py` | Generate PNG visualization of TEX file |
| `compare_tex_pair.py` | Compare even/odd TEX file pair |
| `analyze_face_indices.py` | Count indices by Y region |

## File Locations

- **Source TEX files**: `C:\Program Files (x86)\Steam\...\Reloaded\Mods\original_squire_v2\FFTIVC\data\enhanced\system\ffto\g2d\`
- **Mod TEX files**: `ColorMod\FFTIVC\data\enhanced\system\ffto\g2d\`
- **Deployed location**: `[Game]\Reloaded\Mods\FFTColorCustomizer\FFTIVC\data\enhanced\system\ffto\g2d\`

## Known Limitations

1. **TEX + SPR relationship**: Custom TEX files work with themed SPR palettes only when BOTH files in a pair are deployed
2. **No new colors**: TEX files use indexed colors from existing palette - cannot add arbitrary RGB colors
3. **Shared indices**: Many sprites share indices between unrelated elements (e.g., hair + boots), requiring Y-threshold or neighbor detection to isolate

## Changelog

- **2026-01-13**: Initial documentation
  - Discovered file pair purpose (poses vs animations)
  - Documented TEX file structure
  - Mapped story character TEX numbers
  - Tested cape removal and bald modifications
