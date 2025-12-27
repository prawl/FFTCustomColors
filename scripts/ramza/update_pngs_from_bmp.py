#!/usr/bin/env python3
"""Update PNG files from BMP files in toolkit directory."""

from PIL import Image
import os

toolkit_dir = r"C:\Users\ptyRa\AppData\Local\FFTSpriteToolkit\working\extracted_sprites"

# List of files to convert
files = [
    "830_Ramuza_Ch1_hd",
    "831_Ramuza_Ch1_hd",
    "832_Ramuza_Ch23_hd",
    "833_Ramuza_Ch23_hd",
    "834_Ramuza_Ch4_hd",
    "835_Ramuza_Ch4_hd",
]

for base_name in files:
    bmp_path = os.path.join(toolkit_dir, f"{base_name}.bmp")
    png_path = os.path.join(toolkit_dir, f"{base_name}.png")

    if os.path.exists(bmp_path):
        print(f"Converting {base_name}.bmp to PNG")
        img = Image.open(bmp_path)
        img.save(png_path, 'PNG')
        print(f"  Saved {base_name}.png")
    else:
        print(f"  WARNING: {base_name}.bmp not found")

print("\nPNG update complete!")