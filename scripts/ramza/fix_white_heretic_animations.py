#!/usr/bin/env python3
"""
Fix white_heretic animation sprites by applying the same color transformation
used for standing sprites.

Strategy:
1. Extract color mapping from original → white_heretic standing sprites
2. Apply that exact mapping to original animation sprites
3. Save corrected animation sprites
"""

from PIL import Image
import os
from collections import defaultdict
import numpy as np

def load_image_as_array(filepath):
    """Load image and return as numpy array."""
    img = Image.open(filepath)
    if img.mode != 'RGB':
        img = img.convert('RGB')
    return np.array(img)

def extract_color_mapping(original_img, themed_img):
    """
    Extract color mapping between two images.
    Returns dict of (r,g,b) -> (r,g,b) mappings.
    """
    color_map = {}

    # Ensure images are same size
    if original_img.shape != themed_img.shape:
        print(f"Warning: Image dimensions don't match!")
        return color_map

    height, width = original_img.shape[:2]

    for y in range(height):
        for x in range(width):
            orig_color = tuple(original_img[y, x])
            theme_color = tuple(themed_img[y, x])

            # Only map non-black pixels (black is background)
            if orig_color != (0, 0, 0):
                if orig_color in color_map:
                    # Verify consistency
                    if color_map[orig_color] != theme_color:
                        # Some pixels might have slight variations due to compression
                        # Keep the most common mapping
                        pass
                else:
                    color_map[orig_color] = theme_color

    return color_map

def apply_color_mapping(source_img, color_map, fuzzy_match=False):
    """
    Apply color mapping to an image.
    If fuzzy_match is True, finds closest color in map for colors not exactly matched.
    """
    result = np.copy(source_img)
    height, width = source_img.shape[:2]

    unmatched_colors = set()

    for y in range(height):
        for x in range(width):
            orig_color = tuple(source_img[y, x])

            if orig_color == (0, 0, 0):  # Keep black background
                continue

            if orig_color in color_map:
                result[y, x] = color_map[orig_color]
            elif fuzzy_match:
                # Find closest color in map
                closest_color = find_closest_color(orig_color, color_map.keys())
                if closest_color:
                    result[y, x] = color_map[closest_color]
                else:
                    unmatched_colors.add(orig_color)
            else:
                unmatched_colors.add(orig_color)

    if unmatched_colors:
        print(f"  Found {len(unmatched_colors)} unmatched colors")

    return result

def find_closest_color(target_color, color_list):
    """Find closest color by RGB distance."""
    if not color_list:
        return None

    min_dist = float('inf')
    closest = None

    for color in color_list:
        dist = sum((a - b) ** 2 for a, b in zip(target_color, color)) ** 0.5
        if dist < min_dist:
            min_dist = dist
            closest = color

    # Only return if reasonably close (threshold for color similarity)
    if min_dist < 50:  # Adjust threshold as needed
        return closest
    return None

def analyze_palette(img_array, name=""):
    """Analyze and report on image palette."""
    unique_colors = set()
    for row in img_array:
        for pixel in row:
            color = tuple(pixel)
            if color != (0, 0, 0):  # Exclude black background
                unique_colors.add(color)

    print(f"  {name}: {len(unique_colors)} unique colors")
    # Show sample of colors
    sample_colors = list(unique_colors)[:5]
    for color in sample_colors:
        print(f"    RGB{color}")
    return unique_colors

