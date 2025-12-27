#!/usr/bin/env python3
"""Convert all white_heretic PNG files to BMP format in the repository."""

from PIL import Image
import os
import glob

base_dir = r"C:\Users\ptyRa\Dev\FFTColorCustomizer\ColorMod\Images"

# Find all white_heretic PNG files
white_heretic_dirs = [
    os.path.join(base_dir, "RamzaChapter1", "white_heretic"),
    os.path.join(base_dir, "RamzaChapter23", "white_heretic"),
    os.path.join(base_dir, "RamzaChapter4", "white_heretic"),
]

for directory in white_heretic_dirs:
    if not os.path.exists(directory):
        print(f"Directory not found: {directory}")
        continue

    png_files = glob.glob(os.path.join(directory, "*.png"))

    for png_path in png_files:
        bmp_path = png_path.replace('.png', '.bmp')

        print(f"Converting {os.path.basename(png_path)} to BMP...")
        img = Image.open(png_path)

        # Convert to RGB if necessary (remove alpha channel)
        if img.mode == 'RGBA':
            # Create a black background (matching sprite sheet standard)
            background = Image.new('RGB', img.size, (0, 0, 0))
            background.paste(img, mask=img.split()[3] if len(img.split()) > 3 else None)
            img = background
        elif img.mode != 'RGB':
            img = img.convert('RGB')

        img.save(bmp_path, 'BMP')
        print(f"  Created {os.path.basename(bmp_path)}")

        # Remove the original PNG file
        os.remove(png_path)
        print(f"  Removed {os.path.basename(png_path)}")

print("\nConversion complete! All white_heretic PNGs have been replaced with BMPs.")