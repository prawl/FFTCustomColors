#!/usr/bin/env python3
"""
Extract corner sprites for themed versions of story characters
(excluding Delita, Celia, Alma, Gaffgarion, Lettie)
"""

import os
import sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from extract_four_corners import extract_sprite_corner
from PIL import Image

def process_story_themes():
    """Extract corner sprites for story character themed versions"""
    base_dir = r"C:\Users\ptyRa\Dev\FFTColorCustomizer"
    unit_base = os.path.join(base_dir, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit")
    output_base = os.path.join(base_dir, "ColorMod", "Resources", "Previews")

    # Story character themes to process (excluding the ones you don't want)
    story_themes = {
        'agrias': {
            'sprite_file': 'battle_aguri_spr.bin',
            'themes': ['agrias_ash_dark']
        },
        'cloud': {
            'sprite_file': 'battle_cloud_spr.bin',
            'themes': ['cloud_knights_round', 'cloud_sephiroth_black']
        },
        'orlandeau': {
            'sprite_file': 'battle_oru_spr.bin',
            'themes': ['orlandeau_thunder_god']
        },
        'marach': {
            'sprite_file': 'battle_mara_spr.bin',
            'themes': ['marach_nightmare_armor']
        },
        'meliadoul': {
            'sprite_file': 'battle_h85_spr.bin',
            'themes': ['meliadoul_void_black']
        },
        'rapha': {
            'sprite_file': 'battle_rafa_spr.bin',
            'themes': ['rapha_twilight_blend']
        },
        'reis': {
            'sprite_file': 'battle_reze_spr.bin',
            'themes': [
                'reis_bronze_armor', 'reis_copper_shine', 'reis_coral',
                'reis_crimson_red', 'reis_deep_purple', 'reis_desert_sand',
                'reis_electric_blue', 'reis_forest_green', 'reis_golden_yellow',
                'reis_iron_gray', 'reis_lavender', 'reis_lime_green',
                'reis_magenta', 'reis_midnight_black', 'reis_mint_green',
                'reis_moss_green', 'reis_ocean_blue', 'reis_orange_flame',
                'reis_platinum', 'reis_rose_pink', 'reis_royal_blue',
                'reis_silver_steel', 'reis_sunset_orange', 'reis_turquoise',
                'reis_violet'
            ]
        }
    }

    corners = ['sw', 'se', 'nw', 'ne']
    total_processed = 0

    print("Extracting themed versions for story characters...")
    print("="*60)

    for char_name, char_info in story_themes.items():
        char_folder = os.path.join(output_base, char_name)
        os.makedirs(char_folder, exist_ok=True)

        sprite_file = char_info['sprite_file']
        themes = char_info['themes']

        print(f"\n{char_name.upper()}:")

        for theme in themes:
            theme_dir = f"sprites_{theme}"
            sprite_path = os.path.join(unit_base, theme_dir, sprite_file)

            if not os.path.exists(sprite_path):
                print(f"  Warning: {theme} sprite not found at {sprite_path}")
                continue

            # Clean theme name for file naming
            clean_theme = theme.replace(f"{char_name}_", "")

            try:
                with open(sprite_path, 'rb') as f:
                    sprite_data = f.read()

                for corner in corners:
                    corner_img = extract_sprite_corner(sprite_data, corner, 0)

                    # Save with consistent naming
                    output_file = os.path.join(char_folder, f"{char_name}_{clean_theme}_{corner}.png")
                    corner_img.save(output_file)
                    total_processed += 1

                print(f"  [OK] {clean_theme}: 4 corners extracted")

            except Exception as e:
                print(f"  [ERROR] {clean_theme}: Error - {e}")

    print(f"\n{'='*60}")
    print(f"Total sprites processed: {total_processed}")
    print(f"Expected: {(total_processed // 4)} theme variations × 4 corners")

    return total_processed > 0

def main():
    print("Story Character Themed Sprite Extraction")
    print("="*60)

    success = process_story_themes()

    if success:
        print("\nSuccess! All themed story character sprites extracted.")
    else:
        print("\nNo themed sprites were extracted.")

if __name__ == "__main__":
    main()