#!/usr/bin/env python3
"""
Generate 50 diverse themes for Cloud Strife based on actual color mapping
Cloud uses a simple two-zone color scheme:
- Indices 3-5: Accents (shoulder pads, wrist guards, outlines, boot trim)
- Indices 6-9: Main clothing (pants, shoes, shirt, main body)
"""

import os
import sys
import struct
import shutil
from pathlib import Path
import colorsys

# Add parent directory to path for imports
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from convert_sprite_sw import extract_southwest_sprite

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
    """Generate 50 diverse color combinations for Cloud"""
    themes = []

    # 1. Classic FF7 Themes (10) - Main/Accent
    themes.extend([
        ("soldier_first_class", "#4169E1", "#FFD700"),  # SOLDIER blue with gold accents
        ("shinra_elite", "#1C1C1C", "#FF0000"),         # Black with red accents
        ("midgar_industrial", "#708090", "#FF8C00"),    # Steel gray with orange accents
        ("mako_infused", "#00FF7F", "#00CED1"),        # Mako green with cyan accents
        ("buster_blade", "#4B0082", "#C0C0C0"),        # Dark purple with silver accents
        ("avalanche_rebel", "#2F4F2F", "#8B4513"),     # Dark green with brown accents
        ("turks_uniform", "#191970", "#FFFFFF"),       # Midnight blue with white accents
        ("sephiroth_black", "#000000", "#C0C0C0"),     # Pure black with silver accents
        ("aerith_pink", "#FFB6C1", "#98FB98"),         # Light pink with pale green accents
        ("tifa_burgundy", "#800020", "#FFD700"),       # Burgundy with gold accents
    ])

    # 2. Materia-Inspired (12)
    themes.extend([
        ("fire_materia", "#FF4500", "#FFD700"),         # Fire red with gold trim
        ("ice_materia", "#00CED1", "#E0FFFF"),         # Ice blue with frost trim
        ("lightning_materia", "#FFD700", "#4B0082"),   # Electric yellow with purple trim
        ("earth_materia", "#8B4513", "#DAA520"),       # Earth brown with golden trim
        ("restore_materia", "#00FF00", "#FFFFFF"),     # Healing green with white trim
        ("time_materia", "#9370DB", "#FFD700"),        # Purple with gold trim
        ("gravity_materia", "#4B0082", "#000000"),     # Indigo with black trim
        ("holy_materia", "#F0F8FF", "#FFD700"),        # Pure white with gold trim
        ("meteor_materia", "#8B0000", "#FF4500"),      # Dark red with orange trim
        ("ultima_materia", "#FF1493", "#00FFFF"),      # Deep pink with cyan trim
        ("poison_materia", "#32CD32", "#8A2BE2"),      # Lime green with violet trim
        ("barrier_materia", "#4169E1", "#98FB98"),     # Royal blue with pale green trim
    ])

    # 3. Location-Based Themes (10)
    themes.extend([
        ("costa_del_sol", "#FF6347", "#40E0D0"),       # Coral with turquoise trim
        ("golden_saucer", "#FFD700", "#FF1493"),       # Gold with hot pink trim
        ("nibelheim_flame", "#FF4500", "#1C1C1C"),     # Fire orange with black trim
        ("northern_crater", "#483D8B", "#00CED1"),     # Dark slate with cyan trim
        ("ancient_forest", "#228B22", "#8B4513"),      # Forest green with brown trim
        ("temple_ancients", "#DAA520", "#4B0082"),     # Golden with indigo trim
        ("forgotten_capital", "#E0FFFF", "#4169E1"),    # Light cyan with royal blue trim
        ("weapon_emerald", "#50C878", "#002000"),      # Emerald with dark green trim
        ("cosmo_canyon", "#CD853F", "#8B0000"),        # Peru with dark red trim
        ("wutai_pagoda", "#DC143C", "#FFD700"),        # Crimson with gold trim
    ])

    # 4. Limit Break Themes (8)
    themes.extend([
        ("omnislash", "#1E90FF", "#FFFFFF"),           # Dodger blue with white trim
        ("meteorain", "#FF8C00", "#8B0000"),           # Dark orange with maroon trim
        ("finishing_touch", "#00BFFF", "#FFD700"),     # Sky blue with gold trim
        ("climhazzard", "#DC143C", "#FFD700"),         # Crimson with gold trim
        ("blade_beam", "#00FF00", "#0000FF"),          # Lime with blue trim
        ("cross_slash", "#800080", "#C0C0C0"),         # Purple with silver trim
        ("braver", "#FF6347", "#4169E1"),              # Tomato with royal blue trim
        ("apocalypse", "#8B008B", "#FF4500"),          # Dark magenta with orange trim
    ])

    # 5. Boss & Enemy Themes (5)
    themes.extend([
        ("jenova_synthesis", "#8B008B", "#00FF00"),    # Dark magenta with green trim
        ("ruby_weapon", "#8B0000", "#FF0000"),         # Dark red with bright red trim
        ("diamond_weapon", "#F0F8FF", "#4169E1"),      # Alice blue with royal trim
        ("bahamut_zero", "#191970", "#FFD700"),        # Midnight blue with gold trim
        ("knights_round", "#696969", "#FFD700"),       # Dim gray with gold trim
    ])

    # 6. Premium & Metallic (5)
    themes.extend([
        ("platinum_edge", "#E5E4E2", "#4169E1"),       # Platinum with royal blue trim
        ("rose_gold_elite", "#B76E79", "#FFD700"),     # Rose gold with gold trim
        ("bronze_warrior", "#CD7F32", "#8B4513"),      # Bronze with saddle brown trim
        ("titanium_core", "#878681", "#000000"),       # Titanium with black trim
        ("copper_strike", "#B87333", "#2F4F4F"),       # Copper with dark slate trim
    ])

    return themes[:50]  # Ensure exactly 50 themes

