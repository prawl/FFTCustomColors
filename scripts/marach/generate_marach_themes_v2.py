#!/usr/bin/env python3
"""Generate 50 NEW varied themes for Marach with different color combinations."""

import os
import struct
import shutil
from typing import Tuple
from pathlib import Path
import subprocess
import random

def rgb_to_fft_color(r: int, g: int, b: int) -> int:
    """Convert RGB (0-255) to FFT 16-bit color format (XBBBBBGGGGGRRRRR)."""
    r5 = (r >> 3) & 0x1F
    g5 = (g >> 3) & 0x1F
    b5 = (b >> 3) & 0x1F
    return (b5 << 10) | (g5 << 5) | r5

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

def create_marach_theme(input_path: str, output_path: str, primary_hex: str,
                      accent_hex: str = None, theme_name: str = "custom",
                      style: str = "standard"):
    """Create a themed Marach sprite with various styles."""

    # Create output directory
    os.makedirs(os.path.dirname(output_path), exist_ok=True)

    # Copy original sprite
    shutil.copy2(input_path, output_path)

    # Parse colors
    primary_r, primary_g, primary_b = hex_to_rgb(primary_hex)
    if accent_hex:
        accent_r, accent_g, accent_b = hex_to_rgb(accent_hex)
    else:
        # Generate complementary accent
        accent_r = 255 - primary_r
        accent_g = 255 - primary_g
        accent_b = 255 - primary_b

    print(f"Creating {theme_name} theme ({style})...")

    # Open and modify the sprite
    with open(output_path, 'r+b') as f:
        data = bytearray(f.read())

        if style == "vibrant":
            # Bright, saturated colors
            bright_r = min(255, primary_r + 30)
            bright_g = min(255, primary_g + 30)
            bright_b = min(255, primary_b + 30)
            main_color = rgb_to_fft_color(bright_r, bright_g, bright_b)
            accent_bright = rgb_to_fft_color(min(255, accent_r + 50), min(255, accent_g + 50), min(255, accent_b + 50))

            data[6:8] = struct.pack('<H', accent_bright)
            data[8:10] = struct.pack('<H', accent_bright)
            data[10:12] = struct.pack('<H', main_color)
            data[12:14] = struct.pack('<H', main_color)
            data[14:16] = struct.pack('<H', main_color)
            data[16:18] = struct.pack('<H', main_color)
            data[18:20] = struct.pack('<H', main_color)
            data[20:22] = struct.pack('<H', main_color)

        elif style == "duotone":
            # Two-tone design
            color1 = rgb_to_fft_color(primary_r, primary_g, primary_b)
            color2 = rgb_to_fft_color(accent_r, accent_g, accent_b)

            data[6:8] = struct.pack('<H', color2)
            data[8:10] = struct.pack('<H', color2)
            data[10:12] = struct.pack('<H', color2)
            data[12:14] = struct.pack('<H', color1)
            data[14:16] = struct.pack('<H', color1)
            data[16:18] = struct.pack('<H', color1)
            data[18:20] = struct.pack('<H', color1)
            data[20:22] = struct.pack('<H', color1)

        elif style == "neon":
            # Neon/glowing effect
            neon_main = rgb_to_fft_color(primary_r, primary_g, primary_b)
            glow_r = min(255, primary_r + 100)
            glow_g = min(255, primary_g + 100)
            glow_b = min(255, primary_b + 100)
            neon_glow = rgb_to_fft_color(glow_r, glow_g, glow_b)

            data[6:8] = struct.pack('<H', neon_glow)
            data[8:10] = struct.pack('<H', neon_glow)
            data[10:12] = struct.pack('<H', neon_main)
            data[12:14] = struct.pack('<H', neon_main)
            data[14:16] = struct.pack('<H', neon_glow)
            data[16:18] = struct.pack('<H', neon_main)
            data[18:20] = struct.pack('<H', neon_main)
            data[20:22] = struct.pack('<H', neon_main)

        elif style == "muted":
            # Desaturated, subtle colors
            muted_r, muted_g, muted_b = lighten_color(primary_r, primary_g, primary_b, 0.3)
            gray_r = int((muted_r + 128) / 2)
            gray_g = int((muted_g + 128) / 2)
            gray_b = int((muted_b + 128) / 2)
            main_color = rgb_to_fft_color(gray_r, gray_g, gray_b)
            accent_muted = rgb_to_fft_color(*lighten_color(accent_r, accent_g, accent_b, 0.4))

            data[6:8] = struct.pack('<H', accent_muted)
            data[8:10] = struct.pack('<H', accent_muted)
            data[10:12] = struct.pack('<H', main_color)
            data[12:14] = struct.pack('<H', main_color)
            data[14:16] = struct.pack('<H', main_color)
            data[16:18] = struct.pack('<H', main_color)
            data[18:20] = struct.pack('<H', main_color)
            data[20:22] = struct.pack('<H', main_color)

        elif style == "triadic":
            # Three-color harmony
            color1 = rgb_to_fft_color(primary_r, primary_g, primary_b)
            # Rotate hue by 120 degrees for triadic colors
            color2 = rgb_to_fft_color(primary_b, primary_r, primary_g)  # Simplified rotation
            color3 = rgb_to_fft_color(primary_g, primary_b, primary_r)  # Simplified rotation

            data[6:8] = struct.pack('<H', color2)
            data[8:10] = struct.pack('<H', color3)
            data[10:12] = struct.pack('<H', color2)
            data[12:14] = struct.pack('<H', color1)
            data[14:16] = struct.pack('<H', color1)
            data[16:18] = struct.pack('<H', color1)
            data[18:20] = struct.pack('<H', color3)
            data[20:22] = struct.pack('<H', color1)

        else:  # standard
            # Traditional shading
            main_color = rgb_to_fft_color(primary_r, primary_g, primary_b)
            edge_r, edge_g, edge_b = darken_color(primary_r, primary_g, primary_b, 0.75)
            edge_color = rgb_to_fft_color(edge_r, edge_g, edge_b)
            shadow_r, shadow_g, shadow_b = darken_color(primary_r, primary_g, primary_b, 0.5)
            shadow_color = rgb_to_fft_color(shadow_r, shadow_g, shadow_b)
            accent_color = rgb_to_fft_color(accent_r, accent_g, accent_b)
            accent_dark = rgb_to_fft_color(*darken_color(accent_r, accent_g, accent_b, 0.7))

            data[6:8] = struct.pack('<H', accent_dark)
            data[8:10] = struct.pack('<H', accent_color)
            data[10:12] = struct.pack('<H', accent_dark)
            data[12:14] = struct.pack('<H', main_color)
            data[14:16] = struct.pack('<H', edge_color)
            data[16:18] = struct.pack('<H', main_color)
            data[18:20] = struct.pack('<H', shadow_color)
            data[20:22] = struct.pack('<H', main_color)

        # Apply to additional palettes
        for palette in range(1, 4):
            palette_offset = palette * 32
            if style in ["standard"]:
                edge_pos = palette_offset + (7 * 2)
                shadow_pos = palette_offset + (9 * 2)
                edge_r, edge_g, edge_b = darken_color(primary_r, primary_g, primary_b, 0.75)
                shadow_r, shadow_g, shadow_b = darken_color(primary_r, primary_g, primary_b, 0.5)
                data[edge_pos:edge_pos+2] = struct.pack('<H', rgb_to_fft_color(edge_r, edge_g, edge_b))
                data[shadow_pos:shadow_pos+2] = struct.pack('<H', rgb_to_fft_color(shadow_r, shadow_g, shadow_b))

        # Write back
        f.seek(0)
        f.write(data)

    return True

