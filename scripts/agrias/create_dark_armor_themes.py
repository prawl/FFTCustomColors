#!/usr/bin/env python3
"""
Create 50 dark armor color themes for Agrias
Only changes armor (indices 3-6) - very dark colors only
Preserves face and hair completely
"""

import os
import sys
import struct
from pathlib import Path
import shutil

def apply_theme_to_sprite(sprite_data, primary_color, accent_color):
    """Apply dark armor colors only"""
    modified_data = bytearray(sprite_data)

    # Create cohesive dark armor gradients
    armor_colors = {
        3: primary_color,  # Main chest armor
        4: (
            max(0, primary_color[0] - 15),
            max(0, primary_color[1] - 15),
            max(0, primary_color[2] - 15)
        ),  # Chest shadow
        5: accent_color,  # Lower armor/boots
        6: (
            max(0, accent_color[0] - 15),
            max(0, accent_color[1] - 15),
            max(0, accent_color[2] - 15)
        ),  # Lower armor shadow
    }

    # Apply armor colors
    for idx, (r, g, b) in armor_colors.items():
        if 3 <= idx <= 6:
            b5 = (b >> 3) & 0x1F
            g5 = (g >> 3) & 0x1F
            r5 = (r >> 3) & 0x1F
            bgr555 = (b5 << 10) | (g5 << 5) | r5
            offset = idx * 2
            modified_data[offset] = bgr555 & 0xFF
            modified_data[offset + 1] = (bgr555 >> 8) & 0xFF

    return modified_data

def create_theme(name, primary_color, accent_color, description):
    """Create a theme with specified armor colors"""
    print(f"  Creating {name}...")

    script_dir = Path(__file__).parent.absolute()
    project_root = script_dir.parent.parent

    source_dir = project_root / "ColorMod" / "FFTIVC" / "data" / "enhanced" / "fftpack" / "unit"
    dest_dir = source_dir / f"sprites_agrias_{name}"

    os.makedirs(dest_dir, exist_ok=True)

    agrias_sprites = ["battle_aguri_spr.bin", "battle_kanba_spr.bin"]

    for sprite_name in agrias_sprites:
        source_file = source_dir / sprite_name
        dest_file = dest_dir / sprite_name

        if not source_file.exists():
            print(f"    [!] Source not found: {sprite_name}")
            continue

        with open(source_file, 'rb') as f:
            sprite_data = f.read()

        modified_data = apply_theme_to_sprite(sprite_data, primary_color, accent_color)

        with open(dest_file, 'wb') as f:
            f.write(modified_data)

    # Write theme description
    with open(dest_dir / "THEME_INFO.txt", 'w') as f:
        f.write(f"{name.upper()} THEME\n")
        f.write("=" * 30 + "\n")
        f.write(f"{description}\n")
        f.write(f"Primary armor RGB: {primary_color}\n")
        f.write(f"Accent armor RGB: {accent_color}\n")

def delete_old_themes():
    """Delete all old theme directories except test and original"""
    print("Cleaning up old themes...")

    script_dir = Path(__file__).parent.absolute()
    project_root = script_dir.parent.parent
    themes_dir = project_root / "ColorMod" / "FFTIVC" / "data" / "enhanced" / "fftpack" / "unit"

    keep_themes = [
        "sprites_agrias_test",
        "sprites_agrias_original",
        "sprites_agrias_absolute_black",  # Keep the one you liked!
    ]

    for theme_dir in themes_dir.glob("sprites_agrias_*"):
        if theme_dir.is_dir() and theme_dir.name not in keep_themes:
            shutil.rmtree(theme_dir)
            print(f"  Deleted: {theme_dir.name}")

