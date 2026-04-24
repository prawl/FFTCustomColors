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

### Enemy-Turn Narrator follow-ups (S60)

- [ ] **⚠ UNVERIFIED: CounterAttackInferrer KO-suppression in one window** [Narrator] — S60 wired + live-verified counter attribution ("Ramza countered Black Chocobo for 336 dmg"). Still need a live repro where enemy attack + counter + KO all land in the SAME 450ms mid-poll window so the ko-suppression path runs (raw "> Bonesnatch died" dropped in favor of "Ramza countered Bonesnatch for N dmg — Bonesnatch died"). S60 observed KO but attack+death split across chunks by a TitleScreen flicker; no suppression exercised.

- [ ] **⚠ UNVERIFIED: SelfDestructInferrer on a live Bomb** [Narrator] — Wired + tests green; no Bomb self-destruct caught during S60 verification. Repro: maneuver a Bomb adjacent to Ramza + another player/ally so when it self-destructs at least 2 units take damage. Expected: `> Bomb self-destructed (dealt N to Ramza, M to Agrias)` appears.

- [ ] **⚠ UNVERIFIED: `battle_ability` HP delta suffix** [Stats/Execution] — Shipped but not live-verified. Best test: Throw Stone from Ramza on an enemy. Expected shape: `Used Throw Stone on (8,5) (90→61/650)`. Phoenix Down on dead ally: ` — revived (0→50/477)`. Cast-time abilities (Cura, Fire, Haste) should omit the delta.

### Phase 3 — Memory hunts (blocks per-action attribution)

- [ ] **Memory hunt: active-unit-index byte during BattleEnemiesTurn / BattleAlliesTurn** [Memory] — Diagnostic `fft.sh` helper takes snapshots at the start of each per-unit enemy-turn window and diffs. Expected: a u8 cycling through roster/battle-array indices. Writes finding to `memory/project_active_unit_index.md`. Unblocks Phase 4 per-action narrator ("> Grenade attacked Ramza for 100 dmg").

- [ ] **Memory hunt: currently-executing-ability-id byte during BattleActing** [Memory] — Diagnostic snapshots during an enemy spell cast. Expected: a u16 with the ability ID for ~N frames of animation. Writes to `memory/project_enemy_ability_id.md`. Unblocks ability-name attribution ("Ice Animna") in narration.

### Phase 4 — per-action narrator (blocks on Phase 3 hunts)

- [ ] **Per-action narrator: mid-turn polling + ability names** [Narrator] — Once the two memory hunts land, restructure `BattleWait` poll loop to sample active-unit-index + ability-id per iteration. Emit `> Grenade cast Ignite on Ramza for 100 dmg` instead of generic `Ramza took 100 damage`. Empirically verify direct battle-array reads are safe during BattleEnemiesTurn (no C+Up) before wiring.

### Execution / detection

- [ ] **🔴 `execute_turn` leaves game stuck mid-sequence** [Execution] — Live-repro S60: `execute_turn 4 6 "Phoenix Down" 4 7` timed out after 5s. Game ended up in BattleMoving with cursor on (4,6), F-confirm not fired. Recovery required manual `battle_wait` which took 30+ seconds. Two bugs in one: (a) the 5s bridge timeout is too aggressive for execute_turn bundles, (b) the move-confirm step didn't fire. Probably same family as the S59 menuCursor drift.

- [ ] **🔴 Screen detection reports BattleMoving while game is in BattleWaiting** [Detection] — Live-repro S60: after the stuck `execute_turn`, `screen` persistently returned `[BattleMoving] ui=(4,6)` but user confirmed the actual game state was BattleWaiting (facing-direction select). Stale `battleMode` byte or stale-cursor-cache issue. Add a signal discriminator: if we arrived at BattleMoving via a recent move-commit key press, re-check after 500ms and accept BattleWaiting if it appears.

- [ ] **🔴 No validation that Act is already consumed this turn** [Execution] — Live-repro S60: after a successful basic Attack (which consumed Act), retrying `battle_attack` failed silently with `Failed to enter targeting mode (current: BattleMoving)` instead of a clean "already acted" message. Helpers should pre-check `battleActed` byte (or equivalent) and return `Act already used this turn — only Move or Wait remain` so Claude pivots instead of banging on a grayed action.

- [ ] **🔴 Victory misdetected as BattleDesertion** [Detection] — Live-repro S60 solo Ramza at Siedge Weald: after killing the last Skeleton, the final `battle_wait` returned `[BattleDesertion]` even though a screenshot immediately confirmed the game was on WorldMap (Victory flow completed cleanly). Petrified Bomb was still on the field — possibly the trigger: game auto-scored Victory on player alive + no undead-animate enemies, mod detection read stale sentinels and classified as Desertion. Sister bug to the S59 post-Victory misdetects. Capture: run `session_tail failed` at the end of each battle; any Desertion followed by WorldMap within 5s is this bug.

- [ ] **🟡 `ui=` reports Abilities when cursor is actually on Move** [Detection] — Live-repro S60: after a battle_attack returned, screen showed `[BattleMyTurn] ui=Abilities` but user confirmed cursor was on Move. Same family as the BattleMoving/BattleWaiting stale-byte bug — menuCursor byte reading stale after an action. Candidate fix: refresh menuCursor on fresh BattleMyTurn entry, reset to 0 by default on state transition.

### Scan output polish

- [ ] **🟡 Wire weapon tag into active-unit banner** [Scan] — S60 shipped `ItemData.ComposeWeaponTag` + `ActiveUnitSummaryFormatter.Format(weaponTag)` with tests. Wire-up from scan → CommandWatcher's `_cachedActiveUnitWeaponTag` is BLOCKED on BattleUnitState not exposing Equipment. Fix: add `Equipment` (List&lt;int&gt;?) field to BattleUnitState OR attach a `ActiveUnitWeaponTag` string directly to `battleState` in NavigationActions.CollectUnitPositionsFull.

- [ ] **🟡 ui field should reflect action-consumed state** [Scan] — If Act is consumed, the `[BattleMyTurn]` header could surface `acted` tag (e.g. `[BattleMyTurn] ui=Move acted Ramza ...`) so Claude knows without probing. Related to menuAvailability work in S58/S59.

- [ ] **🟡 `scan_move` deprecation message is terse** [Shell] — `scan_move` prints `[USE screen] scan_move is deprecated. Use: screen` and exits. Should either forward to `screen` (forward compat) OR print a single clear line with the migration path + cite Commands.md.

### Speed

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
