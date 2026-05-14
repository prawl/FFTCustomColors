#!/usr/bin/env python3
"""Crop given cells from a TEX, scale them up, arrange in a labeled grid for
close inspection. Skin (14/15) -> red, so unfixed hair-highlight shows as
red specks in the gold hair.
Usage: python cellzoom.py <tex.bin> <out.png> --cells 7,8,... [--scale 6] [--cols 5]"""
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
BG = (40, 0, 40)
SKIN = (255, 0, 0)
NUMFG = (255, 255, 0)
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


def detect_sprites(g, h):
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
                minx = min(minx, x)
                maxx = max(maxx, x)
                miny = min(miny, y)
                maxy = max(maxy, y)
                for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < h and 0 <= nx < WIDTH and not visited[ny][nx] and g[ny][nx] != 0:
                        visited[ny][nx] = True
                        q.append((ny, nx))
            if n >= 30:
                sprites.append((minx, miny, maxx, maxy))
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

    png = (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0)) +
           chunk(b'IDAT', zlib.compress(bytes(raw), 9)) + chunk(b'IEND', b''))
    open(path, 'wb').write(png)


def main():
    inp, outp = sys.argv[1], sys.argv[2]

    def opt(n, d):
        return sys.argv[sys.argv.index(n) + 1] if n in sys.argv else d

    cells = [int(c) for c in opt('--cells', '').split(',') if c.strip()]
    scale = int(opt('--scale', '6'))
    cols = int(opt('--cols', '5'))

    g, h = decode(inp)
    sprites = detect_sprites(g, h)
    pal = list(PAL)
    pal[14] = pal[15] = SKIN

    crops = []
    cellw = cellh = 0
    for c in cells:
        x0, y0, x1, y1 = sprites[c]
        cw, ch = x1 - x0 + 1, y1 - y0 + 1
        sub = [[g[y0 + yy][x0 + xx] for xx in range(cw)] for yy in range(ch)]
        crops.append((c, cw, ch, sub))
        cellw = max(cellw, cw)
        cellh = max(cellh, ch)

    pad = 3
    labelh = 7
    cbw = (cellw + pad) * scale
    cbh = (cellh + pad + labelh) * scale
    rows = (len(crops) + cols - 1) // cols
    W, H = cbw * cols, cbh * rows
    flat = [BG] * (W * H)

    def setpx(px, py, col):
        if 0 <= px < W and 0 <= py < H:
            flat[py * W + px] = col

    for idx, (c, cw, ch, sub) in enumerate(crops):
        gx = (idx % cols) * cbw
        gy = (idx // cols) * cbh
        for di, chr_ in enumerate(str(c)):
            for ry, bits in enumerate(FONT[chr_]):
                for rx, bit in enumerate(bits):
                    if bit == '1':
                        for ddy in range(scale):
                            for ddx in range(scale):
                                setpx(gx + 2 * scale + di * 4 * scale + rx * scale + ddx,
                                      gy + scale + ry * scale + ddy, NUMFG)
        oy = gy + labelh * scale
        for yy in range(ch):
            for xx in range(cw):
                v = sub[yy][xx]
                col = BG if v == 0 else pal[v]
                for ddy in range(scale):
                    for ddx in range(scale):
                        setpx(gx + xx * scale + ddx, oy + yy * scale + ddy, col)

    write_png(outp, flat, W, H)
    print("  wrote %s (%dx%d), %d cells" % (outp, W, H, len(crops)))


if __name__ == '__main__':
    main()
