# Memory scan plan — pending your return

## Goal
Find a stable memory address that distinguishes the "byte-identical" state pairs from the detection audit:

**Pair 1 (highest priority):**
- `TravelList` at a location (world map → Enter on location → see travel menu)
- `LocationMenu` at same location (after confirming travel, shop/service list visible)

**Pair 2 (secondary):**
- `LocationMenu` vs `WorldMap` when rawLocation is sticky

## The problem with the first attempt

I tried `heap_snap` + `heap_diff` earlier today. Got a 30MB diff with 694K changed bytes — too noisy. UE4 heap snapshots include:
- Per-frame rendering state
- Animation timers
- UI widget allocations
- Garbage collection churn

None of which help us find the screen-state discriminator.

## Better approach — module_snap

I added a new helper `module_snap` (in fft.sh) that snapshots only **FFT's main module writable regions** (~0x140000000 range) — where all our known screen-state addresses live. Should be ~10-100x less noisy than heap snapshots.

## Protocol when you return

Run this with me driving the bash side, you driving the game side:

### Round 1 — LocationMenu vs TravelList
```bash
# 1. Put game at TravelList (world map → Enter on Dorter → see travel details panel)
source ./fft.sh
module_snap travel
# Tell Claude: "at TravelList"

# 2. Advance to LocationMenu (press Enter again to actually enter Dorter, see shop list)
module_snap location_a
# Tell Claude: "at LocationMenu"

# 3. Back out to TravelList (press Escape twice or similar)
module_snap travel_b
# Tell Claude: "back at TravelList"

# 4. Forward to LocationMenu again
module_snap location_b

# 5. Claude diffs A→A' and B→B', intersects to find stable toggles
```

### Round 2 — verify with different location
Repeat with a different town (e.g. Warjilis) to rule out location-specific artifacts.

## What Claude will do with the diffs

1. Run `fft '{"id":"x","action":"diff","fromLabel":"travel","toLabel":"location_a","searchLabel":"t_to_l_a"}'`
2. Run forward and reverse diffs
3. Intersect candidates: addresses that change in the forward diff AND the reverse diff (with opposite values) are real signal
4. Filter to "small, clean" transitions (0→1, 0→5, etc.) — skip anything that looks like a pointer or timer
5. Narrow to 1-3 candidates
6. Verify each by reading via `rv <addr> 1` at each state and confirming consistency

## Fallback if module_snap is also noisy

If the module snapshot still has thousands of changed bytes, narrow further by:
- Only looking at addresses where BOTH diffs saw a flip (A→B and B→A)
- Stripping addresses near our known-address regions (already characterized)
- Focusing on transitions to small integer values that look like menu IDs

## What's ready

- ✓ `module_snap <label>` helper added to fft.sh
- ✓ Existing `heap_diff <from> <to> <out>` works with any snapshot labels
- ✓ `rv <addr> <size>` for verification reads

Waiting on you to drive the state transitions.
