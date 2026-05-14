#!/usr/bin/env python3
"""Render a TEX sprite sheet with a numbered grid.
Detects each sprite (80-row slots x horizontal content runs), draws a cyan
box + yellow number on each. Skin (idx 14/15) is rendered RED, so any
unfixed hair-highlight shows up as red specks inside the gold hair.
Usage: python gridnumber.py <tex.bin> <out.png> [scale=3] [frameh=80]"""
import sys
import struct
import zlib
from collections import deque

HEADER = 0x800
WIDTH = 512

PAL = [
    (0, 0, 0), (40, 40, 32), (224, 224, 208), (80, 72, 64),
    (120, 112, 96), (160, 152, 136), (200, 192, 176), (112, 48, 32),
    (184, 48, 32), (232, 72, 32), (96, 56, 16), (144, 104, 40),
    (200, 152, 64), (144, 88, 32), (184, 128, 64), (232, 184, 120),
]
BG = (255, 0, 255)       # transparent -> magenta
SKIN = (255, 0, 0)       # idx 14/15 -> red (so unfixed highlight pops)
BOX = (0, 255, 255)      # cyan grid box
NUMFG = (255, 255, 0)    # yellow number
NUMBG = (0, 0, 0)        # black backing

FONT = {
    '0': ["111", "101", "101", "101", "111"], '1': ["010", "110", "010", "010", "111"],
    '2': ["111", "001", "111", "100", "111"], '3': ["111", "001", "111", "001", "111"],
    '4': ["101", "101", "111", "001", "001"], '5': ["111", "100", "111", "001", "111"],
    '6': ["111", "100", "111", "101", "111"], '7': ["111", "001", "001", "001", "001"],
    '8': ["111", "101", "111", "101", "111"], '9': ["111", "101", "111", "001", "111"],
}


def decode(path):
    data = open(path, 'rb').read()[HEADER:]
    h = len(data) * 2 // WIDTH
    g = [[0] * WIDTH for _ in range(h)]
    for i, b in enumerate(data):
        o = i * 2
        g[o // WIDTH][o % WIDTH] = (b >> 4) & 0xF
        g[(o + 1) // WIDTH][(o + 1) % WIDTH] = b & 0xF
    return g, h


def detect_sprites(g, h, frameh):
    """2D connected-component detection -- each sprite blob gets its own box,
    regardless of where it sits. Tiny noise components are filtered out."""
    visited = [[False] * WIDTH for _ in range(h)]
    sprites = []
    for sy in range(h):
        for sx in range(WIDTH):
            if g[sy][sx] == 0 or visited[sy][sx]:
                continue
            q = deque([(sy, sx)])
            visited[sy][sx] = True
            n = 0
            minx = maxx = sx
            miny = maxy = sy
            while q:
                y, x = q.popleft()
                n += 1
                if x < minx:
                    minx = x
                if x > maxx:
                    maxx = x
                if y < miny:
                    miny = y
                if y > maxy:
                    maxy = y
                for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < h and 0 <= nx < WIDTH and not visited[ny][nx] and g[ny][nx] != 0:
                        visited[ny][nx] = True
                        q.append((ny, nx))
            if n >= 30:  # drop stray-pixel noise
                sprites.append((minx, miny, maxx, maxy))
    # reading order: group into rough rows by top edge, then left-to-right
    sprites.sort(key=lambda s: (s[1] // 30, s[0]))
    return sprites


def write_png(path, flat, w, h):
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        for (r, gg, b) in flat[y * w:(y + 1) * w]:
            raw += bytes((r, gg, b))

    def chunk(t, d):
        return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)

    png = b'\x89PNG\r\n\x1a\n'
    png += chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0))
    png += chunk(b'IDAT', zlib.compress(bytes(raw), 9))
    png += chunk(b'IEND', b'')
    open(path, 'wb').write(png)


def main():
    inp, outp = sys.argv[1], sys.argv[2]
    scale = int(sys.argv[3]) if len(sys.argv) > 3 else 3
    frameh = int(sys.argv[4]) if len(sys.argv) > 4 else 80

    g, h = decode(inp)
    pal = list(PAL)
    pal[14] = pal[15] = SKIN
    sprites = detect_sprites(g, h, frameh)

    W, H = WIDTH * scale, h * scale
    flat = [BG] * (W * H)
    for y in range(h):
        for x in range(WIDTH):
            idx = g[y][x]
            rgb = BG if idx == 0 else pal[idx]
            for sy in range(scale):
                base = (y * scale + sy) * W + x * scale
                for sx in range(scale):
                    flat[base + sx] = rgb

    def setpx(px, py, c):
        if 0 <= px < W and 0 <= py < H:
            flat[py * W + px] = c

    ds = scale  # digit pixel scale
    for i, (x0, y0, x1, y1) in enumerate(sprites):
        sx0, sy0 = x0 * scale, y0 * scale
        sx1, sy1 = (x1 + 1) * scale - 1, (y1 + 1) * scale - 1
        for px in range(sx0, sx1 + 1):
            setpx(px, sy0, BOX)
            setpx(px, sy1, BOX)
        for py in range(sy0, sy1 + 1):
            setpx(sx0, py, BOX)
            setpx(sx1, py, BOX)
        s = str(i)
        bw = len(s) * 4 * ds + ds
        bh = 5 * ds + 2 * ds
        for by in range(sy0, sy0 + bh):
            for bx in range(sx0, sx0 + bw):
                setpx(bx, by, NUMBG)
        cx = sx0 + ds
        for ch in s:
            for ry, bits in enumerate(FONT[ch]):
                for rx, bit in enumerate(bits):
                    if bit == '1':
                        for ddy in range(ds):
                            for ddx in range(ds):
                                setpx(cx + rx * ds + ddx, sy0 + ds + ry * ds + ddy, NUMFG)
            cx += 4 * ds

    write_png(outp, flat, W, H)
    print("  wrote %s (%dx%d), %d sprites detected" % (outp, W, H, len(sprites)))


if __name__ == '__main__':
    main()
