#!/usr/bin/env python3
"""
Extract the original color palette from Beowulf's sprite.
"""

import os
import struct

def extract_colors():
    """Extract and display the original Beowulf palette."""

    # Paths
    base_dir = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    sprites_dir = os.path.join(base_dir, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit")
    sprite_path = os.path.join(sprites_dir, "battle_beio_spr.bin")

    if not os.path.exists(sprite_path):
        print(f"Error: {sprite_path} not found!")
        return

    # Read sprite data
    with open(sprite_path, 'rb') as f:
        sprite_data = f.read()

    print("Beowulf Original Palette (Palette 0):")
    print("=" * 50)
    print("Index | R   G   B  | Hex     | Description")
    print("-" * 50)

    # Extract palette 0 (first 16 colors)
    colors = []
    for idx in range(16):
        offset = idx * 2
        color_16bit = struct.unpack('<H', sprite_data[offset:offset+2])[0]

        # Extract 5-bit color channels
        r = (color_16bit & 0x1F) << 3
        g = ((color_16bit >> 5) & 0x1F) << 3
        b = ((color_16bit >> 10) & 0x1F) << 3

        colors.append((r, g, b))

        # Guess description based on index
        if idx == 0:
            desc = "Black shadow"
        elif idx <= 2:
            desc = "Dark shadow/outline"
        elif 3 <= idx <= 6:
            desc = "Armor colors"
        elif 7 <= idx <= 10:
            desc = "Secondary/undergarments"
        elif 11 <= idx <= 15:
            desc = "Cape/skin tones"
        else:
            desc = "Unknown"

        print(f"{idx:3d}   | {r:3d} {g:3d} {b:3d} | #{r:02X}{g:02X}{b:02X} | {desc}")

    print("\n" + "=" * 50)
    print("\nPython list format for copying:")
    print("ORIGINAL_PALETTE = [")
    for i, (r, g, b) in enumerate(colors):
        print(f"    ({r}, {g}, {b}),  # {i}")
    print("]")

if __name__ == "__main__":
    extract_colors()