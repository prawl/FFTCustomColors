#!/usr/bin/env python3
"""
FFT Sprite Converter - Extract 4 corner views (NW, NE, SW, SE) for all classes and themes
Generates preview images for carousel display
"""

import struct
import sys
import os
from PIL import Image
import numpy as np

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

def extract_sprite_corner(data, corner, palette_index=0):
    """Extract a specific corner sprite from FFT sprite sheet

    Sprite layout indices (corrected based on testing):
    0: West, 1: Southwest, 2: South, 3: Northwest, 4: North
    """

    # Map corners to sprite indices
    corner_map = {
        'sw': 1,   # Southwest - index 1
        'nw': 3,   # Northwest - index 3
        'ne': 3,   # Northeast - mirror of NW (will be flipped)
        'se': 1    # Southeast - mirror of SW (will be flipped)
    }

    if corner.lower() not in corner_map:
        raise ValueError(f"Invalid corner: {corner}")

    sprite_index = corner_map[corner.lower()]
    needs_flip = corner.lower() in ['ne', 'se']

    sprite_data = data[512:]  # Skip palette data
    palette = read_palette(data, palette_index)
    palette[0] = (0, 0, 0, 0)  # Make background transparent

    sprite_width = 32
    sprite_height = 40
    sheet_width = 256

    x_offset = sprite_index * 32
    y_offset = 0

    # Create image array
    image = np.zeros((sprite_height, sprite_width, 4), dtype=np.uint8)

    for y in range(sprite_height):
        for x in range(sprite_width):
            sheet_x = x_offset + x
            sheet_y = y_offset + y

            pixel_index = (sheet_y * sheet_width) + sheet_x
            byte_index = pixel_index // 2

            if byte_index >= 0 and byte_index < len(sprite_data):
                byte = sprite_data[byte_index]
                if pixel_index % 2 == 0:
                    color_index = byte & 0x0F
                else:
                    color_index = (byte >> 4) & 0x0F

                if color_index < 16:
                    image[y, x] = palette[color_index]
                else:
                    image[y, x] = (0, 0, 0, 0)
            else:
                image[y, x] = (0, 0, 0, 0)

    pil_image = Image.fromarray(image, 'RGBA')

    # Flip if needed for NE or SE
    if needs_flip:
        pil_image = pil_image.transpose(Image.FLIP_LEFT_RIGHT)

    # Scale to 64x80 then crop to 64x64
    scaled = pil_image.resize((64, 80), Image.NEAREST)
    final_img = Image.new('RGBA', (64, 64), (0, 0, 0, 0))
    final_img.paste(scaled, (0, -8), scaled)

    return final_img

def get_all_job_sprites():
    """Return dictionary of all job sprites to process"""
    return {
        # Generic Jobs
        'squire_male': 'battle_mina_m_spr.bin',
        'squire_female': 'battle_mina_w_spr.bin',
        'chemist_male': 'battle_item_m_spr.bin',
        'chemist_female': 'battle_item_w_spr.bin',
        'knight_male': 'battle_knight_m_spr.bin',
        'knight_female': 'battle_knight_w_spr.bin',
        'archer_male': 'battle_yumi_m_spr.bin',
        'archer_female': 'battle_yumi_w_spr.bin',
        'monk_male': 'battle_monk_m_spr.bin',
        'monk_female': 'battle_monk_w_spr.bin',
        'white_mage_male': 'battle_siro_m_spr.bin',
        'white_mage_female': 'battle_siro_w_spr.bin',
        'black_mage_male': 'battle_kuro_m_spr.bin',
        'black_mage_female': 'battle_kuro_w_spr.bin',
        'time_mage_male': 'battle_toki_m_spr.bin',
        'time_mage_female': 'battle_toki_w_spr.bin',
        'summoner_male': 'battle_syou_m_spr.bin',
        'summoner_female': 'battle_syou_w_spr.bin',
        'thief_male': 'battle_thief_m_spr.bin',
        'thief_female': 'battle_thief_w_spr.bin',
        'orator_male': 'battle_waju_m_spr.bin',
        'orator_female': 'battle_waju_w_spr.bin',
        'mystic_male': 'battle_onmyo_m_spr.bin',
        'mystic_female': 'battle_onmyo_w_spr.bin',
        'geomancer_male': 'battle_fusui_m_spr.bin',
        'geomancer_female': 'battle_fusui_w_spr.bin',
        'dragoon_male': 'battle_ryu_m_spr.bin',
        'dragoon_female': 'battle_ryu_w_spr.bin',
        'samurai_male': 'battle_samu_m_spr.bin',
        'samurai_female': 'battle_samu_w_spr.bin',
        'ninja_male': 'battle_ninja_m_spr.bin',
        'ninja_female': 'battle_ninja_w_spr.bin',
        'calculator_male': 'battle_san_m_spr.bin',
        'calculator_female': 'battle_san_w_spr.bin',
        'bard_male': 'battle_gin_m_spr.bin',
        'dancer_female': 'battle_odori_w_spr.bin',
        'mime_male': 'battle_mono_m_spr.bin',
        'mime_female': 'battle_mono_w_spr.bin',
        'dark_knight_male': 'battle_dknight_m_spr.bin',
        'dark_knight_female': 'battle_dknight_w_spr.bin',
        'onion_knight_male': 'battle_tamanegi_m_spr.bin',
        'onion_knight_female': 'battle_tamanegi_w_spr.bin',
    }

