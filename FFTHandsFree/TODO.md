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


### Session 58 — follow-ups (2026-04-22)

- [ ] **⚠ UNVERIFIED: Wire S58 AoE pure helpers into scan_move output** [Abilities] — S58 shipped `LineAoeCalculator`, `SelfCenteredAoeCalculator`, `MultiHitTargetEnumerator`, `GeomancySurfaceTable` as pure helpers with full test coverage, but they're NOT yet wired into the `scan_move` ability-target rendering path. Current path in `NavigationActions.cs` / `AbilityTargetCalculator` has its own AoE enumeration — switching to the new pure helpers would reduce duplication + make geomancy terrain-aware. Next session: replace the ad-hoc AoE logic with the pure helpers. Start with `SelfCenteredAoeCalculator` (Chakra/Cyclone), then Line (Shockwave), then Geomancy surface lookup.

- [ ] **⚠ UNVERIFIED: Wire CharacterStatusLeakGuard into detection path** [Detection] — S58 shipped the pure helper but it's not called anywhere yet. Session-31 leak (sourceScreen=CharacterStatus → targetScreen=BattleMyTurn during battle_wait animations) is still in the session logs until this gets wired. Add a call in `CommandWatcher` around the `DetectScreenSettled` path: `CharacterStatusLeakGuard.Filter(previousDetected, screen.Name, keysSinceLastSettle)`. Requires tracking `previousDetected` + `keysSinceLastSettle` counters — small refactor.

- [ ] **⚠ UNVERIFIED: Wire BattleMenuAvailability into screen response** [Rendering] — Pure helper exposes "Move/Abilities/Wait/Status/Auto-battle — each enabled or grayed" based on BattleMoved/BattleActed flags. Currently nothing consumes it. Add to `screen` response as a new field (e.g. `menuAvailability: [{name, slot, available}]`) so Claude doesn't blindly navigate into a grayed slot.

- [ ] **⚠ UNVERIFIED: `[counter-KO]` marker on battle_ability response** [Execution] — S58 wired counter-KO detection to prepend `[counter-KO] active unit died from reaction — do not battle_wait` to response.Info. Untested live — needs an attack against an enemy with Counter where the counter damage kills the attacker. Next repro: hit a high-Brave enemy with Counter learned at low-HP attacker.

- [ ] **⚠ UNVERIFIED: `execute_turn` TurnSummary + milestone callouts** [Execution] — S58 wired `TurnSummary` (HpDelta/PreMoveXY/PostMoveXY/KilledUnits) into the `execute_turn` response, and `MilestoneDetector` into `RenderBattleSummary`. Not seen in live output yet — `execute_turn` bundle wasn't exercised in S58 verification. Next session: run `execute_turn <x> <y> <ability> <tx> <ty>` and confirm `turnSummary` field appears + `stats battle` renders milestones like "🏆 Ramza reached 10 kills!".

- [ ] **⚠ UNVERIFIED: `[turn-interrupt]` marker on execute_turn** [Execution] — New `TurnInterruptionClassifier` aborts `execute_turn` bundle early when landing on `BattleVictory` / `BattleDesertion` / `WorldMap` mid-sequence. Needs a live scenario where a sub-step ends the battle (e.g. final-kill attack during execute_turn) — response should show the Info marker.

- [ ] **⚠ UNVERIFIED: Strict mode default on** [Execution] — S58 flipped `StrictMode = true` default. Fresh sessions now reject raw key presses unless the caller runs `strict 0` first. Verify next session: fresh boot + `screen` should work (no-op query); raw `up`/`down` should be blocked with the STRICT MODE error.

- [ ] **⚠ UNVERIFIED: battle_attack submenu retry + charging-confirm dismiss** [Execution] — S58 added 1500ms poll-retry when the Abilities submenu doesn't open on first Enter, and a retry when charging-confirm modal eats the first Enter. Both observed in S58 live play but fixes not yet confirmed in-game with a clean repro.

- [ ] **⚠ UNVERIFIED: BattleVictory false-positive during Shout/Chakra** [Detection] — S58 tightened the Victory sentinel with `submenuFlag==1`. Live session 58 observed brief `[BattleVictory]` flashes during Shout/Chakra casts; fix should eliminate them. Next battle with a Shout-caster: confirm no stray Victory flashes in session log.

