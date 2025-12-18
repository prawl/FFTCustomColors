#!/usr/bin/env python3
"""
Extract corner sprites for Mustadio, Meliadoul, and Beowulf
"""

import os
import sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from extract_four_corners import extract_sprite_corner
from PIL import Image

def process_final_three():
    """Extract corner sprites for the three missing story characters"""
    base_dir = r"C:\Users\ptyRa\Dev\FFTColorCustomizer"
    sprites_base = os.path.join(base_dir, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit", "sprites_original")
    output_base = os.path.join(base_dir, "ColorMod", "Resources", "Previews")

    # Now we have the correct files
    characters = {
        'mustadio': 'battle_musu_spr.bin',
        'meliadoul': 'battle_h85_spr.bin',
        'beowulf': 'battle_beio_spr.bin'
    }

    corners = ['sw', 'se', 'nw', 'ne']
    total_processed = 0

    for char_name, sprite_file in characters.items():
        char_folder = os.path.join(output_base, char_name)
        os.makedirs(char_folder, exist_ok=True)

        sprite_path = os.path.join(sprites_base, sprite_file)

        if not os.path.exists(sprite_path):
            print(f"Error: Sprite file not found: {sprite_path}")
            continue

        print(f"\nProcessing {char_name} from {sprite_file}...")
        print(f"  File size: {os.path.getsize(sprite_path)} bytes")

        try:
            # Read sprite data
            with open(sprite_path, 'rb') as f:
                sprite_data = f.read()

            # Extract all 4 corners for original theme
            for corner in corners:
                corner_img = extract_sprite_corner(sprite_data, corner, 0)

                # Save with consistent naming
                output_file = os.path.join(char_folder, f"{char_name}_original_{corner}.png")
                corner_img.save(output_file)
                print(f"  Created {corner} sprite")
                total_processed += 1

        except Exception as e:
            print(f"  Error processing {char_name}: {e}")
            import traceback
            traceback.print_exc()

    print(f"\n{'='*60}")
    print(f"Successfully processed {total_processed} corner sprites")

    # Also check if these characters have other themes
    print("\nChecking for alternate themes...")

    for char_name in characters.keys():
        themes_found = []
        unit_dir = os.path.dirname(sprites_base)

        for theme_dir in os.listdir(unit_dir):
            if theme_dir.startswith("sprites_") and theme_dir != "sprites_original":
                theme_name = theme_dir.replace("sprites_", "")
                sprite_file = characters[char_name]
                theme_sprite_path = os.path.join(unit_dir, theme_dir, sprite_file)

                if os.path.exists(theme_sprite_path):
                    themes_found.append(theme_name)

                    # Extract corners for this theme too
                    try:
                        with open(theme_sprite_path, 'rb') as f:
                            sprite_data = f.read()

                        for corner in corners:
                            corner_img = extract_sprite_corner(sprite_data, corner, 0)
                            output_file = os.path.join(char_folder, f"{char_name}_{theme_name}_{corner}.png")
                            corner_img.save(output_file)
                            total_processed += 1

                        print(f"  Extracted {char_name} theme: {theme_name}")

                    except Exception as e:
                        print(f"  Error extracting {char_name} {theme_name}: {e}")

        if themes_found:
            print(f"{char_name}: Found themes - {', '.join(themes_found)}")
        else:
            print(f"{char_name}: No alternate themes found")

    return total_processed > 0

def main():
    print("Extracting corner sprites for Mustadio, Meliadoul, and Beowulf...")
    print("="*60)

    success = process_final_three()

    if success:
        print("\nSuccess! All three characters now have corner sprites.")
        print("The carousel should now work for all story characters!")
    else:
        print("\nFailed to extract sprites.")

if __name__ == "__main__":
    main()