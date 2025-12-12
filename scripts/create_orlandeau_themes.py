#!/usr/bin/env python3
"""
Create 10 epic themes specifically for Orlandeau (Thunder God Cid).
Based on the Black Lion theme style - preserves hair/hood while changing armor.
"""

import os
import shutil
import struct
from pathlib import Path

# Epic Orlandeau themes
ORLANDEAU_THEMES = [
    {
        "name": "thunder_god",
        "description": "Electric blue armor with golden accents",
        "buckles": ["#FFD700", "#FFA500", "#FF8C00"],  # Gold gradient
        "cape": ["#4169E1", "#1E90FF"],  # Royal/dodger blue
        "armor": ["#00BFFF", "#87CEEB"],  # Sky blue/electric
    },
    {
        "name": "crimson_knight",
        "description": "Deep crimson armor with silver accents",
        "buckles": ["#C0C0C0", "#A9A9A9", "#808080"],  # Silver gradient
        "cape": ["#8B0000", "#DC143C"],  # Dark red/crimson
        "armor": ["#FF0000", "#FF6B6B"],  # Bright red
    },
    {
        "name": "shadow_lord",
        "description": "Pure black with purple energy accents",
        "buckles": ["#9400D3", "#8A2BE2", "#9932CC"],  # Purple gradient
        "cape": ["#1C1C1C", "#2F2F2F"],  # Near black
        "armor": ["#000000", "#191919"],  # Pure black
    },
    {
        "name": "holy_paladin",
        "description": "White and gold divine armor",
        "buckles": ["#FFD700", "#FFED4E", "#FFF59D"],  # Bright gold
        "cape": ["#F5F5DC", "#FFFAF0"],  # Beige/cream
        "armor": ["#FFFFFF", "#F8F8FF"],  # Pure white
    },
    {
        "name": "azure_dragon",
        "description": "Deep azure with jade green accents",
        "buckles": ["#50C878", "#00FF7F", "#98FB98"],  # Jade/emerald
        "cape": ["#000080", "#191970"],  # Navy/midnight blue
        "armor": ["#0047AB", "#4682B4"],  # Azure/steel blue
    },
    {
        "name": "inferno_warlord",
        "description": "Molten orange and red with obsidian",
        "buckles": ["#2F4F4F", "#36454F", "#414A4C"],  # Dark slate
        "cape": ["#FF4500", "#FF6347"],  # Orange red
        "armor": ["#FF8C00", "#FFA500"],  # Dark orange/orange
    },
    {
        "name": "frost_emperor",
        "description": "Ice blue with silver frost accents",
        "buckles": ["#E0E0E0", "#D3D3D3", "#DCDCDC"],  # Light silver
        "cape": ["#B0C4DE", "#B0E0E6"],  # Light steel/powder blue
        "armor": ["#00CED1", "#48D1CC"],  # Dark turquoise
    },
    {
        "name": "void_breaker",
        "description": "Deep purple with void black accents",
        "buckles": ["#000000", "#0A0A0A", "#141414"],  # Black gradient
        "cape": ["#4B0082", "#6A0DAD"],  # Indigo/purple
        "armor": ["#8B008B", "#9370DB"],  # Dark magenta/medium purple
    },
    {
        "name": "golden_lion",
        "description": "Royal gold with bronze accents",
        "buckles": ["#CD7F32", "#B87333", "#A0522D"],  # Bronze gradient
        "cape": ["#DAA520", "#B8860B"],  # Goldenrod
        "armor": ["#FFD700", "#FFC125"],  # Gold
    },
    {
        "name": "storm_bringer",
        "description": "Storm gray with lightning yellow accents",
        "buckles": ["#FFFF00", "#FFEB3B", "#FDD835"],  # Lightning yellow
        "cape": ["#708090", "#778899"],  # Slate gray
        "armor": ["#4A4A4A", "#5A5A5A"],  # Dark gray
    }
]