- [ ] **Post-Victory BattleDesertion when unit crystallizes** [Stats] — S58 Fix #1 added `previous == "BattleVictory"` guard so post-Victory Desertion doesn't double-fire EndBattle (previously stomped a Win with a Loss). Still a pre-existing TODO: Desertion screen appearing after Victory means we lost a unit — surface that in the battle summary as "unit deserted: Wilham" alongside the Won result, not silent.


### Session 57 — follow-ups (2026-04-22)

- [ ] **Aurablast follow-up: learned-ability bitfield audit** [Execution] — S57 fixed the NAV path (`d9f143a`: full skillset list used for cursor index math), but the user-facing `abilities[]` array in scan_move output still goes through `FilterAbilitiesBySkillsets` which filters by learned state. Kenrick's Martial Arts displayed `[Cyclone, Purification, Chakra, Revive]` (4) when the game UI shows 8 entries. Two possibilities: (a) learned bitfield reader at `AddrCondensedBase + 0x28` undercounts for secondary skillsets, or (b) `FilterAbilitiesBySkillsets` is over-filtering. **Next-session repro:** live-dump Kenrick's condensed-struct ability bytes, parse with `ActionAbilityLookup.ParseLearnedIdsFromBytes`, diff against the game's visible list. Fix whichever is wrong. Nav is unblocked today, but scan output lying about what Kenrick can cast is a decision-aid bug.

- [ ] **⚠ UNVERIFIED: `battle_attack` post-cast adaptive animation wait** [Execution] — S57 shipped `14ff34f` replacing the fixed `Thread.Sleep(2000)` with a poll for post-animation resolved state. Untested live because no enemy was adjacent during S57 test battles. S58 live test hit submenu retries instead of getting to the attack. Next battle with an adjacent enemy: confirm `battle_attack` returns as soon as the animation settles (not the full 2500ms ceiling) and HP delta still reads correctly.


### Session 56 — follow-ups (2026-04-22)

- [ ] **Post-Victory WorldMap mis-detects as `LoadGame`** [Detection] — S56 live: after `kill_enemies` → Victory → 5× Advance, the game is visibly on WorldMap at The Siedge Weald ("13 Capricorn" world date shown, character sprite on the map node), but detection persistently reports `[LoadGame]` with `curLoc=Walled City of Yardrow` (stale location). Root cause: `ScreenDetectionLogic.cs:557` LoadGame rule requires `gameOverFlag == 1` — but gameOverFlag is STICKY across the full post-battle sequence. Rule fires whenever gameOverFlag stuck at 1 + `!actedOrMoved` + `!atNamedLocation` + `IsEventIdUnset`, which is exactly the post-Victory WorldMap fingerprint. Fix approach: require an additional positive signal specific to LoadGame (e.g. menuCursor in save-slot range 0-7 + a sub-screen flag). Needs live capture of both LoadGame and post-Victory-WorldMap detection inputs to design the discriminator. Cosmetic today (the game renders WorldMap correctly) but blocks `execute_action` because ValidPaths come from the wrong screen.

- [ ] **⚠ UNVERIFIED: battle_wait "Auto-facing detected" path triggers on BattleAttacking re-targeting** [Detection] — `BattleWaitLogic.ShouldSkipMenuNavigation(BattleAttacking) == true` is ambiguous: BattleAttacking AFTER a successful Attack means facing-confirm (skip menu, F to confirm), but BattleAttacking AFTER a MISS means re-targeting (needs Escape). S56 fixed the miss case in battle_attack itself (escapes re-targeting before returning), so the ambiguity should no longer surface — but the BattleWaitLogic rule itself is still fragile. Revisit if any new repro lands on BattleAttacking without going through battle_attack.

- [ ] **⚠ UNVERIFIED: Auto-battle submenu unintended activation** [Execution] — During S56 live-play testing, the game's Auto-battle submenu (Manual/Attack Enemy/Protect Ally/Heal Allies/Get to Safety) opened once during menu nav. Cause unclear — may have been a stray Enter from the now-fixed drift bugs. Not repro'd post-fixes. Watch for it next session.

