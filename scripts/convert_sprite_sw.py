#!/usr/bin/env python3
"""
FFT Sprite Converter - Extract southwest-facing sprite for configuration previews
Generates standardized 64x64 preview images for all themes
"""

import struct
import sys
import os
from PIL import Image
import numpy as np

def bgr555_to_rgb888(bgr555):
    """Convert BGR555 color format to RGB888"""
    # Extract components (5 bits each)
    b = (bgr555 >> 10) & 0x1F
    g = (bgr555 >> 5) & 0x1F
    r = bgr555 & 0x1F

    # Convert 5-bit to 8-bit (multiply by 8.225 to get full 0-255 range)
    r = (r * 255) // 31
    g = (g * 255) // 31
    b = (b * 255) // 31

    return (r, g, b, 255)  # RGBA with full opacity

def read_palette(data, palette_index=0):
    """Read a single 16-color palette from sprite data"""
    palette = []
    base_offset = palette_index * 32  # Each palette is 16 colors * 2 bytes = 32 bytes

    for i in range(16):
        offset = base_offset + (i * 2)
        if offset + 1 < len(data):
            # Read 16-bit color value (little endian)
            color_value = struct.unpack('<H', data[offset:offset+2])[0]
            # Convert BGR555 to RGB888
            rgba = bgr555_to_rgb888(color_value)
            palette.append(rgba)
        else:
            # If we run out of data, use black
            palette.append((0, 0, 0, 255))

    return palette

def extract_southwest_sprite(input_file, output_file=None, palette_index=0, preview_mode=False):
    """Extract just the southwest-facing sprite from FFT sprite sheet

    Args:
        input_file: Path to the sprite .bin file
        output_file: Optional output path
        palette_index: Which palette to use (0-15)
        preview_mode: If True, generates 64x64 config menu preview
    """

    # Read the binary file
    with open(input_file, 'rb') as f:
        data = f.read()

    print(f"Processing: {os.path.basename(input_file)}")
    print(f"File size: {len(data)} bytes")

    # Skip palette data (512 bytes)
    sprite_data = data[512:]

    # Read the specified palette and make background transparent
    palette = read_palette(data, palette_index)
    palette[0] = (0, 0, 0, 0)  # Make first color (background) transparent

    # FFT sprites layout:
    # - Width: 256 pixels
    # - Each sprite is roughly 32x32 pixels
    # - Southwest is the 2nd sprite (index 1), so x offset = 32 pixels

    sprite_width = 32
    sprite_height = 40  # Extract 8 extra pixels to capture full boots
    sheet_width = 256

    # Southwest sprite position
    # Southwest facing sprite position
    x_offset = 32  # Second sprite in the row (index 1)
    y_offset = 0   # Start at the top to capture the full sprite including boots

    # Create image array for the sprite
    image = np.zeros((sprite_height, sprite_width, 4), dtype=np.uint8)

    # Extract the sprite pixel by pixel
    for y in range(sprite_height):
        for x in range(sprite_width):
            # Calculate position in the full sprite sheet
            sheet_x = x_offset + x
            sheet_y = y_offset + y

            # Handle edge cases
            if sheet_y < 0:
                image[y, x] = (0, 0, 0, 0)  # Transparent padding
                continue

            # Calculate pixel index in the sprite data
            pixel_index = (sheet_y * sheet_width) + sheet_x
            byte_index = pixel_index // 2

            if byte_index >= 0 and byte_index < len(sprite_data):
                byte = sprite_data[byte_index]
                # Get 4-bit value (alternate between high and low nibble)
                if pixel_index % 2 == 0:
                    color_index = byte & 0x0F  # Low nibble
                else:
                    color_index = (byte >> 4) & 0x0F  # High nibble

                # Use the palette color
                if color_index < 16:
                    image[y, x] = palette[color_index]
                else:
                    image[y, x] = (0, 0, 0, 0)  # Transparent for invalid indices
            else:
                # If we run out of data, use transparent
                image[y, x] = (0, 0, 0, 0)

    # Convert to PIL image
    pil_image = Image.fromarray(image, 'RGBA')

    if preview_mode:
        # Create 64x64 preview for config menu
        final_img = Image.new('RGBA', (64, 64), (0, 0, 0, 0))

        # Scale 2x width, but keep aspect ratio for height (32x40 -> 64x80)
        scaled_sprite = pil_image.resize((64, 80), Image.NEAREST)

        # Paste into 64x64 frame, positioned to align with original sprites
        final_img.paste(scaled_sprite, (0, -8), scaled_sprite)

        result_image = final_img
        size_info = "64x64 (config preview)"
    else:
        # Original 8x scale mode for detailed viewing
        scale_factor = 8
        scaled_width = sprite_width * scale_factor
        scaled_height = sprite_height * scale_factor
        result_image = pil_image.resize((scaled_width, scaled_height), Image.NEAREST)
        size_info = f"{scaled_width}x{scaled_height} ({scale_factor}x)"

    # Generate output filename if not provided
    if output_file is None:
        base_name = os.path.splitext(input_file)[0]
        suffix = "_preview" if preview_mode else "_sw"
        output_file = f"{base_name}{suffix}.png"

    # Save the image
    result_image.save(output_file)
    print(f"Saved southwest-facing sprite to: {output_file}")
    print(f"Output size: {size_info}")
    print(f"Using palette: {palette_index}")

    return output_file

