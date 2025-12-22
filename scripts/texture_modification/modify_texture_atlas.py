#!/usr/bin/env python3
"""
Modify actual texture atlas files to create custom color themes.
This targets the real texture data, not UV coordinates.
"""

import struct
import os
import shutil
from PIL import Image
import numpy as np

def load_texture_as_bgr555(filepath, width=512, height=256):
    """Load texture file as BGR555 image."""
    with open(filepath, 'rb') as f:
        data = f.read()
    
    pixels = []
    bytes_needed = width * height * 2
    
    for i in range(0, min(len(data), bytes_needed) - 1, 2):
        value = struct.unpack('<H', data[i:i+2])[0]
        b = (value & 0x1F) << 3
        g = ((value >> 5) & 0x1F) << 3
        r = ((value >> 10) & 0x1F) << 3
        pixels.append((r, g, b))
    
    if len(pixels) >= width * height:
        img_array = np.array(pixels[:width*height], dtype=np.uint8).reshape(height, width, 3)
        return Image.fromarray(img_array, 'RGB'), data
    return None, data

def create_dark_knight_texture(input_file, output_file):
    """Transform a texture to dark knight theme."""
    print(f"\nProcessing {os.path.basename(input_file)}...")
    
    # Load as 512x256 16-bit texture (most common for larger files)
    with open(input_file, 'rb') as f:
        data = bytearray(f.read())
    
    # Process as BGR555
    modified_count = 0
    width, height = 512, 256
    bytes_to_process = min(len(data), width * height * 2)
    
    for i in range(0, bytes_to_process - 1, 2):
        value = struct.unpack('<H', data[i:i+2])[0]
        
        # Extract BGR555 components
        b = value & 0x1F
        g = (value >> 5) & 0x1F
        r = (value >> 10) & 0x1F
        
        # Convert to 8-bit for easier manipulation
        r8 = r << 3
        g8 = g << 3
        b8 = b << 3
        
        # Apply dark knight transformation
        new_r, new_g, new_b = r8, g8, b8
        
        # Transform white/light colors to dark gray
        if r8 > 200 and g8 > 200 and b8 > 200:
            new_r, new_g, new_b = 32, 32, 32  # Dark gray
            modified_count += 1
        
        # Transform blue colors to dark red
        elif b8 > r8 + 30 and b8 > g8 + 30:
            new_r = min(255, b8)  # Transfer blue intensity to red
            new_g = 0
            new_b = 0
            modified_count += 1
        
        # Transform brown/tan to black with red hints
        elif abs(r8 - g8) < 30 and r8 > b8 + 20:
            new_r = max(0, r8 // 4)  # Darken significantly
            new_g = 0
            new_b = 0
            modified_count += 1
        
        # Transform bright colors to darker versions
        elif r8 + g8 + b8 > 300:
            new_r = r8 // 3
            new_g = g8 // 3
            new_b = b8 // 3
            modified_count += 1
        
        # Convert back to BGR555 if changed
        if (new_r, new_g, new_b) != (r8, g8, b8):
            new_value = ((new_b >> 3) & 0x1F) | (((new_g >> 3) & 0x1F) << 5) | (((new_r >> 3) & 0x1F) << 10)
            struct.pack_into('<H', data, i, new_value)
    
    # Save modified texture
    with open(output_file, 'wb') as f:
        f.write(data)
    
    print(f"  Modified {modified_count:,} colors")
    return modified_count

def modify_texture_atlases():
    """Modify the identified texture atlas files."""
    
    tex_dir = "extract/TEXTURES"
    output_dir = "modified_textures"
    os.makedirs(output_dir, exist_ok=True)
    
    # Target the texture atlas files we identified
    texture_files = [
        "tex_1556.bin",  # High color variety texture atlas
        "tex_1558.bin",  # High color variety texture atlas
        "tex_1560.bin",  # High color variety texture atlas
        "tex_158.bin",   # Potential Ramza-related texture
    ]
    
    print("Creating Dark Knight texture modifications...")
    print("=" * 60)
    
    total_modified = 0
    
    for filename in texture_files:
        input_path = os.path.join(tex_dir, filename)
        if not os.path.exists(input_path):
            print(f"File not found: {filename}")
            continue
        
        output_path = os.path.join(output_dir, filename)
        count = create_dark_knight_texture(input_path, output_path)
        total_modified += count
        
        # Also save a visualization
        if filename in ["tex_1556.bin", "tex_1558.bin", "tex_1560.bin"]:
            width, height = 512, 256
        else:
            width, height = 256, 256
            
        # Visualize original
        img_orig, _ = load_texture_as_bgr555(input_path, width, height)
        if img_orig:
            img_orig.save(os.path.join(output_dir, f"{filename[:-4]}_original.png"))
        
        # Visualize modified
        img_mod, _ = load_texture_as_bgr555(output_path, width, height)
        if img_mod:
            img_mod.save(os.path.join(output_dir, f"{filename[:-4]}_dark_knight.png"))
    
    print("\n" + "=" * 60)
    print(f"Total colors modified: {total_modified:,}")
    print(f"Modified textures saved to: {output_dir}/")
    
    return texture_files, output_dir

def prepare_for_repacking(texture_files, modified_dir):
    """Prepare modified textures for repacking into g2d.dat."""
    
    print("\n" + "=" * 60)
    print("Preparing for g2d.dat repacking...")
    
    # Create repack directory structure
    repack_dir = "g2d_repack"
    repack_textures = os.path.join(repack_dir, "TEXTURES")
    os.makedirs(repack_textures, exist_ok=True)
    
    # Copy all original textures first
    original_dir = "extract/TEXTURES"
    print("\nCopying original textures as base...")
    
    import glob
    for orig_file in glob.glob(os.path.join(original_dir, "*.bin")):
        shutil.copy2(orig_file, repack_textures)
    
    # Overwrite with our modified textures
    print("\nOverwriting with modified textures...")
    for filename in texture_files:
        modified_path = os.path.join(modified_dir, filename)
        if os.path.exists(modified_path):
            dest_path = os.path.join(repack_textures, filename)
            shutil.copy2(modified_path, dest_path)
            print(f"  Replaced {filename}")
    
    print(f"\nRepack directory ready: {repack_dir}/")
    print("\nNext steps:")
    print("1. Use g2d_repack.py to create new g2d.dat")
    print("2. Deploy the modified g2d.dat to the game")
    print("3. Test with white_heretic UV mappings to see texture changes")

if __name__ == "__main__":
    print("=" * 60)
    print("DARK KNIGHT TEXTURE ATLAS MODIFICATION")
    print("=" * 60)
    print()
    print("This modifies the actual texture atlases, not UV coordinates.")
    print("The white_heretic UV mappings will point to these modified textures.")
    print()
    
    # Modify the textures
    modified_files, output_dir = modify_texture_atlases()
    
    # Prepare for repacking
    prepare_for_repacking(modified_files, output_dir)
    
    print("\n" + "=" * 60)
    print("IMPORTANT: This approach modifies the actual textures.")
    print("When combined with white_heretic UV mappings:")
    print("- The UV coords will point to the same texture regions")
    print("- But those regions now contain dark colors instead of white")
    print("- This should produce a Dark Knight appearance!")
    print("=" * 60)