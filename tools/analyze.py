#!/usr/bin/env python3
"""Release-package content gate (CC-10).

Verifies a built release zip against tools/package_manifest.json:
  - no backslash entry names (strict extractors flatten those; the v3.0.x defect)
  - every manifest required_entries file present
  - listed Data JSONs parse
  - per-prefix file-count floors (sprite payload completeness)
  - minimum count of sprites_* theme dirs

Exit 0 = pass, exit 1 = any violation (listed). Run by Publish.ps1 as a hard gate.

Usage: python tools/analyze.py --zip <path-to-release-zip> [--manifest <path>]
"""
import argparse
import json
import os
import sys
import zipfile


def load_entries(zf):
    names = zf.namelist()
    bad = [n for n in names if "\\" in n]
    files = [n for n in names if not n.endswith("/")]
    return files, bad


def strip_wrapper(files):
    """The zip wraps everything in one top-level mod folder; strip it if so."""
    tops = {f.split("/", 1)[0] for f in files}
    if len(tops) == 1 and all("/" in f for f in files):
        w = tops.pop()
        return [f[len(w) + 1:] for f in files], w
    return files, None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--zip", required=True)
    ap.add_argument("--manifest",
                    default=os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                         "package_manifest.json"))
    args = ap.parse_args()

    with open(args.manifest, encoding="utf-8") as fh:
        manifest = json.load(fh)

    failures = []
    zf = zipfile.ZipFile(args.zip)
    files, bad_slashes = load_entries(zf)

    if bad_slashes:
        failures.append(f"{len(bad_slashes)} entries use backslashes "
                        f"(flatten on strict extractors), e.g. {bad_slashes[0]}")

    files, wrapper = strip_wrapper(files)
    fileset = set(files)
    print(f"analyze.py: {len(files)} files"
          + (f" under wrapper '{wrapper}/'" if wrapper else " (no wrapper folder)"))

    for req in manifest["required_entries"]:
        if req not in fileset:
            failures.append(f"required entry missing: {req}")

    for jpath in manifest.get("parse_json", []):
        if jpath not in fileset:
            continue  # absence already reported above if required
        full = (wrapper + "/" + jpath) if wrapper else jpath
        try:
            json.loads(zf.read(full))
        except Exception as exc:  # noqa: BLE001 - any parse failure fails the gate
            failures.append(f"JSON does not parse: {jpath} ({exc})")

    for floor in manifest.get("floors", []):
        prefix, suffix = floor["prefix"], floor.get("suffix", "")
        count = sum(1 for f in files if f.startswith(prefix) and f.endswith(suffix))
        if count < floor["min"]:
            failures.append(f"floor violated: {count} files at {prefix}*{suffix} "
                            f"(need >= {floor['min']}: {floor['desc']})")
        else:
            print(f"  ok: {count} files at {prefix}*{suffix} (floor {floor['min']})")

    unit_prefix = "FFTIVC/data/enhanced/fftpack/unit/"
    theme_dirs = {f[len(unit_prefix):].split("/", 1)[0]
                  for f in files
                  if f.startswith(unit_prefix + "sprites_") and "/" in f[len(unit_prefix):]}
    if len(theme_dirs) < manifest["min_theme_dirs"]:
        failures.append(f"only {len(theme_dirs)} sprites_* theme dirs under unit/ "
                        f"(need >= {manifest['min_theme_dirs']})")
    else:
        print(f"  ok: {len(theme_dirs)} sprites_* theme dirs (floor {manifest['min_theme_dirs']})")

    if failures:
        print(f"\nanalyze.py: FAIL ({len(failures)} violations):", file=sys.stderr)
        for f in failures:
            print(f"  - {f}", file=sys.stderr)
        sys.exit(1)
    print("analyze.py: PASS")


if __name__ == "__main__":
    main()
