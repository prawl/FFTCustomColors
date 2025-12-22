#!/usr/bin/env python3
"""
FFT Texture Analysis Tool
Analyzes decompressed .bin files to identify color palettes and texture data structures.
"""

import struct
import os
from collections import Counter

def rgb555_to_rgb888(color_val):
    """Convert 16-bit RGB555 to RGB888 format"""
    r = (color_val & 0x1F) << 3
    g = ((color_val >> 5) & 0x1F) << 3
    b = ((color_val >> 10) & 0x1F) << 3
    return (r, g, b)

def rgb565_to_rgb888(color_val):
    """Convert 16-bit RGB565 to RGB888 format"""
    r = (color_val & 0x1F) << 3
    g = ((color_val >> 5) & 0x3F) << 2
    b = ((color_val >> 11) & 0x1F) << 3
    return (r, g, b)

def analyze_binary_file(filename):
    """Analyze binary file structure for color palettes and texture data"""

    with open(filename, 'rb') as f:
        data = f.read()

    file_size = len(data)
    print(f"File size: {file_size} bytes")
    print(f"File size in hex: 0x{file_size:X}")

    # Analyze byte distribution
    byte_counts = Counter(data)
    print(f"\nMost common bytes: {byte_counts.most_common(10)}")

    # Look for common palette sizes (16, 256 colors)
    # 16 colors * 2 bytes = 32 bytes
    # 256 colors * 2 bytes = 512 bytes

    print("\n" + "="*50)
    print("ANALYZING POTENTIAL 16-BIT COLOR DATA")
    print("="*50)

    # Extract 16-bit values (little endian)
    colors_16bit = []
    for i in range(0, len(data) - 1, 2):
        val = struct.unpack('<H', data[i:i+2])[0]
        colors_16bit.append(val)

    print(f"Total 16-bit values: {len(colors_16bit)}")

    # Look for potential palettes by finding groups of non-zero colors
    potential_palettes = []

    # Check for 16-color palettes (common in PSX games)
    for start_idx in range(0, len(colors_16bit) - 15, 16):
        palette = colors_16bit[start_idx:start_idx + 16]
        non_zero = sum(1 for c in palette if c != 0)

        if non_zero >= 8:  # At least half the colors are non-zero
            potential_palettes.append({
                'offset': start_idx * 2,
                'size': 16,
                'colors': palette,
                'non_zero_count': non_zero
            })

    # Check for 256-color palettes
    for start_idx in range(0, len(colors_16bit) - 255, 256):
        palette = colors_16bit[start_idx:start_idx + 256]
        non_zero = sum(1 for c in palette if c != 0)

        if non_zero >= 32:  # At least 1/8 of colors are non-zero
            potential_palettes.append({
                'offset': start_idx * 2,
                'size': 256,
                'colors': palette,
                'non_zero_count': non_zero
            })

    print(f"\nFound {len(potential_palettes)} potential palettes:")

    for i, palette in enumerate(potential_palettes):
        print(f"\nPalette {i+1}:")
        print(f"  Offset: 0x{palette['offset']:X} ({palette['offset']} bytes)")
        print(f"  Size: {palette['size']} colors")
        print(f"  Non-zero colors: {palette['non_zero_count']}")

        # Show first few colors
        print(f"  First 8 colors (RGB555):")
        for j in range(min(8, len(palette['colors']))):
            color_val = palette['colors'][j]
            if color_val != 0:
                rgb = rgb555_to_rgb888(color_val)
                print(f"    [{j:2d}]: 0x{color_val:04X} -> RGB({rgb[0]:3d},{rgb[1]:3d},{rgb[2]:3d})")
            else:
                print(f"    [{j:2d}]: 0x{color_val:04X} -> RGB(  0,  0,  0)")

    return potential_palettes, colors_16bit, data

def visualize_palettes(potential_palettes):
    """Create text-based visualization of found palettes"""

    if not potential_palettes:
        print("No palettes found to visualize")
        return

    print("\n" + "="*50)
    print("PALETTE VISUALIZATION (RGB VALUES)")
    print("="*50)

    for i, palette in enumerate(potential_palettes):
        print(f"\nPalette {i+1} (Offset: 0x{palette['offset']:X}, Size: {palette['size']}):")
        print("-" * 60)

        colors = palette['colors']

        # Display colors in a grid format
        cols_per_row = 8
        for row in range(0, len(colors), cols_per_row):
            row_colors = colors[row:row + cols_per_row]

            # Color indices
            indices = " ".join(f"{row+j:3d}" for j in range(len(row_colors)))
            print(f"Idx: {indices}")

            # RGB values
            rgb_line = ""
            for j, color_val in enumerate(row_colors):
                if color_val != 0:
                    rgb = rgb555_to_rgb888(color_val)
                    rgb_line += f"{rgb[0]:3d},{rgb[1]:3d},{rgb[2]:3d} "
                else:
                    rgb_line += "  0,  0,  0 "
            print(f"RGB: {rgb_line}")

            # Hex values
            hex_line = " ".join(f"0x{color:04X}" for color in row_colors)
            print(f"Hex: {hex_line}")
            print()

    # Save palette colors to a CSV file for external visualization
    if potential_palettes:
        with open("palette_colors.csv", "w") as f:
            f.write("palette_id,color_index,hex_value,red,green,blue\n")
            for i, palette in enumerate(potential_palettes):
                for j, color_val in enumerate(palette['colors']):
                    rgb = rgb555_to_rgb888(color_val)
                    f.write(f"{i+1},{j},0x{color_val:04X},{rgb[0]},{rgb[1]},{rgb[2]}\n")
        print("Palette colors saved to 'palette_colors.csv' for external visualization")

