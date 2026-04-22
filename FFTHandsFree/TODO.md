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


### Session 55 — ability nav resolver (2026-04-22)

- [ ] **🔴 BLOCKING — `battle_ability` picks the wrong ability** — Pre-S55 blind reset code (`Up×(listSize+1)` followed by `Down×index`) is back in place after S55 reverted the AOB experiment. The Up math is wrong by 1 (Up at idx 0 wraps to last, so listSize+1 lands at last). Symptom: requesting Haste casts Meteor; requesting Cure opens Attack-targeting. **Recommended path (per `memory/project_ability_list_cursor_addr.md` "What to try NEXT"):** option 3 — escape fully out (`Escape Escape`) before each ability nav so submenu+list both reset to known idx 0. Cost ~500ms vs flaky. Code site: `NavigationActions.cs:1093` (`int resetUps = listSize + 1`).

- [ ] **AOB resolver is a research dead-end without pointer chains** — S55 spent ~2h on `<listSize_u64> + 0x1407FC6D8 vtable` AOB; worked once, then failed because the cursor's offset within the widget shifts between widget allocations (sometimes +0x10, sometimes pushed back by a second-vtable insertion). Reverted commits `7def3c2`, `892f979`, `b4d7d98`. **Don't re-attempt without first solving** the structural-offset instability — see memory note for full failure analysis. Pointer-chain (Cheat Engine pointer scan) is the only path that survives widget reallocation.


### Session 52 — scan_diff identity + per-unit ct hunt (2026-04-20)

- [ ] **kill_one player persistence regression** — Session 52 found `kill_one Wilham` wrote HP=0 + dead-bit to master HP slot `0x14184FEC0` but after a turn cycle Wilham showed HP=477 again. Session-49 docs say master is authoritative but for PLAYERS the write reverts. Investigate whether there's a per-frame refresh from roster into master for player slots specifically. See `memory/project_deathcounter_battle_array_scan.md`.

- [ ] **Per-unit casting ct hunt — second attempt** — Session 52 deferred because `search_bytes` doesn't expose `broadSearch`. Fix: add `broadSearch` param to the bridge action, retry HP=MaxHp fingerprint hunt for Kenrick's heap struct. See `memory/project_per_unit_ct_hunt_deferred.md`.


### Session 49 — follow-ups (2026-04-20)


- [ ] **Live-verify `kill_enemies` Reraise-clear path against a real Skeleton / Bonesnatch** — Session 50 confirmed `kill_enemies` cleared a Bonesnatch at Siedge Weald (Victory triggered, no revive observed). Still need proof the **Reraise-bit-clear writes specifically** fire (they may be no-ops if the Bonesnatch's status byte[2] didn't have the Reraise bit set). Check by reading battle-array +0x47 before and after `kill_enemies` on a unit that provably has the Reraise status.

- [ ] **Verify `+0x29` as deathCounter with natural KO** — Session 49 found candidate at master-HP-table +0x29 that ticked 3→2 on a `kill_enemies`-KO'd Goblin. The `+0x31 |= 0x20` dead-bit write may have initialized the counter artificially; need natural KO (normal attack) to confirm it's the true crystallize countdown. See `memory/project_deathcounter_offset_0x29.md`.

- [ ] **Verify cast queue at `0x14077D220`** — Session 49 found 3 u32 records with `(u8, 0xB0, 0x00, 0x00)` pattern after queuing Curaja ct=10. Bytes didn't tick across polling — may not be the ct counter, or ct only advances during enemy/ally turns (not player's turn). Next-session approach: queue a spell on Kenrick, end Lloyd's turn (so CT advances), immediately read `0x14077D220`, wait another turn, read again; expect monotonic decrement. See `memory/project_charging_ability_queue.md`.

- [ ] **Hunt Zodiac byte via heap-struct scan** — needs 2 known-different-zodiac party members loaded. Open CharacterStatus for unit A (read zodiac from UI), snapshot heap, switch to unit B, note zodiac, snapshot heap, diff for bytes that went zodiacA → zodiacB. Cross-validate on a third unit. See `memory/project_zodiac_heap_hunt_deferred.md`.

- [~] **BattleVictory post-banner false-GameOver edge** — Deferred until a real repro surfaces. Current `battleTeam==0` guard handles the known cases (session 49 Kenrick counter-kill). Regression test `DetectScreen_VictoryWithRamzaDying_TeamZeroGuard_ReturnsBattleVictory` pins current behavior. If a team=2 NPC counter-kill scenario gets captured, swap the guard for a dedicated encA/encB condition.


