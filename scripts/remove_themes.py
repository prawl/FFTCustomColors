#!/usr/bin/env python3
"""
Remove specified themes from JobClasses.json
"""

import json
from pathlib import Path

# Themes to remove
THEMES_TO_REMOVE = {
    "steel_warrior",   # Remove from Squire
    "master_archer",   # Remove from all
    "holy_knight",     # Remove from Chemist only
    "sky_soldier",     # Remove from all
    "sun_priest"       # Remove from all
}

# Job-specific removals
JOB_SPECIFIC_REMOVALS = {
    "Chemist": ["holy_knight"],
    "Squire": ["steel_warrior"]
}

def main():
    json_path = Path("C:/Users/ptyRa/Dev/FFTColorCustomizer/ColorMod/Data/JobClasses.json")

    # Read the JSON file
    with open(json_path, 'r') as f:
        data = json.load(f)

    # Process each job class
    for job_class in data["jobClasses"]:
        job_type = job_class.get("jobType", "")
        current_themes = job_class.get("jobSpecificThemes", [])

        if not current_themes:
            continue

        # Start with current themes
        new_themes = current_themes.copy()

        # Remove globally banned themes
        global_removals = ["master_archer", "sky_soldier", "sun_priest"]
        for theme in global_removals:
            if theme in new_themes:
                new_themes.remove(theme)
                print(f"  Removed {theme} from {job_class['name']}")

        # Remove job-specific themes
        if job_type in JOB_SPECIFIC_REMOVALS:
            for theme in JOB_SPECIFIC_REMOVALS[job_type]:
                if theme in new_themes:
                    new_themes.remove(theme)
                    print(f"  Removed {theme} from {job_class['name']}")

        # Update the themes
        if len(new_themes) != len(current_themes):
            job_class["jobSpecificThemes"] = new_themes

    # Write the updated JSON
    with open(json_path, 'w') as f:
        json.dump(data, f, indent=2)

    print("\nJobClasses.json updated successfully!")

if __name__ == "__main__":
    main()