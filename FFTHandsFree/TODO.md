<!-- This file is exempt from the 200 line limit. -->
# FFT Hands-Free — Battle Automation (V1 push)

> **V1 scope (2026-04-22):** this TODO tracks only battle-related work. Everything non-battle (shops, taverns, party menu, world travel, cutscenes, mod separation, etc.) moved to [DEFERRED_TODO.md](DEFERRED_TODO.md). Goal: Claude fully automated in battle.

## Project Goal

Make FFT fully hands-free by giving Claude Code a platform to play the game as if it were a human player. Claude sends commands to the game through a file-based bridge, reads game state from memory, and makes intelligent decisions during gameplay.

**Core principles:**
- **Speed** — Claude's interactions with the game should be as fast as possible. Every round-trip matters. Batch operations, embed state in responses, minimize tool calls.
- **Intelligence** — Claude should make smart tactical decisions in battle, manage party builds, navigate the world map, and plan ahead like an experienced player.
- **Engagement** — This should be fun to watch. Claude experiences the story as a new player — reading dialogue, reacting to plot twists, commenting on characters, sharing facts and observations as it learns. It should feel like watching a friend play for the first time.
- **Autonomy** — Claude should be able to play extended sessions with minimal human intervention. Scan the battlefield, pick a strategy, execute moves, handle unexpected situations, and recover from mistakes.

The ultimate vision: you say "play FFT" and Claude boots the game, loads a save, navigates the world, enters battles, makes tactical decisions, enjoys the story, and keeps you entertained along the way.


## Design Principle: Automate the Interface, Not the Intelligence

Give Claude the same tools a human player has, just digitized. The bridge should make it easy to *see* and *act* — but never make decisions for Claude.

**What a human player can do (Claude should too):**
- See the whole battlefield at a glance → `screen` (unified battle state)
- Move a cursor to any tile → `move_grid x y`
- Read the menu options → `validPaths`
- Check a unit's stats by hovering → static battle array reads
- Press buttons quickly and accurately → key commands with settling

**What a human player does NOT have (neither should Claude):**
- A computer telling them the optimal move
- Auto-pathfinding around obstacles
- Pre-calculated damage numbers
- Filtered "only valid" tile lists that remove bad options
- Auto-targeting the weakest enemy

**The rule:** If it removes a decision from Claude, it's too much automation. If it makes Claude fumble with the controller instead of thinking about strategy, it's not enough.

---


## Design Principle: What Goes In Compact vs Verbose vs Nowhere

Every field on the `screen` response has to earn its spot by changing a decision Claude actually makes. Three tests:

1. **Would a human consult this on this screen?** If yes → strong candidate. If no → don't surface.
2. **Does Claude need it to act HERE, or could they navigate to it?** Need it here → surface. Could navigate → don't pre-populate.
3. **Would not having it cause a worse decision OR wasted round-trips?** Yes → surface. No → drop.

**Plus a noise penalty.** Claude greps past dense responses — every field in the compact one-liner makes other fields harder to find. There's a budget. Anything that doesn't strongly pass the three tests pays rent against that budget.

**Prefer decision aids over data dumps.** `jobCellState: "Visible"` (one word, decision is obvious) beats dumping 19 grid cells of raw JP that Claude has to interpret. Surface the *conclusion*, not the inputs.

**Where things go:**

| Compact one-liner | Verbose JSON only | Nowhere |
|---|---|---|
| Things Claude reads on every turn — state name, `ui=`, `viewedUnit=`, location, status. Tight budget; add only when a missing field would cost decisions on the next action. | Things Claude reads when planning — full loadouts, ability lists, grid dumps, per-unit detail. Liberal budget; if it could plausibly inform a decision, surface it here. | Anything that mirrors what hovering already reveals in-game. Per-cell stats Claude can read by moving the cursor. Anything the game shows clearly that isn't load-bearing for a *programmatic* decision. |

**Before adding a new field, write one sentence answering "what decision changes if Claude has this?"** If you can't, drop it. If the answer is "Claude could plan a turn ahead with this," verbose. If it's "Claude needs this to pick the next action," compact. If it's "it's nice to have," nowhere.

