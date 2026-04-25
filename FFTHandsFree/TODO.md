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

(All shipped items archived to [COMPLETED_TODO.md](COMPLETED_TODO.md) under the 2026-04-25 entries.)

### Multi-unit turn-handoff (top priority — proposal drafted)

- [ ] **Loud `=== TURN HANDOFF: A → B ===` banner in `execute_turn` / `battle_wait` response** [Bridge] — when the bundled-turn helper returns and the active-unit identity has changed (diff `accumulator.InitialPostAction` vs final read), prepend the banner to `response.Info`. Multi-unit party agents lose track of which unit is now active and issue commands meant for the prior unit. See `project_multi_unit_turn_handoff_bug.md`.

- [ ] **Cache invalidation on turn-cycle boundary** [Bridge] — when BattleMyTurn returns from a non-MyTurn state (BattleEnemiesTurn / BattleAlliesTurn ended), clear `_lastValidMoveTiles`, `_lastValidAttackTiles`, `_lastValidAbilityTiles`, `_cachedActiveUnitName` / Job / X / Y / HP / WeaponTag. Different unit = different stats, abilities, start position; the prior unit's caches are stale.

- [ ] **Auto-scan post-turn-cycle** [Bridge] — when `execute_turn` or `battle_wait` returns to BattleMyTurn, automatically run `scan_move` and include results in the response. Mirror the existing post-`battle_move` re-scan at `CommandWatcher.cs:3937`. Caller's `response.battle.activeUnit` and `response.validPaths` are populated for the NEW unit immediately.

### Narrator gap (deferred from playtest #3)

- [ ] **Narrator damage/KO not always captured** [Narrator] — playtest agent saw a Skeleton go from 344/680 → DEAD with no `> X took N damage` / `> X died` line in the `battle_wait` event log. `BattleNarratorRenderer` supports `damaged`/`ko` events, so the gap is upstream in `UnitScanDiff.Compare` (HP transitions not surfaced for some damage paths, possibly enemy-on-enemy or multi-target hits). Repro: get into a battle where multiple enemies attack each other / ally each other; diff scans pre/post `battle_wait`; verify `damaged` events fire for all HP changes.

### Phase 3 — Memory hunts (blocks per-action attribution)

- [~] **Memory hunt: active-unit-index byte during BattleEnemiesTurn / BattleAlliesTurn** [Memory] — Infrastructure SHIPPED 2026-04-25 (`0455332` `memory_diff` bridge action + `memory_diff` shell helper). Hunt itself deferred — needs a battle survival window long enough to capture: snap during BattleMyTurn (Ramza active), chunked battle_wait into BattleEnemiesTurn (use `maxPollMs:1500`), snap, diff via `memory_diff`. Today's attempt at Siedge Weald lost the battle before the cycle completed (Ramza died to enemy advance at HP 431 → 0). Re-attempt in a battle where Ramza can sustain multiple enemy-turn cycles without dying. The diff should reveal a u8 cycling through roster indices.

- [~] **Memory hunt: currently-executing-ability-id byte during BattleActing** [Memory] — Infrastructure SHIPPED 2026-04-25 (same `memory_diff` action). Hunt itself deferred — needs Ramza casting a high-CT spell (e.g. Ultima ct=20). Snap pre-cast (BattleMyTurn) and during cast (BattleActing window ~2s). Ramza's current jobset (Gallant Knight) doesn't have suitable cast-time abilities; needs a job change to Wizard / Time Mage first.

### Phase 4 — per-action narrator (blocks on Phase 3 hunts)

- [~] **Per-action narrator: mid-turn polling + ability names** [Narrator] — Blocked on the two `[~]` memory hunts above (active-unit-index + ability-id). Once those addresses surface, this is a ~1h restructure of `BattleWait` poll loop to sample both per iteration. Empirically verify direct battle-array reads are safe during BattleEnemiesTurn (no C+Up) before wiring.

### Speed

- [~] **🟡 `battle_wait` slow — variable per-turn** [Speed] — 2026-04-25: re-enabled Ctrl-hold fast-forward (Travel/AutoMove pattern, focus-aware so terminal typing isn't hijacked). Stale 2026-04-12 "doesn't speed up animations" note was wrong — Ctrl IS recognized. Live samples Siedge Weald: 5.4s (short enemy turn, only 1 active enemy reaching Ramza) / 9.3s / 9.0s. Down from baseline ~10s/turn to ~5-9s depending on enemy activity. Variance is now dominated by per-turn animation count, not per-frame speed. Remaining knobs if more speed needed: (a) shorter poll interval (currently 150ms — could try 100ms); (b) 2026-04-22 Options speed setting; (c) gamepad trigger emulation (XInput) for any *additional* fast-forward.


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
