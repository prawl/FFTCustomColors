#!/usr/bin/env python3
"""
Apply creative names to all job-specific themes:
1. Rename theme directories from code names to creative names
2. Update JobClasses.json with creative names
"""

import os
import json
import shutil
from pathlib import Path

# Base paths
MOD_DIR = Path("C:/Users/ptyRa/Dev/FFTColorCustomizer")
SPRITES_DIR = MOD_DIR / "ColorMod/FFTIVC/data/enhanced/fftpack/unit"
DATA_DIR = MOD_DIR / "ColorMod/Data"
SCRIPTS_DIR = MOD_DIR / "scripts"

def load_theme_mappings():
    """Load creative name mappings from JSON file"""
    mapping_file = SCRIPTS_DIR / "theme_creative_names.json"
    with open(mapping_file, 'r') as f:
        data = json.load(f)
    return data['theme_mappings'], data['job_specific_overrides']

def get_creative_name(job_name, theme_code, mappings, overrides):
    """Get the creative name for a theme, considering job-specific overrides"""
    # Check for job-specific override first
    if job_name in overrides and theme_code in overrides[job_name]:
        return overrides[job_name][theme_code]

    # Otherwise use the general mapping
    if theme_code in mappings:
        return mappings[theme_code]

    # If no mapping found, return the original code
    print(f"    Warning: No creative name found for {theme_code}, keeping original")
    return theme_code

def rename_theme_directories(job_name, themes, mappings, overrides):
    """Rename theme directories from codes to creative names"""
    renamed_themes = []

    for theme_code in themes:
        creative_name = get_creative_name(job_name, theme_code, mappings, overrides)

        if creative_name != theme_code:
            old_dir = SPRITES_DIR / f"sprites_{job_name}_{theme_code}"
            new_dir = SPRITES_DIR / f"sprites_{job_name}_{creative_name}"

            if old_dir.exists() and not new_dir.exists():
                old_dir.rename(new_dir)
                print(f"    Renamed: sprites_{job_name}_{theme_code} -> sprites_{job_name}_{creative_name}")
            elif new_dir.exists():
                print(f"    Already exists: sprites_{job_name}_{creative_name}")

        renamed_themes.append(creative_name)

    return renamed_themes

def update_job_classes_json(job_updates):
    """Update JobClasses.json with creative theme names"""
    json_path = DATA_DIR / "JobClasses.json"

    # Read existing data
    with open(json_path, 'r') as f:
        data = json.load(f)

    # Update each job class
    for job_class in data["jobClasses"]:
        job_type = job_class.get("jobType", "").lower()

        if job_type in job_updates:
            # Update with creative names
            job_class["jobSpecificThemes"] = job_updates[job_type]
            print(f"  Updated {job_class['name']} with creative theme names")

    # Write updated data
    with open(json_path, 'w') as f:
        json.dump(data, f, indent=2)

    print("\nJobClasses.json updated with creative names!")

def main():
    """Apply creative names to all job themes"""
    print("Applying creative names to all job-specific themes...")
    print("=" * 60)

    # Load mappings
    mappings, overrides = load_theme_mappings()
    print(f"Loaded {len(mappings)} creative name mappings")
    print(f"Loaded job-specific overrides for {len(overrides)} jobs")
    print()

    # Load current JobClasses.json to get current themes
    json_path = DATA_DIR / "JobClasses.json"
    with open(json_path, 'r') as f:
        data = json.load(f)

    # Process each job
    job_updates = {}

    for job_class in data["jobClasses"]:
        job_type = job_class.get("jobType", "").lower()
        current_themes = job_class.get("jobSpecificThemes", [])

        # Skip Knight since we already renamed it manually
        if job_type == "knight":
            print(f"Skipping {job_type} (already has creative names)")
            continue

        if current_themes:
            print(f"Processing {job_type}...")

            # Rename directories and get new theme names
            new_themes = rename_theme_directories(job_type, current_themes, mappings, overrides)

            # Store updates for this job
            if job_type not in job_updates or len(new_themes) > len(job_updates.get(job_type, [])):
                job_updates[job_type] = new_themes

    # Update JobClasses.json
    print("\n" + "=" * 60)
    update_job_classes_json(job_updates)

    # Summary
    print("\n" + "=" * 60)
    print("Summary:")
    print(f"  Jobs processed: {len(job_updates)}")
    total_themes = sum(len(themes) for themes in job_updates.values())
    print(f"  Total themes renamed: {total_themes}")
    print("\nCreative names applied successfully!")

if __name__ == "__main__":
    main()