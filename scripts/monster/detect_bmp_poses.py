#!/usr/bin/env python3
"""Auto-detect and number the pose bounding boxes in a monster HD BMP (<id>_<Name>_hd.bmp).
Monster sheets are irregular (poses aren't grid-aligned), so instead of fighting a uniform grid
this finds each pose blob (connected components over non-transparent indices), draws a numbered
cyan box on each, and prints exact (x, y, w, h) rects. Pick the two clean front-facing standing
poses by number, then encode them as a FrameLayout.For("<Family>") = FrameLayout(w, h, swCol,
nwCol, row, offsetX, offsetY) in SpriteSheetExtractor.cs.

BMP format: 4bpp indexed, rows bottom-up, stride padded to 4 bytes, high nibble = left pixel,
16-color BGRA palette at 14 + DIBheaderSize. The embedded palette matches the sprite bin's index
order 1:1.

Usage: python detect_bmp_poses.py <id>_<Name>_hd.bmp <out.png> [min_pixels=300]
"""
import sys, struct, zlib
from collections import deque

FONT = {'0':["111","101","101","101","111"],'1':["010","110","010","010","111"],
        '2':["111","001","111","100","111"],'3':["111","001","111","001","111"],
        '4':["101","101","111","001","001"],'5':["111","100","111","001","111"],
        '6':["111","100","111","101","111"],'7':["111","001","001","001","001"],
        '8':["111","101","111","101","111"],'9':["111","101","111","001","111"]}

def main():
    bmp, outp = sys.argv[1], sys.argv[2]
    min_px = int(sys.argv[3]) if len(sys.argv) > 3 else 300
    b = open(bmp, 'rb').read()
    off = struct.unpack('<I', b[10:14])[0]
    w = struct.unpack('<i', b[18:22])[0]
    h = struct.unpack('<i', b[22:26])[0]
    dib = struct.unpack('<I', b[14:18])[0]
    paloff = 14 + dib
    emb = [(b[paloff + i*4 + 2], b[paloff + i*4 + 1], b[paloff + i*4]) for i in range(16)]
    rowb = ((w * 4 + 31) // 32) * 4

    def idx(x, y):
        sy = h - 1 - y                      # rows are bottom-up
        byte = b[off + sy * rowb + x // 2]
        return (byte >> 4) & 0xF if x % 2 == 0 else byte & 0xF  # high nibble = left

    G = [[idx(x, y) for x in range(w)] for y in range(h)]
    vis = [[False] * w for _ in range(h)]
    boxes = []
    for y in range(h):
        for x in range(w):
            if G[y][x] == 0 or vis[y][x]:
                continue
            q = deque([(y, x)]); vis[y][x] = True
            n = 0; x0 = x1 = x; y0 = y1 = y
            while q:
                cy, cx = q.popleft(); n += 1
                x0, x1 = min(x0, cx), max(x1, cx); y0, y1 = min(y0, cy), max(y1, cy)
                for dy, dx in ((-1,0),(1,0),(0,-1),(0,1)):
                    ny, nx = cy + dy, cx + dx
                    if 0 <= ny < h and 0 <= nx < w and not vis[ny][nx] and G[ny][nx] != 0:
                        vis[ny][nx] = True; q.append((ny, nx))
            if n >= min_px and (x1 - x0) >= 24 and (y1 - y0) >= 24:
                boxes.append([x0, y0, x1, y1])
    boxes.sort(key=lambda bx: (bx[1] // 40, bx[0]))

    buf = [[(0, 0, 0, 0) if G[y][x] == 0 else (*emb[G[y][x]], 255) for x in range(w)] for y in range(h)]
    def setp(x, y, c):
        if 0 <= x < w and 0 <= y < h: buf[y][x] = c
    for i, (x0, y0, x1, y1) in enumerate(boxes):
        for x in range(x0, x1 + 1): setp(x, y0, (0,255,255,255)); setp(x, y1, (0,255,255,255))
        for y in range(y0, y1 + 1): setp(x0, y, (0,255,255,255)); setp(x1, y, (0,255,255,255))
        cx = x0 + 2
        for chx in str(i):
            for ry, bits in enumerate(FONT[chx]):
                for rx, bit in enumerate(bits):
                    if bit == '1':
                        for ddy in range(2):
                            for ddx in range(2): setp(cx + rx*2 + ddx, y0 + 2 + ry*2 + ddy, (255,255,0,255))
            cx += 8

    def ch(t, da):
        c = t + da
        return struct.pack('>I', len(da)) + c + struct.pack('>I', zlib.crc32(c) & 0xffffffff)
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        for x in range(w):
            r, g, bb, a = buf[y][x]; raw += bytes((r, g, bb, a))
    open(outp, 'wb').write(b'\x89PNG\r\n\x1a\n'
        + ch(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
        + ch(b'IDAT', zlib.compress(bytes(raw), 9)) + ch(b'IEND', b''))
    print(f"{bmp}: {w}x{h}, {len(boxes)} poses")
    for i, (x0, y0, x1, y1) in enumerate(boxes):
        print(f"  {i}: x={x0} y={y0} w={x1-x0+1} h={y1-y0+1}")


if __name__ == '__main__':
    main()
