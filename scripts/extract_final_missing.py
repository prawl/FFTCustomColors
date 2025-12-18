#!/usr/bin/env python3
"""
Extract corner sprites for the final missing story characters
"""

import os
import sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from extract_four_corners import extract_sprite_corner
from PIL import Image

def process_missing_characters():
    """Extract corner sprites for Beowulf, Meliadoul, Mustadio, Reis"""
    base_dir = r"C:\Users\ptyRa\Dev\FFTColorCustomizer"
    sprites_base = os.path.join(base_dir, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit", "sprites_original")
    output_base = os.path.join(base_dir, "ColorMod", "Resources", "Previews")

    # These characters only have sprites in sprites_original
    missing_chars = {
        'beowulf': None,  # Need to find the correct file
        'meliadoul': None,  # Need to find the correct file
        'mustadio': None,  # Need to find the correct file
        'reis': 'battle_reze_spr.bin'  # Found this one
    }

    # Let's search for the missing files
    import glob
    sprite_files = glob.glob(os.path.join(sprites_base, "battle_*.bin"))

    print("Searching for story character sprites...")
    print("="*60)

    # Common character name variations to check
    name_variations = {
        'beowulf': ['beou', 'beowulf', 'beo'],
        'meliadoul': ['meri', 'melia', 'meliadoul', 'mel'],
        'mustadio': ['musu', 'musta', 'mustadio', 'must', 'masuta']
    }

    for char, variations in name_variations.items():
        for sprite_file in sprite_files:
            filename = os.path.basename(sprite_file).lower()
            for var in variations:
                if var in filename:
                    missing_chars[char] = os.path.basename(sprite_file)
                    print(f"Found {char}: {os.path.basename(sprite_file)}")
                    break
            if missing_chars[char]:
                break

    # If we still haven't found them, let's check a broader search
    if not missing_chars['beowulf']:
        # Beowulf might not have a dedicated sprite
        print("Warning: Could not find Beowulf sprite file")
    if not missing_chars['meliadoul']:
        # Meliadoul might not have a dedicated sprite
        print("Warning: Could not find Meliadoul sprite file")
    if not missing_chars['mustadio']:
        # Mustadio might not have a dedicated sprite
        print("Warning: Could not find Mustadio sprite file")

    print("\n" + "="*60)
    print("Extracting corner sprites for found characters...")

    corners = ['sw', 'se', 'nw', 'ne']
    processed = 0

    for char_name, sprite_file in missing_chars.items():
        if not sprite_file:
            continue

        char_folder = os.path.join(output_base, char_name)
        os.makedirs(char_folder, exist_ok=True)

        sprite_path = os.path.join(sprites_base, sprite_file)

        if not os.path.exists(sprite_path):
            print(f"Sprite file not found: {sprite_path}")
            continue

        print(f"\nProcessing {char_name} from {sprite_file}...")

        try:
            # Read sprite data
            with open(sprite_path, 'rb') as f:
                sprite_data = f.read()

            # Extract all 4 corners
            for corner in corners:
                corner_img = extract_sprite_corner(sprite_data, corner, 0)

                # Save with consistent naming
                output_file = os.path.join(char_folder, f"{char_name}_original_{corner}.png")
                corner_img.save(output_file)
                processed += 1
                print(f"  Created {corner} sprite")

        except Exception as e:
            print(f"  Error processing {char_name}: {e}")

    print(f"\n{'='*60}")
    print(f"Processed {processed} corner sprites")

    # Also process reis for other themes if they exist
    if missing_chars['reis']:
        print("\nChecking for Reis alternate themes...")
        reis_themes = []

        # Check which theme folders have reis sprites
        for theme_dir in os.listdir(os.path.join(base_dir, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit")):
            if theme_dir.startswith("sprites_") and theme_dir != "sprites_original":
                theme_name = theme_dir.replace("sprites_", "")
                sprite_path = os.path.join(base_dir, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit", theme_dir, missing_chars['reis'])
                if os.path.exists(sprite_path):
                    reis_themes.append(theme_name)

        if reis_themes:
            print(f"Found Reis in themes: {', '.join(reis_themes)}")
            # For now, we'll skip alternate themes since they might not be needed

    return processed > 0

def main():
    print("Extracting corner sprites for missing story characters...")
    print("="*60)

    success = process_missing_characters()

    if success:
        print("\nSuccess! Missing character sprites extracted.")
    else:
        print("\nNo sprites were extracted.")

if __name__ == "__main__":
    main()