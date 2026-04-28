"""
Render a sprite from a BIN file to a PNG image for visual inspection.
Uses the same rendering logic as BinSpriteExtractor.cs.

Usage:
    python render_sprite_preview.py input.bin output.png
    python render_sprite_preview.py input.bin  # outputs to input_preview.png
"""

import sys
import os
from PIL import Image

# Sprite sheet parameters (matching BinSpriteExtractor.cs)
SPRITE_WIDTH = 32
SPRITE_HEIGHT = 40
SHEET_WIDTH = 256
DISPLAY_SCALE = 3  # Scale 3x for better visibility


def read_palette(data, palette_index=0):
    """Read a 16-color palette from BIN data (BGR555 format)."""
    palette = []
    offset = palette_index * 32  # Each palette is 16 colors * 2 bytes

    for i in range(16):
        color_offset = offset + (i * 2)

        # Read BGR555 color (2 bytes, little-endian)
        bgr555 = data[color_offset] | (data[color_offset + 1] << 8)

        # First color is transparent
        if i == 0:
            palette.append((0, 0, 0, 0))  # RGBA with alpha=0
            continue

        # Extract 5-bit components
        r5 = bgr555 & 0x1F
        g5 = (bgr555 >> 5) & 0x1F
        b5 = (bgr555 >> 10) & 0x1F

        # Convert 5-bit to 8-bit
        r8 = (r5 * 255) // 31
        g8 = (g5 * 255) // 31
        b8 = (b5 * 255) // 31

        palette.append((r8, g8, b8, 255))

    return palette


