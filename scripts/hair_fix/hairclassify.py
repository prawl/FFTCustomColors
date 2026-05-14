#!/usr/bin/env python3
"""
Surgical hair-highlight classifier for FFT TEX files.

The hair highlight on these sprites is painted with palette index 15 -- the
same index as skin base -- so it follows the Skin slider instead of Hair.
The crude fix (scripts/fix_hair_highlight_tex.py) blanket-remaps every index-15
pixel in the top N rows, which also recolors faces/foreheads that sit up there.

Surgical method (from commit a7a1ee94, Squire Male): connected-component
analysis. An index-15 blob is a hair highlight if >= THRESHOLD of its border
pixels are hair indices; otherwise it's face and left untouched.

`maxy` is a HARD WALL within each frame: the flood-fill cannot cross
localY >= maxy, so a hair-highlight blob never merges with the face that
touches it just below, AND the body/hands/boots are never even considered.
Frame height is `--frameh` (FFT IVC TEX sheets are 80-row slots).

TEX format: 0x800 header, 4-bit indexed, high nibble = first pixel of a byte,
low nibble = second; sheet width 512px.

Usage:
  python hairclassify.py <in.bin> <out.bin> --hair 11,12,13 [--src 15] [--dst 12]
         [--threshold 0.6] [--conn 4|8] [--ignore-bg] [--maxy N] [--frameh 80]
         [--debugline IDX] [--dry-run]
"""
import sys
from collections import deque

HEADER = 0x800
WIDTH = 512


def decode(path):
    data = bytearray(open(path, 'rb').read())
    px = data[HEADER:]
    n = len(px) * 2
    height = n // WIDTH
    grid = [[0] * WIDTH for _ in range(height)]
    for i, b in enumerate(px):
        o = i * 2
        grid[o // WIDTH][o % WIDTH] = (b >> 4) & 0xF        # high nibble = first pixel
        grid[(o + 1) // WIDTH][(o + 1) % WIDTH] = b & 0xF   # low nibble  = second pixel
    return data, grid, height


def encode(data, grid, height):
    px = bytearray(height * WIDTH // 2)
    for y in range(height):
        row = grid[y]
        for x in range(WIDTH):
            o = y * WIDTH + x
            v = row[x] & 0xF
            if o % 2 == 0:
                px[o // 2] = (px[o // 2] & 0x0F) | (v << 4)
            else:
                px[o // 2] = (px[o // 2] & 0xF0) | v
    data[HEADER:] = px
    return data


def classify(grid, height, hair_set, src, dst, threshold, conn, ignore_bg, maxy, frameh, blanket):
    # blanket mode: above the maxy line it's all hair (the line excludes the
    # face), so just remap every src pixel there -- no border test, no under-catch.
    if blanket:
        remapped = 0
        localy_hist = {}
        for y in range(height):
            ly = y % frameh
            if maxy is not None and ly >= maxy:
                continue
            for x in range(WIDTH):
                if grid[y][x] == src:
                    grid[y][x] = dst
                    remapped += 1
                    localy_hist[ly] = localy_hist.get(ly, 0) + 1
        return remapped, 0, 0, localy_hist

    neigh4 = ((-1, 0), (1, 0), (0, -1), (0, 1))
    neigh8 = neigh4 + ((-1, -1), (-1, 1), (1, -1), (1, 1))
    neigh = neigh8 if conn == 8 else neigh4
    visited = [[False] * WIDTH for _ in range(height)]
    remapped = 0
    blobs_hair = blobs_face = 0
    localy_hist = {}

    for sy in range(height):
        for sx in range(WIDTH):
            if grid[sy][sx] != src or visited[sy][sx]:
                continue
            if maxy is not None and (sy % frameh) >= maxy:
                continue  # seed must lie within the head region (above the maxy wall)
            # flood-fill the connected component of `src` pixels.
            # maxy is a HARD WALL: the fill cannot cross localY >= maxy, so a
            # hair-highlight blob never merges with the face that touches it below.
            comp = []
            q = deque([(sy, sx)])
            visited[sy][sx] = True
            while q:
                y, x = q.popleft()
                comp.append((y, x))
                for dy, dx in neigh:
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < height and 0 <= nx < WIDTH and not visited[ny][nx] and grid[ny][nx] == src:
                        if maxy is not None and (ny % frameh) >= maxy:
                            continue  # cannot cross the maxy wall
                        visited[ny][nx] = True
                        q.append((ny, nx))
            # collect unique border pixels (non-src neighbours of the component)
            border_seen = set()
            border_vals = []
            for (y, x) in comp:
                for dy, dx in neigh:
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < height and 0 <= nx < WIDTH and grid[ny][nx] != src and (ny, nx) not in border_seen:
                        border_seen.add((ny, nx))
                        border_vals.append(grid[ny][nx])
            if ignore_bg:
                border_vals = [v for v in border_vals if v != 0]
            if not border_vals:
                continue
            hair_frac = sum(1 for v in border_vals if v in hair_set) / len(border_vals)
            if hair_frac >= threshold:
                blobs_hair += 1
                for (y, x) in comp:
                    grid[y][x] = dst
                    remapped += 1
                    ly = y % frameh
                    localy_hist[ly] = localy_hist.get(ly, 0) + 1
            else:
                blobs_face += 1
    return remapped, blobs_hair, blobs_face, localy_hist


def main():
    a = sys.argv
    if len(a) < 3:
        print(__doc__)
        return 1
    inp, outp = a[1], a[2]

    def opt(name, default):
        return a[a.index(name) + 1] if name in a else default

    hair_set = set(int(x) for x in opt('--hair', '11,12,13').split(','))
    src = int(opt('--src', '15'))
    dst = int(opt('--dst', '12'))
    threshold = float(opt('--threshold', '0.6'))
    conn = int(opt('--conn', '4'))
    ignore_bg = '--ignore-bg' in a
    maxy = int(opt('--maxy', '0')) or None
    frameh = int(opt('--frameh', '80'))
    dry = '--dry-run' in a

    data, grid, height = decode(inp)
    remapped, bh, bf, hist = classify(grid, height, hair_set, src, dst, threshold, conn, ignore_bg, maxy, frameh)

    # optional bright debug line painted along the maxy cutoff row of every frame,
    # so the cutoff can be eyeballed and tuned. paints over non-transparent px only.
    debugline = opt('--debugline', None)
    if debugline is not None and maxy is not None:
        di = int(debugline)
        painted = 0
        for y in range(height):
            if y % frameh == maxy:
                for x in range(WIDTH):
                    if grid[y][x] != 0:
                        grid[y][x] = di
                        painted += 1
        print(f"  DEBUG LINE: painted {painted}px at localY={maxy} (frameh={frameh}) with index {di}")

    print(f"  {inp}  ({height} rows)")
    print(f"  hair_set={sorted(hair_set)} src={src} dst={dst} thr={threshold} conn={conn} "
          f"ignore_bg={ignore_bg} maxy={maxy} frameh={frameh}")
    print(f"  blobs: {bh} classified hair (remapped), {bf} classified face (kept)")
    print(f"  pixels remapped {src}->{dst}: {remapped}")
    if hist:
        lo, hi = min(hist), max(hist)
        print(f"  remapped localY range: {lo}..{hi}")
        for y in sorted(hist):
            print(f"    y={y:2d}: {hist[y]}")
    if not dry:
        encode(data, grid, height)
        open(outp, 'wb').write(data)
        print(f"  wrote {outp}")
    else:
        print("  [dry-run] not written")
    return 0


if __name__ == '__main__':
    sys.exit(main())
