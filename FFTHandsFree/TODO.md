<!-- This file is exempt from the 200 line limit. -->
# FFT Hands-Free — Battle Automation (V1 push)

> **V1 scope (2026-04-22):** this TODO now tracks only battle-related work. Everything non-battle (shops, taverns, party menu, world travel, cutscenes, mod separation, etc.) moved to [DEFERRED_TODO.md](DEFERRED_TODO.md). Goal: Claude fully automated in battle.

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

This rule killed AC5 (per-class Lv/JP on JobSelection grid) — Claude doesn't need 19 JP values to decide a job change; the cell they're hovering shows it in-game already.

---


## Status Key
- [ ] Not started — atomic task, split larger items into smaller ones
- [x] Done (archived at bottom)

---


## Priority Order

Organized by "what blocks Claude from playing a full session end-to-end" — most blocking first.

---


## 0. Urgent Bugs


### Session 56 — battle_ability fix (2026-04-22)

<!-- "battle_ability targeting cursor stale across abilities" — investigated and found NOT a bug. The "cursor at (8,9) expected (9,9)" failure in S56 live-testing was actually Claude-side: I passed stale (9,9) target coords while Kenrick had moved to (8,9) between scans. The targeting cursor correctly started on the caster's actual position. No code fix needed. -->

- [ ] **AOB resolver is a research dead-end without pointer chains** — S55 spent ~2h on `<listSize_u64> + 0x1407FC6D8 vtable` AOB; worked once, then failed because the cursor's offset within the widget shifts between widget allocations (sometimes +0x10, sometimes pushed back by a second-vtable insertion). Reverted commits `7def3c2`, `892f979`, `b4d7d98`. **Don't re-attempt without first solving** the structural-offset instability — see memory note for full failure analysis. Pointer-chain (Cheat Engine pointer scan) is the only path that survives widget reallocation. (S56: escape-to-known-state option 3 shipped — this AOB approach not needed for V1.)

<!-- S55 🔴 BLOCKING `battle_ability picks the wrong ability` + 3-step breakdown (extract BattleAbilityEntryReset / wire escape-to-known-state / wire AbilityListCursorNavPlanner.Plan) all shipped in S56. Live-verified: Chakra (Martial Arts idx 6, planner chose Up×2) and Haste (Time Magicks idx 0, None×0) cast correctly. Memory note updated. -->


### Session 52 — scan_diff identity + per-unit ct hunt (2026-04-20)

- [ ] **kill_one player persistence regression** — Session 52 found `kill_one Wilham` wrote HP=0 + dead-bit to master HP slot `0x14184FEC0` but after a turn cycle Wilham showed HP=477 again. Session-49 docs say master is authoritative but for PLAYERS the write reverts. Investigate whether there's a per-frame refresh from roster into master for player slots specifically. See `memory/project_deathcounter_battle_array_scan.md`.

- [ ] **Code-only: add `broadSearch` param to `search_bytes` bridge action** — `Explorer.SearchBytesInAllMemory(pattern, max, min, max, broadSearch)` already supports it (MemoryExplorer.cs:222); the `search_bytes` case in `CommandWatcher.cs:2400` doesn't pass it through. Add a `broadSearch` field to `CommandRequest`, thread it through the two SearchBytesInAllMemory call sites (2420 + 2421), pin with a unit test confirming broadSearch=true gets forwarded. Unblocks the per-unit-ct hunt below.

- [ ] **Per-unit casting ct hunt — second attempt** — Blocked on the previous item. Once `broadSearch` is exposed on `search_bytes`, retry HP=MaxHp fingerprint hunt for Kenrick's heap struct. See `memory/project_per_unit_ct_hunt_deferred.md`.


### Session 49 — follow-ups (2026-04-20)


- [ ] **Live-verify `kill_enemies` Reraise-clear path against a real Skeleton / Bonesnatch** — Session 50 confirmed `kill_enemies` cleared a Bonesnatch at Siedge Weald (Victory triggered, no revive observed). Still need proof the **Reraise-bit-clear writes specifically** fire (they may be no-ops if the Bonesnatch's status byte[2] didn't have the Reraise bit set). Check by reading battle-array +0x47 before and after `kill_enemies` on a unit that provably has the Reraise status.

- [ ] **Verify `+0x29` as deathCounter with natural KO** — Session 49 found candidate at master-HP-table +0x29 that ticked 3→2 on a `kill_enemies`-KO'd Goblin. The `+0x31 |= 0x20` dead-bit write may have initialized the counter artificially; need natural KO (normal attack) to confirm it's the true crystallize countdown. See `memory/project_deathcounter_offset_0x29.md`.

- [ ] **Verify cast queue at `0x14077D220`** — Session 49 found 3 u32 records with `(u8, 0xB0, 0x00, 0x00)` pattern after queuing Curaja ct=10. Bytes didn't tick across polling — may not be the ct counter, or ct only advances during enemy/ally turns (not player's turn). Next-session approach: queue a spell on Kenrick, end Lloyd's turn (so CT advances), immediately read `0x14077D220`, wait another turn, read again; expect monotonic decrement. See `memory/project_charging_ability_queue.md`.

<!-- "Hunt Zodiac byte via heap-struct scan" — broken down into strategies A-E under §1 Tier 4 "Zodiac byte hunt strategy A-E". Duplicate removed. -->

