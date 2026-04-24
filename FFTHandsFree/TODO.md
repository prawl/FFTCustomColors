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

- [ ] **⚠ UNVERIFIED: CriticalHpInferrer threshold-crossing line** [Narrator] — Wired + 15 tests (9 original + 6 edge-case boundary tests shipped `442ef5d`). Regen tends to keep Ramza above the 1/3 threshold during solo battles, so the crossing rarely triggers live. Repro: take a hard hit that drops a player below `MaxHp / 3` mid-wait. Expected: `> Ramza reached critical HP (400→180/719)`.

### Narrator — polish follow-ups

- [ ] **⚠ UNVERIFIED: Narrator pre-snap 400ms settle is enough for every action** [Narrator] — Shipped `5adeda1` 2026-04-24 bumped settle 200ms → 400ms. Not yet organically tripped a false-positive at 400ms, but also not exhaustively tested. If a false-positive counter line appears post-player-action, bump to 600ms or thread a post-action explicit refresh hook.

- [ ] **🔴 BattleDialogue misdetected as TravelList after save-load** [Detection] — Live-repro 2026-04-24: user loaded a save at Zeklaus Desert with an active BattleDialogue, `screen` returned `[TravelList] curLoc=Zeklaus Desert` consistently across polls. rawLocation=28 + stale party/ui flags from the load likely match the TravelList rule at `ScreenDetectionLogic.cs:471-472` (`party==0 && ui==1`) before the BattleDialogue rule gets a chance to fire. Fix path: either loosen the BattleDialogue rule to accept pre-battle event states, or tighten TravelList/WorldMap rules to check eventId first (if an event is active, we're NOT on a plain world-map screen).

- [ ] **🟡 execute_turn lacks Act/Move-consumed pre-flight validation** [Execution] — `battle_ability` and `battle_attack` already shipped the Act-consumed re-check (commit `8cf9197`) returning the clean message "Act already used this turn — only Move or Wait remain." `execute_turn` bundles move+ability+wait but doesn't do the same pre-flight — when entered with an already-consumed Act, it silently tries the move sub-step, misses, and fails with the misleading "Not in Move mode (current: BattleMyTurn)". Live-repro 2026-04-24: Ramza acted earlier via a partially-advanced prior execute_turn, then the next execute_turn attempt returned the wrong error. Fix: mirror the entry-reset logic from BattleAttack/BattleAbility at the top of ExecuteTurn, return the canonical "Act already used" / "Move already used" message before dispatching sub-steps.

- [ ] **🔴 PLANNING-HEAVY: menuCursor byte keeps drifting from visible cursor state** [Detection] — Recurring bug across sessions: `ui=X` on BattleMyTurn reads a stale menuCursor (0x1407FC620) after non-action transitions (pause-menu escape, mid-submenu escape, state restoration). Live-repro 2026-04-24 Siedge Weald: cursor visually on Wait, scan reported `ui=Abilities`. The existing `_actedThisTurn` / `_movedThisTurn` correction (shipped `481e64d`) only handles post-action stales. The NOT-acted-NOT-moved-but-navigated case is currently uncovered. **Don't tackle this without a planning pass first.** Options, all with tradeoffs: (a) reset menuCursor byte to 0 (Move) on fresh-BattleMyTurn detection — needs reliable "fresh entry" signal across paths (enemy turn end, pause escape); (b) shadow the cursor via key-press history in CommandWatcher — robust but tracks every key path; (c) WRITE memory after known navigations to force the byte in-sync — leaks mod state into game memory; (d) drop the ui= label entirely on transitions we can't verify and render `ui=?` — safe but loses info. Estimate 2-3 hours to spec, another 2-3 to implement + TDD. Read `project_sm_cursor_tracking_pattern.md` first — it's the pattern that worked for non-battle cursors.

- [ ] **🟡 Player facing byte reads wrong value — off from visible direction** [Scan] — Live-repro 2026-04-24 Lenalian Plateau: Ramza visually facing West, memory decoder returned `facing=East`. All 6 enemies' facing bytes matched visuals correctly. Enemy facings come from static battle array slot +0x35 (FacingByteDecoder: 0=S, 1=W, 2=N, 3=E). Player slot may use a different layout, OR the player's facing byte doesn't update after movement completes, OR Ramza's slot is being resolved to the wrong index. Next step: live-inspect Ramza's slot bytes at slot_base+0x30..+0x36 after setting known facings via `battle_wait` N/S/E/W and recording memory bytes. If the byte IS updating but we read the wrong one, the resolution is an index fix. If the byte ISN'T updating, find where the game writes player facing (probably a different address).

- [x] **🟡 `Recommend Wait: Face X` picks wrong direction + arc counts off** [Scan] — AUDITED + TEST PINNED 2026-04-25: FacingDecider N/S label flip was shipped `c36ec53`. This session hand-traced the live-repro scenario (Ramza (8,2) + 6 enemies in varied positions) and found the algorithm is CORRECT — it picks facing (0,+1)="South" because zero enemies are north, minimizing back-arc exposure. The earlier "0/4/2 expected" hand-count was for a DIFFERENT facing (North, 0,-1); the "West 2/1/1" count only included adjacent/close enemies while the algorithm scores all enemies with distance decay. New characterization test `ArcCount_LenalianPlateauLiveScenario_PinsRecommendation` pins the expected output so future refactors can't silently drift.

### Phase 3 — Memory hunts (blocks per-action attribution)

- [ ] **Memory hunt: active-unit-index byte during BattleEnemiesTurn / BattleAlliesTurn** [Memory] — Diagnostic `fft.sh` helper takes snapshots at the start of each per-unit enemy-turn window and diffs. Expected: a u8 cycling through roster/battle-array indices. Writes finding to `memory/project_active_unit_index.md`. Unblocks Phase 4 per-action narrator ("> Grenade attacked Ramza for 100 dmg").

- [ ] **Memory hunt: currently-executing-ability-id byte during BattleActing** [Memory] — Diagnostic snapshots during an enemy spell cast. Expected: a u16 with the ability ID for ~N frames of animation. Writes to `memory/project_enemy_ability_id.md`. Unblocks ability-name attribution ("Ice Animna") in narration.

- [ ] **Memory hunt: `battleActed` / `battleMoved` byte drift** [Memory] — Bytes read 0 transiently right after a confirmed player action (Phoenix Down, Throw Stone both live-observed with `battleActed: 0` in the response JSON despite the action clearly resolving). The `acted`/`moved` tag feature is correct given the bytes; the bytes themselves are unreliable. Fix path: find an alternative authoritative memory cell (possibly in the per-unit struct rather than the global BattleActed) OR add a software-side "I just sent an action-confirm Enter" flag that overrides the byte read for ~500ms.

### Phase 4 — per-action narrator (blocks on Phase 3 hunts)

- [ ] **Per-action narrator: mid-turn polling + ability names** [Narrator] — Once the two memory hunts land, restructure `BattleWait` poll loop to sample active-unit-index + ability-id per iteration. Emit `> Grenade cast Ignite on Ramza for 100 dmg` instead of generic `Ramza took 100 damage`. Empirically verify direct battle-array reads are safe during BattleEnemiesTurn (no C+Up) before wiring.

### Execution / detection

- [ ] **🔴 Victory misdetected as BattleDesertion** [Detection] — Live-repro solo Ramza at Siedge Weald: after killing the last Skeleton, the final `battle_wait` returned `[BattleDesertion]` even though a screenshot immediately confirmed the game was on WorldMap (Victory flow completed cleanly). Petrified Bomb was still on the field — possibly the trigger: game auto-scored Victory on player alive + no undead-animate enemies, mod detection read stale sentinels and classified as Desertion. Capture: run `session_tail failed` at the end of each battle; any Desertion followed by WorldMap within 5s is this bug.

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
