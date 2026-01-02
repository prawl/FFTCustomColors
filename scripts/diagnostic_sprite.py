"""
Diagnostic Sprite Generator for FFT Color Customizer
Replaces palette with rainbow colors for visual section identification.

This tool helps identify which palette indices control which visual sections
(cape, boots, armor, etc.) by replacing each index with a distinct color.

Usage:
    python diagnostic_sprite.py input.bin output_diagnostic.bin
    python diagnostic_sprite.py input.bin  # outputs to input_diagnostic.bin

Example:
    python diagnostic_sprite.py battle_knight_m_spr.bin
    python diagnostic_sprite.py battle_mina_m_spr.bin squire_m_diagnostic.bin
"""

import sys
import os

# Rainbow palette - each index gets a distinct, easily identifiable color
# These colors are chosen to be visually distinct from each other
# Updated: indices 11-15 now use more distinguishable colors
DIAGNOSTIC_COLORS = [
    (0, 0, 0),        # 0: Transparent (ignored by game)
    (255, 0, 0),      # 1: RED
    (255, 128, 0),    # 2: ORANGE
    (255, 255, 0),    # 3: YELLOW
    (128, 255, 0),    # 4: LIME
    (0, 255, 0),      # 5: GREEN
    (0, 255, 128),    # 6: SPRING GREEN
    (0, 255, 255),    # 7: CYAN
    (0, 128, 255),    # 8: SKY BLUE
    (0, 0, 255),      # 9: BLUE
    (128, 0, 255),    # 10: PURPLE
    (255, 0, 255),    # 11: MAGENTA
    (255, 0, 128),    # 12: PINK
    (128, 64, 0),     # 13: BROWN
    (255, 255, 255),  # 14: WHITE
    (64, 64, 64),     # 15: DARK GRAY
]

# Color name lookup for documentation
COLOR_NAMES = [
    "Transparent", "Red", "Orange", "Yellow", "Lime", "Green",
    "Cyan", "Blue", "Purple", "Magenta", "Pink", "Teal",
    "Salmon", "Black", "Brown", "White"
]


def rgb_to_bgr555(r, g, b):
    """Convert RGB888 to BGR555 format used by FFT."""
    r5 = (r * 31) // 255
    g5 = (g * 31) // 255
    b5 = (b * 31) // 255
    return r5 | (g5 << 5) | (b5 << 10)


def create_diagnostic_sprite(input_path, output_path):
    """Replace palette 0 with rainbow colors for visual identification."""

    if not os.path.exists(input_path):
        print(f"Error: Input file not found: {input_path}")
        return False

    with open(input_path, 'rb') as f:
        data = bytearray(f.read())

    if len(data) < 512:
        print(f"Error: File too small ({len(data)} bytes). Expected at least 512 bytes for palette data.")
        return False

    # Write rainbow colors to palette 0 (bytes 0-31, 16 colors x 2 bytes)
    for i, (r, g, b) in enumerate(DIAGNOSTIC_COLORS):
        bgr555 = rgb_to_bgr555(r, g, b)
        offset = i * 2
        data[offset] = bgr555 & 0xFF
        data[offset + 1] = (bgr555 >> 8) & 0xFF

    with open(output_path, 'wb') as f:
        f.write(data)

    return True


def print_color_reference():
    """Print the color reference table for documentation."""
    print("\nPalette Index Color Reference:")
    print("-" * 40)
    for i, (name, (r, g, b)) in enumerate(zip(COLOR_NAMES, DIAGNOSTIC_COLORS)):
        hex_color = f"#{r:02X}{g:02X}{b:02X}"
        print(f"  Index {i:2d}: {name:12s} {hex_color}")
    print("-" * 40)


def main():
    if len(sys.argv) < 2:
        print("Diagnostic Sprite Generator for FFT Color Customizer")
        print("=" * 50)
        print("\nUsage:")
        print("  python diagnostic_sprite.py <input.bin> [output.bin]")
        print("\nExamples:")
        print("  python diagnostic_sprite.py battle_knight_m_spr.bin")
        print("  python diagnostic_sprite.py battle_mina_m_spr.bin squire_diagnostic.bin")
        print_color_reference()
        sys.exit(1)

    input_path = sys.argv[1]

    # Generate output path if not provided
    if len(sys.argv) >= 3:
        output_path = sys.argv[2]
    else:
        base, ext = os.path.splitext(input_path)
        output_path = f"{base}_diagnostic{ext}"

    print(f"Input:  {input_path}")
    print(f"Output: {output_path}")

    if create_diagnostic_sprite(input_path, output_path):
        print(f"\nSuccess! Created diagnostic sprite: {output_path}")
        print("\nNext steps:")
        print("1. View the diagnostic sprite in the preview tool or in-game")
        print("2. Note which colors appear on each body part")
        print("3. Use the color reference below to identify palette indices")
        print_color_reference()
        print("\nExample observations:")
        print('  "The cape shows Yellow(3), Lime(4), Green(5)"')
        print('  "The boots show Cyan(6), Blue(7)"')
        print('  "The armor shows Purple(8), Magenta(9), Pink(10)"')
    else:
        sys.exit(1)


if __name__ == "__main__":
    main()
