#!/usr/bin/env python3
"""Generate 50 varied themes for Marach with diverse color combinations."""

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
                      accent_hex: str = None, theme_name: str = "custom"):
    """Create a themed Marach sprite with proper color handling."""

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
    """Generate 50 creative themes for Marach."""

    # Input and output paths
    input_sprite = r"C:\Users\ptyRa\OneDrive\Desktop\bin files\battle_mara_spr.bin"
    output_base = r"C:\Users\ptyRa\OneDrive\Desktop\FFT_Palette_Tests\marach_themes"

    # Verify input file exists
    if not os.path.exists(input_sprite):
        print(f"Error: Input sprite not found at {input_sprite}")
        return

    # Create output directory
    os.makedirs(output_base, exist_ok=True)

    # Define 50 creative themes for Marach (Heaven Knight - male counterpart to Rapha)
    themes = [
        # Dark/Shadow themes (fitting for male Heaven Knight)
        ("shadow_priest", "#2F4F4F", "#8B008B"),
        ("dark_oracle", "#191970", "#4B0082"),
        ("void_walker", "#0C0C0C", "#4B0082"),
        ("nightmare_black", "#1C1C1C", "#8B0000"),
        ("abyss_guardian", "#000033", "#330066"),

        # Electric/Energy themes
        ("thunder_lord", "#1E90FF", "#FFD700"),
        ("storm_bringer", "#4682B4", "#F0E68C"),
        ("lightning_master", "#00CED1", "#FFFF00"),
        ("energy_surge", "#00FFFF", "#FF00FF"),
        ("plasma_core", "#8A2BE2", "#00FF00"),

        # Fire/Heat themes
        ("inferno_knight", "#FF4500", "#FF8C00"),
        ("phoenix_warrior", "#DC143C", "#FFD700"),
        ("lava_walker", "#B22222", "#FF6347"),
        ("solar_knight", "#FFA500", "#FFFF00"),
        ("ember_lord", "#CD5C5C", "#8B0000"),

        # Ice/Cold themes
        ("frost_sage", "#4682B4", "#B0E0E6"),
        ("glacier_knight", "#5F9EA0", "#E0FFFF"),
        ("arctic_warrior", "#F0F8FF", "#87CEEB"),
        ("blizzard_master", "#B0C4DE", "#FFFFFF"),
        ("frozen_heart", "#6495ED", "#ADD8E6"),

        # Earth/Nature themes
        ("stone_sentinel", "#696969", "#A9A9A9"),
        ("mountain_sage", "#8B4513", "#D2691E"),
        ("forest_guardian", "#228B22", "#32CD32"),
        ("nature_prophet", "#3CB371", "#98FB98"),
        ("earthen_warrior", "#8B7355", "#DEB887"),

        # Metallic/Armor themes
        ("iron_knight", "#434B4D", "#71797E"),
        ("steel_warrior", "#71797E", "#C0C0C0"),
        ("bronze_guardian", "#CD7F32", "#B87333"),
        ("silver_sage", "#C0C0C0", "#E5E5E5"),
        ("golden_prophet", "#FFD700", "#FFA500"),

        # Mystical/Magic themes
        ("arcane_master", "#6A0DAD", "#9370DB"),
        ("mystic_sage", "#8B008B", "#DA70D6"),
        ("ethereal_knight", "#9370DB", "#E6E6FA"),
        ("astral_warrior", "#4B0082", "#9400D3"),
        ("cosmic_guardian", "#000080", "#8A2BE2"),

        # Holy/Light themes
        ("divine_knight", "#F0E68C", "#FFD700"),
        ("celestial_warrior", "#F5F5DC", "#FFFAF0"),
        ("radiant_sage", "#FAFAD2", "#FFFFE0"),
        ("holy_guardian", "#FFF8DC", "#F0E68C"),
        ("light_bringer", "#FFFFF0", "#F5F5DC"),

        # Demonic/Dark themes
        ("demon_knight", "#8B0000", "#DC143C"),
        ("hell_guardian", "#800000", "#A52A2A"),
        ("chaos_warrior", "#4B0082", "#8B008B"),
        ("doom_bringer", "#2F4F4F", "#696969"),
        ("shadow_demon", "#000000", "#4B0082"),

        # Unique/Special themes
        ("blood_knight", "#8B0000", "#B22222"),
        ("poison_master", "#32CD32", "#9ACD32"),
        ("royal_guard", "#4B0082", "#FFD700"),
        ("ancient_warrior", "#8B7D6B", "#CDAA7D"),
        ("spirit_walker", "#708090", "#B0C4DE"),
    ]

    # Generate each theme
    for i, (theme_name, primary, accent) in enumerate(themes, 1):
        print(f"\n[{i}/50] Generating theme: {theme_name}")

        # Create .bin file
        output_bin = os.path.join(output_base, f"marach_{theme_name}.bin")
        success = create_marach_theme(input_sprite, output_bin, primary, accent, theme_name)

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

    print(f"\n[COMPLETE] Generated 50 themes for Marach")
    print(f"[COMPLETE] Files saved to: {output_base}")

if __name__ == "__main__":
    generate_all_themes()