- [ ] **AOB resolver is a research dead-end without pointer chains** — S55 spent ~2h on `<listSize_u64> + 0x1407FC6D8 vtable` AOB; worked once, then failed because the cursor's offset within the widget shifts between widget allocations. Reverted commits `7def3c2`, `892f979`, `b4d7d98`. **Don't re-attempt without first solving** the structural-offset instability — see memory note for full failure analysis. Pointer-chain (Cheat Engine pointer scan) is the only path that survives widget reallocation. (S56: escape-to-known-state option 3 shipped — this AOB approach not needed for V1.)

<!-- S55 🔴 BLOCKING `battle_ability picks the wrong ability` + 3-step breakdown all shipped in S56 (commit 8bb692e). Live-verified: Chakra (Martial Arts idx 6, Up×2) and Haste (Time Magicks idx 0, None×0) cast correctly. `AbilityListCursorNavPlanner.Plan` wired in that same commit. Memory note updated. -->

<!-- S56 ADDITIONAL FIXES SHIPPED (all live-verified):
- `battle_wait` overshoot to Auto-battle (08e9d99) — stale byte retry amplification
- Move/Jump UIBuffer fallback → honest Mv=0 (bdcd5bb)
- State-gate retry for transient reads (b2f035d)
- Broad-search fallback for heap Move/Jump (402bd92)
- MoveGrid confirmation tightened to BattleMyTurn/BattleActing (3ba67d5)
- battle_wait accepts BattleActing (07646ab)
- battle_ability post-cast settle (85682fb)
- battle_wait F→Enter fallback + dismiss learned-ability/reward banners (24f0348)
- Heap search uses MaxHP-twice pattern (damage-invariant) (b3180e3)
- battle_attack escapes re-targeting after miss (851a927)
- battle_wait exits poll on Victory/Desertion (6cc2e7d)
-->


### Session 52 — scan_diff identity + per-unit ct hunt (2026-04-20)

- [ ] **kill_one player persistence regression** — Session 52 found `kill_one Wilham` wrote HP=0 + dead-bit to master HP slot `0x14184FEC0` but after a turn cycle Wilham showed HP=477 again. Session-49 docs say master is authoritative but for PLAYERS the write reverts. Investigate whether there's a per-frame refresh from roster into master for player slots specifically. See `memory/project_deathcounter_battle_array_scan.md`.

- [ ] **Per-unit casting ct hunt — unblocked, ready to retry** — S58 shipped `broadSearch` plumbing on `search_bytes` (via `SearchBytesPlan`). Ready to retry HP=MaxHp fingerprint hunt for Kenrick's heap struct. See `memory/project_per_unit_ct_hunt_deferred.md`.


### Session 49 — follow-ups (2026-04-20)


- [ ] **Live-verify `kill_enemies` Reraise-clear path against a real Skeleton / Bonesnatch** — Session 50 confirmed `kill_enemies` cleared a Bonesnatch at Siedge Weald (Victory triggered, no revive observed). Still need proof the **Reraise-bit-clear writes specifically** fire (they may be no-ops if the Bonesnatch's status byte[2] didn't have the Reraise bit set). Check by reading battle-array +0x47 before and after `kill_enemies` on a unit that provably has the Reraise status.

- [ ] **Verify `+0x29` as deathCounter with natural KO** — Session 49 found candidate at master-HP-table +0x29 that ticked 3→2 on a `kill_enemies`-KO'd Goblin. The `+0x31 |= 0x20` dead-bit write may have initialized the counter artificially; need natural KO (normal attack) to confirm it's the true crystallize countdown. See `memory/project_deathcounter_offset_0x29.md`.

- [ ] **Verify cast queue at `0x14077D220`** — Session 49 found 3 u32 records with `(u8, 0xB0, 0x00, 0x00)` pattern after queuing Curaja ct=10. Bytes didn't tick across polling — may not be the ct counter, or ct only advances during enemy/ally turns (not player's turn). Next-session approach: queue a spell on Kenrick, end Lloyd's turn (so CT advances), immediately read `0x14077D220`, wait another turn, read again; expect monotonic decrement. See `memory/project_charging_ability_queue.md`.

<!-- "Hunt Zodiac byte via heap-struct scan" — broken down into strategies A-E under §1 Tier 4 "Zodiac byte hunt strategy A-E". Duplicate removed. -->

- [~] **BattleVictory post-banner false-GameOver edge** — Deferred until a real repro surfaces. Current `battleTeam==0` guard handles the known cases (session 49 Kenrick counter-kill). Regression test `DetectScreen_VictoryWithRamzaDying_TeamZeroGuard_ReturnsBattleVictory` pins current behavior. If a team=2 NPC counter-kill scenario gets captured, swap the guard for a dedicated encA/encB condition.


