#!/usr/bin/env python3
"""
Extract g2d.dat file to individual texture files.
Based on script from FFHacktics community.
"""

import os
import struct
import zlib
from pathlib import Path

def extract_g2d(input_path="g2d.dat", output_dir="extract"):
    """Extract all textures from g2d.dat file"""

    if not os.path.exists(input_path):
        print(f"Error: {input_path} not found!")
        return False

    print(f"Extracting {input_path}...")

    # Create output directories
    output_path = Path(output_dir)
    textures_path = output_path / "TEXTURES"
    textures_path.mkdir(parents=True, exist_ok=True)

    with open(input_path, 'rb') as input_file:
        # Read header
        input_file.seek(8)  # Seeking to location of the Index List
        index_location = struct.unpack('<I', input_file.read(4))[0]
        print(f"Index location: 0x{index_location:08X}")

        input_file.seek(12)  # Seeking to number of files in the Index
        num_files = struct.unpack('<H', input_file.read(2))[0]
        print(f"Number of files: {num_files}")

        # Read index
        input_file.seek(index_location)
        tex_list = []

        for i in range(num_files):
            file_offset = struct.unpack('<I', input_file.read(4))[0]
            file_size = int.from_bytes(input_file.read(3), 'little')
            tex_list.append((file_offset, file_size))
            input_file.seek(9, 1)  # Skip to next entry

        # Extract files
        extracted_count = 0
        for i in range(num_files):
            file_offset, file_size = tex_list[i]

            # Read compression flag
            input_file.seek(file_offset + 4)
            is_compressed = struct.unpack('B', input_file.read(1))[0]

            # Skip to data (after 16-byte header)
            input_file.seek(file_offset + 16)

            # Read and decompress if needed
            data_size = file_size - 16  # Subtract header size
            if data_size <= 0:
                continue

            raw_data = input_file.read(data_size)

            if is_compressed == 2:
                try:
                    buffer = zlib.decompress(raw_data)
                except:
                    print(f"Warning: Failed to decompress file {i}")
                    buffer = raw_data
            else:
                buffer = raw_data

            # Skip padding files
            if len(buffer) < 33:
                continue

            # Save file
            filename = textures_path / f"tex_{i}.bin"
            with open(filename, 'wb') as output_file:
                output_file.write(buffer)

            extracted_count += 1
            if extracted_count % 100 == 0:
                print(f"  Extracted {extracted_count} files...")

        print(f"Successfully extracted {extracted_count} texture files to {textures_path}")

        # Save metadata for repacking
        metadata_file = output_path / "metadata.txt"
        with open(metadata_file, 'w') as f:
            f.write(f"num_files={num_files}\n")
            f.write(f"index_location=0x{index_location:08X}\n")
            f.write(f"extracted_count={extracted_count}\n")
            for i, (offset, size) in enumerate(tex_list):
                f.write(f"file_{i}=offset:0x{offset:08X},size:{size}\n")

        print(f"Metadata saved to {metadata_file}")
        return True

def find_g2d_dat():
    """Try to find g2d.dat in common locations"""

    possible_paths = [
        # Current directory
        "g2d.dat",
        # Game installation paths
        r"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\data\enhanced\0000.pac",
        # Check if it's in a Downloads folder
        r"C:\Users\ptyRa\Downloads\g2d.dat",
        # Check OneDrive
        r"C:\Users\ptyRa\OneDrive\Desktop\g2d.dat",
    ]

    for path in possible_paths:
        if os.path.exists(path):
            print(f"Found: {path}")
            return path

    print("Could not find g2d.dat automatically")
    print("g2d.dat is likely packed inside 0000.pac file")
    print("You may need to extract it from the .pac file first")
    return None

def main():
    """Main extraction function"""

    print("=" * 60)
    print("G2D.DAT TEXTURE EXTRACTOR")
    print("=" * 60)

    # Try to find g2d.dat
    g2d_path = find_g2d_dat()

    if not g2d_path:
        print("\nPlease provide the path to g2d.dat file")
        return

    # For now, check if it's in the pac file
    if g2d_path.endswith(".pac"):
        print(f"\ng2d.dat is inside {g2d_path}")
        print("The file needs to be extracted from the .pac archive first")
        print("This would require a .pac extractor tool")
        return

    # Extract the g2d.dat file
    if extract_g2d(g2d_path):
        print("\n" + "=" * 60)
        print("Extraction complete!")
        print("Next steps:")
        print("1. Check extract/TEXTURES/ for the texture files")
        print("2. Identify which tex_XXX.bin files correspond to Ramza")
        print("3. Modify the textures as needed")
        print("4. Use g2d_repack.py to create a new g2d.dat")

if __name__ == "__main__":
    main()