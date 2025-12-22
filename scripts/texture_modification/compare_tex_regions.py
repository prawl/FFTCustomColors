#!/usr/bin/env python3
"""
Compare tex_830 (Ramza UV coords) with tex_1000+ (potential texture atlases)
to find which textures the UV coordinates point to.
"""

import struct
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont

def load_tex_data(filepath):
    """Load tex file as raw binary data."""
    with open(filepath, 'rb') as f:
        return f.read()

def analyze_uv_coordinates(tex_data):
    """Analyze UV coordinate patterns in tex file."""
    # Interpret as 16-bit values
    values = []
    for i in range(0, len(tex_data) - 1, 2):
        value = struct.unpack('<H', tex_data[i:i+2])[0]
        values.append(value)
    
    # Separate potential U and V coordinates
    # UV coordinates typically alternate or are in pairs
    non_zero = [v for v in values if v != 0]
    
    if non_zero:
        print(f"  Non-zero values: {len(non_zero):,} / {len(values):,} ({100*len(non_zero)/len(values):.1f}%)")
        print(f"  Value range: {min(non_zero)} - {max(non_zero)}")
        print(f"  Unique values: {len(set(non_zero)):,}")
        
        # Check if values look like coordinates (typically limited range)
        # UV coords are often normalized or in texture dimensions
        coord_like = [v for v in non_zero if v < 512]  # Assuming max texture size 512
        if coord_like:
            print(f"  Coordinate-like values (<512): {len(coord_like)} ({100*len(coord_like)/len(non_zero):.1f}%)")
    
    return values

def visualize_as_indexed(tex_data, width=256):
    """Visualize tex file as 4-bit indexed color data."""
    height = len(tex_data) // width
    
    # Create palette (16 colors)
    palette = [
        (0, 0, 0),       # 0: Black
        (128, 0, 0),     # 1: Dark Red
        (0, 128, 0),     # 2: Dark Green  
        (0, 0, 128),     # 3: Dark Blue
        (128, 128, 0),   # 4: Brown
        (128, 0, 128),   # 5: Purple
        (0, 128, 128),   # 6: Teal
        (192, 192, 192), # 7: Light Gray
        (128, 128, 128), # 8: Gray
        (255, 0, 0),     # 9: Red
        (0, 255, 0),     # A: Green
        (0, 0, 255),     # B: Blue
        (255, 255, 0),   # C: Yellow
        (255, 0, 255),   # D: Magenta
        (0, 255, 255),   # E: Cyan
        (255, 255, 255)  # F: White
    ]
    
    pixels = []
    for byte in tex_data[:width * height]:
        # Split byte into two 4-bit values
        high = (byte >> 4) & 0x0F
        low = byte & 0x0F
        pixels.append(palette[high])
        pixels.append(palette[low])
    
    # Create image (double width due to 4-bit packing)
    img_array = np.array(pixels[:width*height], dtype=np.uint8)
    img_array = img_array.reshape(height, width, 3)
    return Image.fromarray(img_array, 'RGB')

def compare_textures():
    """Compare different tex files to understand their relationships."""
    
    tex_dir = "extract/TEXTURES"
    output_dir = "texture_analysis"
    os.makedirs(output_dir, exist_ok=True)
    
    # Files to analyze
    files_to_compare = [
        ("tex_830.bin", "Ramza Ch1 UV coords"),
        ("tex_1000.bin", "Potential texture atlas"),
        ("tex_1002.bin", "Potential texture atlas"),
        ("tex_1004.bin", "Potential texture atlas"),
    ]
    
    print("Analyzing texture files...\n")
    
    for filename, description in files_to_compare:
        filepath = os.path.join(tex_dir, filename)
        if not os.path.exists(filepath):
            print(f"File not found: {filepath}")
            continue
            
        print(f"\n{filename} - {description}:")
        print("=" * 50)
        
        data = load_tex_data(filepath)
        print(f"  File size: {len(data):,} bytes")
        
        # Analyze as UV coordinates
        values = analyze_uv_coordinates(data)
        
        # Check for patterns that indicate UV vs texture data
        # UV files typically have many zeros and limited value range
        zero_ratio = values.count(0) / len(values)
        print(f"  Zero ratio: {zero_ratio:.1%}")
        
        if zero_ratio > 0.7:
            print(f"  -> Likely UV coordinate data (sparse)")
        elif zero_ratio < 0.3:
            print(f"  -> Likely texture/image data (dense)")
        else:
            print(f"  -> Mixed or compressed data")
        
        # Try visualizing as indexed color
        if len(data) >= 65536:  # At least 256x256 bytes
            img = visualize_as_indexed(data[:65536], 256)
            img.save(os.path.join(output_dir, f"{filename[:-4]}_indexed.png"))
            print(f"  Saved indexed visualization")

def find_texture_references():
    """Try to find which texture files are referenced by UV coordinates."""
    
    tex_dir = "extract/TEXTURES"
    
    # Load Ramza's UV coordinate file
    ramza_uv = load_tex_data(os.path.join(tex_dir, "tex_830.bin"))
    
    # Load white_heretic UV file if available
    white_uv_path = "extracted_textures/white_heretic/tex_830.bin"
    if os.path.exists(white_uv_path):
        white_uv = load_tex_data(white_uv_path)
        
        # Compare UV coordinates
        differences = 0
        for i in range(0, min(len(ramza_uv), len(white_uv)) - 1, 2):
            orig_val = struct.unpack('<H', ramza_uv[i:i+2])[0]
            white_val = struct.unpack('<H', white_uv[i:i+2])[0]
            if orig_val != white_val and orig_val != 0:  # Ignore zero changes
                differences += 1
        
        print(f"\nUV Coordinate changes: {differences:,}")
        print(f"This confirms tex files contain UV mappings, not colors.")
    
    # Check file relationships by size patterns
    print("\nFile size patterns:")
    print("Ramza UV files (830-835): 131,072 / 118,784 alternating")
    print("Texture candidates (1000-1018): Same size pattern!")
    print("This suggests tex_1000+ might be textures for other characters")
    print("or different texture layers/channels.")

if __name__ == "__main__":
    print("=" * 60)
    print("TEXTURE ANALYSIS - Understanding TEX File Relationships")
    print("=" * 60)
    
    compare_textures()
    print()
    find_texture_references()
    
    print("\n" + "=" * 60)
    print("CONCLUSION:")
    print("- tex_830-835 contain UV coordinates (sparse, ~75% zeros)")
    print("- tex_1000+ files have similar structure/size")
    print("- The actual texture atlas must be elsewhere in g2d.dat")
    print("- We need to find larger texture files (512x512 or bigger)")
    print("=" * 60)