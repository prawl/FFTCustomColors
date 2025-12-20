#!/usr/bin/env python3
"""
Create the ULTIMATE sprite sheet with EVERY character in EVERY theme
This will show all job/character combinations for each theme!
"""

import struct
import sys
import os
from PIL import Image
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
    sprite_height = 40  # Reduced to avoid cut-off heads
    sheet_width = 256

    # Position of southwest-facing sprite (index 1)
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

def create_ultimate_spritesheet():
    """Create a massive sprite sheet with ALL characters in ALL themes"""

    # Base directory
    base_dir = Path(r"C:\Users\ptyRa\Dev\FFTColorCustomizer")
    sprites_dir = base_dir / "ColorMod" / "FFTIVC" / "data" / "enhanced" / "fftpack" / "unit"

    print("Scanning for all theme directories...")

    # Get all sprite theme directories
    theme_dirs = [d for d in sprites_dir.glob("sprites_*") if d.is_dir()]
    theme_dirs.sort()

    print(f"Found {len(theme_dirs)} theme directories!")

    # Define all character sprites we want to show
    # Format: (filename, display_name)
    character_sprites = [
        # Story Characters
        ("battle_oru_spr.bin", "Orlandeau"),
        ("battle_aguri_spr.bin", "Agrias"),
        ("battle_kura_spr.bin", "Cloud"),
        ("battle_masuta_spr.bin", "Mustadio"),
        ("battle_reisu_spr.bin", "Reis"),
        ("battle_beo_spr.bin", "Beowulf"),
        ("battle_rafa_spr.bin", "Rapha"),
        ("battle_marati_spr.bin", "Marach"),
        ("battle_meri_spr.bin", "Meliadoul"),

        # Male Jobs
        ("battle_mina_m_spr.bin", "Squire-M"),
        ("battle_kan_m_spr.bin", "Knight-M"),
        ("battle_mon_m_spr.bin", "Monk-M"),
        ("battle_arc_m_spr.bin", "Archer-M"),
        ("battle_wmg_m_spr.bin", "WhtMage-M"),
        ("battle_bmg_m_spr.bin", "BlkMage-M"),
        ("battle_tmg_m_spr.bin", "TimeMage-M"),
        ("battle_sum_m_spr.bin", "Summoner-M"),
        ("battle_thf_m_spr.bin", "Thief-M"),
        ("battle_med_m_spr.bin", "Mediator-M"),
        ("battle_ora_m_spr.bin", "Mystic-M"),
        ("battle_geo_m_spr.bin", "Geomancer-M"),
        ("battle_dra_m_spr.bin", "Dragoon-M"),
        ("battle_sam_m_spr.bin", "Samurai-M"),
        ("battle_nin_m_spr.bin", "Ninja-M"),
        ("battle_cal_m_spr.bin", "Calculator-M"),
        ("battle_bar_spr.bin", "Bard"),
        ("battle_mim_m_spr.bin", "Mime-M"),
        ("battle_che_m_spr.bin", "Chemist-M"),

        # Female Jobs
        ("battle_mina_f_spr.bin", "Squire-F"),
        ("battle_kan_f_spr.bin", "Knight-F"),
        ("battle_mon_f_spr.bin", "Monk-F"),
        ("battle_arc_f_spr.bin", "Archer-F"),
        ("battle_wmg_f_spr.bin", "WhtMage-F"),
        ("battle_bmg_f_spr.bin", "BlkMage-F"),
        ("battle_tmg_f_spr.bin", "TimeMage-F"),
        ("battle_sum_f_spr.bin", "Summoner-F"),
        ("battle_thf_f_spr.bin", "Thief-F"),
        ("battle_med_f_spr.bin", "Mediator-F"),
        ("battle_ora_f_spr.bin", "Mystic-F"),
        ("battle_geo_f_spr.bin", "Geomancer-F"),
        ("battle_dra_f_spr.bin", "Dragoon-F"),
        ("battle_sam_f_spr.bin", "Samurai-F"),
        ("battle_nin_f_spr.bin", "Ninja-F"),
        ("battle_cal_f_spr.bin", "Calculator-F"),
        ("battle_dan_spr.bin", "Dancer"),
        ("battle_mim_f_spr.bin", "Mime-F"),
        ("battle_che_f_spr.bin", "Chemist-F"),
    ]

    # Collect all sprites to show
    all_sprites = []

    for theme_dir in theme_dirs:
        theme_name = theme_dir.name.replace("sprites_", "")

        for sprite_file, char_name in character_sprites:
            full_path = theme_dir / sprite_file
            if full_path.exists():
                all_sprites.append((full_path, f"{theme_name}/{char_name}"))

    print(f"\nTotal character/theme combinations: {len(all_sprites)}")

    if not all_sprites:
        print("No sprites found!")
        return

    # Calculate grid dimensions - use more columns for better layout
    sprites_per_row = 40  # More columns since we have many sprites
    num_rows = math.ceil(len(all_sprites) / sprites_per_row)

    # Individual sprite dimensions (with padding)
    sprite_width = 32
    sprite_height = 40
    padding = 2  # Smaller padding to fit more
    cell_width = sprite_width + padding * 2
    cell_height = sprite_height + padding * 2

    # Create the mega canvas
    canvas_width = sprites_per_row * cell_width
    canvas_height = num_rows * cell_height

    print(f"\nCreating ULTIMATE sprite sheet: {canvas_width}x{canvas_height} pixels")
    print(f"Grid: {sprites_per_row} columns x {num_rows} rows")
    print(f"Total sprites: {len(all_sprites)}")

    # Create the canvas with a dark background
    canvas = Image.new('RGBA', (canvas_width, canvas_height), (32, 32, 48, 255))

    # Process each sprite
    for idx, (sprite_file, label) in enumerate(all_sprites):
        if idx % 50 == 0:
            print(f"Processing sprite {idx+1}/{len(all_sprites)}...")

        try:
            # Read the sprite file
            with open(sprite_file, 'rb') as f:
                data = f.read()

            # Extract the southwest-facing sprite
            sprite_img = extract_single_sprite(data, sprite_index=1)

            # Calculate position in grid
            col = idx % sprites_per_row
            row = idx // sprites_per_row

            x_pos = col * cell_width + padding
            y_pos = row * cell_height + padding

            # Paste the sprite
            canvas.paste(sprite_img, (x_pos, y_pos), sprite_img)

        except Exception as e:
            print(f"  Error processing {label}: {e}")

    # Save the ultimate sprite sheet
    output_file = base_dir / "ULTIMATE_ALL_SPRITES.png"
    canvas.save(output_file, 'PNG', optimize=True)

    print(f"\n{'='*60}")
    print(f"SUCCESS! Ultimate sprite sheet created!")
    print(f"File: {output_file}")
    print(f"Size: {canvas_width}x{canvas_height} pixels")
    print(f"Total sprites: {len(all_sprites)}")
    print(f"{'='*60}")

    # Open the result
    if sys.platform == "win32":
        os.startfile(str(output_file))

    return str(output_file)

def main():
    try:
        result = create_ultimate_spritesheet()
        if result:
            print("\nULTIMATE SPRITE SHEET COMPLETE!")
            print("This shows EVERY character in EVERY theme!")
    except Exception as e:
        print(f"Error: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == "__main__":
    main()