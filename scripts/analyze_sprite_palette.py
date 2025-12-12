#!/usr/bin/env python3
"""
Analyze the palette of an FFT sprite to understand color usage.
Useful for understanding existing themes before creating new ones.
"""

import sys
import struct
from pathlib import Path
from collections import Counter

def read_palette(sprite_path):
    """Read the palette from an FFT sprite file."""
    with open(sprite_path, 'rb') as f:
        # First 512 bytes are the palette (256 colors * 2 bytes each)
        palette_data = f.read(512)

    colors = []
    for i in range(0, 512, 2):
        if i < len(palette_data):
            # Read 16-bit color (XBBBBBGGGGGRRRRR format)
            color_bytes = palette_data[i:i+2]
            if len(color_bytes) == 2:
                color = struct.unpack('<H', color_bytes)[0]

                # Extract RGB components (5 bits each)
                r = (color & 0x1F) * 8  # Scale 5-bit to 8-bit
                g = ((color >> 5) & 0x1F) * 8
                b = ((color >> 10) & 0x1F) * 8

                colors.append((r, g, b))

    return colors

def rgb_to_hex(r, g, b):
    """Convert RGB values to hex color."""
    return f"#{r:02x}{g:02x}{b:02x}"

def analyze_palette(sprite_path, reference_sprite=None):
    """Analyze and display the palette of a sprite."""
    print(f"\n=== Analyzing: {sprite_path} ===\n")

    colors = read_palette(sprite_path)

    # Group colors by palette (16 colors each)
    for palette_idx in range(16):
        start = palette_idx * 16
        end = start + 16
        palette = colors[start:end]

        print(f"Palette {palette_idx}:")
        for i, (r, g, b) in enumerate(palette):
            hex_color = rgb_to_hex(r, g, b)
            # Mark important indices based on our research
            marker = ""
            if palette_idx < 8:  # Unit sprite palettes
                if i == 0:
                    marker = " (transparency)"
                elif i in [3, 4, 5]:
                    marker = " (buckles/clasps)"
                elif i in [6, 7, 8, 9, 10]:
                    marker = " (cape/armor)"
                elif i in [11, 12, 13, 14, 15]:
                    marker = " (hair/details)"

            print(f"  [{i:2}] RGB({r:3}, {g:3}, {b:3}) {hex_color}{marker}")
        print()

    # If we have a reference sprite, show differences
    if reference_sprite and Path(reference_sprite).exists():
        print("\n=== Comparing with reference ===\n")
        ref_colors = read_palette(reference_sprite)

        for palette_idx in range(8):  # Only compare unit palettes
            start = palette_idx * 16
            end = start + 16

            differences = []
            for i in range(start, end):
                if i < len(colors) and i < len(ref_colors):
                    if colors[i] != ref_colors[i]:
                        local_idx = i % 16
                        differences.append((local_idx, colors[i], ref_colors[i]))

            if differences:
                print(f"Palette {palette_idx} differences:")
                for idx, (r1, g1, b1), (r2, g2, b2) in differences:
                    hex1 = rgb_to_hex(r1, g1, b1)
                    hex2 = rgb_to_hex(r2, g2, b2)
                    print(f"  Index {idx}: {hex1} -> {hex2}")
                print()

def main():
    if len(sys.argv) < 2:
        print("Usage: python analyze_sprite_palette.py <sprite.bin> [reference.bin]")
        sys.exit(1)

    sprite_path = sys.argv[1]
    reference_sprite = sys.argv[2] if len(sys.argv) > 2 else None

    if not Path(sprite_path).exists():
        print(f"Error: {sprite_path} not found")
        sys.exit(1)

    analyze_palette(sprite_path, reference_sprite)

if __name__ == "__main__":
    main()