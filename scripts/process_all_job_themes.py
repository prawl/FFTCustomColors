#!/usr/bin/env python3
"""
Process all job-specific themes:
1. Read PNG files from each job's theme directory
2. Copy corresponding BIN files from GENERIC_COMBOS
3. Create theme directories in the mod
4. Update JobClasses.json with the themes
"""

import os
import shutil
import json
from pathlib import Path

# Base paths
PALETTE_TESTS_DIR = Path("C:/Users/ptyRa/OneDrive/Desktop/FFT_Palette_Tests")
GENERIC_COMBOS_DIR = PALETTE_TESTS_DIR / "GENERIC_COMBOS"
MOD_DIR = Path("C:/Users/ptyRa/Dev/FFTColorCustomizer")
SPRITES_DIR = MOD_DIR / "ColorMod/FFTIVC/data/enhanced/fftpack/unit"
DATA_DIR = MOD_DIR / "ColorMod/Data"

# Job name mappings
JOB_MAPPINGS = {
    "archer": {"display": "Archer", "sprite_m": "battle_yumi_m_spr.bin", "sprite_w": "battle_yumi_w_spr.bin"},
    "bard": {"display": "Bard", "sprite_m": "battle_gin_m_spr.bin", "sprite_w": None},
    "calculator": {"display": "Calculator", "sprite_m": "battle_san_m_spr.bin", "sprite_w": "battle_san_w_spr.bin"},
    "chemist": {"display": "Chemist", "sprite_m": "battle_item_m_spr.bin", "sprite_w": "battle_item_w_spr.bin"},
    "dancer": {"display": "Dancer", "sprite_m": None, "sprite_w": "battle_odori_w_spr.bin"},
    "dragoon": {"display": "Dragoon", "sprite_m": "battle_ryu_m_spr.bin", "sprite_w": "battle_ryu_w_spr.bin"},
    "geomancer": {"display": "Geomancer", "sprite_m": "battle_fusui_m_spr.bin", "sprite_w": "battle_fusui_w_spr.bin"},
    "knight": {"display": "Knight", "sprite_m": "battle_knight_m_spr.bin", "sprite_w": "battle_knight_w_spr.bin"},
    "mediator": {"display": "Mediator", "sprite_m": "battle_waju_m_spr.bin", "sprite_w": "battle_waju_w_spr.bin"},
    "mime": {"display": "Mime", "sprite_m": "battle_mono_m_spr.bin", "sprite_w": "battle_mono_w_spr.bin"},
    "monk": {"display": "Monk", "sprite_m": "battle_monk_m_spr.bin", "sprite_w": "battle_monk_w_spr.bin"},
    "ninja": {"display": "Ninja", "sprite_m": "battle_nin_m_spr.bin", "sprite_w": "battle_nin_w_spr.bin"},
    "oracle": {"display": "Oracle", "sprite_m": "battle_onmyo_m_spr.bin", "sprite_w": "battle_onmyo_w_spr.bin"},
    "priest": {"display": "Priest", "sprite_m": "battle_siro_m_spr.bin", "sprite_w": "battle_siro_w_spr.bin"},
    "samurai": {"display": "Samurai", "sprite_m": "battle_samu_m_spr.bin", "sprite_w": "battle_samu_w_spr.bin"},
    "squire": {"display": "Squire", "sprite_m": "battle_mina_m_spr.bin", "sprite_w": "battle_mina_w_spr.bin"},
    "summoner": {"display": "Summoner", "sprite_m": "battle_syou_m_spr.bin", "sprite_w": "battle_syou_w_spr.bin"},
    "thief": {"display": "Thief", "sprite_m": "battle_nusumu_m_spr.bin", "sprite_w": "battle_nusumu_w_spr.bin"},
    "time_mage": {"display": "Time Mage", "sprite_m": "battle_toki_m_spr.bin", "sprite_w": "battle_toki_w_spr.bin"},
    "wizard": {"display": "Wizard", "sprite_m": "battle_kuro_m_spr.bin", "sprite_w": "battle_kuro_w_spr.bin"},
}

