#!/usr/bin/env python
"""
Reverse-engineer the real source bin for the Hydra/Tiamat monster family.

The family was deferred because the assumed bin (battle_hebi_spr.bin, "snake")
has only palette 0 populated and its pixels don't match the Tiamat HD BMP. This
tool re-derives the answer from scratch across ALL plausible candidate bins:

  - reports, per bin, how many of palettes 0/1/2 are "populated" (have real,
    distinct colors vs all-black/empty) -> a 3-tier family needs 0/1/2 all live;
  - decodes palettes 0/1/2 to hex so the tier color story is visible;
  - extracts the embedded 16-color palette from each Tiamat HD BMP and scores how
    well each bin-palette matches each BMP-palette (the BMP's indices are 1:1 with
    the bin palette that paints it, so the right bin's pal0 ~= the BMP's palette).

Usage: python scripts/monster/analyze_hydra_candidates.py
"""

import struct
from pathlib import Path

UNIT = Path(r"C:\Users\ptyRa\OneDrive\Desktop\Pac Files\0002\fftpack\unit")
SPRITES = Path(r"C:\Users\ptyRa\OneDrive\Desktop\Extracted Game Files\extracted_sprites")

# Candidate bins to test as the Hydra/Tiamat source. dora1 = confirmed Dragon
# family (control). hebi = the old (rejected) guess. dora2/dora22/adora = the
# unexamined leads.
CANDIDATE_BINS = [
    "battle_hebi_spr.bin",     # old guess (snake)
    "battle_dora2_spr.bin",    # lead
    "battle_dora22_sp2.bin",   # lead
    "battle_adora_spr.bin",    # lead
    "battle_dora1_spr.bin",    # control = known Dragon family (green/blue/red)
]

# The HD textures the bestiary labels "Tiamat" (the Hydra family's top tier).
TIAMAT_BMPS = ["1098_Tiamat_hd.bmp", "1099_Tiamat_hd.bmp", "1136_Tiamat_2_hd.bmp"]


def decode_bgr555(word):
    """FFT palette word -> (r,g,b) 0..248, XBBBBBGGGGGRRRRR (5 bits each, *8)."""
    r = (word & 0x1F) * 8
    g = ((word >> 5) & 0x1F) * 8
    b = ((word >> 10) & 0x1F) * 8
    return (r, g, b)


def read_bin_palettes(path):
    """Return list of 16 palettes, each a list of 16 (r,g,b). 512-byte region."""
    data = path.read_bytes()[:512]
    pals = []
    for p in range(16):
        pal = []
        for i in range(16):
            off = (p * 16 + i) * 2
            word = struct.unpack_from("<H", data, off)[0]
            pal.append(decode_bgr555(word))
        pals.append(pal)
    return pals


def palette_liveness(pal):
    """How 'real' a palette is: count of distinct non-black colors (index 0 is
    transparent, so skip it). Empty/all-black palettes score ~0-1."""
    distinct = set()
    for i, (r, g, b) in enumerate(pal):
        if i == 0:
            continue
        if (r, g, b) != (0, 0, 0):
            distinct.add((r, g, b))
    return len(distinct)


def read_bmp_palette(path):
    """Extract the 16-entry palette from a 4bpp indexed BMP (RGBQUAD: B,G,R,0)."""
    data = path.read_bytes()
    # BITMAPFILEHEADER(14) + biSize. Palette starts at 14 + biSize.
    bi_size = struct.unpack_from("<I", data, 14)[0]
    bpp = struct.unpack_from("<H", data, 28)[0]
    clr_used = struct.unpack_from("<I", data, 46)[0]
    n = clr_used if clr_used else (1 << bpp)
    n = min(n, 16)
    base = 14 + bi_size
    pal = []
    for i in range(n):
        b, g, r, _ = data[base + i * 4: base + i * 4 + 4]
        pal.append((r, g, b))
    return pal, bpp


def quantize555(c):
    """Quantize an 8-bit channel triple to the bin's 5-bit*8 grid for fair compare."""
    return tuple((v // 8) * 8 for v in c)


def palette_match_score(bin_pal, bmp_pal):
    """Lower = better. Sum of per-index nearest-channel distance after quantizing
    the BMP palette to the 5-bit grid. Compares indices 1..15 (skip transparent)."""
    total = 0
    n = min(len(bin_pal), len(bmp_pal))
    for i in range(1, n):
        a = bin_pal[i]
        b = quantize555(bmp_pal[i])
        total += sum(abs(x - y) for x, y in zip(a, b))
    return total


def hexs(pal):
    return " ".join(f"{r:02x}{g:02x}{b:02x}" for (r, g, b) in pal)


def main():
    print("=" * 78)
    print("HYDRA / TIAMAT SOURCE-BIN INVESTIGATION")
    print("=" * 78)

    # --- 1. Palette liveness + tier colors per candidate bin ------------------
    bin_pals = {}
    print("\n[1] Palette liveness (distinct non-black colors per palette; need 0/1/2 all live)\n")
    for name in CANDIDATE_BINS:
        path = UNIT / name
        if not path.exists():
            print(f"  {name:28} MISSING")
            continue
        pals = read_bin_palettes(path)
        bin_pals[name] = pals
        live = [palette_liveness(pals[i]) for i in range(16)]
        tier_live = live[:3]
        verdict = "3-TIER OK" if all(v >= 3 for v in tier_live) else "NOT 3-tier"
        print(f"  {name:28} pal0/1/2 distinct = {tier_live}   {verdict}")
        # show how many of the 16 palettes are live at all
        live_count = sum(1 for v in live if v >= 3)
        print(f"  {'':28} ({live_count}/16 palettes populated)")

    # --- 2. Tier color story for each live candidate --------------------------
    print("\n[2] Decoded tier palettes (pal0=tier I, pal1=tier II, pal2=tier III)\n")
    for name, pals in bin_pals.items():
        print(f"  {name}")
        for t in range(3):
            print(f"     pal{t}: {hexs(pals[t])}")
        print()

    # --- 3. Embedded BMP palettes --------------------------------------------
    print("[3] Tiamat HD BMP embedded palettes\n")
    bmp_pals = {}
    for bmp in TIAMAT_BMPS:
        path = SPRITES / bmp
        if not path.exists():
            print(f"  {bmp:24} MISSING")
            continue
        pal, bpp = read_bmp_palette(path)
        bmp_pals[bmp] = pal
        print(f"  {bmp:24} ({bpp}bpp): {hexs(pal)}")
    print()

    # --- 4. Cross-match: which bin palette best matches each BMP palette -------
    print("[4] Match scores (bin palette vs BMP palette; LOWER = better fit)\n")
    print(f"  {'bin / palette':34}" + "".join(f"{b[:14]:>16}" for b in bmp_pals))
    for name, pals in bin_pals.items():
        for t in range(3):
            row = f"  {name+' pal'+str(t):34}"
            for bmp, bpal in bmp_pals.items():
                row += f"{palette_match_score(pals[t], bpal):16d}"
            print(row)
    print()

    # --- 5. Verdict -----------------------------------------------------------
    print("[5] Best bin-palette match per BMP\n")
    for bmp, bpal in bmp_pals.items():
        best = None
        for name, pals in bin_pals.items():
            for t in range(3):
                score = palette_match_score(pals[t], bpal)
                if best is None or score < best[0]:
                    best = (score, name, t)
        print(f"  {bmp:24} -> {best[1]} pal{best[2]}  (score {best[0]})")


if __name__ == "__main__":
    main()
