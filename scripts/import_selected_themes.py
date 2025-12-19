#!/usr/bin/env python3
"""
Import selected themes from FFT_Palette_Tests with creative names
"""

import os
import shutil
import json

SOURCE_DIR = "C:/Users/ptyRa/OneDrive/Desktop/FFT_Palette_Tests/ALL_COMBOS"
DEST_DIR = "ColorMod/FFTIVC/data/enhanced/fftpack/unit"
JSON_PATH = "ColorMod/Data/StoryCharacters.json"

# Selected themes with creative names
# Format: (folder_name, character, creative_name, description)
SELECTED_THEMES = [
    # Agrias themes
    ("agrias_bariten", "agrias", "grand_duke", "Noble authority and power"),
    ("agrias_baruna", "agrias", "temple_engineer", "Machinist precision with holy purpose"),
    ("agrias_cyomon2", "agrias", "demon_hunter", "Darkness to fight darkness"),
    ("agrias_h78", "agrias", "mysterious_knight", "Unknown warrior's legacy"),
    ("agrias_mono_w", "agrias", "mime_dancer", "Silent grace and deadly art"),
    ("agrias_waju_m", "agrias", "diplomat_blade", "Words and steel combined"),

    # Beowulf themes
    ("beowulf_baruna", "beowulf", "forge_templar", "Temple knight machinist"),
    ("beowulf_cyomon1", "beowulf", "demon_slayer", "Hunter of the damned"),
    ("beowulf_eru", "beowulf", "vampire_lord", "Embraced the darkness"),
    ("beowulf_h77", "beowulf", "ancient_guardian", "Timeless protector"),

    # Cloud themes
    ("cloud_20m", "cloud", "young_soldier", "Fresh recruit colors"),
    ("cloud_baru", "cloud", "sky_pirate", "Adventurer from above"),
    ("cloud_daisu", "cloud", "academy_scholar", "Studied warrior"),
    ("cloud_item_m", "cloud", "merchant_blade", "Coin and steel"),
    ("cloud_knight_w", "cloud", "valkyrie_soldier", "Female knight inspiration"),
    ("cloud_ryu_m", "cloud", "dragon_soldier", "Draconic power"),

    # Marach themes
    ("marach_baruna", "marach", "temple_artificer", "Machinist oracle"),
    ("marach_cyomon4", "marach", "void_herald", "Speaker of emptiness"),
    ("marach_h78", "marach", "enigma_caster", "Mystery incarnate"),
    ("marach_ryu_m", "marach", "dragon_sage", "Draconic wisdom"),
    ("marach_ryu_w", "marach", "dragon_priestess", "Feminine dragon power"),
    ("marach_simon", "marach", "scholar_prophet", "Knowledge and prophecy"),

    # Meliadoul themes
    ("meliadoul_60w", "meliadoul", "elder_wisdom", "Aged grace and power"),
    ("meliadoul_baruna", "meliadoul", "siege_engineer", "Fortress breaker"),
    ("meliadoul_fyune", "meliadoul", "funeral_knight", "Death's companion"),
    ("meliadoul_kyuku", "meliadoul", "plague_knight", "Pestilence blade"),
    ("meliadoul_ryu_m", "meliadoul", "dragon_general", "Draconic commander"),

    # Mustadio themes
    ("mustadio_fyune", "mustadio", "mourning_gun", "Sorrow and steel"),
    ("mustadio_siro_w", "mustadio", "white_rose", "Pure elegance"),

    # Orlandeau themes
    ("orlandeau_cyomon1", "orlandeau", "demon_god", "Ascended darkness"),
    ("orlandeau_h80", "orlandeau", "eternal_thunder", "Timeless power"),
    ("orlandeau_monk_m", "orlandeau", "fist_god", "Martial perfection"),

    # Rapha themes
    ("rapha_arute", "rapha", "fallen_oracle", "Corrupted prophecy"),
    ("rapha_h80", "rapha", "ancient_witch", "Timeless magic"),
    ("rapha_ninja_m", "rapha", "shadow_oracle", "Stealth and sorcery"),
]

def copy_theme_files(source_folder, character, theme_name):
    """Copy theme files to project directory"""
    source_path = os.path.join(SOURCE_DIR, source_folder)
    dest_path = os.path.join(DEST_DIR, f"sprites_{character}_{theme_name}")

    if not os.path.exists(source_path):
        print(f"  WARNING: Source not found: {source_path}")
        return False

    # Create destination directory
    os.makedirs(dest_path, exist_ok=True)

    # Copy all .bin files
    copied = False
    for file in os.listdir(source_path):
        if file.endswith('.bin'):
            src = os.path.join(source_path, file)

            # Special handling for Rapha - rename rafa to h79
            if character == "rapha" and "rafa" in file:
                dst_file = file.replace("rafa", "h79")
            else:
                dst_file = file

            dst = os.path.join(dest_path, dst_file)
            shutil.copy2(src, dst)
            copied = True

    return copied

def update_story_characters_json():
    """Update StoryCharacters.json with new themes"""
    # Read current JSON
    with open(JSON_PATH, 'r') as f:
        data = json.load(f)

    # Collect themes by character
    themes_to_add = {}
    for folder, character, theme_name, _ in SELECTED_THEMES:
        if character not in themes_to_add:
            themes_to_add[character] = []
        themes_to_add[character].append(theme_name)

    # Update each character's available themes
    for char_data in data['characters']:
        char_name = char_data['name'].lower()
        if char_name in themes_to_add:
            for theme in themes_to_add[char_name]:
                if theme not in char_data['availableThemes']:
                    char_data['availableThemes'].append(theme)

    # Write updated JSON
    with open(JSON_PATH, 'w') as f:
        json.dump(data, f, indent=2)

    return themes_to_add

def main():
    print("=== Importing Selected Themes with Creative Names ===")
    print("=" * 60)

    success_count = 0

    # Process each selected theme
    for folder, character, theme_name, description in SELECTED_THEMES:
        if copy_theme_files(folder, character, theme_name):
            print(f"[OK] {character:10} -> '{theme_name:20}' : {description}")
            success_count += 1
        else:
            print(f"[FAIL] {character:10} -> '{theme_name:20}'")

    print("=" * 60)
    print(f"Successfully imported {success_count}/{len(SELECTED_THEMES)} themes")

    if success_count > 0:
        print("\nUpdating StoryCharacters.json...")
        themes_added = update_story_characters_json()

        print("\nThemes added per character:")
        for char, themes in sorted(themes_added.items()):
            print(f"  {char.capitalize():10} : {len(themes)} new themes")

        print("\n✓ All themes imported and configured!")
        print("Run BuildLinked.ps1 to deploy the mod with all new themes.")

if __name__ == "__main__":
    main()