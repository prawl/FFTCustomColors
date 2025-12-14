#!/usr/bin/env python3
"""
Create a detailed color test preview for Cloud with a legend
"""

import sys
import os
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

# Add parent directory to path
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from convert_sprite_sw import extract_southwest_sprite

def create_detailed_preview():
    """Create a detailed preview with legend"""

    base_dir = Path(__file__).parent.parent.parent
    test_sprite = base_dir / "ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_cloud_test/battle_cloud_spr.bin"

    if not test_sprite.exists():
        print(f"Error: Test sprite not found at {test_sprite}")
        return

    # Create output directory
    output_dir = base_dir / "ColorMod/Resources/Previews/Cloud_Test"
    output_dir.mkdir(exist_ok=True, parents=True)

    # Extract at different scales
    print("Creating previews at different scales...")

    # Large detailed view (8x scale)
    large_output = output_dir / "cloud_test_large.png"
    extract_southwest_sprite(str(test_sprite), str(large_output), palette_index=0, preview_mode=False)

    # Config preview size (64x64)
    small_output = output_dir / "cloud_test_preview.png"
    extract_southwest_sprite(str(test_sprite), str(small_output), palette_index=0, preview_mode=True)

    # Create a combined image with legend
    create_legend_image(output_dir)

    print(f"\nCreated previews in: {output_dir}")
    print("\nFiles created:")
    print("  - cloud_test_large.png (256x320, detailed view)")
    print("  - cloud_test_preview.png (64x64, config size)")
    print("  - cloud_test_with_legend.png (combined with color key)")

    # Open the folder
    if sys.platform == "win32":
        os.startfile(str(output_dir))

def create_legend_image(output_dir):
    """Create an image with the sprite and color legend"""

    # Load the large sprite
    large_sprite = Image.open(output_dir / "cloud_test_large.png")

    # Create a new image with space for legend
    width = 600
    height = 400
    img = Image.new('RGBA', (width, height), (32, 32, 32, 255))
    draw = ImageDraw.Draw(img)

    # Paste the sprite
    img.paste(large_sprite, (20, 40), large_sprite)

    # Add title
    draw.text((20, 10), "Cloud Color Index Test", fill=(255, 255, 255))

    # Add legend
    legend_x = 300
    legend_y = 60

    colors_and_labels = [
        ((255, 0, 0), "RED: Indices 3-5 (Buckles/Trim)"),
        ((0, 255, 0), "GREEN: Indices 6-9 (Primary Armor/Cape)"),
        ((0, 0, 255), "BLUE: Indices 20-31 (Secondary Armor)"),
        ((255, 255, 0), "YELLOW: Indices 35-47 (Extended Armor)"),
        ((255, 0, 255), "MAGENTA: Indices 51-62 (Additional Armor)"),
        ((200, 200, 200), "UNCHANGED: Indices 10-19 (Hair - Preserved)"),
    ]

    for i, (color, label) in enumerate(colors_and_labels):
        y = legend_y + (i * 35)

        # Draw color box
        draw.rectangle([legend_x, y, legend_x + 25, y + 25], fill=color, outline=(255, 255, 255))

        # Draw label
        draw.text((legend_x + 35, y + 5), label, fill=(255, 255, 255))

    # Add note at bottom
    note_y = legend_y + (len(colors_and_labels) * 35) + 20
    draw.text((legend_x, note_y), "Note: Index 44 skipped (problematic)", fill=(150, 150, 150))

    # Save combined image
    img.save(output_dir / "cloud_test_with_legend.png")

if __name__ == "__main__":
    create_detailed_preview()