- [~] **BattleVictory post-banner false-GameOver edge** — Deferred until a real repro surfaces. Current `battleTeam==0` guard handles the known cases (session 49 Kenrick counter-kill). Regression test `DetectScreen_VictoryWithRamzaDying_TeamZeroGuard_ReturnsBattleVictory` pins current behavior. If a team=2 NPC counter-kill scenario gets captured, swap the guard for a dedicated encA/encB condition.


### Session 47 — follow-ups (2026-04-19)

- [ ] **🆕 Wire `AbilityListCursorNavPlanner.Plan` once real-cursor source exists** — Session 55 shipped this pure planner with 12 tests (`AbilityListCursorNavPlanner.Plan(currentIdx, targetIdx, listSize)` returns shorter Down/Up direction with wrap). Wire once the cursor-byte address is available reliably (option 3 from `memory/project_ability_list_cursor_addr.md` — escape-and-re-enter from known idx 0 — would feed currentIdx=0 trivially).


### Session 48 — follow-ups (2026-04-19)

- [ ] **Random-encounter map resolution: FIXED via live-map-id byte — regression test only** — Commit `9f87bfc` swapped `screen.location`-keyed lookups for `0x14077D83C` (u8, current battle map id). Three maps live-verified + survives restart. Reopens only if the byte shifts after a game patch. If the locId-based fallback at `NavigationActions.cs:Try 1/2` ever gets reached, log why so we know when it matters.



### Session 46 — follow-ups (2026-04-19)


- [ ] **Extend SM cursor tracking — BattleMoving grid cursor** — Session 47 shipped CharacterStatus sidebar + BattleAbilities submenu + TavernRumors/TavernErrands via `OnKeyPressedForDetectedScreen`. Still needed: 2D x,y tracked via arrow keys + current camera rotation on BattleMoving. Cursor-rotation math complicates this one vs. the 1D cases already shipped.

- [ ] **Fight→Formation transition settle** — The 3s settle cap increase was reverted (made every menu nav slow). Formation loads after `execute_action Fight` can exceed 3s (observed 5+s). Needs per-action custom settle logic: the Fight action handler should poll until detection sees `BattleFormation` OR 10s elapsed, rather than relying on the generic settle loop. Low priority since auto-placement mostly handles the Fight flow anyway.


### Session 45 — new follow-ups (2026-04-19)


- [ ] **Crystal-sequence states S2/S3 not live-verified** — Session 45 SHIPPED `BattleCrystalReward` (Acquire/Restore) and `BattleAbilityAcquireConfirm` (Yes/No) detection but only walked through them in the INPUT-CAPTURE phase, not with the active detection code. Next crystal pickup: verify ui= renders correctly for each state, and that encA boundary at 5 (S2 vs S3) holds across different ability lists.

- [ ] **encA cross-session stability check** — encA values in crystal/chest states (0, 1, 2, 4, 7) captured this session need re-verification on a fresh boot. If encA is a heap widget-stack byte (likely), values may shuffle across restarts — then our rule's thresholds (encA>=5) break. First restart-load of a crystal or chest should re-dump detection inputs to confirm.

- [ ] **auto_place_units pre-formation buffer** — Crashed twice at Dorter formation session 45 (worked on 3rd try). Helper sleeps 4s then sends 10 keys over 6s; story battles have a longer formation animation that races the Enter sequence. Add 5-8s more sleep OR poll for "all 4 unit portraits populated" widget state before sending Enter. Memory: `feedback_auto_place_crashes_dorter.md`.


### 🔴 State Detection — TOP PRIORITY (consolidated 2026-04-18)

User direction session 44: **refocus on state-related tasks. Bad state detection blocks everything else**. These are the known screen/state-detection bugs, ordered roughly by blast radius. Items cross-reference their detailed entries below.


- [ ] **BattleChoice event catalog — add more entries as encountered** — Session 47 shipped `BattleChoiceEventIds.KnownEventIds` + regression-pin tests. Mandalia Plain event 16 ("Defeat the Brigade" / "Rescue captive") is the only catalogued entry. Detection uses the signal-based path (eventHasChoice + choiceModalFlag at ScreenDetectionLogic.cs:347) which works for any event with those signals, but the catalog is a documentation + regression pin. As new choice events are encountered in live play, add their IDs to the catalog and confirm detection classifies them correctly.

<!-- Moved to Session 31 follow-ups below (has richer detail). Duplicate removed. -->

- [ ] **Cutscene vs BattleDialogue are byte-identical** — 10+ datapoints session 44: rawLoc, battleTeam, acted/moved, eventId range all overlap. No main-module discriminator found. eventId-whitelist approach (as with BattleChoice) is the likely path here too — catalog which eventIds are "cinematic-style" (no battleboard) vs "mid-battle text."

- [ ] **Replace fixed post-key delay with poll-until-stable** — Currently a fixed 350ms sleep in detection fallback. Should poll-until-stable. Large refactor, deferred pending safer repro harness. S29 `KeyDelayClassifier` nav/transition split (200/350ms) is the shipped partial win.