def generate_all_themes():
    """Generate 50 NEW creative themes for Marach."""

    # Input and output paths
    input_sprite = r"C:\Users\ptyRa\OneDrive\Desktop\bin files\battle_mara_spr.bin"
    output_base = r"C:\Users\ptyRa\OneDrive\Desktop\FFT_Palette_Tests\marach_themes_v2"

    # Verify input file exists
    if not os.path.exists(input_sprite):
        print(f"Error: Input sprite not found at {input_sprite}")
        return

    # Create output directory
    os.makedirs(output_base, exist_ok=True)

    # Define 50 COMPLETELY NEW themes with varied styles
    themes = [
        # Vibrant/Bright themes
        ("neon_pink", "#FF1493", "#00FF00", "neon"),
        ("electric_cyan", "#00FFFF", "#FF00FF", "neon"),
        ("laser_green", "#00FF00", "#FF0000", "neon"),
        ("hot_magenta", "#FF00FF", "#FFFF00", "vibrant"),
        ("bright_coral", "#FF7F50", "#40E0D0", "vibrant"),

        # Ocean/Water themes
        ("deep_ocean", "#003366", "#00CCCC", "duotone"),
        ("tidal_wave", "#006994", "#40E0D0", "standard"),
        ("coral_reef", "#FF7F50", "#20B2AA", "vibrant"),
        ("sea_foam", "#71EEB8", "#4682B4", "muted"),
        ("abyssal_blue", "#000080", "#1E90FF", "standard"),

        # Sunset/Dawn themes
        ("sunset_orange", "#FF8C00", "#FFD700", "standard"),
        ("dawn_purple", "#9370DB", "#FFC0CB", "muted"),
        ("dusk_red", "#CD5C5C", "#4B0082", "duotone"),
        ("twilight_pink", "#C71585", "#191970", "standard"),
        ("morning_gold", "#FFD700", "#87CEEB", "vibrant"),

        # Gem/Crystal themes
        ("ruby_red", "#E0115F", "#FFB6C1", "standard"),
        ("sapphire_blue", "#0F52BA", "#87CEEB", "standard"),
        ("emerald_green", "#50C878", "#98FF98", "vibrant"),
        ("amethyst_purple", "#9966CC", "#DDA0DD", "muted"),
        ("topaz_yellow", "#FFC87C", "#FFE5B4", "vibrant"),

        # Cyberpunk themes
        ("cyber_purple", "#9D00FF", "#00FFF0", "neon"),
        ("matrix_green", "#00FF41", "#003B00", "neon"),
        ("synthwave_pink", "#FF6EC7", "#1F1F1F", "duotone"),
        ("retrowave_blue", "#00D4FF", "#FF00FF", "neon"),
        ("digital_orange", "#FF6600", "#0099CC", "vibrant"),

        # Pastel/Soft themes
        ("pastel_mint", "#AAFFC3", "#FFB3E6", "muted"),
        ("soft_peach", "#FFDAB9", "#E6E6FA", "muted"),
        ("baby_blue", "#89CFF0", "#FFE4E1", "muted"),
        ("lavender_dream", "#E6E6FA", "#FFB3E6", "muted"),
        ("cotton_candy", "#FFBCD9", "#AFDAFC", "muted"),

        # Monochrome variations
        ("charcoal_gray", "#36454F", "#71797E", "duotone"),
        ("silver_chrome", "#AAA9AD", "#E5E4E2", "standard"),
        ("graphite_dark", "#41424C", "#6C7A89", "muted"),
        ("pearl_white", "#FBFCF8", "#E8E8E8", "muted"),
        ("obsidian_black", "#0C0C0C", "#3B3B3B", "standard"),

        # Food-inspired themes
        ("mint_chocolate", "#3EB489", "#7B3F00", "duotone"),
        ("strawberry_cream", "#FC5A8D", "#FFFDD0", "vibrant"),
        ("blueberry_muffin", "#4F86F7", "#F5DEB3", "muted"),
        ("lime_sorbet", "#32CD32", "#F0E68C", "vibrant"),
        ("caramel_swirl", "#AF6F09", "#FFE5B4", "standard"),

        # Sports team colors
        ("lakers_purple", "#552583", "#FDB927", "duotone"),
        ("celtics_green", "#007A33", "#BA9653", "duotone"),
        ("heat_red", "#98002E", "#F9A01B", "duotone"),
        ("warriors_blue", "#1D428A", "#FFC72C", "duotone"),
        ("bulls_red", "#CE1141", "#000000", "duotone"),

        # Unique combinations
        ("vaporwave", "#FF71CE", "#01CDFE", "neon"),
        ("northern_lights", "#00FF7F", "#9370DB", "triadic"),
        ("cosmic_nebula", "#FF1493", "#00CED1", "triadic"),
        ("prismatic", "#FF0080", "#00FF80", "triadic"),
        ("holographic", "#C0C0C0", "#AB82FF", "triadic"),
    ]

    # Generate each theme
    for i, (theme_name, primary, accent, style) in enumerate(themes, 1):
        print(f"\n[{i}/50] Generating theme: {theme_name}")

        # Create .bin file
        output_bin = os.path.join(output_base, f"marach_{theme_name}.bin")
        success = create_marach_theme(input_sprite, output_bin, primary, accent, theme_name, style)

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

    print(f"\n[COMPLETE] Generated 50 NEW themes for Marach")
    print(f"[COMPLETE] Files saved to: {output_base}")

if __name__ == "__main__":
    generate_all_themes()