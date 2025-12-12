#!/usr/bin/env python3
"""
Apply color themes to story character sprites or revert them to original.
Uses the correct sprite names discovered from the game files.
"""

import os
import shutil
import struct
import sys

# Correct story character sprite names from game files
STORY_CHARACTERS = [
    "battle_musu_spr.bin",    # Mustadio
    "battle_aguri_spr.bin",   # Agrias
    "battle_kanba_spr.bin",   # Agrias second sprite
    "battle_oru_spr.bin",     # Orlandeau (NOT oran!)
    "battle_dily_spr.bin",    # Delita chapter 1
    "battle_dily2_spr.bin",   # Delita chapter 2
    "battle_dily3_spr.bin",   # Delita chapter 3
    "battle_hime_spr.bin",    # Ovelia
    "battle_aruma_spr.bin",   # Alma
    "battle_rafa_spr.bin",    # Rafa
    "battle_mara_spr.bin",    # Malak
    "battle_cloud_spr.bin",   # Cloud
    "battle_beio_spr.bin",    # Beowulf
    "battle_reze_spr.bin",    # Reis human
    "battle_reze_d_spr.bin"   # Reis dragon
]

def apply_story_character_themes():
    """Apply themes to story character sprites."""

    # Source directory with original story character sprites
    source_dir = r"c:\program files (x86)\steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\data\enhanced\0002\0002\fftpack\unit"

    # Our mod's sprite directories
    mod_base = r"ColorMod\FFTIVC\data\enhanced\fftpack\unit"

    # Get list of theme directories
    themes = []
    for entry in os.listdir(mod_base):
        if entry.startswith("sprites_") and entry != "sprites_original":
            themes.append(entry.replace("sprites_", ""))

    print(f"Found {len(themes)} themes to apply to story characters")
    print(f"Themes: {', '.join(themes)}")

    # Process each theme
    for theme in themes:
        theme_dir = os.path.join(mod_base, f"sprites_{theme}")

        # Check if we already have an Orlandeau sprite (battle_oru_spr.bin)
        # If we do, use it as a template for how the theme modifies sprites
        oru_path = os.path.join(theme_dir, "battle_oru_spr.bin")
        has_oru = os.path.exists(oru_path)

        if has_oru:
            print(f"\n{theme}: Found existing Orlandeau sprite, will use as reference")

        # Copy story character sprites to this theme
        copied = 0
        for sprite_name in STORY_CHARACTERS:
            dest_path = os.path.join(theme_dir, sprite_name)

            # Skip if already exists
            if os.path.exists(dest_path):
                print(f"  {sprite_name}: Already exists")
                continue

            # Find source sprite
            source_path = os.path.join(source_dir, sprite_name)
            if not os.path.exists(source_path):
                # Try fallback to main unit directory
                source_path = os.path.join(
                    r"c:\program files (x86)\steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\data\enhanced\fftpack\unit",
                    sprite_name
                )

            if not os.path.exists(source_path):
                print(f"  {sprite_name}: Source not found, skipping")
                continue

            # For now, just copy the original sprite
            # In the future, we could apply the theme's color transformations
            shutil.copy2(source_path, dest_path)
            print(f"  {sprite_name}: Copied to {theme}")
            copied += 1

        print(f"{theme}: Added {copied} story character sprites")

    print("\nDone! Story character sprites have been added to all themes.")
    print("Note: The sprites currently use original colors. Run create_themed_sprites.py to apply theme colors.")

# Working story characters that successfully change colors
WORKING_STORY_CHARACTERS = [
    "battle_oru_spr.bin",     # Orlandeau
    "battle_mara_spr.bin",    # Malak
    "battle_reze_spr.bin",    # Reis human
    "battle_reze_d_spr.bin",  # Reis dragon
    "battle_aguri_spr.bin",   # Agrias
    "battle_kanba_spr.bin",   # Agrias second sprite
    "battle_beio_spr.bin",    # Beowulf
]

def revert_working_characters_to_original():
    """Revert working story characters to original sprites in all themes."""

    # Source for original sprites
    original_source = r"c:\program files (x86)\steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\data\enhanced\0002\0002\fftpack\unit"

    # Our mod's sprite directories
    mod_base = r"ColorMod\FFTIVC\data\enhanced\fftpack\unit"

    # Get list of theme directories
    themes = []
    for entry in os.listdir(mod_base):
        if entry.startswith("sprites_") and entry != "sprites_original":
            themes.append(entry)

    print(f"Found {len(themes)} themes to revert story characters in")

    # Create backup directory for themed versions
    backup_dir = "story_character_themed_backup"
    os.makedirs(backup_dir, exist_ok=True)

    for theme_dir_name in themes:
        theme_name = theme_dir_name.replace("sprites_", "")
        theme_dir = os.path.join(mod_base, theme_dir_name)
        theme_backup = os.path.join(backup_dir, theme_name)
        os.makedirs(theme_backup, exist_ok=True)

        print(f"\n{theme_name}:")
        reverted = 0

        for sprite_name in WORKING_STORY_CHARACTERS:
            themed_path = os.path.join(theme_dir, sprite_name)

            if os.path.exists(themed_path):
                # Backup the themed version
                backup_path = os.path.join(theme_backup, sprite_name)
                shutil.copy2(themed_path, backup_path)
                print(f"  Backed up themed {sprite_name}")

                # Find original sprite
                original_path = os.path.join(original_source, sprite_name)

                if os.path.exists(original_path):
                    # Copy original over themed
                    shutil.copy2(original_path, themed_path)
                    print(f"  Reverted {sprite_name} to original")
                    reverted += 1
                else:
                    print(f"  Original {sprite_name} not found, keeping themed")
            else:
                print(f"  {sprite_name} not found in theme")

        print(f"{theme_name}: Reverted {reverted} story character sprites to original")

    print(f"\nDone! Working story characters reverted to original sprites.")
    print(f"Themed versions backed up to: {backup_dir}/")
    print("\nNote: These characters will still swap when F1/F2 is pressed,")
    print("but they'll all use original colors until we create custom themes for them.")

def print_usage():
    """Print usage information."""
    print("Usage:")
    print("  python apply_story_characters.py           - Copy story characters to all themes")
    print("  python apply_story_characters.py revert    - Revert working characters to original")
    print("  python apply_story_characters.py help      - Show this help message")

if __name__ == "__main__":
    if len(sys.argv) > 1:
        command = sys.argv[1].lower()
        if command == "revert":
            revert_working_characters_to_original()
        elif command == "help":
            print_usage()
        else:
            print(f"Unknown command: {command}")
            print_usage()
    else:
        apply_story_character_themes()