def hex_to_rgb(hex_color):
    """Convert hex color to RGB tuple."""
    hex_color = hex_color.lstrip('#')
    return tuple(int(hex_color[i:i+2], 16) for i in (0, 2, 4))

def rgb_to_16bit(r, g, b):
    """Convert RGB (0-255) to 16-bit color format used by FFT."""
    r = min(31, r >> 3)  # Convert to 5-bit
    g = min(31, g >> 3)
    b = min(31, b >> 3)
    return (b << 10) | (g << 5) | r

def create_orlandeau_theme(source_sprite, theme_config, output_path):
    """Create a themed Orlandeau sprite based on configuration."""
    # Read the original sprite
    with open(source_sprite, 'rb') as f:
        sprite_data = bytearray(f.read())

    # Process each palette (0-7 are unit palettes)
    for palette_idx in range(8):
        palette_offset = palette_idx * 32  # Each palette is 16 colors * 2 bytes

        # Apply buckle/clasp colors (indices 3-5)
        if theme_config.get("buckles"):
            for i, hex_color in enumerate(theme_config["buckles"][:3]):
                r, g, b = hex_to_rgb(hex_color)
                color_16bit = rgb_to_16bit(r, g, b)
                idx = palette_offset + (3 + i) * 2
                sprite_data[idx:idx+2] = struct.pack('<H', color_16bit)

        # Apply cape colors (indices 7-8)
        if theme_config.get("cape"):
            for i, hex_color in enumerate(theme_config["cape"][:2]):
                r, g, b = hex_to_rgb(hex_color)
                color_16bit = rgb_to_16bit(r, g, b)
                idx = palette_offset + (7 + i) * 2
                sprite_data[idx:idx+2] = struct.pack('<H', color_16bit)

        # Apply armor colors (indices 9-10, and 6 for additional armor)
        if theme_config.get("armor"):
            # Index 6 - additional armor piece
            r, g, b = hex_to_rgb(theme_config["armor"][0])
            # Make it slightly darker than main armor
            r, g, b = int(r * 0.7), int(g * 0.7), int(b * 0.7)
            color_16bit = rgb_to_16bit(r, g, b)
            idx = palette_offset + 6 * 2
            sprite_data[idx:idx+2] = struct.pack('<H', color_16bit)

            # Indices 9-10 - main armor
            for i, hex_color in enumerate(theme_config["armor"][:2]):
                r, g, b = hex_to_rgb(hex_color)
                color_16bit = rgb_to_16bit(r, g, b)
                idx = palette_offset + (9 + i) * 2
                sprite_data[idx:idx+2] = struct.pack('<H', color_16bit)

    # Write the themed sprite
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    with open(output_path, 'wb') as f:
        f.write(sprite_data)

    print(f"Created: {output_path}")
    print(f"  Theme: {theme_config.get('description', 'Custom theme')}")

def main():
    # Source sprite - we'll use the Black Lion as our base
    source_sprite = "orlandeau_black_lion_reference.bin"

    # Alternative: use original if Black Lion not available
    if not Path(source_sprite).exists():
        source_sprite = "ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_original/battle_oru_spr.bin"
        if not Path(source_sprite).exists():
            print("Error: No source Orlandeau sprite found!")
            return

    print(f"Using source sprite: {source_sprite}")
    print(f"\nCreating {len(ORLANDEAU_THEMES)} epic Orlandeau themes...\n")

    # Create all themes
    for theme in ORLANDEAU_THEMES:
        theme_name = theme["name"]
        output_dir = f"ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_orlandeau_{theme_name}"
        output_path = f"{output_dir}/battle_oru_spr.bin"

        create_orlandeau_theme(source_sprite, theme, output_path)

    print("\n✅ All Orlandeau themes created successfully!")
    print("\nThemes created:")
    for theme in ORLANDEAU_THEMES:
        print(f"  - sprites_orlandeau_{theme['name']}: {theme['description']}")

if __name__ == "__main__":
    main()