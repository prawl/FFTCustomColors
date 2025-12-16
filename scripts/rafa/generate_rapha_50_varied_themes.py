#!/usr/bin/env python3
"""Generate 50 varied themes for Rapha with diverse color combinations."""

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

def create_rapha_theme(input_path: str, output_path: str, primary_hex: str,
                      accent_hex: str = None, theme_name: str = "custom",
                      variation_style: str = "standard"):
    """Create a themed Rapha sprite with various color application styles."""

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

    print(f"Creating {theme_name} theme ({variation_style} style)...")

    # Open and modify the sprite
    with open(output_path, 'r+b') as f:
        data = bytearray(f.read())

        if variation_style == "standard":
            # Standard theme approach
            main_color = rgb_to_fft_color(primary_r, primary_g, primary_b)
            edge_r, edge_g, edge_b = darken_color(primary_r, primary_g, primary_b, 0.75)
            edge_color = rgb_to_fft_color(edge_r, edge_g, edge_b)
            shadow_r, shadow_g, shadow_b = darken_color(primary_r, primary_g, primary_b, 0.5)
            shadow_color = rgb_to_fft_color(shadow_r, shadow_g, shadow_b)
            accent_color = rgb_to_fft_color(accent_r, accent_g, accent_b)
            accent_dark = rgb_to_fft_color(*darken_color(accent_r, accent_g, accent_b, 0.7))

            # Apply standard coloring
            data[6:8] = struct.pack('<H', accent_dark)    # Index 3
            data[8:10] = struct.pack('<H', accent_color)  # Index 4
            data[10:12] = struct.pack('<H', accent_dark)  # Index 5
            data[12:14] = struct.pack('<H', main_color)   # Index 6
            data[14:16] = struct.pack('<H', edge_color)   # Index 7
            data[16:18] = struct.pack('<H', main_color)   # Index 8
            data[18:20] = struct.pack('<H', shadow_color) # Index 9
            data[20:22] = struct.pack('<H', main_color)   # Index 10

        elif variation_style == "gradient":
            # Gradient from primary to accent
            for i in range(6, 11):
                t = (i - 6) / 4.0
                grad_r = int(primary_r * (1-t) + accent_r * t)
                grad_g = int(primary_g * (1-t) + accent_g * t)
                grad_b = int(primary_b * (1-t) + accent_b * t)
                pos = i * 2
                data[pos:pos+2] = struct.pack('<H', rgb_to_fft_color(grad_r, grad_g, grad_b))

        elif variation_style == "inverted":
            # Inverted - accent as main, primary as trim
            main_color = rgb_to_fft_color(accent_r, accent_g, accent_b)
            trim_color = rgb_to_fft_color(primary_r, primary_g, primary_b)
            data[6:8] = struct.pack('<H', trim_color)     # Index 3
            data[8:10] = struct.pack('<H', trim_color)    # Index 4
            data[10:12] = struct.pack('<H', trim_color)   # Index 5
            data[12:14] = struct.pack('<H', main_color)   # Index 6
            data[14:16] = struct.pack('<H', main_color)   # Index 7
            data[16:18] = struct.pack('<H', main_color)   # Index 8
            data[18:20] = struct.pack('<H', main_color)   # Index 9
            data[20:22] = struct.pack('<H', main_color)   # Index 10

        elif variation_style == "pastel":
            # Pastel version - lightened colors
            light_r, light_g, light_b = lighten_color(primary_r, primary_g, primary_b, 0.5)
            accent_light_r, accent_light_g, accent_light_b = lighten_color(accent_r, accent_g, accent_b, 0.5)
            main_color = rgb_to_fft_color(light_r, light_g, light_b)
            accent_color = rgb_to_fft_color(accent_light_r, accent_light_g, accent_light_b)

            data[6:8] = struct.pack('<H', accent_color)   # Index 3
            data[8:10] = struct.pack('<H', accent_color)  # Index 4
            data[10:12] = struct.pack('<H', accent_color) # Index 5
            data[12:14] = struct.pack('<H', main_color)   # Index 6-10
            data[14:16] = struct.pack('<H', main_color)
            data[16:18] = struct.pack('<H', main_color)
            data[18:20] = struct.pack('<H', main_color)
            data[20:22] = struct.pack('<H', main_color)

        elif variation_style == "metallic":
            # Metallic sheen effect
            sheen_r = min(255, primary_r + 50)
            sheen_g = min(255, primary_g + 50)
            sheen_b = min(255, primary_b + 50)
            main_color = rgb_to_fft_color(primary_r, primary_g, primary_b)
            sheen_color = rgb_to_fft_color(sheen_r, sheen_g, sheen_b)
            dark_color = rgb_to_fft_color(*darken_color(primary_r, primary_g, primary_b, 0.6))

            data[6:8] = struct.pack('<H', sheen_color)    # Index 3
            data[8:10] = struct.pack('<H', sheen_color)   # Index 4
            data[10:12] = struct.pack('<H', main_color)   # Index 5
            data[12:14] = struct.pack('<H', main_color)   # Index 6
            data[14:16] = struct.pack('<H', main_color)   # Index 7
            data[16:18] = struct.pack('<H', main_color)   # Index 8
            data[18:20] = struct.pack('<H', dark_color)   # Index 9
            data[20:22] = struct.pack('<H', dark_color)   # Index 10

        # Apply to additional palettes for consistency
        for palette in range(1, 4):
            palette_offset = palette * 32
            # Copy some color consistency across palettes
            if variation_style in ["standard", "gradient"]:
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
    """Generate 50 varied themes for Rapha."""

    # Input and output paths
    input_sprite = r"C:\Users\ptyRa\OneDrive\Desktop\bin files\battle_h79_spr.bin"
    output_base = r"C:\Users\ptyRa\OneDrive\Desktop\FFT_Palette_Tests\batch2"

    # Verify input file exists
    if not os.path.exists(input_sprite):
        print(f"Error: Input sprite not found at {input_sprite}")
        return

    # Create output directory
    os.makedirs(output_base, exist_ok=True)

    # Define 50 varied themes with different styles
    themes = [
        # Metallic themes
        ("steel_guardian", "#708090", "#C0C0C0", "metallic"),
        ("bronze_warrior", "#CD7F32", "#8B4513", "metallic"),
        ("copper_knight", "#B87333", "#722F37", "metallic"),
        ("iron_maiden", "#434B4D", "#A8A8A8", "metallic"),
        ("platinum_queen", "#E5E4E2", "#BCC6CC", "metallic"),

        # Pastel themes
        ("soft_lavender", "#E6E6FA", "#DDA0DD", "pastel"),
        ("mint_breeze", "#98FF98", "#AFEEEE", "pastel"),
        ("peach_blossom", "#FFDAB9", "#FFE4E1", "pastel"),
        ("sky_whisper", "#87CEEB", "#E0FFFF", "pastel"),
        ("rose_mist", "#FFE4E1", "#FFC0CB", "pastel"),

        # Gradient themes
        ("sunset_gradient", "#FF6347", "#FFD700", "gradient"),
        ("ocean_wave", "#00CED1", "#000080", "gradient"),
        ("forest_fade", "#228B22", "#90EE90", "gradient"),
        ("twilight_blend", "#4B0082", "#FF1493", "gradient"),
        ("aurora_shift", "#00FF00", "#FF00FF", "gradient"),

        # Inverted themes
        ("shadow_reverse", "#2F4F4F", "#F0F8FF", "inverted"),
        ("light_inverse", "#FFFFFF", "#000000", "inverted"),
        ("nature_flip", "#8FBC8F", "#8B4513", "inverted"),
        ("flame_reverse", "#FF4500", "#0000FF", "inverted"),
        ("ice_invert", "#00FFFF", "#FF6347", "inverted"),

        # Dark/Gothic themes
        ("midnight_gothic", "#191970", "#4B0082", "standard"),
        ("blood_moon", "#8B0000", "#DC143C", "standard"),
        ("shadow_walker", "#2F4F4F", "#696969", "standard"),
        ("dark_matter", "#0C0C0C", "#1C1C1C", "standard"),
        ("void_empress", "#000033", "#330066", "standard"),

        # Bright/Neon themes
        ("neon_green", "#39FF14", "#FFFF00", "standard"),
        ("electric_pink", "#FF1493", "#FF69B4", "standard"),
        ("cyber_blue", "#00FFFF", "#0080FF", "standard"),
        ("radioactive", "#7FFF00", "#ADFF2F", "standard"),
        ("laser_red", "#FF0000", "#FF4444", "standard"),

        # Nature themes
        ("autumn_leaf", "#FF8C00", "#8B4513", "standard"),
        ("spring_bloom", "#FF69B4", "#98FB98", "standard"),
        ("winter_frost", "#F0FFFF", "#B0E0E6", "standard"),
        ("summer_sun", "#FFD700", "#FFA500", "standard"),
        ("desert_mirage", "#F4A460", "#D2691E", "standard"),

        # Jewel tones
        ("garnet_glow", "#9A2A2A", "#E52B50", "metallic"),
        ("citrine_sparkle", "#E4D00A", "#FFD700", "metallic"),
        ("aquamarine_shine", "#7FFFD4", "#40E0D0", "metallic"),
        ("onyx_darkness", "#353839", "#0F0F0F", "metallic"),
        ("moonstone_gleam", "#ADDFFF", "#F5F5F5", "metallic"),

        # Cultural/Historical themes
        ("royal_purple", "#7851A9", "#CF71AF", "standard"),
        ("samurai_red", "#C41E3A", "#960018", "standard"),
        ("celtic_green", "#00A550", "#004225", "standard"),
        ("egyptian_gold", "#D4AF37", "#FFC125", "standard"),
        ("norse_blue", "#003153", "#4682B4", "standard"),

        # Abstract/Artistic themes
        ("prism_split", "#FF0000", "#0000FF", "gradient"),
        ("dream_weaver", "#E0B0FF", "#957DAD", "pastel"),
        ("chaos_theory", "#FF00FF", "#00FF00", "inverted"),
        ("harmony_balance", "#FFB6C1", "#ADD8E6", "pastel"),
        ("entropy_flow", "#4B0082", "#8A2BE2", "gradient"),
    ]

    # Generate each theme
    for i, (theme_name, primary, accent, style) in enumerate(themes, 1):
        print(f"\n[{i}/50] Generating theme: {theme_name}")

        # Create .bin file
        output_bin = os.path.join(output_base, f"rapha_{theme_name}.bin")
        success = create_rapha_theme(input_sprite, output_bin, primary, accent, theme_name, style)

        if success:
            # Generate PNG preview
            output_png = os.path.join(output_base, f"rapha_{theme_name}.png")
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

    print(f"\n[COMPLETE] Generated 50 varied themes for Rapha")
    print(f"[COMPLETE] Files saved to: {output_base}")

    # List all theme names for easy copying to enum
    print("\n[THEME NAMES FOR ENUM]:")
    for theme_name, _, _, _ in themes:
        print(f'        [Description("{theme_name.replace("_", " ").title()}")]')
        print(f"        {theme_name},")
        print()

if __name__ == "__main__":
    generate_all_themes()