def fix_white_heretic_animations():
    """Main function to fix white_heretic animation sprites."""

    base_dir = r"C:\Users\ptyRa\Dev\FFTColorCustomizer\ColorMod\Images"
    original_dir = r"C:\Users\ptyRa\OneDrive\Desktop\FFT_Original_Sprites"

    chapters = [
        ("RamzaChapter1", "830_Ramuza_Ch1", "831_Ramuza_Ch1"),
        ("RamzaChapter23", "832_Ramuza_Ch23", "833_Ramuza_Ch23"),
        ("RamzaChapter4", "834_Ramuza_Ch4", "835_Ramuza_Ch4"),
    ]

    for chapter_dir, standing_name, animation_name in chapters:
        print(f"\n{'='*60}")
        print(f"Processing {chapter_dir}")
        print('='*60)

        # Load original sprites
        orig_standing_path = os.path.join(original_dir, f"{standing_name}_hd.bmp")
        orig_animation_path = os.path.join(original_dir, f"{animation_name}_hd.bmp")

        if not os.path.exists(orig_standing_path):
            print(f"ERROR: Original standing sprite not found: {orig_standing_path}")
            continue
        if not os.path.exists(orig_animation_path):
            print(f"ERROR: Original animation sprite not found: {orig_animation_path}")
            continue

        print(f"\nLoading original sprites...")
        orig_standing = load_image_as_array(orig_standing_path)
        orig_animation = load_image_as_array(orig_animation_path)

        # Load white_heretic standing sprite
        white_standing_path = os.path.join(base_dir, chapter_dir, "white_heretic", f"{standing_name}.bmp")

        if not os.path.exists(white_standing_path):
            print(f"ERROR: White heretic standing sprite not found: {white_standing_path}")
            continue

        print(f"Loading white_heretic standing sprite...")
        white_standing = load_image_as_array(white_standing_path)

        # Analyze palettes
        print(f"\nAnalyzing color palettes:")
        orig_stand_colors = analyze_palette(orig_standing, "Original standing")
        white_stand_colors = analyze_palette(white_standing, "White standing")
        orig_anim_colors = analyze_palette(orig_animation, "Original animation")

        # Extract color mapping from standing sprites
        print(f"\nExtracting color transformation...")
        color_map = extract_color_mapping(orig_standing, white_standing)
        print(f"  Mapped {len(color_map)} colors")

        # Apply mapping to animation sprite
        print(f"\nApplying color transformation to animation sprite...")
        corrected_animation = apply_color_mapping(orig_animation, color_map, fuzzy_match=True)

        # Analyze result
        corrected_colors = analyze_palette(corrected_animation, "Corrected animation")

        # Save corrected animation sprite
        output_path = os.path.join(base_dir, chapter_dir, "white_heretic", f"{animation_name}.bmp")

        print(f"\nSaving corrected animation sprite to:")
        print(f"  {output_path}")

        result_img = Image.fromarray(corrected_animation.astype('uint8'), 'RGB')
        result_img.save(output_path, 'BMP')

        print(f"[SUCCESS] Fixed {animation_name}")

    print(f"\n{'='*60}")
    print("All white_heretic animation sprites have been corrected!")
    print('='*60)

def verify_crimson_blade():
    """Verify that crimson_blade has consistent colors between standing and animation."""
    print("\n" + "="*60)
    print("VERIFYING CRIMSON_BLADE COLOR CONSISTENCY")
    print("="*60)

    base_dir = r"C:\Users\ptyRa\Dev\FFTColorCustomizer\ColorMod\Images"

    chapters = [
        ("RamzaChapter4", "834_Ramuza_Ch4", "835_Ramuza_Ch4"),
    ]

    for chapter_dir, standing_name, animation_name in chapters:
        standing_path = os.path.join(base_dir, chapter_dir, "crimson_blade", f"{standing_name}.bmp")
        animation_path = os.path.join(base_dir, chapter_dir, "crimson_blade", f"{animation_name}.bmp")

        if os.path.exists(standing_path) and os.path.exists(animation_path):
            print(f"\nAnalyzing {chapter_dir} crimson_blade:")
            standing_img = load_image_as_array(standing_path)
            animation_img = load_image_as_array(animation_path)

            stand_colors = analyze_palette(standing_img, "Standing")
            anim_colors = analyze_palette(animation_img, "Animation")

            # Check color overlap
            overlap = stand_colors & anim_colors
            print(f"  Color overlap: {len(overlap)} colors in common")
            print(f"  Standing unique: {len(stand_colors - anim_colors)} colors")
            print(f"  Animation unique: {len(anim_colors - stand_colors)} colors")

if __name__ == "__main__":
    # First verify how crimson_blade handles colors
    verify_crimson_blade()

    # Then fix white_heretic
    fix_white_heretic_animations()