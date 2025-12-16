#!/usr/bin/env python3
"""Generate 50 new themes for Rafa/Rapha with creative names."""

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

def darken_color(r: int, g: int, b: int, factor: float) -> Tuple[int, int, int]:
    """Darken a color by a factor (0.0 = black, 1.0 = original)."""
    return (int(r * factor), int(g * factor), int(b * factor))

def hex_to_rgb(hex_color: str) -> Tuple[int, int, int]:
    """Convert hex color string to RGB tuple."""
    hex_color = hex_color.lstrip('#')
    return tuple(int(hex_color[i:i+2], 16) for i in (0, 2, 4))

def create_rafa_theme(input_path: str, output_path: str, primary_hex: str,
                      accent_hex: str = None, theme_name: str = "custom"):
    """Create a themed Rafa sprite with proper color handling."""

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
        accent_r, accent_g, accent_b = (200, 180, 100)  # Golden accent

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

    print(f"Creating {theme_name} theme...")

    # Open and modify the sprite
    with open(output_path, 'r+b') as f:
        data = bytearray(f.read())

        # PALETTE 0 - Main sprite colors
        # Accent pieces (buckles, clasps, trim)
        data[6:8] = struct.pack('<H', accent_dark)    # Index 3
        data[8:10] = struct.pack('<H', accent_color)  # Index 4
        data[10:12] = struct.pack('<H', accent_dark)  # Index 5

        # Main clothing/armor
        data[12:14] = struct.pack('<H', main_color)    # Index 6
        data[14:16] = struct.pack('<H', edge_color)    # Index 7 (edge)
        data[16:18] = struct.pack('<H', main_color)    # Index 8
        data[18:20] = struct.pack('<H', shadow_color)  # Index 9 (shadow)
        data[20:22] = struct.pack('<H', main_color)    # Index 10

        # Apply edge and shadow to multiple palettes for consistency
        for palette in range(1, 4):  # Palettes 1-3
            palette_offset = palette * 32  # Each palette is 16 colors * 2 bytes

            # Edge (index 7 in each palette)
            pos = palette_offset + (7 * 2)
            data[pos:pos+2] = struct.pack('<H', edge_color)

            # Shadow (index 9 in each palette)
            pos = palette_offset + (9 * 2)
            data[pos:pos+2] = struct.pack('<H', shadow_color)

            # Additional armor/clothing details (indices 12-15)
            for idx in range(12, 16):
                pos = palette_offset + (idx * 2)
                darkness = 0.8 - ((idx - 12) * 0.1)
                r, g, b = darken_color(primary_r, primary_g, primary_b, darkness)
                data[pos:pos+2] = struct.pack('<H', rgb_to_fft_color(r, g, b))

        # Write back
        f.seek(0)
        f.write(data)

    return True

