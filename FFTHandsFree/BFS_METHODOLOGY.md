# BFS Movement-Tile Calculation — Methodology & Debugging Guide

> **When the BFS mismatch warning fires, read this file first.**
> It captures everything we've learned across sessions 22–28 about how
> valid-move tiles work in FFT: The Ivalice Chronicles and why our BFS
> (`ColorMod/GameBridge/MovementBfs.cs`) disagrees with the game.

---

## What the mismatch warning means

The mod logs a warning like:
```
[BFS OVERCOUNT] BFS reported 21 valid tiles; game memory reports 11 (delta=10). BFS tile list may be wrong.
```

…and surfaces it in the compact `screen` response on BattleMoving as:
```
⚠ [BFS OVERCOUNT] BFS reported 21 valid tiles; game memory reports 11 (delta=10). BFS tile list may be wrong.
   (review FFTHandsFree/BFS_METHODOLOGY.md for context and debugging playbook)
```

The mismatch means the BFS-computed tile list (`screen.Tiles`) disagrees with
whatever signal we currently believe encodes the game's own count. **Currently
the warning is disabled** because the byte we thought encoded the count
(`0x142FEA008`) doesn't actually. See §6 below. When a real count signal is
found and re-wired, this file is where we'll document it.

---

## 1. Why BFS is the primary path

We use BFS for runtime tile calculation (not memory extraction). Memory
extraction turned out to be a dead end — see §2. BFS produces a tile list
instantly during `scan_move` and is the authoritative `screen.Tiles` source
in the response.

BFS lives in:

- `ColorMod/GameBridge/MovementBfs.cs` — pure functions: `ComputeValidTiles(map, startX, startY, move, jump, enemyPositions, allyPositions)`, `ApplyMovementAbility(move, jump, abilityName)`.
- `ColorMod/Utilities/CommandWatcher.PopulateBattleTileData` — wires BFS into `screen.Tiles` during BattleMoving.
- `ColorMod/GameBridge/MapLoader.cs` — loads per-map JSON with per-tile height, slope type, depth, and no-walk flags.

---

## 2. Why we don't extract tiles from memory (dead ends, session 22–28)

We spent many hours across 4+ sessions trying to find a memory address that
stores the game's own valid-tile list. **It is not stored as a persistent
bitmap.** Full findings in `memory/project_move_bitmap_hunt_s28.md`. Short
version of what's ruled out:

- `0x140C66315` (7 bytes/entry, `[X][Y][elev][flag][0][0][0]`) — this is the
  **movement-zone outline** (perimeter) in world coords, not all valid tiles.
  World coords extend beyond the grid (X up to 29 on an 11×12 map), so it's
  not usable as a tile list.
- `0x140C6F400` (stride 0x88, flag at `+0x1D`) — per-frame rendering struct.
  Volatile; baseline already has ~95 flags set, Move mode adds only 16-19,
  counts swing 13-19 across reads. Not stable.
- `0x14077CA5C` — Move-mode flag (0xFF during Move, 0x00 otherwise). Useful
  as a gate, not a tile list.
- `0x142FEA000` region (4 KB) — contains static map-topology adjacency data
  (14 blocks × 46 bytes, alphabet `{0x4, 0x8, 0xC}` per nibble). **Identical
  for Kenrick vs Lloyd on the same map** — so this is map-wide adjacency,
  not per-unit valid tiles.
- `0x142FEA008` — toggles on Move-mode entry/exit, briefly believed to be
  tileCount. **Later live-verified FALSE**: reads `0x0B` (11) for Lloyd on
  Siedge Weald while user visually counted 20 blue tiles. Do not use.
- Render slot array at `0x140DE2xxx` + mirror `0x140F95xxx` — UE4 mesh
  triangle records with vertex indices. Flag bit `0x04` at `+0x06` marks
  valid tiles; `0x05` marks cursor-on-valid. But slot addresses are
  dynamically reassigned per cursor move (ring-buffer / LRU pool), so there
  is no stable (x,y)→slot mapping.
- `battleMode` at `0x140900650` — `2` on unit's own tile, `1` on any non-unit
  tile (valid or invalid indiscriminately). Not granular enough.

**Conclusion (session 28):** the game computes valid tiles on the fly from
static map adjacency + unit stats. No persistent per-unit bitmap exists
anywhere reachable via memory-diff or AoB search. Stop hunting.

---

## 3. The `cursor_walk` diagnostic

`cursor_walk` (shell helper + bridge action) is our experimental tool for
extracting ground-truth valid tiles. It:

1. Reads current cursor (x,y).
2. Calibrates arrow keys (presses each once, observes cursor delta,
   auto-recalibrates when a direction is edge-blocked at the start but
   usable after drift).
3. Flood-fills from the unit's position via `CursorFloodFill`. The predicate
   navigates the cursor to each candidate tile, snapshots before/after,
   and counts `0x04 → 0x05` transitions in `0x140DDF000..0x140DE8000`
   (cursor landing on a valid-move render slot).
4. Compares the flood-fill result against `screen.Tiles` (our BFS output)
   via `BfsTileVerifier.Compare` and logs agreements / false positives /
   false negatives.

**Current status:** detects only ~5 of 20 valid tiles for Lloyd at Siedge
Weald. The transition-count probe is too narrow. See §0 Urgent Bugs in
`TODO.md` for the probe-reliability work.

**Manual fallback when `cursor_walk` is unreliable:** the user counts blue
tiles visually (we call it out-of-band "how many blue tiles?") and we fix
BFS against the ground-truth count + the BFS tile list.

