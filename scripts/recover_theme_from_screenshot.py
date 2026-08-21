#!/usr/bin/env python
"""Recover a theme recipe from a theme-editor screenshot whose sliders are cut off.

WHY THIS EXISTS
---------------
Authoring a built-in theme normally means reading the hex under each section in the
theme editor (see docs/ADDING_AN_NPC_THEME.md). Screenshots routinely miss sections:
Valmafra has five and a single screen caught two. The sliders being off-frame does not
mean the information is gone, because the editor shows BOTH previews at once:

    Modified pane   = the sprite with the new palette
    Original pane   = the same pose, same background, vanilla palette

Both panes are nearest-neighbour renders of the same HD sheet, so every pixel of the
Modified pane is the Original pane's pixel run through the palette swap. Line the two
panes up and each original palette colour tells you exactly what it became.

The base colour itself is often unreadable: a section's base slot can be three pixels
wide and every one of them an anti-aliased blend. So this does not read the base. It
reads the SHADES, which are usually plentiful and clean, then inverts the shade maths
to find the only base that could have produced them.

WHAT IT ASSUMES
---------------
- Both preview panes are fully visible and show the SAME pose (do not rotate one).
- The screenshot is lossless (PNG). A JPEG will smear the palette and defeat this.
- The character's sprite is unmodified vanilla in the Original pane, i.e. the editor
  was showing 'original' as the template.

USAGE
-----
    python scripts/recover_theme_from_screenshot.py <screenshot.png> <Character>
    python scripts/recover_theme_from_screenshot.py shot.png Valmafra --theme dark_witch

Prints a per-index report and a ready-to-paste recipe for
ColorMod/Data/BuiltInThemes/NPC/<Character>.json.

VERIFY THE OUTPUT. Bake it, then diff the baked palette against the screenshot report;
the shades must match exactly. An index that draws NO pixels in the chosen pose cannot
be recovered at all; the tool warns, and that warning is the one you must not ignore.
"""

import argparse
import json
import os
import sys
from collections import Counter

import numpy as np
from PIL import Image

