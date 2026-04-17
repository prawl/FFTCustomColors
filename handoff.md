# Session Handoff — 2026-04-17 (Session 28)

Delete this file after reading.

## TL;DR

**7 commits + 1 handoff (pt.8), +28 tests (2137 → 2165), all on branch `auto-play-with-claude`.** Session split in half: first half was a battle-task verification pass that shipped clean wins (BattleMoving ui=, auto_place_units verified, SkillsetItemLookup verified); second half was a multi-hour deep dive into the "read valid-move tiles from game memory" problem that ended in a **conclusive negative result** — the game does not store a per-unit bitmap anywhere reachable via memory-diff or AoB search. That dead end is now fully documented so future sessions skip the re-investigation.

A count-mismatch warning (`BFS OVERCOUNT`) was shipped, then **disabled** mid-session when live-testing revealed the byte we were reading doesn't actually encode the game's tile count (byte=11 while user visually counted 20 blue tiles). Experimental `cursor_walk` diagnostic action + pure-function scaffolding stayed in the tree for future iteration. Methodology doc created to anchor next session's work.

**Commits (oldest → newest):**

1. `9b9a627` — Session 28 pt.1: battle-task findings in TODO, drop session 27 handoff
2. `3d7ba31` — Session 28 pt.2: surface cursor (x,y) as ui= on BattleMoving
3. `f074719` — Session 28 pt.3: consolidate move-tile memory-read investigation in TODO
4. `b24f8b4` — Session 28 pt.4: warn loudly when BFS count != game's own tileCount
5. `c0dcda3` — Session 28 pt.5: surface BFS mismatch in screen response + dump debug context
6. `258b69a` — Session 28 pt.6: TDD pure functions for upcoming BFS-verify diagnostic
7. `3f41b00` — Session 28 pt.7: disable BFS mismatch warning; add experimental cursor_walk diagnostic

Tests: **2137 → 2165** (+28 new, 0 regressions).

## What landed, grouped by theme

### Battle-task verification pass (`9b9a627`, `3d7ba31`)

Went into a Siedge Weald random encounter with Ramza/Kenrick/Lloyd/Wilham (all Move=4 Jump=4) and exercised the bridge actions that had been shipped but not live-verified:

- **`auto_place_units` VERIFIED** — from `EncounterDialog → Fight` → BattleFormation, helper placed all 4 units and commenced the battle in a single call. One new memory note `feedback_auto_place_settle.md` documents that the helper returns BattleMyTurn *before* enemy-first turns actually play out; wait ~30s before scanning.
- **`SkillsetItemLookup` VERIFIED** — Ramza's Items secondary, `response.json` had `heldCount` on every Items ability (Potion=4, Hi-Potion=1, X-Potion=94, Ether=99, Antidote=97, Echo Herbs=99, Gold Needle=99, Holy Water=99). Remaining gap: shell compact renderer doesn't display `heldCount`; follow-up TODO in `§1`.
- **BattleMoving `ui=(x,y)` shipped** — `PopulateBattleTileData` now writes `screen.UI = FormatCursor(CursorX, CursorY)`. Previously the compact line was `[BattleMoving] curLoc=...` with no cursor info. Pure `BattleCursorFormatter` extracted + 3 TDD tests.
- **3 new bugs logged in §0** — the `battle_move` NOT-CONFIRMED flake (game moved the unit, state machine missed the confirm signal); missing `ui=` on `BattleAttacking` (requires `_lastAbilityName` which only gets set via `battle_ability` not manual `Select`); and the "`BattleMyTurn` compact line should include the active unit's name" request.

### Move-tile memory hunt concluded (`f074719`, `b24f8b4`, `c0dcda3`, `258b69a`, `3f41b00`)

The headline deep-dive. Context: BFS in `MovementBfs.cs` has known correctness gaps and we wanted to fix them by extracting the game's own tile list from memory. This turned out to be a dead end, exhaustively.

**What we tried:**

