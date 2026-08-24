import struct, sys

SIZES = [(8,8),(16,8),(16,16),(16,24),(24,8),(24,16),(24,24),(32,8),
         (32,16),(32,24),(32,32),(32,40),(48,16),(40,32),(48,48),(56,56)]

def u32(b, o):
    return struct.unpack_from("<I", b, o)[0]

def parse_tile(b, off, yOffset):
    x = struct.unpack_from("<b", b, off)[0]
    y = struct.unpack_from("<b", b, off+1)[0]
    flags = b[off+2] + b[off+3]*256
    rx = (flags & 0x4000) != 0
    ry = (flags & 0x8000) != 0
    f = (flags >> 10) & 0x0F
    tileX = (flags & 0x1F) * 8
    tileY = ((flags >> 5) & 0x1F) * 8 + yOffset
    w, h = SIZES[f]
    return {"dst": (x, y), "src": (tileX, tileY, w, h), "flipX": rx, "flipY": ry}

def parse_frame(b, off, yOffset):
    n = b[off] + b[off+1]*256
    if n > 64:
        return None  # garbage guard
    tiles = [parse_tile(b, off + 2 + i*4, yOffset) for i in range(n+1)]
    # Shishi reverses then draws in list order: file order tile0 = drawn LAST = frontmost.
    return tiles  # keep FILE order: index 0 = frontmost

def parse_shp(path):
    b = open(path, "rb").read()
    jump = u32(b, 0)
    secondHalf = b[4] + b[5]*256
    offsets = [0]
    i = 0
    while True:
        addy = u32(b, 0x0C + 4*i); i += 1
        if addy == 0:
            break
        offsets.append(addy)
    frames = []
    for i, off in enumerate(offsets):
        frames.append(parse_frame(b, off + 0x40A, 256 if i >= secondHalf else 0))
    if jump > 8:
        offsets2 = [0]
        i = 0
        while True:
            addy = u32(b, jump + 4*i + 4); i += 1
            if addy == 0:
                break
            offsets2.append(addy)
        for i, off in enumerate(offsets2):
            frames.append(parse_frame(b, off + jump + 0x402, 256 if i >= secondHalf else 0))
    return frames, secondHalf, jump

JUMPS = {192:1,198:1,211:1,212:1,214:1,215:1,216:1,226:1,238:1,239:1,240:1,246:1,
         193:2,217:2,242:2,252:2,250:3}

def parse_seq(path):
    b = open(path, "rb").read()
    offsets = []
    for i in range(0x100):
        cur = u32(b, i*4 + 4)
        if cur == 0xFFFFFFFF:
            break
        offsets.append(cur)
    anims = []
    start = 0x0406
    for i in range(len(offsets)):
        end = start + offsets[i+1] if i+1 < len(offsets) else len(b)
        if i+1 < len(offsets) and offsets[i] == offsets[i+1]:
            anims.append(None)
            continue
        chunk = b[start + offsets[i]: (start + offsets[i+1]) if i+1 < len(offsets) else len(b)]
        seq = []
        j = 0
        while j < len(chunk) - 1:
            if chunk[j] != 0xFF:
                seq.append((chunk[j], chunk[j+1]))
            elif chunk[j+1] in JUMPS:
                j += JUMPS[chunk[j+1]] * 2
            j += 2
        anims.append(seq if seq else None)
    return anims

if __name__ == "__main__":
    shp_path, seq_path = sys.argv[1], sys.argv[2]
    frames, secondHalf, jump = parse_shp(shp_path)
    anims = parse_seq(seq_path)
    print(f"SHP: {len(frames)} frames, secondHalf={secondHalf}, jump={hex(jump)}")
    print(f"SEQ: {len(anims)} animations")
    for a in range(0, 16):
        if a < len(anims) and anims[a]:
            print(f"anim {a:3d}: " + " ".join(f"f{fi}@{d}" for fi, d in anims[a]))
    # dump tiles of the frames used by anims 6 and 7 (walk fwd / walk back)
    used = []
    for a in (6, 7):
        if a < len(anims) and anims[a]:
            for fi, d in anims[a]:
                if fi not in used:
                    used.append(fi)
    for fi in used:
        t = frames[fi] if fi < len(frames) else None
        print(f"frame {fi}: " + (" | ".join(
            f"dst({x},{y}) src({sx},{sy},{w}x{h}){'X' if fx else ''}{'Y' if fy else ''}"
            for (x, y), (sx, sy, w, h), fx, fy in
            [(tt['dst'], tt['src'], tt['flipX'], tt['flipY']) for tt in t]) if t else "None"))