# A screenshot reading is trusted as a hard constraint only above these.
MIN_TRUSTED_PX = 20
MIN_TRUSTED_AGREEMENT = 0.8

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UNIT = os.path.join(REPO, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit")


# --------------------------------------------------------------------------
# Colour maths. Mirrors ColorMod/ThemeEditor/HslColor.cs and
# RelativeShadeGenerator.cs. If those change, this must change with them. The
# check that catches the drift is the per-section 'verified' verdict: a recovered
# base is re-baked here and compared against what was measured off the screen.
# --------------------------------------------------------------------------

def from_rgb(c):
    r, g, b = c[0] / 255.0, c[1] / 255.0, c[2] / 255.0
    mx, mn = max(r, g, b), min(r, g, b)
    d = mx - mn
    h = s = 0.0
    l = (mx + mn) / 2.0
    if d > 0:
        s = d / (mx + mn) if l < 0.5 else d / (2.0 - mx - mn)
        if mx == r:
            h = ((g - b) / d) + (6 if g < b else 0)
        elif mx == g:
            h = ((b - r) / d) + 2
        else:
            h = ((r - g) / d) + 4
        h *= 60
    return h, s, l


def _hue_to_rgb(p, q, t):
    if t < 0:
        t += 1
    if t > 1:
        t -= 1
    if t < 1.0 / 6.0:
        return p + (q - p) * 6 * t
    if t < 1.0 / 2.0:
        return q
    if t < 2.0 / 3.0:
        return p + (q - p) * (2.0 / 3.0 - t) * 6
    return p


def to_rgb(hsl):
    h, s, l = hsl
    if s == 0:
        gray = int(l * 255)
        return (gray, gray, gray)
    q = l * (1 + s) if l < 0.5 else l + s - l * s
    p = 2 * l - q
    hn = h / 360.0
    return tuple(int(round(v * 255)) for v in (
        _hue_to_rgb(p, q, hn + 1.0 / 3.0),
        _hue_to_rgb(p, q, hn),
        _hue_to_rgb(p, q, hn - 1.0 / 3.0)))


def quantize(c):
    """RGB -> BGR555 -> RGB, exactly as PaletteModifier stores a colour."""
    r5, g5, b5 = (c[0] * 31) // 255, (c[1] * 31) // 255, (c[2] * 31) // 255
    return ((r5 * 255) // 31, (g5 * 255) // 31, (b5 * 255) // 31)


def bake_section(originals, primary, new_base, uniform_hue=False):
    """One section through the shade generator and the BGR555 store."""
    ph, ps, pl = from_rgb(originals[primary])
    rel = {}
    for i, c in originals.items():
        ih, isat, il = from_rgb(c)
        if uniform_hue:
            rel[i] = (isat - ps, il - pl)
        else:
            rel[i] = (ih - ph,
                      isat / ps if ps > 0.001 else 1.0,
                      il / pl if pl > 0.001 else 1.0)
    bh, bs, bl = from_rgb(new_base)
    out = {}
    for i in originals:
        if i == primary:
            out[i] = quantize(new_base)
            continue
        if uniform_hue:
            ds, dl = rel[i]
            nh, ns, nl = bh, bs + ds, bl + dl
        else:
            dh, sr, lr = rel[i]
            nh = (bh + dh) % 360
            if nh < 0:
                nh += 360
            ns, nl = bs * sr, bl * lr
        out[i] = quantize(to_rgb((nh, min(1.0, max(0.0, ns)), min(1.0, max(0.0, nl)))))
    return out


# --------------------------------------------------------------------------
# Repo data
# --------------------------------------------------------------------------

def hx(c):
    return "#%02X%02X%02X" % tuple(c)


def load_mapping(character):
    for category in ("NPC", "Story", "Monster"):
        path = os.path.join(REPO, "ColorMod", "Data", "SectionMappings",
                            category, "%s.json" % character)
        if os.path.exists(path):
            with open(path, encoding="utf-8") as handle:
                return json.load(handle), path
    raise SystemExit("No section mapping found for %s" % character)


def load_original_palette(sprite_bin):
    """Palette 0 of the vanilla sprite, as index -> RGB."""
    path = os.path.join(UNIT, "sprites_original", sprite_bin)
    if not os.path.exists(path):
        raise SystemExit("Original sprite not found: %s" % path)
    with open(path, "rb") as handle:
        data = handle.read(32)
    palette = {}
    for i in range(16):
        v = data[i * 2] | (data[i * 2 + 1] << 8)
        palette[i] = (((v & 31) * 255) // 31,
                      (((v >> 5) & 31) * 255) // 31,
                      (((v >> 10) & 31) * 255) // 31)
    return palette


# --------------------------------------------------------------------------
# Screenshot reading
# --------------------------------------------------------------------------

def isolate_original_pane(masks, height, max_gap=12):
    """Restrict the masks to the ORIGINAL preview pane.

    Why this step is not optional: colours belonging to UNCHANGED sections appear
    in BOTH panes. Leaving those duplicates in makes the wrong offset score well
    (it lines the Modified pane's hair up with the Original pane's hair) while the
    changed indices quietly fall out of bounds and get dropped, so the tool reports
    a confident 'nothing changed'. That is the exact failure this guards.

    The Original pane is identified structurally: it is the horizontal band that
    carries the MOST DISTINCT palette indices, because every vanilla colour appears
    there whereas only the untouched ones survive into the Modified pane.
    """
    rows = np.zeros(height, dtype=bool)
    for ys, _ in masks.values():
        rows[ys] = True

    bands, start, gap = [], None, 0
    for y in range(height):
        if rows[y]:
            if start is None:
                start = y
            gap = 0
        elif start is not None:
            gap += 1
            if gap > max_gap:
                bands.append((start, y - gap))
                start = None
    if start is not None:
        bands.append((start, height - 1))

    scored = []
    for lo, hi in bands:
        distinct = pixels = 0
        for ys, _ in masks.values():
            n = int(((ys >= lo) & (ys <= hi)).sum())
            if n:
                distinct += 1
                pixels += n
        scored.append((distinct, pixels, lo, hi))
    if not scored:
        return None, None

    scored.sort(reverse=True)
    _, _, lo, hi = scored[0]

    trimmed = {}
    for idx, (ys, xs) in masks.items():
        keep = (ys >= lo) & (ys <= hi)
        if keep.sum():
            trimmed[idx] = (ys[keep], xs[keep])
    return trimmed, (lo, hi)


def find_offset(arr, masks, min_shift=40):
    """Find the (dy, dx) taking the Original pane onto the Modified pane.

    Objective: every original palette index must map to ONE colour, because a
    palette swap is a function. So the right offset is the one maximising, over
    all indexed pixels, the share agreeing with their index's modal target.

    Makes no assumption about WHICH sections changed. Guards against three ways
    of being confidently wrong: the identity offset (rejected via min_shift and a
    changed-index floor), offsets that push most pixels off-image (rejected via a
    coverage floor), and offsets that drop the changed indices entirely (rejected
    by requiring nearly every index to survive the shift).
    """
    h, w = arr.shape[:2]
    n_masks = len(masks)
    best = None
    for dy in range(-h + 1, h):
        if abs(dy) < min_shift:
            continue
        for dx in (0,):
            agree = total = kept_idx = changed = in_bounds = all_px = 0
            for idx, (ys, xs) in masks.items():
                ny, nx = ys + dy, xs + dx
                keep = (ny >= 0) & (ny < h) & (nx >= 0) & (nx < w)
                all_px += len(ny)
                in_bounds += int(keep.sum())
                if keep.sum() < 3:
                    continue
                kept_idx += 1
                target = arr[ny[keep], nx[keep]]
                packed = (target[:, 0].astype(np.int32) << 16 |
                          target[:, 1].astype(np.int32) << 8 |
                          target[:, 2].astype(np.int32))
                values, counts = np.unique(packed, return_counts=True)
                agree += int(counts.max())
                total += len(packed)
                src = arr[ys[0], xs[0]]
                if values[counts.argmax()] != (int(src[0]) << 16 | int(src[1]) << 8 | int(src[2])):
                    changed += 1
            # Every index must survive the shift; losing one means the offset is
            # walking the changed sections off the edge of the image.
            if total < 200 or changed < 2 or kept_idx < n_masks - 1:
                continue
            if all_px and in_bounds / all_px < 0.9:
                continue
            score = agree / total
            if best is None or score > best[0]:
                best = (score, dy, dx)
    return best


def read_new_colours(arr, masks, dy, dx):
    """index -> (new colour, agreement fraction, pixel count)."""
    h, w = arr.shape[:2]
    out = {}
    for idx, (ys, xs) in masks.items():
        ny, nx = ys + dy, xs + dx
        keep = (ny >= 0) & (ny < h) & (nx >= 0) & (nx < w)
        if keep.sum() == 0:
            continue
        target = arr[ny[keep], nx[keep]]
        counts = Counter(map(tuple, target))
        colour, n = counts.most_common(1)[0]
        out[idx] = (tuple(int(v) for v in colour), n / keep.sum(), int(keep.sum()))
    return out


# --------------------------------------------------------------------------
# Inversion
# --------------------------------------------------------------------------

def _vec_from_rgb(r, g, b):
    r, g, b = r / 255.0, g / 255.0, b / 255.0
    mx = np.maximum(r, np.maximum(g, b))
    mn = np.minimum(r, np.minimum(g, b))
    d = mx - mn
    l = (mx + mn) / 2.0
    with np.errstate(divide="ignore", invalid="ignore"):
        s = np.where(l < 0.5, d / (mx + mn), d / (2.0 - mx - mn))
        s = np.where(d > 0, s, 0.0)
        h = np.where(mx == r, ((g - b) / d) + np.where(g < b, 6.0, 0.0),
            np.where(mx == g, ((b - r) / d) + 2.0, ((r - g) / d) + 4.0))
        h = np.where(d > 0, h * 60.0, 0.0)
    return np.nan_to_num(h), np.nan_to_num(s), l


def _vec_hue_to_rgb(p, q, t):
    t = np.mod(t, 1.0)
    return np.where(t < 1.0 / 6.0, p + (q - p) * 6 * t,
           np.where(t < 1.0 / 2.0, q,
           np.where(t < 2.0 / 3.0, p + (q - p) * (2.0 / 3.0 - t) * 6, p)))


def _vec_to_rgb(h, s, l):
    q = np.where(l < 0.5, l * (1 + s), l + s - l * s)
    p = 2 * l - q
    hn = h / 360.0
    r = np.round(_vec_hue_to_rgb(p, q, hn + 1.0 / 3.0) * 255)
    g = np.round(_vec_hue_to_rgb(p, q, hn) * 255)
    b = np.round(_vec_hue_to_rgb(p, q, hn - 1.0 / 3.0) * 255)
    # C# takes the S==0 shortcut with a CAST, which truncates, not rounds.
    gray = np.floor(l * 255)
    zero = s == 0
    return (np.where(zero, gray, r), np.where(zero, gray, g), np.where(zero, gray, b))


def _vec_quantize(r, g, b):
    out = []
    for c in (r, g, b):
        k = (c.astype(np.int64) * 31) // 255
        out.append((k * 255) // 31)
    return out


def invert_section(section, original, observed):
    """Find every base colour reproducing the section's OBSERVED shades.

    Searches the FULL 8-bit RGB cube, not just colours that are already
    representable in BGR555. The base is applied BEFORE quantization, so the
    value someone picked on a slider is usually off-grid: #2D2D2D is a real base
    that stores as #292929, and an on-grid-only search misses it entirely and
    reports a confident 'no solution'.

    Scored on the non-base indices only; the base slot is frequently three
    blended pixels and cannot be trusted.
    """
    indices = section["indices"]
    roles = section.get("roles", [])
    primary = section.get("primaryIndex")
    if primary is None:
        primary = indices[roles.index("base")] if "base" in roles else indices[0]
    uniform = section.get("shadeMode") == "uniformHue"
    originals = {i: original[i] for i in indices}

    targets = {i: observed[i][0] for i in indices
               if i in observed and i != primary}

    # The base slot is normally ignored, being small and blend-prone. But a
    # section can have its shade slots entirely hidden in the chosen pose (a
    # ribbon whose shadow is not drawn at all), leaving the base as the ONLY
    # evidence. Use it when the reading is substantial enough to trust.
    use_primary = (primary in observed
                   and observed[primary][2] >= MIN_TRUSTED_PX
                   and observed[primary][1] >= MIN_TRUSTED_AGREEMENT)
    if not targets and not use_primary:
        return primary, uniform, []

    # Captured relationships, exactly as RelativeShadeGenerator does.
    ph, ps, pl = from_rgb(originals[primary])
    rel = {}
    for i, c in originals.items():
        ih, isat, il = from_rgb(c)
        rel[i] = ((isat - ps, il - pl) if uniform else
                  (ih - ph,
                   isat / ps if ps > 0.001 else 1.0,
                   il / pl if pl > 0.001 else 1.0))

    gg, bb = np.meshgrid(np.arange(256), np.arange(256), indexing="ij")
    gg, bb = gg.ravel(), bb.ravel()
    exact = []
    for rv in range(256):
        rr = np.full_like(gg, rv)
        bh, bs, bl = _vec_from_rgb(rr, gg, bb)
        ok = np.ones(len(gg), dtype=bool)
        if use_primary:
            want = observed[primary][0]
            qr, qg, qb = _vec_quantize(rr, gg, bb)
            ok &= (qr == want[0]) & (qg == want[1]) & (qb == want[2])
        for i, want in targets.items():
            if not ok.any():
                break
            if uniform:
                ds, dl = rel[i]
                nh, ns, nl = bh, bs + ds, bl + dl
            else:
                dh, sr, lr = rel[i]
                nh = np.mod(bh + dh, 360.0)
                ns, nl = bs * sr, bl * lr
            ns = np.clip(ns, 0.0, 1.0)
            nl = np.clip(nl, 0.0, 1.0)
            qr, qg, qb = _vec_quantize(*_vec_to_rgb(nh, ns, nl))
            ok &= (qr == want[0]) & (qg == want[1]) & (qb == want[2])
            if not ok.any():
                break
        for j in np.nonzero(ok)[0]:
            exact.append((rv, int(gg[j]), int(bb[j])))
    return primary, uniform, exact


# --------------------------------------------------------------------------

def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("screenshot")
    ap.add_argument("character")
    ap.add_argument("--theme", default="new_theme", help="theme key for the emitted recipe")
    ap.add_argument("--min-shift", type=int, default=40,
                    help="ignore offsets smaller than this (guards the identity match)")
    args = ap.parse_args()

    mapping, mapping_path = load_mapping(args.character)
    sprite = mapping["sprites"][0]
    original = load_original_palette(sprite)

    print("character : %s" % args.character)
    print("mapping   : %s" % os.path.relpath(mapping_path, REPO))
    print("sprite    : %s" % sprite)

    img = Image.open(args.screenshot).convert("RGB")
    arr = np.asarray(img)

    # Locate every pixel carrying a vanilla palette colour. Index 0 is
    # transparent and is skipped; duplicate colours would be ambiguous.
    seen = {}
    masks = {}
    for i in range(1, 16):
        c = original[i]
        if c in seen:
            print("  note: index %d duplicates index %d (%s); skipped"
                  % (i, seen[c], hx(c)))
            continue
        seen[c] = i
        hit = np.where((arr[:, :, 0] == c[0]) & (arr[:, :, 1] == c[1]) & (arr[:, :, 2] == c[2]))
        if len(hit[0]):
            masks[i] = hit

    if len(masks) < 3:
        raise SystemExit("Found too few vanilla palette colours in the screenshot. "
                         "Is the Original pane visible, and is the image lossless PNG?")

    masks, band = isolate_original_pane(masks, arr.shape[0])
    if not masks:
        raise SystemExit("Could not isolate the Original preview pane.")
    print("original pane: rows %d-%d, %d palette colours present" % (band[0], band[1], len(masks)))

    best = find_offset(arr, masks, args.min_shift)
    if best is None:
        raise SystemExit("Could not align the two preview panes. Are both fully "
                         "visible, in the same pose, in this screenshot?")
    score, dy, dx = best
    print("pane offset: dy=%d dx=%d  (agreement %.1f%%)" % (dy, dx, score * 100))
    if score < 0.8:
        print("  WARNING: weak alignment. Treat every number below as suspect.")

    observed = read_new_colours(arr, masks, dy, dx)

    print("\nper-index reading")
    print("  idx  original   ->  new        px   agree")
    for i in sorted(observed):
        c, frac, n = observed[i]
        flag = "" if c != original[i] else "   (unchanged)"
        print("  %3d  %s  ->  %s  %4d  %5.1f%%%s"
              % (i, hx(original[i]), hx(c), n, frac * 100, flag))

    recipe_sections = {}
    print("\nper-section inversion")
    for section in mapping["sections"]:
        name = section["name"]
        indices = section["indices"]
        touched = [i for i in indices if i in observed and observed[i][0] != original[i]]
        if not touched:
            print("  %-12s unchanged" % name)
            continue

        primary, uniform, exact = invert_section(section, original, observed)
        mode = "uniformHue" if uniform else "preserve"
        if exact:
            # Many bases can produce identical stored shades, because the palette
            # only holds 5 bits per channel. They are interchangeable. Narrow with
            # the observed base slot when it is trustworthy, then take the centroid
            # so the answer sits in the middle of the valid region rather than on
            # its edge, where a rounding change would push it out.
            pool = exact
            if primary in observed:
                obs = observed[primary][0]
                trusted = (observed[primary][2] >= MIN_TRUSTED_PX
                           and observed[primary][1] >= MIN_TRUSTED_AGREEMENT)
                narrowed = [b for b in pool if quantize(b) == obs]
                if narrowed:
                    pool = narrowed
                elif not trusted:
                    # The base slot reading is too small or too blended to use as a
                    # hard constraint, but it is still evidence: a blend contains the
                    # true colour. Keep the candidates whose stored base sits nearest
                    # to it rather than discarding the only signal we have. Without
                    # this the base drifts to the centroid of a wide equivalence
                    # class and the shades still 'verify', so the error is invisible.
                    dist = [sum((a - b) ** 2 for a, b in zip(quantize(base), obs))
                            for base in pool]
                    lo = min(dist)
                    pool = [b for b, d in zip(pool, dist) if d == lo]
            arr_pool = np.array(pool)
            centroid = arr_pool.mean(axis=0)
            pick = tuple(int(v) for v in
                         arr_pool[np.argmin(((arr_pool - centroid) ** 2).sum(axis=1))])

            got = bake_section({i: original[i] for i in indices}, primary, pick, uniform)
            bad = [i for i in observed
                   if i in indices and i != primary and got[i] != observed[i][0]]
            verdict = "verified" if not bad else "MISMATCH at %s" % bad
            recipe_sections[name] = hx(pick)
            print("  %-12s base %s   (%d equivalent solution%s, %s, %s)"
                  % (name, hx(pick), len(exact), "" if len(exact) == 1 else "s", mode, verdict))

            # An index drawing zero pixels in this pose is UNRECOVERABLE. The
            # recipe will still write a colour there, derived from the shade
            # maths, and it may not be the one the author's slider produced.
            # Observed for Valmafra: the ribbon shadow (index 6) is not drawn at
            # all, and two recipes agreeing on every visible pixel still differed
            # there. Nothing downstream can detect this, so it must be said here.
            blind = [i for i in indices if i not in observed]
            if blind:
                print("  %-12s   WARNING: index %s draws no pixels in this pose, so its"
                      % ("", ", ".join(str(i) for i in blind)))
                print("  %-12s   colour cannot be read or verified. The value baked there"
                      % "")
                print("  %-12s   is a guess. Confirm it against the slider if it matters."
                      % "")
        else:
            print("  %-12s NO SOLUTION. Observed shades are not reachable from any"
                  % name)
            print("  %-12s base. Likely a blended/occluded reading, or the pane is"
                  % "")
            print("  %-12s misaligned. Read this section off the sliders instead."
                  % "")

    print("\nrecipe for ColorMod/Data/BuiltInThemes/NPC/%s.json" % args.character)
    print(json.dumps({
        "character": args.character,
        "themes": [{"name": args.theme, "sections": recipe_sections}],
    }, indent=2))
    print("\nBake it, then confirm the baked shades match the per-index reading above.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
