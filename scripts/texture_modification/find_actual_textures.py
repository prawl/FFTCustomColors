#!/usr/bin/env python3
"""
Find and visualize the actual texture atlas files in g2d.dat.
Focusing on larger files that could be texture atlases.
"""

import struct
import os
from PIL import Image
import numpy as np

def visualize_texture(filepath, width, height, format_type='bgr555'):
    """Visualize a texture file with specified dimensions and format."""
    with open(filepath, 'rb') as f:
        data = f.read()
    
    pixels = []
    
    if format_type == 'bgr555':
        # 16-bit BGR555 format
        bytes_needed = width * height * 2
        if len(data) < bytes_needed:
            print(f"  Warning: File too small for {width}x{height} 16-bit")
            return None
            
        for i in range(0, bytes_needed - 1, 2):
            value = struct.unpack('<H', data[i:i+2])[0]
            b = (value & 0x1F) << 3
            g = ((value >> 5) & 0x1F) << 3
            r = ((value >> 10) & 0x1F) << 3
            pixels.append((r, g, b))
    
    elif format_type == 'rgba32':
        # 32-bit RGBA format
        bytes_needed = width * height * 4
        if len(data) < bytes_needed:
            print(f"  Warning: File too small for {width}x{height} 32-bit")
            return None
            
        for i in range(0, bytes_needed - 3, 4):
            r, g, b, a = data[i:i+4]
            pixels.append((r, g, b))
    
    elif format_type == 'palette8':
        # 8-bit indexed with palette
        # Assume first 512 bytes are palette (256 colors * 2 bytes BGR555)
        palette = []
        for i in range(0, 512, 2):
            value = struct.unpack('<H', data[i:i+2])[0]
            b = (value & 0x1F) << 3
            g = ((value >> 5) & 0x1F) << 3
            r = ((value >> 10) & 0x1F) << 3
            palette.append((r, g, b))
        
        # Rest is indexed pixel data
        for i in range(512, min(512 + width * height, len(data))):
            index = data[i]
            if index < len(palette):
                pixels.append(palette[index])
            else:
                pixels.append((0, 0, 0))
    
    if len(pixels) >= width * height:
        img_array = np.array(pixels[:width*height], dtype=np.uint8).reshape(height, width, 3)
        return Image.fromarray(img_array, 'RGB')
    return None

def analyze_large_textures():
    """Analyze the larger texture files that might be actual texture atlases."""
    
    tex_dir = "extract/TEXTURES"
    output_dir = "actual_textures"
    os.makedirs(output_dir, exist_ok=True)
    
    # Files to check - the largest ones found
    large_files = [
        # 262,144 bytes = 256KB files
        ("tex_1552.bin", 262144),
        ("tex_1556.bin", 262144),
        ("tex_1558.bin", 262144),
        ("tex_1560.bin", 262144),
        
        # 226,304 bytes files
        ("tex_1287.bin", 226304),
        ("tex_1288.bin", 226304),
        
        # Also check some specific numbered files that might be important
        ("tex_158.bin", None),  # Ramza sprite number?
        ("tex_159.bin", None),  # Ramza sprite number?
        ("tex_160.bin", None),  # Ramza sprite number?
    ]
    
    print("Analyzing potential texture atlas files...\n")
    
    for entry in large_files:
        if len(entry) == 2:
            filename, expected_size = entry
        else:
            filename = entry
            expected_size = None
            
        filepath = os.path.join(tex_dir, filename)
        if not os.path.exists(filepath):
            continue
        
        with open(filepath, 'rb') as f:
            data = f.read()
        
        file_size = len(data)
        print(f"\n{filename}: {file_size:,} bytes")
        print("=" * 50)
        
        # Try different interpretations based on size
        interpretations = []
        
        if file_size == 262144:  # 256KB
            interpretations = [
                (512, 256, 'bgr555', '512x256 16-bit BGR555'),
                (256, 512, 'bgr555', '256x512 16-bit BGR555'),
                (256, 256, 'rgba32', '256x256 32-bit RGBA'),
                (512, 512, 'palette8', '512x512 8-bit indexed')
            ]
        elif file_size == 226304:  # ~221KB
            # 226304 = 336 * 336 * 2
            interpretations = [
                (336, 336, 'bgr555', '336x336 16-bit BGR555'),
                (256, 441, 'bgr555', '256x441 16-bit BGR555'),
            ]
        elif file_size == 131072:  # 128KB
            interpretations = [
                (256, 256, 'bgr555', '256x256 16-bit BGR555'),
            ]
        elif file_size == 118784:  # ~116KB
            interpretations = [
                (256, 232, 'bgr555', '256x232 16-bit BGR555'),
            ]
        else:
            # Try to guess dimensions
            if file_size % 2 == 0:
                pixels_16bit = file_size // 2
                width = 256
                if pixels_16bit % width == 0:
                    height = pixels_16bit // width
                    interpretations = [(width, height, 'bgr555', f'{width}x{height} 16-bit')]
        
        # Try each interpretation
        for width, height, format_type, description in interpretations:
            img = visualize_texture(filepath, width, height, format_type)
            if img:
                # Analyze image content
                img_array = np.array(img)
                unique_colors = len(np.unique(img_array.reshape(-1, 3), axis=0))
                black_pixels = np.all(img_array == 0, axis=2).sum()
                total_pixels = img_array.shape[0] * img_array.shape[1]
                non_black_ratio = 1 - (black_pixels / total_pixels)
                
                print(f"  {description}:")
                print(f"    - Unique colors: {unique_colors:,}")
                print(f"    - Non-black: {non_black_ratio:.1%}")
                
                # Save if it looks like actual texture data
                if unique_colors > 500 and non_black_ratio > 0.3:
                    output_name = f"{filename[:-4]}_{width}x{height}_{format_type}.png"
                    img.save(os.path.join(output_dir, output_name))
                    print(f"    -> SAVED! Likely texture atlas!")
                elif unique_colors < 50:
                    print(f"    -> Too few colors, likely not texture")
                else:
                    output_name = f"{filename[:-4]}_{width}x{height}_{format_type}.png"
                    img.save(os.path.join(output_dir, output_name))
                    print(f"    -> Saved for inspection")

def check_for_sprite_files():
    """Check if there are actual sprite files in the extraction."""
    print("\n" + "=" * 60)
    print("Checking for sprite files...")
    
    # Look for files with 'spr' in the name or specific sizes
    tex_dir = "extract/TEXTURES"
    
    # Check metadata file
    metadata_path = "extract/metadata.txt"
    if os.path.exists(metadata_path):
        with open(metadata_path, 'r') as f:
            lines = f.readlines()[:50]  # First 50 lines
            print("\nMetadata excerpt:")
            for line in lines:
                if 'ramza' in line.lower() or 'sprite' in line.lower() or '830' in line:
                    print(f"  {line.strip()}")

if __name__ == "__main__":
    print("=" * 60)
    print("FINDING ACTUAL TEXTURE ATLASES IN G2D.DAT")
    print("=" * 60)
    
    analyze_large_textures()
    check_for_sprite_files()
    
    print("\n" + "=" * 60)
    print("Analysis complete! Check actual_textures/ directory.")
    print("\nKey findings:")
    print("- tex_1552+ (262KB) might be 512x256 texture atlases")
    print("- tex_1287+ (226KB) might be 336x336 texture atlases") 
    print("- These larger files are more likely to be actual textures")
    print("- The UV coordinates in tex_830-835 likely point to these")
    print("=" * 60)