def analyze_texture_patterns(data):
    """Look for texture/sprite patterns in the data"""

    print("\n" + "="*50)
    print("ANALYZING TEXTURE PATTERNS")
    print("="*50)

    # Look for common texture dimensions (powers of 2, multiples of 8, etc.)
    common_widths = [8, 16, 24, 32, 48, 64, 80, 96, 128, 256]

    for width in common_widths:
        if len(data) % width == 0:
            height = len(data) // width
            print(f"Possible dimensions: {width}x{height}")

    # Look for repeating patterns that might indicate tiles
    tile_sizes = [8, 16, 32, 64]

    for tile_size in tile_sizes:
        if len(data) >= tile_size:
            patterns = {}
            for i in range(0, len(data) - tile_size + 1, tile_size):
                pattern = data[i:i+tile_size]
                pattern_hash = hash(pattern)
                if pattern_hash in patterns:
                    patterns[pattern_hash] += 1
                else:
                    patterns[pattern_hash] = 1

            repeated = {k: v for k, v in patterns.items() if v > 1}
            if repeated:
                print(f"\nFound {len(repeated)} repeated {tile_size}-byte patterns:")
                for pattern_hash, count in sorted(repeated.items(), key=lambda x: x[1], reverse=True)[:5]:
                    print(f"  Pattern appears {count} times")

def dump_hex_sections(data, sections=None):
    """Dump hex sections of the file for manual analysis"""

    if sections is None:
        # Default sections: beginning, middle, end
        sections = [
            (0, min(128, len(data))),  # First 128 bytes
            (len(data)//2 - 64, len(data)//2 + 64),  # Middle 128 bytes
            (max(0, len(data) - 128), len(data))  # Last 128 bytes
        ]

    print("\n" + "="*50)
    print("HEX DUMP SECTIONS")
    print("="*50)

    for i, (start, end) in enumerate(sections):
        print(f"\nSection {i+1}: Bytes 0x{start:X} - 0x{end:X}")
        print("-" * 40)

        for offset in range(start, min(end, len(data)), 16):
            hex_bytes = ' '.join(f'{b:02X}' for b in data[offset:offset+16])
            ascii_chars = ''.join(chr(b) if 32 <= b < 127 else '.' for b in data[offset:offset+16])
            print(f"{offset:08X}: {hex_bytes:<48} {ascii_chars}")

def main():
    """Main analysis function"""

    filename = "tex_830_decompressed.bin"

    if not os.path.exists(filename):
        print(f"Error: File '{filename}' not found!")
        return

    print(f"Analyzing texture file: {filename}")
    print("="*50)

    # Perform analysis
    potential_palettes, colors_16bit, data = analyze_binary_file(filename)

    # Analyze texture patterns
    analyze_texture_patterns(data)

    # Dump hex sections for manual inspection
    dump_hex_sections(data)

    # Visualize palettes if found
    if potential_palettes:
        visualize_palettes(potential_palettes)

    # Save analysis results
    with open("texture_analysis_results.txt", "w") as f:
        f.write(f"FFT Texture Analysis Results\n")
        f.write(f"File: {filename}\n")
        f.write(f"Size: {len(data)} bytes\n\n")

        f.write(f"Found {len(potential_palettes)} potential palettes:\n\n")

        for i, palette in enumerate(potential_palettes):
            f.write(f"Palette {i+1}:\n")
            f.write(f"  Offset: 0x{palette['offset']:X}\n")
            f.write(f"  Size: {palette['size']} colors\n")
            f.write(f"  Non-zero colors: {palette['non_zero_count']}\n")

            f.write(f"  All colors (RGB555):\n")
            for j, color_val in enumerate(palette['colors']):
                rgb = rgb555_to_rgb888(color_val)
                f.write(f"    [{j:3d}]: 0x{color_val:04X} -> RGB({rgb[0]:3d},{rgb[1]:3d},{rgb[2]:3d})\n")
            f.write("\n")

    print(f"\nAnalysis complete! Results saved to 'texture_analysis_results.txt'")

if __name__ == "__main__":
    main()