def main():
    print("Creating 50 Very Dark Armor Color Themes for Agrias")
    print("=" * 50)
    print("All dark variants - chest, boots, armor\n")

    # Clean up old themes
    delete_old_themes()

    print("\nCreating 50 dark armor themes...")
    print("-" * 50)

    themes = [
        # Pure Dark Shades (10) - Nearly black
        ((20, 20, 20), (10, 10, 10), "void_armor", "Void dark armor"),
        ((25, 25, 25), (15, 15, 15), "shadow_armor", "Shadow dark armor"),
        ((30, 30, 30), (20, 20, 20), "obsidian_armor", "Obsidian dark armor"),
        ((35, 35, 35), (25, 25, 25), "charcoal_armor", "Charcoal dark armor"),
        ((40, 40, 40), (30, 30, 30), "graphite_armor", "Graphite dark armor"),
        ((45, 45, 45), (35, 35, 35), "iron_dark", "Dark iron armor"),
        ((50, 50, 50), (40, 40, 40), "steel_dark", "Dark steel armor"),
        ((55, 55, 55), (45, 45, 45), "smoke_dark", "Dark smoke armor"),
        ((60, 60, 60), (50, 50, 50), "ash_dark", "Dark ash armor"),
        ((65, 65, 65), (55, 55, 55), "slate_dark", "Dark slate armor"),

        # Dark Blues (10)
        ((20, 20, 40), (10, 10, 30), "midnight_navy", "Midnight navy armor"),
        ((25, 25, 45), (15, 15, 35), "deep_ocean", "Deep ocean armor"),
        ((30, 30, 50), (20, 20, 40), "abyss_blue", "Abyss blue armor"),
        ((35, 35, 55), (25, 25, 45), "storm_blue", "Storm blue armor"),
        ((40, 40, 60), (30, 30, 50), "twilight_blue", "Twilight blue armor"),
        ((20, 30, 50), (10, 20, 40), "prussian_blue", "Prussian blue armor"),
        ((25, 35, 55), (15, 25, 45), "cobalt_dark", "Dark cobalt armor"),
        ((30, 40, 60), (20, 30, 50), "sapphire_dark", "Dark sapphire armor"),
        ((35, 45, 65), (25, 35, 55), "indigo_dark", "Dark indigo armor"),
        ((40, 50, 70), (30, 40, 60), "royal_dark", "Dark royal blue armor"),

        # Dark Reds (10)
        ((40, 20, 20), (30, 10, 10), "blood_dark", "Dark blood armor"),
        ((45, 25, 25), (35, 15, 15), "crimson_dark", "Dark crimson armor"),
        ((50, 30, 30), (40, 20, 20), "wine_dark", "Dark wine armor"),
        ((55, 35, 35), (45, 25, 25), "burgundy_dark", "Dark burgundy armor"),
        ((60, 40, 40), (50, 30, 30), "maroon_dark", "Dark maroon armor"),
        ((50, 20, 30), (40, 10, 20), "cherry_dark", "Dark cherry armor"),
        ((55, 25, 35), (45, 15, 25), "ruby_dark", "Dark ruby armor"),
        ((60, 30, 40), (50, 20, 30), "garnet_dark", "Dark garnet armor"),
        ((65, 35, 45), (55, 25, 35), "rust_dark", "Dark rust armor"),
        ((70, 40, 50), (60, 30, 40), "brick_dark", "Dark brick armor"),

        # Dark Greens (10)
        ((20, 40, 20), (10, 30, 10), "forest_dark", "Dark forest armor"),
        ((25, 45, 25), (15, 35, 15), "emerald_dark", "Dark emerald armor"),
        ((30, 50, 30), (20, 40, 20), "jade_dark", "Dark jade armor"),
        ((35, 55, 35), (25, 45, 25), "moss_dark", "Dark moss armor"),
        ((40, 60, 40), (30, 50, 30), "pine_dark", "Dark pine armor"),
        ((30, 50, 20), (20, 40, 10), "olive_dark", "Dark olive armor"),
        ((35, 55, 25), (25, 45, 15), "sage_dark", "Dark sage armor"),
        ((40, 60, 30), (30, 50, 20), "hunter_green", "Hunter green armor"),
        ((45, 65, 35), (35, 55, 25), "evergreen_dark", "Dark evergreen armor"),
        ((50, 70, 40), (40, 60, 30), "fern_dark", "Dark fern armor"),

        # Dark Purples (10)
        ((40, 20, 40), (30, 10, 30), "violet_dark", "Dark violet armor"),
        ((45, 25, 45), (35, 15, 35), "purple_dark", "Dark purple armor"),
        ((50, 30, 50), (40, 20, 40), "amethyst_dark", "Dark amethyst armor"),
        ((55, 35, 55), (45, 25, 45), "plum_dark", "Dark plum armor"),
        ((60, 40, 60), (50, 30, 50), "royal_purple_dark", "Dark royal purple armor"),
        ((50, 20, 60), (40, 10, 50), "indigo_purple", "Indigo purple armor"),
        ((55, 25, 65), (45, 15, 55), "byzantium_dark", "Dark byzantium armor"),
        ((60, 30, 70), (50, 20, 60), "grape_dark", "Dark grape armor"),
        ((65, 35, 75), (55, 25, 65), "eggplant_dark", "Dark eggplant armor"),
        ((70, 40, 80), (60, 30, 70), "mulberry_dark", "Dark mulberry armor"),
    ]

    # Create all themes
    for primary_color, accent_color, name, desc in themes:
        create_theme(name, primary_color, accent_color, desc)

    print("\n" + "=" * 50)
    print("SUCCESSFULLY CREATED 50 DARK ARMOR THEMES!")
    print("\nCategories:")
    print("- Pure Dark Shades (10) - Nearly black variations")
    print("- Dark Blues (10) - Midnight to prussian blues")
    print("- Dark Reds (10) - Blood to burgundy reds")
    print("- Dark Greens (10) - Forest to hunter greens")
    print("- Dark Purples (10) - Violet to mulberry purples")
    print("\nKept themes: original, test, absolute_black")
    print("All themes preserve face/hair - only armor changes!")

if __name__ == "__main__":
    main()