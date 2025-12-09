#!/usr/bin/env python3
"""
Generic sprite theme generator for FFT Color Mod.
Creates new color themes by transforming existing sprite palettes.
"""

import argparse
import os
import shutil
import struct
import colorsys
import json
from pathlib import Path
from typing import List, Tuple, Dict, Optional
import re

class SpriteThemeGenerator:
    """Generates new sprite themes by transforming color palettes."""

    PALETTE_SIZE = 512  # First 512 bytes contain the color palettes
    COLOR_COUNT = 256   # 256 colors total (16 palettes × 16 colors)

    def __init__(self, base_dir: Path = Path(".")):
        """Initialize the theme generator."""
        self.base_dir = base_dir
        self.sprite_dir = base_dir / "ColorMod" / "FFTIVC" / "data" / "enhanced" / "fftpack" / "unit"

    def read_palette(self, sprite_path: Path) -> List[Tuple[int, int, int]]:
        """Read the color palette from a sprite file."""
        colors = []
        with open(sprite_path, 'rb') as f:
            palette_data = f.read(self.PALETTE_SIZE)

        # Parse 16-bit colors (XBBBBBGGGGGRRRRR format)
        for i in range(0, self.PALETTE_SIZE, 2):
            if i + 1 < len(palette_data):
                # Read 16-bit color value
                color_value = struct.unpack('<H', palette_data[i:i+2])[0]

                # Extract 5-bit BGR components
                r = (color_value & 0x001F) << 3  # Red: bits 0-4
                g = ((color_value & 0x03E0) >> 5) << 3  # Green: bits 5-9
                b = ((color_value & 0x7C00) >> 10) << 3  # Blue: bits 10-14

                # Convert to 8-bit values (0-255)
                colors.append((r, g, b))

        return colors

    def write_palette(self, sprite_path: Path, colors: List[Tuple[int, int, int]]) -> None:
        """Write a color palette to a sprite file."""
        # Read the entire sprite file
        with open(sprite_path, 'rb') as f:
            sprite_data = bytearray(f.read())

        # Convert colors back to 16-bit format and write
        for i, (r, g, b) in enumerate(colors[:self.COLOR_COUNT]):
            if i * 2 + 1 < self.PALETTE_SIZE:
                # Convert 8-bit RGB to 5-bit BGR
                r5 = (r >> 3) & 0x1F
                g5 = (g >> 3) & 0x1F
                b5 = (b >> 3) & 0x1F

                # Pack into 16-bit value (XBBBBBGGGGGRRRRR)
                color_value = r5 | (g5 << 5) | (b5 << 10)

                # Write to sprite data
                struct.pack_into('<H', sprite_data, i * 2, color_value)

        # Write back the modified sprite
        with open(sprite_path, 'wb') as f:
            f.write(sprite_data)

    def is_skin_tone(self, r: int, g: int, b: int) -> bool:
        """Detect if a color is likely a skin tone."""
        # Convert to HSV for better skin detection
        h, s, v = colorsys.rgb_to_hsv(r/255.0, g/255.0, b/255.0)
        h_degrees = h * 360

        # Skin tones typically fall in these ranges:
        # - Hue: 0-50 degrees (reds to oranges/yellows)
        # - Saturation: 10-60% (not too gray, not too vivid)
        # - Value: 25-95% (not too dark, not pure white)

        # Check if it's in the skin tone hue range
        is_skin_hue = (0 <= h_degrees <= 50) or (340 <= h_degrees <= 360)
        is_skin_saturation = 0.10 <= s <= 0.60
        is_skin_value = 0.25 <= v <= 0.95

        # Also check RGB ratios typical of skin
        # Skin usually has R > G > B
        has_skin_rgb_pattern = r > g and g > b and (r - b) > 15

        return (is_skin_hue and is_skin_saturation and is_skin_value) or has_skin_rgb_pattern

    def detect_clothing_item(self, r: int, g: int, b: int, item_type: str) -> bool:
        """Detect if a color belongs to a specific clothing item."""
        h, s, v = colorsys.rgb_to_hsv(r/255.0, g/255.0, b/255.0)
        h_degrees = h * 360

        # Skip if it's skin tone
        if self.is_skin_tone(r, g, b):
            return False

        # Item-specific color patterns
        if item_type in ["boots", "shoes", "footwear"]:
            # Boots are the darkest browns in FFT sprites (based on palette analysis)
            # Index 39: RGB(88,80,72), Index 13: RGB(104,72,32), Index 61: RGB(112,72,32)
            is_boot_dark_brown = (20 <= h_degrees <= 35) and (s > 0.15) and (0.30 < v < 0.50)
            is_boot_very_dark = (s < 0.20) and (v < 0.40) and (r > 70)  # Dark grayish-browns
            # Specific pattern: RGB values close together but slightly brown-tinted
            is_leather_pattern = (abs(r - g) < 30) and (abs(g - b) < 30) and (r >= g >= b) and (v < 0.45)
            return is_boot_dark_brown or is_boot_very_dark or is_leather_pattern

        elif item_type in ["gloves", "gauntlets", "hands"]:
            # Gloves are medium browns (lighter than boots but not skin)
            # Index 11: RGB(152,96,56), Index 59: RGB(152,96,48)
            is_glove_brown = (20 <= h_degrees <= 35) and (0.50 < s < 0.75) and (0.50 < v < 0.65)
            # Also check for tan/lighter brown variants
            is_glove_tan = (20 <= h_degrees <= 35) and (0.60 < s < 0.70) and (0.55 < v < 0.70)
            # Pattern: Clear brown with good separation between RGB channels
            is_medium_leather = (r > g > b) and (r - b > 40) and (0.50 < v < 0.70)
            # Exclude skin tones
            not_skin = not self.is_skin_tone(r, g, b)
            return (is_glove_brown or is_glove_tan or is_medium_leather) and not_skin

        elif item_type in ["belt", "belts", "strap", "straps"]:
            # Belts are usually darker browns or blacks
            is_belt_brown = (15 <= h_degrees <= 35) and (s > 0.2) and (0.15 < v < 0.45)
            is_belt_black = (s < 0.2) and (v < 0.35) and (v > 0.1)
            return is_belt_brown or is_belt_black

        elif item_type in ["armor", "chestplate", "breastplate"]:
            # Armor is typically metallic - grays, silvers, or the main color theme
            is_metallic = (s < 0.3) and (0.3 < v < 0.8)
            is_colored_armor = (s > 0.3) and (v > 0.4)  # Non-metallic colored armor
            return is_metallic or is_colored_armor

        elif item_type in ["cape", "cloak", "cloth"]:
            # Capes have more saturated colors, often the accent color
            is_fabric = (s > 0.2) and (v > 0.3)
            not_leather = not ((15 <= h_degrees <= 45) and (v < 0.4))
            return is_fabric and not_leather

        elif item_type in ["hair"]:
            # Hair colors - browns, blacks, blondes, but not skin
            is_hair_brown = (10 <= h_degrees <= 40) and (s > 0.15) and (0.2 < v < 0.6)
            is_hair_black = (s < 0.2) and (v < 0.25)
            is_hair_blonde = (35 <= h_degrees <= 55) and (s < 0.5) and (v > 0.5)
            return is_hair_brown or is_hair_black or is_hair_blonde

        return False

    def transform_clothing_items(self, colors: List[Tuple[int, int, int]],
                                items: List[str], target_color: Tuple[int, int, int],
                                preserve_shading: bool = True) -> List[Tuple[int, int, int]]:
        """Transform specific clothing items to a target color."""
        result_colors = []
        target_h, target_s, target_v = colorsys.rgb_to_hsv(target_color[0]/255, target_color[1]/255, target_color[2]/255)

        for r, g, b in colors:
            # Check if this color matches any of the target items
            is_target_item = any(self.detect_clothing_item(r, g, b, item) for item in items)

            if is_target_item:
                if preserve_shading:
                    # Preserve the relative brightness while changing color
                    orig_h, orig_s, orig_v = colorsys.rgb_to_hsv(r/255, g/255, b/255)

                    # Keep relative brightness but use target hue/saturation
                    new_h = target_h
                    new_s = target_s
                    # Scale the target brightness by the original brightness ratio
                    new_v = target_v * (orig_v / 0.35) if orig_v > 0 else target_v

                    # Clamp values
                    new_v = min(1.0, max(0, new_v))

                    r_new, g_new, b_new = colorsys.hsv_to_rgb(new_h, new_s, new_v)
                    result_colors.append((
                        int(r_new * 255),
                        int(g_new * 255),
                        int(b_new * 255)
                    ))
                else:
                    # Direct color replacement
                    result_colors.append(target_color)
            else:
                # Keep original color
                result_colors.append((r, g, b))

        return result_colors

    def hue_shift(self, colors: List[Tuple[int, int, int]], shift_degrees: float, preserve_skin: bool = True) -> List[Tuple[int, int, int]]:
        """Shift the hue of all colors by a given number of degrees."""
        shifted_colors = []
        shift_fraction = shift_degrees / 360.0

        for r, g, b in colors:
            # Skip pure black/white/gray
            if r == g == b:
                shifted_colors.append((r, g, b))
                continue

            # Preserve skin tones if requested
            if preserve_skin and self.is_skin_tone(r, g, b):
                shifted_colors.append((r, g, b))
                continue

            # Convert to HSV
            h, s, v = colorsys.rgb_to_hsv(r/255.0, g/255.0, b/255.0)

            # Shift hue
            h = (h + shift_fraction) % 1.0

            # Convert back to RGB
            r_new, g_new, b_new = colorsys.hsv_to_rgb(h, s, v)
            shifted_colors.append((
                int(r_new * 255),
                int(g_new * 255),
                int(b_new * 255)
            ))

        return shifted_colors

    def adjust_hsv(self, colors: List[Tuple[int, int, int]],
                   hue_shift: float = 0, saturation_delta: float = 0,
                   brightness_delta: float = 0, preserve_skin: bool = True) -> List[Tuple[int, int, int]]:
        """Adjust HSV values of all colors."""
        adjusted_colors = []

        for r, g, b in colors:
            # Preserve skin tones if requested
            if preserve_skin and self.is_skin_tone(r, g, b):
                adjusted_colors.append((r, g, b))
                continue

            # Convert to HSV
            h, s, v = colorsys.rgb_to_hsv(r/255.0, g/255.0, b/255.0)

            # Apply adjustments
            h = (h + hue_shift / 360.0) % 1.0
            s = max(0, min(1, s + saturation_delta / 100.0))
            v = max(0, min(1, v + brightness_delta / 100.0))

            # Convert back to RGB
            r_new, g_new, b_new = colorsys.hsv_to_rgb(h, s, v)
            adjusted_colors.append((
                int(r_new * 255),
                int(g_new * 255),
                int(b_new * 255)
            ))

        return adjusted_colors

    def map_colors(self, colors: List[Tuple[int, int, int]],
                   color_map: Dict[str, str]) -> List[Tuple[int, int, int]]:
        """Map specific colors to new colors."""
        mapped_colors = []

        # Parse color map (hex strings to RGB tuples)
        rgb_map = {}
        for src_hex, dst_hex in color_map.items():
            src_rgb = self.hex_to_rgb(src_hex)
            dst_rgb = self.hex_to_rgb(dst_hex)
            rgb_map[src_rgb] = dst_rgb

        for color in colors:
            # Check if this exact color should be mapped
            if color in rgb_map:
                mapped_colors.append(rgb_map[color])
            else:
                # Check for close matches (within tolerance)
                replaced = False
                for src_color, dst_color in rgb_map.items():
                    if self.colors_similar(color, src_color, tolerance=30):
                        mapped_colors.append(dst_color)
                        replaced = True
                        break

                if not replaced:
                    mapped_colors.append(color)

        return mapped_colors

    def replace_color_range(self, colors: List[Tuple[int, int, int]],
                           source_color: Tuple[int, int, int],
                           target_color: Tuple[int, int, int],
                           tolerance: int = 50) -> List[Tuple[int, int, int]]:
        """Replace colors within a range of a source color with variations of target color."""
        result_colors = []

        # Convert source and target to HSV for better range matching
        src_h, src_s, src_v = colorsys.rgb_to_hsv(source_color[0]/255, source_color[1]/255, source_color[2]/255)
        tgt_h, tgt_s, tgt_v = colorsys.rgb_to_hsv(target_color[0]/255, target_color[1]/255, target_color[2]/255)

        for r, g, b in colors:
            # Check if color is within range of source color
            if self.colors_similar((r, g, b), source_color, tolerance):
                # Calculate how different this color is from source
                h, s, v = colorsys.rgb_to_hsv(r/255, g/255, b/255)

                # Apply the same relative difference to target color
                v_ratio = v / src_v if src_v > 0 else 1
                s_ratio = s / src_s if src_s > 0 else 1

                new_h = tgt_h
                new_s = min(1.0, tgt_s * s_ratio)
                new_v = min(1.0, tgt_v * v_ratio)

                # Convert back to RGB
                r_new, g_new, b_new = colorsys.hsv_to_rgb(new_h, new_s, new_v)
                result_colors.append((
                    int(r_new * 255),
                    int(g_new * 255),
                    int(b_new * 255)
                ))
            else:
                result_colors.append((r, g, b))

        return result_colors

    def colors_similar(self, c1: Tuple[int, int, int], c2: Tuple[int, int, int], tolerance: int) -> bool:
        """Check if two colors are similar within a tolerance."""
        return (abs(c1[0] - c2[0]) <= tolerance and
                abs(c1[1] - c2[1]) <= tolerance and
                abs(c1[2] - c2[2]) <= tolerance)

    def hex_to_rgb(self, hex_color: str) -> Tuple[int, int, int]:
        """Convert hex color string to RGB tuple."""
        hex_color = hex_color.lstrip('#')
        return tuple(int(hex_color[i:i+2], 16) for i in (0, 2, 4))

    def create_theme(self, source_theme: str, target_theme: str,
                    transformation: str, **kwargs) -> None:
        """Create a new theme by transforming an existing one."""
        # Create source and target directories
        source_dir = self.sprite_dir / f"sprites_{source_theme}"
        target_dir = self.sprite_dir / f"sprites_{target_theme}"

        if not source_dir.exists():
            raise ValueError(f"Source theme directory does not exist: {source_dir}")

        # Create target directory
        target_dir.mkdir(parents=True, exist_ok=True)

        # Process each sprite file
        sprite_files = list(source_dir.glob("*.bin"))
        print(f"Processing {len(sprite_files)} sprite files...")

        for sprite_file in sprite_files:
            print(f"  Processing {sprite_file.name}...")

            # Copy sprite to target directory
            target_sprite = target_dir / sprite_file.name
            shutil.copy2(sprite_file, target_sprite)

            # Read palette
            colors = self.read_palette(target_sprite)

            # Apply transformation
            if transformation == "hue_shift":
                colors = self.hue_shift(colors, kwargs.get('degrees', 0),
                                       kwargs.get('preserve_skin', True))
            elif transformation == "hsv_adjust":
                colors = self.adjust_hsv(colors,
                                        kwargs.get('hue', 0),
                                        kwargs.get('saturation', 0),
                                        kwargs.get('brightness', 0),
                                        kwargs.get('preserve_skin', True))
            elif transformation == "color_map":
                colors = self.map_colors(colors, kwargs.get('mappings', {}))
            elif transformation == "color_range":
                for replacement in kwargs.get('replacements', []):
                    colors = self.replace_color_range(colors,
                                                     replacement['source'],
                                                     replacement['target'],
                                                     replacement.get('tolerance', 50))
            elif transformation == "clothing_items":
                colors = self.transform_clothing_items(colors,
                                                      kwargs.get('items', []),
                                                      kwargs.get('target_color', (0, 0, 0)),
                                                      kwargs.get('preserve_shading', True))
            else:
                raise ValueError(f"Unknown transformation: {transformation}")

            # Write modified palette
            self.write_palette(target_sprite, colors)

        print(f"Theme '{target_theme}' created successfully in {target_dir}")

