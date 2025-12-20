#!/usr/bin/env python3
"""
Create a theme showcase with one representative sprite from EACH theme
Shows all 210+ themes with their best available sprite
"""

import struct
import sys
import os
from PIL import Image, ImageDraw, ImageFont
import numpy as np
from pathlib import Path
import math

def bgr555_to_rgb888(bgr555):
    """Convert BGR555 color format to RGB888"""
    b = (bgr555 >> 10) & 0x1F
    g = (bgr555 >> 5) & 0x1F
    r = bgr555 & 0x1F

    r = (r * 255) // 31
    g = (g * 255) // 31
    b = (b * 255) // 31

    return (r, g, b, 255)

def read_palette(data, palette_index=0):
    """Read a single 16-color palette from sprite data"""
    palette = []
    base_offset = palette_index * 32

    for i in range(16):
        offset = base_offset + (i * 2)
        if offset + 1 < len(data):
            color_value = struct.unpack('<H', data[offset:offset+2])[0]
            rgba = bgr555_to_rgb888(color_value)
            palette.append(rgba)
        else:
            palette.append((0, 0, 0, 255))

    return palette

def extract_single_sprite(data, sprite_index=1, palette_index=0):
    """Extract a single sprite (facing southwest) from the sheet"""
    # Skip palette data
    sprite_data = data[512:]

    # Read palette
    palette = read_palette(data, palette_index)
    palette[0] = (0, 0, 0, 0)  # Transparent background

    # Sprite dimensions
    sprite_width = 32
    sprite_height = 40
    sheet_width = 256

    # Position of southwest-facing sprite
    x_offset = sprite_index * 32
    y_offset = 0

    # Create image array
    image = np.zeros((sprite_height, sprite_width, 4), dtype=np.uint8)

    # Extract the sprite
    for y in range(sprite_height):
        for x in range(sprite_width):
            sheet_x = x_offset + x
            sheet_y = y_offset + y

            pixel_index = (sheet_y * sheet_width) + sheet_x
            byte_index = pixel_index // 2

            if byte_index < len(sprite_data):
                byte = sprite_data[byte_index]
                if pixel_index % 2 == 0:
                    color_index = byte & 0x0F
                else:
                    color_index = (byte >> 4) & 0x0F

                if color_index < 16:
                    image[y, x] = palette[color_index]
            else:
                image[y, x] = (0, 0, 0, 0)

    return Image.fromarray(image, 'RGBA')

def get_best_sprite_from_theme(theme_dir):
    """Get the best representative sprite from a theme directory"""

    # Priority order for selecting representative sprites
    priority_sprites = [
        # Story characters (most distinctive)
        "battle_oru_spr.bin",      # Orlandeau
        "battle_aguri_spr.bin",    # Agrias
        "battle_kura_spr.bin",     # Cloud
        "battle_masuta_spr.bin",   # Mustadio
        "battle_reisu_spr.bin",    # Reis
        "battle_rafa_spr.bin",     # Rapha
        "battle_marati_spr.bin",   # Marach
        "battle_beo_spr.bin",      # Beowulf
        "battle_meri_spr.bin",     # Meliadoul

        # Popular male jobs
        "battle_kan_m_spr.bin",    # Knight Male
        "battle_nin_m_spr.bin",    # Ninja Male
        "battle_dra_m_spr.bin",    # Dragoon Male
        "battle_mon_m_spr.bin",    # Monk Male
        "battle_sam_m_spr.bin",    # Samurai Male
        "battle_bmg_m_spr.bin",    # Black Mage Male
        "battle_mina_m_spr.bin",   # Squire Male

        # Female jobs
        "battle_kan_f_spr.bin",    # Knight Female
        "battle_nin_f_spr.bin",    # Ninja Female
        "battle_dan_spr.bin",      # Dancer
        "battle_sum_f_spr.bin",    # Summoner Female

        # Any other male job sprites (different naming schemes)
        "battle_gin_m_spr.bin",    # Silver male
        "battle_sei_m_spr.bin",    # Holy male
        "battle_fusui_m_spr.bin",  # Geomancer male (alt name)
        "battle_item_m_spr.bin",   # Item/Chemist male (alt name)
        "battle_yin_m_spr.bin",    # Mystic male (alt name)
        "battle_jyut_m_spr.bin",   # Mediator male (alt name)
        "battle_toki_m_spr.bin",   # Time Mage male (alt name)

        # Female variants
        "battle_gin_w_spr.bin",    # Silver female
        "battle_sei_w_spr.bin",    # Holy female
        "battle_fusui_w_spr.bin",  # Geomancer female
    ]

    # Try priority sprites first
    for sprite_name in priority_sprites:
        sprite_path = theme_dir / sprite_name
        if sprite_path.exists():
            return sprite_path

    # If no priority sprite found, just get the first .bin file
    bin_files = list(theme_dir.glob("*.bin"))
    if bin_files:
        # Prefer _m_ (male) or _spr files over _w_ (female) for consistency
        male_sprites = [f for f in bin_files if "_m_" in f.name or "_spr.bin" in f.name]
        if male_sprites:
            return male_sprites[0]
        return bin_files[0]

    return None

