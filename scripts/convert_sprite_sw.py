#!/usr/bin/env python3
"""
FFT Sprite Converter - Extract only southwest-facing sprite
Extracts just the SW facing sprite from the sprite sheet
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

def extract_southwest_sprite(input_file, output_file=None, palette_index=0):
    """Extract just the southwest-facing sprite from FFT sprite sheet"""

    # Read the binary file
    with open(input_file, 'rb') as f:
        data = f.read()

    print(f"Processing: {os.path.basename(input_file)}")
    print(f"File size: {len(data)} bytes")

    # Skip palette data (512 bytes)
    sprite_data = data[512:]

    # Read the specified palette
    palette = read_palette(data, palette_index)

    # FFT sprites layout:
    # - Width: 256 pixels
    # - Each sprite is roughly 32x32 pixels
    # - 8 directions in first row: S, SW, W, NW, N, NE, E, SE
    # - Southwest is the 2nd sprite (index 1), so x offset = 32 pixels

    sprite_width = 32
    sprite_height = 32
    sheet_width = 256

    # Southwest sprite position
    x_offset = 32  # Second sprite in the row (index 1)
    y_offset = 0   # First row

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

            if byte_index < len(sprite_data):
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
                    image[y, x] = (0, 0, 0, 255)  # Black for invalid indices
            else:
                # If we run out of data, use transparent
                image[y, x] = (0, 0, 0, 0)

    # Convert to PIL image
    pil_image = Image.fromarray(image, 'RGBA')

    # Scale up for better visibility (8x scale)
    scale_factor = 8
    scaled_width = sprite_width * scale_factor
    scaled_height = sprite_height * scale_factor
    scaled_image = pil_image.resize((scaled_width, scaled_height), Image.NEAREST)

    # Generate output filename if not provided
    if output_file is None:
        base_name = os.path.splitext(input_file)[0]
        output_file = f"{base_name}_sw.png"

    # Save the image
    scaled_image.save(output_file)
    print(f"Saved southwest-facing sprite to: {output_file}")
    print(f"Original size: {sprite_width}x{sprite_height}")
    print(f"Scaled size: {scaled_width}x{scaled_height} ({scale_factor}x)")
    print(f"Using palette: {palette_index}")

    return output_file

def main():
    """Main entry point"""
    if len(sys.argv) < 2:
        print("Usage: python convert_sprite_sw.py <input.bin> [output.png] [palette_index]")
        print("\nExtracts only the southwest-facing sprite")
        print("\nParameters:")
        print("  input.bin      - Input FFT sprite file")
        print("  output.png     - Optional output filename")
        print("  palette_index  - Optional palette index (0-15, default: 0)")
        print("\nExample:")
        print("  python convert_sprite_sw.py battle_oru_spr.bin")
        print("  python convert_sprite_sw.py battle_oru_spr.bin oru_sw.png 2")
        sys.exit(1)

    input_file = sys.argv[1]
    output_file = sys.argv[2] if len(sys.argv) > 2 and not sys.argv[2].isdigit() else None

    # Check for palette index
    palette_index = 0
    if len(sys.argv) > 2 and sys.argv[-1].isdigit():
        palette_index = int(sys.argv[-1])
        palette_index = max(0, min(15, palette_index))  # Clamp to 0-15

    # Check if input file exists
    if not os.path.exists(input_file):
        print(f"Error: File '{input_file}' not found")
        sys.exit(1)

    # Convert the sprite
    try:
        output = extract_southwest_sprite(input_file, output_file, palette_index)
        print(f"Conversion successful!")

        # Try to open the image (Windows)
        if sys.platform == "win32":
            os.startfile(output)
        elif sys.platform == "darwin":
            os.system(f"open '{output}'")
        else:
            print(f"Please open {output} to view the southwest-facing sprite")

    except Exception as e:
        print(f"Error converting sprite: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == "__main__":
    main()