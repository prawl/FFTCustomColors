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

- [ ] **⚠ UNVERIFIED: CriticalHpInferrer threshold-crossing line** [Narrator] — Wired + 15 tests (9 original + 6 edge-case boundary tests shipped `442ef5d`). Regen tends to keep Ramza above the 1/3 threshold during solo battles, so the crossing rarely triggers live. Repro: take a hard hit that drops a player below `MaxHp / 3` mid-wait. Expected: `> Ramza reached critical HP (400→180/719)`. — 2026-04-25 live-verify attempt: tried to engineer the threshold crossing at Siedge Weald but enemy skeletons hit for ~8 damage/swing, far below the ~480 needed in a single enemy-turn window to cross from full HP (719) to below 239 threshold. `buff_ramza` sets roster values applied next battle, not mid-battle. Better repro: (a) use a story battle with heavy hitters (Goug fight, Fort Besselat); (b) buff an enemy's PA pre-battle via memory write; (c) accept unit-test coverage as sufficient — 15 tests with 6 edge-case boundary cases pin the logic.

### Narrator — polish follow-ups

- [ ] **⚠ UNVERIFIED: Narrator pre-snap 400ms settle is enough for every action** [Narrator] — Shipped `5adeda1` 2026-04-24 bumped settle 200ms → 400ms. Not yet organically tripped a false-positive at 400ms, but also not exhaustively tested. If a false-positive counter line appears post-player-action, bump to 600ms or thread a post-action explicit refresh hook.

- [x] **🔴 PLANNING-HEAVY: menuCursor byte keeps drifting from visible cursor state** [Detection] — SHIPPED 2026-04-25 Phases 1-4 (`9518a81` proposal, `7401a2c` helper, `d471e1d` wiring, `5f71fa4` commit-to-act fix). Picked Option (a) from the proposal: write 0x1407FC620=0 on fresh-BattleMyTurn detection via `FreshBattleMyTurnEntryClassifier`. Submenu-escape paths (BattleAbilities/BattleMoving/etc.) preserve cursor; turn-boundary paths (BattleEnemiesTurn/Paused/Formation/etc.) reset to Move. Phase 5 (ui=? fallback for remaining uncertainty) deferred — fresh-entry write proved sufficient in live verify.

- [x] **🟡 battle_move onto a treasure tile triggers a Yes/No confirm dialog the helper doesn't handle** [Battle] — SHIPPED 2026-04-25 (`68dcb4f` chest banner detection, `f03772b` crystal move-confirm detection, `f3b5a80` MoveGrid auto-dismiss). Detection rules: dropped the `battleTeam==0` guard from BattleRewardObtainedBanner and BattleCrystalMoveConfirm — both fire whenever the modal fingerprint matches, regardless of which team's turn the modal interrupts. MoveGrid poll loop auto-Enters on those modals + BattleAbilityLearnedBanner (Yes is default-selected on confirms; Enter dismisses banners). Live-verified end-to-end at Siedge Weald.

- [x] **🟡 Strict mode silently blocks raw Enter key sends** [Bridge] — SHIPPED 2026-04-25 (`298307e`) — narrowest fix taken: route `enter()` shell helper through the `advance_dialogue` named action (which is already in AllowedGameActions and does exactly the same thing — SendKey(VK_ENTER)). Other raw key helpers (up/down/left/right/space/tab) remain blocked under strict mode but those are normally used via per-context named actions; only `enter` was a hot-path gap.

- [ ] **🟡 Player facing byte reads wrong value — off from visible direction** [Scan] — Live-repro 2026-04-24 Lenalian Plateau: Ramza visually facing West, memory decoder returned `facing=East`. All 6 enemies' facing bytes matched visuals correctly. Enemy facings come from static battle array slot +0x35 (FacingByteDecoder: 0=S, 1=W, 2=N, 3=E). Player slot may use a different layout, OR the player's facing byte doesn't update after movement completes, OR Ramza's slot is being resolved to the wrong index. Next step: live-inspect Ramza's slot bytes at slot_base+0x30..+0x36 after setting known facings via `battle_wait` N/S/E/W and recording memory bytes. If the byte IS updating but we read the wrong one, the resolution is an index fix. If the byte ISN'T updating, find where the game writes player facing (probably a different address).

### Phase 3 — Memory hunts (blocks per-action attribution)

- [ ] **Memory hunt: active-unit-index byte during BattleEnemiesTurn / BattleAlliesTurn** [Memory] — Diagnostic `fft.sh` helper takes snapshots at the start of each per-unit enemy-turn window and diffs. Expected: a u8 cycling through roster/battle-array indices. Writes finding to `memory/project_active_unit_index.md`. Unblocks Phase 4 per-action narrator ("> Grenade attacked Ramza for 100 dmg"). — ATTEMPTED 2026-04-25: direct reads of condensed struct region (0x14077CA00..0x14077D200) showed unexpected values for known fields (`AddrActiveNameId` at 0x14077CA94 read 0x0191=401 when Ramza's nameId should be 1 during his own turn). Suggests the address mapping in BattleTracker.cs field consts may be stale for IC remaster OR those addresses are updated only on certain events. Next step: spec a `memory_diff` bridge action that takes 2 addresses + a size + returns only differing bytes. Call it at player-turn start + each enemy-turn start, grep for bytes that cycle through 0..N (roster index range).

- [ ] **Memory hunt: currently-executing-ability-id byte during BattleActing** [Memory] — Diagnostic snapshots during an enemy spell cast. Expected: a u16 with the ability ID for ~N frames of animation. Writes to `memory/project_enemy_ability_id.md`. Unblocks ability-name attribution ("Ice Animna") in narration. — BLOCKED on same infrastructure gap as active-unit-index hunt. Plan: build `memory_diff` action first (above TODO), then run Ramza cast Ultima (ct=20, ~2s BattleActing window) and snapshot. Ability ID 0x19 (Ultima / Ultima Demon ability) should appear somewhere in main module when cast is active vs idle.

- [x] **Memory hunt: `battleActed` / `battleMoved` byte drift** [Memory] — SHIPPED 2026-04-25 via the software-side override path (the alternative listed in the original TODO). `BattleActedMovedOverride` pure helper applies the existing `_actedThisTurn` / `_movedThisTurn` commit-time flags to the response.screen.battleActed/battleMoved fields when the raw bytes lag. Wired in CommandWatcher's BattleMyTurn/BattleActing block alongside the UI rendering that already used those flags. +6 tests pinning the override semantics. Memory hunt for an alternative authoritative byte deferred — the override resolves the user-visible inconsistency and the flags reset cleanly on turn boundaries.

### Phase 4 — per-action narrator (blocks on Phase 3 hunts)

- [ ] **Per-action narrator: mid-turn polling + ability names** [Narrator] — Once the two memory hunts land, restructure `BattleWait` poll loop to sample active-unit-index + ability-id per iteration. Emit `> Grenade cast Ignite on Ramza for 100 dmg` instead of generic `Ramza took 100 damage`. Empirically verify direct battle-array reads are safe during BattleEnemiesTurn (no C+Up) before wiring.

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
