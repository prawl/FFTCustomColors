#!/usr/bin/env python
"""
Fix Hair Highlight in SPR Files (Preview Fix)

This script modifies SPR files (used for Config UI previews) to remap hair highlight
pixels from palette index 15 (skin) to index 12 (boots/hair).

SPR File Format:
- Header: 512 bytes (16 palettes, 32 bytes each)
- Pixel data: 4-bit indexed (2 pixels per byte)
- Low nibble = first pixel, high nibble = second pixel (opposite of TEX!)
- Sprite sheet: 256 pixels wide
- Individual sprites: 32x40 pixels

Hair region: localY < 12 (top portion of each sprite)
"""

import os
import sys
import glob


def fix_hair_highlight_spr(input_path, output_path=None, hair_y_threshold=12, dry_run=False):
    """
    Remap index 15 to index 12 in hair region of SPR file.

    Args:
        input_path: Path to original SPR file
        output_path: Path to save modified SPR file (None = modify in place)
        hair_y_threshold: Y position within each sprite below which pixels are "hair"
        dry_run: If True, only analyze without modifying

    Returns:
        Number of pixels remapped
    """
    # SPR file constants (different from TEX!)
    PALETTE_SIZE = 512  # 16 palettes * 32 bytes
    SHEET_WIDTH = 256
    SPRITE_WIDTH = 32
    SPRITE_HEIGHT = 40

    if output_path is None:
        output_path = input_path

    with open(input_path, 'rb') as f:
        data = bytearray(f.read())

    # Statistics
    total_index15 = 0
    hair_region_remapped = 0
    face_region_kept = 0

    # Process each byte in the pixel data section
    for i in range(PALETTE_SIZE, len(data)):
        byte_val = data[i]
        # SPR uses opposite nibble order from TEX!
        low_nibble = byte_val & 0x0F       # First pixel
        high_nibble = (byte_val >> 4) & 0x0F  # Second pixel

        new_low = low_nibble
        new_high = high_nibble

        # Calculate pixel positions for both nibbles
        pixel_offset = (i - PALETTE_SIZE) * 2

        # Low nibble = first pixel (even offset)
        if low_nibble == 0xF:
            total_index15 += 1
            pixel_x = pixel_offset % SHEET_WIDTH
            pixel_y = pixel_offset // SHEET_WIDTH
            local_y = pixel_y % SPRITE_HEIGHT

            if local_y < hair_y_threshold:
                new_low = 0xC  # Remap to index 12
                hair_region_remapped += 1
            else:
                face_region_kept += 1

        # High nibble = second pixel (odd offset)
        if high_nibble == 0xF:
            total_index15 += 1
            pixel_x = (pixel_offset + 1) % SHEET_WIDTH
            pixel_y = (pixel_offset + 1) // SHEET_WIDTH
            local_y = pixel_y % SPRITE_HEIGHT

            if local_y < hair_y_threshold:
                new_high = 0xC  # Remap to index 12
                hair_region_remapped += 1
            else:
                face_region_kept += 1

        # Write back modified byte
        if not dry_run:
            data[i] = (new_high << 4) | new_low

    if not dry_run:
        with open(output_path, 'wb') as f:
            f.write(data)

    return hair_region_remapped, face_region_kept, total_index15


def process_all_themes(base_path, sprite_filename, hair_y_threshold=12, dry_run=False):
    """
    Process all theme variants of a sprite file.

    Args:
        base_path: Path to the ColorMod FFTIVC directory
        sprite_filename: Name of the sprite file (e.g., "battle_mina_m_spr.bin")
        hair_y_threshold: Y threshold for hair region
        dry_run: If True, only analyze

    Returns:
        Total number of files processed
    """
    # Find all theme directories
    unit_path = os.path.join(base_path, "data", "enhanced", "fftpack", "unit")
    theme_dirs = glob.glob(os.path.join(unit_path, "sprites_*"))

    processed = 0
    total_remapped = 0

    print(f"Looking for {sprite_filename} in theme directories...")
    print(f"Base path: {unit_path}")
    print()

    for theme_dir in sorted(theme_dirs):
        theme_name = os.path.basename(theme_dir)
        spr_path = os.path.join(theme_dir, sprite_filename)

        if os.path.exists(spr_path):
            remapped, kept, total = fix_hair_highlight_spr(
                spr_path,
                hair_y_threshold=hair_y_threshold,
                dry_run=dry_run
            )
            processed += 1
            total_remapped += remapped
            print(f"  {theme_name}: {remapped} pixels remapped, {kept} kept")

    return processed, total_remapped


def main():
    if len(sys.argv) < 2:
        print("Usage: python fix_hair_highlight_spr.py <command> [options]")
        print()
        print("Commands:")
        print("  single <input_spr> [output_spr]  - Fix a single SPR file")
        print("  all <base_path> <sprite_name>    - Fix all theme variants")
        print()
        print("Options:")
        print("  --dry-run   Only analyze, don't modify files")
        print("  --threshold N  Set hair Y threshold (default: 12)")
        print()
        print("Examples:")
        print("  python fix_hair_highlight_spr.py single battle_mina_m_spr.bin")
        print("  python fix_hair_highlight_spr.py all ./ColorMod/FFTIVC battle_mina_m_spr.bin")
        return 1

    command = sys.argv[1]
    dry_run = '--dry-run' in sys.argv

    # Parse threshold
    threshold = 12
    if '--threshold' in sys.argv:
        idx = sys.argv.index('--threshold')
        if idx + 1 < len(sys.argv):
            threshold = int(sys.argv[idx + 1])

    print("=" * 60)
    print("HAIR HIGHLIGHT SPR FIX (Preview Files)")
    print("=" * 60)
    print(f"Mode: {'DRY RUN' if dry_run else 'LIVE'}")
    print(f"Hair Y threshold: {threshold}")
    print()

    if command == 'single':
        if len(sys.argv) < 3:
            print("Error: Missing input file path")
            return 1

        input_path = sys.argv[2]
        output_path = None
        for arg in sys.argv[3:]:
            if not arg.startswith('--'):
                output_path = arg
                break

        if not os.path.exists(input_path):
            print(f"Error: File not found: {input_path}")
            return 1

        remapped, kept, total = fix_hair_highlight_spr(
            input_path, output_path, threshold, dry_run
        )

        print(f"File: {input_path}")
        print(f"Total index 15 pixels: {total}")
        print(f"Hair region remapped: {remapped}")
        print(f"Face region kept: {kept}")

        if not dry_run:
            out = output_path or input_path
            print(f"Saved to: {out}")

    elif command == 'all':
        if len(sys.argv) < 4:
            print("Error: Missing base_path or sprite_name")
            return 1

        base_path = sys.argv[2]
        sprite_name = sys.argv[3]

        if not os.path.isdir(base_path):
            print(f"Error: Directory not found: {base_path}")
            return 1

        processed, total_remapped = process_all_themes(
            base_path, sprite_name, threshold, dry_run
        )

        print()
        print(f"Processed {processed} theme variants")
        print(f"Total pixels remapped: {total_remapped}")

    else:
        print(f"Unknown command: {command}")
        return 1

    print()
    print("=" * 60)
    if dry_run:
        print("DRY RUN COMPLETE - No files modified")
    else:
        print("SUCCESS - SPR files patched for preview fix!")
    print("=" * 60)

    return 0


if __name__ == "__main__":
    sys.exit(main())
