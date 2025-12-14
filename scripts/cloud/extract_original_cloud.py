#!/usr/bin/env python3
"""
Extract Cloud's original sprite from the game files
"""

import shutil
import os
from pathlib import Path

def extract_cloud_sprite():
    """Extract Cloud's original sprite to sprites_original"""

    base_dir = Path(__file__).parent.parent.parent

    # Source: Where Cloud's sprite currently exists (in original theme)
    source_file = base_dir / "ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_original/battle_cloud_spr.bin"

    # Check multiple possible locations for Cloud's sprite
    possible_sources = [
        base_dir / "ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_original/battle_cloud_spr.bin",
        base_dir / "ColorMod/FFTIVC/data/enhanced/fftpack/unit/battle_cloud_spr.bin",
    ]

    source_file = None
    for path in possible_sources:
        if path.exists():
            source_file = path
            print(f"Found Cloud sprite at: {path}")
            break

    if not source_file:
        print("Error: Could not find Cloud sprite (battle_cloud_spr.bin)")
        print("Checking what sprites are available...")

        # List available sprites
        unit_dir = base_dir / "ColorMod/FFTIVC/data/enhanced/fftpack/unit"
        if unit_dir.exists():
            sprites = sorted([f for f in unit_dir.glob("*.bin") if "cloud" in f.name.lower()])
            if sprites:
                print("\nFound Cloud-related sprites:")
                for sprite in sprites:
                    print(f"  - {sprite.name}")
            else:
                print("\nNo Cloud sprites found. Checking sprites_original directory...")
                orig_dir = unit_dir / "sprites_original"
                if orig_dir.exists():
                    all_sprites = sorted([f.name for f in orig_dir.glob("*.bin")])
                    print(f"\nAvailable sprites in sprites_original: {len(all_sprites)} files")
                    # Show a sample
                    story_sprites = [s for s in all_sprites if any(x in s for x in ["aguri", "oru", "beio", "mara", "rafa", "cloud", "musu"])]
                    if story_sprites:
                        print("\nStory character sprites found:")
                        for s in story_sprites:
                            print(f"  - {s}")
        return None

    # Ensure sprites_original directory exists
    dest_dir = base_dir / "ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_original"
    dest_dir.mkdir(exist_ok=True, parents=True)

    dest_file = dest_dir / "battle_cloud_spr.bin"

    # Copy if not already in sprites_original
    if source_file != dest_file:
        shutil.copy2(source_file, dest_file)
        print(f"Copied Cloud sprite to: {dest_file}")
    else:
        print(f"Cloud sprite already in sprites_original: {dest_file}")

    return dest_file

if __name__ == "__main__":
    result = extract_cloud_sprite()
    if result:
        print(f"\nSuccess! Cloud sprite ready at: {result}")
        print("\nNext step: Run create_simple_color_test.py to test color mapping")
    else:
        print("\nFailed to extract Cloud sprite. Please check if Cloud sprite exists in your game files.")