def get_theme_names_from_pngs(job_name, gender):
    """Get the list of themes from PNG files in the job's theme directory"""
    theme_dir = PALETTE_TESTS_DIR / f"{job_name}_{gender}_themes"

    if not theme_dir.exists():
        print(f"  Directory not found: {theme_dir}")
        return []

    png_files = list(theme_dir.glob("*.png"))
    themes = []

    for png_file in png_files:
        # Extract theme name from filename
        # Format: jobname_themename.png
        filename = png_file.stem
        if filename.startswith(job_name + "_"):
            theme_name = filename[len(job_name) + 1:]
            themes.append(theme_name)

    return themes

def copy_theme_sprites(job_name, theme_name):
    """Copy sprite BIN files from GENERIC_COMBOS to mod directory"""
    source_dir = GENERIC_COMBOS_DIR / f"{job_name}_{theme_name}"

    if not source_dir.exists():
        print(f"    Source directory not found: {source_dir}")
        return False

    # Create destination directory
    dest_dir = SPRITES_DIR / f"sprites_{job_name}_{theme_name}"
    dest_dir.mkdir(parents=True, exist_ok=True)

    # Copy all BIN files
    copied = False
    for bin_file in source_dir.glob("*.bin"):
        dest_file = dest_dir / bin_file.name
        shutil.copy2(bin_file, dest_file)
        copied = True

    if copied:
        print(f"    Copied sprites to sprites_{job_name}_{theme_name}/")

    return copied

def process_job(job_name):
    """Process all themes for a specific job"""
    print(f"\nProcessing {job_name}...")

    # Check for male themes
    male_themes = get_theme_names_from_pngs(job_name, "male")
    female_themes = get_theme_names_from_pngs(job_name, "female")

    # Use whichever exists (some jobs are gender-specific)
    themes = male_themes if male_themes else female_themes

    if not themes:
        print(f"  No themes found for {job_name}")
        return []

    print(f"  Found {len(themes)} themes: {', '.join(themes)}")

    # Copy sprites for each theme
    successful_themes = []
    for theme in themes:
        if copy_theme_sprites(job_name, theme):
            successful_themes.append(theme)

    return successful_themes

def update_job_classes_json(job_themes):
    """Update JobClasses.json with the new themes"""
    json_path = DATA_DIR / "JobClasses.json"

    # Read existing data
    with open(json_path, 'r') as f:
        data = json.load(f)

    # Update each job class
    for job_class in data["jobClasses"]:
        # Extract base job name from class name (e.g., "Knight_Male" -> "knight")
        job_type = job_class.get("jobType", "").lower()

        if job_type in job_themes:
            # Update job-specific themes
            job_class["jobSpecificThemes"] = job_themes[job_type]
            print(f"  Updated {job_class['name']} with {len(job_themes[job_type])} themes")

    # Write updated data
    with open(json_path, 'w') as f:
        json.dump(data, f, indent=2)

    print("\nJobClasses.json updated successfully!")

def main():
    """Process all job themes"""
    print("Processing all job-specific themes...")
    print("=" * 50)

    # Skip knight since we already did it
    jobs_to_process = [job for job in JOB_MAPPINGS.keys() if job != "knight"]

    # Process each job
    all_job_themes = {}
    for job_name in jobs_to_process:
        themes = process_job(job_name)
        if themes:
            all_job_themes[job_name] = themes

    # Update JobClasses.json
    print("\n" + "=" * 50)
    print("Updating JobClasses.json...")
    update_job_classes_json(all_job_themes)

    # Summary
    print("\n" + "=" * 50)
    print("Summary:")
    for job, themes in all_job_themes.items():
        print(f"  {job}: {len(themes)} themes")
    print(f"\nTotal jobs processed: {len(all_job_themes)}")

if __name__ == "__main__":
    main()