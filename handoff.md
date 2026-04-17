# Session Handoff — 2026-04-17 (Session 29)

Delete this file after reading.

## TL;DR

**13 commits, +23 tests (2165 → 2188), 0 regressions.** BFS now matches the game's highlighted blue tiles exactly across 4 live-verified unit/position scenarios. The two root-cause discoveries: (a) `scan_move` was reading active-unit Move/Jump from `UIBuffer` which holds the cursor-hovered unit's BASE stats, not the active unit's effective stats — fixed by reading from the per-unit UE4 heap struct at `+0x22`/`+0x23`; (b) BFS tile costs had four subtle rule mismatches (averaged heights, ally-penalty, depth entry, swamp continuation) — all ported to canonical IC-remaster rules. Along the way we unified two divergent BFS implementations, shipped active-unit summary on `screen`, wired `BattleAttacking ui=`, added `dry-run` to `open_*` helpers, and strengthened the JobCursor liveness probe.

**Commits (oldest → newest):**

1. `925de71` — pt.1: TDD `ActiveUnitSummaryFormatter`
2. `5091b61` — pt.2: wire `activeUnitSummary` into `BattleMyTurn` + shell (and `heldCount` annotation)
3. `320ec7a` — pt.3: `TargetingLabelResolver` fixes `BattleAttacking ui=`
4. `abc2b3c` — pt.4: richer `MoveGrid` timeout diagnostics
5. `49fef3b` — pt.5: TDD `TileEdgeHeight` for canonical FFT slope rules
6. `cff6c99` — pt.6: integrate `TileEdgeHeight` into `MovementBfs` (+ 3-step JobCursor liveness)
7. `49fd756` — pt.7: TODO updates
8. `84530b5` — pt.8: unify `scan_move` BFS to shared `MovementBfs` function
9. `ad5acce` — pt.9: log root-cause finding for BFS tile-count discrepancies
10. `e4ba516` — pt.10: ally pass-through + depth-based MoveCost + swamp axis-continue
11. `3a57caa` — pt.11: read per-unit Move/Jump from heap struct, not UIBuffer
12. `8afd133` — pt.12: `dry-run` arg on `open_eqa` / `open_character_status` / `open_job_selection`
13. `8354f12` — pt.13: JobCursor bidirectional liveness + EqA re-fire on picker close

Tests: **2165 → 2188** (+23 new, 0 regressions).

## What landed, grouped by theme

### Active-unit summary on compact battle line (`925de71`, `5091b61`, `320ec7a`)

- **`ActiveUnitSummaryFormatter.Format(name, jobName, x, y, hp, maxHp) → "Wilham(Monk) (10,10) HP=477/477"`** (pt.1, 5 TDD cases). Handles null-name/null-job/no-HP paths.
- **Cache active unit identity in `CacheLearnedAbilities`** (pt.2). Reset on turn transitions. `DetectScreen` emits `ActiveUnitSummary` on any `Battle*` state.
- **Shell compact renderer widened from `Battle_*` to `Battle*`** so `BattleMyTurn` / `BattleMoving` / `BattleAttacking` all get the active-unit banner.
- **`TargetingLabelResolver.Resolve(lastAbilityName, selectedAbility, selectedItem)`** (pt.3). Precedence chain lets `BattleMenuTracker.SelectedAbility/SelectedItem` fill in `ui=<ability>` when `_lastAbilityName` is null (manual-nav entry path via `execute_action Select`). Live-verified: `[BattleAttack] ui=Attack`.
- **Shell `heldCount` annotation on Items abilities** (bundled in pt.2). `fmtAb` in `fft.sh:~3122` prefixes names with `[xN]` or `[OUT]`. Not live-verified yet.

### Canonical BFS tile cost rules (`49fef3b`, `cff6c99`, `84530b5`, `e4ba516`, `3a57caa`)

This was the headline work. BFS now agrees with the game across every tested scenario. The rule set:

