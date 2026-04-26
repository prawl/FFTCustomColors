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

(Multi-unit turn-handoff items shipped earlier; see `project_multi_unit_turn_handoff_bug.md` and `COMPLETED_TODO.md` 2026-04-25 entries — `TurnHandoffBannerClassifier`, `ClearActiveUnitCache`, post-wait settle-scan are all wired.)

(Narrator damage/KO gap shipped 2026-04-26 in commit below — `removed`-with-`OldHp>0` now emits a death line; `PhantomKoCoalescer` suppresses the false-positive shape from transient bad scans.)

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

(Items 1, 2, 4-pin, 7, 9 above shipped 2026-04-26 — see `COMPLETED_TODO.md`. Remaining open §9 items below.)

- [ ] **Jump can whiff when target moves out of the landing tile during the airborne window** [Bridge/Render] — Live-flagged 2026-04-26 playtest: Kenrick Jump'd (4,8) targeting wounded Archer; Archer moved (4,8)→(4,10) during enemy turn before Kenrick landed; Jump hit empty tile (no damage). Mechanically correct FFT behavior — Jump targets a TILE not a unit. But the bridge gave the agent zero pre-flight signal that this could happen, and no post-flight feedback that the jump whiffed. Suggested surfacing: (a) on Jump targeting, annotate the target as `<Archer (likely-mobile, may move)>` vs `<Archer [Stop|Charging|DontMove] (locked)>` so agent prefers immobile targets. (b) when the airborne unit lands, surface a `> [Jump landed empty — target moved (4,8)→(4,10)]` event so agent doesn't have to grok damage = 0 from HP diff. (c) document this in BattleTurns.md or AbilitiesAndJobs.md so a fresh agent knows the trade-off.

- [ ] **Phantom-success: ability "lands" but the game never received the target** [Bridge/Timing] — Live-flagged 2026-04-26 playtest: Ramza X-Potion → (10,8) (Wilham at 8 HP). Bridge response: `> [battle_ability] Used X-Potion on (10,8)` and turn advanced. But Wilham was NOT actually healed — user observed the input was too fast for the game UI to register the target selection. Distinguishes from the static-region-stale-HP bug above (where heal landed but scan was stale): here, the heal genuinely never happened. Likely cause: between submenu nav, target-tile nav, and final Enter-confirm, one of the key intervals is too short and the game dropped a press; the bridge interprets "no error during nav" as success. Fix: post-action verification on heal/damage abilities — read target HP before AND after, classify miss-vs-stale vs phantom: (a) HP changed = success; (b) HP unchanged + we waited a settling window = real failure (don't claim success). Tie-breaker: read MaxHP/Status (e.g. Critical for low-HP) — success should also clear Critical when applicable. Cross-ref: `feedback_phantom_success_pattern.md` memory note covers similar shape. Needs ability metadata to know which abilities expect HP delta.

- [ ] **Stale post-heal HP in `screen` Units block after execute_turn X-Potion** [Bridge/Read] — Live-flagged 2026-04-26 playtest: Ramza X-Potion'd Wilham (9,8) for +150 HP. The bridge confirmed `> [battle_ability] Used X-Potion on (9,8)` and the heal landed visually + per the user's eyes. But the very next `screen` scan reported Wilham at HP=92/528 unchanged from pre-heal. The agent (correctly!) interpreted this as "heal failed" and retried — that retry would have wasted an X-Potion. Smells like the static battle array is lagging the live-HP write again (matches `feedback_persistent_snap_stale_read.md` — static region lags hundreds of ms post-action). Fix path: after `battle_ability` resolves a heal/damage, force a fresh-snap settle before returning the post-action HP. OR — read live-HP region (not static) for the targeted unit specifically when the response carries a heal/damage outcome. OR — surface a `[stale-warning]` flag on Units block entries when scan was within ~500ms of a HP-mutating action. Repro: any heal that succeeds in-game; immediately call `screen`; observe pre-heal HP.

- [ ] **Geomancy renders one terrain-resolved spell, not a 13-row catalog dump** [Bridge/Render] — Geomancer's "Elemental" ability casts a SINGLE spell determined by the terrain the unit is standing on (Grass→Tanglevine, Lava→Magma Surge, Sand→Sandstorm, etc.). Today the scan output dumps all 13 variants as separate rows under Abilities, which is a wall of nearly-identical lines that buries everything else (playtest #7 / #8 friction — the agent had to scroll past the catalog to find the Units block). User-requested format: render as a single line — `Geomancy: <ResolvedSpellName> R:5 AoE:2 [Element] {Status}` — like other primary skillset entries. Requires: (a) read the terrain-type byte at the active Geomancer's tile, (b) map terrain ID → spell name (canonical FFT mapping; per-spell description text references the terrain types it covers), (c) filter the scan's ability list to only the resolved spell when the skillset is Geomancy. **Deferred 2026-04-26** because it needs a terrain-byte memory hunt; the [fft-map-json](https://github.com/rainbowbismuth/fft-map-json) reference at the bottom of this file may have the static terrain table. Live battle reads still needed for confirmation. Surface to fix is `screen` rendering and possibly `ActionAbilityLookup.cs`'s Geomancy entry list.

- [ ] ⚠ UNVERIFIED — **Post-victory screen detection mis-classifies WorldMap as LoadGame** [Bridge/Detection] — Live-flagged 2026-04-26 at Siedge Weald: after `execute_action Advance` post-Victory, bridge surfaced `LoadGame` (validPaths ScrollUp/ScrollDown/Select/Cancel) on what was visually the WorldMap. A regression-pin test was added 2026-04-26 (`DetectScreen_PostVictoryWorldMap_BattlegroundRawLocation_DoesNotMisdetectAsLoadGame`) for the obvious fingerprint (rawLocation=26, gameOverFlag=1, moveMode=0) but it PASSED with the existing rules — meaning the live bug repro had a different memory fingerprint we haven't captured yet. Likely culprit: the `Advance` validPath impl is overshooting (post-victory dialog → EXP → etc.) and lands the player on an actual LoadGame screen; the detection isn't wrong, the action is. Re-investigate: capture the exact memory state when this misfires next time and either tighten the LoadGame rule OR cap `Advance` to a single confirm.


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