---


## Status Key
- [ ] Not started — atomic task, split larger items into smaller ones
- [x] Done (archived at bottom)

---


## Priority Order

Organized by "what blocks Claude from playing a full session end-to-end" — most blocking first.

---


## 0. Urgent Bugs

### Session 60 — Enemy-Turn Narrator feature (2026-04-23)

> Replaces the two "enemy-turn damage / counter-KO not surfaced" bugs below with a concrete feature plan. See [plan file](../../../../Users/ptyRa/.claude/plans/cached-dazzling-panda.md) for design + rationale.

**Phase 1 — MVP (before/after diff, no attribution)**

- [ ] **Pure helper: `BattleNarratorRenderer`** [Narrator] — Takes `List<ScanChangeEventDto>` + active player name → returns `> ...` lines appended to `response.Info`. TDD fixtures: damage/heal/move/ko/revived/empty/truncation-to-~8-lines. Filter: suppress healed events on the active player when delta ≤ expected Regen tick. File: `ColorMod/GameBridge/BattleNarratorRenderer.cs`. 6-8 tests.

- [ ] **Wire pre-wait snapshot into `BattleWait()`** [Narrator] — At method entry in `NavigationActions.cs:458`, before the poll loop, call `CollectUnitPositionsFull()` and stash into a local `List<UnitSnap> preWait`. No behavior change yet — plumbing only.

- [ ] **Wire post-wait snapshot + diff + render** [Narrator] — At poll-exit block (line ~715), call `CollectUnitPositionsFull()` again, pass `(preWait, postWait)` to `UnitScanDiff.Compare()`, feed result into `BattleNarratorRenderer`, append lines to `response.Info`. Behavior: narrated events now appear in `battle_wait` output.

- [ ] **Integration test: BattleWait snapshot/diff wiring** [Narrator] — One test using fake pre/post UnitSnap lists + assertion that response.Info contains expected `> ...` lines. Verifies end-to-end without live game.

**Phase 2 — Inference helpers**

- [ ] **Pure helper: `CounterAttackInferrer`** [Narrator] — Event list + active player name → synthesized `Ramza countered X for N dmg` when a player took damage AND an enemy died in the same wait-window AND no player `moved`/`ko` event preceded the enemy death. TDD fixtures: counter-KO, counter-no-KO, multiple counters, false positives. File: `ColorMod/GameBridge/CounterAttackInferrer.cs`. 5-7 tests.

- [ ] **Pure helper: `SelfDestructInferrer`** [Narrator] — Event list → detect one enemy dying AND multiple units taking damage in the same wait-window → emit `Bomb self-destructed (dealt N to Ramza, N to Skeleton)`. TDD fixtures. File: `ColorMod/GameBridge/SelfDestructInferrer.cs`. 4-6 tests.

- [ ] **Wire inferrers into narrator pipeline** [Narrator] — After `UnitScanDiff.Compare`, feed event list through `CounterAttackInferrer` and `SelfDestructInferrer` before rendering. Inferrers append synthesized events (or replace raw damaged/ko pair with a richer synthesized one). Integration test.

**Phase 3 — Memory hunts (prereqs for per-action attribution)**

- [ ] **Memory hunt: active-unit-index byte during BattleEnemiesTurn / BattleAlliesTurn** [Memory] — Diagnostic `fft.sh` helper takes snapshots at the start of each per-unit enemy-turn window and diffs. Expected: a u8 cycling through roster/battle-array indices. Writes finding to `memory/project_active_unit_index.md`.

- [ ] **Memory hunt: currently-executing-ability-id byte during BattleActing** [Memory] — Diagnostic snapshots during an enemy spell cast. Expected: a u16 with the ability ID for ~N frames of animation. Writes to `memory/project_enemy_ability_id.md`.

**Phase 4 — bonus, only if hunts succeed**