- **Edge-height instead of averaged display heights** (pt.5/6). `TileEdgeHeight.Edge(tile, direction)` returns the exact height at the edge of a tile in a given cardinal direction. Flat tiles = uniform `Height`. Incline splits into low-edge (`Height`) and high-edge (`Height + SlopeHeight`). Convex/Concave raise two adjacent corner edges. BFS steps compare exit-edge of A vs entry-edge of B.
- **Unified BFS implementation** (pt.8). `scan_move` used to inline its own BFS that averaged heights and treated all occupied tiles as blocking. Deleted; now both `scan_move` and the Move-mode `PopulateBattleTileData` delegate to `MovementBfs.ComputeValidTiles`.
- **Ally pass-through** = normal tile cost (pt.10). Not +1 penalty (earlier rule blocked Wilham's paths through Lloyd), not 0 (tested, over-extended reach). Allies can't be final destinations.
- **Depth-based MoveCost** (pt.10). Any tile with `depth > 0` costs `1+depth` (not hardcoded-2 for Swamp). IC map data stores depth=1 for Swamp tiles and the game uses the same rule uniformly. Marsh / Poisoned marsh / Lava kept at flat 2 until we see a counter-example.
- **Swamp axis-continue** (pt.10). Once inside a depth tile, continuing along the SAME cardinal direction costs 1 (splash paid once per straight-line wade). Turning pays full 1+depth again. Implementation: each BFS queue entry carries a `lastDir`, and swamp→swamp only discounts when `lastDir == stepDir`.
- **Per-unit effective Move/Jump from heap struct** (pt.11). `TryReadMoveJumpFromHeap(hp, maxHp)` searches the UE4 heap (range `0x4000000000..0x4200000000`) for the unit's HP+MaxHP u16 pair, computes struct base = match_addr − `0x10`, reads Move at `+0x22` (u8), Jump at `+0x23` (u8). Sanity filter: Move in [1,10], Jump in [1,8]. Falls back to UIBuffer if heap search fails. Diagnostic infra shipped alongside: `dump_unit_struct` bridge action dumps 256 bytes of any unit's struct for future offset hunts.

**Live-verified matches (no manual stat override required):**

| Unit | Position | Stats | BFS tiles | Game tiles |
|---|---|---|---|---|
| Kenrick (Knight) | (9,9) | Mv=3 Jmp=3 | 11 | 11 ✓ |
| Kenrick (Knight) | (8,5) | Mv=3 Jmp=3 | 15 | 15 ✓ |
| Kenrick (Knight) | (8,2) | Mv=3 Jmp=3 | 19 | 19 ✓ |
| Wilham (Monk)  | (10,11) | Mv=3 Jmp=4 | 5 | 5 ✓ |

### Navigation + resolver hardening (`abc2b3c`, `8afd133`, `8354f12`)

- **MoveGrid timeout diagnostics** (pt.4). Captures `lastScreenSeen` + poll count in the error string so the next false-negative repro has enough context to root-cause.
- **`dry-run` arg on open_* helpers** (pt.12). `open_eqa Ramza dry-run` / `open_character_status Agrias dry-run` / `open_job_selection Cloud --dry-run` route to the `dry_run_nav` bridge action (shipped session 27) instead of firing. Prints the planned key sequence. Unblocks the crashy chain-nav debugging scenario.
- **JobCursor bidirectional liveness** (pt.13). Extends the 3-Rights-expect-+3 probe with a phase-2 check: 3 Lefts must return the byte to its pre-probe value. Change-count widgets reach +6 total instead of returning to baseline and now fail phase 2. Current save still 0 candidates (expected — no live byte exists for this save).
- **EqA row resolver re-fire on picker close** (pt.13). `_lastEqaMenuDepth` tracks the previous DetectScreen's MenuDepth on EqA; when it transitions >2 → 2 (picker→EqA main), the resolver re-fires. ~2s cost per picker close, prevents stale `ui=Right Hand (none)` after equipment edits.

## Technique discoveries worth propagating

### "Dump the heap struct with a diagnostic before committing rules"

The root cause of repeated BFS discrepancies was the same bug masked by `scan_move` manual overrides: `UIBuffer at 0x1407AC7C0 +0x24/+0x26` reads the cursor-hovered unit's base stats, not the active unit's effective stats. This was documented in two memory notes already (`project_memory_scan_results.md`, `project_battle_bfs_verified.md`) but the fix was never landed until live data forced the issue (Kenrick returning 16 tiles at Mv=4 when his true stats are Mv=3). **Technique:** when the data source is suspect, ship a `dump_unit_struct`-style bridge action that prints the raw bytes, correlate known values (Kenrick=3, Lloyd=5, Archer=3) against byte offsets, THEN wire the new offset. Saved us from more guessing.

### "Live-verify every tile-cost rule change, one unit/position at a time"

Swamp rules proved subtle. `Depth = 1+depth` alone was right for perpendicular swamp entry, but over-counted when chaining straight through swamp. A straight-line axis-continue discount fit Kenrick (8,5) reaching (8,7)/(6,5) via straight chains but explicitly rejecting (7,6) which requires a south→west turn in swamp. One extra data point distinguished "swamp continue any direction" (wrong) from "swamp continue along same axis" (right). **Technique:** always test at least 2 scenarios that differ in one variable each; a single passing scenario doesn't lock in the rule.

### "Unify divergent implementations BEFORE you fix the bug"

Wilham BFS returned 4 tiles when the game showed 7. Initial hypothesis was another tile-cost bug, but grep-ing for `ComputeValidTiles` callsites found **two** BFS implementations — `scan_move` inlined its own (averaged heights, occupied-blocks-all) while `PopulateBattleTileData` used the shared pure function. Unifying (pt.8) auto-fixed 2 of 3 missing Wilham tiles before we even touched the rule set. **Technique:** when you hit inconsistent behaviour across related commands, grep for duplicated implementations first.

### "Shell variable scope: subshell doesn't propagate back"

First pass of the `dry-run` arg parsing used a helper function that echoed the unit name and set `__OPEN_DRY_RUN=1`. But bash `local unit=$(_parse "$@")` runs the helper in a subshell, so `__OPEN_DRY_RUN` never escaped. Had to inline the parsing into each `open_*` function. **Technique:** bash output-capture always forks a subshell; side-effects in that subshell are invisible to the caller. For anything that needs both a return value AND a flag, either inline the parsing or use a temp file / global with explicit marker.

### "Route dry-run through the same code path that asks for info"

The shell `fft` wrapper renders `screen` and doesn't emit `info`. When we route `open_eqa ... dry-run` to the `dry_run_nav` action (which writes the plan into `response.info`), the user sees `[EquipmentAndAbilities] ui=...` but NOT the plan. Fix: the dry-run branch in the shell helper also greps `response.json` for `info` and echoes it. **Technique:** when shipping a diagnostic command, verify what the shell actually displays — `completed` status alone doesn't prove the user saw anything useful.

## What's NOT done — top priorities for next session

### 1. `battle_move` NOT CONFIRMED false-negative — awaits next repro

Session 29 pt.4 added diagnostic logging. The next repro error message will include `lastScreenSeen=X polls=N`. Read that log. If `lastScreenSeen=BattleMoving` across all 80 polls, the fix is in screen detection — `battleMode` byte likely stays 2 past the walk animation because UE4 keeps painting Move UI briefly. If intermediate states appear, expand the accept-list in `NavigationActions.cs MoveGrid`. Repro is reliable on turn 1 after `auto_place_units` settles.

### 2. ⚠ UNVERIFIED `activeUnitSummary` sweep across non-MyTurn battle states

Shipped session 29 pt.1/2. Confirmed live on `BattleMyTurn` and `BattleAttacking`. Still needs a quick sweep on `BattleMoving` / `BattleCasting` / `BattleAbilities` / `BattleActing` / `BattleWaiting` / `Battle_*` sub-screens. The renderer branch is `Battle*` so all should work, but until it's verified the ⚠ UNVERIFIED flag should stay. One battle turn through each state is enough.

### 3. ⚠ UNVERIFIED `heldCount` rendering on Items abilities

Session 29 pt.2 shell-only change. Tests pass, shell compiles. Needs a unit with Items secondary in a battle (Ramza at Siedge Weald has it). `scan_move` should now show `Potion [x4]` / `Ether [OUT]` inline.

### 4. Lloyd `(10,4)` false-positive at (10,9) Mv=5 Jmp=4

Session 29 tile-cost rules match the game for 4 tested combos. Untested: Lloyd's prior FP scenario. Re-run at Siedge Weald with the new heap-read active Move/Jump and see if (10,4) is still over-reported. If still wrong, read `memory/project_bfs_tile_cost_rules.md` for what we've tried.

### 5. `cursor_walk` probe reliability — 5 of 20

Still the main blocker for automated BFS regression fixtures. Fix ideas: widen `0x140DDF000..0x140DE8000` to also cover `0x140F9xxxx` mirror; count `00→05` and `01→05` transitions in addition to `04→05`; baseline "cursor on known valid tile" and compare the SET of `0x05` bytes rather than transition counts.

### 6. Find a real count signal to re-enable the BFS mismatch warning

`MoveTileCountValidator` + `DetectedScreen.BfsMismatchWarning` + shell `⚠` rendering all still in place. Get user visual counts for 3-5 combos, snapshot module memory before/after Move mode entry, scan diff for any byte/u16 whose post-entry value matches the real count on ALL combos.

### 7. Smaller follow-ups

- JobCursor resolver bidirectional probe needs a save with a truly-live byte to validate it.
- `execute_action` response compact renderer shares `_fmt_screen_compact` so activeUnitSummary should already appear; quick live-verify to close the loop.
- Carryovers still open: NameTableLookup "Reis" collision, chain-nav viewedUnit lag, Chronicle/Options tab discriminator.

## Things that DIDN'T work (don't-repeat list)

1. **Ally pass-through at 0 cost.** Initially implemented as `tileCost = 0` for allies (seemed intuitive after the +1 penalty broke Wilham). Over-extended Kenrick's reach — BFS admitted tiles 2+ steps past an ally that the game rejected. Reverted to normal tile cost.

2. **Swamp discount for ANY swamp→swamp step.** First attempt at the depth-continue rule allowed any swamp→swamp step to cost 1. Admitted (7,6) from (8,5) via a south-then-west turn — game rejects. Narrowed to same-axis only.

3. **Trusting my initial user-input parse.** The user listed 13 tiles and called them 12; I narrowed the diff to 3 candidates and one turned out to be a miscount. Double-check counts against screenshots before shipping a rule change.

4. **Subshell-assigned flags in bash helper functions.** `local x=$(helper "$@")` inside `helper` sets `FLAG=1` — FLAG is invisible to caller. Inline-parsed instead.

5. **UIBuffer as source of active-unit stats.** Documented-as-wrong in two session-26 memory notes, but the fix wasn't wired until session 29 when BFS tile-count miscounts finally forced it.

## Things that DID work (repeat-this list)

1. **Live-verify every tile-cost change immediately.** Kenrick (8,5) surfaced the axis-continue rule by rejecting exactly the one tile a "swamp→swamp any direction" rule would admit.

2. **Ship diagnostic bridge actions for memory hunts.** `dump_unit_struct` took 10 minutes to write and immediately found Move/Jump offsets by correlating known values against byte positions. New pattern for future per-unit data hunts.

3. **Match exactly 4 of 4 scenarios before calling a rule set "done".** Each new scenario added evidence and caught edge cases (ally traversal, swamp axis-turn, swamp entry from non-swamp, depth continuation).

4. **Delete rather than preserve stale TODOs.** Moved 5 `[x]` items from §0 to the archive, cross-referenced session-29 commits, and scrubbed the "already shipped in an earlier session" stragglers. §0 is now actionable-only.

5. **Pure function + TDD cycle for every new rule.** `TileEdgeHeight`, `TargetingLabelResolver`, `ActiveUnitSummaryFormatter` all locked in behaviour with tests before wiring into live code. Zero regressions across the +23 test adds.

6. **Move verification to the wider set before committing to the narrower rule.** The "3-check agreement" rule saved us: Kenrick (9,9), (8,5), (8,2), Wilham (10,11) each independently validated the final tile-cost rules.

## Memory notes saved this session

New entries:

- `project_heap_unit_struct_movejump.md` — Per-unit effective Move/Jump live at heap struct `+0x22` (Move, u8) and `+0x23` (Jump, u8). Struct base = HP-pattern-match-addr − `0x10`. Verified for Kenrick/Lloyd/Archer. Supersedes UIBuffer for active-unit stats.
- `project_bfs_tile_cost_rules.md` — The 4 canonical BFS tile-cost rules that make `MovementBfs.ComputeValidTiles` match the game exactly on MAP074 Siedge Weald. Includes regression fixture data and "what we tried that didn't work".
- `feedback_ui_buffer_stale_cursor.md` — UIBuffer at `0x1407AC7C0` holds the CURSOR-HOVERED unit's BASE stats, never the active unit's effective stats. Don't read active-unit stats from here.

All 3 indexed in `MEMORY.md` with 🎯 marker on the two project notes.

## Quick-start commands for next session

```bash
# Baseline
./RunTests.sh                              # 2188 passing
source ./fft.sh
running                                    # check game alive

# Read the new memory notes before any BFS / Move/Jump work:
cat ~/.claude/projects/c--Users-ptyRa-Dev-FFTColorCustomizer/memory/project_heap_unit_struct_movejump.md
cat ~/.claude/projects/c--Users-ptyRa-Dev-FFTColorCustomizer/memory/project_bfs_tile_cost_rules.md

# Sanity-check the BFS fix:
#   Enter a random encounter at Siedge Weald (location 26) as Kenrick.
#   scan_move — should report correct Mv/Jmp from heap, not UIBuffer.
#   Tile counts should match the game blue tiles.
# (scan_move is aliased to `screen`; use `_old_scan_move` for the
# structured payload including validPaths.ValidMoveTiles.tiles[].)

# Dry-run a nav without firing:
open_eqa Agrias dry-run                    # prints the planned key sequence

# Dump any unit's heap struct for offset hunts:
fft '{"id":"x","action":"dump_unit_struct","pattern":"4A024A02"}'
#                                                  HP+MaxHP u16 LE pattern
cat "$B/dump_unit_struct.txt"              # 256 bytes per match
```

## Top-of-queue TODO items the next session should tackle first

These live in `TODO.md §0`:

1. **`battle_move` NOT CONFIRMED diagnostic repro** — the logging is ready; the next time it fires the error includes enough to pick the fix direction.
2. **⚠ UNVERIFIED `activeUnitSummary` across non-MyTurn battle states** — single-battle sweep.
3. **⚠ UNVERIFIED `heldCount` rendering** — single scan_move in a battle with Items secondary.
4. **Lloyd `(10,4)` false-positive** — verify the new rule set against Lloyd's prior FP scenario.
5. **`cursor_walk` probe reliability** — unblocks automated regression-fixture generation.

Plus carryovers from §0 earlier sessions (NameTableLookup "Reis", chain-nav viewedUnit lag, BattleSequence detection, Chronicle/Options discriminator, JP Next live-verify).

## Insights / lessons captured

- **The cheapest diagnostic is often a bridge action that dumps raw bytes.** `dump_unit_struct` took 10 minutes and immediately solved a year-old documented problem. Write the diagnostic before the fix.

- **Four live-verified scenarios beat twenty unit tests for rule-correctness confidence.** Unit tests lock in specific behaviour; live scenarios exercise the full data pipeline including reads, caching, rendering. Both matter; tests come first, then burn-in against live data.

- **Stale TODOs cost navigation cycles.** Multiple "already done" items sat in §0 because nobody cross-referenced them against the code during a handoff. Part of hygiene: grep for the feature name before picking up a task; if it's already implemented, archive the TODO.

- **Dead-end memory notes are force multipliers.** Session 28's `project_move_bitmap_hunt_s28.md` said "there is no valid-tile bitmap, stop looking." Session 29 respected that and went a totally different direction (fix the BFS algorithm directly) — got the win. Without the note we'd have scanned memory again.

- **The right rule isn't always the one that fits all scenarios — it's the one that fits all scenarios AND rejects the counter-examples.** Swamp any-direction continue fit 3 scenarios. Only the "same axis only" variant fit ALL of them. When a rule "almost works," look harder for the one tile it gets wrong.

- **Shell-side and mod-side parity matters for user-facing commands.** The `activeUnitSummary` shipped as a compact-line field but was initially only rendered by one of two code paths (`_fmt_screen_compact` vs `_fmt_execute_response`). Audit the rendering pipeline when adding a response field, not just the C# model.

- **Bash subshells silently eat side effects.** This'll bite someone again. Note it in the handoff so future-you grep the right keyword.

- **IC remaster ≠ PSX.** Swamp MoveCost, Move/Jump effective-vs-base, and ally traversal all differ from canonical PSX rules. Trust live data over wiki-sourced formulas (already a memory note: `feedback_wiki_psx_vs_ic.md`).
