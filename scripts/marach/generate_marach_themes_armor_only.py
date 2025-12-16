#!/usr/bin/env python3
"""Generate 50 themes for Marach that change armor but preserve face/skin colors."""

import os
import struct
import shutil
from typing import Tuple
from pathlib import Path
import subprocess

def rgb_to_fft_color(r: int, g: int, b: int) -> int:
    """Convert RGB (0-255) to FFT 16-bit color format (XBBBBBGGGGGRRRRR)."""
    r5 = (r >> 3) & 0x1F
    g5 = (g >> 3) & 0x1F
    b5 = (b >> 3) & 0x1F
    return (b5 << 10) | (g5 << 5) | r5

def fft_color_to_rgb(color_bytes: bytes) -> Tuple[int, int, int]:
    """Convert FFT 16-bit color to RGB (0-255)."""
    color = struct.unpack('<H', color_bytes)[0]
    r = ((color & 0x1F) << 3) | ((color & 0x1F) >> 2)
    g = (((color >> 5) & 0x1F) << 3) | (((color >> 5) & 0x1F) >> 2)
    b = (((color >> 10) & 0x1F) << 3) | (((color >> 10) & 0x1F) >> 2)
    return r, g, b

def darken_color(r: int, g: int, b: int, factor: float) -> Tuple[int, int, int]:
    """Darken a color by a factor (0.0 = black, 1.0 = original)."""
    return (int(r * factor), int(g * factor), int(b * factor))

def lighten_color(r: int, g: int, b: int, factor: float) -> Tuple[int, int, int]:
    """Lighten a color by blending with white."""
    return (
        min(255, int(r + (255 - r) * factor)),
        min(255, int(g + (255 - g) * factor)),
        min(255, int(b + (255 - b) * factor))
    )

def hex_to_rgb(hex_color: str) -> Tuple[int, int, int]:
    """Convert hex color string to RGB tuple."""
    hex_color = hex_color.lstrip('#')
    return tuple(int(hex_color[i:i+2], 16) for i in (0, 2, 4))

