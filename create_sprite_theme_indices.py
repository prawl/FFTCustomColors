#!/usr/bin/env python3
"""
Create sprite themes by modifying specific palette indices.
This is more precise than color-based detection.
"""

import argparse
import os
import shutil
import struct
import colorsys
from pathlib import Path
from typing import List, Tuple, Dict, Set

class IndexBasedThemeGenerator:
    """Generate themes by targeting specific palette indices."""

    PALETTE_SIZE = 512  # First 512 bytes contain the color palettes
    COLOR_COUNT = 256   # 256 colors total

    # Known palette index mappings for different sprite parts
    # CONFIRMED TESTING 2024-12-09:
    # NEVER USE indices 10-19 (ALL affect hair, including 13 which was mistaken for boots)
    # WORKING indices verified from existing themes: 3-9, 20-31, 35-47 (skip 44), 51-62
    ITEM_INDICES = {
        # CONFIRMED WORKING - These indices change visible armor WITHOUT affecting hair
        # Based on analysis of existing working themes (corpse_brigade, lucavi, etc.)
        "armor_all": [3, 4, 5, 6, 7, 8, 9, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31,
                     35, 36, 37, 38, 39, 40, 41, 42, 43, 45, 46, 47,
                     51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62],

        # Grouped by ranges for targeted changes
        "armor_primary": [3, 4, 5, 6, 7, 8, 9],  # Primary armor colors
        "armor_secondary": [20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31],  # Secondary armor
        "armor_tertiary": [35, 36, 37, 38, 39, 40, 41, 42, 43, 45, 46, 47],  # Additional elements
        "armor_extended": [51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62],  # Extended palette

        # Legacy names for compatibility
        "generic_armor": [3, 4, 5, 6, 7, 8, 9, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31,
                         35, 36, 37, 38, 39, 40, 41, 42, 43, 45, 46, 47,
                         51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62],
    }

    def __init__(self, base_dir: Path = Path(".")):
        """Initialize the generator."""
        self.base_dir = base_dir
        self.sprite_dir = base_dir / "ColorMod" / "FFTIVC" / "data" / "enhanced" / "fftpack" / "unit"

    def read_sprite(self, sprite_path: Path) -> bytearray:
        """Read entire sprite file."""
        with open(sprite_path, 'rb') as f:
            return bytearray(f.read())

    def write_sprite(self, sprite_path: Path, data: bytearray) -> None:
        """Write sprite data to file."""
        with open(sprite_path, 'wb') as f:
            f.write(data)

    def get_color_at_index(self, sprite_data: bytearray, index: int) -> Tuple[int, int, int]:
        """Get RGB color at a specific palette index."""
        if index >= self.COLOR_COUNT or index * 2 + 1 >= self.PALETTE_SIZE:
            return (0, 0, 0)

        offset = index * 2
        color_value = struct.unpack('<H', sprite_data[offset:offset+2])[0]

        r = (color_value & 0x001F) << 3
        g = ((color_value & 0x03E0) >> 5) << 3
        b = ((color_value & 0x7C00) >> 10) << 3

        return (r, g, b)

    def set_color_at_index(self, sprite_data: bytearray, index: int, color: Tuple[int, int, int]) -> None:
        """Set RGB color at a specific palette index."""
        if index >= self.COLOR_COUNT or index * 2 + 1 >= self.PALETTE_SIZE:
            return

        r, g, b = color
        # Convert 8-bit RGB to 5-bit BGR
        r5 = (r >> 3) & 0x1F
        g5 = (g >> 3) & 0x1F
        b5 = (b >> 3) & 0x1F

        # Pack into 16-bit value
        color_value = r5 | (g5 << 5) | (b5 << 10)

        # Write to sprite data
        offset = index * 2
        struct.pack_into('<H', sprite_data, offset, color_value)

    def transform_indices(self, sprite_data: bytearray, indices: Set[int],
                         target_color: Tuple[int, int, int],
                         preserve_shading: bool = True) -> bytearray:
        """Transform specific palette indices to a target color."""
        result = sprite_data.copy()

        if not preserve_shading:
            # Direct replacement
            for idx in indices:
                self.set_color_at_index(result, idx, target_color)
        else:
            # Preserve relative brightness
            target_h, target_s, target_v = colorsys.rgb_to_hsv(
                target_color[0]/255, target_color[1]/255, target_color[2]/255
            )

            # Get brightness range of original colors
            brightnesses = []
            for idx in indices:
                r, g, b = self.get_color_at_index(sprite_data, idx)
                _, _, v = colorsys.rgb_to_hsv(r/255, g/255, b/255)
                brightnesses.append((idx, v))

            if brightnesses:
                min_v = min(v for _, v in brightnesses)
                max_v = max(v for _, v in brightnesses)
                v_range = max_v - min_v if max_v > min_v else 0.1

                for idx, orig_v in brightnesses:
                    # Scale the target brightness based on original
                    if v_range > 0:
                        # Map original brightness range to target
                        relative_pos = (orig_v - min_v) / v_range
                        # Create darker/lighter versions of target color
                        new_v = target_v * (0.3 + 0.7 * relative_pos)
                    else:
                        new_v = target_v

                    # Generate new color with preserved relative brightness
                    r_new, g_new, b_new = colorsys.hsv_to_rgb(target_h, target_s, new_v)
                    new_color = (
                        int(r_new * 255),
                        int(g_new * 255),
                        int(b_new * 255)
                    )
                    self.set_color_at_index(result, idx, new_color)

        return result

    def create_theme_with_indices(self, source_theme: str, target_theme: str,
                                 index_sets: Dict[str, Set[int]],
                                 color_map: Dict[str, Tuple[int, int, int]],
                                 preserve_shading: bool = True) -> None:
        """Create a theme by modifying specific indices."""
        source_dir = self.sprite_dir / f"sprites_{source_theme}"
        target_dir = self.sprite_dir / f"sprites_{target_theme}"

        if not source_dir.exists():
            raise ValueError(f"Source theme directory does not exist: {source_dir}")

        target_dir.mkdir(parents=True, exist_ok=True)

        sprite_files = list(source_dir.glob("*.bin"))
        print(f"Processing {len(sprite_files)} sprite files...")

        for sprite_file in sprite_files:
            print(f"  Processing {sprite_file.name}...")

            # Read sprite data
            sprite_data = self.read_sprite(sprite_file)

            # Apply transformations for each index set
            for set_name, indices in index_sets.items():
                if set_name in color_map:
                    sprite_data = self.transform_indices(
                        sprite_data, indices, color_map[set_name], preserve_shading
                    )

            # Write to target
            target_sprite = target_dir / sprite_file.name
            self.write_sprite(target_sprite, sprite_data)

        print(f"Theme '{target_theme}' created successfully in {target_dir}")

    def analyze_sprite_indices(self, sprite_path: Path, indices: List[int]) -> None:
        """Analyze specific indices in a sprite to see their colors."""
        sprite_data = self.read_sprite(sprite_path)

        print(f"\nAnalyzing indices in {sprite_path.name}:")
        print("-" * 60)

        for idx in indices:
            r, g, b = self.get_color_at_index(sprite_data, idx)
            h, s, v = colorsys.rgb_to_hsv(r/255, g/255, b/255)
            print(f"  Index {idx:3d}: RGB({r:3d}, {g:3d}, {b:3d}) = #{r:02X}{g:02X}{b:02X}")
            print(f"             HSV(H:{h*360:3.0f}°, S:{s:.2f}, V:{v:.2f})")

