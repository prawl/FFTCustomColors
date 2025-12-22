#!/usr/bin/env python3
"""
Visualize tex files from g2d.dat to identify actual texture images.
Focuses on tex_1000+ range which might be the actual textures.
"""

import struct
import os
from PIL import Image
import numpy as np

def load_tex_as_bgr555(filepath):
    """Load a tex file and interpret as 16-bit BGR555 image data."""
    with open(filepath, 'rb') as f:
        data = f.read()
    
    # Determine dimensions based on file size
    file_size = len(data)
    if file_size == 131072:  # 256x256 16-bit
        width, height = 256, 256
    elif file_size == 118784:  # 224x236 or similar
        # Try different dimensions that multiply to 59392 pixels
        width, height = 256, 232  # Close approximation
    elif file_size == 262144:  # 512x256 16-bit
        width, height = 512, 256
    elif file_size == 524288:  # 512x512 16-bit
        width, height = 512, 512
    else:
        print(f"Unusual size {file_size} bytes, trying to factor...")
        pixels = file_size // 2  # 16-bit = 2 bytes per pixel
        # Try common widths
        for w in [128, 256, 512]:
            if pixels % w == 0:
                width, height = w, pixels // w
                break
        else:
            # Default to square-ish
            width = int(pixels ** 0.5)
            height = pixels // width
            if width * height * 2 != file_size:
                print(f"Warning: Size mismatch for {filepath}")
                return None
    
    print(f"Processing {os.path.basename(filepath)}: {width}x{height} ({file_size} bytes)")
    
    # Convert BGR555 to RGB
    pixels = []
    for i in range(0, min(len(data) - 1, width * height * 2), 2):
        value = struct.unpack('<H', data[i:i+2])[0]
        
        # BGR555 format
        b = (value & 0x1F) << 3
        g = ((value >> 5) & 0x1F) << 3
        r = ((value >> 10) & 0x1F) << 3
        
        pixels.append((r, g, b))
    
    # Create image
    if len(pixels) >= width * height:
        img_array = np.array(pixels[:width*height], dtype=np.uint8).reshape(height, width, 3)
        return Image.fromarray(img_array, 'RGB')
    else:
        print(f"Not enough pixels for {filepath}")
        return None

def visualize_texture_candidates():
    """Visualize potential texture files from g2d extraction."""
    
    tex_dir = "extract/TEXTURES"
    output_dir = "texture_visualization"
    os.makedirs(output_dir, exist_ok=True)
    
    # Files to check - focusing on potential actual textures
    candidates = [
        # Ramza UV coordinate files (for reference)
        "tex_830.bin", "tex_831.bin", "tex_832.bin", 
        "tex_833.bin", "tex_834.bin", "tex_835.bin",
        
        # Potential texture atlas files (1000-1018 range)
        "tex_1000.bin", "tex_1001.bin", "tex_1002.bin",
        "tex_1003.bin", "tex_1004.bin", "tex_1005.bin",
        "tex_1006.bin", "tex_1007.bin", "tex_1008.bin",
        "tex_1009.bin", "tex_1010.bin", "tex_1011.bin",
        "tex_1012.bin", "tex_1013.bin", "tex_1014.bin",
        "tex_1015.bin", "tex_1016.bin", "tex_1017.bin",
        "tex_1018.bin"
    ]
    
    # Also check some lower numbered files that might be textures
    for i in [0, 1, 10, 100, 200, 300, 400, 500, 600, 700, 800, 900]:
        candidates.append(f"tex_{i}.bin")
    
    print("Visualizing texture candidates...\n")
    
    for filename in candidates:
        filepath = os.path.join(tex_dir, filename)
        if not os.path.exists(filepath):
            continue
            
        img = load_tex_as_bgr555(filepath)
        if img:
            output_path = os.path.join(output_dir, f"{filename[:-4]}.png")
            img.save(output_path)
            
            # Check if it looks like actual sprite/texture data
            # Real textures typically have varied colors and patterns
            img_array = np.array(img)
            unique_colors = len(np.unique(img_array.reshape(-1, 3), axis=0))
            
            # Calculate how much is non-black
            black_pixels = np.all(img_array == 0, axis=2).sum()
            total_pixels = img_array.shape[0] * img_array.shape[1]
            non_black_ratio = 1 - (black_pixels / total_pixels)
            
            print(f"  -> {unique_colors} unique colors, {non_black_ratio:.1%} non-black")
            
            if unique_colors > 100 and non_black_ratio > 0.1:
                print(f"  -> Likely texture/sprite data!")
            elif unique_colors < 10:
                print(f"  -> Likely metadata or empty")
            print()

if __name__ == "__main__":
    print("=" * 60)
    print("TEX FILE VISUALIZATION - Finding Actual Texture Images")
    print("=" * 60)
    print()
    
    visualize_texture_candidates()
    
    print("\nVisualization complete! Check texture_visualization/ directory.")
    print("Look for files with many unique colors and sprite-like patterns.")
    print("\nThe tex_1000+ files are good candidates as they:")
    print("1. Have the same sizes as Ramza's tex files (131,072/118,784 bytes)")
    print("2. Are in a different numbering range suggesting different purpose")
    print("3. May contain the actual texture atlas that UV coords reference")