def create_marach_theme_armor_only(input_path: str, output_path: str, primary_hex: str,
                                   accent_hex: str = None, theme_name: str = "custom"):
    """Create a themed Marach sprite that preserves face/skin colors.

    Based on FFT sprite structure:
    - Indices 0-2: Typically transparent/background
    - Indices 3-5: Trim, buckles, accents
    - Indices 6-10: Main armor/clothing
    - Indices 11-15: Often includes skin tones (PRESERVE THESE)
    - Indices 20+: Additional armor elements

    We'll modify only the armor indices while preserving skin tones.
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
        # If no accent color, use a complementary color
        accent_r = min(255, primary_r + 60)
        accent_g = min(255, primary_g + 60)
        accent_b = min(255, primary_b + 60)

    # Create color variations
    main_color = rgb_to_fft_color(primary_r, primary_g, primary_b)

    # Edge should be 25% darker than main
    edge_r, edge_g, edge_b = darken_color(primary_r, primary_g, primary_b, 0.75)
    edge_color = rgb_to_fft_color(edge_r, edge_g, edge_b)

    # Shadow should be 50% darker than main
    shadow_r, shadow_g, shadow_b = darken_color(primary_r, primary_g, primary_b, 0.5)
    shadow_color = rgb_to_fft_color(shadow_r, shadow_g, shadow_b)

    # Accent pieces
    accent_color = rgb_to_fft_color(accent_r, accent_g, accent_b)
    accent_dark = rgb_to_fft_color(*darken_color(accent_r, accent_g, accent_b, 0.7))

    print(f"Creating {theme_name} theme (armor only)...")

    # Open and modify the sprite
    with open(output_path, 'r+b') as f:
        data = bytearray(f.read())

        # Store original skin colors from indices 11-19 (typical skin tone range)
        original_skin_colors = []
        for i in range(11, 20):
            pos = i * 2
            if pos + 2 <= len(data):
                original_skin_colors.append(data[pos:pos+2])

        # PALETTE 0 - Main sprite colors
        # Accent pieces (buckles, clasps, trim) - indices 3-5
        data[6:8] = struct.pack('<H', accent_dark)    # Index 3
        data[8:10] = struct.pack('<H', accent_color)  # Index 4
        data[10:12] = struct.pack('<H', accent_dark)  # Index 5

        # Main armor/clothing - indices 6-10
        data[12:14] = struct.pack('<H', main_color)    # Index 6
        data[14:16] = struct.pack('<H', edge_color)    # Index 7 (edge)
        data[16:18] = struct.pack('<H', main_color)    # Index 8
        data[18:20] = struct.pack('<H', shadow_color)  # Index 9 (shadow)
        data[20:22] = struct.pack('<H', main_color)    # Index 10

        # PRESERVE indices 11-19 (skin tones) - restore original values
        for i, color_bytes in enumerate(original_skin_colors):
            pos = (11 + i) * 2
            if pos + 2 <= len(data) and color_bytes:
                data[pos:pos+2] = color_bytes

        # Additional armor colors (indices 20-31)
        for i in range(20, 32):
            if i < 26:
                # Use main color variations
                pos = i * 2
                if pos + 2 <= len(data):
                    variation_factor = 0.9 - ((i - 20) * 0.05)
                    var_r, var_g, var_b = darken_color(primary_r, primary_g, primary_b, variation_factor)
                    data[pos:pos+2] = struct.pack('<H', rgb_to_fft_color(var_r, var_g, var_b))
            else:
                # Use accent variations
                pos = i * 2
                if pos + 2 <= len(data):
                    variation_factor = 0.85 - ((i - 26) * 0.05)
                    var_r, var_g, var_b = darken_color(accent_r, accent_g, accent_b, variation_factor)
                    data[pos:pos+2] = struct.pack('<H', rgb_to_fft_color(var_r, var_g, var_b))

        # Apply to additional palettes for consistency
        for palette in range(1, 4):  # Palettes 1-3
            palette_offset = palette * 32  # Each palette is 16 colors * 2 bytes

            # Store original skin colors for this palette
            palette_skin_colors = []
            for i in range(11, 20):
                pos = palette_offset + (i * 2)
                if pos + 2 <= len(data):
                    palette_skin_colors.append(data[pos:pos+2])

            # Apply armor colors but preserve skin
            # Accents
            for i in range(3, 6):
                pos = palette_offset + (i * 2)
                if pos + 2 <= len(data):
                    if i == 4:
                        data[pos:pos+2] = struct.pack('<H', accent_color)
                    else:
                        data[pos:pos+2] = struct.pack('<H', accent_dark)

            # Main armor
            pos = palette_offset + (6 * 2)
            if pos + 2 <= len(data):
                data[pos:pos+2] = struct.pack('<H', main_color)

            # Edge (index 7)
            pos = palette_offset + (7 * 2)
            if pos + 2 <= len(data):
                data[pos:pos+2] = struct.pack('<H', edge_color)

            pos = palette_offset + (8 * 2)
            if pos + 2 <= len(data):
                data[pos:pos+2] = struct.pack('<H', main_color)

            # Shadow (index 9)
            pos = palette_offset + (9 * 2)
            if pos + 2 <= len(data):
                data[pos:pos+2] = struct.pack('<H', shadow_color)

            pos = palette_offset + (10 * 2)
            if pos + 2 <= len(data):
                data[pos:pos+2] = struct.pack('<H', main_color)

            # Restore skin colors for this palette
            for i, color_bytes in enumerate(palette_skin_colors):
                pos = palette_offset + ((11 + i) * 2)
                if pos + 2 <= len(data) and color_bytes:
                    data[pos:pos+2] = color_bytes

        # Write back
        f.seek(0)
        f.write(data)

    return True

def generate_all_themes():
    """Generate 50 creative themes for Marach with preserved face colors."""

    # Input and output paths
    input_sprite = r"C:\Users\ptyRa\OneDrive\Desktop\bin files\battle_mara_spr.bin"
    output_base = r"C:\Users\ptyRa\OneDrive\Desktop\FFT_Palette_Tests\marach_armor_only"

    # Verify input file exists
    if not os.path.exists(input_sprite):
        print(f"Error: Input sprite not found at {input_sprite}")
        return

    # Create output directory
    os.makedirs(output_base, exist_ok=True)

    # Define 50 themes with armor colors only
    themes = [
        # Classic armor colors
        ("steel_plate", "#71797E", "#C0C0C0"),
        ("iron_armor", "#434B4D", "#71797E"),
        ("bronze_mail", "#CD7F32", "#8B4513"),
        ("gold_plate", "#FFD700", "#FFA500"),
        ("silver_guard", "#C0C0C0", "#E5E5E5"),

        # Elemental armor
        ("flame_mail", "#FF4500", "#FFD700"),
        ("frost_armor", "#4682B4", "#B0E0E6"),
        ("storm_plate", "#1E90FF", "#FFD700"),
        ("earth_guard", "#8B4513", "#D2691E"),
        ("wind_armor", "#87CEEB", "#98FB98"),

        # Dark/Shadow armors
        ("shadow_mail", "#2F4F4F", "#696969"),
        ("void_plate", "#191970", "#4B0082"),
        ("nightmare_armor", "#1C1C1C", "#8B0000"),
        ("abyss_guard", "#000033", "#4B0082"),
        ("demon_mail", "#8B0000", "#DC143C"),

        # Holy/Light armors
        ("holy_plate", "#F0E68C", "#FFD700"),
        ("divine_mail", "#FFFAF0", "#F0E68C"),
        ("radiant_armor", "#FAFAD2", "#FFFFE0"),
        ("celestial_guard", "#F5F5DC", "#FFFAF0"),
        ("blessed_plate", "#FFF8DC", "#FFD700"),

        # Gemstone armors
        ("ruby_mail", "#E0115F", "#FFB6C1"),
        ("sapphire_plate", "#0F52BA", "#87CEEB"),
        ("emerald_armor", "#50C878", "#98FF98"),
        ("amethyst_guard", "#9966CC", "#DDA0DD"),
        ("diamond_mail", "#B9F2FF", "#F0F8FF"),

        # Nature-themed armors
        ("forest_plate", "#228B22", "#32CD32"),
        ("ocean_mail", "#006994", "#40E0D0"),
        ("mountain_armor", "#8B4513", "#D2691E"),
        ("desert_guard", "#F4A460", "#D2691E"),
        ("jungle_plate", "#3CB371", "#98FB98"),

        # Mystic/Magic armors
        ("arcane_mail", "#6A0DAD", "#9370DB"),
        ("mystic_plate", "#8B008B", "#DA70D6"),
        ("ethereal_armor", "#9370DB", "#E6E6FA"),
        ("astral_guard", "#4B0082", "#9400D3"),
        ("cosmic_mail", "#000080", "#8A2BE2"),

        # Military/Tactical armors
        ("combat_green", "#4B5320", "#6B8E23"),
        ("navy_plate", "#000080", "#4169E1"),
        ("tactical_black", "#1C1C1C", "#36454F"),
        ("urban_gray", "#708090", "#C0C0C0"),
        ("desert_tan", "#C19A6B", "#F4A460"),

        # Royal/Noble armors
        ("royal_purple", "#7851A9", "#CF71AF"),
        ("imperial_red", "#C41E3A", "#DC143C"),
        ("noble_blue", "#002FA7", "#4169E1"),
        ("regal_gold", "#FFD700", "#FFC125"),
        ("crown_silver", "#C0C0C0", "#FFD700"),

        # Unique/Special armors
        ("phoenix_mail", "#FF8C00", "#FFD700"),
        ("dragon_plate", "#8B0000", "#FFD700"),
        ("titan_armor", "#36454F", "#71797E"),
        ("guardian_mail", "#4682B4", "#C0C0C0"),
        ("warrior_plate", "#8B4513", "#CD7F32"),
    ]

    # Generate each theme
    for i, (theme_name, primary, accent) in enumerate(themes, 1):
        print(f"\n[{i}/50] Generating theme: {theme_name}")

        # Create .bin file
        output_bin = os.path.join(output_base, f"marach_{theme_name}.bin")
        success = create_marach_theme_armor_only(input_sprite, output_bin, primary, accent, theme_name)

        if success:
            # Generate PNG preview
            output_png = os.path.join(output_base, f"marach_{theme_name}.png")
            try:
                # Use the convert_sprite_sw.py script to generate PNG
                cmd = [
                    "python",
                    "scripts/convert_sprite_sw.py",
                    output_bin,
                    output_png,
                    "--preview"
                ]
                result = subprocess.run(cmd, capture_output=True, text=True)
                if result.returncode == 0:
                    print(f"  [OK] Generated PNG preview: {output_png}")
                else:
                    print(f"  [ERROR] Failed to generate PNG: {result.stderr}")
            except Exception as e:
                print(f"  [ERROR] Error generating PNG: {e}")

    print(f"\n[COMPLETE] Generated 50 themes for Marach (armor colors only)")
    print(f"[COMPLETE] Face/skin colors preserved from original")
    print(f"[COMPLETE] Files saved to: {output_base}")

if __name__ == "__main__":
    generate_all_themes()