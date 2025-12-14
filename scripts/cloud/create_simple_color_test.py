#!/usr/bin/env python3
"""
Create a test sprite for Cloud with distinct colors to identify which palette indices control which visual elements
"""

import struct
import shutil
import os
from pathlib import Path

def bgr555_to_rgb(bgr555):
    """Convert BGR555 to RGB tuple"""
    b = (bgr555 >> 10) & 0x1F
    g = (bgr555 >> 5) & 0x1F
    r = bgr555 & 0x1F
    return (r * 255 // 31, g * 255 // 31, b * 255 // 31)

def rgb_to_bgr555(r, g, b):
    """Convert RGB to BGR555 format"""
    r = min(31, r * 31 // 255)
    g = min(31, g * 31 // 255)
    b = min(31, b * 31 // 255)
    return (b << 10) | (g << 5) | r

def create_test_sprite():
    """Create a test sprite with distinct colors for mapping"""
    base_dir = Path(__file__).parent.parent.parent
    source_file = base_dir / "ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_original/battle_cloud_spr.bin"

    if not source_file.exists():
        print(f"Error: Source file not found: {source_file}")
        return False

    # Create test directory
    test_dir = base_dir / "ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_cloud_test"
    test_dir.mkdir(exist_ok=True, parents=True)

    output_file = test_dir / "battle_cloud_spr.bin"

    # Read the original sprite
    with open(source_file, 'rb') as f:
        data = bytearray(f.read())

    print(f"Processing Cloud sprite: {len(data)} bytes")

    # Define distinct test colors for different index ranges
    test_colors = {
        # Test different ranges to identify what they control
        (3, 6): (255, 0, 0),      # RED - Likely buckles/trim (indices 3-5)
        (6, 10): (0, 255, 0),     # GREEN - Primary armor/cape (indices 6-9)
        (20, 32): (0, 0, 255),    # BLUE - Secondary armor (indices 20-31)
        (35, 48): (255, 255, 0),  # YELLOW - Extended armor (indices 35-47, skip 44)
        (51, 63): (255, 0, 255),  # MAGENTA - Additional armor (indices 51-62)

        # Leave indices 10-19 unchanged (hair)
        # Skip index 44 as documented
    }

    # Apply test colors to all 8 unit palettes
    for palette_idx in range(8):
        base_offset = palette_idx * 32  # Each palette is 16 colors * 2 bytes

        for (start_idx, end_idx), color in test_colors.items():
            for idx in range(start_idx, end_idx):
                # Skip hair indices and problematic index 44
                if (idx >= 10 and idx < 20) or idx == 44:
                    continue

                offset = base_offset + (idx * 2)
                if offset + 1 < 512:  # Stay within palette data
                    bgr555 = rgb_to_bgr555(*color)
                    struct.pack_into('<H', data, offset, bgr555)

    # Write the modified sprite
    with open(output_file, 'wb') as f:
        f.write(data)

    print(f"Test sprite created: {output_file}")

    # Also create a PNG preview for immediate viewing
    import sys
    sys.path.append(str(base_dir / "scripts"))
    from convert_sprite_sw import extract_southwest_sprite

    preview_dir = base_dir / "ColorMod/Resources/Previews/Cloud_Test"
    preview_dir.mkdir(exist_ok=True, parents=True)

    png_output = preview_dir / "cloud_color_test.png"
    extract_southwest_sprite(str(output_file), str(png_output), palette_index=0, preview_mode=False)

    print(f"\nPNG preview created: {png_output}")
    print("\nColor Mapping Key:")
    print("  RED     = Indices 3-5 (likely buckles/trim)")
    print("  GREEN   = Indices 6-9 (likely primary armor/cape)")
    print("  BLUE    = Indices 20-31 (likely secondary armor)")
    print("  YELLOW  = Indices 35-47 (likely extended armor, skipping 44)")
    print("  MAGENTA = Indices 51-62 (likely additional armor)")
    print("\nOriginal colors preserved for indices 10-19 (hair)")

    return True

if __name__ == "__main__":
    if create_test_sprite():
        print("\n✓ Success! Test sprite created.")
        print("\nNext steps:")
        print("1. Deploy the test sprite to your game")
        print("2. Check which colors appear where on Cloud")
        print("3. Use this information to create proper themes")