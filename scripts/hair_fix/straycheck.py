#!/usr/bin/env python3
"""Report index-15 islands enclosed by hair -- unfixed hair-highlight strays
-- per sprite cell. Read-only; tells you which cells still need the flood-fill
pass. Cell numbers match gridnumber.py, so you can cross-reference the grid.

The face is a big idx-15 blob bordered by skin-shadow + background, so its
border hair-fraction is low and it is NOT flagged. A highlight strand trapped
in the hair has a border that is mostly hair indices -- that is what flags.

Auto-detects TEX (0x800 header, 4-bit, 512 wide) vs 4-bit indexed BMP.

Usage:
  python straycheck.py <tex.bin|bmp> --hair 11,12,13,14 [--src 15]
         [--threshold 0.5] [--min-island 2]
"""
import sys
import struct
from collections import deque

HEADER = 0x800
TEX_WIDTH = 512


def decode_tex(path):
    data = open(path, 'rb').read()[HEADER:]
    h = len(data) * 2 // TEX_WIDTH
    g = [[0] * TEX_WIDTH for _ in range(h)]
    for i, b in enumerate(data):
        o = i * 2
        g[o // TEX_WIDTH][o % TEX_WIDTH] = (b >> 4) & 0xF
        g[(o + 1) // TEX_WIDTH][(o + 1) % TEX_WIDTH] = b & 0xF
    return g, h, TEX_WIDTH


def decode_bmp(path):
    d = open(path, 'rb').read()
    pixoff = struct.unpack('<I', d[10:14])[0]
    w = struct.unpack('<i', d[18:22])[0]
    h = struct.unpack('<i', d[22:26])[0]
    rowbytes = (((w + 1) // 2) + 3) & ~3
    g = [[0] * w for _ in range(h)]
    for fy in range(h):                 # BMP rows are bottom-up
        iy = h - 1 - fy
        base = pixoff + fy * rowbytes
        for bx in range((w + 1) // 2):
            byte = d[base + bx]
            g[iy][bx * 2] = byte & 0xF                  # low nibble = left pixel
            if bx * 2 + 1 < w:
                g[iy][bx * 2 + 1] = (byte >> 4) & 0xF    # high nibble = right pixel
    return g, h, w


def detect_sprites(g, h, w):
    """Same 2D connected-component detection + ordering as gridnumber.py, so
    cell numbers line up with the rendered grid."""
    visited = [[False] * w for _ in range(h)]
    sprites = []
    for sy in range(h):
        for sx in range(w):
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
                    if 0 <= ny < h and 0 <= nx < w and not visited[ny][nx] and g[ny][nx] != 0:
                        visited[ny][nx] = True
                        q.append((ny, nx))
            if n >= 30:
                sprites.append((minx, miny, maxx, maxy))
    sprites.sort(key=lambda s: (s[1] // 30, s[0]))
    return sprites


def islands(g, bbox, src):
    """Connected components of `src` within bbox; yields (size, border_vals)."""
    x0, y0, x1, y1 = bbox
    seen = set()
    out = []
    for sy in range(y0, y1 + 1):
        for sx in range(x0, x1 + 1):
            if g[sy][sx] != src or (sy, sx) in seen:
                continue
            comp = []
            q = deque([(sy, sx)])
            seen.add((sy, sx))
            while q:
                y, x = q.popleft()
                comp.append((y, x))
                for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                    ny, nx = y + dy, x + dx
                    if x0 <= nx <= x1 and y0 <= ny <= y1 and (ny, nx) not in seen and g[ny][nx] == src:
                        seen.add((ny, nx))
                        q.append((ny, nx))
            bseen = set()
            bvals = []
            for (y, x) in comp:
                for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                    ny, nx = y + dy, x + dx
                    if x0 <= nx <= x1 and y0 <= ny <= y1 and g[ny][nx] != src and (ny, nx) not in bseen:
                        bseen.add((ny, nx))
                        bvals.append(g[ny][nx])
            out.append((len(comp), bvals))
    return out


def main():
    a = sys.argv
    if len(a) < 2:
        print(__doc__)
        return 1
    inp = a[1]

    def opt(n, d):
        return a[a.index(n) + 1] if n in a else d

    hair = set(int(x) for x in opt('--hair', '11,12,13,14').split(','))
    src = int(opt('--src', '15'))
    thr = float(opt('--threshold', '0.5'))
    min_island = int(opt('--min-island', '2'))

    is_bmp = open(inp, 'rb').read(2) == b'BM'
    g, h, w = decode_bmp(inp) if is_bmp else decode_tex(inp)
    sprites = detect_sprites(g, h, w)

    print(f"  {inp}  ({w}x{h}, {'BMP' if is_bmp else 'TEX'}, {len(sprites)} cells)")
    print(f"  hair={sorted(hair)} src={src} threshold={thr} min-island={min_island}")
    flagged = []
    for i, bbox in enumerate(sprites):
        hits = []
        for size, bvals in islands(g, bbox, src):
            if size < min_island or not bvals:
                continue
            frac = sum(1 for v in bvals if v in hair) / len(bvals)
            if frac >= thr:
                hits.append((size, frac))
        if hits:
            flagged.append(i)
            tot = sum(s for s, _ in hits)
            detail = ", ".join(f"{s}px@{f:.2f}" for s, f in sorted(hits, reverse=True))
            print(f"    cell {i:3d}: {len(hits)} stray island(s), {tot}px total  [{detail}]")
    if flagged:
        print(f"  FLAGGED CELLS ({len(flagged)}): {','.join(map(str, flagged))}")
    else:
        print("  clean -- no hair-enclosed idx-15 islands")
    return 0


if __name__ == '__main__':
    sys.exit(main())
