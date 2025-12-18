#!/usr/bin/env python3
"""
Extract missing corner sprites for mediators and story characters
"""

import os
import shutil
import sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from extract_four_corners import extract_sprite_corner, get_all_themes

def process_mediators():
    """Copy orator sprites to mediator folders"""
    base_dir = r"C:\Users\ptyRa\Dev\FFTColorCustomizer"
    previews_dir = os.path.join(base_dir, "ColorMod", "Resources", "Previews")

    # Rename orator folders to mediator
    for gender in ['male', 'female']:
        orator_dir = os.path.join(previews_dir, f"orator_{gender}")
        mediator_dir = os.path.join(previews_dir, f"mediator_{gender}")

        if os.path.exists(orator_dir):
            # Create mediator folder
            os.makedirs(mediator_dir, exist_ok=True)

            # Copy all files, renaming from orator to mediator
            for file in os.listdir(orator_dir):
                if file.endswith('.png'):
                    old_path = os.path.join(orator_dir, file)
                    new_name = file.replace(f"orator_{gender}", f"mediator_{gender}")
                    new_path = os.path.join(mediator_dir, new_name)
                    shutil.copy2(old_path, new_path)
                    print(f"Copied {file} -> {new_name}")

    print("Mediator sprites created from orator sprites")

def get_story_character_sprites():
    """Get story character sprite definitions"""
    return {
        # Main story characters
        'agrias': ['battle_aguri_spr.bin', ['original', 'ash_dark']],
        'orlandeau': ['battle_oru_spr.bin', ['original', 'black_armor', 'golden_armor']],
        'cloud': ['battle_cloud_spr.bin', ['original', 'sephiroth_black', 'knights_round']],
        'alma': ['battle_aruma_spr.bin', ['original'] + [f'{color}' for color in [
            'bronze_armor', 'copper_shine', 'coral', 'crimson_red', 'deep_purple',
            'desert_sand', 'electric_blue', 'forest_green', 'golden_yellow',
            'iron_gray', 'lavender', 'lime_green', 'magenta', 'midnight_black'
        ]]],
        'delita': ['battle_deruta_spr.bin', ['original'] + [f'{color}' for color in [
            'crimson_red', 'deep_purple', 'desert_sand', 'electric_blue',
            'forest_green', 'golden_yellow', 'iron_gray'
        ]]],
        'mustadio': ['battle_masuta_spr.bin', ['original']],
        'beowulf': ['battle_beou_spr.bin', ['original']],
        'reis': ['battle_reisu_spr.bin', ['original']],
        'meliadoul': ['battle_meri_spr.bin', ['original', 'void_black']],
        'marach': ['battle_mara_spr.bin', ['original', 'nightmare_armor']],
        'rapha': ['battle_rafa_spr.bin', ['original']],
        'malak': ['battle_marakku_spr.bin', ['original'] + [f'{color}' for color in [
            'bronze_armor', 'copper_shine', 'coral', 'crimson_red'
        ]]],
        'celia': ['battle_seria_spr.bin', ['original'] + [f'{color}' for color in [
            'bronze_armor', 'copper_shine', 'coral', 'crimson_red'
        ]]],
        'lettie': ['battle_rety_spr.bin', ['original'] + [f'{color}' for color in [
            'coral', 'crimson_red', 'deep_purple'
        ]]],
        'gaffgarion': ['battle_gafu_spr.bin', ['original', 'blacksteel_red']],
        'elmdore': ['battle_erumu_spr.bin', ['original']],
    }

def process_story_characters():
    """Extract corner sprites for story characters"""
    base_dir = r"C:\Users\ptyRa\Dev\FFTColorCustomizer"
    sprites_base = os.path.join(base_dir, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit")
    output_base = os.path.join(base_dir, "ColorMod", "Resources", "Previews")

    story_chars = get_story_character_sprites()
    corners = ['sw', 'se', 'nw', 'ne']

    processed = 0
    for char_name, (sprite_file, themes) in story_chars.items():
        # Create character folder
        char_folder = os.path.join(output_base, char_name)
        os.makedirs(char_folder, exist_ok=True)

        print(f"\nProcessing {char_name}...")

        for theme in themes:
            # Try to find the sprite file
            # Story characters might be in sprites_original or have their own folders
            sprite_path = None

            # Try original first
            if theme == 'original':
                test_path = os.path.join(sprites_base, 'sprites_original', sprite_file)
                if os.path.exists(test_path):
                    sprite_path = test_path

            # Try theme-specific folder
            if not sprite_path:
                test_path = os.path.join(sprites_base, f'sprites_{theme}', sprite_file)
                if os.path.exists(test_path):
                    sprite_path = test_path

            if not sprite_path:
                # Skip if file not found
                continue

            try:
                # Read sprite data
                with open(sprite_path, 'rb') as f:
                    sprite_data = f.read()

                # Extract all 4 corners
                for corner in corners:
                    from PIL import Image
                    corner_img = extract_sprite_corner(sprite_data, corner)

                    # Save with consistent naming
                    output_file = os.path.join(char_folder, f"{char_name}_{theme}_{corner}.png")
                    corner_img.save(output_file)
                    processed += 1

            except Exception as e:
                print(f"  Error processing {char_name}/{theme}: {e}")

    print(f"\nProcessed {processed} story character corner sprites")

def main():
    print("Extracting missing corner sprites...")
    print("="*60)

    # Fix mediator naming
    print("\n1. Fixing mediator/orator naming...")
    process_mediators()

    # Extract story character corners
    print("\n2. Extracting story character corner sprites...")
    process_story_characters()

    print("\n" + "="*60)
    print("Complete! Missing sprites have been generated.")

if __name__ == "__main__":
    main()