def create_theme_showcase():
    """Create a comprehensive showcase of all themes"""

    # Base directory
    base_dir = Path(r"C:\Users\ptyRa\Dev\FFTColorCustomizer")
    sprites_dir = base_dir / "ColorMod" / "FFTIVC" / "data" / "enhanced" / "fftpack" / "unit"

    print("Scanning for all theme directories...")

    # Get all sprite theme directories
    theme_dirs = [d for d in sprites_dir.glob("sprites_*") if d.is_dir()]
    theme_dirs.sort()

    print(f"Found {len(theme_dirs)} theme directories!")

    # Collect best sprite from each theme
    theme_sprites = []

    for theme_dir in theme_dirs:
        theme_name = theme_dir.name.replace("sprites_", "")
        best_sprite = get_best_sprite_from_theme(theme_dir)

        if best_sprite:
            # Extract character type from filename for better labeling
            sprite_filename = best_sprite.name
            if "oru" in sprite_filename:
                char_type = "Orlandeau"
            elif "aguri" in sprite_filename:
                char_type = "Agrias"
            elif "kura" in sprite_filename:
                char_type = "Cloud"
            elif "kan" in sprite_filename:
                char_type = "Knight"
            elif "nin" in sprite_filename:
                char_type = "Ninja"
            elif "dra" in sprite_filename:
                char_type = "Dragoon"
            elif "mon" in sprite_filename:
                char_type = "Monk"
            elif "mina" in sprite_filename:
                char_type = "Squire"
            else:
                char_type = "Sprite"

            theme_sprites.append((best_sprite, theme_name, char_type))
            print(f"  {theme_name}: Using {char_type}")

    print(f"\nTotal themes to showcase: {len(theme_sprites)}")

    if not theme_sprites:
        print("No sprites found!")
        return

    # Calculate grid dimensions
    sprites_per_row = 21  # 21 columns for nice layout with 210 themes
    num_rows = math.ceil(len(theme_sprites) / sprites_per_row)

    # Individual sprite dimensions
    sprite_width = 32
    sprite_height = 40
    padding = 2
    cell_width = sprite_width + padding * 2
    cell_height = sprite_height + padding * 2

    # Create the canvas
    canvas_width = sprites_per_row * cell_width
    canvas_height = num_rows * cell_height

    print(f"\nCreating theme showcase: {canvas_width}x{canvas_height} pixels")
    print(f"Grid: {sprites_per_row} columns x {num_rows} rows")

    # Create canvas with dark background
    canvas = Image.new('RGBA', (canvas_width, canvas_height), (24, 24, 32, 255))

    # Process each theme
    for idx, (sprite_file, theme_name, char_type) in enumerate(theme_sprites):
        if idx % 20 == 0:
            print(f"Processing theme {idx+1}/{len(theme_sprites)}...")

        try:
            # Read sprite file
            with open(sprite_file, 'rb') as f:
                data = f.read()

            # Extract sprite
            sprite_img = extract_single_sprite(data, sprite_index=1)

            # Calculate grid position
            col = idx % sprites_per_row
            row = idx // sprites_per_row

            x_pos = col * cell_width + padding
            y_pos = row * cell_height + padding

            # Paste sprite
            canvas.paste(sprite_img, (x_pos, y_pos), sprite_img)

        except Exception as e:
            print(f"  Error processing {theme_name}: {e}")

    # Save the showcase
    output_file = base_dir / "THEME_SHOWCASE_ALL_210.png"
    canvas.save(output_file, 'PNG', optimize=True)

    print(f"\n{'='*60}")
    print(f"SUCCESS! Theme showcase created!")
    print(f"File: {output_file}")
    print(f"Size: {canvas_width}x{canvas_height} pixels")
    print(f"Themes included: {len(theme_sprites)}")
    print(f"{'='*60}")

    # Open result
    if sys.platform == "win32":
        os.startfile(str(output_file))

    return str(output_file)

def main():
    try:
        result = create_theme_showcase()
        if result:
            print("\nTHEME SHOWCASE COMPLETE!")
            print("This shows the best sprite from each of your 210+ themes!")
    except Exception as e:
        print(f"Error: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == "__main__":
    main()