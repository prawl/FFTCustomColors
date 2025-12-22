#!/usr/bin/env python3
"""
Compare two tex files to find differences
"""

import struct
import sys

def rgb555_to_rgb888(color_val):
    """Convert 16-bit RGB555 to RGB888 format"""
    r = (color_val & 0x1F) << 3
    g = ((color_val >> 5) & 0x1F) << 3
    b = ((color_val >> 10) & 0x1F) << 3
    return (r, g, b)

def compare_tex_files(file1, file2):
    """Compare two tex files and highlight differences"""

    with open(file1, 'rb') as f1:
        data1 = f1.read()
    with open(file2, 'rb') as f2:
        data2 = f2.read()

    if len(data1) != len(data2):
        print(f"WARNING: Files have different sizes! {len(data1)} vs {len(data2)}")
        min_len = min(len(data1), len(data2))
    else:
        min_len = len(data1)
        print(f"Both files are {min_len} bytes")

    # Find all differences
    differences = []
    for i in range(0, min_len - 1, 2):  # Process as 16-bit values
        val1 = struct.unpack('<H', data1[i:i+2])[0]
        val2 = struct.unpack('<H', data2[i:i+2])[0]

        if val1 != val2:
            differences.append({
                'offset': i,
                'val1': val1,
                'val2': val2
            })

    print(f"\nFound {len(differences)} different 16-bit values")

    # Group differences by regions
    if differences:
        print("\nDifference regions:")
        current_region_start = differences[0]['offset']
        last_offset = differences[0]['offset']
        region_count = 0

        for diff in differences[1:]:
            if diff['offset'] - last_offset > 32:  # New region if gap > 32 bytes
                print(f"  Region {region_count}: 0x{current_region_start:04X} - 0x{last_offset:04X}")
                current_region_start = diff['offset']
                region_count += 1
            last_offset = diff['offset']

        print(f"  Region {region_count}: 0x{current_region_start:04X} - 0x{last_offset:04X}")

    # Show first 20 differences with color interpretation
    print(f"\nFirst {min(20, len(differences))} differences (as potential colors):")
    print("Offset    | Original          | Modified")
    print("-" * 60)

    for diff in differences[:20]:
        rgb1 = rgb555_to_rgb888(diff['val1'])
        rgb2 = rgb555_to_rgb888(diff['val2'])

        print(f"0x{diff['offset']:04X}   | 0x{diff['val1']:04X} RGB({rgb1[0]:3},{rgb1[1]:3},{rgb1[2]:3}) | "
              f"0x{diff['val2']:04X} RGB({rgb2[0]:3},{rgb2[1]:3},{rgb2[2]:3})")

    # Check specific known offsets
    print("\n\nChecking known offsets:")
    known_offsets = {
        0x0E50: "Hair color (from RamzaTexGenerator)",
        0x0E52: "Hair color 2",
        0x0E54: "Hair color 3",
        # Add more known offsets as we discover them
    }

    for offset, description in known_offsets.items():
        if offset < min_len - 1:
            val1 = struct.unpack('<H', data1[offset:offset+2])[0]
            val2 = struct.unpack('<H', data2[offset:offset+2])[0]
            rgb1 = rgb555_to_rgb888(val1)
            rgb2 = rgb555_to_rgb888(val2)

            if val1 != val2:
                print(f"  0x{offset:04X} - {description}:")
                print(f"    Original: 0x{val1:04X} RGB({rgb1[0]:3},{rgb1[1]:3},{rgb1[2]:3})")
                print(f"    Modified: 0x{val2:04X} RGB({rgb2[0]:3},{rgb2[1]:3},{rgb2[2]:3})")
            else:
                print(f"  0x{offset:04X} - {description}: No change")

    return differences

if __name__ == "__main__":
    # Compare original vs white_heretic
    original = r"C:\Users\ptyRa\Dev\FFTColorCustomizer\ColorMod\FFTIVC\data\enhanced\system\ffto\g2d\original_backup\tex_830.bin"
    white = r"C:\Users\ptyRa\Dev\FFTColorCustomizer\ColorMod\FFTIVC\data\enhanced\system\ffto\g2d\white_heretic\tex_830.bin"

    print("Comparing tex_830.bin files:")
    print("Original:", original)
    print("White Heretic:", white)
    print("=" * 60)

    differences = compare_tex_files(original, white)