def extract_sprite(data, sprite_index, palette):
    """Extract a single sprite from the BIN data."""
    img = Image.new('RGBA', (SPRITE_WIDTH, SPRITE_HEIGHT))
    pixels = img.load()

    # Sprites are arranged horizontally
    x_offset = sprite_index * SPRITE_WIDTH
    y_offset = 0

    # Skip palette data (512 bytes)
    sprite_data_start = 512

    for y in range(SPRITE_HEIGHT):
        for x in range(SPRITE_WIDTH):
            # Calculate position in the full sprite sheet
            sheet_x = x_offset + x
            sheet_y = y_offset + y

            # Calculate pixel index in sprite data (256-pixel wide sheet)
            pixel_index = (sheet_y * SHEET_WIDTH) + sheet_x
            byte_index = sprite_data_start + (pixel_index // 2)

            if byte_index >= len(data):
                break

            pixel_data = data[byte_index]

            # Get 4-bit value (alternate between low and high nibble)
            if pixel_index % 2 == 0:
                color_index = pixel_data & 0x0F  # Low nibble
            else:
                color_index = (pixel_data >> 4) & 0x0F  # High nibble

            pixels[x, y] = palette[color_index]

    return img


def render_all_directions(data, palette_index=0):
    """Render all 8 directions in a single image."""
    palette = read_palette(data, palette_index)

    # Extract the 5 base sprites from the sheet
    sprites = {}
    sprites['W'] = extract_sprite(data, 0, palette)   # Position 0: West
    sprites['SW'] = extract_sprite(data, 1, palette)  # Position 1: Southwest
    sprites['S'] = extract_sprite(data, 2, palette)   # Position 2: South
    sprites['NW'] = extract_sprite(data, 3, palette)  # Position 3: Northwest
    sprites['N'] = extract_sprite(data, 4, palette)   # Position 4: North

    # Mirror to create East directions
    sprites['E'] = sprites['W'].transpose(Image.FLIP_LEFT_RIGHT)
    sprites['NE'] = sprites['NW'].transpose(Image.FLIP_LEFT_RIGHT)
    sprites['SE'] = sprites['SW'].transpose(Image.FLIP_LEFT_RIGHT)

    # Arrange in a grid:
    # Row 1: NW, N, NE
    # Row 2: W, [center], E
    # Row 3: SW, S, SE

    grid_width = SPRITE_WIDTH * 3
    grid_height = SPRITE_HEIGHT * 3

    result = Image.new('RGBA', (grid_width, grid_height), (64, 64, 64, 255))

    # Paste sprites in compass arrangement
    result.paste(sprites['NW'], (0, 0))
    result.paste(sprites['N'], (SPRITE_WIDTH, 0))
    result.paste(sprites['NE'], (SPRITE_WIDTH * 2, 0))

    result.paste(sprites['W'], (0, SPRITE_HEIGHT))
    # Center is empty
    result.paste(sprites['E'], (SPRITE_WIDTH * 2, SPRITE_HEIGHT))

    result.paste(sprites['SW'], (0, SPRITE_HEIGHT * 2))
    result.paste(sprites['S'], (SPRITE_WIDTH, SPRITE_HEIGHT * 2))
    result.paste(sprites['SE'], (SPRITE_WIDTH * 2, SPRITE_HEIGHT * 2))

    # Scale up for better visibility
    scaled_width = grid_width * DISPLAY_SCALE
    scaled_height = grid_height * DISPLAY_SCALE
    result = result.resize((scaled_width, scaled_height), Image.NEAREST)

    return result


def render_single_sprite(data, sprite_index=2, palette_index=0):
    """Render a single sprite (default: South-facing)."""
    palette = read_palette(data, palette_index)
    sprite = extract_sprite(data, sprite_index, palette)

    # Scale up for better visibility
    scaled_width = SPRITE_WIDTH * DISPLAY_SCALE
    scaled_height = SPRITE_HEIGHT * DISPLAY_SCALE
    return sprite.resize((scaled_width, scaled_height), Image.NEAREST)


def render_indexed_frames(data, palette_index=0, num_frames=8, scale=3):
    """Render the first N sprite frames side-by-side with index labels.

    Useful for figuring out the layout of non-standard sprites: the user
    can visually identify which sprite_index corresponds to each pose.
    """
    from PIL import ImageDraw

    palette = read_palette(data, palette_index)
    frame_w = SPRITE_WIDTH * scale
    frame_h = SPRITE_HEIGHT * scale
    label_h = 16
    cell_h = frame_h + label_h

    grid = Image.new('RGBA', (frame_w * num_frames, cell_h), (32, 32, 32, 255))
    draw = ImageDraw.Draw(grid)

    for i in range(num_frames):
        frame = extract_sprite(data, i, palette)
        frame_scaled = frame.resize((frame_w, frame_h), Image.NEAREST)
        grid.paste(frame_scaled, (i * frame_w, label_h))
        # Label "0", "1", "2", ... centered above each frame
        draw.text((i * frame_w + frame_w // 2 - 4, 2), str(i), fill=(255, 255, 255, 255))

    return grid


def render_full_sheet(data, palette_index=0, scale=2):
    """Render the entire sprite sheet at SHEET_WIDTH pixels wide.

    Useful for inspecting non-standard layouts (e.g. Construct 8 / tetsu)
    where the standard 32x40 frame assumption doesn't apply.
    """
    palette = read_palette(data, palette_index)

    sprite_data_start = 512
    pixel_bytes = len(data) - sprite_data_start
    if pixel_bytes <= 0:
        raise ValueError("File too small — no pixel data after palette block.")

    total_pixels = pixel_bytes * 2  # 4 bits per pixel
    height = total_pixels // SHEET_WIDTH

    img = Image.new('RGBA', (SHEET_WIDTH, height), (64, 64, 64, 255))
    pixels = img.load()

    for y in range(height):
        for x in range(SHEET_WIDTH):
            pixel_index = (y * SHEET_WIDTH) + x
            byte_index = sprite_data_start + (pixel_index // 2)

            if byte_index >= len(data):
                break

            pixel_data = data[byte_index]
            if pixel_index % 2 == 0:
                color_index = pixel_data & 0x0F
            else:
                color_index = (pixel_data >> 4) & 0x0F

            pixels[x, y] = palette[color_index]

    if scale != 1:
        img = img.resize((SHEET_WIDTH * scale, height * scale), Image.NEAREST)

    return img


def main():
    if len(sys.argv) < 2:
        print("Sprite Preview Renderer for FFT BIN files")
        print("=" * 50)
        print("\nUsage:")
        print("  python render_sprite_preview.py <input.bin> [output.png]")
        print("\nOptions:")
        print("  --all     Render all 8 directions in a grid (default)")
        print("  --south   Render only the south-facing sprite")
        print("  --full    Render the entire 256px-wide sheet (for non-standard layouts)")
        print("\nExamples:")
        print("  python render_sprite_preview.py squire_m_diagnostic.bin")
        print("  python render_sprite_preview.py squire_m_diagnostic.bin preview.png")
        print("  python render_sprite_preview.py battle_tetsu_spr.bin --full")
        sys.exit(1)

    input_path = sys.argv[1]

    # Parse options
    render_full = '--full' in sys.argv
    render_frames = '--frames' in sys.argv
    render_all = not render_full and not render_frames and '--south' not in sys.argv

    # Determine output path
    if len(sys.argv) >= 3 and not sys.argv[2].startswith('--'):
        output_path = sys.argv[2]
    else:
        base, _ = os.path.splitext(input_path)
        if render_full:
            suffix = '_full'
        elif render_frames:
            suffix = '_frames'
        elif render_all:
            suffix = '_all'
        else:
            suffix = '_south'
        output_path = f"{base}{suffix}.png"

    if not os.path.exists(input_path):
        print(f"Error: Input file not found: {input_path}")
        sys.exit(1)

    print(f"Input:  {input_path}")
    print(f"Output: {output_path}")

    with open(input_path, 'rb') as f:
        data = f.read()

    if render_full:
        print(f"Rendering full sheet ({SHEET_WIDTH}px wide, height auto-calculated from data size)...")
        result = render_full_sheet(data)
    elif render_frames:
        print("Rendering sprite frames 0-7 side-by-side with index labels...")
        result = render_indexed_frames(data)
    elif render_all:
        print("Rendering all 8 directions...")
        result = render_all_directions(data)
    else:
        print("Rendering south-facing sprite...")
        result = render_single_sprite(data, sprite_index=2)

    result.save(output_path)
    print(f"Saved: {output_path} ({result.size[0]}x{result.size[1]})")


if __name__ == "__main__":
    main()