def generate_all_themes():
    """Generate 50 creative themes for Rafa."""

    # Input and output paths
    input_sprite = r"C:\Users\ptyRa\OneDrive\Desktop\bin files\battle_h79_spr.bin"
    output_base = r"C:\Users\ptyRa\OneDrive\Desktop\FFT_Palette_Tests"

    # Verify input file exists
    if not os.path.exists(input_sprite):
        print(f"Error: Input sprite not found at {input_sprite}")
        return

    # Create output directory
    os.makedirs(output_base, exist_ok=True)

    # Define 50 creative themes with cool names
    themes = [
        # Mystical/Magic themed (fitting for Heaven Knight)
        ("mystic_aurora", "#9D4EDD", "#FFD700"),      # Purple with gold accents
        ("celestial_blue", "#0077BE", "#E6E6FA"),     # Sky blue with lavender
        ("astral_pink", "#FF69B4", "#4B0082"),        # Hot pink with indigo
        ("lunar_silver", "#C0C0C0", "#191970"),       # Silver with midnight blue
        ("solar_flare", "#FFA500", "#FF4500"),        # Orange with red-orange
        ("ethereal_mist", "#E0E0E0", "#9370DB"),      # Light gray with medium purple
        ("starlight_white", "#F8F8FF", "#4169E1"),    # Ghost white with royal blue
        ("nebula_purple", "#8A2BE2", "#FF1493"),      # Blue violet with deep pink
        ("cosmic_teal", "#008B8B", "#FFE4B5"),        # Dark cyan with moccasin
        ("void_black", "#1C1C1C", "#9400D3"),         # Near black with violet

        # Elemental themes
        ("flame_dancer", "#FF6347", "#FFD700"),       # Tomato with gold
        ("frost_maiden", "#B0E0E6", "#4682B4"),       # Powder blue with steel blue
        ("earth_shaman", "#8B4513", "#DEB887"),       # Saddle brown with burlywood
        ("storm_caller", "#708090", "#1E90FF"),       # Slate gray with dodger blue
        ("thunder_strike", "#FFE055", "#4B0082"),     # Yellow with indigo
        ("crystal_ice", "#E0FFFF", "#00CED1"),        # Light cyan with dark turquoise
        ("magma_core", "#DC143C", "#FF8C00"),         # Crimson with dark orange
        ("wind_walker", "#98FB98", "#228B22"),        # Pale green with forest green
        ("ocean_depths", "#000080", "#00FFFF"),       # Navy with cyan
        ("lightning_bolt", "#FFFF00", "#FF00FF"),     # Yellow with magenta

        # Gemstone/Precious themes
        ("ruby_empress", "#E0115F", "#FFB6C1"),       # Ruby with light pink
        ("sapphire_knight", "#0F52BA", "#87CEEB"),    # Sapphire with sky blue
        ("emerald_sage", "#50C878", "#98FF98"),       # Emerald with mint green
        ("amethyst_oracle", "#9966CC", "#DDA0DD"),    # Amethyst with plum
        ("topaz_guardian", "#FFC87C", "#FFE5B4"),     # Topaz with peach
        ("opal_dream", "#A8C3BC", "#F5F5DC"),         # Opal with beige
        ("obsidian_shadow", "#3D3D3D", "#696969"),    # Dark gray with dim gray
        ("pearl_maiden", "#FDF5E6", "#FFE4E1"),       # Old lace with misty rose
        ("jade_mystic", "#00A86B", "#7FFFD4"),        # Jade with aquamarine
        ("diamond_prism", "#B9F2FF", "#F0F8FF"),      # Diamond blue with alice blue

        # Fantasy/RPG themes
        ("dragon_scale", "#228B22", "#FFD700"),       # Forest green with gold
        ("phoenix_feather", "#FF4500", "#FFA500"),    # Orange red with orange
        ("unicorn_grace", "#DDA0DD", "#F0E68C"),      # Plum with khaki
        ("valkyrie_steel", "#C0C0C0", "#FF1493"),     # Silver with deep pink
        ("demon_hunter", "#8B0000", "#FF6347"),       # Dark red with tomato
        ("angel_wing", "#FFFAF0", "#FFD700"),         # Floral white with gold
        ("shadow_assassin", "#2F4F4F", "#778899"),    # Dark slate gray with light slate
        ("holy_paladin", "#F0E68C", "#FFD700"),       # Khaki with gold
        ("dark_sorcerer", "#4B0082", "#8A2BE2"),      # Indigo with blue violet
        ("nature_druid", "#228B22", "#8FBC8F"),       # Forest green with dark sea green

        # Unique/Creative themes
        ("neon_punk", "#FF1493", "#00FF00"),          # Deep pink with lime
        ("cyber_violet", "#9400D3", "#00FFFF"),       # Violet with cyan
        ("pastel_dream", "#FFB3E6", "#E6E6FA"),       # Light pink with lavender
        ("midnight_rose", "#191970", "#C71585"),      # Midnight blue with medium violet red
        ("golden_lotus", "#FFD700", "#FF69B4"),       # Gold with hot pink
        ("silver_moon", "#C0C0C0", "#E6E6FA"),        # Silver with lavender
        ("crimson_lotus", "#DC143C", "#FFC0CB"),      # Crimson with pink
        ("azure_sky", "#007FFF", "#87CEEB"),          # Azure with sky blue
        ("violet_storm", "#8B008B", "#DA70D6"),       # Dark magenta with orchid
        ("rainbow_prism", "#FF0000", "#FF69B4"),      # Red with hot pink (simplified rainbow)
    ]

    # Generate each theme
    for i, (theme_name, primary, accent) in enumerate(themes, 1):
        print(f"\n[{i}/50] Generating theme: {theme_name}")

        # Create .bin file
        output_bin = os.path.join(output_base, f"rafa_{theme_name}.bin")
        success = create_rafa_theme(input_sprite, output_bin, primary, accent, theme_name)

        if success:
            # Generate PNG preview
            output_png = os.path.join(output_base, f"rafa_{theme_name}.png")
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

    print(f"\n[COMPLETE] Generated 50 themes for Rafa")
    print(f"[COMPLETE] Files saved to: {output_base}")

if __name__ == "__main__":
    generate_all_themes()