- [ ] **Per-action narrator: mid-turn polling + ability names** [Narrator] — Once the two memory hunts land, restructure `BattleWait` poll loop to sample active-unit-index + ability-id per iteration. Emit `> Grenade cast Ignite on Ramza for 100 dmg` instead of generic `Ramza took 100 damage`. Empirically verify direct battle-array reads are safe during BattleEnemiesTurn (no C+Up) before wiring.

### Session 60 — other battle-play gaps (2026-04-23)

- [ ] **🔴 `execute_turn` leaves game stuck mid-sequence** [Execution] — Live-repro S60: `execute_turn 4 6 "Phoenix Down" 4 7` timed out after 5s. Game ended up in BattleMoving with cursor on (4,6), F-confirm not fired. Recovery required manual `battle_wait` which took 30+ seconds. Two bugs in one: (a) the 5s bridge timeout is too aggressive for execute_turn bundles, (b) the move-confirm step didn't fire. Probably same family as the S59 menuCursor drift.

- [ ] **🔴 Screen detection reports BattleMoving while game is in BattleWaiting** [Detection] — Live-repro S60: after the stuck `execute_turn`, `screen` persistently returned `[BattleMoving] ui=(4,6)` but user confirmed the actual game state was BattleWaiting (facing-direction select). Stale `battleMode` byte or stale-cursor-cache issue. Add a signal discriminator: if we arrived at BattleMoving via a recent move-commit key press, re-check after 500ms and accept BattleWaiting if it appears.

- [ ] **🟡 scan_move output missing basic Attack entry + weapon name** [Scan] — Live-repro S60: active-unit Abilities list shows Mettle + Items but no line for basic `Attack` (the `battle_attack`/`battle_ability "Attack"` action). Claude has to guess the weapon's range/element. Add a synthetic top entry: `Attack R:N [weapon-element] → <targets>` with the equipped weapon's WP/range/element from ItemData. Also surface the weapon name in the compact header (`[BattleMyTurn] Ramza(Gallant Knight) [Iron Flail] (2,1) HP=...`).

- [ ] **🟡 `scan_move` deprecation message is terse** [Shell] — `scan_move` now prints `[USE screen] scan_move is deprecated. Use: screen` and exits. Should either run `screen` directly (forward compat) OR print a single clear line explaining the migration path + cite Commands.md section.

- [ ] **🔴 `battle_ability` submenu detection misses "Items"** [Execution] — Live-repro S60 at The Siedge Weald: `battle_ability "Phoenix Down" 6 4` failed with `Skillset 'Items' not in submenu: Attack, Mettle`, but Items WAS visible in the in-game submenu. Helper parses only Attack/Mettle and doesn't see Items. Blocks every item use via the helper (Phoenix Down, X-Potion, Ether, Remedy). Likely a submenu-scraping bug: either the memory read stops after 2 entries or the label lookup filters out "Items". Repro: fresh BattleMyTurn with Items secondary → `battle_ability "Phoenix Down" ...` → check what `battle_ability` logs for the submenu list.

- [ ] **🟡 Scan doesn't tag petrified/stoned units as effectively-dead** [Scan] — Live-repro S60: Bomb at (5,5) had `[Petrify,Float,Critical]` but scan still listed it as a valid target for Attack/abilities. User feedback: stoned units are untargetable (can't be attacked, can't act, counted for battle-end like KO). Needs: `Petrify` status treated same as `Dead` for target filtering + append `DEAD` (or `STONE`) suffix in unit listing. Avoids wasted turns trying to hit statue enemies.

- [ ] **🟡 Weapon on-hit status effect not surfaced on scan_move** [Scan] — Live-repro S60: Ramza's equipped weapon had a Petrify proc chance that stoned a Bomb on a basic Attack. User informed Claude in-the-loop; the mod didn't surface it. Needs: active-unit summary includes `weapon=<Name> [onHit:Petrify%]` style when `AttackEffects` is set on the weapon (ItemData.cs already has an `AttackEffects` field — just needs wire-up to scan render).