def create_themed_sprite(source_file, theme_name, main_color, accent_color):
    """Create a themed sprite with the given colors

    Based on color test results:
    - Accent color (RED test) goes to indices 3-5 (shoulder pads, wrist guards, outlines)
    - Main color (GREEN test) goes to indices 6-9 (pants, shoes, shirt, main body)
    """

    # Read original sprite
    with open(source_file, 'rb') as f:
        data = bytearray(f.read())

    main_rgb = hex_to_rgb(main_color)
    accent_rgb = hex_to_rgb(accent_color) if accent_color else main_rgb

    # Create slight variations for depth within the main clothing
    main_shadow = adjust_brightness(main_rgb, 0.75)  # 25% darker for shadows
    main_dark = adjust_brightness(main_rgb, 0.5)     # 50% darker for deep shadows

    # Cloud's actual color mapping based on test:
    accent_indices = [3, 4, 5]  # Shoulder pads, wrist guards, outlines, boot trim
    main_indices = [6, 7, 8, 9]  # Main clothing - pants, shoes, shirt, body

    # Apply colors to first 8 palettes (unit sprites)
    for palette_idx in range(8):
        base_offset = palette_idx * 32

        # Apply accent colors to trim/outlines
        for idx in accent_indices:
            offset = base_offset + (idx * 2)
            if offset + 1 < 512:
                bgr555 = rgb_to_bgr555(*accent_rgb)
                struct.pack_into('<H', data, offset, bgr555)

        # Apply main colors with slight variations for depth
        for i, idx in enumerate(main_indices):
            offset = base_offset + (idx * 2)
            if offset + 1 < 512:
                # Add subtle variation to create depth
                if i == 2:  # Index 8 - slightly darker
                    bgr555 = rgb_to_bgr555(*main_shadow)
                elif i == 3:  # Index 9 - darkest for deep shadows
                    bgr555 = rgb_to_bgr555(*main_dark)
                else:  # Indices 6-7 - main color
                    bgr555 = rgb_to_bgr555(*main_rgb)
                struct.pack_into('<H', data, offset, bgr555)

    return data

def generate_all_themes():
    """Generate all 50 Cloud themes"""
    print("\n" + "="*60)
    print("Generating 50 Cloud Themes")
    print("Based on actual color mapping:")
    print("  - Accent colors -> Shoulder pads, wrist guards, outlines")
    print("  - Main colors -> Pants, shoes, shirt, main body")
    print("="*60)

    base_dir = Path(__file__).parent.parent.parent
    source_file = base_dir / "ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_original/battle_cloud_spr.bin"

    if not source_file.exists():
        print(f"Error: Cloud sprite not found at {source_file}")
        return []

    themes = generate_theme_colors()
    created_themes = []

    for i, (theme_name, main, accent) in enumerate(themes, 1):
        print(f"[{i:2d}/50] Creating theme: {theme_name:30s}")

        # Create theme directory
        theme_dir = base_dir / f"ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_cloud_{theme_name}"
        theme_dir.mkdir(exist_ok=True, parents=True)

        # Generate themed sprite
        sprite_data = create_themed_sprite(source_file, theme_name, main, accent)

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
    preview_dir = base_dir / "ColorMod/Resources/Previews/Cloud_Themes_Final"
    preview_dir.mkdir(exist_ok=True, parents=True)

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
        .mapping-note {
            background: #16213e;
            padding: 15px;
            margin-bottom: 20px;
            border-radius: 10px;
            text-align: center;
            border-left: 4px solid #667eea;
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
        <h1>Cloud Strife Theme Gallery</h1>
        <div class="subtitle">50 Custom Color Variations</div>
    </div>

    <div class="mapping-note">
        <strong>Color Mapping:</strong> Accent colors appear on shoulder pads, wrist guards, and outlines<br>
        Main colors cover pants, shoes, shirt, and body
    </div>

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
    with open(gallery_file, 'w', encoding='utf-8') as f:
        f.write(html_content)

    print(f"HTML gallery created: {gallery_file}")
    return gallery_file

def main():
    """Main execution"""
    print("="*60)
    print("CLOUD STRIFE THEME GENERATOR - FINAL VERSION")
    print("Generating 50 themes based on actual color mapping")
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