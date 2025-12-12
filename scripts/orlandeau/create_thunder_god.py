#!/usr/bin/env python3
"""
Create the Thunder God Golden theme for Orlandeau.
Electric blue armor with gold undergarments and original brown cape.
"""

import os
import struct

# Original colors extracted from battle_oru_spr.bin
ORIGINAL_PALETTE = [
    (0, 0, 0),       # 0: Black shadow
    (40, 40, 32),    # 1: Dark shadow
    (224, 200, 168), # 2: Light outline
    (56, 48, 72),    # 3: Armor dark (will be replaced)
    (64, 64, 104),   # 4: Armor mid-dark (will be replaced)
    (96, 96, 152),   # 5: Armor mid-light (will be replaced)
    (144, 128, 176), # 6: Armor light (will be replaced)
    (88, 72, 56),    # 7: Belt/hair dark (will be replaced with gold)
    (160, 136, 112), # 8: Belt/hair light (will be replaced with gold)
    (136, 56, 32),   # 9: Accent dark (will be replaced with gold)
    (176, 64, 48),   # 10: Accent light (will be replaced with gold)
    (88, 48, 32),    # 11: Cape/skin darkest (keep original)
    (120, 72, 32),   # 12: Cape/skin dark (keep original)
    (168, 104, 48),  # 13: Cape/skin mid (keep original)
    (200, 128, 88),  # 14: Cape/skin light (keep original)
    (248, 184, 136), # 15: Skin tone (keep original)
]

# Thunder God theme colors
THUNDER_GOD = {
    "armor": [
        (0, 35, 70),    # Dark blue
        (0, 71, 139),   # Cobalt blue
        (30, 144, 255), # Dodger blue
        (135, 206, 235), # Sky blue
    ],
    "undergarments": [
        (139, 90, 0),    # Dark goldenrod
        (218, 165, 32),  # Goldenrod
        (255, 195, 0),   # Gold accent
        (255, 215, 0),   # Pure gold
    ]
}

def create_thunder_god():
    """Create the Thunder God theme for Orlandeau."""

    # Paths
    base_dir = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    sprites_dir = os.path.join(base_dir, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit")
    output_dir = os.path.join(sprites_dir, "sprites_orlandeau_thunder_god")

    # Remove old theme if it exists
    if os.path.exists(output_dir):
        import shutil
        shutil.rmtree(output_dir)

    os.makedirs(output_dir, exist_ok=True)

    # Orlandeau sprite variants
    sprite_variants = [
        "battle_oru_spr.bin",   # Main Orlandeau
        "battle_goru_spr.bin",  # Guest Orlandeau
        "battle_voru_spr.bin"   # Variant Orlandeau
    ]

    print("Creating Thunder God theme for Orlandeau:")
    print("=" * 50)
    print("- Electric blue armor")
    print("- Gold undergarments")
    print("- Original brown cape")
    print("=" * 50)

    for sprite_name in sprite_variants:
        source_path = os.path.join(sprites_dir, sprite_name)
        if not os.path.exists(source_path):
            print(f"  Warning: {sprite_name} not found")
            continue

        # Read original sprite
        with open(source_path, 'rb') as f:
            sprite_data = bytearray(f.read())

        # Apply the Thunder God theme to palette 0
        for idx in range(16):
            if 3 <= idx <= 6:
                # Electric blue armor
                armor_idx = idx - 3
                r, g, b = THUNDER_GOD["armor"][armor_idx]
            elif 7 <= idx <= 10:
                # Gold undergarments
                under_idx = idx - 7
                r, g, b = THUNDER_GOD["undergarments"][under_idx]
            else:
                # Keep original colors (shadows, cape, skin)
                r, g, b = ORIGINAL_PALETTE[idx]

            # Convert to 16-bit color format
            r_5bit = min(31, r >> 3)
            g_5bit = min(31, g >> 3)
            b_5bit = min(31, b >> 3)
            color_16bit = (b_5bit << 10) | (g_5bit << 5) | r_5bit

            # Write to palette 0
            offset = idx * 2
            sprite_data[offset:offset+2] = struct.pack('<H', color_16bit)

        # Save themed sprite
        output_path = os.path.join(output_dir, sprite_name)
        with open(output_path, 'wb') as f:
            f.write(sprite_data)

        print(f"  Created {sprite_name}")

    print("\n" + "=" * 50)
    print("Thunder God theme created successfully!")
    print(f"Location: {output_dir}")
    print("\nThis is Orlandeau's signature look:")
    print("- Electric blue armor befitting the Thunder God")
    print("- Gold accents representing divine power")
    print("- Classic brown cape maintaining his nobility")

if __name__ == "__main__":
    create_thunder_god()