def main():
    parser = argparse.ArgumentParser(description="Generate sprite themes using palette indices")
    parser.add_argument("--source", help="Source theme name")
    parser.add_argument("--name", help="New theme name")

    # Transformation options
    parser.add_argument("--boots-color", help="Color for boots (hex format: #000000)")
    parser.add_argument("--gloves-color", help="Color for gloves (hex format: #000000)")
    parser.add_argument("--belt-color", help="Color for belt (hex format: #000000)")
    parser.add_argument("--custom-indices", help="Custom indices to transform (comma-separated)")
    parser.add_argument("--custom-color", help="Color for custom indices (hex format)")

    # Analysis mode
    parser.add_argument("--analyze", help="Analyze a sprite file")
    parser.add_argument("--analyze-indices", help="Indices to analyze (comma-separated)")

    # Options
    parser.add_argument("--no-preserve-shading", action="store_true", help="Don't preserve shading")

    args = parser.parse_args()
    generator = IndexBasedThemeGenerator()

    if args.analyze and args.analyze_indices:
        # Analysis mode
        sprite_path = Path(args.analyze)
        indices = [int(i) for i in args.analyze_indices.split(',')]
        generator.analyze_sprite_indices(sprite_path, indices)
        return 0

    # Theme creation mode
    index_sets = {}
    color_map = {}

    def hex_to_rgb(hex_color: str) -> Tuple[int, int, int]:
        hex_color = hex_color.lstrip('#')
        return tuple(int(hex_color[i:i+2], 16) for i in (0, 2, 4))

    # Build index sets based on arguments
    if args.boots_color:
        index_sets["boots"] = set(generator.ITEM_INDICES["generic_boots"])
        color_map["boots"] = hex_to_rgb(args.boots_color)

    if args.gloves_color:
        index_sets["gloves"] = set(generator.ITEM_INDICES["generic_gloves"])
        color_map["gloves"] = hex_to_rgb(args.gloves_color)

    if args.belt_color:
        index_sets["belt"] = set(generator.ITEM_INDICES["generic_belt"])
        color_map["belt"] = hex_to_rgb(args.belt_color)

    if args.custom_indices and args.custom_color:
        indices = set(int(i) for i in args.custom_indices.split(','))
        index_sets["custom"] = indices
        color_map["custom"] = hex_to_rgb(args.custom_color)

    if not index_sets:
        print("Error: No transformations specified")
        return 1

    if not args.source or not args.name:
        print("Error: --source and --name are required for theme creation")
        return 1

    generator.create_theme_with_indices(
        args.source, args.name, index_sets, color_map,
        preserve_shading=not args.no_preserve_shading
    )

    return 0

if __name__ == "__main__":
    exit(main())