### Session 47 — follow-ups (2026-04-19)

<!-- "Wire AbilityListCursorNavPlanner.Plan once real-cursor source exists" — shipped S56 (commit 8bb692e). Escape-to-known-state from BattleAbilityEntryReset gives currentIdx=0 trivially; planner wired at NavigationActions.cs ListNav. Live-verified with Chakra (Up×2) and Haste (None×0). -->



### Session 48 — follow-ups (2026-04-19)

<!-- "Random-encounter map resolution: FIXED via live-map-id byte — regression test only" — S58 extracted `LiveBattleMapId` constant + `IsValid(mapId)` helper with 3 regression-pin tests. If the byte shifts after a game patch, the `LiveBattleMapIdTests` fails loudly. -->


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


- [ ] **LoS option A: read game's projected hit% from memory during targeting** [Abilities] — **Blocked by session 30 finding** that hit% isn't findable via flat-memory AoB search — see `memory/project_damage_preview_hunt_s30.md`. LoS-via-memory now depends on the same widget-introspection or formula-compute path that damage preview needs. Prefer LoS option B (compute from map height data) until that path lands.

- [~] **LoS option B: compute LoS from map height data** [Abilities] — Session 31: shipped. `LineOfSightCalculator` (pure, DDA walk + linear altitude interp, 13 tests) + `ProjectileAbilityClassifier` (pure rule: ranged Attack + Ninja Throw qualify, spells/summons don't, 9 tests) + wire-up in `NavigationActions.AnnotateTile` populating `ValidTargetTile.LosBlocked`. Shell renders `!blocked` sigil. Needs live verify on a bow/gun/crossbow unit with terrain blocking a shot.

- [ ] **LoS option C: enter targeting, check if game rejects tile, cancel if blocked** [Abilities] — Brute-force fallback. Slow but reliable. Use only as last resort if A and B both fail.


- [ ] **battle_move reports NOT CONFIRMED for valid moves** [Movement] — Navigation succeeds but F key confirmation doesn't transition. Timeout increased from 5s to 8s for long-distance moves.


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

<!-- S58 shipped: `execute_turn` HP-delta / move-delta / kill-diff accumulator via `ExecuteTurnResultAccumulator` + `TurnSummary` DTO + `LastScannedUnitSnapshots()`. Response now carries `turnSummary` field when execute_turn is invoked. -->




---


## 2. Story Progression (P0, BLOCKING)

- [ ] **Orbonne Monastery (loc 18) encounter: capture detection inputs** — Loc 18 has a different encounter screen than regular battlegrounds. Step 1: travel to Orbonne, capture full detection inputs (rawLocation, encA/encB, battleMode, paused, submenuFlag, eventId, battleTeam) at each transition (WorldMap→Encounter→Formation→BattleMyTurn). Save snapshot JSON to memory note.

- [ ] **Orbonne Monastery (loc 18) encounter: add detection rule** — Based on inputs from the capture step, add a discriminator rule to `ScreenDetectionLogic.cs`. Pin with a regression test in `ScreenDetectionTests.cs`.

- [ ] **Orbonne Monastery (loc 18) encounter: wire ValidPaths** — Once detected, add `NavigationPaths` entries so `execute_action` can fire the correct Fight/Flee/Advance actions.





---


## 8. Speed Optimizations (P1)

<!-- "Auto-scan on Battle_MyTurn" — SHIPPED S57 (3a95585). Screen-query responses auto-scan when landing on a fresh BattleMyTurn. Gated on _turnTracker.ShouldAutoScan so fires at most once per friendly turn. -->

<!-- "Latency measurement" — SHIPPED S57 (e07b3ab). session_stats bridge action + shell helper. Per-action count/median/p95/max/failed summary. -->

<!-- "Pre-compute actionable data" — intentionally NOT shipped. Drafted BattleSituation (nearestEnemy, hurtAllies) during S57 but reverted per user direction before commit. Existing scan output already surfaces per-unit distance, attack tiles, ability targets. Reopen only if a specific decision-aid gap appears. -->

- [ ] **Background position tracking** — Poll positions during enemy turns so they're fresh when it's our turn



---


## 9. Battle — Advanced (P2)


### Error Recovery
<!-- S58 shipped (pure helpers + wire-ups):
 - Detect failed move/attack: `battle_attack` submenu retry loop + charging-confirm dismiss in NavigationActions
 - Handle unexpected screen transitions: `TurnInterruptionClassifier` + wire in `ExecuteTurn`
 - Counter attack KO: `CounterAttackKoClassifier` + wire in `battle_ability` response.Info
-->


### Advanced Targeting
<!-- S58 shipped pure helpers (not yet wired into scan_move output — see §0 S58 follow-ups):
 - `LineAoeCalculator` (Shockwave, Ice Saber)
 - `SelfCenteredAoeCalculator` (Chakra, Cyclone, Wave Fist, Bard/Dancer)
 - `MultiHitTargetEnumerator` (Bio, Ramuh — rank centers by enemy coverage)
 - `GeomancySurfaceTable` (surface id → Elemental ability, 15 surface types)
-->




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

- [ ] **Remove `encA/encB` from BattleVictory discriminators** — S58 PARTIAL: Fix #4 added `submenuFlag==1` guard on the encA=255 sentinel (line ~545), killing the Shout-mid-cast false positive. Two other Victory rules (lines ~487, ~545 before-fix) still use encA/encB. Full rewrite still needs live captures showing the rules' fingerprints WITHOUT encA/encB columns.

<!-- S58 shipped regression pins for BattleDesertion + EncounterDialog: 72- and 36-combination tests sweep the full (encA, encB) range and assert the classification holds. Rules already didn't depend on encA/encB; pins prevent future drift. -->


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

S57 wired the `BattleStatTracker` (previously orphaned) into bootstrap + added `stats` / `stats battle` shell helpers + `OnTurnTaken` lifecycle hook. Foundation is live. Per-battle detail hooks (damage, kills, heal, move, ability-usage) are the remaining work — tracked in §0 "Detailed in-battle hooks for BattleStatTracker".

<!-- "Per-unit career totals + MVP + Post-battle summary + stats command" — SHIPPED S57 (b9a6c59). BattleStatTracker instantiated, lifetime_stats.json persisted, OnTurnTaken credited per wait, MvpSelector picks per battle, RenderBattleSummary / RenderLifetimeSummary exposed via render_battle_summary / render_lifetime_summary bridge actions. Shell: `stats` / `stats battle`. Detailed per-action hooks still needed — see §0. -->

<!-- S58 shipped: `MilestoneDetector` (pure helper, 10 TDD tests) + wired into `BattleStatTracker.EndBattle` via SnapshotLifetime diff. Milestones surface in `RenderBattleSummary` output (first kill / 10 / 50 / 100 / 500 kills; 1k / 5k / 10k / 50k damage; 10 / 50 / 100 battles). Emoji callouts with 🎯 🏆 💥 ⚔️. -->



---


## Low Priority / Deferred


<!-- S58 shipped: strict mode default = true. `CommandWatcher.StrictMode` property default flipped. Raw key input now opt-in via `strict 0`. See Instructions/Rules.md "Always enable strict mode for play sessions". -->

<!-- S58 shipped regression pin: `DetectScreen_PostBattleRules_NoDependsOn_GameOverFlagZero` — post-battle WorldMap classification works with gameOverFlag=0 OR gameOverFlag=1. Audit confirmed no rule uses `gameOverFlag == 0` as a positive assertion. Stickiness no longer blocks. -->

- [ ] **Re-enable Ctrl fast-forward during enemy turns** [Execution] — Tested both continuous hold and pulse approaches. Neither visibly sped up animations. Low priority.


## Sources & References

- [fft-map-json](https://github.com/rainbowbismuth/fft-map-json) — Pre-parsed map terrain data (122 MAP JSON files)
- [FFT Move-Find Item Guide by FFBeowulf](https://gamefaqs.gamespot.com/ps/197339-final-fantasy-tactics/faqs/32346) — ASCII height maps for MAP identification
- [FFHacktics Wiki](https://ffhacktics.com/wiki/) — PSX memory maps, terrain format, scenario tables


---


## Completed — Archive

Completed `[x]` items live in [COMPLETED_TODO.md](COMPLETED_TODO.md). During the session-end `/handoff`, move newly-completed items from here into that file. Partial `[~]` items stay in this file in their original section.
