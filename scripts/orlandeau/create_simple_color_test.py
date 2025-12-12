#!/usr/bin/env python3
"""
Create a simpler color test for Orlandeau with just a few distinct colors.
This makes it easier to identify which indices control major elements.
"""

import os
import struct

def create_simple_test_sprite():
    """Create a test sprite with just 4 distinct color groups."""

    # Paths
    base_dir = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    sprites_dir = os.path.join(base_dir, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit")
    source_sprite = os.path.join(sprites_dir, "battle_oru_spr.bin")

    # Create test theme directory - we'll cycle to crimson_knight next
    test_dir = os.path.join(sprites_dir, "sprites_orlandeau_crimson_knight")
    os.makedirs(test_dir, exist_ok=True)

    # Read original sprite
    with open(source_sprite, 'rb') as f:
        sprite_data = bytearray(f.read())

    print("Creating SIMPLE color test for Orlandeau:")
    print("=" * 50)

    # Define color groups - we'll paint indices in groups to see patterns
    # Based on Black Lion analysis, we know palette 0 (first 16 colors) is key

    # Group 1: Indices 0-2 = BLACK (keep original or make black)
    # Group 2: Indices 3-6 = RED (these were blue->gray in Black Lion)
    # Group 3: Indices 7-10 = GREEN (mixed in Black Lion)
    # Group 4: Indices 11-15 = BLUE (rest of palette)

    color_groups = [
        # Group 1: BLACK for indices 0-2
        [(0, 0, 0), (0, 0, 0), (0, 0, 0)],
        # Group 2: RED for indices 3-6 (armor in Black Lion)
        [(255, 0, 0), (255, 0, 0), (255, 0, 0), (255, 0, 0)],
        # Group 3: GREEN for indices 7-10
        [(0, 255, 0), (0, 255, 0), (0, 255, 0), (0, 255, 0)],
        # Group 4: BLUE for indices 11-15
        [(0, 0, 255), (0, 0, 255), (0, 0, 255), (0, 0, 255), (0, 0, 255)]
    ]

    # Flatten the color groups
    test_colors = []
    for group in color_groups:
        test_colors.extend(group)

    print("Simple Color Mapping:")
    print("Indices  0-2:  BLACK (shadow/outline)")
    print("Indices  3-6:  RED (main armor based on Black Lion)")
    print("Indices  7-10: GREEN (secondary elements)")
    print("Indices 11-15: BLUE (additional details)")
    print()

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

    # Save all Orlandeau variant sprites with the test palette
    sprite_variants = [
        "battle_oru_spr.bin",   # Main Orlandeau
        "battle_goru_spr.bin",  # Guest Orlandeau
        "battle_voru_spr.bin"   # Variant Orlandeau
    ]

    print("Creating test sprites in crimson_knight theme slot:")

    for sprite_name in sprite_variants:
        source_path = os.path.join(sprites_dir, sprite_name)
        if os.path.exists(source_path):
            # Read original sprite
            with open(source_path, 'rb') as f:
                variant_data = bytearray(f.read())

            # Apply test palette to palette 0 only
            variant_data[0:32] = sprite_data[0:32]

            # Save to test directory
            output_path = os.path.join(test_dir, sprite_name)
            with open(output_path, 'wb') as f:
                f.write(variant_data)

            print(f"  [OK] Created {sprite_name}")

    print("\n" + "=" * 50)
    print("SIMPLE TEST READY!")
    print(f"Location: {test_dir}")
    print("\nTo test:")
    print("1. Deploy with BuildLinked.ps1")
    print("2. Launch the game")
    print("3. Press F2 once to cycle to crimson_knight")
    print("4. Observe which parts are RED, GREEN, or BLUE")
    print("\nExpected observations based on Black Lion:")
    print("- RED should appear on main armor (indices 3-6)")
    print("- GREEN should appear on secondary elements")
    print("- BLUE should appear on additional details")

    # Create reference file
    reference_file = os.path.join(test_dir, "SIMPLE_COLOR_REFERENCE.txt")
    with open(reference_file, 'w') as f:
        f.write("ORLANDEAU SIMPLE COLOR TEST\n")
        f.write("=" * 30 + "\n\n")
        f.write("Color Groups (Palette 0 only):\n")
        f.write("Indices  0-2:  BLACK (shadow/outline)\n")
        f.write("Indices  3-6:  RED (main armor)\n")
        f.write("Indices  7-10: GREEN (secondary)\n")
        f.write("Indices 11-15: BLUE (details)\n")
        f.write("\n" + "=" * 30 + "\n")
        f.write("Note which parts of Orlandeau show each color!\n")

if __name__ == "__main__":
    create_simple_test_sprite()