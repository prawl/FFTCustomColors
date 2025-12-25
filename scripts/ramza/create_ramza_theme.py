#!/usr/bin/env python3
"""
Create custom Ramza themes with preserved hair/face colors.
Supports creating cohesive themes across all chapters.
"""

from PIL import Image
import os
from typing import Dict, Tuple

class RamzaThemeCreator:
    def __init__(self):
        self.input_dir = "C:/Users/ptyRa/AppData/Local/FFTSpriteToolkit/working/extracted_sprites"
        self.output_base = "C:/Users/ptyRa/Dev/FFTColorCustomizer/scripts/ramza/themes"

        # Define original colors for each chapter
        self.ch1_colors = {
            'armor': [  # Blues - Main armor
                (48, 72, 104), (56, 96, 136), (40, 56, 80), (80, 128, 184)
            ],
            # Only gloves/legs/feet accessories - NOT hair colors
            'accessories': [
                # Leave empty for now - we need to identify which golds are NOT hair
            ],
            # Hair/face browns AND golds that are part of hair - KEEP ORIGINAL
            'hair_skin': [
                (72, 48, 40), (104, 72, 24), (104, 64, 32),
                (160, 104, 40), (144, 80, 40),
                # These golds are actually hair/skin, not accessories
                (216, 160, 72), (200, 136, 80), (232, 192, 128)
            ]
        }

        self.ch2_colors = {
            'armor': [  # Purples - Main armor
                (48, 40, 80), (88, 64, 120), (128, 96, 200)
            ],
            'accents': [  # Other Browns - Will become red accents
                (72, 40, 8), (112, 88, 24)
            ],
            # Hair/face browns - KEEP ORIGINAL
            'hair_skin': [
                (216, 160, 72), (200, 136, 80), (232, 192, 128),
                (176, 160, 136), (184, 120, 40), (72, 64, 48)
            ]
        }

        self.ch34_colors = {
            'armor': [  # Teals - Shoulders/arms/hands/legs/boots
                (32, 64, 88), (40, 96, 120), (64, 136, 152)
            ],
            'accents': [
                # Only the dark gray accent - not the light gray (eyes)
                (40, 40, 32),  # Dark gray - make it a darker black
            ],
            # Hair/face browns - KEEP ORIGINAL (includes all browns)
            'hair_skin': [
                (64, 56, 56), (72, 40, 8), (104, 72, 24),
                (112, 96, 80), (128, 56, 8),
                # These browns are also part of hair/face
                (184, 120, 40), (200, 136, 80), (232, 192, 128),
                (176, 160, 136), (216, 160, 72),
                # Keep the light gray for eyes
                (224, 224, 216)
            ]
        }

        # Only process main files (not alt) for preview
        self.ramza_files = [
            ("830_Ramuza_Ch1_hd.bmp", "Chapter 1", 'ch1'),
            ("832_Ramuza_Ch23_hd.bmp", "Chapter 2", 'ch2'),  # File says Ch23 but it's just Ch2
            ("834_Ramuza_Ch4_hd.bmp", "Chapter 3/4", 'ch34'),  # File says Ch4 but it's Ch3/4
        ]

        # All files including alternates
        self.all_ramza_files = [
            ("830_Ramuza_Ch1_hd.bmp", "Chapter 1", 'ch1'),
            ("831_Ramuza_Ch1_hd.bmp", "Chapter 1 (alt)", 'ch1'),
            ("832_Ramuza_Ch23_hd.bmp", "Chapter 2", 'ch2'),
            ("833_Ramuza_Ch23_hd.bmp", "Chapter 2 (alt)", 'ch2'),
            ("834_Ramuza_Ch4_hd.bmp", "Chapter 3/4", 'ch34'),
            ("835_Ramuza_Ch4_hd.bmp", "Chapter 3/4 (alt)", 'ch34'),
        ]

    def create_theme(self, theme_name: str,
                    armor_color: Tuple[int, int, int],
                    accent_color: Tuple[int, int, int] = None,
                    preserve_hair: bool = True,
                    preview_only: bool = True):
        """
        Create a custom Ramza theme.

        Args:
            theme_name: Name of the theme (e.g., "dark_knight")
            armor_color: RGB color for the main armor
            accent_color: RGB color for accents (optional)
            preserve_hair: Keep original hair/face colors (default True)
            preview_only: Only process main files, not alts (default True)
        """

        output_dir = os.path.join(self.output_base, theme_name)
        os.makedirs(output_dir, exist_ok=True)

        # Generate variations for shading
        armor_variations = self._generate_color_variations(armor_color, 4)
        accent_variations = self._generate_color_variations(accent_color, 3) if accent_color else None

        print("=" * 60)
        print(f"CREATING {theme_name.upper()} THEME")
        print("=" * 60)
        print(f"  Armor: RGB{armor_color}")
        if accent_color:
            print(f"  Accents: RGB{accent_color}")
        print(f"  Hair/Face: {'Original colors preserved' if preserve_hair else 'Will be modified'}")
        print("-" * 50)

        # Choose which files to process
        files_to_process = self.ramza_files if preview_only else self.all_ramza_files

        for filename, chapter_name, chapter_type in files_to_process:
            self._process_sprite(filename, chapter_name, chapter_type,
                               armor_variations, accent_variations,
                               output_dir, theme_name, preserve_hair)

        print("\n" + "=" * 60)
        print(f"{theme_name.upper()} THEME CREATED!")
        print("=" * 60)
        print(f"\nTheme saved to: {output_dir}")

    def create_dark_knight_theme(self):
        """Create the Dark Knight theme specifically."""
        self.create_theme(
            theme_name="dark_knight",
            armor_color=(40, 40, 50),
            accent_color=(30, 30, 35),
            preserve_hair=True,
            preview_only=True
        )

    def _process_sprite(self, filename, chapter_name, chapter_type,
                       armor_variations, accent_variations,
                       output_dir, theme_name, preserve_hair):
        """Process a single sprite file."""

        input_path = os.path.join(self.input_dir, filename)
        output_filename = filename.replace('.bmp', f'_{theme_name}.png')
        output_path = os.path.join(output_dir, output_filename)

        if not os.path.exists(input_path):
            print(f"Skipping {filename} - file not found")
            return

        print(f"\nProcessing {chapter_name}: {filename}")

        # Build color map based on chapter
        color_map = {}

        if chapter_type == 'ch1':
            colors = self.ch1_colors
            # Map armor colors to armor variations
            for i, color in enumerate(colors['armor']):
                color_map[color] = armor_variations[min(i, len(armor_variations)-1)]

        elif chapter_type == 'ch2':
            colors = self.ch2_colors
            # Map armor colors to armor variations
            for i, color in enumerate(colors['armor']):
                color_map[color] = armor_variations[min(i, len(armor_variations)-1)]
            # Map accents if provided
            if accent_variations and 'accents' in colors:
                for i, color in enumerate(colors['accents']):
                    color_map[color] = accent_variations[min(i, len(accent_variations)-1)]

        elif chapter_type == 'ch34':
            colors = self.ch34_colors
            # Map armor colors to armor variations
            for i, color in enumerate(colors['armor']):
                color_map[color] = armor_variations[min(i, len(armor_variations)-1)]
            # Map accents if provided
            if accent_variations and 'accents' in colors:
                for i, color in enumerate(colors['accents']):
                    color_map[color] = accent_variations[min(i, len(accent_variations)-1)]

        # Apply the color map to the image
        img = Image.open(input_path)
        if img.mode != 'RGBA':
            img = img.convert('RGBA')

        pixels = img.load()
        width, height = img.size
        changed_pixels = 0
        preserved_pixels = 0

        for y in range(height):
            for x in range(width):
                r, g, b, a = pixels[x, y]
                old_color = (r, g, b)

                # Check if this is a hair/face color to preserve
                is_hair_face = False
                if preserve_hair:
                    if chapter_type == 'ch1':
                        is_hair_face = old_color in self.ch1_colors['hair_skin']
                    elif chapter_type == 'ch2':
                        is_hair_face = old_color in self.ch2_colors['hair_skin']
                    elif chapter_type == 'ch34':
                        is_hair_face = old_color in self.ch34_colors['hair_skin']

                if is_hair_face and preserve_hair:
                    preserved_pixels += 1
                elif old_color in color_map:
                    new_color = color_map[old_color]
                    pixels[x, y] = (*new_color, a)
                    changed_pixels += 1

        img.save(output_path)
        print(f"  Modified: {changed_pixels} pixels (armor/accents)")
        print(f"  Preserved: {preserved_pixels} pixels (hair/face)")
        print(f"  Saved to: {output_filename}")

    def _generate_color_variations(self, base_color: Tuple[int, int, int],
                                  count: int) -> list:
        """Generate darker and lighter variations of a color for shading."""
        variations = []
        r, g, b = base_color

        # Generate from darkest to lightest
        for i in range(count):
            factor = 0.4 + (0.8 * (i / (count - 1))) if count > 1 else 1.0

            # For very dark colors, add brightness instead of just multiply
            if r < 50 and g < 50 and b < 50:
                new_r = min(255, int(r + (50 * factor)))
                new_g = min(255, int(g + (50 * factor)))
                new_b = min(255, int(b + (50 * factor)))
            else:
                new_r = min(255, int(r * factor))
                new_g = min(255, int(g * factor))
                new_b = min(255, int(b * factor))

            variations.append((new_r, new_g, new_b))

        return variations


if __name__ == "__main__":
    creator = RamzaThemeCreator()

    # Create Dark Knight theme with ALL files for sprite toolkit
    creator.create_theme(
        theme_name="dark_knight_full",
        armor_color=(40, 40, 50),
        accent_color=(30, 30, 35),
        preserve_hair=True,
        preview_only=False  # Process ALL files including alts
    )