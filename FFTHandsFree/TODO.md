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

### Narrator — remaining UNVERIFIED features

- [ ] **⚠ UNVERIFIED: SelfDestructInferrer on a live Bomb** [Narrator] — Wired + tests green; no live Bomb self-destruct caught yet. Repro: maneuver a Bomb adjacent to Ramza + another player/ally so when it dies, 2+ units take damage in the same ~450ms mid-poll window. Expected: `> Bomb self-destructed (dealt N to Ramza, M to Agrias)` appears.

- [ ] **⚠ UNVERIFIED: CriticalHpInferrer threshold-crossing line** [Narrator] — Wired + 9 tests covering crossing/stay-above/already-critical/ko/enemy/healed. Regen tends to keep Ramza above the 1/3 threshold during solo battles, so the crossing rarely triggers live. Repro: take a hard hit that drops a player below `MaxHp / 3` mid-wait. Expected: `> Ramza reached critical HP (400→180/719)`.

### Narrator — polish follow-ups surfaced during live verify

- [~] **🟡 Enemy name misattribution across chunks** [Scan] — Same enemy can render as "Black Chocobo" in one scan and "Skeletal Fiend" in the next mid-battle. SHIPPED `2f52a79` 2026-04-24: `HeapUnitMatchClassifier` scores candidates by level-byte agreement at struct+0x09, `CollectUnitPositionsFull` picks highest-scoring match instead of first-match. Needs live-verify that common HP patterns (HP=4 / HP=100 etc.) no longer produce chunk-level relabeling. If relabeling persists, the root cause is deeper in the search (team byte, other discriminators).

- [ ] **🟡 Narrator pre-snap may still lag after some player actions** [Narrator] — Earlier commit (`24a0746`) added a 200ms settle before the fresh pre-snap in `BattleWait`. Not yet live-verified that 200ms is enough for every action type — basic Attack animations run longer than abilities. If a false-positive counter line appears post-player-action, bump the settle to 400-500ms or thread a post-action explicit refresh hook.

- [ ] **🟡 `[BattleVictory]` spurious detection flash during non-Victory actions** [Detection] — Observed multiple times this session during `battle_ability` response headers. The flash doesn't break anything (immediate follow-up screen query returns real state), but it's noisy in `session_tail` + any log review. Likely a short-circuit rule in `ScreenDetectionLogic` firing on transient sentinel values during cast animations. Audit the BattleVictory rules for tighter guards.

- [ ] **🟡 `Facing` direction + arc counts wrong — camera rotation stale** [Scan] — Live-repro 2026-04-24 Lenalian Plateau: Ramza at (8,2) actually facing West with arcs `2 front, 1 side, 1 back`; scan reported `Face North — 2 front, 4 side, 0 back`. Both the direction label AND the arc distribution are off — the FacingStrategy uses `_lastDetectedRightDelta` empirically detected during grid nav, and if the camera rotated since the last move, that delta is stale. Fix path: either re-detect rotation on every scan (fire a harmless arrow key + read cursor delta), or read the camera rotation byte at 0x14077C970 directly (already have the address from project_battle_coords.md) and derive Right-delta from it.

- [ ] **🟡 Attack tiles lists dead enemies as valid targets without marker** [Scan] — Live-repro 2026-04-24 Lenalian Plateau: Exploder at (7,2) shown in scan as `[ENEMY] Exploder (7,2) f=E HP=87/541 d=1 [Float,Critical]` but user confirmed the Exploder was actually dead (likely a stale read or post-self-destruct state we missed). `Attack tiles: Down→(7,2) enemy (Exploder) HP=87` rendered it as a valid melee target. Either (a) filter the `Attack tiles` render to exclude units whose lifeState is dead/crystal/treasure, (b) mark them like `Down→(7,2) dead (Exploder)` so Claude doesn't attempt a useless attack, OR (c) fix the upstream stale-read that's surfacing a dead unit as alive. (c) is probably a separate bug; ship (b) as a cheap safety net regardless.

