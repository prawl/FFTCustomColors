#!/usr/bin/env python3
"""Convert white_heretic PNG files to BMP format for FFT Sprite Toolkit."""

from PIL import Image
import os

toolkit_dir = r"C:\Users\ptyRa\AppData\Local\FFTSpriteToolkit\working\extracted_sprites"

# List of files to convert
files = [
    ("830_Ramuza_Ch1_hd.png", "830_Ramuza_Ch1_hd.bmp"),
    ("831_Ramuza_Ch1_hd.png", "831_Ramuza_Ch1_hd.bmp"),
    ("832_Ramuza_Ch23_hd.png", "832_Ramuza_Ch23_hd.bmp"),
    ("833_Ramuza_Ch23_hd.png", "833_Ramuza_Ch23_hd.bmp"),
    ("834_Ramuza_Ch4_hd.png", "834_Ramuza_Ch4_hd.bmp"),
    ("835_Ramuza_Ch4_hd.png", "835_Ramuza_Ch4_hd.bmp"),
]

for png_name, bmp_name in files:
    png_path = os.path.join(toolkit_dir, png_name)
    bmp_path = os.path.join(toolkit_dir, bmp_name)

    if os.path.exists(png_path):
        print(f"Converting {png_name} to {bmp_name}")
        img = Image.open(png_path)
        # Convert to RGB if necessary (remove alpha channel)
        if img.mode == 'RGBA':
            # Create a white background
            background = Image.new('RGB', img.size, (0, 0, 0))
            background.paste(img, mask=img.split()[3] if len(img.split()) > 3 else None)
            img = background
        elif img.mode != 'RGB':
            img = img.convert('RGB')

        img.save(bmp_path, 'BMP')
        print(f"  Saved {bmp_name}")
    else:
        print(f"  WARNING: {png_name} not found")

print("\nConversion complete!")