- Scanned `0x140C66315` (7 bytes/entry tile table) — it's the movement-zone PERIMETER in world coords, not per-tile validity. Coords go up to X=29 on an 11×12 map.
- Scanned `0x140C6F400` render struct array (stride 0x88, flag at +0x1D) — volatile per-frame, counts swing 13-19 across reads.
- Heap-diffed `0x142FEA000..FFF` on Move-mode entry — 2024 bytes changed, structured as 14 "blocks" of 46 bytes each using the alphabet `{0x4, 0x8, 0xC}` per nibble. **Identical for Kenrick vs Lloyd on the same map** — so this is static map-adjacency data, not per-unit valid tiles.
- Tried to locate a vertex buffer via UE4 mesh triangle indices in the `0x140DE2xxx` render slot array — found the indices but the vertex positions are elsewhere in unknown format.
- Tried to build an (x,y)→slot calibration table by walking the cursor — **slot addresses dynamically recycle per cursor move (ring buffer / LRU)**. There's no stable mapping.
- `0x142FEA008` byte that toggles on Move-mode entry briefly looked like "the count" — but live-verified FALSE: reads `0x0B` (11) for Lloyd while user counts 20 visible blue tiles.

**What we shipped anyway:**

- `MoveTileCountValidator` pure function + 6 tests — compares BFS count vs memory count, returns a formatted warning string with a reference to `BFS_METHODOLOGY.md`.
- `DetectedScreen.BfsMismatchWarning` JSON field + shell-side `⚠` rendering in `fft.sh` — warning surfaces in the compact `screen` response without needing `logs grep`.
- Rich `[BFS DEBUG]` log dump on mismatch: activeUnit name, position, Move/Jump, movement ability, map number, blockedTiles, full BFS tile list.
- `BfsTileVerifier.Compare(bfsList, gameList)` pure function → `(agreements, falsePositives, falseNegatives)` + 6 tests.
- `CursorFloodFill.Flood(startX, startY, isValid)` pure BFS flood-fill primitive + 8 tests.
- `ArrowKeyCalibration` — screen-space arrow → map-space delta translator for the current camera rotation + 6 tests.
- `MemoryExplorer.CountTransitionsInRange` — lightweight in-memory diff counter for specific `oldVal→newVal` byte transitions in a range.
- `cursor_walk` bridge action (`CommandWatcher.RunCursorWalkDiagnostic`) + shell helper — calibrates arrows (auto-recalibrates when edge-blocked), flood-fills from unit start via cursor probe + snapshot-diff, compares to `screen.Tiles` via `BfsTileVerifier`.

**What we disabled:**

- `LogBfsTileCountMismatch` call sites in `CommandWatcher.PopulateBattleTileData` are commented out because the count byte turned out to be wrong. Everything else (validator, JSON field, shell rendering) stays in place waiting for a real count signal.

### Methodology documentation (this commit)

- New `FFTHandsFree/BFS_METHODOLOGY.md` — the canonical "read-me-first-when-BFS-is-wrong" doc. Explains: why BFS is the primary path; why memory extraction is a dead end (with specific addresses ruled out); the `cursor_walk` diagnostic; the PSX reference algorithm we should port; session 28 diagnostic targets; file index.
- The mismatch warning message now points to this doc.

## Technique discoveries worth propagating

### "Looks like the answer, isn't the answer" — verify with out-of-band ground truth

`0x142FEA008` toggled on Move-mode entry/exit with values that happened to correlate with Move stat + jump + something. I built 3 commits (pt.4, pt.5) assuming it was the tile count. The user visually counting blue tiles broke the assumption immediately. **When a memory signal looks promising, ALWAYS cross-check against a ground truth you didn't derive from memory** (screenshot, user count, game-visible UI). Especially before wiring it into a warning that will fire in normal play.

### Rapid memory reads during Move mode crash the game

Hit this twice. Reading `0x142FEA000+` or `0x140C6F000+` in quick succession (even with 1-2 second sleeps between) crashed FFT. `snapshot` + `diff` is safe (atomic capture). Live reads of regions that are being written by the game's Move-mode state machine are not. Memory note `feedback_move_mode_read_crashes.md`.

### Snapshot+diff is the right tool for "what changes when I do X?"

Every useful memory discovery this session came from `snapshot → action → snapshot → diff` (often with a clever before-state to isolate the signal). Avoid repeated `read_bytes` loops; they're slow, crash-prone, and noisier than a single diff.

### Slot pools vs stable addresses

UE4 uses dynamic object pools for per-frame state. Slots in `0x140DE2xxx` get recycled per cursor move — the address for tile (5,5) at time T isn't the same at time T+1. This pattern broke our calibration plan. When you see **"the address changed between reads with no state change"**, suspect a pool and switch to content-based identification.

### Verifying arrow-key directions per rotation needs retry logic