---

## 4. The canonical PSX BFS rules (the target algorithm)

FFT's movement rules are well-documented online (GameFAQs, FFHacktics,
Reddit). Rather than reverse-engineer from scratch, port from the
canonical reference. Core rules:

- **Cardinal expansion** from unit's tile; each step costs 1 Move.
- **Height check**: `|h_from - h_to| ≤ Jump`. Height is the tile surface
  height (integer; slope height adds a half-unit).
- **Slopes**: a tile with a slope has an "enter height" and "exit height"
  that depend on the direction of travel. E.g., entering "Incline W"
  from the east enters at the tile's base height; exiting to the west
  uses base + slope_height. Missing this causes false positives on steep
  cliff faces.
- **Depth (water)**: tiles with `depth > 0` cost extra to enter; certain
  depth thresholds block non-aquatic units entirely (depth > 2 typically).
- **No-Walk flag**: blocks entry unconditionally (trees, rocks, etc).
- **Occupied tiles**: can pass through teammates (with penalty in the
  ally-traversal heuristic) but cannot pass through enemies; cannot stop
  on ANY occupied tile.
- **Movement abilities**: Move+N adds to move, Jump+N adds to jump,
  Waterwalking / Fly / Teleport / Ignore Height change the rules entirely.
  `ApplyMovementAbility` already handles Move+1/+2/+3 and Jump+1/+2/+3;
  the exotic ones are TODO.

Next time we dig in, **start from a canonical PSX reference doc** rather
than reverse-engineering via probes.

---

## 5. Known false positives (diagnostic targets)

From session 28 cursor_walk output (Lloyd at (10,9), Dragoon Move=4 Jump=4
+ Movement +2, map MAP074 Siedge Weald):

**BFS said valid, game rejected:** `(10,6), (9,7), (10,7), (8,8), (9,8),
(6,9), (7,9), (8,9), (7,10), (8,10), (8,11), (9,11)`.

These are the concrete targets: step through each tile's path through
`MovementBfs.ComputeValidTiles` and find why BFS thinks they're reachable.
Probable culprits: slope direction, effective Jump computation, tree/obstacle
flags.

(Note: `cursor_walk` only reached 5 tiles, so the FP list above is only a
subset of BFS errors. Get a full ground-truth list by manual counting or
fixing the probe reliability.)

---

## 6. The (now-disabled) count-mismatch warning

`LogBfsTileCountMismatch` in `CommandWatcher.cs` reads `0x142FEA008` and
compares to BFS count. If they differ, it logs a loud warning AND populates
`DetectedScreen.BfsMismatchWarning`. The shell renders this as a `⚠` line
beneath the BattleMoving screen header.

**Currently disabled** (call sites commented out) because the byte doesn't
actually encode the game's tile count. When we find the real signal:

1. Update `LogBfsTileCountMismatch` to read the new address.
2. Uncomment the call sites in `PopulateBattleTileData` (search for
   `LogBfsTileCountMismatch(`).
3. Verify across multiple unit/map combos before trusting the warning.

The `MoveTileCountValidator` pure function, `BfsMismatchWarning` JSON field,
and shell rendering are all still in place and ready.

---

## 7. Suggested next-session workflow

When picking up BFS correctness work:

1. **Read this file** and `memory/project_move_bitmap_hunt_s28.md`.
2. Enter a random-encounter battle at a known map (MAP074 Siedge Weald is
   well-studied). Note unit positions and stats.
3. Run `scan_move` to get BFS output into `screen.Tiles`.
4. Ask the user or count blue tiles yourself (screenshot + visual count).
5. **Option A (fast):** web-search canonical PSX movement rules, port the
   slope/height/depth logic into `MovementBfs.cs`, add TDD tests against
   known-good fixtures, iterate until agreement.
6. **Option B (thorough but slow):** fix `cursor_walk` probe reliability
   (widen the transition-search range, try different probe signals) so it
   automatically produces the ground-truth list. Then run it on multiple
   unit/map combos to build a regression fixture set.
7. When agreement is achieved, find a real count signal (see §6) and
   re-enable the warning so future regressions are caught.

---

## 8. Files to know

- `ColorMod/GameBridge/MovementBfs.cs` — the BFS itself (pure functions).
- `ColorMod/GameBridge/MapLoader.cs` — per-map JSON loader, height/slope/depth.
- `ColorMod/GameBridge/BfsTileVerifier.cs` — pure comparator.
- `ColorMod/GameBridge/CursorFloodFill.cs` — pure flood-fill primitive.
- `ColorMod/GameBridge/ArrowKeyCalibration.cs` — arrow→map-delta translator.
- `ColorMod/GameBridge/MoveTileCountValidator.cs` — pure count-mismatch check
  (hooked up to a disabled address; kept in place).
- `ColorMod/Utilities/CommandWatcher.cs` → `RunCursorWalkDiagnostic`,
  `PopulateBattleTileData`, `LogBfsTileCountMismatch`.
- `FFTHandsFree/TODO.md` §0 — current BFS-related bugs/follow-ups.
- `memory/project_move_bitmap_hunt_s28.md` — full investigation log.
- `memory/feedback_move_mode_read_crashes.md` — don't spam memory reads in
  Move mode; use snapshot+diff.
- `memory/project_bfs_manual_verify_workflow.md` — manual tile-probe workflow.

---

*Last updated: 2026-04-17 (session 28).*
