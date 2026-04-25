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

- [ ] **🟡 Detection drift to `[EquipmentAndAbilities]` between battle-end and GameOver** [Detection] — Live-captured 2026-04-25 Siedge Weald. battle_wait was polling enemy turns; Ramza died; bridge's first post-loop screen call returned `[EquipmentAndAbilities]` (with abilities list rendered) instead of GameOver. Subsequent `screen` calls correctly returned `[GameOver]`. Fingerprint of the bad frame wasn't captured (only stable fingerprint after settle was). Hypothesis: when Ramza dies during enemy turn, transient memory state shares signal bytes with EqA screen for ~1 frame. Next step: add capture-on-misdetect logic that snapshots detection inputs whenever `[EquipmentAndAbilities]` fires while a battle was active, then add the right discriminator. Low-priority — the next screen call self-corrects.



### Narrator — remaining UNVERIFIED features

- [x] **CriticalHpInferrer threshold-crossing line** [Narrator] — LIVE-VERIFIED 2026-04-25 Siedge Weald random encounter. Ramza took a 300-dmg hit (HP 432→132, MaxHP 719, threshold=239.67). Live narrator emitted `> Ramza reached critical HP (432→132/719)` immediately following `> Ramza gained Critical`. Both the status-add and the threshold-crossing lines fired correctly. 15 unit tests + live verification — closing.

### Narrator — polish follow-ups

- [x] **⚠ UNVERIFIED: Narrator pre-snap 400ms settle is enough for every action** [Narrator] — RESOLVED 2026-04-25. Multiple battles played across recent sessions (Siedge Weald, plus Lenalian Plateau, plus today's random encounter) without a false-positive counter line at 400ms. Treating as adequate. If a false-positive surfaces in future play, the fix is a 1-line bump in BattleWait — re-open this item then. Closing because passive monitoring without an observed failure isn't an actionable TODO.

- [x] **🔴 PLANNING-HEAVY: menuCursor byte keeps drifting from visible cursor state** [Detection] — SHIPPED 2026-04-25 Phases 1-4 (`9518a81` proposal, `7401a2c` helper, `d471e1d` wiring, `5f71fa4` commit-to-act fix). Picked Option (a) from the proposal: write 0x1407FC620=0 on fresh-BattleMyTurn detection via `FreshBattleMyTurnEntryClassifier`. Submenu-escape paths (BattleAbilities/BattleMoving/etc.) preserve cursor; turn-boundary paths (BattleEnemiesTurn/Paused/Formation/etc.) reset to Move. Phase 5 (ui=? fallback for remaining uncertainty) deferred — fresh-entry write proved sufficient in live verify.

- [x] **🟡 battle_move onto a treasure tile triggers a Yes/No confirm dialog the helper doesn't handle** [Battle] — SHIPPED 2026-04-25 (`68dcb4f` chest banner detection, `f03772b` crystal move-confirm detection, `f3b5a80` MoveGrid auto-dismiss). Detection rules: dropped the `battleTeam==0` guard from BattleRewardObtainedBanner and BattleCrystalMoveConfirm — both fire whenever the modal fingerprint matches, regardless of which team's turn the modal interrupts. MoveGrid poll loop auto-Enters on those modals + BattleAbilityLearnedBanner (Yes is default-selected on confirms; Enter dismisses banners). Live-verified end-to-end at Siedge Weald.

- [x] **🟡 Strict mode silently blocks raw Enter key sends** [Bridge] — SHIPPED 2026-04-25 (`298307e`) — narrowest fix taken: route `enter()` shell helper through the `advance_dialogue` named action (which is already in AllowedGameActions and does exactly the same thing — SendKey(VK_ENTER)). Other raw key helpers (up/down/left/right/space/tab) remain blocked under strict mode but those are normally used via per-context named actions; only `enter` was a hot-path gap.

- [x] **🟡 Player facing byte reads wrong value — off from visible direction** [Scan] — ROOT CAUSED + SHIPPED 2026-04-25. Live-investigated at Siedge Weald rot=3: requested `battle_wait N`, expected Ramza to face game-North; he faced West instead. Slot 1 + slot 5 (player dual-table) bytes both stuck at pre-call values, neither matching the visual. The bug was at the boundary between `ParseFacingDirection` (post-219e60a uses -y=N convention) and `FacingStrategy.GetFacingArrowKey`'s `FacingArrowDelta` table (still used +y=N). Two helpers disagreeing about which sign means North silently sent the wrong arrow key on the facing screen. Fix: flipped FacingArrowDelta to -y=N, updated empirical tests to match, extended `FacingCoordConventionConsistencyTests` to pin the GetFacingArrowKey boundary so this drift can't recur. The player slot facing bytes themselves (+0x35) are now expected to update correctly when battle_wait sends the right arrow key. Note: the original Lenalian repro (visual W → byte E) was a 180° offset — at rotations 0/2 the +y=N vs -y=N flip produces a 180° error; at rotations 1/3 it produces 90°, matching today's Siedge Weald observation.

### Phase 3 — Memory hunts (blocks per-action attribution)

- [~] **Memory hunt: active-unit-index byte during BattleEnemiesTurn / BattleAlliesTurn** [Memory] — Infrastructure SHIPPED 2026-04-25 (`0455332` `memory_diff` bridge action + `memory_diff` shell helper). Hunt itself deferred — needs a battle survival window long enough to capture: snap during BattleMyTurn (Ramza active), chunked battle_wait into BattleEnemiesTurn (use `maxPollMs:1500`), snap, diff via `memory_diff`. Today's attempt at Siedge Weald lost the battle before the cycle completed (Ramza died to enemy advance at HP 431 → 0). Re-attempt in a battle where Ramza can sustain multiple enemy-turn cycles without dying. The diff should reveal a u8 cycling through roster indices.

- [~] **Memory hunt: currently-executing-ability-id byte during BattleActing** [Memory] — Infrastructure SHIPPED 2026-04-25 (same `memory_diff` action). Hunt itself deferred — needs Ramza casting a high-CT spell (e.g. Ultima ct=20). Snap pre-cast (BattleMyTurn) and during cast (BattleActing window ~2s). Ramza's current jobset (Gallant Knight) doesn't have suitable cast-time abilities; needs a job change to Wizard / Time Mage first.

- [x] **Memory hunt: `battleActed` / `battleMoved` byte drift** [Memory] — SHIPPED 2026-04-25 via the software-side override path (the alternative listed in the original TODO). `BattleActedMovedOverride` pure helper applies the existing `_actedThisTurn` / `_movedThisTurn` commit-time flags to the response.screen.battleActed/battleMoved fields when the raw bytes lag. Wired in CommandWatcher's BattleMyTurn/BattleActing block alongside the UI rendering that already used those flags. +6 tests pinning the override semantics. Memory hunt for an alternative authoritative byte deferred — the override resolves the user-visible inconsistency and the flags reset cleanly on turn boundaries.

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