- [ ] **🟡 `ui=` reports Abilities when cursor is actually on Move** [Detection] — Live-repro S60: after a battle_attack returned, screen showed `[BattleMyTurn] ui=Abilities` but user confirmed cursor was on Move. Same family as the BattleMoving/BattleWaiting stale-byte bug — menuCursor byte reading stale after an action. Candidate fix: refresh menuCursor on fresh BattleMyTurn entry, reset to 0 by default on state transition.

- [ ] **🟡 `battle_ability`/`battle_attack` returns no damage/kill info** [Stats/Execution] — Live-repro S60: `Used Throw Stone on (8,5)` printed without any HP delta or kill marker, even though it dealt damage. Scan after-the-fact showed target still alive at 61 HP. Needs: post-action scan to compute target HP delta and surface `→ Skeleton HP 90 → 61 (−29)` in the response line. Matches the pattern `battle_attack` already does for basic attacks (`HIT (650→90/650)`).

- [ ] **🔴 No validation that Act is already consumed this turn** [Execution] — Live-repro S60: after a successful basic Attack (which consumed Act), retrying `battle_attack` failed silently with `Failed to enter targeting mode (current: BattleMoving)` instead of the correct error `Act already used this turn — only Move or Wait remain`. Helpers should pre-check `battleActed` byte (or equivalent) and return a clean "already acted" message so Claude knows to pivot to move-then-wait. Blocks autonomous play — Claude currently wastes cycles banging on an action the game won't allow.

- [ ] **🟡 ui field should reflect action-consumed state** [Scan] — If Act is consumed, the `[BattleMyTurn]` header could surface `acted` tag (e.g. `[BattleMyTurn] ui=Move acted Ramza ...`) so Claude knows without probing. Related to menuAvailability work in S58/S59.

- [ ] **🔴 Victory misdetected as BattleDesertion** [Detection] — Live-repro S60 solo Ramza at Siedge Weald: after killing the last Skeleton, the final `battle_wait` returned `[BattleDesertion]` even though a screenshot immediately confirmed the game was on WorldMap (Victory flow completed cleanly). Petrified Bomb was still on the field — possibly the trigger: game auto-scored Victory on player alive + no undead-animate enemies, mod detection read stale sentinels and classified as Desertion. Sister bug to the S59 post-Victory misdetects. Capture: run `session_tail failed` at the end of each battle; any Desertion followed by WorldMap within 5s is this bug.

- [ ] **🟡 Attack tiles include dead units** [Scan] — Live-repro S60: `Attack tiles: Up→(4,4) enemy (Skeleton)` when the Skeleton at (4,4) had HP=0 `[Dead]`. Dead tile shouldn't be attack-suggestable. Filter attack-tile occupants by lifeState.

- [ ] **🟡 `battle_wait` slow — 17-33s per end-of-turn** [Speed] — Live-repro S60: `battle_wait` took 17s, 20s, 21s, 33s across consecutive turns. Even with Ctrl fast-forward, enemy turn animations dominate. Investigate: is the bridge polling at a slower rate than needed? Is the Ctrl fast-forward actually applying? Target: sub-10s for a 2-enemy end-of-turn.


## 1. Battle Execution (P0, BLOCKING)


## 2. Story Progression (P0, BLOCKING)


## 8. Speed Optimizations (P1)


## 9. Battle — Advanced (P2)


## 12. Known Issues / Blockers


## 13. Battle Statistics & Lifetime Tracking


## Low Priority / Deferred


## Sources & References

- [fft-map-json](https://github.com/rainbowbismuth/fft-map-json) — Pre-parsed map terrain data (122 MAP JSON files)
- [FFT Move-Find Item Guide by FFBeowulf](https://gamefaqs.gamespot.com/ps/197339-final-fantasy-tactics/faqs/32346) — ASCII height maps for MAP identification
- [FFHacktics Wiki](https://ffhacktics.com/wiki/) — PSX memory maps, terrain format, scenario tables


---


## Completed — Archive

Completed `[x]` items live in [COMPLETED_TODO.md](COMPLETED_TODO.md). During the session-end `/handoff`, move newly-completed items from here into that file. Partial `[~]` items stay in this file in their original section.
