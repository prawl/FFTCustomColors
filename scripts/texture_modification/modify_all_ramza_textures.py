#!/usr/bin/env python3
"""
More aggressive texture modification to create Dark Knight theme.
Targets more texture files to achieve complete color change.
"""

import struct
import os
import shutil
from pathlib import Path

def modify_texture_aggressive(data, filename):
    """Apply aggressive dark knight transformation to texture data."""
    
    modified_count = 0
    
    # Process as BGR555 16-bit values
    for i in range(0, len(data) - 1, 2):
        value = struct.unpack('<H', data[i:i+2])[0]
        
        # Skip completely black pixels (likely transparency)
        if value == 0:
            continue
            
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
        
        # MORE AGGRESSIVE: Transform ANY light color to dark
        if r8 > 150 or g8 > 150 or b8 > 150:
            # Make it dark with red tint
            new_r = min(80, r8 // 3)
            new_g = 0
            new_b = 0
            modified_count += 1
        
        # Transform white/gray to black
        elif r8 > 100 and g8 > 100 and b8 > 100:
            new_r = 20
            new_g = 20
            new_b = 20
            modified_count += 1
            
        # Transform blue to dark red
        elif b8 > r8 + 20:
            new_r = min(255, b8)
            new_g = 0
            new_b = 0
            modified_count += 1
            
        # Transform green to dark
        elif g8 > r8 + 20:
            new_r = 30
            new_g = 30
            new_b = 30
            modified_count += 1
            
        # Darken everything else
        elif r8 + g8 + b8 > 150:
            new_r = r8 // 4
            new_g = g8 // 4
            new_b = b8 // 4
            modified_count += 1
        
        # Convert back to BGR555 if changed
        if (new_r, new_g, new_b) != (r8, g8, b8):
            new_value = ((new_b >> 3) & 0x1F) | (((new_g >> 3) & 0x1F) << 5) | (((new_r >> 3) & 0x1F) << 10)
            struct.pack_into('<H', data, i, new_value)
    
    return modified_count

def process_all_textures():
    """Process all potential Ramza-related textures."""
    
    tex_dir = Path("extract/TEXTURES")
    output_dir = Path("dark_knight_complete")
    output_dir.mkdir(exist_ok=True)
    
    # First, copy ALL textures as base
    print("Copying all original textures as base...")
    for tex_file in tex_dir.glob("*.bin"):
        shutil.copy2(tex_file, output_dir / tex_file.name)
    
    # List of texture files to modify - expanded list
    # Including more files that might affect character appearance
    textures_to_modify = []
    
    # Add the ones we know work (gave us black pants and red tint)
    for i in [1556, 1558, 1560, 158]:
        textures_to_modify.append(f"tex_{i}.bin")
    
    # Add more texture files in similar ranges
    # tex_150-170 range (character textures)
    for i in range(150, 171):
        textures_to_modify.append(f"tex_{i}.bin")
    
    # tex_1550-1590 range (larger texture atlases)
    for i in range(1550, 1591):
        textures_to_modify.append(f"tex_{i}.bin")
    
    # tex_900-920 range (might be character related)
    for i in range(900, 921):
        textures_to_modify.append(f"tex_{i}.bin")
    
    # Process each texture
    total_modified = 0
    files_modified = 0
    
    print(f"\nProcessing {len(textures_to_modify)} texture files...")
    print("=" * 60)
    
    for filename in textures_to_modify:
        filepath = output_dir / filename
        if not filepath.exists():
            continue
            
        with open(filepath, 'rb') as f:
            data = bytearray(f.read())
        
        count = modify_texture_aggressive(data, filename)
        
        if count > 0:
            with open(filepath, 'wb') as f:
                f.write(data)
            print(f"  {filename}: {count:,} colors modified")
            total_modified += count
            files_modified += 1
    
    print("\n" + "=" * 60)
    print(f"Total: {total_modified:,} colors modified across {files_modified} files")
    
    return output_dir

def create_new_g2d(texture_dir):
    """Create a new g2d.dat with all modified textures."""
    
    print("\nCreating new g2d.dat with dark knight textures...")
    
    # Use the existing g2d_repack script
    import subprocess
    result = subprocess.run(
        ["python", "g2d_repack.py", str(texture_dir), "g2d_dark_knight_complete.dat"],
        capture_output=True,
        text=True
    )
    
    if result.returncode == 0:
        print("Successfully created g2d_dark_knight_complete.dat")
        return True
    else:
        print(f"Error creating g2d.dat: {result.stderr}")
        return False

if __name__ == "__main__":
    print("=" * 60)
    print("AGGRESSIVE DARK KNIGHT TEXTURE MODIFICATION")
    print("=" * 60)
    print()
    print("This will modify many more textures to ensure complete")
    print("transformation to dark knight appearance.")
    print()
    
    # Process textures
    output_dir = process_all_textures()
    
    # Create new g2d.dat
    if create_new_g2d(output_dir):
        print("\n" + "=" * 60)
        print("SUCCESS! Dark Knight textures ready.")
        print("\nFile created: g2d_dark_knight_complete.dat")
        print("\nSince the full g2d.dat override isn't working,")
        print("the partial changes (black pants, red tint) confirm")
        print("that SOME textures are being loaded correctly.")
        print("\nThe white_heretic UV mappings are pointing to textures")
        print("we haven't fully identified yet.")
        print("=" * 60)
    else:
        print("\nFailed to create g2d.dat")