- [ ] **🟡 CounterAttackInferrer false-KO attribution when MaxHp changes** [Narrator] — Live-repro 2026-04-24 Lenalian Plateau: Knight had `HP=521/521 [Defending]` before enemy turn. During the enemy turn Knight's Defending buff dropped, shifting the snapshot MaxHp 521→524. Narrator emitted `> Ramza countered Knight for 521 dmg — Knight died` but Knight was actually at 521/524 HP alive afterward (scan_move confirmed). Inferrer likely computed delta from (pre-snap HP=521) vs (post-snap HP=521 with a DIFFERENT MaxHp) and misread it as a 521-damage counter-KO. Fix path: when computing counter damage, require the unit to actually have `HP==0` or `lifeState==dead` before attributing a KO; and when MaxHp changes between snaps, treat HP delta as unreliable without a visual-damage confirm. Characterization test should pin the pre/post shape: `(preHp=521, preMaxHp=521, postHp=521, postMaxHp=524) → no-op` not `→ 521 dmg KO'd`.

### Phase 3 — Memory hunts (blocks per-action attribution)

- [ ] **Memory hunt: active-unit-index byte during BattleEnemiesTurn / BattleAlliesTurn** [Memory] — Diagnostic `fft.sh` helper takes snapshots at the start of each per-unit enemy-turn window and diffs. Expected: a u8 cycling through roster/battle-array indices. Writes finding to `memory/project_active_unit_index.md`. Unblocks Phase 4 per-action narrator ("> Grenade attacked Ramza for 100 dmg").

- [ ] **Memory hunt: currently-executing-ability-id byte during BattleActing** [Memory] — Diagnostic snapshots during an enemy spell cast. Expected: a u16 with the ability ID for ~N frames of animation. Writes to `memory/project_enemy_ability_id.md`. Unblocks ability-name attribution ("Ice Animna") in narration.

- [ ] **Memory hunt: `battleActed` / `battleMoved` byte drift** [Memory] — Bytes read 0 transiently right after a confirmed player action (Phoenix Down, Throw Stone both live-observed with `battleActed: 0` in the response JSON despite the action clearly resolving). The `acted`/`moved` tag feature is correct given the bytes; the bytes themselves are unreliable. Fix path: find an alternative authoritative memory cell (possibly in the per-unit struct rather than the global BattleActed) OR add a software-side "I just sent an action-confirm Enter" flag that overrides the byte read for ~500ms.

### Phase 4 — per-action narrator (blocks on Phase 3 hunts)

- [ ] **Per-action narrator: mid-turn polling + ability names** [Narrator] — Once the two memory hunts land, restructure `BattleWait` poll loop to sample active-unit-index + ability-id per iteration. Emit `> Grenade cast Ignite on Ramza for 100 dmg` instead of generic `Ramza took 100 damage`. Empirically verify direct battle-array reads are safe during BattleEnemiesTurn (no C+Up) before wiring.

### Execution / detection

- [~] **🔴 WorldMap false positive mid-battle during enemy-turn animations** [Detection] — Live-repro 2026-04-24 Lenalian Plateau: `battle_wait` returned `[WorldMap]` at t=10370ms during an enemy turn (next poll 2s later returned `[BattleEnemiesTurn]` correctly). battleMode transiently flickers to 0 during certain enemy animations while slot9 stays at 0xFFFFFFFF, tripping the post-battle-stale rule at `ScreenDetectionLogic.cs:612`. SHIPPED `7ca8b1d` 2026-04-24: `WorldMapBattleResidueClassifier` suppresses WorldMap when a Battle* state was detected <3s ago; CommandWatcher reverts to cached last-battle-state name. Needs live-verify that real battle→WorldMap transitions aren't blocked by the 3s window (edge case: instant-win Victory banners).

- [~] **🔴 Screen detection reports BattleMoving while game is in BattleWaiting** [Detection] — Live-repro: after a move-confirm, `screen` persistently returned `[BattleMoving] ui=(4,6)` but the actual game state was BattleWaiting (facing-direction select). SHIPPED `52fe7ea` 2026-04-24: `StaleBattleMovingClassifier` + CommandWatcher override flips BattleMoving → BattleWaiting when a Wait Enter was sent <500ms ago. `NavigationActions.LastWaitEnterTickMs` stamps the tick before the Enter send. Needs live-verify — watch logs for `[StateOverride] BattleMoving→BattleWaiting`. If the post-move-confirm variant (not Wait-Enter) still occurs, extend to a separate LastMoveConfirmTickMs stamp.

- [ ] **🔴 Victory misdetected as BattleDesertion** [Detection] — Live-repro solo Ramza at Siedge Weald: after killing the last Skeleton, the final `battle_wait` returned `[BattleDesertion]` even though a screenshot immediately confirmed the game was on WorldMap (Victory flow completed cleanly). Petrified Bomb was still on the field — possibly the trigger: game auto-scored Victory on player alive + no undead-animate enemies, mod detection read stale sentinels and classified as Desertion. Capture: run `session_tail failed` at the end of each battle; any Desertion followed by WorldMap within 5s is this bug.

- [ ] **🟡 `ui=` reports Abilities when cursor is actually on Move** [Detection] — Live-repro: after a `battle_attack` returned, screen showed `[BattleMyTurn] ui=Abilities` but user confirmed cursor was on Move. Same family as the BattleMoving/BattleWaiting stale-byte bug — menuCursor byte reading stale after an action. Candidate fix: refresh menuCursor on fresh BattleMyTurn entry, reset to 0 by default on state transition.

### Scan output polish

- [x] **🟡 Populate enemy Move / Jump for verbose unit rows** [Scan] — Shipped `3b868ae` + `26aa860` 2026-04-24: new `JobBaseStatsTable` (103 tests) covers generic jobs + monsters + story-unique classes with canonical WotL Mv/Jp. New `MoveJumpFallbackResolver` (7 tests) composes (live heap read) + (table base); both scan_move BFS input AND BattleUnitState now route through it. LIVE-VERIFIED 2026-04-24: Goblin Mv=4 Jp=3, Knight Mv=3 Jp=3, Archer Mv=3 Jp=3, Exploder Mv=4 Jp=3 all match the table. Active-unit Mv=0 collapse (live-observed after armor break) also patched via same resolver. Approximate values (no equipment bonuses) but sufficient for threat assessment.

### Speed

- [~] **🟡 `battle_wait` slow — 17-33s per end-of-turn** [Speed] — Live-repro pre-restart: 17s / 20s / 21s / 33s across consecutive turns. 2026-04-24 measured ~10s/turn after the 300→150ms poll interval drop. AUDITED 2026-04-24: poll overhead is already tight (150ms sleep + cached `_detectScreen` + every-3rd-tick narrator batch). Remaining cost is pure animation playback. Next step is a LIVE EXPLORATION: `NavigationActions.cs:740` notes Ctrl fast-forward doesn't work in IC remaster; find the actual fast-forward hotkey by trying Space / L-Shift / Tab / R / hold combinations during an enemy turn and timing with `session_stats`. Once found, wire via `DirectInput` hook same pattern as the old Ctrl injection.


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
