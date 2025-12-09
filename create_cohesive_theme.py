#!/usr/bin/env python3
"""Create cohesive color themes with proper cape edge and accent handling."""

import os
import struct
import shutil
import argparse
from typing import Tuple

def rgb_to_fft_color(r: int, g: int, b: int) -> int:
    """Convert RGB (0-255) to FFT 16-bit color format (XBBBBBGGGGGRRRRR)."""
    r5 = (r >> 3) & 0x1F
    g5 = (g >> 3) & 0x1F
    b5 = (b >> 3) & 0x1F
    return (b5 << 10) | (g5 << 5) | r5

def fft_color_to_rgb(color: int) -> Tuple[int, int, int]:
    """Convert FFT 16-bit color to RGB (0-255)."""
    r = ((color & 0x1F) << 3) | ((color & 0x1F) >> 2)
    g = (((color >> 5) & 0x1F) << 3) | (((color >> 5) & 0x1F) >> 2)
    b = (((color >> 10) & 0x1F) << 3) | (((color >> 10) & 0x1F) >> 2)
    return r, g, b

def darken_color(r: int, g: int, b: int, factor: float) -> Tuple[int, int, int]:
    """Darken a color by a factor (0.0 = black, 1.0 = original)."""
    return (int(r * factor), int(g * factor), int(b * factor))

def hex_to_rgb(hex_color: str) -> Tuple[int, int, int]:
    """Convert hex color string to RGB tuple."""
    hex_color = hex_color.lstrip('#')
    return tuple(int(hex_color[i:i+2], 16) for i in (0, 2, 4))

def create_cohesive_theme(input_path: str, output_path: str, primary_hex: str,
                         accent_hex: str = None, theme_name: str = "custom"):
    """Create a cohesive color theme with proper cape handling.

    Based on our discoveries:
    - Main cape body: indices 6-10 in palette 0
    - Cape edge/trim: index 7 in palettes 0-3
    - Cape accent/shadows: index 9 in palettes 0-3
    - Buckles/clasps: indices 3-5 in palette 0
    """

    # Create output directory
    os.makedirs(os.path.dirname(output_path), exist_ok=True)

    # Copy original sprite
    shutil.copy2(input_path, output_path)

    # Parse colors
    primary_r, primary_g, primary_b = hex_to_rgb(primary_hex)
    if accent_hex:
        accent_r, accent_g, accent_b = hex_to_rgb(accent_hex)
    else:
        # If no accent color, use a complementary or neutral color
        accent_r, accent_g, accent_b = (200, 180, 100)  # Golden accent

    # Create color variations
    cape_main = rgb_to_fft_color(primary_r, primary_g, primary_b)

    # Cape edge should be 25% darker than main
    edge_r, edge_g, edge_b = darken_color(primary_r, primary_g, primary_b, 0.75)
    cape_edge = rgb_to_fft_color(edge_r, edge_g, edge_b)

    # Cape accent/shadow should be 50% darker than main
    shadow_r, shadow_g, shadow_b = darken_color(primary_r, primary_g, primary_b, 0.5)
    cape_shadow = rgb_to_fft_color(shadow_r, shadow_g, shadow_b)

    # Accent pieces (buckles, clasps)
    accent_color = rgb_to_fft_color(accent_r, accent_g, accent_b)
    accent_dark = rgb_to_fft_color(*darken_color(accent_r, accent_g, accent_b, 0.7))

    print(f"\nCreating {theme_name} theme:")
    print(f"  Primary: RGB({primary_r}, {primary_g}, {primary_b})")
    print(f"  Cape Edge (25% darker): RGB({edge_r}, {edge_g}, {edge_b})")
    print(f"  Cape Shadow (50% darker): RGB({shadow_r}, {shadow_g}, {shadow_b})")
    print(f"  Accent: RGB({accent_r}, {accent_g}, {accent_b})")

    # Open and modify the sprite
    with open(output_path, 'r+b') as f:
        data = bytearray(f.read())

        # PALETTE 0 - Main sprite colors
        # Accent pieces (buckles, clasps, trim)
        data[6:8] = struct.pack('<H', accent_dark)    # Index 3
        data[8:10] = struct.pack('<H', accent_color)  # Index 4
        data[10:12] = struct.pack('<H', accent_dark)  # Index 5

        # Main cape body
        data[12:14] = struct.pack('<H', cape_main)    # Index 6
        data[14:16] = struct.pack('<H', cape_edge)    # Index 7 (also edge)
        data[16:18] = struct.pack('<H', cape_main)    # Index 8
        data[18:20] = struct.pack('<H', cape_shadow)  # Index 9 (also shadow)
        data[20:22] = struct.pack('<H', cape_main)    # Index 10

        # CRITICAL: Apply cape edge and shadow to multiple palettes
        # This is what makes the cape edges and accents actually appear!
        for palette in range(1, 4):  # Palettes 1-3
            palette_offset = palette * 32  # Each palette is 16 colors * 2 bytes

            # Cape edge (index 7 in each palette)
            pos = palette_offset + (7 * 2)
            data[pos:pos+2] = struct.pack('<H', cape_edge)

            # Cape shadow/accent (index 9 in each palette)
            pos = palette_offset + (9 * 2)
            data[pos:pos+2] = struct.pack('<H', cape_shadow)

            # Also update indices 12-15 for consistency
            # These seem to be additional armor/cape details
            for idx in range(12, 16):
                pos = palette_offset + (idx * 2)
                # Use gradually darker shades for depth
                darkness = 0.8 - ((idx - 12) * 0.1)
                r, g, b = darken_color(primary_r, primary_g, primary_b, darkness)
                data[pos:pos+2] = struct.pack('<H', rgb_to_fft_color(r, g, b))

        # Write back
        f.seek(0)
        f.write(data)

    print(f"[OK] Created cohesive theme: {output_path}")

def process_all_sprites(theme_name: str, primary_hex: str, accent_hex: str = None):
    """Apply theme to all sprites in a directory."""

    base_dir = "ColorMod/FFTIVC/data/enhanced/fftpack/unit"
    input_dir = f"{base_dir}/sprites_original"
    output_dir = f"{base_dir}/sprites_{theme_name}"

    os.makedirs(output_dir, exist_ok=True)

    # Process all sprite files
    sprite_files = [f for f in os.listdir(input_dir) if f.endswith('_spr.bin')]

    for sprite_file in sprite_files:
        input_path = os.path.join(input_dir, sprite_file)
        output_path = os.path.join(output_dir, sprite_file)

        create_cohesive_theme(input_path, output_path, primary_hex, accent_hex, theme_name)

    print(f"\n[OK] Processed {len(sprite_files)} sprites for {theme_name} theme")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Create cohesive FFT color themes")
    parser.add_argument("--name", required=True, help="Theme name")
    parser.add_argument("--primary", required=True, help="Primary color (hex)")
    parser.add_argument("--accent", help="Accent color (hex, optional)")
    parser.add_argument("--single", help="Process single sprite file (for testing)")

    args = parser.parse_args()

    if args.single:
        # Test with single sprite
        input_path = "ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_original/battle_knight_m_spr.bin"
        output_path = f"ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_{args.name}/battle_knight_m_spr.bin"
        create_cohesive_theme(input_path, output_path, args.primary, args.accent, args.name)
    else:
        # Process all sprites
        process_all_sprites(args.name, args.primary, args.accent)