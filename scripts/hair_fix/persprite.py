#!/usr/bin/env python3
"""Per-sprite hair-highlight remap for TEX files.

For each given cell number (matching gridnumber.py's numbering), remap
index src -> dst within the top `maxy` rows of THAT sprite's bounding box.
Wide/short sprites (lying-down poses) auto-skip -- their head isn't at the
top, so a bbox-top cut would hit the wrong place.

Usage:
  python persprite.py <in.bin> <out.bin> --cells 7,8,48,... [--maxy 12]
                      [--src 15] [--dst 12] [--maxy-cell N:V,...]
"""
import sys
import struct
from collections import deque

HEADER = 0x800
WIDTH = 512


def decode(path):
    data = bytearray(open(path, 'rb').read())
    px = data[HEADER:]
    h = len(px) * 2 // WIDTH
    g = [[0] * WIDTH for _ in range(h)]
    for i, b in enumerate(px):
        o = i * 2
        g[o // WIDTH][o % WIDTH] = (b >> 4) & 0xF
        g[(o + 1) // WIDTH][(o + 1) % WIDTH] = b & 0xF
    return data, g, h


def encode(data, g, h):
    px = bytearray(h * WIDTH // 2)
    for y in range(h):
        for x in range(WIDTH):
            o = y * WIDTH + x
            v = g[y][x] & 0xF
            if o % 2 == 0:
                px[o // 2] = (px[o // 2] & 0x0F) | (v << 4)
            else:
                px[o // 2] = (px[o // 2] & 0xF0) | v
    data[HEADER:] = px
    return data


def decode_bmp(path):
    d = bytearray(open(path, 'rb').read())
    pixoff = struct.unpack('<I', d[10:14])[0]
    w = struct.unpack('<i', d[18:22])[0]
    h = struct.unpack('<i', d[22:26])[0]
    rowbytes = (((w + 1) // 2) + 3) & ~3
    grid = [[0] * w for _ in range(h)]
    for fy in range(h):           # BMP rows are bottom-up
        iy = h - 1 - fy
        base = pixoff + fy * rowbytes
        for bx in range((w + 1) // 2):
            byte = d[base + bx]
            grid[iy][bx * 2] = byte & 0xF                   # low nibble = left pixel
            if bx * 2 + 1 < w:
                grid[iy][bx * 2 + 1] = (byte >> 4) & 0xF     # high nibble = right pixel
    return d, grid, h, pixoff, rowbytes


def encode_bmp(d, grid, h, pixoff, rowbytes):
    d = bytearray(d)
    w = len(grid[0])
    for fy in range(h):
        iy = h - 1 - fy
        base = pixoff + fy * rowbytes
        for bx in range((w + 1) // 2):
            lo = grid[iy][bx * 2] & 0xF                               # left pixel = low nibble
            hi = grid[iy][bx * 2 + 1] & 0xF if bx * 2 + 1 < w else 0   # right pixel = high nibble
            d[base + bx] = (hi << 4) | lo
    return d


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


def floodfill_cell(g, h, bbox, src, dst, hair_set, threshold):
    """Within bbox, find connected components of `src`; remap a component to
    `dst` if >= threshold of its border pixels are hair indices. The face is a
    big skin region NOT hugged by hair, so it stays put -- only the trapped
    hair-highlight islands flip."""
    x0, y0, x1, y1 = bbox
    visited = set()
    remapped = flipped = kept = 0
    for sy in range(y0, y1 + 1):
        for sx in range(x0, x1 + 1):
            if g[sy][sx] != src or (sy, sx) in visited:
                continue
            comp = []
            q = deque([(sy, sx)])
            visited.add((sy, sx))
            while q:
                y, x = q.popleft()
                comp.append((y, x))
                for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                    ny, nx = y + dy, x + dx
                    if x0 <= nx <= x1 and y0 <= ny <= y1 and (ny, nx) not in visited and g[ny][nx] == src:
                        visited.add((ny, nx))
                        q.append((ny, nx))
            bseen = set()
            bvals = []
            for (y, x) in comp:
                for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                    ny, nx = y + dy, x + dx
                    if x0 <= nx <= x1 and y0 <= ny <= y1 and g[ny][nx] != src and (ny, nx) not in bseen:
                        bseen.add((ny, nx))
                        bvals.append(g[ny][nx])
            if not bvals:
                continue
            frac = sum(1 for v in bvals if v in hair_set) / len(bvals)
            if frac >= threshold:
                for (y, x) in comp:
                    g[y][x] = dst
                    remapped += 1
                flipped += 1
            else:
                kept += 1
    return remapped, flipped, kept


def main():
    inp, outp = sys.argv[1], sys.argv[2]

    def opt(n, d):
        return sys.argv[sys.argv.index(n) + 1] if n in sys.argv else d

    cells = set(int(c) for c in opt('--cells', '').split(',') if c.strip())
    maxy = int(opt('--maxy', '12'))
    src = int(opt('--src', '15'))
    dst = int(opt('--dst', '12'))
    # per-cell maxy override, e.g. "7:8,48:16"
    per = {}
    for kv in opt('--maxy-cell', '').split(','):
        if ':' in kv:
            k, v = kv.split(':')
            per[int(k)] = int(v)

    is_bmp = open(inp, 'rb').read(2) == b'BM'
    if is_bmp:
        data, g, h, pixoff, rowbytes = decode_bmp(inp)
    else:
        data, g, h = decode(inp)
    sprites = detect_sprites(g, h)
    all_mode = '--all' in sys.argv

    def write_out():
        if is_bmp:
            open(outp, 'wb').write(encode_bmp(data, g, h, pixoff, rowbytes))
        else:
            encode(data, g, h)
            open(outp, 'wb').write(data)

    if '--floodfill' in sys.argv:
        hair_set = set(int(x) for x in opt('--hair', '10,11,12').split(','))
        threshold = float(opt('--threshold', '0.6'))
        print("  FLOOD-FILL (threshold %.2f, hair %s, all=%s, fmt=%s)" %
              (threshold, sorted(hair_set), all_mode, 'BMP' if is_bmp else 'TEX'))
        total = 0
        for i, bbox in enumerate(sprites):
            if not all_mode and i not in cells:
                continue
            rm, fl, kp = floodfill_cell(g, h, bbox, src, dst, hair_set, threshold)
            total += rm
            if rm:
                print("    cell %2d: %d px remapped  (%d islands -> hair, %d kept)" % (i, rm, fl, kp))
        print("  total: %d px remapped across %d sprites" % (total, len(sprites)))
        write_out()
        print("  wrote %s" % outp)
        return

    done = []
    skipped = []
    for i, (x0, y0, x1, y1) in enumerate(sprites):
        if i not in cells:
            continue
        w = x1 - x0 + 1
        ht = y1 - y0 + 1
        if w > ht * 1.3:  # wide/short -> lying-down, head not at the top
            skipped.append((i, w, ht))
            continue
        m = per.get(i, maxy)
        cnt = 0
        for y in range(y0, min(y0 + m, h)):
            for x in range(x0, x1 + 1):
                if g[y][x] == src:
                    g[y][x] = dst
                    cnt += 1
        done.append((i, cnt, m))
    encode(data, g, h)
    open(outp, 'wb').write(data)
    print("  remapped %d cells:" % len(done))
    for i, c, m in done:
        print("    cell %2d: %d px (maxy %d)" % (i, c, m))
    if skipped:
        print("  SKIPPED (wide/lying-down, head not at top): %s" %
              [s[0] for s in skipped])
    print("  wrote %s" % outp)


if __name__ == '__main__':
    main()