### Session 47 — follow-ups (2026-04-19)

- [ ] **Wire `AbilityCursorDeltaPlanner.Decide` into the ability-list scroll loop** — Pure planner shipped session 47, **was wired then reverted in session 55** along with the rest of the broken cursor-resolver work. Code currently calls blind Up×N + Down×index (off-by-one bug — see "🔴 BLOCKING" item above). To re-attempt: wire AbilityCursorDeltaPlanner once a reliable real-cursor source exists (today's blind-reset has nothing to feed the planner).
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

- [ ] **Detection leaks CharacterStatus / CombatSets during battle_wait animations** — session_tail shows `CharacterStatus → BattleMyTurn` transitions with 15-23s latency during battle_wait. False-positive on facing-confirm animations.

- [ ] **Cutscene vs BattleDialogue are byte-identical** — 10+ datapoints session 44: rawLoc, battleTeam, acted/moved, eventId range all overlap. No main-module discriminator found. eventId-whitelist approach (as with BattleChoice) is the likely path here too — catalog which eventIds are "cinematic-style" (no battleboard) vs "mid-battle text."

- [ ] **Replace fixed post-key delay with poll-until-stable** — Currently a fixed 350ms sleep in detection fallback. Should poll-until-stable. Large refactor, deferred pending safer repro harness.

- [ ] **⚠ UNVERIFIED: `activeUnitSummary` on BattleMoving/BattleCasting/BattleAbilities/BattleActing/BattleWaiting** — Shell compact-render match widened session 29. Confirmed on MyTurn/Attacking. Other states need quick eye-check during battle.

- [ ] **Battle state verification — BattleActing** — Session 45 live-verified BattleAlliesTurn at Zeklaus (Cornell as guest). BattleActing remains unverified — transient state, hard to catch mid-animation.


### Session 44 — urgent bugs (new)

- [ ] **`scan_move` misreads team classification on Orbonne opening** — Fresh-game Orbonne battle: scan labeled units at (6,5), (5,5), (6,6), (6,4), (5,4), (4,4), (9,2), (5,1) as `[ENEMY]` but the in-game visual shows several of them are PLAYER-side (knights in Ramza's party, Delita, etc.). User corrected: (6,6) is an ALLY, not an enemy. Also: scan labeled the monster-job "Ahriman" at (4,5) as `[PLAYER]` which is also suspicious. Root cause unknown — possibly story battles use team bytes the bridge doesn't recognize, OR the battle unit struct positions are misaligned on this specific battle (similar to the mod-forced battles at Grogh/Dugeura). Blocks autonomous battle play on story-forced combat — I almost attacked an ally because the scan said they were an enemy. **Fix path**: dump unit structs at Orbonne opening, compare the team byte values to known-good random-encounter battles (e.g. Mount Bervenia from earlier), find the value that represents "player-side story unit" vs generic enemy.


### Session 33 batch 2 — deferred (needs live battle / environment I can't verify)


- [~] **Live-verify `execute_action` responses include `ui=` field across battle screens — MOSTLY DONE (session 44 parts 1+5)** — Live-tested in a Mount Bervenia random encounter. Findings: **ui POPULATES** on `BattleMyTurn` ("Move"/"Abilities"/"Status"), `BattleMoving` (tile "(8,7)"), `BattleAbilities` ("Attack"), `BattleAttacking` ("Attack" via tracker, or "(x,y)" via new cursor fallback). **`BattleStatus` fix shipped + root-cause bug found + fixed**: A hidden "EqA-promote" block at CommandWatcher:6190 was unconditionally renaming `screen.Name` from `BattleStatus` → `EquipmentAndAbilities` whenever the mirror matched the active unit's equipment — stripping the battle context. Fix: exclude `BattleStatus` from promotion. NEW pure-class `BattleStatusUiResolver` + 2 tests; `screen.UI` set from `_cachedActiveUnitName`. Post-fix log confirmed `ui='Kenrick'` set correctly inside CommandWatcher. **Remaining for next session**: BattlePaused ui decode (cursor byte CANDIDATES found — see next line). **BattleActing / BattleCasting / BattleWaiting** not exercised this session — TargetingLabelResolver change covers BattleCasting structurally. **Deprioritized (user 2026-04-18)**: the `_cachedActiveUnitName` cache-revert race between `execute_action Status` and the subsequent `screen` call — compact shell rendering already shows "Kenrick" via ASUM field so user-visible behavior is fine; do not pick up again without a direct user ask.

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


### Session 27 — next-up follow-ups


- [ ] **Zodiac: try heap-struct hunt (the 0x258 slot is confirmed empty)** — session 27 ruled out the static roster slot across 9 encodings (`memory/project_zodiac_not_in_slot.md`). Three productive next attempts documented: (a) oscillation diff while cycling PartyMenu sort order (if a "sort by zodiac" option exists), (b) reverse from battle damage math — set up a zodiac-opposite attacker/target pair, read damage modifier to back out both zodiacs, (c) dump HoveredUnitArray struct beyond +0x37 (currently we only decode HP/MP); zodiac may live in the per-hover widget at a higher offset.


### Session 20 — state detection + EqA resolver


- [ ] **BattleSequence detection: find memory discriminator** — Session 21 built full scaffolding (whitelist of 8 locations, NavigationPaths, SM sync, LocationSaveLogic) but detection DISABLED because BattleSequence minimap is byte-identical to WorldMap at those locations across all 29 detection inputs. Whitelist approach false-triggers on fresh boot/save load at sequence locations. Scaffolding ready in ScreenDetectionLogic.cs (commented out rule + `BattleSequenceLocations` HashSet). Next step: heap diff scan while ON the minimap vs WorldMap at same location to find a dedicated flag. Locations: Riovanes(1), Lionel(3), Limberry(4), Zeltennia(5), Ziekden(15), Mullonde(16), Orbonne(18), FortBesselat(21).


- [ ] **New state: BattleChoice — mid-battle objective choice screen** — Some battles pause and present 2 options (e.g. "We must press on, to battle" vs "Protect him at all costs"). Selecting an option changes the battle objective (e.g. from "defeat all" to "protect X"). Needs memory investigation to find a discriminator byte. Likely paused=1 with a unique submenu/menuCursor combo.

- [ ] **BattleVictory/BattleDesertion misdetect as BattlePaused** — Session 21 at Orbonne Monastery: slot0=0x67 (not 255) during Victory and Desertion screens. `unitSlotsPopulated` (slot0==255) is false, so `postBattle` and `postBattlePausedState` both fail, and the rules fall through to BattlePaused. Fix: relax the Victory/Desertion rules to not require unitSlotsPopulated — use `battleModeActive && actedOrMoved && battleMode == 0` instead. Inputs captured: party=1, ui=1, slot0=0x67, slot9=0xFFFFFFFF, battleMode=0, paused=1, submenuFlag=1, actedOrMoved=true, eventId=303.



- [ ] **Replace fixed post-key delay with poll-until-stable** — Currently a fixed 350ms sleep in the detection fallback path. Replace with: read state, wait 50ms, read again, if identical return, else keep polling up to 500ms. **Session 24 note:** attempted but punted — timing refactors are hard to validate without a safe repro harness, and an unrelated crash during chain-nav testing showed how fragile the current timing sandwich is. Needs a way to measure "did a key drop?" without firing more keys. Consider wiring up the `[i=N, +Nms]` timing log into a per-call verify-before-advance pattern before retry. Alternative cheaper win: split `KEY_DELAY` into nav (200ms) vs transition (350ms+) per TODO #82.



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


- [ ] **Zodiac byte memory hunt for generics/Ramza** [Identity] — Story-character zodiacs shipped session 19 via hardcoded nameId table (`ZodiacData.cs`, commit 1674bb6). Generics and Ramza return null. Hunt attempted offsets 0x00-0x100 with 4 anchor points (Agrias=Cancer, Mustadio=Libra, Orlandeau=Scorpio, Cloud=Aquarius); no match found. Retry strategies: (a) nibble-packed encoding — search half-bytes instead of bytes, (b) outside the 0x258 slot stride — try a parallel array, (c) non-zero-indexed ordering — try +1, +3, *2 variants.

- [ ] **Zodiac compatibility damage multiplier** [Combat] — Once zodiac is readable for every unit, wire `ZodiacData.GetOpposite` + Good/Bad compatibility tables into damage preview calculations. Multipliers per wiki: Best 1.5x, Good 1.25x, Bad 0.75x, Worst 0.5x. Requires projected-damage preview work (separate task) to ship first.



### Tier 5 — Speed optimization

- [ ] **`execute_turn` — return full post-turn state** [Execution] — Orchestrator + sub-action dispatch shipped session 47 (TurnPlan + ExecuteTurn). Remaining: capture per-step HP deltas + movement + kills, roll into a bundled `PostAction` summary on the final response. Currently only the last step's PostAction is returned.



---


## 2. Story Progression (P0, BLOCKING)

- [ ] **Orbonne Monastery story encounter** — Loc 18 has a different encounter screen. Need to detect and handle it.





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




- [ ] **`Battle_Objective_Choice`** [P0 — gameplay-affecting] — some story battles open with a pre-battle dialogue that forks the win condition. Examples recalled from prior playthroughs: "We must save Agrias, protect her at all cost" vs. "Focusing on defeating all enemies is priority". Picking the first changes the objective to `Protect Agrias — battle ends if she's KO'd`; picking the second leaves the standard `defeat all enemies` objective. New state distinct from `Battle_Dialogue` (which is advance-only): `Battle_Objective_Choice` with two Y/N-style options, `ui=<option A text>` / `ui=<option B text>` based on cursor. ValidPaths: `Confirm` (Enter), `CursorUp/Down` (or Left/Right — verify live). Memory scan needed: (a) discriminator for this modal vs. regular `Battle_Dialogue`, (b) cursor index, (c) option text scrape (same FString problem as shop items). Priority HIGH because picking blindly can permanently fail the battle — Claude needs to SEE the options and decide.


- [ ] **`Recruit_Offer` modal** — end-of-battle: a defeated/befriended enemy offers to join your party (e.g. "Orlandeau wants to join your party"). Accept adds them to the roster; decline loses them forever (story-character one-shot). Possibly uses the same detection as `Battle_Objective_Choice` if both are driven by the same underlying modal system — check during scanning. New state: `Recruit_Offer` with `ui=Accept` / `ui=Decline`, ValidPaths `Confirm` / `Cancel` / `CursorUp/Down`. Also HIGH priority: wrong choice loses a unit permanently.




### Screen Detection Edge Cases
- Battle_Paused false positives: pauseFlag=1 stale after facing confirmation. **Session 31 investigation**: the bare `paused == 1 → BattlePaused` fallthrough at `ScreenDetectionLogic.cs:374` is too loose — any stale pause byte catches. Candidate tighten: also require `battleMode == 0 && submenuFlag == 0`, but UNTESTED because reproducing needs a live facing-confirmation screen (happens after `battle_wait` commits). Next repro: capture detection inputs DURING facing confirm (before Enter advances), compare to real pause.
- Menu cursor unreliable after animations


### Screen Detection Rewrite (P0) — identified 2026-04-14 audit
Comprehensive 45-sample audit of `ScreenDetectionLogic.Detect` revealed the detection layer is the root cause of most UI navigation bugs ("Auto-Battle instead of Wait", cursor desync, broken world-side detection). Full data in `detection_audit.md` in repo root. Key findings in `BATTLE_MEMORY_MAP.md` §12.

**Root causes:**
- `menuCursor` is overloaded (different meaning per context: action menu vs submenu vs targeting vs pause)
- `battleMode` is overloaded (encodes cursor-tile-class, not screen submode)
- `encA/encB` are noise counters — every rule using them is a coincidence-detector
- `gameOverFlag` is sticky process-lifetime — rules requiring `gameOverFlag==0` fail after first GameOver
- `rawLocation==255 → TitleScreen` rule preempts valid world-side screens (WorldMap/TravelList/PartyMenu all fall through wrongly)
- Two distinct TitleScreen states exist (fresh process vs post-GameOver) with different memory fingerprints

**Fix tasks:**

- [ ] **Remove `encA/encB`-dependent rules** — replace Battle_Victory / Battle_Desertion / EncounterDialog discriminators with stable signals (`paused`, `submenuFlag`, `acted/moved` combos).


- [ ] **Add `Battle_ChooseLocation` discriminator** — requires location-type annotation (which location IDs are multi-battle campaign grounds vs villages). Add to `project_location_ids_verified.md`.




### Coordinate System
- Grid coords and world coords are different systems
- Camera auto-rotates when entering Move mode or Attack targeting — always re-read rotation


### Bugs Found 2026-04-12
- [ ] **Ability list navigation: wire AbilityCursorDeltaPlanner into scroll loop** — Pure planner with trust rules (sign match + magnitude guard + expected-magnitude check) shipped session 47 (`AbilityCursorDeltaPlanner.Decide`). Still needed: wire into the Up-reset / Down-scroll loop in the battle ability nav path. When planner returns `TrustDelta=false`, fall back to the existing blind Up×N / Down×index approach. Session 31 context: Up-wrap produces negative deltas that exploded retry math — planner formalizes the fallback trigger.


- [ ] **Detect rain/weather on battle maps** — Rain boosts Lightning spells by 25%.


- [ ] **Post-battle memory values stuck at 255 after auto-battle** — All memory addresses stayed at 255/0xFFFFFFFF permanently. May require game restart.


- [ ] **Fix stale location address (255) after restart breaking battle-map auto-detect** — Location ID lookup + random encounter maps + fingerprint fallback already shipped. Remaining bug: after game restart, `0x14077D208` reads 255 which defaults to the wrong map. Need a fallback read or forced re-read on first post-restart scan.




### Bugs Found 2026-04-12 Session 2


- [~] **Static array at 0x140893C00 is stale mid-turn** [State] — **session 30 scope audit** confirmed this bug no longer has an active trigger: (a) damage-preview code was removed after the statBase-62/-96 hunt concluded (see memory/project_damage_preview_hunt_s30.md), taking the main attacker-MaxHP-post-move lookup with it; (b) `ReadLiveHp` already reads readonly-region copies at 0x141xxx/0x15Axxx which update in real time; (c) `CollectPositions` only runs at turn boundaries (scan blocked in BattleActing/Casting/Attacking/Enemies/Allies turns) where the static array IS fresh; (d) surviving BattleAttack target-HP reads happen on a unit that hasn't moved, so those are fresh too. Reopen only if a new scenario surfaces stale-HP symptoms.


- [~] **Damage/hit% preview during targeting** [State] — **AoB-search path ruled out, session 30 (2026-04-17).** See `memory/project_damage_preview_hunt_s30.md`. Live-verified: statBase-62 / statBase-96 offsets relative to both (attacker MaxHP+MaxHP) and (target HP+MaxHP) patterns contain only unit base stats in all 10 copies found across 0x140xxx / 0x141xxx / 0x15Axxx / 0x4166xxx regions. `ReadDamagePreview` now returns (0,0) permanently; `response.Info` on `battle_attack` uses post-attack delta-HP as ground truth instead. If preview is ever needed, pivot to: (a) UE4 widget vtable walk from a stable static pointer to the combat HUD, (b) DLL detour on the preview-render callback, or (c) formula compute (blocks on zodiac + PSX-vs-IC formula verification).


- [ ] **Screen detection shows Cutscene during ability targeting** [State] — While in targeting mode for Aurablast (selecting a target tile), screen detection reports "Cutscene" instead of "Battle_Attacking" or "Battle_Casting". This causes key commands to fail because they check screen state. Observed 2026-04-13.


- [ ] **Failed battle_move reports ui=Abilities instead of ui=Move** [State] — After battle_move fails validation, the response shows ui=Abilities but the in-game cursor is still on Move. The scan that runs before the move might be changing the reported ui state. Observed 2026-04-13.


- [ ] **battle_ability selects wrong ability from list** [Execution] — battle_ability "Aurablast" selected Pummel instead. The ability list navigation (Up×N to top, Down×index) is picking the wrong index. The learned ability list may not match the hardcoded index, or the scroll navigation is off-by-one. Observed 2026-04-13.


- [ ] **Abilities submenu remembers cursor position** [Execution] — After battle_ability navigates to a skillset (e.g. Martial Arts for Revive), then escapes, the submenu cursor stays on that skillset. Next battle_attack enters Martial Arts instead of Attack. Need to verify/navigate to correct submenu item rather than assuming cursor is at index 0. Observed 2026-04-13.


- [ ] **battle_ability response says "Used" for cast-time abilities** [State] — Abilities with ct>0 (Haste ct=50) are queued, not instant. Response says "Used Haste" but spell is only queued in Combat Timeline. Unit still needs to Wait. Response should say "Queued" for ct>0 abilities. Observed 2026-04-13.




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
