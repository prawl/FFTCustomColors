#!/usr/bin/env python
"""
Fix Hair Highlight in TEX Files (Direct G2D Edit)

This script directly modifies TEX files to remap hair highlight pixels
from palette index 15 (skin) to index 12 (boots/hair).

This bypasses the need for the FFT Sprite Toolkit by editing the TEX
files directly.

TEX File Format:
- Header: 0x800 (2048) bytes of zeros
- Pixel data: 4-bit indexed (2 pixels per byte)
- High nibble = first pixel, low nibble = second pixel
- Sprite sheet: 512x512 pixels
- Individual sprites: 32x40 pixels

Hair region: localY < 12 (top portion of each sprite)
"""

import os
import sys
import shutil


def fix_hair_highlight_tex(input_path, output_path, hair_y_threshold=12, dry_run=False):
    """
    Remap index 15 to index 12 in hair region of TEX file.

    Args:
        input_path: Path to original TEX file
        output_path: Path to save modified TEX file
        hair_y_threshold: Y position within each sprite below which pixels are "hair"
        dry_run: If True, only analyze without modifying

    Returns:
        Number of pixels remapped
    """
    # TEX file constants
    HEADER_SIZE = 0x800  # 2048 bytes
    SHEET_WIDTH = 512
    SPRITE_WIDTH = 32
    SPRITE_HEIGHT = 40

    with open(input_path, 'rb') as f:
        data = bytearray(f.read())

    print(f"TEX file size: {len(data)} bytes")
    print(f"Header size: {HEADER_SIZE} bytes")
    print(f"Pixel data size: {len(data) - HEADER_SIZE} bytes")
    print()

    # Statistics
    total_index15 = 0
    hair_region_remapped = 0
    face_region_kept = 0

    # Process each byte in the pixel data section
    for i in range(HEADER_SIZE, len(data)):
        byte_val = data[i]
        low_nibble = byte_val & 0x0F
        high_nibble = (byte_val >> 4) & 0x0F

        new_low = low_nibble
        new_high = high_nibble

        # Calculate pixel positions for both nibbles
        pixel_offset = (i - HEADER_SIZE) * 2

        # High nibble = first pixel (even offset)
        if high_nibble == 0xF:
            total_index15 += 1
            pixel_x = pixel_offset % SHEET_WIDTH
            pixel_y = pixel_offset // SHEET_WIDTH
            local_y = pixel_y % SPRITE_HEIGHT

            if local_y < hair_y_threshold:
                new_high = 0xC  # Remap to index 12
                hair_region_remapped += 1
            else:
                face_region_kept += 1

        # Low nibble = second pixel (odd offset)
        if low_nibble == 0xF:
            total_index15 += 1
            pixel_x = (pixel_offset + 1) % SHEET_WIDTH
            pixel_y = (pixel_offset + 1) // SHEET_WIDTH
            local_y = pixel_y % SPRITE_HEIGHT

            if local_y < hair_y_threshold:
                new_low = 0xC  # Remap to index 12
                hair_region_remapped += 1
            else:
                face_region_kept += 1

        # Write back modified byte
        if not dry_run:
            data[i] = (new_high << 4) | new_low

    print(f"Total index 15 pixels found: {total_index15}")
    print(f"Hair region pixels remapped (15->12): {hair_region_remapped}")
    print(f"Face region pixels kept (index 15): {face_region_kept}")

    if not dry_run:
        with open(output_path, 'wb') as f:
            f.write(data)
        print(f"\nSaved modified TEX to: {output_path}")
    else:
        print(f"\n[DRY RUN] Would save to: {output_path}")

    return hair_region_remapped


def main():
    if len(sys.argv) < 2:
        print("Usage: python fix_hair_highlight_tex.py <input_tex> [output_tex] [--dry-run]")
        print()
        print("Example:")
        print("  python fix_hair_highlight_tex.py tex_992.bin tex_992_fixed.bin")
        print()
        print("This script remaps hair highlight pixels from index 15 (skin)")
        print("to index 12 (boots/hair) in the hair region of each sprite.")
        return 1

    input_path = sys.argv[1]
    output_path = sys.argv[2] if len(sys.argv) > 2 and not sys.argv[2].startswith('--') else input_path.replace('.bin', '_fixed.bin')
    dry_run = '--dry-run' in sys.argv

    if not os.path.exists(input_path):
        print(f"Error: Input file not found: {input_path}")
        return 1

    print("=" * 60)
    print("HAIR HIGHLIGHT TEX FIX")
    print("=" * 60)
    print(f"Input:  {input_path}")
    print(f"Output: {output_path}")
    print(f"Mode:   {'DRY RUN' if dry_run else 'LIVE'}")
    print()

    remapped = fix_hair_highlight_tex(input_path, output_path, dry_run=dry_run)

    print()
    print("=" * 60)
    if dry_run:
        print(f"DRY RUN COMPLETE - {remapped} pixels would be remapped")
    else:
        print(f"SUCCESS - {remapped} pixels remapped!")
        print()
        print("Next steps:")
        print("1. Copy the fixed TEX file to your mod's g2d folder")
        print("2. Test in-game to verify hair highlight follows BootsAndHair color")
    print("=" * 60)

    return 0


if __name__ == "__main__":
    sys.exit(main())
