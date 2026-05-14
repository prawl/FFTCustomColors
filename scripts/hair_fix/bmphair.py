#!/usr/bin/env python3
"""HD BMP hair-highlight fixer -- the Config UI preview pipeline.

Same remap logic as hairclassify.py, but for the indexed-BMP container:
14-byte file header + 40-byte DIB header + 16*4-byte BGRA palette, then
4bpp pixel data stored BOTTOM-UP (high nibble = leftmost pixel).

Modes:
  python bmphair.py <in.bmp> --analyze
  python bmphair.py <in.bmp> --render <out.png> [--blackskin] [--scale N]
  python bmphair.py <in.bmp> --remap <out.bmp> --maxy N --frameh H [--src 15] [--dst 12]
"""
import sys
import struct
import zlib

BG = (255, 0, 255)


def decode_bmp(path):
    d = bytearray(open(path, 'rb').read())
    pixoff = struct.unpack('<I', d[10:14])[0]
    w = struct.unpack('<i', d[18:22])[0]
    h = struct.unpack('<i', d[22:26])[0]
    bpp = struct.unpack('<H', d[28:30])[0]
    if bpp != 4:
        raise SystemExit("expected 4bpp BMP, got %d" % bpp)
    pal = []
    for i in range(16):
        b, g, r, _a = d[54 + i * 4:54 + i * 4 + 4]
        pal.append((r, g, b))
    rowbytes = (((w + 1) // 2) + 3) & ~3  # 4bpp, padded to 4-byte boundary
    grid = [[0] * w for _ in range(h)]
    for fy in range(h):            # file rows are bottom-up
        iy = h - 1 - fy            # -> top-down image row
        base = pixoff + fy * rowbytes
        for bx in range((w + 1) // 2):
            byte = d[base + bx]
            grid[iy][bx * 2] = (byte >> 4) & 0xF
            if bx * 2 + 1 < w:
                grid[iy][bx * 2 + 1] = byte & 0xF
    return d, grid, w, h, pal, pixoff, rowbytes


def encode_bmp(d, grid, w, h, pixoff, rowbytes):
    d = bytearray(d)
    for fy in range(h):
        iy = h - 1 - fy
        base = pixoff + fy * rowbytes
        for bx in range((w + 1) // 2):
            hi = grid[iy][bx * 2] & 0xF
            lo = grid[iy][bx * 2 + 1] & 0xF if bx * 2 + 1 < w else 0
            d[base + bx] = (hi << 4) | lo
    return d


def write_png(path, flat_rgb, w, h):
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        for (r, g, b) in flat_rgb[y * w:(y + 1) * w]:
            raw += bytes((r, g, b))

    def chunk(typ, data):
        return (struct.pack('>I', len(data)) + typ + data +
                struct.pack('>I', zlib.crc32(typ + data) & 0xffffffff))

    png = b'\x89PNG\r\n\x1a\n'
    png += chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0))
    png += chunk(b'IDAT', zlib.compress(bytes(raw), 9))
    png += chunk(b'IEND', b'')
    open(path, 'wb').write(png)


def analyze(grid, w, h):
    counts = [sum(1 for v in grid[y] if v != 0) for y in range(h)]
    empties = [y for y in range(h) if counts[y] <= 3]
    runs = []
    if empties:
        s = p = empties[0]
        for e in empties[1:]:
            if e == p + 1:
                p = e
            else:
                runs.append((s, p))
                s = p = e
        runs.append((s, p))
    print("  dimensions %dx%d" % (w, h))
    print("  gap-row runs (<=3 nonzero px): %s" % runs)
    starts = [r[0] for r in runs]
    print("  band starts: %s" % [r[1] + 1 for r in runs][:-1])
    # row-similarity period check
    for P in (40, 48, 56, 64, 72, 80, 88):
        match = same = 0
        for y in range(0, h - P):
            for x in range(0, w, 8):
                match += 1
                if grid[y][x] == grid[y + P][x]:
                    same += 1
        print("  period %2d: %.1f%% row-match" % (P, 100.0 * same / max(1, match)))


def main():
    a = sys.argv
    if len(a) < 3:
        print(__doc__)
        return 1
    inp = a[1]

    def opt(name, default):
        return a[a.index(name) + 1] if name in a else default

    d, grid, w, h, pal, pixoff, rowbytes = decode_bmp(inp)

    if '--analyze' in a:
        print("ANALYZE %s" % inp)
        analyze(grid, w, h)
        return 0

    if '--render' in a:
        out = opt('--render', None)
        scale = int(opt('--scale', '2'))
        rpal = list(pal)
        if '--blackskin' in a:
            rpal[14] = rpal[15] = (0, 0, 0)
        W, H = w * scale, h * scale
        flat = [BG] * (W * H)
        for y in range(h):
            for x in range(w):
                idx = grid[y][x]
                rgb = BG if idx == 0 else rpal[idx]
                for sy in range(scale):
                    base = (y * scale + sy) * W + x * scale
                    for sx in range(scale):
                        flat[base + sx] = rgb
        write_png(out, flat, W, H)
        print("  wrote %s (%dx%d)" % (out, W, H))
        return 0

    if '--remap' in a:
        out = opt('--remap', None)
        maxy = int(opt('--maxy', '12'))
        frameh = int(opt('--frameh', '80'))
        offset = int(opt('--offset', '0'))  # rows of top margin before frame 0
        src = int(opt('--src', '15'))
        dst = int(opt('--dst', '12'))
        remapped = 0
        hist = {}
        for y in range(h):
            ly = (y - offset) % frameh
            if ly >= maxy:
                continue
            for x in range(w):
                if grid[y][x] == src:
                    grid[y][x] = dst
                    remapped += 1
                    hist[ly] = hist.get(ly, 0) + 1
        out_d = encode_bmp(d, grid, w, h, pixoff, rowbytes)
        open(out, 'wb').write(out_d)
        print("  blanket remap %d->%d above localY %d (frameh %d): %d px" %
              (src, dst, maxy, frameh, remapped))
        print("  localY hist: %s" % dict(sorted(hist.items())))
        print("  wrote %s" % out)
        return 0

    print("no mode given (--analyze / --render / --remap)")
    return 1


if __name__ == '__main__':
    sys.exit(main())
