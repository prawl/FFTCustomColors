#!/usr/bin/env python3
"""
Extract the original color palette from both Agrias sprites.
Agrias has two sprite files:
- battle_aguri_spr.bin (main Agrias)
- battle_kanba_spr.bin (guest Agrias)
"""

import os
import struct

def extract_colors_from_sprite(sprite_path, sprite_name):
    """Extract and display the original palette from a single sprite."""

    if not os.path.exists(sprite_path):
        print(f"Error: {sprite_path} not found!")
        return None

    # Read sprite data
    with open(sprite_path, 'rb') as f:
        sprite_data = f.read()

    print(f"{sprite_name} Original Palette (Palette 0):")
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
    print(f"\nPython list format for {sprite_name}:")
    print(f"ORIGINAL_PALETTE_{sprite_name.upper().replace(' ', '_')} = [")
    for i, (r, g, b) in enumerate(colors):
        print(f"    ({r}, {g}, {b}),  # {i}")
    print("]")

    return colors

def extract_colors():
    """Extract and display the original palettes from both Agrias sprites."""

    # Paths
    base_dir = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    sprites_dir = os.path.join(base_dir, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit")

    # Agrias sprite files
    agrias_sprites = [
        ("battle_aguri_spr.bin", "Main Agrias"),
        ("battle_kanba_spr.bin", "Guest Agrias")
    ]

    print("AGRIAS PALETTE EXTRACTION")
    print("=" * 60)
    print("Extracting palettes from both Agrias sprite files:")
    print()

    all_palettes = {}

    for sprite_file, sprite_name in agrias_sprites:
        sprite_path = os.path.join(sprites_dir, sprite_file)
        colors = extract_colors_from_sprite(sprite_path, sprite_name)
        if colors:
            all_palettes[sprite_name] = colors
        print("\n" + "=" * 60 + "\n")

    # Compare palettes if both were extracted
    if len(all_palettes) == 2:
        main_colors = all_palettes["Main Agrias"]
        guest_colors = all_palettes["Guest Agrias"]

        print("PALETTE COMPARISON:")
        print("=" * 60)
        print("Index | Main Agrias    | Guest Agrias   | Match?")
        print("-" * 60)

        matches = 0
        for idx in range(16):
            main_hex = f"#{main_colors[idx][0]:02X}{main_colors[idx][1]:02X}{main_colors[idx][2]:02X}"
            guest_hex = f"#{guest_colors[idx][0]:02X}{guest_colors[idx][1]:02X}{guest_colors[idx][2]:02X}"
            match = "✓" if main_colors[idx] == guest_colors[idx] else "✗"
            if main_colors[idx] == guest_colors[idx]:
                matches += 1

            print(f"{idx:3d}   | {main_hex:10s}     | {guest_hex:10s}     | {match}")

        print("-" * 60)
        print(f"Matching colors: {matches}/16 ({matches/16*100:.1f}%)")

        if matches == 16:
            print("\n✓ Both sprites use identical palettes - single theme can work for both!")
        else:
            print(f"\n⚠ Sprites have different palettes - may need separate themes or careful color selection")

if __name__ == "__main__":
    extract_colors()