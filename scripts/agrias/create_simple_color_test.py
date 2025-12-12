#!/usr/bin/env python3
"""
Create a simple color test for Agrias with distinct colors.
This helps identify which indices control major sprite elements.
Agrias has two sprite files:
- battle_aguri_spr.bin (main Agrias)
- battle_kanba_spr.bin (guest Agrias)
"""

import os
import struct

def create_test_sprite(source_sprite, output_sprite, sprite_name):
    """Create a test sprite with 4 distinct color groups."""

    if not os.path.exists(source_sprite):
        print(f"Error: {source_sprite} not found!")
        return False

    # Read original sprite
    with open(source_sprite, 'rb') as f:
        sprite_data = bytearray(f.read())

    print(f"Processing {sprite_name}:")

    # Define color groups to identify palette indices
    # Group 1: Indices 0-2 = BLACK (shadows/outlines)
    # Group 2: Indices 3-6 = RED (main armor)
    # Group 3: Indices 7-10 = GREEN (secondary elements/undergarments)
    # Group 4: Indices 11-15 = BLUE (cape/additional details)

    color_groups = [
        # Group 1: BLACK for indices 0-2
        [(0, 0, 0), (0, 0, 0), (0, 0, 0)],
        # Group 2: RED for indices 3-6 (expected main armor)
        [(255, 0, 0), (255, 0, 0), (255, 0, 0), (255, 0, 0)],
        # Group 3: GREEN for indices 7-10 (expected secondary elements)
        [(0, 255, 0), (0, 255, 0), (0, 255, 0), (0, 255, 0)],
        # Group 4: BLUE for indices 11-15 (expected cape/details)
        [(0, 0, 255), (0, 0, 255), (0, 0, 255), (0, 0, 255), (0, 0, 255)]
    ]

    # Flatten the color groups
    test_colors = []
    for group in color_groups:
        test_colors.extend(group)

    # Apply colors to ONLY palette 0 (first 32 bytes)
    for idx in range(16):
        r, g, b = test_colors[idx]

        # Convert to 16-bit color format (5 bits per channel)
        r_5bit = min(31, r >> 3)
        g_5bit = min(31, g >> 3)
        b_5bit = min(31, b >> 3)
        color_16bit = (b_5bit << 10) | (g_5bit << 5) | r_5bit

        # Write to palette 0 only
        offset = idx * 2
        sprite_data[offset:offset+2] = struct.pack('<H', color_16bit)

    # Save the test sprite
    with open(output_sprite, 'wb') as f:
        f.write(sprite_data)

    print(f"  [OK] Created {os.path.basename(output_sprite)}")
    return True

def create_simple_test_sprites():
    """Create test sprites for both Agrias variants."""

    # Paths
    base_dir = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    sprites_dir = os.path.join(base_dir, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit")

    # Create test theme directory
    test_dir = os.path.join(sprites_dir, "sprites_agrias_test")
    os.makedirs(test_dir, exist_ok=True)

    print("Creating SIMPLE color test for Agrias:")
    print("=" * 50)

    # Agrias sprite files
    agrias_sprites = [
        ("battle_aguri_spr.bin", "Main Agrias"),
        ("battle_kanba_spr.bin", "Guest Agrias")
    ]

    success_count = 0
    for sprite_file, sprite_name in agrias_sprites:
        source_sprite = os.path.join(sprites_dir, sprite_file)
        output_sprite = os.path.join(test_dir, sprite_file)

        if create_test_sprite(source_sprite, output_sprite, sprite_name):
            success_count += 1

    print("\nSimple Color Mapping:")
    print("Indices  0-2:  BLACK (shadow/outline)")
    print("Indices  3-6:  RED (main armor)")
    print("Indices  7-10: GREEN (secondary elements)")
    print("Indices 11-15: BLUE (cape/additional details)")
    print()

    if success_count == 2:
        print("=" * 50)
        print("SIMPLE TEST READY!")
        print(f"Location: {test_dir}")
        print("\nTo test:")
        print("1. Deploy with BuildLinked.ps1")
        print("2. Launch the game with Agrias in your party")
        print("3. Use F2 to cycle to the test theme")
        print("4. Observe which parts are RED, GREEN, or BLUE")
        print("\nExpected observations:")
        print("- RED should appear on main armor (indices 3-6)")
        print("- GREEN should appear on secondary elements/undergarments")
        print("- BLUE should appear on cape/additional details")
        print("\nIMPORTANT:")
        print("- Test with BOTH main Agrias and guest Agrias if possible")
        print("- Note any differences between the two sprite variants")
        print("- Pay attention to face/skin areas for unexpected color mapping")

        # Create reference file
        reference_file = os.path.join(test_dir, "COLOR_REFERENCE.txt")
        with open(reference_file, 'w') as f:
            f.write("AGRIAS SIMPLE COLOR TEST\n")
            f.write("=" * 30 + "\n\n")
            f.write("Color Groups (Palette 0 only):\n")
            f.write("Indices  0-2:  BLACK (shadow/outline)\n")
            f.write("Indices  3-6:  RED (main armor)\n")
            f.write("Indices  7-10: GREEN (secondary)\n")
            f.write("Indices 11-15: BLUE (cape/details)\n")
            f.write("\n" + "=" * 30 + "\n")
            f.write("CRITICAL: Test with BOTH Agrias variants!\n")
            f.write("- Main Agrias (battle_aguri_spr.bin)\n")
            f.write("- Guest Agrias (battle_kanba_spr.bin)\n")
            f.write("\nNote which parts of each show each color!\n")
            f.write("Document any differences between variants!\n")
    else:
        print(f"⚠ Warning: Only {success_count}/2 sprites were created successfully")

if __name__ == "__main__":
    create_simple_test_sprites()