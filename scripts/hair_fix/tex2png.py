#!/usr/bin/env python3
"""Render a TEX file to PNG (stdlib only -- no PIL).
TEX: 0x800 header, 4-bit indexed, 512 wide, high nibble = first pixel.
Maps indices via WMM's palette. index 0 (transparent) -> magenta so the
sprite silhouette is obvious. Optional --blackskin sets idx 14/15 black.
Usage: python tex2png.py <tex.bin> <out.png> [scale] [--blackskin]"""
import sys
import zlib
import struct

HEADER = 0x800
WIDTH = 512

# WMM palette (from the HD BMP), index -> (r,g,b)
PAL = [
    (0, 0, 0), (40, 40, 32), (224, 224, 208), (80, 72, 64),
    (120, 112, 96), (160, 152, 136), (200, 192, 176), (112, 48, 32),
    (184, 48, 32), (232, 72, 32), (96, 56, 16), (144, 104, 40),
    (200, 152, 64), (144, 88, 32), (184, 128, 64), (232, 184, 120),
]
BG = (255, 0, 255)  # render transparent (index 0) as magenta


def decode(path):
    data = open(path, 'rb').read()[HEADER:]
    n = len(data) * 2
    h = n // WIDTH
    g = [[0] * WIDTH for _ in range(h)]
    for i, b in enumerate(data):
        o = i * 2
        g[o // WIDTH][o % WIDTH] = (b >> 4) & 0xF
        g[(o + 1) // WIDTH][(o + 1) % WIDTH] = b & 0xF
    return g, h


def write_png(path, flat_rgb, w, h):
    raw = bytearray()
    for y in range(h):
        raw.append(0)  # filter type 0 (none)
        row = flat_rgb[y * w:(y + 1) * w]
        for (r, g, b) in row:
            raw += bytes((r, g, b))

    def chunk(typ, data):
        return (struct.pack('>I', len(data)) + typ + data +
                struct.pack('>I', zlib.crc32(typ + data) & 0xffffffff))

    png = b'\x89PNG\r\n\x1a\n'
    png += chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0))
    png += chunk(b'IDAT', zlib.compress(bytes(raw), 9))
    png += chunk(b'IEND', b'')
    open(path, 'wb').write(png)


def main():
    inp, outp = sys.argv[1], sys.argv[2]
    scale = 3
    for arg in sys.argv[3:]:
        if not arg.startswith('--'):
            scale = int(arg)
    pal = list(PAL)
    if '--blackskin' in sys.argv:
        pal[14] = pal[15] = (0, 0, 0)

    grid, h = decode(inp)
    W, H = WIDTH * scale, h * scale
    flat = [BG] * (W * H)
    for y in range(h):
        for x in range(WIDTH):
            idx = grid[y][x]
            rgb = BG if idx == 0 else pal[idx]
            for sy in range(scale):
                base = (y * scale + sy) * W + x * scale
                for sx in range(scale):
                    flat[base + sx] = rgb
    write_png(outp, flat, W, H)
    print(f"  wrote {outp}  ({W}x{H}, scale {scale})")


if __name__ == '__main__':
    main()