<!-- `activeUnitSummary` on BattleMoving/BattleCasting/BattleAbilities/BattleActing/BattleWaiting live-verify: kept in Session 29 follow-ups below; duplicate removed. `Battle state verification — BattleActing` also removed — covered by the BattleActing live-verify item in Session 33 batch 2 below. -->


### Session 44 — urgent bugs (new)

- [ ] **`scan_move` misreads team classification on Orbonne opening** — Fresh-game Orbonne battle: scan labeled units at (6,5), (5,5), (6,6), (6,4), (5,4), (4,4), (9,2), (5,1) as `[ENEMY]` but the in-game visual shows several of them are PLAYER-side (knights in Ramza's party, Delita, etc.). User corrected: (6,6) is an ALLY, not an enemy. Also: scan labeled the monster-job "Ahriman" at (4,5) as `[PLAYER]` which is also suspicious. Root cause unknown — possibly story battles use team bytes the bridge doesn't recognize, OR the battle unit struct positions are misaligned on this specific battle (similar to the mod-forced battles at Grogh/Dugeura). Blocks autonomous battle play on story-forced combat — I almost attacked an ally because the scan said they were an enemy. **Fix path**: dump unit structs at Orbonne opening, compare the team byte values to known-good random-encounter battles (e.g. Mount Bervenia from earlier), find the value that represents "player-side story unit" vs generic enemy.


### Session 33 batch 2 — deferred (needs live battle / environment I can't verify)


- [ ] **Live-verify `ui=` on BattleActing** — TargetingLabelResolver covers BattleCasting structurally; BattleActing + BattleWaiting never exercised live. Needs one call on each state during a battle, eyeball the label.

- [ ] **Live-verify `ui=` on BattleWaiting** — Same as above; pair-up with BattleActing verification in one session.

- [ ] **Live-verify `ui=` BattlePaused cursor decode** — cursor byte candidates found session 33; decode still awaits live capture during a facing-confirm animation (see memory `project_battle_pause_cursor.md`). Absorbs the `BattlePaused ui decode` bit from the old Session 33 compound TODO.

<!-- Session 33 batch 2 compound TODO "Live-verify execute_action ui= across battle screens" split into the three atomic live-verify items above. Shipped parts (BattleMyTurn/BattleMoving/BattleAbilities/BattleAttacking/BattleStatus + BattleStatusUiResolver) archived to COMPLETED_TODO.md. Deprioritized `_cachedActiveUnitName` race removed — user told us not to pick up without direct ask (2026-04-18). -->

- [ ] **IC remaster deathCounter offset hunt** — PSX had it at ~0x58-0x59 in battle unit struct. Needs live battle with a KO'd unit to find the IC equivalent. Blocks KO/crystallize-aware tactics. Absorbs dupes at former lines 311 ("Find IC remaster deathCounter offset") and 318 ("Read death counter for KO'd units") — same task, closed session 44 pt 8 dedup.


### Session 31 — next-up follow-ups (live-verify pending)


- [ ] **Detection leaks CharacterStatus / CombatSets during battle_wait animations** — Session 31 `session_tail slow 1500` exposed: battle_wait rows report `sourceScreen=CharacterStatus` → `targetScreen=BattleMyTurn` with 15-23 second latencies. The player is IN battle the whole time, not on CharacterStatus. Likely a detection false-positive during facing-confirm or wait-ct animation frames where unit-slot / ui bytes transiently match CharacterStatus patterns. Doesn't break gameplay (final target is correct) but slows down logging diagnostics and could mis-route screen-gated actions. Next repro: `session_tail` during a battle, look for any `*→BattleMyTurn` with non-Battle source.



- [ ] **Live-verify `!weak` / `+absorb` / `~half` / `=null` / `^strengthen` per-tile sigils** — Session 31 shipped `ElementAffinityAnnotator` + `ValidTargetTile.Affinity` + shell render. JSON field populates correctly (confirmed via response-json inspection — Black Goblins show `elementWeak:['Ice']`). Per-tile shell sigil UNTRIGGERED in current save: all available caster abilities are non-elemental (Mettle/Monk/Time Magicks) so the marker never fires. Next repro: a Wizard with Fire + Ice-weak enemy on field, OR a White Mage with Holy + undead enemy. Confirm `<Goblin !weak>` / `<Skeleton +absorb>` style suffixes render. Best party candidates on current save: Kenrick (White Mage, Holy) or Rapha (Skyseer, Holy) vs an undead enemy for `+absorb:['Dark']` / `!weak:['Holy']`. Needs a random-encounter zone with undead (Skeleton/Ghost/Ghoul). Absorbs dupe at former line 96 ("Live-verify !weak / +absorb sigils") — same task, closed session 44 pt 8 dedup.

- [ ] **Live-verify `>BACK` / `>side` arc sigils** — Session 31 shipped `BackstabArcCalculator` + `ValidTargetTile.Arc` + `AttackTileInfo.Arc` + shell render (front omitted, only back/side show). JSON field populates correctly (confirmed: `arc:"front"` on enemy tiles during Ramza scan). Back/side sigils UNTRIGGERED in current save because all attack approaches ended up front-arc relative to east-facing goblins. Next repro: reposition Ramza west of an east-facing enemy (attacker behind target's facing axis) to trigger `>BACK`.

- [ ] **Live-verify LoS `!blocked` sigil** — Session 31 shipped `LineOfSightCalculator` + `ProjectileAbilityClassifier` + wire-up + shell sigil. Code path triggers only on `Attack` (ranged weapon) or `Throw` (Ninja) skillsets. Current save has no active Archer/Gunner/Ninja — Mustadio is Machinist but unequipped; attempts to change Lloyd to Archer failed due to helper bugs. Next repro: any unit with a bow/gun/crossbow/ninja-throw + a battle map with terrain between them and an enemy.

- [~] **Live-verify full 5-field element decode on varied enemies — 3/5 FIELDS CONFIRMED (session 44 2026-04-18)** — Lenalian Plateau random encounter confirmed: **`elementWeak`** (Piscodaemon Lightning, Red Panther Earth), **`elementAbsorb`** (Piscodaemon Water), **`elementHalf`** (Knight Dark). Still unconfirmed: **`elementNull`** (was called `elementCancel` in the old wiki — may need a Lucavi or a unit with elemental nullification gear) and **`elementStrengthen`** (needs a player unit with elemental-strengthen weapon/gear equipped — check the shop for Materia Blade / Faith Rod / elemental staves). All 3 confirmed fields serialize correctly from memory and appear in the `scan_move` output JSON — the pure decode path is validated on varied enemy archetypes.

- [ ] **⚠ UNVERIFIED: `AutoEndTurnAbilities` — Self-Destruct** — shipped session 33 batch 2 (commit `0917e34`) as a hardcoded addition alongside Jump. Needs live repro on a Bomb monster: Self-Destruct should end the caster's turn without a Wait prompt. Wish / Blood Price still NOT in the set — per documentation comments their behavior varies by version; defer until live damage/turn data exists.

- [ ] **Live-verify weather damage modifier** — Session 31 shipped `WeatherDamageModifier` pure table (Rain→Lightning×1.25/Fire×0.75, Snow→Ice×1.25/Fire×0.75, Thunderstorm→Lightning×1.25). NOT yet wired into scan_move because the weather-state memory byte is unknown. Blocked on memory hunt. Validate the formula values AGAINST IC remaster once a rainy/snowy battle can be scanned. Wiki values are PSX-canonical.

- [ ] **Live-verify BattleModalChoice scaffold** — Session 31 shipped `BattleModalChoice` pure helper + 6 tests. NOT wired into detection (needs discriminator memory hunt). Next session find the modal-open byte during a BattleObjectiveChoice or RecruitOffer screen (story battle required — Orbonne Monastery probably has both).


### Session 29 — next-up follow-ups

- [ ] **Fix `battle_move` NOT CONFIRMED false-negative after next live repro** — Session 29 pt.4 added diagnostic logging. Next repro error message includes `lastScreenSeen=X polls=N` for the 8s poll window. Read that log. If `lastScreenSeen=BattleMoving` across all 80 polls, fix is in screen detection (`battleMode` byte likely stays 2 past the animation); add a second signal to distinguish "Move UI open" from "unit has not moved yet". If intermediate states appear in the log, expand the accept-list in `NavigationActions.cs` `MoveGrid`.

- [ ] **⚠ UNVERIFIED: `activeUnitSummary` on BattleMoving / BattleCasting / BattleAbilities / BattleActing / BattleWaiting** — Session 29 pt.1/2 widened the fft.sh compact renderer to match `Battle*` (was `Battle_*`). Confirmed on BattleMyTurn and BattleAttacking during session 29. Still needs a quick sweep — one call on each state during a battle and eyeball the line.

- [ ] **Diagnose `(10,4)` false-positive on Lloyd (Dragoon Mv=5 Jmp=4) at (10,9)** — Session 29 tile-cost rules match the game for 4 of 4 tested unit/position combos (Kenrick×3, Wilham×1). Untested: Lloyd's old FP scenario. Re-run at Siedge Weald with heap Move/Jump reading active and see if (10,4) is still over-reported. If still wrong, see `memory/project_bfs_tile_cost_rules.md` for what we've tried.

- [ ] **Rescue `cursor_walk` probe reliability (currently 5 of 20)** — `cursor_walk` counts `0x04 → 0x05` transitions in `0x140DDF000..0x140DE8000` between pre/post snapshots — only catches 5 of 20 valid tiles for Lloyd at Siedge Weald. Fix ideas: (a) widen range to cover mirror region `0x140F9xxxx`, (b) also count `0x00 → 0x05` and `0x01 → 0x05` transitions, (c) baseline "cursor on known valid tile" and compare the SET of 0x05 bytes that appear, not the count. Blocks automated BFS regression fixture building.

- [ ] **Find a byte (or compound signal) that encodes the game's valid-tile count** — Session 28 proved `0x142FEA008` is NOT the count. `LogBfsTileCountMismatch` call sites in `CommandWatcher.cs` are commented out waiting for a real signal. Plan: user visual count for 3-5 unit/map combos → module snapshot before/after Move mode entry → scan diff for any byte/u16 whose post-entry value matches the real count on ALL combos. `MoveTileCountValidator` + `DetectedScreen.BfsMismatchWarning` + shell `⚠` rendering are all in place, waiting.


<!-- Session 27 Zodiac heap-struct hunt broken down into atomic strategies A-E under §1 Tier 4 "Zodiac byte hunt strategy A/B/C/D/E". -->



### Session 20 — state detection + EqA resolver


- [ ] **BattleSequence detection: find memory discriminator** — Session 21 built full scaffolding (whitelist of 8 locations, NavigationPaths, SM sync, LocationSaveLogic) but detection DISABLED because BattleSequence minimap is byte-identical to WorldMap at those locations across all 29 detection inputs. Whitelist approach false-triggers on fresh boot/save load at sequence locations. Scaffolding ready in ScreenDetectionLogic.cs (commented out rule + `BattleSequenceLocations` HashSet). Next step: heap diff scan while ON the minimap vs WorldMap at same location to find a dedicated flag. Locations: Riovanes(1), Lionel(3), Limberry(4), Zeltennia(5), Ziekden(15), Mullonde(16), Orbonne(18), FortBesselat(21).





<!-- Moved to consolidated State Detection block above (§0); duplicate removed. Session 29 nav/transition KEY_DELAY split already shipped via `KeyDelayClassifier` (200ms nav / 350ms transition). -->



## 1. Battle Execution (P0, BLOCKING)

Basic turn cycle works: `screen` → `battle_attack` → `battle_wait`. First battle WON autonomously.


### NEXT 5 — Do these first (identified 2026-04-12 battle testing)


### Tier 0 — Critical (BFS broken, need game-truth tiles)

- [~] **Read valid movement tiles from game memory** [Movement] — **Session 28 conclusion: the game does NOT persistently store a per-unit valid-tile bitmap.** Valid tiles are computed on-the-fly each frame from static map topology + unit stats; only the count (or something we mistook for a count) is cached. Conclusively ruled out across hundreds of scans. Remaining path for ground-truth extraction is `cursor_walk` (see Session 28 TODO §0) + manual visual counting. **Full investigation in `memory/project_move_bitmap_hunt_s28.md`** — next session should read that before doing any more memory scanning in this region. Rather than chase the bitmap further, the pragmatic path is to **fix the BFS algorithm directly** against canonical PSX movement rules (slope, height, depth, jump, movement abilities). See the sibling "Fix MovementBfs" entry in §0 for concrete false-positive tile data.





- [ ] **Cone abilities — Abyssal Blade** [AoE] — Deferred. 3-row falling-damage cone with stepped power bands. Low value (only 1 ability uses this shape) and requires map-relative orientation. Skip for now.




### Tier 2 — Core tactical depth



### Tier 2.5 — Navigation completeness

- [ ] **Chocobo riding: detect mounted state** [Combat] — Find memory flag that indicates a unit is currently riding a chocobo. Possibly a status byte or a separate "mount" field in the battle unit struct.

- [ ] **Chocobo riding: adjust Move stat when mounted** [Combat] — Mounted units use the chocobo's Move, not their own. Override `Move` in scan_move when the mount flag is set.

- [ ] **Chocobo riding: surface chocobo-specific actions** [Combat] — Mounted units have different action menu (Choco Attack, Choco Cure, etc.). Populate ability list accordingly.




### Tier 3 — Robustness

Turn-state recovery, edge case handlers, multi-unit battle reliability.


- [ ] **battle_ability first-scan null/null for secondary skillset** [Execution] — Primary detection works (Martial Arts secondaryIdx=9 for Lloyd verified); auto-scan catches misses on retry; all-skillsets fallback works. Remaining: first scan sometimes returns null/null before auto-scan fires — investigate race and eliminate the initial miss.


- [ ] **LoS option A: read game's projected hit% from memory during targeting** [Abilities] — **Blocked by session 30 finding** that hit% isn't findable via flat-memory AoB search — see `memory/project_damage_preview_hunt_s30.md`. LoS-via-memory now depends on the same widget-introspection or formula-compute path that damage preview needs. Prefer LoS option B (compute from map height data) until that path lands.

- [~] **LoS option B: compute LoS from map height data** [Abilities] — Session 31: shipped. `LineOfSightCalculator` (pure, DDA walk + linear altitude interp, 13 tests) + `ProjectileAbilityClassifier` (pure rule: ranged Attack + Ninja Throw qualify, spells/summons don't, 9 tests) + wire-up in `NavigationActions.AnnotateTile` populating `ValidTargetTile.LosBlocked`. Shell renders `!blocked` sigil. Needs live verify on a bow/gun/crossbow unit with terrain blocking a shot.

- [ ] **LoS option C: enter targeting, check if game rejects tile, cancel if blocked** [Abilities] — Brute-force fallback. Slow but reliable. Use only as last resort if A and B both fail.


- [ ] **Active unit name/job stale across battles** [State] — After restarting a battle with different equipment/jobs, the name/job display doesn't refresh between battles.


- [ ] **battle_move reports NOT CONFIRMED for valid moves** [Movement] — Navigation succeeds but F key confirmation doesn't transition. Timeout increased from 5s to 8s for long-distance moves.


- [ ] **Detect disabled/grayed action menu items** [Movement] — Need to find a memory flag or detect from cursor behavior.


- [ ] **Live-test `battle_retry` from GameOver screen** [Execution] — Code path exists, GameOver detection fixed. Needs in-game verification after losing a battle.



- [ ] **Find IC remaster chargingAbility + chargeCt bytes** [State] — Units charging a spell show in the Combat Timeline. Find which ability ID is queued and remaining CT for each charging unit. Needed to avoid wasted Silence/Stop attempts. Absorbs dupe at former line 314 ("Detect charging/casting units") — same concept, closed session 44 pt 8 dedup.





### Tier 4 — Known hard problems

- [ ] **Unit names — enemies** [Identity] — Enemy display names not found in memory. May need NXD table access or glyph-based lookup.


- [ ] **Zodiac byte hunt strategy A: nibble-packed encoding** [Identity] — Story-character zodiacs shipped S19 via `ZodiacData.cs` nameId table; generics/Ramza return null. Prior byte-aligned hunt (offsets 0x00-0x100, 4 anchors: Agrias=Cancer / Mustadio=Libra / Orlandeau=Scorpio / Cloud=Aquarius) found nothing. Retry: search half-byte nibbles (2 zodiacs per byte) instead of full bytes.

- [ ] **Zodiac byte hunt strategy B: parallel array outside 0x258 slot stride** [Identity] — Prior hunt assumed zodiac sits inside the 0x258 roster slot; may instead live in a separate parallel array keyed by unit index. Dump heap near roster base for byte sequences matching the 4-anchor zodiac pattern.

- [ ] **Zodiac byte hunt strategy C: non-zero-indexed ordering** [Identity] — Prior hunt assumed Sign enum matches in-game byte values directly. Retry with offset variants (+1, +3, *2) on the anchor pattern before declaring a miss.

- [ ] **Zodiac byte hunt strategy D: heap-struct hunt via per-unit widget** [Identity] — Session 27 approach: dump HoveredUnitArray struct beyond +0x37 (HP/MP decoded range); zodiac may live at a higher offset in the per-hover widget. Requires 2 different-zodiac party members + CharacterStatus open for each to diff. See `memory/project_zodiac_heap_hunt_deferred.md`.

- [ ] **Zodiac byte hunt strategy E: reverse from damage math** [Identity] — Set up a zodiac-opposite attacker/target pair (e.g. Aries attacker + Libra defender), read the damage modifier applied in-game, back out both zodiacs from the multiplier. Cross-validate on a second known-pairing. See `memory/project_zodiac_heap_hunt_deferred.md`.

- [ ] **Zodiac compatibility damage multiplier** [Combat] — Once zodiac is readable for every unit, wire `ZodiacData.GetOpposite` + Good/Bad compatibility tables into damage preview calculations. Multipliers per wiki: Best 1.5x, Good 1.25x, Bad 0.75x, Worst 0.5x. Requires projected-damage preview work (separate task) to ship first.



### Tier 5 — Speed optimization

- [ ] **`execute_turn`: accumulate per-step HP deltas** [Execution] — Today `ExecuteTurn` (CommandWatcher.cs:3818) returns only the final sub-step's `PostAction`. Pure-extract an `ExecuteTurnResultAccumulator` that merges HP deltas across move / ability / wait sub-steps. Pin via unit tests on the accumulator before wiring.

- [ ] **`execute_turn`: accumulate per-step movement (pre/post x,y)** [Execution] — Add `PreMoveX/Y` + `PostMoveX/Y` to the accumulated `PostAction`. Source: `_postMoveX/_postMoveY` already captured at CommandWatcher.cs:3616. Same accumulator as the HP-delta item.

- [ ] **`execute_turn`: accumulate per-step kills** [Execution] — If any sub-step kills a unit, include the killed unit's name/team/job in the accumulated summary. Requires diffing `scan_units` before vs after the final sub-step.

<!-- Old compound "execute_turn return full post-turn state" split into the three atomic items above. Session 47 TurnPlan + ExecuteTurn orchestrator already shipped; the three remaining pieces are pure data aggregation. -->



---


## 2. Story Progression (P0, BLOCKING)

- [ ] **Orbonne Monastery (loc 18) encounter: capture detection inputs** — Loc 18 has a different encounter screen than regular battlegrounds. Step 1: travel to Orbonne, capture full detection inputs (rawLocation, encA/encB, battleMode, paused, submenuFlag, eventId, battleTeam) at each transition (WorldMap→Encounter→Formation→BattleMyTurn). Save snapshot JSON to memory note.

- [ ] **Orbonne Monastery (loc 18) encounter: add detection rule** — Based on inputs from the capture step, add a discriminator rule to `ScreenDetectionLogic.cs`. Pin with a regression test in `ScreenDetectionTests.cs`.

- [ ] **Orbonne Monastery (loc 18) encounter: wire ValidPaths** — Once detected, add `NavigationPaths` entries so `execute_action` can fire the correct Fight/Flee/Advance actions.





---


## 8. Speed Optimizations (P1)

- [ ] **Auto-scan on Battle_MyTurn** — Include unit scan results in response automatically


- [ ] **Background position tracking** — Poll positions during enemy turns so they're fresh when it's our turn


- [ ] **Pre-compute actionable data** — Distances, valid adjacent tiles, attack range in responses


- [ ] **Latency measurement** — Log round-trip times, flag >2s actions



---


## 9. Battle — Advanced (P2)


### Error Recovery
- [ ] Detect failed move/attack — retry or cancel


- [ ] Handle unexpected screen transitions during turn execution


- [ ] **Counter attack KO** — Active unit KO'd by reaction ability after attacking. Need to detect and recover.




### Advanced Targeting
- [ ] Line AoE abilities


- [ ] Self-centered AoE abilities


- [ ] Multi-hit abilities (random targeting)


- [ ] Terrain-aware Geomancy (surface type determines ability)



---


## 12. Known Issues / Blockers



### Missing Screen States
<!-- Battle_Cutscene: REMOVED (user decision 2026-04-18 session 44).
     Simpler two-state model: Cutscene (pre/post-battle, out of combat) vs
     BattleDialogue (mid-combat scripted text). Do not re-introduce a
     third state for "mid-battle cinematic" — treat those as BattleDialogue. -->




<!--
`Battle_Objective_Choice` — superseded by the shipped `BattleChoice` state. Detection wires `eventHasChoice` (from .mes 0xFB parse) + runtime `choiceModalFlag` (ScreenDetectionLogic.cs:344-378). Remaining work is event-catalog entries (see §0 "BattleChoice event catalog") and per-option cursor decode (tracked separately below).
-->

- [ ] **BattleChoice: per-option cursor decode** — `BattleChoice` state exists and ValidPaths populate Yes/No, but `ui=<option text>` doesn't surface the currently-highlighted option text. Needs the same FString problem as shop items — the option glyph table isn't located yet. Without it, Claude can't see WHICH option the cursor is on until it Enters. Priority HIGH because wrong choice can fail a battle objective.

- [ ] **`Recruit_Offer` modal** — end-of-battle: a defeated/befriended enemy offers to join your party (e.g. "Orlandeau wants to join your party"). Accept adds them to the roster; decline loses them forever (story-character one-shot). Likely uses the same BattleChoice modal shape; confirm during the next recruitable enemy encounter (Orlandeau mid-game earliest). New state: `Recruit_Offer` with `ui=Accept` / `ui=Decline`, ValidPaths `Confirm` / `Cancel` / `CursorUp/Down`. HIGH priority — wrong choice loses a unit permanently.




### Screen Detection Edge Cases
- Battle_Paused false positives: pauseFlag=1 stale after facing confirmation. **Session 31 investigation**: the bare `paused == 1 → BattlePaused` fallthrough at `ScreenDetectionLogic.cs:374` is too loose — any stale pause byte catches. Candidate tighten: also require `battleMode == 0 && submenuFlag == 0`, but UNTESTED because reproducing needs a live facing-confirmation screen (happens after `battle_wait` commits). Next repro: capture detection inputs DURING facing confirm (before Enter advances), compare to real pause.
- Menu cursor unreliable after animations


### Screen Detection Rewrite (P0) — from 2026-04-14 audit

Context: 45-sample audit of `ScreenDetectionLogic.Detect` found detection is the root cause of most UI nav bugs. Root causes list: `menuCursor` overloaded per context; `battleMode` encodes cursor-tile-class not screen submode; `encA/encB` are noise counters; `gameOverFlag` sticky process-lifetime; `rawLocation==255 → TitleScreen` preempts world-side screens; two TitleScreen variants (fresh process vs post-GameOver) with different fingerprints. Full data: `detection_audit.md` + `BATTLE_MEMORY_MAP.md` §12. Atomic fix tasks below.

- [ ] **Remove `encA/encB` from BattleVictory discriminators** — 3 BattleVictory rules in ScreenDetectionLogic.cs depend on `encA==255 && encB==255` (lines ~487, ~545). Replace with stable signals (`paused`, `submenuFlag`, `acted/moved` + `atNamedLocation` combos). Need live captures showing the three rules' fingerprints WITHOUT the encA/encB columns before rewriting. Guard against regression via existing `DetectScreen_Victory_*` tests.

- [ ] **Remove `encA/encB` from BattleDesertion discriminators** — Desertion has no explicit encA/encB dep today (confirmed ScreenDetectionLogic.cs:506, :511), but shares the post-battle-stale fingerprint with Victory so changes to the Victory rules may cascade. Re-audit both after Victory rules rewrite.

- [ ] **Remove `encA/encB` from EncounterDialog discriminator** — Audit `EncounterDialog` rule for noise-counter dependence. If present, swap to stable signals.

- [ ] **Add `Battle_ChooseLocation` discriminator** — requires location-type annotation: which location IDs are multi-battle campaign grounds (Fort Besselat, etc.) vs villages. Add to `project_location_ids_verified.md` memory note + data table in ScreenDetectionLogic.

- [ ] **Fix `rawLocation==255 → TitleScreen` preemption** — rule at ScreenDetectionLogic.cs preempts valid world-side screens post-GameOver or post-battle when stale location byte reads 255. Current post-GameOver TitleScreen rule (lines 561-566) narrows via gameOverFlag+submenuFlag+menuCursor; ensure the preemption is ONLY that narrow rule, not a blanket fallthrough.




### Coordinate System
- Grid coords and world coords are different systems
- Camera auto-rotates when entering Move mode or Attack targeting — always re-read rotation


### Bugs Found 2026-04-12

<!-- "Ability list navigation: wire AbilityCursorDeltaPlanner into scroll loop" — shipped. Planner wired at NavigationActions.cs:1127, trust-rules gate retry math. Session-55 reverts did not touch this. -->

- [ ] **Detect rain/weather on battle maps** — Rain boosts Lightning spells by 25%.


- [ ] **Post-battle memory values stuck at 255 after auto-battle** — All memory addresses stayed at 255/0xFFFFFFFF permanently. May require game restart.


- [ ] **Fix stale location address (255) after restart breaking battle-map auto-detect** — Location ID lookup + random encounter maps + fingerprint fallback already shipped. Remaining bug: after game restart, `0x14077D208` reads 255 which defaults to the wrong map. Need a fallback read or forced re-read on first post-restart scan.




### Bugs Found 2026-04-12 Session 2


- [~] **Static array at 0x140893C00 is stale mid-turn** [State] — **session 30 scope audit** confirmed this bug no longer has an active trigger: (a) damage-preview code was removed after the statBase-62/-96 hunt concluded (see memory/project_damage_preview_hunt_s30.md), taking the main attacker-MaxHP-post-move lookup with it; (b) `ReadLiveHp` already reads readonly-region copies at 0x141xxx/0x15Axxx which update in real time; (c) `CollectPositions` only runs at turn boundaries (scan blocked in BattleActing/Casting/Attacking/Enemies/Allies turns) where the static array IS fresh; (d) surviving BattleAttack target-HP reads happen on a unit that hasn't moved, so those are fresh too. Reopen only if a new scenario surfaces stale-HP symptoms.


- [~] **Damage/hit% preview during targeting** [State] — **AoB-search path ruled out, session 30 (2026-04-17).** See `memory/project_damage_preview_hunt_s30.md`. Live-verified: statBase-62 / statBase-96 offsets relative to both (attacker MaxHP+MaxHP) and (target HP+MaxHP) patterns contain only unit base stats in all 10 copies found across 0x140xxx / 0x141xxx / 0x15Axxx / 0x4166xxx regions. `ReadDamagePreview` now returns (0,0) permanently; `response.Info` on `battle_attack` uses post-attack delta-HP as ground truth instead. If preview is ever needed, pivot to: (a) UE4 widget vtable walk from a stable static pointer to the combat HUD, (b) DLL detour on the preview-render callback, or (c) formula compute (blocks on zodiac + PSX-vs-IC formula verification).


- [ ] **Screen detection shows Cutscene during ability targeting** [State] — While in targeting mode for Aurablast (selecting a target tile), screen detection reports "Cutscene" instead of "Battle_Attacking" or "Battle_Casting". This causes key commands to fail because they check screen state. Observed 2026-04-13.


- [ ] **Failed battle_move reports ui=Abilities instead of ui=Move** [State] — After battle_move fails validation, the response shows ui=Abilities but the in-game cursor is still on Move. The scan that runs before the move might be changing the reported ui state. Observed 2026-04-13.


<!--
Superseded items (removed 2026-04-22 S56 sweep):
- "battle_ability selects wrong ability from list" — this is the SAME BUG as the S55-BLOCKING `battle_ability` off-by-one at the top of §0. Duplicate removed; live fix tracked there.
- "Abilities submenu remembers cursor position" — shipped as the force-to-Attack reset in `battle_attack` at NavigationActions.cs:670-697. Submenu cursor index is measured via `FindSkillsetIndex(uiSubmenu, submenuItems)` and then Up-wrapped to 0 before Enter.
- `battle_ability` response says "Used" for cast-time abilities — shipped. Verb switches via `loc.castSpeed > 0 ? "Queued" : "Used"` at NavigationActions.cs:1167; `ctSuffix` surfaces `(ct=N)` in response.Info.
-->





---


## 13. Battle Statistics & Lifetime Tracking


### Per-battle stats
- [ ] Turns to complete, per-unit damage/healing/kills/KOs, MVP selection




### Lifetime stats (persisted to JSON across sessions)
- [ ] Per-unit career totals, ability usage breakdown, session aggregates




### Display
- [ ] Post-battle summary, `stats` command, milestone announcements


---


## Low Priority / Deferred


- [ ] **Re-enable strict mode** [Execution] — Disabled. Re-enable once all gameplay commands are tested.

- [ ] **Remove `gameOverFlag==0` requirement from post-battle rules** — treat as sticky, use other signals. Deferred 2026-04-17 because reproducing requires losing a real battle to trigger GameOver — not cheap to set up. Re-prioritize once we're running battles regularly and this misdetection actually blocks a session (Cutscene→LoadGame collision after GameOver is the main documented symptom).

- [ ] **Re-enable Ctrl fast-forward during enemy turns** [Execution] — Tested both continuous hold and pulse approaches. Neither visibly sped up animations. Low priority.


## Sources & References

- [fft-map-json](https://github.com/rainbowbismuth/fft-map-json) — Pre-parsed map terrain data (122 MAP JSON files)
- [FFT Move-Find Item Guide by FFBeowulf](https://gamefaqs.gamespot.com/ps/197339-final-fantasy-tactics/faqs/32346) — ASCII height maps for MAP identification
- [FFHacktics Wiki](https://ffhacktics.com/wiki/) — PSX memory maps, terrain format, scenario tables


---


## Completed — Archive

Completed `[x]` items live in [COMPLETED_TODO.md](COMPLETED_TODO.md). During the session-end `/handoff`, move newly-completed items from here into that file. Partial `[~]` items stay in this file in their original section.
