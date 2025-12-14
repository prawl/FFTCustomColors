#!/usr/bin/env python3
"""
Generate 50 diverse themes for Cloud Strife and create PNG previews
Super efficient workflow - generates themes and previews all in one go
"""

import os
import sys
import struct
import shutil
import subprocess
from pathlib import Path
from PIL import Image
import numpy as np
import colorsys
import random

# Add parent directory to path for imports
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from convert_sprite_sw import extract_southwest_sprite

def bgr555_to_rgb(bgr555):
    """Convert BGR555 to RGB tuple"""
    b = (bgr555 >> 10) & 0x1F
    g = (bgr555 >> 5) & 0x1F
    r = bgr555 & 0x1F
    return (r * 255 // 31, g * 255 // 31, b * 255 // 31)

def rgb_to_bgr555(r, g, b):
    """Convert RGB to BGR555 format"""
    r = min(31, r * 31 // 255)
    g = min(31, g * 31 // 255)
    b = min(31, b * 31 // 255)
    return (b << 10) | (g << 5) | r

def hex_to_rgb(hex_color):
    """Convert hex color to RGB tuple"""
    hex_color = hex_color.lstrip('#')
    return tuple(int(hex_color[i:i+2], 16) for i in (0, 2, 4))

def adjust_brightness(rgb, factor):
    """Adjust brightness of RGB color"""
    return tuple(min(255, int(c * factor)) for c in rgb)

def generate_theme_colors():
    """Generate 50 diverse, aesthetically pleasing color combinations"""
    themes = []

    # 1. Classic FF7 Themes (10)
    themes.extend([
        ("soldier_first_class", "#4169E1", "#FFD700"),  # SOLDIER blue with gold
        ("shinra_elite", "#1C1C1C", "#FF0000"),         # Black with red accents
        ("midgar_industrial", "#708090", "#FF8C00"),    # Steel gray with orange
        ("mako_infused", "#00FF7F", "#E0FFFF"),        # Mako green with cyan
        ("buster_legacy", "#4B0082", "#C0C0C0"),       # Dark purple with silver
        ("avalanche_rebel", "#2F4F2F", "#8B4513"),     # Dark green with brown
        ("turks_uniform", "#191970", "#FFFFFF"),       # Midnight blue with white
        ("sephiroth_black", "#000000", "#C0C0C0"),     # Pure black with silver
        ("aerith_pink", "#FFB6C1", "#98FB98"),         # Light pink with pale green
        ("tifa_burgundy", "#800020", "#FFD700"),       # Burgundy with gold
    ])

    # 2. Materia-Inspired (12)
    themes.extend([
        ("fire_materia", "#FF4500", "#FFD700"),         # Fire red with gold
        ("ice_materia", "#00CED1", "#E0FFFF"),         # Ice blue with frost white
        ("lightning_materia", "#FFD700", "#4B0082"),   # Electric yellow with purple
        ("earth_materia", "#8B4513", "#DAA520"),       # Earth brown with gold
        ("restore_materia", "#00FF00", "#FFFFFF"),     # Healing green with white
        ("time_materia", "#9370DB", "#FFD700"),        # Purple with gold
        ("gravity_materia", "#4B0082", "#000000"),     # Indigo with black
        ("holy_materia", "#F0F8FF", "#FFD700"),        # Pure white with gold
        ("meteor_materia", "#8B0000", "#FF4500"),      # Dark red with orange
        ("ultima_materia", "#FF1493", "#00FFFF"),      # Deep pink with cyan
        ("poison_materia", "#32CD32", "#8A2BE2"),      # Lime green with violet
        ("barrier_materia", "#4169E1", "#98FB98"),     # Royal blue with pale green
    ])

    # 3. Location-Based Themes (10)
    themes.extend([
        ("costa_del_sol", "#FF6347", "#40E0D0"),       # Coral with turquoise
        ("golden_saucer", "#FFD700", "#FF1493"),       # Gold with hot pink
        ("nibelheim_flame", "#FF4500", "#1C1C1C"),     # Fire orange with black
        ("northern_crater", "#483D8B", "#00CED1"),     # Dark slate with cyan
        ("ancient_forest", "#228B22", "#8B4513"),      # Forest green with brown
        ("temple_ancients", "#DAA520", "#4B0082"),     # Golden with indigo
        ("forgotten_capital", "#E0FFFF", "#4169E1"),    # Light cyan with royal blue
        ("weapon_emerald", "#50C878", "#002000"),      # Emerald with dark green
        ("cosmo_canyon", "#CD853F", "#8B0000"),        # Peru with dark red
        ("wutai_pagoda", "#DC143C", "#FFD700"),        # Crimson with gold
    ])

    # 4. Limit Break Themes (8)
    themes.extend([
        ("omnislash", "#1E90FF", "#FFFFFF"),           # Dodger blue with white
        ("meteorain", "#FF8C00", "#8B0000"),           # Dark orange with maroon
        ("finishing_touch", "#00BFFF", "#FFD700"),     # Sky blue with gold
        ("climhazzard", "#DC143C", "#FFD700"),         # Crimson with gold
        ("blade_beam", "#00FF00", "#0000FF"),          # Lime with blue
        ("cross_slash", "#800080", "#C0C0C0"),         # Purple with silver
        ("braver", "#FF6347", "#4169E1"),              # Tomato with royal blue
        ("apocalypse", "#8B008B", "#FF4500"),          # Dark magenta with orange red
    ])

    # 5. Boss & Enemy Themes (5)
    themes.extend([
        ("jenova_synthesis", "#8B008B", "#00FF00"),    # Dark magenta with green
        ("ruby_weapon", "#8B0000", "#FF0000"),         # Dark red with bright red
        ("diamond_weapon", "#F0F8FF", "#4169E1"),      # Alice blue with royal
        ("bahamut_zero", "#191970", "#FFD700"),        # Midnight blue with gold
        ("knights_round", "#696969", "#FFD700"),       # Dim gray with gold
    ])

    # 6. Premium & Metallic (5)
    themes.extend([
        ("platinum_edge", "#E5E4E2", "#4169E1"),       # Platinum with royal blue
        ("rose_gold_elite", "#B76E79", "#FFD700"),     # Rose gold with gold
        ("bronze_warrior", "#CD7F32", "#8B4513"),      # Bronze with saddle brown
        ("titanium_core", "#878681", "#000000"),       # Titanium with black
        ("copper_strike", "#B87333", "#2F4F4F"),       # Copper with dark slate
    ])

    return themes[:50]  # Ensure exactly 50 themes

def create_themed_sprite(source_file, theme_name, primary_color, accent_color):
    """Create a themed sprite with the given colors"""
    # Read original sprite
    with open(source_file, 'rb') as f:
        data = bytearray(f.read())

    primary_rgb = hex_to_rgb(primary_color)
    accent_rgb = hex_to_rgb(accent_color) if accent_color else primary_rgb

    # Create color variations for depth
    cape_edge = adjust_brightness(primary_rgb, 0.75)  # 25% darker
    cape_shadow = adjust_brightness(primary_rgb, 0.5)  # 50% darker

    # Define indices based on our color test results
    # RED (3-5) = buckles/trim -> use accent color
    # GREEN (6-9) = primary armor/cape -> use primary color
    # BLUE (20-31) = secondary armor -> use primary color
    # YELLOW (35-47) = extended armor -> use primary color
    # MAGENTA (51-62) = additional armor -> use primary color

    accent_indices = [3, 4, 5]  # Buckles and trim
    primary_indices = (
        list(range(6, 10)) +      # Primary armor/cape
        list(range(20, 32)) +     # Secondary armor
        list(range(35, 44)) +     # Extended armor (before 44)
        list(range(45, 48)) +     # Extended armor (after 44)
        list(range(51, 63))       # Additional armor
    )

    # Apply colors to first 8 palettes (unit sprites)
    for palette_idx in range(8):
        base_offset = palette_idx * 32

        # Apply accent colors to buckles/trim
        for idx in accent_indices:
            offset = base_offset + (idx * 2)
            if offset + 1 < 512:
                bgr555 = rgb_to_bgr555(*accent_rgb)
                struct.pack_into('<H', data, offset, bgr555)

        # Apply primary colors with variations for depth
        for idx in primary_indices:
            offset = base_offset + (idx * 2)
            if offset + 1 < 512:
                # Add depth variations for specific indices
                if idx == 7:  # Cape edge
                    bgr555 = rgb_to_bgr555(*cape_edge)
                elif idx == 9:  # Cape shadow
                    bgr555 = rgb_to_bgr555(*cape_shadow)
                else:
                    bgr555 = rgb_to_bgr555(*primary_rgb)
                struct.pack_into('<H', data, offset, bgr555)

    return data

def generate_all_themes():
    """Generate all 50 Cloud themes"""
    print("\n" + "="*60)
    print("Generating 50 Cloud Themes")
    print("="*60)

    base_dir = Path(__file__).parent.parent.parent
    source_file = base_dir / "ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_original/battle_cloud_spr.bin"

    if not source_file.exists():
        print(f"Error: Cloud sprite not found at {source_file}")
        return []

    themes = generate_theme_colors()
    created_themes = []

    for i, (theme_name, primary, accent) in enumerate(themes, 1):
        print(f"[{i:2d}/50] Creating theme: {theme_name:30s}")

        # Create theme directory
        theme_dir = base_dir / f"ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_cloud_{theme_name}"
        theme_dir.mkdir(exist_ok=True, parents=True)

        # Generate themed sprite
        sprite_data = create_themed_sprite(source_file, theme_name, primary, accent)

        # Write themed sprite
        output_file = theme_dir / "battle_cloud_spr.bin"
        with open(output_file, 'wb') as f:
            f.write(sprite_data)

        created_themes.append((theme_name, output_file))

    print(f"\nSuccessfully created {len(created_themes)} themes")
    return created_themes

def convert_all_to_png(themes):
    """Convert all themes to PNG previews"""
    print("\n" + "="*60)
    print("Converting All Themes to PNG Previews")
    print("="*60)

    base_dir = Path(__file__).parent.parent.parent
    preview_dir = base_dir / "ColorMod/Resources/Previews/Cloud_Themes"
    preview_dir.mkdir(exist_ok=True, parents=True)

    # Include the test sprite if it exists
    test_sprite = base_dir / "ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_cloud_test/battle_cloud_spr.bin"
    if test_sprite.exists():
        print("Converting test sprite...")
        test_output = preview_dir / "00_COLOR_TEST.png"
        extract_southwest_sprite(str(test_sprite), str(test_output), palette_index=0, preview_mode=True)

    # Convert all theme sprites
    for i, (theme_name, sprite_file) in enumerate(themes, 1):
        print(f"[{i:2d}/50] Converting: {theme_name:30s}")

        output_file = preview_dir / f"{i:02d}_{theme_name}.png"
        try:
            extract_southwest_sprite(str(sprite_file), str(output_file), palette_index=0, preview_mode=True)
        except Exception as e:
            print(f"Error converting {theme_name}: {e}")

    print(f"\nPNG previews saved to: {preview_dir}")
    return preview_dir

def create_html_gallery(preview_dir, themes):
    """Create an HTML gallery for easy viewing"""
    print("\n" + "="*60)
    print("Creating HTML Gallery")
    print("="*60)

    html_content = """<!DOCTYPE html>
<html>
<head>
    <title>Cloud Strife - 50 Theme Previews</title>
    <style>
        body {
            background: #1a1a2e;
            color: #eee;
            font-family: 'Segoe UI', Arial, sans-serif;
            padding: 20px;
            margin: 0;
        }
        .header {
            text-align: center;
            margin-bottom: 30px;
            padding: 20px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border-radius: 10px;
            box-shadow: 0 4px 20px rgba(0,0,0,0.3);
        }
        h1 {
            margin: 0;
            font-size: 2.5em;
            text-shadow: 2px 2px 4px rgba(0,0,0,0.3);
        }
        .subtitle {
            margin-top: 10px;
            opacity: 0.9;
        }
        .test-section {
            background: #16213e;
            padding: 20px;
            margin-bottom: 30px;
            border-radius: 10px;
            box-shadow: 0 4px 15px rgba(0,0,0,0.3);
        }
        .gallery {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
            gap: 25px;
            padding: 20px;
        }
        .theme {
            text-align: center;
            background: #0f3460;
            padding: 15px;
            border-radius: 10px;
            transition: all 0.3s ease;
            box-shadow: 0 2px 10px rgba(0,0,0,0.3);
            cursor: pointer;
        }
        .theme:hover {
            transform: translateY(-5px) scale(1.05);
            background: #1e5f8e;
            box-shadow: 0 5px 20px rgba(102, 126, 234, 0.4);
        }
        .theme img {
            width: 100px;
            height: 100px;
            image-rendering: pixelated;
            image-rendering: crisp-edges;
            border: 3px solid #2a2a2a;
            background: #000;
            border-radius: 5px;
        }
        .theme-name {
            margin-top: 10px;
            font-size: 13px;
            font-weight: 500;
            color: #aaa;
            text-transform: capitalize;
        }
        .theme:hover .theme-name {
            color: #fff;
        }
        .legend {
            margin: 20px 0;
            padding: 15px;
            background: rgba(255,255,255,0.05);
            border-radius: 8px;
            border-left: 4px solid #667eea;
        }
        .color-key {
            margin: 8px 0;
            display: flex;
            align-items: center;
        }
        .color-box {
            display: inline-block;
            width: 24px;
            height: 24px;
            margin-right: 10px;
            border: 2px solid #333;
            border-radius: 4px;
        }
        .category-header {
            grid-column: 1 / -1;
            text-align: center;
            padding: 10px;
            margin: 10px 0;
            background: linear-gradient(90deg, transparent, #667eea, transparent);
            color: #fff;
            font-weight: bold;
            font-size: 1.2em;
        }
    </style>
</head>
<body>
    <div class="header">
        <h1>☁️ Cloud Strife Theme Gallery</h1>
        <div class="subtitle">50 Custom Color Variations</div>
    </div>
"""

    # Add test section if it exists
    test_file = preview_dir / "00_COLOR_TEST.png"
    if test_file.exists():
        html_content += """
    <div class="test-section">
        <h2>Color Mapping Test</h2>
        <div class="legend">
            <h3>Index Mapping Key:</h3>
            <div class="color-key"><span class="color-box" style="background:#FF0000"></span>RED = Buckles/Trim (indices 3-5)</div>
            <div class="color-key"><span class="color-box" style="background:#00FF00"></span>GREEN = Primary armor (indices 6-9)</div>
            <div class="color-key"><span class="color-box" style="background:#0000FF"></span>BLUE = Secondary armor (indices 20-31)</div>
            <div class="color-key"><span class="color-box" style="background:#FFFF00"></span>YELLOW = Extended armor (indices 35-47)</div>
            <div class="color-key"><span class="color-box" style="background:#FF00FF"></span>MAGENTA = Additional armor (indices 51-62)</div>
        </div>
        <div style="text-align:center; margin-top:20px;">
            <img src="00_COLOR_TEST.png" style="width:128px;height:128px;image-rendering:pixelated;border:3px solid #FFD700">
            <div style="margin-top:10px;color:#FFD700;font-weight:bold">COLOR MAPPING TEST</div>
        </div>
    </div>
"""

    html_content += """
    <h2 style="text-align:center; margin: 30px 0;">Theme Gallery</h2>
    <div class="gallery">
"""

    # Add category headers and themes
    categories = [
        ("FF7 Classic Themes", 0, 10),
        ("Materia-Inspired", 10, 22),
        ("FF7 Locations", 22, 32),
        ("Limit Breaks", 32, 40),
        ("Bosses & Enemies", 40, 45),
        ("Premium & Metallic", 45, 50),
    ]

    theme_idx = 0
    for cat_name, start, end in categories:
        html_content += f'<div class="category-header">{cat_name}</div>'

        for i in range(start, min(end, len(themes))):
            theme_name = themes[i][0]
            theme_idx += 1
            html_content += f"""
        <div class="theme">
            <img src="{theme_idx:02d}_{theme_name}.png" alt="{theme_name}">
            <div class="theme-name">{theme_name.replace('_', ' ')}</div>
        </div>
"""

    html_content += """
    </div>
</body>
</html>
"""

    gallery_file = preview_dir / "gallery.html"
    with open(gallery_file, 'w') as f:
        f.write(html_content)

    print(f"HTML gallery created: {gallery_file}")
    return gallery_file

def main():
    """Main execution"""
    print("="*60)
    print("CLOUD STRIFE THEME GENERATOR")
    print("Generating 50 themes with automatic PNG preview")
    print("="*60)

    # Step 1: Generate all themes
    themes = generate_all_themes()
    if not themes:
        print("Failed to generate themes. Exiting.")
        return

    # Step 2: Convert to PNGs
    preview_dir = convert_all_to_png(themes)

    # Step 3: Create HTML gallery
    gallery_file = create_html_gallery(preview_dir, themes)

    # Summary
    print("\n" + "="*60)
    print("COMPLETE! Generated 50 Cloud themes")
    print("="*60)
    print(f"\nTheme sprites: ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_cloud_*")
    print(f"PNG previews: {preview_dir}")
    print(f"HTML gallery: {gallery_file}")
    print("\nOpening preview gallery...")

    # Open the gallery in default browser
    if sys.platform == "win32":
        os.startfile(str(gallery_file))

if __name__ == "__main__":
    main()