#!/usr/bin/env python3
"""
Fix dark_knight animation sprites by applying the same color transformation
used for standing sprites. Currently the animation sprites are just copies
of the standing sprites, but they should be the animation frames with
dark_knight colors applied.
"""

from PIL import Image
import os
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

    if original_img.shape != themed_img.shape:
        print(f"Warning: Image dimensions don't match!")
        return color_map

    height, width = original_img.shape[:2]

    for y in range(height):
        for x in range(width):
            orig_color = tuple(original_img[y, x])
            theme_color = tuple(themed_img[y, x])

            if orig_color != (0, 0, 0):  # Skip black background
                color_map[orig_color] = theme_color

    return color_map

def apply_color_mapping(source_img, color_map, fuzzy_match=False):
    """Apply color mapping to an image."""
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

    # Only return if reasonably close
    if min_dist < 50:
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
    return unique_colors

def fix_dark_knight_animations():
    """Create proper dark_knight animation sprites."""

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

        # Load dark_knight standing sprite
        dark_standing_path = os.path.join(base_dir, chapter_dir, "dark_knight", f"{standing_name}.bmp")

        if not os.path.exists(dark_standing_path):
            print(f"ERROR: Dark knight standing sprite not found: {dark_standing_path}")
            continue

        print(f"Loading dark_knight standing sprite...")
        dark_standing = load_image_as_array(dark_standing_path)

        # Analyze palettes
        print(f"\nAnalyzing color palettes:")
        orig_stand_colors = analyze_palette(orig_standing, "Original standing")
        dark_stand_colors = analyze_palette(dark_standing, "Dark knight standing")
        orig_anim_colors = analyze_palette(orig_animation, "Original animation")

        # Extract color mapping from standing sprites
        print(f"\nExtracting color transformation...")
        color_map = extract_color_mapping(orig_standing, dark_standing)
        print(f"  Mapped {len(color_map)} colors from original to dark knight")

        # Apply mapping to animation sprite
        print(f"\nApplying dark knight colors to animation sprite...")
        corrected_animation = apply_color_mapping(orig_animation, color_map, fuzzy_match=True)

        # Analyze result
        corrected_colors = analyze_palette(corrected_animation, "Corrected animation")

        # Save corrected animation sprite
        output_path = os.path.join(base_dir, chapter_dir, "dark_knight", f"{animation_name}.bmp")

        print(f"\nSaving corrected animation sprite to:")
        print(f"  {output_path}")

        result_img = Image.fromarray(corrected_animation.astype('uint8'), 'RGB')
        result_img.save(output_path, 'BMP')

        print(f"[SUCCESS] Created proper dark_knight animation sprite: {animation_name}")

    print(f"\n{'='*60}")
    print("All dark_knight animation sprites have been properly generated!")
    print('='*60)

if __name__ == "__main__":
    fix_dark_knight_animations()