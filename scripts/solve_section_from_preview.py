#!/usr/bin/env python
"""Recover ONE section's base colour from a Modified-preview image alone.

WHEN TO USE THIS INSTEAD OF recover_theme_from_screenshot.py
-----------------------------------------------------------
The sibling script needs BOTH preview panes so it can align them and read what
each vanilla colour became. Sometimes that is not what got captured: a set of
screenshots may be a cropped preview plus a few scrolled slider panels, with one
section's hex below the fold and no Original pane anywhere.

That is still solvable, because the preview is a nearest-neighbour render of a
16-colour palette. Every colour the sprite draws is IN the image. So instead of
measuring what a section became, this proposes a base, bakes the section, and
asks whether all of the resulting colours actually appear. Almost no base
survives that for a 3+ index section.

WHAT IT CANNOT DO
-----------------
- It cannot see an index the pose does not draw. Same blind spot as the sibling
  script, same consequence: the value baked there is a guess.
- It is a MEMBERSHIP test, not a position test. A base whose colours all happen
  to occur somewhere in the image passes, even on unrelated pixels. Sections with
  few indices are therefore weakly constrained; the report gives the solution
  count so you can see how tight the answer is. Treat a 1-index section
  (SkinColor) as unverifiable by this method.
- Backgrounds are not excluded. Raise --min-count to suppress stray matches.

USAGE
-----
    python scripts/solve_section_from_preview.py <preview.png> Isilud Surcoat
    python scripts/solve_section_from_preview.py <preview.png> Isilud Armor --known "#866C4B"

--known checks a value you already read off a slider instead of searching: it
reports whether that base's colours are present, which is a cheap way to confirm
the image really is the theme you think it is before trusting anything else.
"""

import argparse
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, __file__.rsplit("\\", 1)[0].rsplit("/", 1)[0])

from recover_theme_from_screenshot import (  # noqa: E402
    _vec_from_rgb, _vec_quantize, _vec_to_rgb, bake_section, from_rgb, hx,
    load_mapping, load_original_palette, quantize,
)


def parse_hex(h):
    h = h.strip().lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16))


def build_presence(image, min_count):
    """3D lookup: present[r, g, b] is True if that exact colour is drawn enough."""
    from collections import Counter
    counts = Counter(image.getdata())
    present = np.zeros((256, 256, 256), dtype=bool)
    for colour, n in counts.items():
        if n >= min_count:
            present[colour[0], colour[1], colour[2]] = True
    return present, counts


