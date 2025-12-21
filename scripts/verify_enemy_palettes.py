#!/usr/bin/env python3
"""
Verify that all job-specific themes have proper enemy palettes (1-4 should have data).
This ensures the fix_enemy_palettes.py script worked correctly.
"""

import struct
from pathlib import Path
from typing import List, Tuple, Dict

class PaletteVerifier:
    """Verify enemy palettes in FFT sprite files."""

    PALETTE_SIZE = 512
    COLORS_PER_PALETTE = 16
    BYTES_PER_COLOR = 2

    # All job classes that can have job-specific themes
    JOB_CLASSES = [
        'knight', 'monk', 'archer', 'squire', 'chemist', 'thief',
        'ninja', 'samurai', 'dragoon', 'geomancer', 'mediator',
        'timemage', 'bard', 'dancer', 'calculator', 'mime'
    ]

    def __init__(self, base_dir: Path = Path(".")):
        """Initialize the verifier."""
        self.base_dir = base_dir
        self.sprite_dir = base_dir / "ColorMod" / "FFTIVC" / "data" / "enhanced" / "fftpack" / "unit"
        self.issues_found = []
        self.themes_checked = 0
        self.sprites_checked = 0

    def read_sprite(self, sprite_path: Path) -> bytearray:
        """Read sprite file."""
        with open(sprite_path, 'rb') as f:
            return bytearray(f.read())

    def get_palette(self, sprite_data: bytearray, palette_index: int) -> List[Tuple[int, int, int]]:
        """Extract a palette (16 colors) from sprite data."""
        colors = []
        start_offset = palette_index * self.COLORS_PER_PALETTE * self.BYTES_PER_COLOR

        for i in range(self.COLORS_PER_PALETTE):
            offset = start_offset + (i * self.BYTES_PER_COLOR)
            if offset + 1 < len(sprite_data):
                color_value = struct.unpack('<H', sprite_data[offset:offset+2])[0]

                # Extract RGB from 16-bit color
                r = (color_value & 0x001F) << 3
                g = ((color_value & 0x03E0) >> 5) << 3
                b = ((color_value & 0x7C00) >> 10) << 3

                colors.append((r, g, b))
            else:
                colors.append((0, 0, 0))

        return colors

    def is_palette_black(self, palette: List[Tuple[int, int, int]]) -> bool:
        """Check if palette is all black (except transparency at index 0)."""
        return all(color == (0, 0, 0) for color in palette[1:])

    def is_job_specific_theme(self, theme_name: str) -> bool:
        """Determine if this is a job-specific theme."""
        for job in self.JOB_CLASSES:
            if theme_name.startswith(job + '_'):
                return True
        return False

    def check_sprite(self, sprite_path: Path, theme_name: str) -> Dict:
        """Check a single sprite file."""
        sprite_data = self.read_sprite(sprite_path)

        results = {
            'file': sprite_path.name,
            'theme': theme_name,
            'palettes': {}
        }

        # Check palettes 1-4 (enemy palettes)
        has_issue = False
        for i in range(1, 5):
            palette = self.get_palette(sprite_data, i)
            is_black = self.is_palette_black(palette)
            results['palettes'][f'palette_{i}'] = 'BLACK' if is_black else 'HAS_DATA'

            # For job-specific themes, palettes 1-4 should NOT be black
            if self.is_job_specific_theme(theme_name) and is_black:
                has_issue = True

        # Also check 5-7 for completeness
        for i in range(5, 8):
            palette = self.get_palette(sprite_data, i)
            is_black = self.is_palette_black(palette)
            results['palettes'][f'palette_{i}'] = 'BLACK' if is_black else 'HAS_DATA'

        results['has_issue'] = has_issue
        return results

    def verify_all_themes(self):
        """Verify all job-specific themed sprites."""
        print("[VERIFICATION] Starting palette verification for all job-specific themes\n")
        print("=" * 80)

        # Get all theme directories
        all_themes = sorted([d for d in self.sprite_dir.glob("sprites_*") if d.is_dir()])

        for theme_dir in all_themes:
            theme_name = theme_dir.name.replace("sprites_", "")

            # Skip if not a job-specific theme
            if not self.is_job_specific_theme(theme_name):
                continue

            self.themes_checked += 1
            theme_has_issues = False

            # Check all sprite files in this theme
            for sprite_file in theme_dir.glob("*.bin"):
                # Skip backup files
                if sprite_file.suffix == '.bak':
                    continue

                self.sprites_checked += 1
                result = self.check_sprite(sprite_file, theme_name)

                if result['has_issue']:
                    theme_has_issues = True
                    self.issues_found.append(result)
                    print(f"[ISSUE] {theme_name}/{result['file']}")
                    print(f"        Palettes 1-4: {[result['palettes'][f'palette_{i}'] for i in range(1, 5)]}")
                    print(f"        ERROR: Enemy palettes are BLACK (should have data)")
                    print()

            if not theme_has_issues:
                # Show progress for correctly fixed themes
                print(f"[OK] {theme_name} - All enemy palettes correct")

        print("=" * 80)
        print("\n[SUMMARY]")
        print(f"Themes checked: {self.themes_checked}")
        print(f"Sprites checked: {self.sprites_checked}")
        print(f"Issues found: {len(self.issues_found)}")

        if self.issues_found:
            print("\n[THEMES NEEDING FIXES]")
            problem_themes = set(issue['theme'] for issue in self.issues_found)
            for theme in sorted(problem_themes):
                print(f"  - {theme}")

            print(f"\n[ACTION REQUIRED]")
            print(f"Run: python scripts/fix_enemy_palettes.py <job_class> --themes <theme_names> --method original")
            print(f"For each problematic job class")
        else:
            print("\n[SUCCESS] All job-specific themes have proper enemy palettes!")
            print("Enemy units should display with correct colors in-game.")

    def verify_specific_theme(self, theme_name: str):
        """Verify a specific theme."""
        theme_dir = self.sprite_dir / f"sprites_{theme_name}"

        if not theme_dir.exists():
            print(f"[ERROR] Theme not found: {theme_name}")
            return

        print(f"[CHECKING] {theme_name}")
        print("-" * 40)

        has_issues = False
        for sprite_file in theme_dir.glob("*.bin"):
            if sprite_file.suffix == '.bak':
                continue

            result = self.check_sprite(sprite_file, theme_name)
            print(f"  {result['file']}:")
            print(f"    Palettes 1-4: {[result['palettes'][f'palette_{i}'] for i in range(1, 5)]}")

            if result['has_issue']:
                has_issues = True
                print(f"    [ISSUE] Enemy palettes are BLACK!")

        if not has_issues:
            print(f"\n[OK] {theme_name} has correct enemy palettes")
        else:
            print(f"\n[ISSUE] {theme_name} needs fixing")

def main():
    import argparse
    parser = argparse.ArgumentParser(description="Verify enemy palettes in FFT sprites")
    parser.add_argument("--theme", help="Check specific theme only")
    parser.add_argument("--job", help="Check all themes for specific job class")

    args = parser.parse_args()

    verifier = PaletteVerifier()

    if args.theme:
        verifier.verify_specific_theme(args.theme)
    elif args.job:
        # Check all themes for a specific job
        print(f"[VERIFICATION] Checking all {args.job} themes\n")
        theme_dir = verifier.sprite_dir
        job_themes = sorted([d.name.replace("sprites_", "")
                           for d in theme_dir.glob(f"sprites_{args.job}_*")
                           if d.is_dir()])

        if not job_themes:
            print(f"No themes found for job: {args.job}")
            return

        for theme in job_themes:
            verifier.verify_specific_theme(theme)
            print()
    else:
        verifier.verify_all_themes()

if __name__ == "__main__":
    main()