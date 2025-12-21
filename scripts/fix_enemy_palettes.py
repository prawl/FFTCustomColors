#!/usr/bin/env python3
"""
Fix enemy palettes (5-7) in FFT sprite files.
These palettes are black (all zeros) in the original files, causing enemies to appear black.
This script populates them with appropriate enemy color variations.
"""

import argparse
import os
import shutil
import struct
from pathlib import Path
from typing import List, Tuple

class EnemyPaletteFixer:
    """Fix black enemy palettes in FFT sprite files."""

    PALETTE_SIZE = 512  # First 512 bytes contain the color palettes
    COLORS_PER_PALETTE = 16
    BYTES_PER_COLOR = 2

    def __init__(self, base_dir: Path = Path(".")):
        """Initialize the fixer."""
        self.base_dir = base_dir
        self.sprite_dir = base_dir / "ColorMod" / "FFTIVC" / "data" / "enhanced" / "fftpack" / "unit"

    def read_sprite(self, sprite_path: Path) -> bytearray:
        """Read entire sprite file."""
        with open(sprite_path, 'rb') as f:
            return bytearray(f.read())

    def write_sprite(self, sprite_path: Path, data: bytearray) -> None:
        """Write sprite data to file."""
        sprite_path.parent.mkdir(parents=True, exist_ok=True)
        with open(sprite_path, 'wb') as f:
            f.write(data)

    def get_palette(self, sprite_data: bytearray, palette_index: int) -> List[Tuple[int, int, int]]:
        """Extract a palette (16 colors) from sprite data."""
        colors = []
        start_offset = palette_index * self.COLORS_PER_PALETTE * self.BYTES_PER_COLOR

        for i in range(self.COLORS_PER_PALETTE):
            offset = start_offset + (i * self.BYTES_PER_COLOR)
            if offset + 1 < len(sprite_data):
                color_value = struct.unpack('<H', sprite_data[offset:offset+2])[0]

                # Extract RGB from 16-bit color (5 bits per channel)
                r = (color_value & 0x001F) << 3
                g = ((color_value & 0x03E0) >> 5) << 3
                b = ((color_value & 0x7C00) >> 10) << 3

                colors.append((r, g, b))
            else:
                colors.append((0, 0, 0))

        return colors

    def set_palette(self, sprite_data: bytearray, palette_index: int, colors: List[Tuple[int, int, int]]) -> None:
        """Set a palette (16 colors) in sprite data."""
        start_offset = palette_index * self.COLORS_PER_PALETTE * self.BYTES_PER_COLOR

        for i, (r, g, b) in enumerate(colors[:self.COLORS_PER_PALETTE]):
            # Convert 8-bit RGB to 5-bit BGR for FFT format
            r5 = (r >> 3) & 0x1F
            g5 = (g >> 3) & 0x1F
            b5 = (b >> 3) & 0x1F

            # Pack into 16-bit value (XBBBBBGGGGGRRRRR format)
            color_value = r5 | (g5 << 5) | (b5 << 10)

            # Write to sprite data
            offset = start_offset + (i * self.BYTES_PER_COLOR)
            if offset + 1 < len(sprite_data):
                struct.pack_into('<H', sprite_data, offset, color_value)

    def darken_color(self, color: Tuple[int, int, int], factor: float) -> Tuple[int, int, int]:
        """Darken a color by a factor (0.0 = black, 1.0 = original)."""
        r, g, b = color
        return (
            int(r * factor),
            int(g * factor),
            int(b * factor)
        )

    def shift_hue(self, color: Tuple[int, int, int], hue_shift: float) -> Tuple[int, int, int]:
        """Shift the hue of a color while preserving brightness."""
        import colorsys
        r, g, b = [x / 255.0 for x in color]
        h, s, v = colorsys.rgb_to_hsv(r, g, b)
        h = (h + hue_shift) % 1.0
        r, g, b = colorsys.hsv_to_rgb(h, s, v)
        return (int(r * 255), int(g * 255), int(b * 255))

    def check_palette_status(self, sprite_data: bytearray) -> dict:
        """Check if palettes are black (corrupted)."""
        status = {}
        # Check palettes 1-4 for job-specific themes
        for palette_idx in range(1, 5):
            palette = self.get_palette(sprite_data, palette_idx)
            # Check if all colors (except transparency at index 0) are black
            is_black = all(color == (0, 0, 0) for color in palette[1:])
            status[f"palette_{palette_idx}"] = "BLACK" if is_black else "HAS_DATA"
        # Also check 5-7 for generic themes
        for palette_idx in range(5, 8):
            palette = self.get_palette(sprite_data, palette_idx)
            is_black = all(color == (0, 0, 0) for color in palette[1:])
            status[f"palette_{palette_idx}"] = "BLACK" if is_black else "HAS_DATA"
        return status

    def fix_enemy_palettes(self, sprite_data: bytearray, method: str = "darken",
                           sprite_file: str = None, theme_name: str = None) -> bytearray:
        """
        Fix enemy palettes. Job-specific themes need palettes 1-4, generic themes need 5-7.

        Methods:
        - darken: Copy palette 0 to 1-4 with darkening for job-specific themes
        - copy: Direct copy of palette 0 to palettes 1-4
        - custom: Custom enemy colors (red, purple, dark gray)
        - original: Copy palettes from original sprites (best option)
        """
        result = sprite_data.copy()

        # Check which palettes need fixing
        palette1 = self.get_palette(result, 1)
        palette2 = self.get_palette(result, 2)
        palette3 = self.get_palette(result, 3)
        palette4 = self.get_palette(result, 4)
        palette5 = self.get_palette(result, 5)

        # Check if palettes 2-4 are black (more reliable than just checking palette 1)
        palette2_black = all(color == (0, 0, 0) for color in palette2[1:])
        palette3_black = all(color == (0, 0, 0) for color in palette3[1:])
        palette4_black = all(color == (0, 0, 0) for color in palette4[1:])

        # If any of palettes 2-4 are black, this is a job-specific theme that needs fixing
        is_job_specific = palette2_black or palette3_black or palette4_black

        if method == "darken":
            if is_job_specific:
                # Fix palettes 1-4 for job-specific themes
                # Use ORIGINAL enemy colors, not variations of the player theme!

                # Read original palettes from the sprites_original directory if available
                original_path = sprite_path.replace(f"sprites_{theme_name}", "sprites_original") if hasattr(self, 'current_sprite_path') else None

                if original_path and Path(original_path).exists():
                    # Load original sprite to get proper enemy palettes
                    original_data = self.read_sprite(Path(original_path))
                    for palette_idx in range(1, 5):
                        original_palette = self.get_palette(original_data, palette_idx)
                        self.set_palette(result, palette_idx, original_palette)
                else:
                    # Fallback: Create standard enemy colors manually
                    # These are approximations of standard FFT enemy colors

                    # Palette 1: Blue team variant (like in original)
                    palette1 = [
                        (0, 0, 0),  # Transparency
                        (40, 40, 32), (224, 216, 208),  # Base colors
                        (40, 56, 80), (56, 80, 104), (72, 104, 128),  # Blue armor
                        (96, 136, 160), (72, 64, 56), (112, 96, 80),  # More blue
                        (136, 120, 96), (88, 48, 24), (136, 80, 32),  # Browns
                        (176, 112, 64), (112, 72, 32), (184, 120, 80), (232, 168, 120)  # Hair/skin
                    ]
                    self.set_palette(result, 1, palette1[:16])  # Only use first 16 colors

                    # Palette 2: Red team variant (common enemy color)
                    palette2 = [
                        (0, 0, 0),  # Transparency
                        (40, 40, 32), (224, 216, 208),  # Base colors
                        (40, 48, 48), (48, 56, 56), (56, 64, 64),  # Dark armor
                        (64, 72, 72), (96, 48, 32), (152, 48, 32),  # Red armor
                        (192, 56, 32), (80, 64, 32), (144, 104, 56),  # More red
                        (208, 144, 80), (104, 64, 40), (160, 104, 72), (216, 144, 104)  # Hair/skin
                    ]
                    self.set_palette(result, 2, palette2[:16])

                    # Palette 3: Green team variant
                    palette3 = [
                        (0, 0, 0),  # Transparency
                        (40, 40, 32), (224, 216, 208),  # Base colors
                        (48, 56, 32), (64, 80, 40), (96, 112, 48),  # Green armor
                        (128, 144, 56), (56, 64, 72), (80, 96, 120),  # More green
                        (104, 128, 136), (88, 56, 32), (136, 80, 32),  # Browns
                        (176, 112, 48), (96, 64, 32), (136, 96, 56), (184, 128, 88)  # Hair/skin
                    ]
                    self.set_palette(result, 3, palette3[:16])

                    # Palette 4: Purple team variant
                    palette4 = [
                        (0, 0, 0),  # Transparency
                        (40, 40, 32), (224, 216, 208),  # Base colors
                        (72, 48, 64), (88, 64, 96), (112, 88, 136),  # Purple armor
                        (136, 112, 176), (96, 104, 112), (144, 152, 160),  # More purple
                        (200, 208, 208), (88, 64, 48), (152, 96, 48),  # Light purple
                        (224, 136, 48), (104, 64, 48), (160, 104, 80), (216, 152, 112)  # Hair/skin
                    ]
                    self.set_palette(result, 4, palette4[:16])

            else:
                # Fix palettes 5-7 for generic themes (original code)
                palette2 = self.get_palette(result, 2)
                enemy_palette5 = [self.darken_color(color, 0.7) if i > 0 else color
                                  for i, color in enumerate(palette2)]
                self.set_palette(result, 5, enemy_palette5)

                palette3 = self.get_palette(result, 3)
                enemy_palette6 = [self.darken_color(color, 0.6) if i > 0 else color
                                  for i, color in enumerate(palette3)]
                self.set_palette(result, 6, enemy_palette6)

                palette4 = self.get_palette(result, 4)
                enemy_palette7 = [self.darken_color(color, 0.5) if i > 0 else color
                                  for i, color in enumerate(palette4)]
                self.set_palette(result, 7, enemy_palette7)

        elif method == "original":
            # Best method: Copy palettes 1-4 from the original sprite
            # ALWAYS replace palettes 1-4 for job-specific themes, even if they have data
            # This ensures we get standard enemy colors, not custom theme colors
            # Job-specific themes have format: jobclass_themename (e.g., monk_shadow_assassin)

            # List of all job classes that can have job-specific themes
            job_classes = ['knight', 'monk', 'archer', 'squire', 'chemist', 'thief',
                          'ninja', 'samurai', 'dragoon', 'geomancer', 'mediator',
                          'timemage', 'bard', 'dancer', 'calculator', 'mime']

            # Check if this is a job-specific theme (starts with a job class name)
            is_job_theme = False
            if theme_name:
                for job in job_classes:
                    if theme_name.startswith(job + '_'):
                        is_job_theme = True
                        break

            if sprite_file and (is_job_specific or is_job_theme):
                # Get the original sprite path
                original_dir = self.sprite_dir / "sprites_original"
                original_sprite = original_dir / sprite_file

                if original_sprite.exists():
                    # Read the original sprite
                    original_data = self.read_sprite(original_sprite)

                    # ALWAYS copy palettes 1-4 from original (force standard enemy colors)
                    for palette_idx in range(1, 5):
                        original_palette = self.get_palette(original_data, palette_idx)
                        self.set_palette(result, palette_idx, original_palette)
                    print(f"     [OK] Copied standard enemy palettes from original")
                else:
                    # Fallback to manual colors if original not found
                    print(f"     [WARNING] Original sprite not found: {original_sprite}")
                    # Use the manual palette fallback from above
                    return self.fix_enemy_palettes(sprite_data, "custom", sprite_file, theme_name)

        elif method == "copy":
            # Direct copy for testing
            palette2 = self.get_palette(result, 2)
            self.set_palette(result, 5, palette2)

            palette3 = self.get_palette(result, 3)
            self.set_palette(result, 6, palette3)

            palette4 = self.get_palette(result, 4)
            self.set_palette(result, 7, palette4)

        elif method == "custom":
            # Create custom enemy palettes
            # Palette 5: Dark red enemy colors
            palette0 = self.get_palette(result, 0)
            enemy_red = []
            for i, color in enumerate(palette0):
                if i == 0:  # Keep transparency
                    enemy_red.append(color)
                elif 3 <= i <= 9:  # Armor colors
                    # Shift to red and darken
                    r, g, b = color
                    enemy_red.append((min(255, r + 40), max(0, g - 20), max(0, b - 20)))
                else:  # Keep other colors similar
                    enemy_red.append(self.darken_color(color, 0.8))
            self.set_palette(result, 5, enemy_red)

            # Palette 6: Dark purple enemy colors
            enemy_purple = []
            for i, color in enumerate(palette0):
                if i == 0:
                    enemy_purple.append(color)
                elif 3 <= i <= 9:
                    r, g, b = color
                    enemy_purple.append((min(255, r + 20), max(0, g - 10), min(255, b + 30)))
                else:
                    enemy_purple.append(self.darken_color(color, 0.7))
            self.set_palette(result, 6, enemy_purple)

            # Palette 7: Dark gray enemy colors
            enemy_gray = []
            for i, color in enumerate(palette0):
                if i == 0:
                    enemy_gray.append(color)
                else:
                    # Convert to grayscale and darken
                    r, g, b = color
                    gray = int((r + g + b) / 3 * 0.6)
                    enemy_gray.append((gray, gray, gray))
            self.set_palette(result, 7, enemy_gray)

        return result

    def process_sprites(self, job_class: str, themes: List[str], method: str = "darken",
                       backup: bool = True) -> None:
        """
        Process sprites for a specific job class across specified themes.

        Args:
            job_class: Job class name (e.g., 'knight', 'monk')
            themes: List of theme names to process (e.g., ['corpse_brigade', 'lucavi'])
            method: Fix method to use
            backup: Whether to backup original files
        """
        # Mapping of job class names to actual sprite file names
        JOB_TO_SPRITE = {
            'bard': 'gin',
            'calculator': 'san',
            'chemist': 'item',
            'dancer': 'odori',
            'dragoon': 'ryu',
            'geomancer': 'fusui',
            'mediator': 'waju',
            'mime': 'mono',
            'ninja': 'ninja',
            'samurai': 'samu',
            'squire': 'mina',
            'thief': 'thief',
            'timemage': 'toki',
            'knight': 'knight',
            'monk': 'monk',
            'archer': 'yumi',
            'whitemage': 'siro',
            'blackmage': 'kuro',
            'summoner': 'syou',
            'mystic': 'onmyo',
            'oracle': 'onmyo'
        }

        # Get the actual sprite name, default to job_class if not in mapping
        sprite_name = JOB_TO_SPRITE.get(job_class.lower(), job_class.lower())

        # Sprite file patterns for the job class
        # Special case: Dancer only has female sprites, Bard only has male
        if job_class.lower() == 'dancer':
            sprite_patterns = [
                f"battle_{sprite_name}_w_spr.bin",  # Female only
            ]
        elif job_class.lower() == 'bard':
            sprite_patterns = [
                f"battle_{sprite_name}_m_spr.bin",  # Male only
            ]
        else:
            sprite_patterns = [
                f"battle_{sprite_name}_m_spr.bin",  # Male battle sprite
                f"battle_{sprite_name}_w_spr.bin",  # Female battle sprite
            ]

        processed_count = 0

        for theme in themes:
            theme_dir = self.sprite_dir / f"sprites_{theme}"

            if not theme_dir.exists():
                print(f"  [WARNING] Theme directory not found: {theme_dir}")
                continue

            print(f"\n[THEME] Processing theme: {theme}")

            for pattern in sprite_patterns:
                sprite_path = theme_dir / pattern

                if not sprite_path.exists():
                    # Try without gender suffix if not found
                    alt_pattern = pattern.replace('_m_', '_').replace('_w_', '_')
                    sprite_path = theme_dir / alt_pattern

                    if not sprite_path.exists():
                        print(f"  [WARNING] Sprite not found: {pattern}")
                        continue

                print(f"  [PROCESSING] {sprite_path.name}")

                # Read sprite
                sprite_data = self.read_sprite(sprite_path)

                # Check current status
                status = self.check_palette_status(sprite_data)
                print(f"     Before: Palettes 5-7: {status}")

                # Backup if requested
                if backup:
                    backup_path = sprite_path.with_suffix('.bin.bak')
                    if not backup_path.exists():
                        shutil.copy2(sprite_path, backup_path)
                        print(f"     [BACKUP] Created: {backup_path.name}")

                # Fix the palettes
                fixed_data = self.fix_enemy_palettes(sprite_data, method, sprite_path.name, theme)

                # Write back
                self.write_sprite(sprite_path, fixed_data)

                # Verify
                status_after = self.check_palette_status(fixed_data)
                print(f"     After:  Palettes 5-7: {status_after}")

                processed_count += 1

        print(f"\n[DONE] Processed {processed_count} sprite files")

    def restore_backups(self, job_class: str, themes: List[str]) -> None:
        """Restore sprites from backup files."""
        restored_count = 0

        for theme in themes:
            theme_dir = self.sprite_dir / f"sprites_{theme}"

            if not theme_dir.exists():
                continue

            for backup_file in theme_dir.glob(f"*{job_class}*.bin.bak"):
                original_file = backup_file.with_suffix('')
                shutil.copy2(backup_file, original_file)
                print(f"  [RESTORED] {original_file.name}")
                restored_count += 1

        print(f"\n[DONE] Restored {restored_count} sprite files from backups")