def section_of(mapping, name):
    for s in mapping["sections"]:
        if s["name"].lower() == name.lower():
            return s
    raise SystemExit("Section %r not in mapping. Have: %s"
                     % (name, ", ".join(s["name"] for s in mapping["sections"])))


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("preview")
    ap.add_argument("character")
    ap.add_argument("section")
    ap.add_argument("--known", help="verify this base instead of searching")
    ap.add_argument("--min-count", type=int, default=15,
                    help="a colour must be drawn at least this many times to count")
    args = ap.parse_args()

    mapping, mapping_path = load_mapping(args.character)
    palette = load_original_palette(mapping["sprites"][0])
    section = section_of(mapping, args.section)

    indices = section["indices"]
    roles = section.get("roles", [])
    primary = section.get("primaryIndex")
    if primary is None:
        primary = indices[roles.index("base")] if "base" in roles else indices[0]
    uniform = section.get("shadeMode") == "uniformHue"
    originals = {i: palette[i] for i in indices}

    image = Image.open(args.preview).convert("RGB")
    present, counts = build_presence(image, args.min_count)

    print("character : %s   section: %s (%s, %d indices, base slot %d)"
          % (args.character, section["name"],
             "uniformHue" if uniform else "preserve", len(indices), primary))
    print("preview   : %s  %dx%d" % (args.preview.split("\\")[-1], *image.size))

    # THE UNCHANGED TEST, and it is this one rather than anything cleverer.
    #
    # If a section was never touched, the bin still holds its vanilla colours, so
    # the preview literally draws them. Requiring EVERY index of the section to be
    # present makes an accidental hit very unlikely. The threshold is 1, not
    # --min-count, because a section can legitimately have a slot only a handful of
    # pixels wide (Alma's dress-stripe shadow draws 5).
    #
    # Two cleverer tests were tried and both were wrong in practice:
    #   - "vanilla base survives the search": fails for untouched sections, because
    #     BGR555 quantization is not idempotent, so re-baking vanilla does not
    #     reproduce vanilla exactly.
    #   - "search returns NO SOLUTION": true for some untouched sections and false
    #     for others (Argath's HairColor happily returns a near-vanilla base).
    #
    # This also catches the trap that the slider hex CANNOT: the editor shows H/S/L
    # truncated to whole numbers, so an untouched section displays a hex slightly
    # off its true colour (vanilla #8B4A18 shows as #864818). Anyone reading that
    # hex would bake a wrong colour and every test would still pass.
    vanilla_hits = [(i, counts.get(palette[i], 0)) for i in indices]
    vanilla_all_present = all(n >= 1 for _, n in vanilla_hits)

    if vanilla_all_present and not args.known:
        print("\nUNCHANGED: every vanilla colour of this section is still drawn, so the")
        print("           section was not touched. Omit it from the recipe.")
        for i, n in vanilla_hits:
            print("   idx %2d %s  x%d" % (i, hx(palette[i]), n))
        return 0

    if args.known:
        base = parse_hex(args.known)
        got = bake_section(originals, primary, base, uniform)
        print("\nchecking known base %s" % hx(base))
        allok = True
        for i in indices:
            n = counts.get(got[i], 0)
            ok = n >= args.min_count
            allok &= ok
            print("   idx %2d -> %s  x%-6d %s" % (i, hx(got[i]), n, "ok" if ok else "NOT DRAWN"))
        print("   verdict:", "consistent with this image" if allok
              else "NOT consistent (wrong theme, wrong crop, or a hidden index)")
        return 0

    # Search: every index the section owns must land on a colour the image draws.
    gg, bb = np.meshgrid(np.arange(256), np.arange(256), indexing="ij")
    gg, bb = gg.ravel(), bb.ravel()

    ph, ps, pl = from_rgb(originals[primary])
    rel = {}
    for i, c in originals.items():
        ih, isat, il = from_rgb(c)
        rel[i] = ((isat - ps, il - pl) if uniform else
                  (ih - ph,
                   isat / ps if ps > 0.001 else 1.0,
                   il / pl if pl > 0.001 else 1.0))

    solutions = []
    for rv in range(256):
        rr = np.full_like(gg, rv)
        bh, bs, bl = _vec_from_rgb(rr, gg, bb)
        ok = np.ones(len(gg), dtype=bool)
        pred = {}
        for i in indices:
            if i == primary:
                qr, qg, qb = _vec_quantize(rr, gg, bb)
            else:
                if uniform:
                    ds, dl = rel[i]
                    nh, ns, nl = bh, bs + ds, bl + dl
                else:
                    dh, sr, lr = rel[i]
                    nh = np.mod(bh + dh, 360.0)
                    ns, nl = bs * sr, bl * lr
                qr, qg, qb = _vec_quantize(
                    *_vec_to_rgb(nh, np.clip(ns, 0, 1), np.clip(nl, 0, 1)))
            pred[i] = (qr, qg, qb)
            ok &= present[qr, qg, qb]
        if not ok.any():
            continue

        # Reject collapses. A very dark or very desaturated base clips every
        # shade onto the same near-black, and near-black is abundant in any
        # sprite because it outlines everything -- so such a base passes the
        # membership test on the OUTLINE rather than on the section. Observed
        # live: a Surcoat search returned #0B0B0B matching 6944 black pixels.
        # A genuine recolour keeps shades that were distinct distinct.
        for a in range(len(indices)):
            for b in range(a + 1, len(indices)):
                ia, ib = indices[a], indices[b]
                if originals[ia] == originals[ib]:
                    continue
                pa, pb = pred[ia], pred[ib]
                ok &= (pa[0] != pb[0]) | (pa[1] != pb[1]) | (pa[2] != pb[2])
            if not ok.any():
                break

        for j in np.nonzero(ok)[0]:
            solutions.append((rv, int(gg[j]), int(bb[j])))

    if not solutions:
        print("\nNO SOLUTION. Either this preview is not that theme, the section's")
        print("indices are not drawn in this pose, or --min-count is too high.")
        return 1

    # Every solution paints the same pixels; report the spread so the reader can
    # see whether the answer is tight or the section was weakly constrained.
    arr = np.array(solutions)
    stored = sorted({quantize(tuple(int(v) for v in b)) for b in arr})
    centroid = arr.mean(axis=0)
    pick = tuple(int(v) for v in arr[np.argmin(((arr - centroid) ** 2).sum(axis=1))])
    got = bake_section(originals, primary, pick, uniform)

    # The decisive unchanged test: if the section really was left alone, the
    # VANILLA base is itself a valid solution. That is far stronger than asking
    # whether vanilla colours happen to occur somewhere in the frame.
    vanilla_base = quantize(originals[primary])
    vanilla_survives = vanilla_base in stored

    print("\n%d candidate base%s, collapsing to %d distinct stored base colour%s"
          % (len(solutions), "" if len(solutions) == 1 else "s",
             len(stored), "" if len(stored) == 1 else "s"))
    if vanilla_survives:
        print("UNCHANGED: the vanilla base %s is itself a solution, so this section"
              % hx(vanilla_base))
        print("           was most likely never touched.")
        if len(stored) > 1:
            print("           (%d bases fit; vanilla is one of them)" % len(stored))
        return 0
    if vanilla_all_present:
        print("note: every vanilla colour of this section appears in the frame, but the")
        print("      vanilla base does NOT fit the shades, so the section did change.")
    print("best estimate: %s" % hx(pick))
    for i in indices:
        print("   idx %2d -> %s  x%d" % (i, hx(got[i]), counts.get(got[i], 0)))
    if len(stored) > 1:
        print("\n   NOTE: the stored base is not pinned down (%d possibilities: %s)."
              % (len(stored), ", ".join(hx(s) for s in stored[:6])
                 + (" ..." if len(stored) > 6 else "")))
        print("   The shades above are consistent either way, but confirm against")
        print("   the slider if this section matters.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