First calibration attempt pressed each arrow + tried to restore. When the unit was at a map edge, one direction was blocked (move didn't happen), and the restore assumed a working inverse. Second calibration (after drift moved us off the edge) completed all 4 directions. The `ArrowKeyCalibration.FromObservations` + `BuildPath` design handles this cleanly, but the CALLER (CommandWatcher) needs to re-observe `(0,0)` keys after drift. Pattern reusable for any camera-rotation-dependent navigation.

## What's NOT done — top priorities for next session

### 1. Fix MovementBfs — the 12 known false positives

Concrete ground-truth data from cursor_walk (partial, 5-tile reach): BFS says these tiles are valid for Lloyd at Siedge Weald but the game rejects them: `(10,6), (9,7), (10,7), (8,8), (9,8), (6,9), (7,9), (8,9), (7,10), (8,10), (8,11), (9,11)`. These are the starting point. Read `FFTHandsFree/BFS_METHODOLOGY.md` §4 (canonical PSX rules). Web-search the canonical algorithm from a reliable source (GameFAQs/FFHacktics), port slope/height/depth logic into `ColorMod/GameBridge/MovementBfs.cs`, TDD tests against the known false positives.

### 2. Find a real count signal (or decide we don't need one)

Options: (a) user-confirmed visual counts across 3-5 unit/map combos + snapshot-diff the full module memory to find a byte whose value matches across all combos; (b) accept that we don't have a runtime count signal and rely on BFS correctness instead. The latter is probably the right answer given how much time went into the hunt.

### 3. `cursor_walk` probe reliability — catches 5 of 20 tiles currently

The transition-count probe in `RunCursorWalkDiagnostic` only finds ~5 of 20 valid tiles. Fix ideas in `§0 Urgent Bugs`: widen the slot-region to include the mirror at `0x140F9xxxx`; count additional transitions (`00→05`, `01→05`); use set-difference instead of transition-count. Would unblock automated ground-truth extraction for BFS fixture-building.

### 4. Carryover from Session 27 (still open)

- `NameTableLookup` returns "Reis" for new-recruit Crestian — blocks JP-Next live-verify.
- JobCursor resolver: no byte passes liveness on current save.
- EqA row resolver: re-fire on detect-drift events.

### 5. The 3 battle-task bugs logged in §0

- `battle_move` NOT CONFIRMED false-negative (game moved the unit, SM missed it).
- `BattleAttacking` compact line missing `ui=`.
- `BattleMyTurn` compact line should include active unit name/job/HP.

## Things that DIDN'T work (don't-repeat list)

1. **Trusting `0x142FEA008` as the game's tile count without visual verification.** Ate 3 commits worth of work on a wrong anchor. The byte really does toggle on Move-mode entry, and it really does correlate with something — but NOT with valid-tile count.

2. **Expecting the game to store a persistent per-unit bitmap.** Session 28 conclusively ruled this out across 4 KB+ of careful scanning. The game recomputes tile validity per frame.

3. **Looking for a direct `(x,y)→slot address` calibration in the `0x140DE2xxx` render slot array.** Slot addresses recycle via an LRU pool; there's no stable mapping.

4. **Using `battleMode` at `0x140900650` as a cursor-on-valid-tile probe.** Reads `1` for *any* non-unit tile — valid or invalid. Not granular.

5. **Chained `read_bytes` calls during Move mode.** Crashed the game twice.

6. **Running `cursor_walk` with a naïve calibration that doesn't handle edge-blocked arrows.** First run fails, drifts the cursor off the unit's tile, then every subsequent probe uses wrong origin and reports bogus results.

## Things that DID work (repeat-this list)

1. **User + visual ground truth.** "How many blue tiles do you see?" broke the 3-commit wrong-anchor chain in 30 seconds.

2. **Snapshot + diff for state-transition analysis.** Every useful discovery this session came from `snapshot → action → snapshot → diff`. Almost never from raw `read_bytes` loops.

3. **Extract pure functions with TDD, wire live separately.** `BattleCursorFormatter`, `MoveTileCountValidator`, `BfsTileVerifier`, `CursorFloodFill`, `ArrowKeyCalibration` — each extracted and tested before being wired into `CommandWatcher`. Keeps the test suite fast (<10s for 2165 tests) and makes wiring errors obvious.

4. **Commit experimental infra even when the feature isn't final.** The pure functions + cursor_walk action are in the tree even though cursor_walk's probe is unreliable. Future sessions get the scaffolding for free.

5. **Loud warnings + rich debug dumps on mismatch.** `[BFS DEBUG]` log block captures active unit name, position, stats, map, BFS tile list, blockedTiles — so diagnosing a mismatch is one screen-read, not a round-trip per field.

6. **Document dead ends proudly.** The memory note `project_move_bitmap_hunt_s28.md` is 200+ lines of "things we tried that didn't work and why." Next session reads it and skips hours of re-investigation.

7. **Methodology docs for recurring problems.** `BFS_METHODOLOGY.md` captures the full context for the BFS debugging playbook. Referenced from the warning message so context travels with the signal.

## Memory notes saved this session

New entries:

- `project_move_bitmap_hunt_s28.md` — Comprehensive account of session 28's move-tile memory hunt. Ruled-out addresses and signals, UE4 mesh structure, slot pool behavior, final conclusion that no persistent bitmap exists, and the late-session correction about `0x142FEA008` being wrong.
- `feedback_auto_place_settle.md` — After `auto_place_units` returns BattleMyTurn, wait ~30s for enemy-first settle before scanning. The helper doesn't actually hand control to the player in the "BattleMyTurn" it returns.
- `feedback_move_mode_read_crashes.md` — Rapid successive reads of `0x142FEA000+` or `0x140C6F000+` during Move mode crash the game. Limit to 1 `read_bytes` per BattleMoving session or use snapshot+diff.
- `project_bfs_manual_verify_workflow.md` — Per-map manual tile-probe workflow. Cursor + slot `+0x06` flag reads per tile. Arrow-key direction mapping varies with camera rotation.

All indexed in `MEMORY.md`.

## Quick-start commands for next session

```bash
# Baseline
./RunTests.sh                              # 2165 passing
source ./fft.sh
running                                    # check game alive

# Read the BFS methodology doc before touching MovementBfs:
cat FFTHandsFree/BFS_METHODOLOGY.md

# Read the move-tile hunt postmortem (seriously, read it first — saves hours):
cat ~/.claude/projects/c--Users-ptyRa-Dev-FFTColorCustomizer/memory/project_move_bitmap_hunt_s28.md

# Check open §0 bugs
grep -nE "Session 28" FFTHandsFree/TODO.md | head -10

# Session 28 smoke tests:
open_eqa Ramza                             # chain-nav still works (session 27 fix)
screen                                     # BattleMoving should show ui=(x,y)
cursor_walk                                # diagnostic; experimental but doesn't crash
```

## Top-of-queue TODO items the next session should tackle first

These live in `TODO.md §0`:

1. **Fix MovementBfs — 12 known false positives for Lloyd at Siedge Weald** — the concrete diagnostic targets. Port canonical PSX rules rather than reverse-engineer.
2. **Find a real count signal (or decide we don't need one)** — may be a sunk-cost trap; consider dropping.
3. **`cursor_walk` probe reliability** — unblocks automated ground-truth extraction.
4. **`battle_move` NOT CONFIRMED false-negative** — shell says fail, game actually moved.
5. **`BattleMyTurn` compact line should include active unit name** — decision-surface gap.

Plus carryovers from §0 Session 27 (NameTableLookup, JobCursor, EqA row resolver).

## Insights / lessons captured

- **Memory hunts are expensive and lossy.** Session 28 burned most of 4 hours on a 0-tile-delta improvement over BFS. The `0x142FEA008` false anchor cost 3 commits. If a memory signal isn't visibly confirmed in 30 minutes, the return probably isn't there.

- **"Not stored" is a valid answer.** The game doesn't store a persistent valid-tile bitmap. That's not a failure of our tools — it's a fact about how UE4 + this game's render pipeline work. Document and move on.

- **Verification plans should be written before the implementation.** Each of the shipped-then-disabled commits (pt.4, pt.5) would have been caught by asking "how will I test this?" before writing it. "I'll assume the byte is correct" is not a verification plan.

- **Visual ground truth beats memory signals when they disagree.** If our memory signal says 11 and the screen shows 20, the screen is right.

- **Dead-end memory notes are load-bearing infrastructure.** `project_move_bitmap_hunt_s28.md` is the single most valuable artifact of this session. Future sessions pick it up and skip hours of re-investigation. Invest in these notes.

- **Pure-function TDD scales.** 2137 → 2165 tests added in <4 hours of dev time across 3-4 new pure functions. Each locks behaviour before live wiring.

- **The "3-check agreement" rule.** When debugging a memory signal: get 3 independent verifications (live read, snapshot diff, visual count) before shipping it as a feature. Session 28 shipped a feature after 2 checks, both of which came from the same source.