def main():
    parser = argparse.ArgumentParser(description="Generate new sprite color themes for FFT Color Mod")
    parser.add_argument("--source", required=True, help="Source theme to base transformation on")
    parser.add_argument("--name", required=True, help="Name for the new theme")

    # Transformation methods
    parser.add_argument("--hue-shift", type=float, help="Shift hue by degrees (-180 to 180)")
    parser.add_argument("--brightness", type=float, help="Adjust brightness (-100 to 100)")
    parser.add_argument("--saturation", type=float, help="Adjust saturation (-100 to 100)")
    parser.add_argument("--map", action="append", help="Map colors (format: #SOURCE:#TARGET)")
    parser.add_argument("--palette", help="JSON file with color mappings")
    parser.add_argument("--replace-range", action="append", help="Replace color range (format: #SOURCE:#TARGET:TOLERANCE)")

    # Clothing item transformation
    parser.add_argument("--transform-items", help="Transform specific clothing items (comma-separated: boots,gloves,belt,armor,cape,hair)")
    parser.add_argument("--item-color", help="Target color for item transformation (hex format: #000000 for black)")

    # Options
    parser.add_argument("--no-preserve-skin", action="store_true", help="Don't preserve skin tones (by default, skin is preserved)")
    parser.add_argument("--no-preserve-shading", action="store_true", help="Don't preserve shading on transformed items")

    args = parser.parse_args()

    generator = SpriteThemeGenerator()
    preserve_skin = not args.no_preserve_skin

    # Determine transformation type and parameters
    if args.hue_shift is not None:
        generator.create_theme(args.source, args.name, "hue_shift",
                             degrees=args.hue_shift, preserve_skin=preserve_skin)

    elif args.brightness is not None or args.saturation is not None:
        generator.create_theme(args.source, args.name, "hsv_adjust",
                             hue=0,
                             saturation=args.saturation or 0,
                             brightness=args.brightness or 0,
                             preserve_skin=preserve_skin)

    elif args.map:
        # Parse color mappings
        mappings = {}
        for mapping in args.map:
            src, dst = mapping.split(':')
            mappings[src] = dst
        generator.create_theme(args.source, args.name, "color_map", mappings=mappings)

    elif args.palette:
        # Load palette from JSON file
        with open(args.palette, 'r') as f:
            palette_data = json.load(f)

        if 'mappings' in palette_data:
            generator.create_theme(args.source, args.name, "color_map", mappings=palette_data['mappings'])
        elif 'replacements' in palette_data:
            # Process color range replacements
            replacements = []
            for repl in palette_data['replacements']:
                replacements.append({
                    'source': generator.hex_to_rgb(repl['source']),
                    'target': generator.hex_to_rgb(repl['target']),
                    'tolerance': repl.get('tolerance', 50)
                })
            generator.create_theme(args.source, args.name, "color_range", replacements=replacements)

    elif args.replace_range:
        # Parse color range replacements
        replacements = []
        for replacement in args.replace_range:
            parts = replacement.split(':')
            if len(parts) >= 2:
                replacements.append({
                    'source': generator.hex_to_rgb(parts[0]),
                    'target': generator.hex_to_rgb(parts[1]),
                    'tolerance': int(parts[2]) if len(parts) > 2 else 50
                })
        generator.create_theme(args.source, args.name, "color_range", replacements=replacements)

    elif args.transform_items and args.item_color:
        # Transform specific clothing items
        items = [item.strip().lower() for item in args.transform_items.split(',')]
        target_color = generator.hex_to_rgb(args.item_color)
        preserve_shading = not args.no_preserve_shading

        generator.create_theme(args.source, args.name, "clothing_items",
                             items=items,
                             target_color=target_color,
                             preserve_shading=preserve_shading)

    else:
        print("Error: Must specify a transformation method (--hue-shift, --brightness, --saturation, --map, --palette, --replace-range, or --transform-items with --item-color)")
        return 1

    return 0

if __name__ == "__main__":
    exit(main())