def batch_generate_theme_previews(theme_name, job_list=None):
    """Generate all preview images for a specific theme

    Args:
        theme_name: Name of the theme (e.g., 'crimson_red', 'original')
        job_list: Optional list of specific jobs to process
    """
    base_dir = r"C:\Users\ptyRa\Dev\FFTColorCustomizer"
    sprites_dir = os.path.join(base_dir, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit", f"sprites_{theme_name}")
    preview_dir = os.path.join(base_dir, "ColorMod", "Resources", "Previews")

    if not os.path.exists(sprites_dir):
        print(f"Error: Theme directory not found: {sprites_dir}")
        return False

    # Ensure preview directory exists
    os.makedirs(preview_dir, exist_ok=True)

    # Define the job mappings - using Japanese names where needed
    # Note: In FFT, chemist uses battle_item files
    # Squire doesn't have its own sprite files - we'll handle it separately
    all_jobs = {
        'chemist_male': 'battle_item_m_spr.bin',
        'chemist_female': 'battle_item_w_spr.bin',
        'knight_male': 'battle_knight_m_spr.bin',
        'knight_female': 'battle_knight_w_spr.bin',
        'monk_male': 'battle_monk_m_spr.bin',
        'monk_female': 'battle_monk_w_spr.bin',
        'white_mage_male': 'battle_siro_m_spr.bin',
        'white_mage_female': 'battle_siro_w_spr.bin',
        'black_mage_male': 'battle_kuro_m_spr.bin',
        'black_mage_female': 'battle_kuro_w_spr.bin',
        'mystic_male': 'battle_onmyo_m_spr.bin',
        'mystic_female': 'battle_onmyo_w_spr.bin',
        'time_mage_male': 'battle_toki_m_spr.bin',
        'time_mage_female': 'battle_toki_w_spr.bin',
        'orator_male': 'battle_waju_m_spr.bin',
        'orator_female': 'battle_waju_w_spr.bin',
        'mediator_male': 'battle_waju_m_spr.bin',  # Mediator is the old name for Orator
        'mediator_female': 'battle_waju_w_spr.bin',
        'summoner_male': 'battle_syou_m_spr.bin',
        'summoner_female': 'battle_syou_w_spr.bin',
        'thief_male': 'battle_thief_m_spr.bin',
        'thief_female': 'battle_thief_w_spr.bin',
        'archer_male': 'battle_yumi_m_spr.bin',
        'archer_female': 'battle_yumi_w_spr.bin',
        'geomancer_male': 'battle_fusui_m_spr.bin',
        'geomancer_female': 'battle_fusui_w_spr.bin',
        'ninja_male': 'battle_ninja_m_spr.bin',
        'ninja_female': 'battle_ninja_w_spr.bin',
        'samurai_male': 'battle_samu_m_spr.bin',
        'samurai_female': 'battle_samu_w_spr.bin',
        'dragoon_male': 'battle_ryu_m_spr.bin',
        'dragoon_female': 'battle_ryu_w_spr.bin',
        'dancer_female': 'battle_odori_w_spr.bin',
        'bard_male': 'battle_gin_m_spr.bin',
        'mime_male': 'battle_mono_m_spr.bin',
        'mime_female': 'battle_mono_w_spr.bin',
        'calculator_male': 'battle_san_m_spr.bin',
        'calculator_female': 'battle_san_w_spr.bin',
        'squire_male': 'battle_mina_m_spr.bin',  # In crimson_red, squire is in mina files
        'squire_female': 'battle_mina_w_spr.bin',

        # Story characters
        'agrias': 'battle_aguri_spr.bin',
        'agrias_knight': 'battle_kanba_spr.bin',  # Agrias in knight armor
        'orlandeau': 'battle_oru_spr.bin',
        'orlandeau_young': 'battle_goru_spr.bin',  # Young Orlandeau
        'orlandeau_old': 'battle_voru_spr.bin',  # Old Orlandeau
    }

    # Use specified job list or all jobs
    jobs_to_process = job_list if job_list else all_jobs

    success_count = 0
    for job_key, filename in all_jobs.items():
        if job_list and job_key not in job_list:
            continue

        input_path = os.path.join(sprites_dir, filename)
        output_path = os.path.join(preview_dir, f"{job_key}_{theme_name}.png")

        if not os.path.exists(input_path):
            print(f"  Skipping {job_key}: File not found")
            continue

        try:
            print(f"\n{job_key}:")
            extract_southwest_sprite(input_path, output_path, palette_index=0, preview_mode=True)
            success_count += 1
        except Exception as e:
            print(f"  Error processing {job_key}: {e}")

    print(f"\n{'='*60}")
    print(f"Generated {success_count} preview images for theme: {theme_name}")
    return success_count > 0

def main():
    """Main entry point"""
    if len(sys.argv) < 2:
        print("Usage:")
        print("  python convert_sprite_sw.py <input.bin> [output.png] [palette_index] [--preview]")
        print("  python convert_sprite_sw.py --batch <theme_name> [job1,job2,...]")
        print("\nExtracts the southwest-facing sprite for previews")
        print("\nModes:")
        print("  Single file:  Extract from a single .bin file")
        print("  Batch mode:   Generate all previews for a theme")
        print("\nParameters:")
        print("  input.bin      - Input FFT sprite file")
        print("  output.png     - Optional output filename")
        print("  palette_index  - Optional palette index (0-15, default: 0)")
        print("  --preview      - Generate 64x64 config menu preview")
        print("  --batch        - Batch generate previews for a theme")
        print("\nExamples:")
        print("  python convert_sprite_sw.py battle_knight_m_spr.bin")
        print("  python convert_sprite_sw.py battle_knight_m_spr.bin knight.png --preview")
        print("  python convert_sprite_sw.py --batch crimson_red")
        print("  python convert_sprite_sw.py --batch royal_purple knight_male,knight_female")
        sys.exit(1)

    # Check for batch mode
    if sys.argv[1] == "--batch":
        if len(sys.argv) < 3:
            print("Error: Theme name required for batch mode")
            sys.exit(1)

        theme_name = sys.argv[2]
        job_list = None

        if len(sys.argv) > 3:
            job_list = sys.argv[3].split(',')

        print(f"Batch generating previews for theme: {theme_name}")
        if job_list:
            print(f"Processing specific jobs: {', '.join(job_list)}")

        success = batch_generate_theme_previews(theme_name, job_list)
        sys.exit(0 if success else 1)

    # Single file mode
    input_file = sys.argv[1]
    output_file = None
    palette_index = 0
    preview_mode = False

    # Parse arguments
    for i, arg in enumerate(sys.argv[2:], 2):
        if arg == "--preview":
            preview_mode = True
        elif arg.isdigit():
            palette_index = int(arg)
            palette_index = max(0, min(15, palette_index))
        elif not output_file and not arg.startswith("--"):
            output_file = arg

    # Check if input file exists
    if not os.path.exists(input_file):
        print(f"Error: File '{input_file}' not found")
        sys.exit(1)

    # Convert the sprite
    try:
        output = extract_southwest_sprite(input_file, output_file, palette_index, preview_mode)
        print(f"Conversion successful!")

        # Try to open the image (Windows) - only in single file mode, not preview mode
        if not preview_mode and sys.platform == "win32":
            os.startfile(output)
        elif not preview_mode and sys.platform == "darwin":
            os.system(f"open '{output}'")
        else:
            print(f"Please open {output} to view the sprite")

    except Exception as e:
        print(f"Error converting sprite: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == "__main__":
    main()