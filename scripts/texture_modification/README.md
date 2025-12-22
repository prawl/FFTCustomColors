# Texture Modification Scripts

These scripts were developed during the FFT texture research sessions to modify character colors by editing texture atlas files.

## Core Scripts

### g2d_extract.py
**Purpose**: Extracts all texture files from g2d.dat  
**Usage**: `python g2d_extract.py`  
**Output**: Creates `extract/TEXTURES/` with 1,442 .bin files  

### g2d_repack.py
**Purpose**: Repacks modified textures back into g2d.dat format  
**Usage**: `python g2d_repack.py [input_dir] [output.dat]`  
**Output**: New g2d.dat file with modified textures  

## Visualization Scripts

### visualize_tex_images.py
**Purpose**: Converts tex files to PNG images for visualization  
**Usage**: `python visualize_tex_images.py`  
**Output**: PNG files in `texture_visualization/` directory  

### find_actual_textures.py
**Purpose**: Identifies actual texture atlas files vs UV coordinate files  
**Usage**: `python find_actual_textures.py`  
**Output**: Analysis of texture files and saves likely atlases as PNGs  

### compare_tex_regions.py
**Purpose**: Analyzes UV coordinate patterns and compares textures  
**Usage**: `python compare_tex_regions.py`  
**Output**: Comparison data and indexed color visualizations  

## Modification Scripts

### modify_texture_atlas.py
**Purpose**: Applies Dark Knight color transformations to specific textures  
**Usage**: `python modify_texture_atlas.py`  
**Output**: Modified textures in `modified_textures/` directory  
**Note**: Modifies tex_1556, 1558, 1560, 158 (known working files)  

### modify_all_ramza_textures.py
**Purpose**: Aggressive texture modification across many files  
**Usage**: `python modify_all_ramza_textures.py`  
**Output**: Modified textures in `dark_knight_complete/` directory  
**Note**: Modifies 57 files but can cause color reversions  

## Proven Texture Effects

From testing, we identified which textures control what:

| Texture Files | Effect |
|--------------|--------|
| tex_1556, 1558, 1560 | Red tint on armor |
| tex_158 | Black pants |
| tex_150-170 | Boots and armor details |
| tex_900-920 | DO NOT MODIFY (causes reversion) |

## Workflow

1. Extract textures: `python g2d_extract.py`
2. Visualize to identify targets: `python find_actual_textures.py`
3. Modify textures: `python modify_texture_atlas.py`
4. Repack: `python g2d_repack.py modified_textures g2d_new.dat`
5. Deploy g2d_new.dat to game directory
6. Test with white_heretic theme active

## Important Notes

- Textures are in BGR555 format (16-bit, 5 bits per channel)
- Must use with UV remapping theme like white_heretic
- Some textures have priority over others
- Save files can retain old appearance

See TEXTURE_MODIFICATION_GUIDE.md in project root for complete documentation.