#!/usr/bin/env bash
# Narrow a pair of diffs to stable, clean state-discriminator candidates.
#
# Usage: ./scripts/narrow_diff.sh <forward_diff.txt> <reverse_diff.txt>
#
# Intersects two diffs (A→B and B→A) to find addresses that flip in both directions.
# Filters to clean small-integer transitions (likely menu IDs/boolean flags).
#
# Input format per line:
#   0xADDR: XX → YY  (byte: N1 → N2)

set -euo pipefail

FWD="${1:?forward diff file}"
REV="${2:?reverse diff file}"

if [[ ! -f "$FWD" ]]; then echo "Missing: $FWD" >&2; exit 1; fi
if [[ ! -f "$REV" ]]; then echo "Missing: $REV" >&2; exit 1; fi

TMPDIR=$(mktemp -d)
trap "rm -rf $TMPDIR" EXIT

# Extract (addr, from, to) triples for each diff
parse() {
  # Output: ADDR  FROM_BYTE  TO_BYTE
  grep -E "^  0x[0-9A-F]+:" "$1" | awk -F'[ :→()]+' '{
    # After split: "", "0xADDR", "", "FROM_HEX", "", "TO_HEX", "", "byte", "FROM_N", "TO_N"
    # Fields vary based on whitespace; simpler: strip prefixes manually
  }' || true
  # Fallback: regex-based extraction
  grep -oE "0x[0-9A-F]+: [0-9A-F]+ → [0-9A-F]+  \(byte: [0-9]+ → [0-9]+\)" "$1" | sed -E 's/0x([0-9A-F]+): ([0-9A-F]+) → ([0-9A-F]+)  \(byte: ([0-9]+) → ([0-9]+)\)/\1 \4 \5/'
}

parse "$FWD" | sort > "$TMPDIR/fwd.txt"
parse "$REV" | sort > "$TMPDIR/rev.txt"

# Intersect: address in both, forward from == reverse to, forward to == reverse from
# (ie, A→B and B→A, with matching values reversed)
echo "=== Addresses that toggle consistently in both directions ==="
join "$TMPDIR/fwd.txt" "$TMPDIR/rev.txt" | awk '
{
  # addr fwd_from fwd_to rev_from rev_to
  addr=$1; ff=$2; ft=$3; rf=$4; rt=$5;
  # Consistent toggle: fwd_from == rev_to AND fwd_to == rev_from
  if (ff == rt && ft == rf) {
    # Small integer filter — skip large/pointer-like values
    if (ff <= 50 && ft <= 50) {
      printf "0x%s : %d ↔ %d\n", addr, ff, ft;
    }
  }
}' | sort -u | head -60

echo
echo "=== Total addresses that toggled in BOTH diffs (any values): $(join "$TMPDIR/fwd.txt" "$TMPDIR/rev.txt" | wc -l) ==="
