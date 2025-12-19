#!/usr/bin/env python3
"""
Generate PNG previews for MISSING job themes only
- Chemist (male and female)
- Female variants of existing male-only jobs
"""

import os
import sys
from PIL import Image

OUTPUT_BASE_DIR = "C:/Users/ptyRa/OneDrive/Desktop/FFT_Palette_Tests/GENERIC_COMBOS"
EXISTING_THEMES_DIR = "C:/Users/ptyRa/OneDrive/Desktop/FFT_Palette_Tests"

# Jobs that already have complete theme folders (male versions exist)
EXISTING_MALE_JOBS = [
    "archer", "bard", "calculator", "dragoon", "geomancer",
    "knight", "mediator", "mime", "monk", "ninja",
    "oracle", "priest", "samurai", "squire", "summoner",
    "thief", "time_mage", "wizard"
]

# Jobs that need PNG generation (missing entirely or need female variants)
JOBS_TO_GENERATE = [
    # Chemist - completely missing
    ("chemist", True, True),  # (job_name, generate_male, generate_female)

    # Female variants for existing male-only jobs
    ("archer", False, True),
    ("knight", False, True),
    ("monk", False, True),
    ("priest", False, True),  # WhiteMage
    ("wizard", False, True),  # BlackMage
    ("thief", False, True),
    ("ninja", False, True),
    ("squire", False, True),
    ("time_mage", False, True),
    ("lancer", False, True),  # Summoner (uses syou sprites)
    ("samurai", False, True),
    ("dragoon", False, True),
    ("geomancer", False, True),
    ("oracle", False, True),  # Mystic
    ("mediator", False, True),
    ("mime", False, True),
    ("calculator", False, True),
    # Bard is male-only, Dancer is female-only (already exists)
]

def read_palette(data, palette_index=0):
    """Read a palette from sprite data"""
    palette_offset = palette_index * 32
    palette = []

    for i in range(16):
        offset = palette_offset + (i * 2)
        if offset + 1 < len(data):
            color_data = data[offset] | (data[offset + 1] << 8)

            # Extract BGR555 components
            b = ((color_data >> 10) & 0x1F) * 8
            g = ((color_data >> 5) & 0x1F) * 8
            r = (color_data & 0x1F) * 8

            # First color is usually transparent
            if i == 0:
                palette.append((r, g, b, 0))
            else:
                palette.append((r, g, b, 255))
        else:
            palette.append((0, 0, 0, 0))

    return palette

def extract_sprite(data, x_offset, y_offset, width=32, height=40):
    """Extract a sprite from the data"""
    palette = read_palette(data, 0)

    # Create image
    img = Image.new('RGBA', (width, height), (0, 0, 0, 0))
    pixels = img.load()

    # The sprite data starts at byte 512
    sprite_offset = 512
    bytes_per_row = 128

    for y in range(height):
        for x in range(width):
            # Calculate position in sprite sheet
            sheet_x = x_offset + x
            sheet_y = y_offset + y

            # Calculate byte position
            byte_pos = sprite_offset + (sheet_y * bytes_per_row) + (sheet_x // 2)

            if byte_pos < len(data):
                byte_val = data[byte_pos]

                # Each byte contains 2 pixels (4-bit each)
                if sheet_x % 2 == 0:
                    palette_idx = byte_val & 0x0F
                else:
                    palette_idx = (byte_val >> 4) & 0x0F

                if palette_idx < len(palette):
                    pixels[x, y] = palette[palette_idx]

    return img

def generate_preview_for_theme(theme_dir, job_name):
    """Generate a preview PNG for a single theme"""
    # Find the first male sprite for preview
    male_sprite = None
    female_sprite = None

    for file in os.listdir(theme_dir):
        if file.endswith('_m_spr.bin'):
            male_sprite = os.path.join(theme_dir, file)
        elif file.endswith('_w_spr.bin'):
            female_sprite = os.path.join(theme_dir, file)

    # Use male sprite if available, otherwise female
    sprite_file = male_sprite if male_sprite else female_sprite

    if not sprite_file:
        return False

    try:
        # Read sprite data
        with open(sprite_file, 'rb') as f:
            data = f.read()

        # Extract southwest-facing sprite (same as original script)
        sprite_img = extract_sprite(data, 32, 1, 32, 40)

        # Scale up 2x for better visibility
        sprite_scaled = sprite_img.resize((64, 80), Image.NEAREST)

        # Save preview PNG next to the theme folder
        preview_path = f"{theme_dir}.png"
        sprite_scaled.save(preview_path)
        return True

    except Exception as e:
        print(f"  Error generating preview: {e}")
        return False

def main():
    print("=== Generating PNG Previews for Missing Job Themes ===")
    print(f"Base directory: {OUTPUT_BASE_DIR}")
    print("=" * 60)

    total_generated = 0
    total_attempted = 0

    for job_name, gen_male, gen_female in JOBS_TO_GENERATE:
        print(f"\nProcessing {job_name}...")
        job_count = 0

        # Get all theme folders for this job
        for theme_folder in os.listdir(OUTPUT_BASE_DIR):
            if theme_folder.startswith(f"{job_name}_") and os.path.isdir(os.path.join(OUTPUT_BASE_DIR, theme_folder)):
                theme_path = os.path.join(OUTPUT_BASE_DIR, theme_folder)
                png_path = f"{theme_path}.png"

                # Skip if PNG already exists
                if os.path.exists(png_path):
                    continue

                total_attempted += 1

                # Check if this is male or female variant based on sprites inside
                has_male = any(f.endswith('_m_spr.bin') for f in os.listdir(theme_path))
                has_female = any(f.endswith('_w_spr.bin') for f in os.listdir(theme_path))

                # Generate preview based on what we need
                should_generate = False
                if gen_male and has_male:
                    should_generate = True
                if gen_female and has_female:
                    should_generate = True

                if should_generate:
                    if generate_preview_for_theme(theme_path, job_name):
                        job_count += 1
                        total_generated += 1

                        # Progress indicator every 10 themes
                        if job_count % 10 == 0:
                            print(f"  [{job_count:3d}] previews generated for {job_name}...")

        if job_count > 0:
            print(f"  Total for {job_name}: {job_count} previews")

    print("=" * 60)
    print(f"Successfully generated {total_generated}/{total_attempted} preview PNGs!")
    print(f"\nPreviews saved in: {OUTPUT_BASE_DIR}")
    print("\nYou can now review the themes and select your favorites!")

if __name__ == "__main__":
    main()