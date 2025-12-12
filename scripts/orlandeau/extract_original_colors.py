#!/usr/bin/env python3
"""
Extract the original colors from Orlandeau's sprite to preserve cape, skin, and hair.
"""

import os
import struct

def extract_original_palette():
    """Extract and display the original Orlandeau palette."""

    # Paths
    base_dir = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    sprites_dir = os.path.join(base_dir, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit")
    original_sprite = os.path.join(sprites_dir, "battle_oru_spr.bin")

    # Read original sprite
    with open(original_sprite, 'rb') as f:
        sprite_data = f.read()

    print("Original Orlandeau Palette (Palette 0):")
    print("=" * 50)

    # Read the first 16 colors (32 bytes)
    for idx in range(16):
        offset = idx * 2
        color_16bit = struct.unpack('<H', sprite_data[offset:offset+2])[0]

        # Extract RGB from 16-bit format
        r = (color_16bit & 0x1F) << 3
        g = ((color_16bit >> 5) & 0x1F) << 3
        b = ((color_16bit >> 10) & 0x1F) << 3

        # Identify what this index controls based on our mapping
        if idx <= 2:
            element = "BLACK (shadows/outlines)"
        elif idx <= 6:
            element = "Armor/gloves/boots"
        elif idx <= 10:
            element = "Belt/undergarments/hair"
        else:
            element = "Cape/face/skin"

        print(f"Index {idx:2d}: RGB({r:3d}, {g:3d}, {b:3d}) - {element}")

    print("\n" + "=" * 50)
    print("\nColors to preserve:")
    print("- Indices 0-2: Keep as shadows")
    print("- Indices 7-10: Contains HAIR - preserve original colors")
    print("- Indices 11-15: Contains CAPE and SKIN - preserve original browns/skin tones")
    print("\nColors we can safely change:")
    print("- Indices 3-6: Main armor, gloves, boots")

if __name__ == "__main__":
    extract_original_palette()