def get_all_themes():
    """Return list of all available themes"""
    return [
        'original', 'corpse_brigade', 'lucavi', 'northern_sky', 'southern_sky',
        'aaron', 'crimson_red', 'royal_purple', 'phoenix_flame', 'frost_knight',
        'silver_knight', 'shadow_assassin', 'emerald_dragon', 'rose_gold',
        'ocean_depths', 'golden_templar', 'blood_moon', 'celestial', 'volcanic',
        'amethyst'
    ]

def process_all_sprites():
    """Process all job sprites for all themes"""
    base_dir = r"C:\Users\ptyRa\Dev\FFTColorCustomizer"
    sprites_base = os.path.join(base_dir, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit")
    output_base = os.path.join(base_dir, "ColorMod", "Resources", "Previews")

    jobs = get_all_job_sprites()
    themes = get_all_themes()
    corners = ['sw', 'se', 'nw', 'ne']

    total_jobs = len(jobs)
    total_themes = len(themes)
    total_images = total_jobs * total_themes * len(corners)

    print(f"Processing {total_jobs} jobs × {total_themes} themes × {len(corners)} corners = {total_images} images")
    print("="*60)

    processed = 0
    failed = 0

    for job_name, sprite_file in jobs.items():
        # Create job folder
        job_folder = os.path.join(output_base, job_name)
        os.makedirs(job_folder, exist_ok=True)

        print(f"\nProcessing {job_name}...")

        for theme in themes:
            # Find sprite file for this theme
            theme_dir = f"sprites_{theme}"
            sprite_path = os.path.join(sprites_base, theme_dir, sprite_file)

            if not os.path.exists(sprite_path):
                # Some themes might not have all sprites
                continue

            try:
                # Read sprite data
                with open(sprite_path, 'rb') as f:
                    sprite_data = f.read()

                # Extract all 4 corners
                for corner in corners:
                    corner_img = extract_sprite_corner(sprite_data, corner)

                    # Save with consistent naming
                    output_file = os.path.join(job_folder, f"{job_name}_{theme}_{corner}.png")
                    corner_img.save(output_file)
                    processed += 1

                    if processed % 100 == 0:
                        print(f"  Processed {processed}/{total_images} images...")

            except Exception as e:
                print(f"  Error processing {job_name}/{theme}: {e}")
                failed += 4  # Count all 4 corners as failed

    print(f"\n{'='*60}")
    print(f"Complete! Processed {processed} images, {failed} failed")
    print(f"Output directory: {output_base}")

    return processed > 0

def main():
    """Main entry point"""
    print("FFT Sprite Corner Extractor")
    print("Extracting 4 corner views for all jobs and themes...")
    print("="*60)

    success = process_all_sprites()

    if success:
        print("\nSuccess! All corner sprites extracted.")
        print("The carousel can now use these 4-corner views.")
    else:
        print("\nNo sprites were extracted. Check the paths and try again.")
        sys.exit(1)

if __name__ == "__main__":
    main()