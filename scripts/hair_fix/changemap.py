#!/usr/bin/env python3
"""Before/after diff render for TEX and HD BMP sheets.

The review artifact for every remap pass: renders the BEFORE sheet dimmed,
paints every changed pixel RED, and every remaining pixel of --src CYAN.
Red on a face = the fix landing; red on gear = over-catch; cyan shows what
a lower threshold would still have to argue about.

Usage:
  python changemap.py <before> <after> <out.png> [--src 14] [--scale 2]

Auto-detects format: 'BM' magic = HD BMP (rows bottom-up, LOW nibble =
left pixel, per-file data offset), else TEX (0x800 header, rows top-down,
HIGH nibble = left pixel, 512 wide). Both inputs must be the same format
and size. Requires PIL only for the PNG write.
"""
import struct
import sys

TEX_HEADER = 0x800
TEX_WIDTH = 512

# Approximate grayscale-ish base per index so structure stays readable in
# the dimmed render without pretending to know the real palette.
BASE = [(0, 0, 0)] + [(40 + i * 14, 36 + i * 13, 32 + i * 12) for i in range(1, 16)]


def decode(path):
    d = open(path, 'rb').read()
    if d[:2] == b'BM':
        off = struct.unpack_from('<I', d, 10)[0]
        w = struct.unpack_from('<i', d, 18)[0]
        h = struct.unpack_from('<i', d, 22)[0]
        rowbytes = w // 2
        g = [[0] * w for _ in range(h)]
        for y in range(h):
            base = off + (h - 1 - y) * rowbytes
            row = g[y]
            for x in range(w):
                b = d[base + x // 2]
                row[x] = b & 15 if x % 2 == 0 else (b >> 4) & 15
        return g, w, h
    px = d[TEX_HEADER:]
    h = len(px) * 2 // TEX_WIDTH
    g = [[0] * TEX_WIDTH for _ in range(h)]
    for i, b in enumerate(px):
        o = i * 2
        g[o // TEX_WIDTH][o % TEX_WIDTH] = (b >> 4) & 15
        g[(o + 1) // TEX_WIDTH][(o + 1) % TEX_WIDTH] = b & 15
    return g, TEX_WIDTH, h


def opt(name, default):
    return sys.argv[sys.argv.index(name) + 1] if name in sys.argv else default


def main():
    if len(sys.argv) < 4:
        print(__doc__)
        sys.exit(1)
    from PIL import Image
    before, w, h = decode(sys.argv[1])
    after, w2, h2 = decode(sys.argv[2])
    if (w, h) != (w2, h2):
        sys.exit("size mismatch: %dx%d vs %dx%d" % (w, h, w2, h2))
    src = int(opt('--src', '14'))
    scale = int(opt('--scale', '2'))
    im = Image.new("RGB", (w, h))
    p = im.load()
    changed = 0
    for y in range(h):
        for x in range(w):
            r, g_, b = BASE[before[y][x]]
            p[x, y] = (r // 3, g_ // 3, b // 3)
            if before[y][x] != after[y][x]:
                p[x, y] = (255, 40, 40)
                changed += 1
            elif before[y][x] == src:
                p[x, y] = (0, 255, 255)
    if scale != 1:
        im = im.resize((w * scale, h * scale), Image.NEAREST)
    im.save(sys.argv[3])
    print("changed px: %d  (red=changed, cyan=remaining idx %d)  wrote %s"
          % (changed, src, sys.argv[3]))


if __name__ == '__main__':
    main()
