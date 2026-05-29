#!/usr/bin/env python3
"""Decode a monster sprite .bin and render two PNGs to help map palette index -> body part:
  <name>_indexmap.png  -- every palette index drawn as a DISTINCT color
  <name>_real.png      -- drawn with the bin's real palette (default palette 0)
Also prints the pixel count per index (big = body, tiny = eye/detail, near-black = outline).

Bin format (FFT IVC unit sprite): first 512 bytes = 16 palettes x 16 colors x 2 bytes (BGR555);
pixel data starts at offset 512, 4-bit indexed, 256-wide sheet, EVEN pixel = low nibble,
ODD pixel = high nibble. Index 0 is transparent.

Usage: python render_index_map.py <battle_x_spr.bin> <out_dir> [palette_index=0] [scale=2]
"""
import sys, os, struct, zlib
from collections import Counter

PAL_BYTES, WIDTH = 512, 256
# 15 distinct colors for indices 1..15 (0 = transparent)
DISTINCT = {1:(230,30,30),2:(255,255,255),3:(40,80,240),4:(240,230,40),5:(230,40,230),
            6:(40,220,220),7:(245,140,20),8:(150,230,40),9:(150,40,200),10:(40,220,40),
            11:(245,90,170),12:(20,150,140),13:(140,80,40),14:(130,130,130),15:(20,30,120)}

def main():
    binp, outdir = sys.argv[1], sys.argv[2]
    pal_index = int(sys.argv[3]) if len(sys.argv) > 3 else 0
    scale = int(sys.argv[4]) if len(sys.argv) > 4 else 2
    os.makedirs(outdir, exist_ok=True)
    d = open(binp, 'rb').read()
    npx = (len(d) - PAL_BYTES) * 2
    h = npx // WIDTH
    base = os.path.splitext(os.path.basename(binp))[0]

    def idx(i):
        b = d[PAL_BYTES + (i // 2)]
        return (b & 0x0F) if i % 2 == 0 else ((b >> 4) & 0x0F)

    def real(i):
        k = idx(i)
        if k == 0: return (0, 0, 0, 0)
        o = pal_index * 32 + k * 2
        v = d[o] | (d[o + 1] << 8)
        return ((v & 31) * 8, ((v >> 5) & 31) * 8, ((v >> 10) & 31) * 8, 255)

    def dist(i):
        k = idx(i)
        return (0, 0, 0, 0) if k == 0 else (*DISTINCT[k], 255)

    def write_png(path, fn):
        def ch(t, da):
            c = t + da
            return struct.pack('>I', len(da)) + c + struct.pack('>I', zlib.crc32(c) & 0xffffffff)
        raw = bytearray()
        for y in range(h):
            line = bytearray()
            for x in range(WIDTH):
                r, g, b, a = fn(y * WIDTH + x)
                line += bytes((r, g, b, a))
            big = bytearray()
            for x in range(WIDTH):
                big += line[x * 4:x * 4 + 4] * scale
            for _ in range(scale):
                raw.append(0); raw += big
        png = (b'\x89PNG\r\n\x1a\n'
               + ch(b'IHDR', struct.pack('>IIBBBBB', WIDTH * scale, h * scale, 8, 6, 0, 0, 0))
               + ch(b'IDAT', zlib.compress(bytes(raw), 9)) + ch(b'IEND', b''))
        open(path, 'wb').write(png)

    write_png(os.path.join(outdir, base + '_indexmap.png'), dist)
    write_png(os.path.join(outdir, base + '_real.png'), real)
    counts = Counter(idx(i) for i in range(npx))
    print(f"sheet {WIDTH}x{h}, palette {pal_index}")
    print("pixel count per index:", dict(sorted(counts.items())))
    print("legend:", DISTINCT)


if __name__ == '__main__':
    main()
