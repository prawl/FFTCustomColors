#!/usr/bin/env python3
"""
FFT Sprite Converter - Extract all 8 directional views from a sprite
Generates preview images for each facing direction
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

def extract_sprite_direction(data, direction, palette_index=0):
    """Extract a specific directional sprite from FFT sprite sheet

    FFT sprite layout (256px wide):
    Row 1: W, SW, S, SE, E, NE, N, NW (indices 0-7)
    Each sprite is approximately 32x40 pixels

    Args:
        data: Raw sprite data
        direction: Direction name ('s', 'sw', 'w', 'nw', 'n', 'ne', 'e', 'se')
        palette_index: Which palette to use (0-15)
    """

    # Direction to sprite index mapping (corrected based on visual inspection)
    # Note: FFT sprite sheets only have 5 true directional sprites
    # Indices 5 and 7 are animation frames, not directions
    direction_map = {
        'w': 0,    # West (facing left) - index 0 - CORRECT
        'sw': 1,   # Southwest - index 1 - CORRECT
        's': 2,    # South (facing down) - index 2 - CORRECT
        'nw': 3,   # Northwest - index 3 - CORRECT
        'n': 4,    # North (facing up/away) - index 4 - CORRECT
        'ne': 3,   # Northeast - mirror NW (no true NE sprite)
        'e': 6,    # East (facing right) - index 6 - CORRECT
        'se': 1    # Southeast - mirror SW (no true SE sprite)
    }

    if direction.lower() not in direction_map:
        raise ValueError(f"Invalid direction: {direction}")

    sprite_index = direction_map[direction.lower()]

    # Skip palette data (512 bytes)
    sprite_data = data[512:]

    # Read the specified palette and make background transparent
    palette = read_palette(data, palette_index)
    palette[0] = (0, 0, 0, 0)  # Make first color (background) transparent

    # Sprite dimensions and positions
    sprite_width = 32
    sprite_height = 40  # Extract extra pixels to capture full sprite
    sheet_width = 256

    # Calculate position based on sprite index
    x_offset = sprite_index * 32  # Each sprite is 32 pixels wide
    y_offset = 0  # First row

    # Create image array for the sprite
    image = np.zeros((sprite_height, sprite_width, 4), dtype=np.uint8)

    # Extract the sprite pixel by pixel
    for y in range(sprite_height):
        for x in range(sprite_width):
            # Calculate position in the full sprite sheet
            sheet_x = x_offset + x
            sheet_y = y_offset + y

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

    return Image.fromarray(image, 'RGBA')

def extract_all_directions(input_file, output_dir=None, palette_index=0):
    """Extract all 8 directional sprites from a sprite file

    Args:
        input_file: Path to the sprite .bin file
        output_dir: Directory to save the extracted sprites
        palette_index: Which palette to use (0-15)
    """

    # Read the binary file
    with open(input_file, 'rb') as f:
        data = f.read()

    print(f"Processing: {os.path.basename(input_file)}")
    print(f"File size: {len(data)} bytes")

    # Create output directory
    if output_dir is None:
        base_name = os.path.splitext(input_file)[0]
        output_dir = f"{base_name}_directions"

    os.makedirs(output_dir, exist_ok=True)

    # All 8 directions
    directions = ['s', 'sw', 'w', 'nw', 'n', 'ne', 'e', 'se']
    direction_names = {
        's': 'south',
        'sw': 'southwest',
        'w': 'west',
        'nw': 'northwest',
        'n': 'north',
        'ne': 'northeast',
        'e': 'east',
        'se': 'southeast'
    }

    extracted_files = []

    for direction in directions:
        try:
            # Extract the sprite
            sprite_img = extract_sprite_direction(data, direction, palette_index)

            # Create 64x64 preview for config menu
            preview_img = Image.new('RGBA', (64, 64), (0, 0, 0, 0))

            # Scale 2x width, but keep aspect ratio for height (32x40 -> 64x80)
            scaled_sprite = sprite_img.resize((64, 80), Image.NEAREST)

            # Mirror sprites to create missing directions
            if direction == 'e':
                # Get the west sprite and mirror it horizontally
                west_sprite_img = extract_sprite_direction(data, 'w', palette_index)
                scaled_sprite = west_sprite_img.resize((64, 80), Image.NEAREST)
                scaled_sprite = scaled_sprite.transpose(Image.FLIP_LEFT_RIGHT)
            elif direction == 'ne':
                # Get the northwest sprite and mirror it horizontally
                nw_sprite_img = extract_sprite_direction(data, 'nw', palette_index)
                scaled_sprite = nw_sprite_img.resize((64, 80), Image.NEAREST)
                scaled_sprite = scaled_sprite.transpose(Image.FLIP_LEFT_RIGHT)
            elif direction == 'se':
                # Get the southwest sprite and mirror it horizontally
                sw_sprite_img = extract_sprite_direction(data, 'sw', palette_index)
                scaled_sprite = sw_sprite_img.resize((64, 80), Image.NEAREST)
                scaled_sprite = scaled_sprite.transpose(Image.FLIP_LEFT_RIGHT)

            # Paste into 64x64 frame, positioned to align with original sprites
            preview_img.paste(scaled_sprite, (0, -8), scaled_sprite)

            # Save the preview
            output_file = os.path.join(output_dir, f"squire_male_{direction_names[direction]}.png")
            preview_img.save(output_file)
            extracted_files.append(output_file)

            print(f"  Extracted {direction_names[direction].upper()}: {output_file}")

        except Exception as e:
            print(f"  Error extracting {direction}: {e}")

    print(f"\nExtracted {len(extracted_files)} directional sprites to: {output_dir}")
    return extracted_files

def main():
    """Extract all directions for male squire sprite"""

    # Path to the male squire sprite
    base_dir = r"C:\Users\ptyRa\Dev\FFTColorCustomizer"
    sprite_file = os.path.join(
        base_dir,
        "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit",
        "sprites_original", "battle_mina_m_spr.bin"
    )

    # Output directory for the extracted sprites
    output_dir = os.path.join(base_dir, "ColorMod", "Resources", "Previews", "squire_male_directions")

    if not os.path.exists(sprite_file):
        print(f"Error: Sprite file not found: {sprite_file}")
        sys.exit(1)

    print("Extracting all 8 directional views for male squire...")
    print("="*60)

    try:
        extracted = extract_all_directions(sprite_file, output_dir, palette_index=0)

        if extracted:
            print("\nSuccess! All directions extracted.")
            print(f"Preview images saved to: {output_dir}")

            # Open the directory in Windows Explorer
            if sys.platform == "win32":
                os.startfile(output_dir)
        else:
            print("\nNo sprites were extracted successfully.")

    except Exception as e:
        print(f"Error: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == "__main__":
    main()