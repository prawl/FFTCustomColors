#!/usr/bin/env python3
"""
Create a test sprite with distinct colors to identify body parts.
"""

from PIL import Image
import os

def create_test_colors():
    """Create test colors to identify what each color maps to."""

    # Based on user feedback:
    # Ch1: Blues -> RED (armor), Browns -> BLUE (hair/face/under armor shirt/knee centers/back stripes), Golds -> GREEN (gloves/legs), Grays -> YELLOW (outlines of hands/feet/legs/eyebrows)
    # Ch2: Purples -> RED (armor), Browns/Tans -> GREEN (hair/face/gloves), Other browns -> BLUE (accents), Grays -> YELLOW (body outlines except head)
    # Ch3/4: Teals -> RED (shoulders/arms/hands/legs/boots), Browns -> GREEN (hair/face/chest under armor), Other -> BLUE (hair tips/pelvic under armor/thong/ankle stripes), Grays -> YELLOW (outlines)

    color_map = {
        # Chapter 1 Blues (main armor) -> BRIGHT RED
        (48, 72, 104): (255, 0, 0),      # Main armor -> Pure Red
        (56, 96, 136): (200, 0, 0),      # Light armor -> Dark Red
        (40, 56, 80): (150, 0, 0),       # Dark armor -> Darker Red
        (80, 128, 184): (255, 50, 50),   # Bright armor -> Light Red

        # Chapter 2 Purples (main armor) -> BRIGHT RED
        (48, 40, 80): (255, 0, 0),       # Dark purple armor -> Pure Red
        (88, 64, 120): (200, 0, 0),      # Medium purple armor -> Dark Red
        (128, 96, 200): (255, 50, 50),   # Light purple armor -> Light Red

        # Chapter 3/4 Teals (shoulders/arms/hands/legs/boots) -> BRIGHT RED
        (32, 64, 88): (255, 0, 0),       # Dark teal armor -> Pure Red
        (40, 96, 120): (200, 0, 0),      # Medium teal armor -> Dark Red
        (64, 136, 152): (255, 50, 50),   # Light teal armor -> Light Red

        # Ch3/4 Main Browns (hair/face/chest under armor) -> BRIGHT GREEN
        (64, 56, 56): (0, 255, 0),       # Ch3/4 main brown -> Pure Green
        (72, 40, 8): (0, 255, 0),        # Ch3/4 dark brown hair -> Pure Green
        (104, 72, 24): (0, 200, 0),      # Ch3/4 medium brown -> Dark Green
        (112, 96, 80): (0, 200, 0),      # Ch3/4 under armor brown -> Dark Green
        (128, 56, 8): (0, 200, 0),       # Ch3/4 dark tan -> Dark Green

        # Ch3/4 Accent Browns (hair tips/pelvic/thong/ankles) -> BRIGHT BLUE
        (184, 120, 40): (0, 0, 255),     # Ch3/4 hair tips -> Pure Blue
        (200, 136, 80): (0, 0, 200),     # Ch3/4 under armor accent -> Dark Blue
        (232, 192, 128): (50, 50, 255),  # Ch3/4 light accent -> Light Blue
        (176, 160, 136): (0, 0, 150),    # Ch3/4 pale accent -> Darker Blue
        (216, 160, 72): (0, 0, 255),     # Ch3/4 bright accent -> Pure Blue

        # Ch2 Browns/Tans (hair/face/gloves) -> BRIGHT GREEN (only for Ch2)
        (72, 64, 48): (0, 200, 0),       # Ch2 brown -> Dark Green

        # Ch1 Browns (hair/face) + Ch2 accents -> BRIGHT BLUE
        (72, 48, 40): (0, 0, 255),       # Ch1 Dark brown -> Pure Blue
        (104, 64, 32): (0, 0, 150),      # Ch1 Brown shadows -> Darker Blue
        (160, 104, 40): (100, 100, 255), # Ch1 Light brown -> Pale Blue
        (144, 80, 40): (0, 0, 225),      # Ch1 Medium brown -> Medium Blue
        (112, 88, 24): (0, 0, 225),      # Ch2 accent brown -> Medium Blue

        # Grays (outlines) -> YELLOW
        (40, 40, 32): (255, 255, 0),     # Dark gray -> Pure Yellow
        (224, 224, 216): (200, 200, 0),  # Light gray -> Dark Yellow
        (224, 216, 192): (200, 200, 0),  # Ch2+ light gray -> Dark Yellow
    }

    input_dir = "C:/Users/ptyRa/AppData/Local/FFTSpriteToolkit/working/extracted_sprites"
    output_dir = "C:/Users/ptyRa/Dev/FFTColorCustomizer/ramza_test_colors"

    os.makedirs(output_dir, exist_ok=True)

    # Process all Ramza chapters - Note: File names say Ch23 but it's actually Ch2, and Ch4 is actually Ch3/4
    ramza_files = [
        ("830_Ramuza_Ch1_hd.bmp", "830_Ramuza_Ch1_hd_TEST.png", "Chapter 1"),
        ("831_Ramuza_Ch1_hd.bmp", "831_Ramuza_Ch1_hd_TEST.png", "Chapter 1 (alt)"),
        ("832_Ramuza_Ch23_hd.bmp", "832_Ramuza_Ch2_hd_TEST.png", "Chapter 2"),
        ("833_Ramuza_Ch23_hd.bmp", "833_Ramuza_Ch2_hd_TEST.png", "Chapter 2 (alt)"),
        ("834_Ramuza_Ch4_hd.bmp", "834_Ramuza_Ch34_hd_TEST.png", "Chapter 3/4"),
        ("835_Ramuza_Ch4_hd.bmp", "835_Ramuza_Ch34_hd_TEST.png", "Chapter 3/4 (alt)"),
    ]

    for input_file, output_file, chapter_name in ramza_files:
        input_path = os.path.join(input_dir, input_file)
        output_path = os.path.join(output_dir, output_file)

        if not os.path.exists(input_path):
            print(f"Skipping {input_file} - file not found")
            continue

        print(f"\nProcessing {chapter_name}: {input_file}")
        print("-" * 50)

        img = Image.open(input_path)
        if img.mode != 'RGBA':
            img = img.convert('RGBA')

        pixels = img.load()
        width, height = img.size

        # Track what we replace
        armor_pixels = 0
        hair_face_pixels = 0
        gloves_legs_pixels = 0
        metal_pixels = 0

        for y in range(height):
            for x in range(width):
                r, g, b, a = pixels[x, y]
                old_color = (r, g, b)

                if old_color in color_map:
                    new_color = color_map[old_color]
                    pixels[x, y] = (*new_color, a)

                    # Count what we're replacing (updated color detection)
                    if new_color[0] > 150 and new_color[1] < 100 and new_color[2] < 100:  # Red
                        armor_pixels += 1
                    elif new_color[2] > 150 and new_color[0] < 100 and new_color[1] < 100:  # Blue
                        hair_face_pixels += 1
                    elif new_color[1] > 150 and new_color[0] < 100 and new_color[2] < 100:  # Green
                        gloves_legs_pixels += 1
                    elif new_color[0] > 150 and new_color[1] > 150:  # Yellow
                        metal_pixels += 1

        print(f"Pixels replaced:")
        print(f"  Armor/chest/arms (RED): {armor_pixels}")
        print(f"  Hair/face/under armor (BLUE): {hair_face_pixels}")
        print(f"  Gloves/legs/feet (GREEN): {gloves_legs_pixels}")
        print(f"  Metal/accessories (YELLOW): {metal_pixels}")

        img.save(output_path)
        print(f"Saved to: {output_path}")

    # Save a guide file
    preview_path = os.path.join(output_dir, "COLOR_MAP_GUIDE.txt")
    with open(preview_path, 'w') as f:
        f.write("RAMZA COLOR TEST MAP\n")
        f.write("====================\n\n")
        f.write("Color Mappings (Updated based on visual analysis):\n")
        f.write("---------------------------------------------------\n")
        f.write("Chapter 1:\n")
        f.write("  RED = Main armor (chest/arms)\n")
        f.write("  BLUE = Hair/face/under armor shirt/knee centers/back stripes\n")
        f.write("  GREEN = Gloves/legs/feet\n")
        f.write("  YELLOW = Outlines (hands/feet/legs/eyebrows)\n\n")
        f.write("Chapter 2:\n")
        f.write("  RED = Main armor (chest/arms)\n")
        f.write("  GREEN = Hair/face/gloves\n")
        f.write("  BLUE = Accent colors (hair/gloves/legs)\n")
        f.write("  YELLOW = Body outlines (shoulders/arms/legs/feet)\n\n")
        f.write("Chapter 3/4:\n")
        f.write("  RED = Shoulders/arms/hands/legs/boots\n")
        f.write("  GREEN = Hair/face/chest under armor\n")
        f.write("  BLUE = Hair tips/pelvic under armor/thong/ankle stripes\n")
        f.write("  YELLOW = Outlines (hands/legs/feet)\n\n")
        f.write("Files Generated:\n")
        f.write("----------------\n")
        for _, output_file, chapter_name in ramza_files:
            f.write(f"- {output_file}: {chapter_name}\n")
        f.write("\nUse these test images to identify what parts of Ramza map to which colors!\n")

    print("\n" + "=" * 60)
    print("ALL TEST IMAGES CREATED!")
    print("=" * 60)
    print("\nColor mapping summary:")
    print("Chapter 1:")
    print("  RED = Main armor (chest/arms)")
    print("  BLUE = Hair/face/under armor shirt/knee centers/back stripes")
    print("  GREEN = Gloves/legs/feet")
    print("  YELLOW = Outlines (hands/feet/legs/eyebrows)")
    print("\nChapter 2:")
    print("  RED = Main armor (chest/arms)")
    print("  GREEN = Hair/face/gloves")
    print("  BLUE = Accent colors (hair/gloves/legs)")
    print("  YELLOW = Body outlines (shoulders/arms/legs/feet)")
    print("\nChapter 3/4:")
    print("  RED = Shoulders/arms/hands/legs/boots")
    print("  GREEN = Hair/face/chest under armor")
    print("  BLUE = Hair tips/pelvic under armor/thong/ankle stripes")
    print("  YELLOW = Outlines (hands/legs/feet)")
    print("\nCheck the ramza_test_colors folder to view all test images!")

if __name__ == "__main__":
    create_test_colors()