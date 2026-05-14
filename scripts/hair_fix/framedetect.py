#!/usr/bin/env python3
"""Detect TEX content bands (sprite rows) by finding gap rows between sprites.
A row is a 'gap' if it has <= TOL non-transparent pixels. Content bands are
runs of non-gap rows. Usage: python framedetect.py <tex.bin> [tex.bin ...]"""
import sys

TOL = 3  # a row with this many or fewer non-zero px counts as a gap


def decode(path):
    data = open(path, 'rb').read()[0x800:]
    h = len(data) * 2 // 512
    g = [[0] * 512 for _ in range(h)]
    for i, b in enumerate(data):
        o = i * 2
        g[o // 512][o % 512] = (b >> 4) & 0xF
        g[(o + 1) // 512][(o + 1) % 512] = b & 0xF
    return g, h


def bands(g, h):
    counts = [sum(1 for v in g[y] if v != 0) for y in range(h)]
    is_content = [c > TOL for c in counts]
    out = []
    y = 0
    while y < h:
        if is_content[y]:
            s = y
            while y < h and is_content[y]:
                y += 1
            out.append((s, y - 1))
        else:
            y += 1
    return out


for path in sys.argv[1:]:
    g, h = decode(path)
    bs = bands(g, h)
    print(path)
    print("  %d rows, %d content bands:" % (h, len(bs)))
    for (s, e) in bs:
        print("    band rows %3d..%3d  (height %d)" % (s, e, e - s + 1))
