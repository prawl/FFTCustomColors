#!/usr/bin/env python3
"""
Repack modified textures back into g2d.dat format.
Based on script from FFHacktics community.
"""

import os
import struct
import zlib
from pathlib import Path

def repack_g2d(input_dir="extract/TEXTURES", output_file="g2d_new.dat"):
    """Repack texture files into g2d.dat format"""

    input_path = Path(input_dir)
    if not input_path.exists():
        print(f"Error: {input_path} not found!")
        return False

    print(f"Repacking textures from {input_path}...")

    # Get all tex files in order
    tex_files = []
    i = 0
    while True:
        tex_file = input_path / f"tex_{i}.bin"
        if tex_file.exists():
            tex_files.append(tex_file)
            i += 1
        else:
            # Check if we've found any files or if we're just at a gap
            if i > 1000:  # Reasonable limit
                break
            i += 1
            continue

    if not tex_files:
        print("Error: No tex_X.bin files found!")
        return False

    print(f"Found {len(tex_files)} texture files to pack")

    # Build the new g2d.dat file
    file_headers = []
    file_sizes = []
    file_total = len(tex_files)

    # Start offset after header (2048 bytes)
    current_offset = 2048
    file_headers.append(current_offset)

    # Main data buffer
    data_buffer = bytearray()

    for i, tex_file in enumerate(tex_files):
        # Read texture data
        with open(tex_file, 'rb') as f:
            texture_data = f.read()

        original_size = len(texture_data)

        # Determine if we should compress
        if original_size < 33:
            # Don't compress small files
            compressed = 0
            compressed_data = texture_data
        else:
            # Compress the data
            compressed = 2
            compressed_data = zlib.compress(texture_data)

        # File entry size (16 byte header + data)
        file_size = len(compressed_data) + 16
        file_sizes.append(file_size)

        # Calculate padding to 2048 byte boundary
        padding = (2048 - (file_size % 2048)) % 2048
        padded_data = compressed_data + bytes(padding)

        # Create file entry header (16 bytes)
        entry_header = bytearray()
        entry_header.extend(b'YOX\x00')  # Magic
        entry_header.extend(struct.pack('B', compressed))  # Compression flag
        entry_header.extend(bytes(3))  # Padding
        entry_header.extend(struct.pack('<I', original_size))  # Original size
        entry_header.extend(bytes(4))  # More padding

        # Add to buffer
        data_buffer.extend(entry_header)
        data_buffer.extend(padded_data)

        # Calculate next offset
        current_offset += len(entry_header) + len(padded_data)
        if i < file_total - 1:
            file_headers.append(current_offset + ((i + 1) * 16))

        if (i + 1) % 100 == 0:
            print(f"  Processed {i + 1} files...")

    # Build index
    index_buffer = bytearray()
    for i in range(file_total):
        index_buffer.extend(struct.pack('<I', file_headers[i]))  # File offset
        index_buffer.extend(struct.pack('<I', file_sizes[i]))    # File size (24-bit + 8-bit)
        index_buffer.extend(bytes(8))  # Padding

    # Build main header
    header_buffer = bytearray()
    header_buffer.extend(b'YOX\x00')  # Magic
    header_buffer.extend(bytes(4))  # Padding
    index_offset = 2048 + len(data_buffer)
    header_buffer.extend(struct.pack('<I', index_offset))  # Index offset
    header_buffer.extend(struct.pack('<H', file_total))    # Number of files
    header_buffer.extend(bytes(2))  # Padding

    # Pad header to 2048 bytes
    header_buffer.extend(bytes(2048 - len(header_buffer)))

    # Write output file
    with open(output_file, 'wb') as f:
        f.write(header_buffer)
        f.write(data_buffer)
        f.write(index_buffer)

    output_size = len(header_buffer) + len(data_buffer) + len(index_buffer)
    print(f"Successfully created {output_file} ({output_size:,} bytes)")
    print(f"Original g2d.dat is ~13.9MB, new file is {output_size/1024/1024:.1f}MB")

    return True

def main():
    """Main repacking function"""

    print("=" * 60)
    print("G2D.DAT TEXTURE REPACKER")
    print("=" * 60)

    # Check if textures exist
    texture_dir = Path("extract/TEXTURES")
    if not texture_dir.exists():
        print(f"Error: {texture_dir} not found!")
        print("Please run g2d_extract.py first to extract textures")
        return

    # Count texture files
    tex_count = len(list(texture_dir.glob("tex_*.bin")))
    print(f"Found {tex_count} texture files in {texture_dir}")

    if tex_count == 0:
        print("No texture files found to repack!")
        return

    # Repack the textures
    if repack_g2d():
        print("\n" + "=" * 60)
        print("Repacking complete!")
        print("Next steps:")
        print("1. Replace the original g2d.dat with g2d_new.dat")
        print("2. Test in game to see if modifications work")
        print("3. Make sure to backup the original g2d.dat!")

if __name__ == "__main__":
    main()