def main():
    parser = argparse.ArgumentParser(description="Fix black enemy palettes in FFT sprites")
    parser.add_argument("job_class", help="Job class to fix (e.g., knight, monk, squire)")
    parser.add_argument("--themes", nargs="+",
                       help="Themes to process (default: all themes except original)")
    parser.add_argument("--method", choices=["darken", "copy", "custom", "original"], default="original",
                       help="Method to fix palettes (default: original)")
    parser.add_argument("--no-backup", action="store_true",
                       help="Don't create backup files")
    parser.add_argument("--restore", action="store_true",
                       help="Restore from backup files instead of fixing")
    parser.add_argument("--check-only", action="store_true",
                       help="Only check palette status without fixing")

    args = parser.parse_args()

    fixer = EnemyPaletteFixer()

    # Default themes if not specified
    if not args.themes:
        # Get all theme directories except original
        all_themes = []
        for theme_dir in fixer.sprite_dir.glob("sprites_*"):
            theme_name = theme_dir.name.replace("sprites_", "")
            if theme_name != "original":
                all_themes.append(theme_name)
        args.themes = sorted(all_themes)[:5]  # Start with just 5 themes for testing
        print(f"[INFO] Processing first 5 themes for testing: {args.themes}")

    if args.restore:
        print(f"[RESTORE] Restoring backups for {args.job_class}")
        fixer.restore_backups(args.job_class, args.themes)
    elif args.check_only:
        print(f"[CHECK] Checking palette status for {args.job_class}")
        for theme in args.themes:
            theme_dir = fixer.sprite_dir / f"sprites_{theme}"
            sprite_path = theme_dir / f"battle_{args.job_class}_m_spr.bin"
            if sprite_path.exists():
                sprite_data = fixer.read_sprite(sprite_path)
                status = fixer.check_palette_status(sprite_data)
                print(f"{theme}: {status}")
    else:
        print(f"[FIX] Fixing enemy palettes for {args.job_class}")
        print(f"   Method: {args.method}")
        print(f"   Backup: {not args.no_backup}")

        fixer.process_sprites(
            args.job_class,
            args.themes,
            method=args.method,
            backup=not args.no_backup
        )

        print(f"\n[COMPLETE] Done! Deploy with BuildLinked.ps1 and test in-game.")
        print(f"   If enemies still appear black, try --method=copy or --method=custom")

if __name__ == "__main__":
    main()