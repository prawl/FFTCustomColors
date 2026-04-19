<!-- This file is exempt from the 200 line limit. -->
# FFT Hands-Free — Claude Plays Final Fantasy Tactics

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

### Session 43 — next-up follow-ups

- [x] **Live-verify IsPartyTree refactor didn't silently narrow CharacterStatus roster population** — VERIFIED 2026-04-18 session 44. Opened CharacterStatus on Ramza from Bervenia→WorldMap; `screen.roster` populated with all 15 units (Ramza, Kenrick, Lloyd, Wilham, Mustadio, Agrias, Rapha, Marach, Beowulf, Construct 8, Orlandeau, Meliadoul, Reis, Cloud, Crestian), each with full stats + equipment + displayOrder. Session 39 refactor did not narrow population.

- [ ] **Live-verify Gollund row 4 unmapped-error path via UI** — Session 42 wired Gollund row 3 → corpus #20 "The Haunted Mine" + row 4 → unmapped. Live-verified row 3 + UI cross-reference session 42. Row 4 verified via bridge but not UI (the "At Bael's End" body renders from a different data source we can't decode). When that source lands, seed Gollund row 4 and all 8 uniform cities' row 3.

### Session 33 batch 2 — deferred (needs live battle / environment I can't verify)

- [~] **Live-verify `execute_action` responses include `ui=` field across battle screens — MOSTLY DONE (session 44 2026-04-18)** — Live-tested in a Mount Bervenia random encounter. Findings: **ui POPULATES** on `BattleMyTurn` ("Move"/"Abilities"/"Status"), `BattleMoving` (tile like "(8,7)"), `BattleAbilities` ("Attack"), `BattleAttacking` ("Attack" via tracker, or "(x,y)" via new cursor fallback — see fix below). **Still null on `BattlePaused` + `BattleStatus`** (separate sub-tasks). **Fix shipped session 44**: `TargetingLabelResolver.ResolveOrCursor(...)` + CommandWatcher call-site swap so BattleAttacking/BattleCasting always surface either the latched ability name OR the cursor tile, matching BattleMoving's semantics. 4 new tests + live-verified after restart. Separate finding: `execute_action` response `screen.name` can lag a step during rapid transitions; subsequent `screen` call returns fresh state. **Remaining sub-tasks split out**: BattleStatus ui decode (viewer screen, no cursor — likely "ui=stat-tab" if tabs exist, else drop the expectation); BattlePaused ui decode (pause-menu cursor byte hunt — 0x1407FC620 reads the battle-action-menu cursor, not pause). BattleActing / BattleCasting / BattleWaiting not exercised this session — TargetingLabelResolver change covers BattleCasting structurally.

- [ ] **PreToolUse hook to block `| node` in Bash commands** — needs explicit per-hook user approval (per `feedback_no_hooks_without_approval.md`). Defer until user green-lights.

- [ ] **IC remaster deathCounter offset hunt** — PSX had it at ~0x58-0x59 in battle unit struct. Needs live battle with a KO'd unit to find the IC equivalent. Blocks KO/crystallize-aware tactics.

- [ ] **`AbilityJpCosts` — backfill Jump and Holy Sword skillsets** — Session 41 coverage audit found both return null from `ComputeNextJpForSkillset` (100% gap rate). Jump collapses sub-abilities (H/V Jump levels 1-8), Holy Sword is Agrias's story skillset. Cost data omitted in code with a comment; a future session should either populate real JP costs or explicitly document both as no-op for Next: N. Characterization test `CoverageAudit_KnownUncoveredSkillsets_StillReturnNull` pins current behavior. **Session 44 attempt**: code shipped + 3202 tests green (Jump backfilled with ABILITY_COSTS.md values, Holy Sword pinned as intentional null, characterization test flipped). Restart + smoke tested clean. But live-verify of non-null Next:N for Jump blocked on this save — every JP-purchasable unit has primary fully mastered. Same blocker as Mettle live-verify deferral. Per "every task live-verified" rule, reverting edits until a partially-learned Dragoon is available.

### Session 33 — next-up follow-ups (from 6-task batch attempt)

- [ ] **Live-verify !weak / +absorb sigils** — session 33 attempt: travel from WorldMap → Yardrow timed out on bridge, left game on OutfitterBuy and subsequent heap reads timed out. Party has no Wizard; best test candidates are Kenrick (White Mage, Holy) or Rapha (Skyseer, Holy) vs an undead enemy for `+absorb:['Dark']` / `!weak:['Holy']`. Needs: (a) stable bridge session (may need `restart`), (b) travel to a random-encounter zone with undead (Skeleton/Ghost/Ghoul), (c) scan during Holy-cast targeting menu.

- [ ] **Wire TavernRumors cursorRow to screen response** — Session 33 found `0x13090F968` at Dorter. **Session 44 (2026-04-18)** confirmed the same `+0x28` widget offset holds at Bervenia — byte shifted to `0x13091F968` (widget base `0x13091F940`, +0x10000 from session 33). Triple-diff intersection (row0→1, 1→2, 2→3) + live-read verification is a RELIABLE per-session re-locator technique (yields ~5-7 candidates, narrowed to 1 by reading at current cursor). Widget header structure (self-pointer / count / tag / cursor at +0x28) is stable across both sessions. Still no stable anchor for AUTO-relocation at runtime — direct pointer-search for widget-base bytes returned 0 hits, confirming UE4 Slate vtable walk is needed. Memory note `project_tavern_rumor_cursor.md` updated with the full technique + next approaches.

- [ ] **BattleSequence discriminator heap-hunt** — session 33 deferred due to bridge state. Scaffolding already in `ScreenDetectionLogic.cs` (commented-out rule + `BattleSequenceLocations` HashSet). Approach: travel to Orbonne (loc 18), confirm BattleSequence minimap, `heap_snap` before entering/after leaving, diff to find a dedicated flag byte.

- [x] **SaveSlotPicker from BattlePaused — DOES NOT EXIST (closed 2026-04-18 session 44)** — Entered a real battle at Mount Bervenia, opened the BattlePaused menu via Pause, screenshotted the full 6-item menu: Data, Retry, Load, Settings, Return to World Map, Return to Title Screen. **No Save option.** The BattlePaused → SaveSlotPicker edge is imaginary — user corrected memory that originally conjectured it. Bridge validPaths already match the 6 items (Resume/Units/Retry/Load/ReturnToWorldMap/ReturnToTitle + cursor nav) with one naming quibble (`Units` vs on-screen `Data`). See also duplicate entry at line 167 — also closed.

### Session 33 — Tavern Scope B (decoder shipped; per-city mapping partial)

- [ ] **Some rumor bodies NOT in `world_wldmes.bin`** — "At Bael's End" (Dorter row 3) doesn't match any substring in the corpus. Searched all 318 .bin files in 0000-0004 pac dirs with the word "Bael" encoded PSX-style → zero hits. Likely lives in a UE4 `.locres` file or the `0002.en.pac/fftpack/world_snplmes_bin.en.bin` (same-ish size, different encoding — includes `D1/D2/D3` multibyte sequences suggesting kanji/Japanese layered with English). Next session: (a) try extracting text from `world_snplmes_bin.en.bin` with a multibyte decoder, (b) check UE4 `Paks/*.pak` for locres files, (c) scan bigger pac dirs (0005-0011).

- [ ] **Per-city row→corpus_index table — remaining cities** — As of session 43: 9 of 15 settlements seeded (Dorter/Gariland/Warjilis/Yardrow/Goug/Zaland/Lesalia/Bervenia uniform via `Chapter1UniformRows` + Gollund divergent at row 3 = corpus #20). Remaining: Riovanes(1), Eagrose(2), Lionel(3), Limberry(4), Zeltennia(5), Sal Ghidos(14) — most are battle-locked or story-progression-locked in Chapter 1. Seed them when Chapter 2+ unlocks access. If any city's uniform-rows break (Chapter-2 rumor refresh), split into per-chapter tables. Workflow in `FFTHandsFree/TavernRumorTitleMap.md`.

- [ ] **Candidate city-specific rumors to check at Chapter 2+** — Session 43 `CorpusCityMentionTests.cs` flagged corpus entries that name specific cities but don't appear at those cities' Chapter-1 taverns: #12 Warjilis (Baert Trading Company), #15 Lionel (Cardinal Delacroix), #23 Bervenia+Dorter (Wailing Orte). Re-check each of these cities' tavern lists after Chapter-2 story progress — likely rehost candidates.

- [ ] **Title→corpus auto-match UX** — `read_rumor "<phrase>"` requires Claude to know a distinctive phrase from the rumor. Titles are not in bridge state (not in RAM, not in decoded file). Session 34 `{locationId, unitIndex}` path is the cleanest alternative but requires knowing the cursor row. Remaining options: (a) screenshot + OCR the title region on TavernRumors entry, include as `rumorTitle` in `screen` response; (b) wire the TavernRumors cursor-row byte at `0x13090F968` via the pointer-chain walk (memory note `project_tavern_rumor_cursor.md`) so `screen` surfaces `cursorRow` automatically and `read_rumor` in the UI resolves via CityRumors instead of needing a phrase. (b) still blocked on unmappable Bael's-End class.

- [ ] **Errand metadata (quester / days / fee)** — separate source from rumor bodies. Likely in an NXD layout — `tools/Nex/Layouts/ffto/Book.layout` exists but no `book.nxd` was found in `0004/nxd/` during session 32 exploration. Possible paths: (a) the `Book` layout actually targets a data file not present in this installation, (b) errand metadata is in `world_wldmes_bin.en.bin` alongside the rumor bodies and shares record structure, (c) a different layout file (`Proposal*.layout`?) — session 32 didn't find one. Re-investigate after Scope B decoder is working; can piggyback on the same parser.


### Session 31 — next-up follow-ups (live-verify pending)

- [ ] **Detection leaks CharacterStatus / CombatSets during battle_wait animations** — Session 31 `session_tail slow 1500` exposed: battle_wait rows report `sourceScreen=CharacterStatus` → `targetScreen=BattleMyTurn` with 15-23 second latencies. The player is IN battle the whole time, not on CharacterStatus. Likely a detection false-positive during facing-confirm or wait-ct animation frames where unit-slot / ui bytes transiently match CharacterStatus patterns. Doesn't break gameplay (final target is correct) but slows down logging diagnostics and could mis-route screen-gated actions. Next repro: `session_tail` during a battle, look for any `*→BattleMyTurn` with non-Battle source.



- [ ] **Live-verify `!weak` / `+absorb` / `~half` / `=null` / `^strengthen` per-tile sigils** — Session 31 shipped `ElementAffinityAnnotator` + `ValidTargetTile.Affinity` + shell render. JSON field populates correctly (confirmed via response-json inspection — Black Goblins show `elementWeak:['Ice']`). Per-tile shell sigil UNTRIGGERED in current save: all available caster abilities are non-elemental (Mettle/Monk/Time Magicks) so the marker never fires. Next repro: a Wizard with Fire + Ice-weak enemy on field, OR a White Mage with Holy + undead enemy. Confirm `<Goblin !weak>` / `<Skeleton +absorb>` style suffixes render.

- [ ] **Live-verify `>BACK` / `>side` arc sigils** — Session 31 shipped `BackstabArcCalculator` + `ValidTargetTile.Arc` + `AttackTileInfo.Arc` + shell render (front omitted, only back/side show). JSON field populates correctly (confirmed: `arc:"front"` on enemy tiles during Ramza scan). Back/side sigils UNTRIGGERED in current save because all attack approaches ended up front-arc relative to east-facing goblins. Next repro: reposition Ramza west of an east-facing enemy (attacker behind target's facing axis) to trigger `>BACK`.

- [ ] **Live-verify LoS `!blocked` sigil** — Session 31 shipped `LineOfSightCalculator` + `ProjectileAbilityClassifier` + wire-up + shell sigil. Code path triggers only on `Attack` (ranged weapon) or `Throw` (Ninja) skillsets. Current save has no active Archer/Gunner/Ninja — Mustadio is Machinist but unequipped; attempts to change Lloyd to Archer failed due to helper bugs. Next repro: any unit with a bow/gun/crossbow/ninja-throw + a battle map with terrain between them and an enemy.

- [ ] **Live-verify Cutscene-over-LoadGame fix** — Session 31 shipped the `eventId ∈ {0, 0xFFFF}` guard on the LoadGame detection rule. Unit tests cover the transition; needs a live repro where `gameOverFlag=1` is sticky (e.g. after a GameOver → load save) while a real cutscene plays. Expected: screen reads `Cutscene` not `LoadGame`.

- [~] **Live-verify full 5-field element decode on varied enemies — 3/5 FIELDS CONFIRMED (session 44 2026-04-18)** — Lenalian Plateau random encounter confirmed: **`elementWeak`** (Piscodaemon Lightning, Red Panther Earth), **`elementAbsorb`** (Piscodaemon Water), **`elementHalf`** (Knight Dark). Still unconfirmed: **`elementNull`** (was called `elementCancel` in the old wiki — may need a Lucavi or a unit with elemental nullification gear) and **`elementStrengthen`** (needs a player unit with elemental-strengthen weapon/gear equipped — check the shop for Materia Blade / Faith Rod / elemental staves). All 3 confirmed fields serialize correctly from memory and appear in the `scan_move` output JSON — the pure decode path is validated on varied enemy archetypes.

- [ ] **⚠ UNVERIFIED: `AutoEndTurnAbilities` — Self-Destruct** — shipped session 33 batch 2 (commit `0917e34`) as a hardcoded addition alongside Jump. Needs live repro on a Bomb monster: Self-Destruct should end the caster's turn without a Wait prompt. Wish / Blood Price still NOT in the set — per documentation comments their behavior varies by version; defer until live damage/turn data exists.

- [ ] **Live-verify weather damage modifier** — Session 31 shipped `WeatherDamageModifier` pure table (Rain→Lightning×1.25/Fire×0.75, Snow→Ice×1.25/Fire×0.75, Thunderstorm→Lightning×1.25). NOT yet wired into scan_move because the weather-state memory byte is unknown. Blocked on memory hunt. Validate the formula values AGAINST IC remaster once a rainy/snowy battle can be scanned. Wiki values are PSX-canonical.

- [ ] **Live-verify BattleModalChoice scaffold** — Session 31 shipped `BattleModalChoice` pure helper + 6 tests. NOT wired into detection (needs discriminator memory hunt). Next session find the modal-open byte during a BattleObjectiveChoice or RecruitOffer screen (story battle required — Orbonne Monastery probably has both).

- [ ] **Live-verify Reis name-lookup hardening** — Session 31 shipped `SelectBestRosterBase` low-address preference + `Invalidate()` on load action. Pure TDD only. Next recruit of a generic-named unit (Warriors' Guild) should resolve correctly to the new name, not collide with existing roster entries like "Reis".

### Session 29 — next-up follow-ups

- [ ] **Fix `battle_move` NOT CONFIRMED false-negative after next live repro** — Session 29 pt.4 added diagnostic logging. Next repro error message includes `lastScreenSeen=X polls=N` for the 8s poll window. Read that log. If `lastScreenSeen=BattleMoving` across all 80 polls, fix is in screen detection (`battleMode` byte likely stays 2 past the animation); add a second signal to distinguish "Move UI open" from "unit has not moved yet". If intermediate states appear in the log, expand the accept-list in `NavigationActions.cs` `MoveGrid`.

- [ ] **⚠ UNVERIFIED: `activeUnitSummary` on BattleMoving / BattleCasting / BattleAbilities / BattleActing / BattleWaiting** — Session 29 pt.1/2 widened the fft.sh compact renderer to match `Battle*` (was `Battle_*`). Confirmed on BattleMyTurn and BattleAttacking during session 29. Still needs a quick sweep — one call on each state during a battle and eyeball the line.

- [ ] **Diagnose `(10,4)` false-positive on Lloyd (Dragoon Mv=5 Jmp=4) at (10,9)** — Session 29 tile-cost rules match the game for 4 of 4 tested unit/position combos (Kenrick×3, Wilham×1). Untested: Lloyd's old FP scenario. Re-run at Siedge Weald with heap Move/Jump reading active and see if (10,4) is still over-reported. If still wrong, see `memory/project_bfs_tile_cost_rules.md` for what we've tried.

- [ ] **Rescue `cursor_walk` probe reliability (currently 5 of 20)** — `cursor_walk` counts `0x04 → 0x05` transitions in `0x140DDF000..0x140DE8000` between pre/post snapshots — only catches 5 of 20 valid tiles for Lloyd at Siedge Weald. Fix ideas: (a) widen range to cover mirror region `0x140F9xxxx`, (b) also count `0x00 → 0x05` and `0x01 → 0x05` transitions, (c) baseline "cursor on known valid tile" and compare the SET of 0x05 bytes that appear, not the count. Blocks automated BFS regression fixture building.

- [ ] **Find a byte (or compound signal) that encodes the game's valid-tile count** — Session 28 proved `0x142FEA008` is NOT the count. `LogBfsTileCountMismatch` call sites in `CommandWatcher.cs` are commented out waiting for a real signal. Plan: user visual count for 3-5 unit/map combos → module snapshot before/after Move mode entry → scan diff for any byte/u16 whose post-entry value matches the real count on ALL combos. `MoveTileCountValidator` + `DetectedScreen.BfsMismatchWarning` + shell `⚠` rendering are all in place, waiting.

### Session 27 — next-up follow-ups

- [ ] **New-recruit name resolves to "Reis" instead of Crestian** — 2026-04-17 session 27: user recruited a new generic character named Crestian at the Warriors' Guild. `NameTableLookup` resolves her slot-4 name bytes to "Reis" (matching an existing party member slot 6, Lv92 Dragonkin). The name in memory likely differs from what the lookup returned — possibly a collision in the PSX-compatible decoder, or a stale anchor-match pointing at the wrong table. Screenshots confirm game renders "Crestian" on her CharacterStatus header while shell says "Reis". Downstream: two units named "Reis" in roster, `GetSlotByDisplayOrder(14)` sometimes returns the wrong Reis for `viewedUnit` resolution, and `open_character_status Crestian` fails with "not found in roster". Test: recruit a generic with a name outside `CharacterData.StoryCharacterName`'s known set; verify NameTableLookup returns the actual recruited name (typed by player at the Guild) from the live name table rather than falling back to a story-character collision.

- [ ] **JP Next live-verify on Lv1 fresh recruit** — carryover from earlier attempt. Crestian (Lv1 Squire, JP 137) is the ideal test candidate for verifying the `Next: N` display. Fundaments cheapest unlearned should be Rush (80 JP) if nothing learned, or whichever unlearned ability is cheapest given what her 150→137 JP spend was (she must have learned Rush already; Next should then be Throw Stone at 90 JP). Blocked this session because the name-resolution bug (above) makes `open_character_status Crestian` fail, and navigating manually sometimes hits the wrong Reis due to displayOrder-vs-name ambiguity. Fix the name lookup first, then this becomes a one-line verification.

- [ ] **JobCursor resolver: find a byte that passes liveness on this save** — session 27 added a liveness probe, session 29 strengthened it to a 3-step Right probe AND session 29 pt.13 added bidirectional verify: after 3 Rights expect +3, then 3 Lefts should return to baseline. Change-count widgets (which increment on any nav) now fail phase 2. Still awaits a save where a truly-live cursor byte exists to validate the approach. Remaining to try if current save still 0 candidates: (a) different heap snapshot timing (maybe the byte settles AFTER the 700ms we wait), (b) resolve AFTER a Down/Up nav to stabilize widget state.

- [ ] **Zodiac: try heap-struct hunt (the 0x258 slot is confirmed empty)** — session 27 ruled out the static roster slot across 9 encodings (`memory/project_zodiac_not_in_slot.md`). Three productive next attempts documented: (a) oscillation diff while cycling PartyMenu sort order (if a "sort by zodiac" option exists), (b) reverse from battle damage math — set up a zodiac-opposite attacker/target pair, read damage modifier to back out both zodiacs, (c) dump HoveredUnitArray struct beyond +0x37 (currently we only decode HP/MP); zodiac may live in the per-hover widget at a higher offset.

- [ ] **Shop item-ID: retry with widget vtable walk** — session 27 confirmed the ID byte is not findable via snapshot-diff or contiguous-AoB on this save (`memory/project_shop_itemid_deadend.md`). Next path: find the OutfitterBuy widget's vtable via AoB, walk to its `HighlightedItem` field. Alternative: mod-side hook on the shop UI render callback to log the item ID being displayed. Either path is multi-session work; `find_toggle` bridge action (shipped session 27) is the reusable infra for the first fresh attempt.

### Session 23 — state stability + helper hardening

- [ ] **Verify open_* compound helpers across CHAIN calls** — Fresh-state runs work after this session's fixes (`open_character_status Agrias` from WorldMap → correct unit). But chained calls (open_eqa Cloud → open_eqa Agrias) still produce the viewedUnit-lag bug. SM-sync changes in `82ccb65` may or may not have resolved this; needs explicit live test sequence cycling 3 different units through each open_* helper and verifying state matches each request. Source: `NavigationActions.cs` `NavigateToCharacterStatus` rewrite, ~line 4419.

- [x] **Second SaveSlotPicker entry point from BattlePaused — DOES NOT EXIST (closed 2026-04-18 session 44)** — Live-verified: BattlePaused menu has 6 items (Data/Retry/Load/Settings/ReturnToWorldMap/ReturnToTitle) — no Save. See dup at line 100.

- [~] **`return_to_world_map` from battle states** — Session 26 added a state-guard refusing from Battle* / EncounterDialog / Cutscene / GameOver with a clear error pointing to the right recovery helper (battle_flee / execute_action ReturnToWorldMap). That closes the footgun. BattleVictory / BattleDesertion are NOT blocked because Escape/Enter on those screens legitimately advances toward WorldMap; they still need a live-verify at some point but the unsafe path is closed. Safe from all non-battle states (verified EqA/JobSelection/PartyMenuUnits tree + all non-Units tabs session 24; SaveSlotPicker verified session 26).


- [ ] **Per-key detection verification (replace blind sleeps)** — Long-term fix for compound nav reliability. Each transition key should poll detection until expected state appears, instead of fixed sleep. Bigger refactor; defer until current 350ms/1000ms approach proves stable across more scenarios.

### Session 22 — bridge actions + authoritative detection

- [ ] **C# bridge action viewedUnit lag on chain calls** — **Session 31 live repro (2026-04-17):** `open_job_selection Kenrick` from PartyMenuUnits landed on LLOYD's JobSelection, not Kenrick's. `open_eqa Lloyd` from PartyMenuUnits was a silent no-op (screen stayed on PartyMenu with cursor on Ramza). `change_job_to Archer` claimed success ("landed on EquipmentAndAbilities") but Lloyd's job was unchanged — helper returned success without the actual change going through. Pattern: open_* helpers frequently navigate to the WRONG unit, and state-mutation helpers sometimes report success without effecting the change. This blocks live-verify workflows that need to set up specific unit builds (Archer for LoS, Wizard for elemental affinity, etc.). Historical context below from sessions 22-24. `open_eqa Cloud` from WorldMap works perfectly (correct gear + name). But `open_eqa Agrias` from inside Cloud's EqA shows Agrias's gear but Cloud's name. Root cause: escape storm drift checks in DetectScreen reset `_savedPartyRow/_savedPartyCol` after `SetViewedGridIndex` sets them. Stashed fix in `git stash list`. Approach: suppress drift checks during bridge action execution (add a flag to CommandWatcher, check it in the drift-check blocks at lines ~4280-4395). Two lines of code. **Session 23 update: SM-sync from `SendKey` (commit 82ccb65) may have changed the symptom — re-test before applying the stashed approach.** **Session 24 update (2nd attempt):** live-reproduced the chain failure — `open_eqa Agrias` from Cloud's EqA fires escapes + Down + Enter that land on WorldMap and drive the WorldMap cursor (not the party grid), ending at LocationMenu instead of Agrias's EqA. Tried a fix in `NavigateToCharacterStatus` (replace detection-polling escape loop with a 2-consecutive-WorldMap-read confirmation). First attempt (unconditional 6 escapes) broke fresh-state by incorrectly assuming Escape-on-WorldMap is a no-op (it actually opens PartyMenu → toggling). Reverted. Second attempt (per-escape poll with 2-consecutive-WorldMap confirmation): not live-tested — during test setup the game CRASHED after landing on Cloud's EqA, possibly from the EqA-row auto-resolver firing at a bad animation moment. **Reverted all NavigationActions changes; code matches commit c5bfb01.** Key lesson: chain-nav + auto-resolvers together form a fragile timing sandwich; fixes need a safer repro harness than "kick it and pray" before retrying. Stash still exists for reference.

- [ ] **⚠ UNVERIFIED auto_place_units** — C# bridge action built (NavigationActions.cs) and shell helper wired, but never live-tested in an actual battle formation. Sequence: sleep 4s → 4×(Enter+Enter) → Space → Enter → poll for battle state. Session 23 added state guard that requires `BattleFormation` so misuse fails fast.


### Session 20 — state detection + EqA resolver

- [ ] **Battle state verification** — Session 21 verified 11/13 with screenshots: BattleMyTurn ✅, BattleMoving ✅, BattleAbilities ✅, BattleAttacking ✅, BattleWaiting ✅, BattleStatus ✅, BattlePaused ✅, BattleFormation ✅, BattleEnemiesTurn ✅, GameOver ✅, EncounterDialog ✅. BattleVictory ❌ and BattleDesertion ❌ misdetect as BattlePaused (see bug below). BattleDialogue and Cutscene blocked by sticky gameOverFlag (see bug below). BattleActing transient (hard to catch). BattleAlliesTurn needs guest allies.

- [ ] **LoadGame/SaveGame from title menu misdetect as TravelList** — Session 21: Both file picker screens (load and save) reached from title/pause menu have party=0, ui=1, slot0=0xFFFFFFFF, slot9=0xFFFFFFFF, battleMode=255, gameOverFlag=0. Matches TravelList rule (party=0, ui=1). Existing LoadGame rule only handles GameOver→LoadGame path (requires gameOverFlag=1, battleMode=0). SaveGame only handled as shop-type label (shopTypeIndex=4). Needs a discriminator byte — or accept the ambiguity since Claude uses `save`/`load` helpers which don't rely on screen detection.

- [ ] **BattleSequence detection: find memory discriminator** — Session 21 built full scaffolding (whitelist of 8 locations, NavigationPaths, SM sync, LocationSaveLogic) but detection DISABLED because BattleSequence minimap is byte-identical to WorldMap at those locations across all 29 detection inputs. Whitelist approach false-triggers on fresh boot/save load at sequence locations. Scaffolding ready in ScreenDetectionLogic.cs (commented out rule + `BattleSequenceLocations` HashSet). Next step: heap diff scan while ON the minimap vs WorldMap at same location to find a dedicated flag. Locations: Riovanes(1), Lionel(3), Limberry(4), Zeltennia(5), Ziekden(15), Mullonde(16), Orbonne(18), FortBesselat(21).

- [ ] **Cutscene misdetects as LoadGame after GameOver** — Session 21: sticky gameOverFlag=1 from prior GameOver causes LoadGame rule to preempt Cutscene. Inputs during real cutscene (eventId=2): gameOverFlag=1, battleMode=0, paused=0, actedOrMoved=false — all match LoadGame. Fix: LoadGame rule should additionally check that eventId is NOT in the real cutscene range (1-399), or Cutscene should be checked before LoadGame in the battle branch.


- [ ] **New state: BattleChoice — mid-battle objective choice screen** — Some battles pause and present 2 options (e.g. "We must press on, to battle" vs "Protect him at all costs"). Selecting an option changes the battle objective (e.g. from "defeat all" to "protect X"). Needs memory investigation to find a discriminator byte. Likely paused=1 with a unique submenu/menuCursor combo.

- [ ] **BattleVictory/BattleDesertion misdetect as BattlePaused** — Session 21 at Orbonne Monastery: slot0=0x67 (not 255) during Victory and Desertion screens. `unitSlotsPopulated` (slot0==255) is false, so `postBattle` and `postBattlePausedState` both fail, and the rules fall through to BattlePaused. Fix: relax the Victory/Desertion rules to not require unitSlotsPopulated — use `battleModeActive && actedOrMoved && battleMode == 0` instead. Inputs captured: party=1, ui=1, slot0=0x67, slot9=0xFFFFFFFF, battleMode=0, paused=1, submenuFlag=1, actedOrMoved=true, eventId=303.



- [ ] **EqA `ui=` shows stale cursor row** — `ui=Right Hand (none)` persists even when the game cursor is elsewhere because the SM's CursorRow only updates on key tracking (which drifts). `resolve_eqa_row` fixes it but costs 4 keypresses so can't run on every `screen` read.

- [ ] **Re-enable Chronicle/Options tab correction when both flags are 0** — Disabled 2026-04-16 because transient flag-clears during screen transitions caused spurious PartyMenuChronicle detection. When a Chronicle-vs-Options discriminator byte is found, re-enable. **Session 24 update:** module-memory snapshot+diff (chron→opts with chron→chron2 and opts→opts2 noise filters) surfaced `0x140900824`/`0x140900828` as promising candidates — stable 9 on Chronicle / 6 on Options within one game session. But they failed the restart test: post-restart Chronicle reads 8 (not 9) while Options stays 6. The value is a widget/load counter driven by navigation history, not a true tab discriminator. Approach for next attempt: (a) try heap diff with the `heap_snapshot` action (module snapshot misses UE4 widget state); (b) try the user's suggestion — pick an unused main-module byte and have the mod write `ScreenMachine.Tab` to it, use it as a write-back cache rather than an authoritative discriminator.

- [ ] **Replace fixed post-key delay with poll-until-stable** — Currently a fixed 350ms sleep in the detection fallback path. Replace with: read state, wait 50ms, read again, if identical return, else keep polling up to 500ms. **Session 24 note:** attempted but punted — timing refactors are hard to validate without a safe repro harness, and an unrelated crash during chain-nav testing showed how fragile the current timing sandwich is. Needs a way to measure "did a key drop?" without firing more keys. Consider wiring up the `[i=N, +Nms]` timing log into a per-call verify-before-advance pattern before retry. Alternative cheaper win: split `KEY_DELAY` into nav (200ms) vs transition (350ms+) per TODO #82.


### Earlier open items (EqA / JobSelection)












### Earlier open items

- [ ] **JP Next: live-verify with a partially-learned priced skillset** — Session 19 attempted verification via `change_job_to` but hit two blockers: (a) current save has every Lv99 generic mastered in every priced generic class, (b) `change_job_to` helper's JobSelection state-machine drift causes it to target the wrong unit (tried Kenrick → Arithmetician, landed on Ramza's JobSelection). Next session strategy: start a fresh game OR find a unit whose primary is priced AND partially learned. Alternatively, recruit a new generic at the Warriors' Guild (starts at low level, few learned abilities). Unit tests are solid (10 passing), so the blocker is purely the live state-machine drift in the JobSelection nav flow.




- [ ] **PartyMenu cursor row/col drift (b)** — `_savedPartyRow/Col` carries stale values across tab switches / multi-step nav. Session 16 repro: `OpenUnits → SelectUnit` reported `ui=Orlandeau` and drilled into Orlandeau's stats while game actually opened Ramza's. Quick mitigation: reset CursorRow/Col to 0 on any Q/E tab-switch. Real fix: read cursor from memory. `ResolvePartyMenuCursor` auto-fire disabled session 20 (8 keypresses, never finds byte). 5 candidate row bytes from session 18 UNTESTED.



- [ ] **JobSelection: live-verify Locked-state branch** — current save has a master unit so no cell renders as shadow silhouette. Need a fresh-game save (or temp dismiss-all-but-one) to verify the Locked branch end-to-end. Three-state classification itself shipped (commit 129f279).

- [ ] **JobSelection: improve Mime proxy to a real per-level prereq check** — session 24 moved Mime from hardcoded-Locked to a skillset-union proxy (checks Summon/Speechcraft/Geomancy/Jump unlocked on unit or party; see `JobGridLayout.ClassifyCell`). Proxy has false-positives: Mime renders Visible when the game would still Lock at <Lv 8 Squire/Chemist. Real fix requires per-class-level data (Squire Lv. 8, Chemist Lv. 8, Summoner Lv. 5, Orator Lv. 5, Geomancer Lv. 5, Dragoon Lv. 5 per `JobPrereqs["Mime"]`) — either read per-class job levels from roster memory or parse the JobPrereqs string at classify time.



- [ ] **JobSelection: live-verify generic male/female grids** — Squire at (0,0), Bard at (2,4) for males, Dancer at (2,4) for females — all inferred, not yet live-verified. Verify when a generic is recruited.



---

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

- [ ] **Add PreToolUse hook to block `| node` in bash commands** [Enforcement] — Claude should never pipe command output through node for parsing. All shell helpers (screen, execute_action, battle_attack, etc.) handle formatting internally. A Claude Code PreToolUse hook on Bash can detect `| node` in the command string and block it with a reminder to use the formatted helpers. Pending testing the unified screen command first.



- [x] **Live-verify `execute_action` responses include `ui=` field across all battle screens** [State] — dup of §0 entry closed in session 44. See partial `[~]` entry at line 84 for findings (BattleMyTurn/BattleMoving/BattleAbilities populate; BattlePaused/BattleStatus/BattleAttacking are null — three gaps that need cursor-to-label decode).


- [ ] **battle_ability first-scan null/null for secondary skillset** [Execution] — Primary detection works (Martial Arts secondaryIdx=9 for Lloyd verified); auto-scan catches misses on retry; all-skillsets fallback works. Remaining: first scan sometimes returns null/null before auto-scan fires — investigate race and eliminate the initial miss.


- [ ] **LoS option A: read game's projected hit% from memory during targeting** [Abilities] — **Blocked by session 30 finding** that hit% isn't findable via flat-memory AoB search — see `memory/project_damage_preview_hunt_s30.md`. LoS-via-memory now depends on the same widget-introspection or formula-compute path that damage preview needs. Prefer LoS option B (compute from map height data) until that path lands.

- [~] **LoS option B: compute LoS from map height data** [Abilities] — Session 31: shipped. `LineOfSightCalculator` (pure, DDA walk + linear altitude interp, 13 tests) + `ProjectileAbilityClassifier` (pure rule: ranged Attack + Ninja Throw qualify, spells/summons don't, 9 tests) + wire-up in `NavigationActions.AnnotateTile` populating `ValidTargetTile.LosBlocked`. Shell renders `!blocked` sigil. Needs live verify on a bow/gun/crossbow unit with terrain blocking a shot.

- [ ] **LoS option C: enter targeting, check if game rejects tile, cancel if blocked** [Abilities] — Brute-force fallback. Slow but reliable. Use only as last resort if A and B both fail.


- [ ] **Equipment IDs stale across battles** [State] — Roster equipment at `+0x0E` reads the save-state equipment, not the current in-battle loadout. Need to find the live equipment address.


- [ ] **Active unit name/job stale across battles** [State] — After restarting a battle with different equipment/jobs, the name/job display doesn't refresh between battles.


- [ ] **battle_move reports NOT CONFIRMED for valid moves** [Movement] — Navigation succeeds but F key confirmation doesn't transition. Timeout increased from 5s to 8s for long-distance moves.


- [ ] **Detect disabled/grayed action menu items** [Movement] — Need to find a memory flag or detect from cursor behavior.


- [ ] **Live-test `battle_retry` from GameOver screen** [Execution] — Code path exists, GameOver detection fixed. Needs in-game verification after losing a battle.



- [ ] **Find IC remaster deathCounter offset** [State] — KO'd units have 3 turns before crystallizing. PSX had this at ~0x58-0x59. Find IC equivalent in battle unit struct.


- [ ] **Find IC remaster chargingAbility + chargeCt bytes** [State] — Units charging a spell show in the Combat Timeline. Find which ability ID is queued and remaining CT for each charging unit. Needed to avoid wasted Silence/Stop attempts.



- [ ] **Read death counter for KO'd units** [State] — KO'd units have 3 turns before crystallizing. Need to find the IC equivalent of PSX offset ~0x58-0x59.


- [ ] **Detect charging/casting units** [Abilities] — Units charging a spell show in the Combat Timeline. Need to read charging state, which spell, and remaining CT from memory.



### Tier 4 — Known hard problems

- [ ] **Unit names — enemies** [Identity] — Enemy display names not found in memory. May need NXD table access or glyph-based lookup.


- [ ] **Zodiac byte memory hunt for generics/Ramza** [Identity] — Story-character zodiacs shipped session 19 via hardcoded nameId table (`ZodiacData.cs`, commit 1674bb6). Generics and Ramza return null. Hunt attempted offsets 0x00-0x100 with 4 anchor points (Agrias=Cancer, Mustadio=Libra, Orlandeau=Scorpio, Cloud=Aquarius); no match found. Retry strategies: (a) nibble-packed encoding — search half-bytes instead of bytes, (b) outside the 0x258 slot stride — try a parallel array, (c) non-zero-indexed ordering — try +1, +3, *2 variants.

- [ ] **Zodiac compatibility damage multiplier** [Combat] — Once zodiac is readable for every unit, wire `ZodiacData.GetOpposite` + Good/Bad compatibility tables into damage preview calculations. Multipliers per wiki: Best 1.5x, Good 1.25x, Bad 0.75x, Worst 0.5x. Requires projected-damage preview work (separate task) to ship first.


### Tier 5 — Speed optimization

- [ ] **`execute_turn` action** [Execution] — Claude sends full intent in one command: move target, ability, wait. One round-trip instead of 6+.


- [ ] **Support partial turns** [Execution] — move only, ability only, move+wait, etc.


- [ ] **Return full post-turn state** [Execution] — where everyone ended up, damage dealt, kills.



---

## 2. Story Progression (P0, BLOCKING)

- [ ] **Orbonne Monastery story encounter** — Loc 18 has a different encounter screen. Need to detect and handle it.





---

## 3. Travel System — Polish (P1)

- [ ] **Encounter polling reliability** — Encounters sometimes trigger before polling starts.


- [ ] **Ctrl fast-forward during travel** — Not working.


- [ ] **Resume polling after flee** — Character continues traveling after fleeing. Need to re-enter poll loop.


- [x] **Location address unreliable — ALREADY HANDLED (verified session 44 2026-04-18)** — `0x14077D208` does hold "last-passed-through node" during travel (live-confirmed: byte read 33 = Grogh Heights mid-transit when destination was 13 = Bervenia). But `ColorMod/GameBridge/LocationSaveLogic.cs` already handles the context: on WorldMap uses `hover` as authoritative, on EncounterDialog uses `rawLocation` (which IS the encounter location). CommandWatcher.cs:6742-6754 consumes the context-aware output via `GetEffectiveLocation`. No fix needed — the compensation exists and works.



---

## 4. Instruction Guides (P1)






---

## 5. Player Instructions & Rules (P1)

- [ ] Integrate PLAYER_RULES.md into Claude's system prompt / CLAUDE.md when playing — session 26 skipped (user wants to defer).


- [ ] Add intelligence level support (Beginner/Normal/Expert context files)


- [ ] Define how Claude should narrate: brief during action, summary after turns, debrief after battles


- [ ] Test that Claude actually follows the rules during gameplay



---

## 6. Intelligence Modes (P1)

### Mode 1 — Blind Playthrough ("First Timer")
- Only knows what's on screen. Discovers mechanics by experience.

### Mode 2 — Experienced Player ("Wiki Open")
- Full game mechanics loaded: damage formulas, ability ranges, zodiac chart, elements.

### Mode 3 — Min-Maxer ("Speedrunner") [Future]
- Optimizes party builds, ability combos, equipment loadouts.

---

## 7. Read Game Data from Memory (P1)

- [ ] **Investigate NXD table access** — The game stores all text strings in NXD database tables.


- [ ] **Unit names** — Read from CharaName NXD table keyed by NameId


- [ ] **Job/Ability/Equipment/Shop/Location names** — Read from memory instead of hardcoded lists



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

## 10. Settlements & Shopping (P2)

### Detection — what's mapped

### Detection — TODO


- [ ] **(NEXT) ValidPaths for the whole shop flow** [P0] — the automation unlock. Every shop screen needs a ValidPaths entry so Claude can drive the UI without knowing individual key sequences. Required entries:
  - **LocationMenu**: `EnterOutfitter`, `EnterTavern`, `EnterWarriorsGuild`, `EnterPoachersDen`, `Leave` (back to world map)
  - **ShopInterior** (shop menu): `Buy`, `Sell`, `Fitting`, `Leave`
  - **Outfitter_Buy**: `SelectItem <name>` (navigates to named item), `SetQuantity <n>`, `Purchase`, `Back`
  - **Outfitter_Sell**: `SelectItem <name>`, `SetQuantity <n>`, `Sell`, `Back`
  - **Outfitter_Fitting**: `SelectCharacter <name>`, `SelectSlot <slot>`, `SelectItem <name>`, `Equip`, `Back`
  - **Tavern / WarriorsGuild / PoachersDen**: fill in once sub-actions mapped
  - **Confirm dialogs**: `Confirm`, `Cancel` (requires the confirm-modal scan from below)




- [ ] **ui label at ShopInterior** — when hovering Buy/Sell/Fitting inside a shop without having entered, `screen.UI` should read `Buy`/`Sell`/`Fitting`. Needs a cursor-index memory scan (current shopSubMenuIndex is 0 at all three hovers). Once ui is populated, Claude can pre-check which sub-action it's about to enter.


- [ ] **Shop row→item name: implement scrape-on-demand by scrolling** — Blocked multi-approach investigation from 2026-04-14 (see git history for ShopItemScraper.cs). Best path forward: instead of one-shot extracting the whole list, loop scroll-cursor-down + read the ONE currently-highlighted item's FString (narrowed by short-string pattern + row memory state), advance. Trades a big scan for ~10 scrolls × 200ms = ~2s. Highlighted row widget may be more stable than enumerating all visible rows.

- [ ] **Shop row→item name: find stable chapter byte** — Shop stock varies per story chapter. Before row→name can be cached across sessions, we need a stable u8/u16 read that reports the current story chapter. Search near the story objective field at `0x1411A0FB6`.

- [ ] **Shop row→item name: document known master pools** — Memory notes for future hunters. Already verified: master item name pool at `0x6007BE4` (all weapons, UTF-16LE flat array with delimiter bytes); master item DB 72-byte stride around `0x5A1000`; shop record candidates near `0x8CB9FB0`. Not yet mappable to per-shop rows because UE4 FString allocations shift per-frame. Save to memory note file for next attempt.


- [ ] **Full stock list inline at Outfitter_Buy** — instead of forcing Claude to scroll through items one at a time (ui=Oak Staff → down → ui=White Staff → down...), surface the entire shop stock in the screen response. Each entry: `{name, price, type, stats}`. Stats tier by type — weapons: `wp, range, element, statMods` (e.g. `WP=5 MA+1`); armor: `hp, def, evade, statMods`; consumables: `effect` (e.g. `Restores 30 HP`, `Removes KO`). Claude picks by name, one round-trip. Matches scan_move's "see everything at once" philosophy.


(Tavern / TavernRumors / TavernErrands state tracking shipped session 26 pt.2 `d1e7160` — see archive.)

(Scope A shell helpers shipped session 32 — see archive. `enter_tavern`, `read_rumor [idx]`, `read_errand [idx]`, `scan_tavern` all live-verified at Dorter.)

- [ ] **Tavern: return body text from `read_rumor` / `read_errand`** — Session-32 investigation proved the body text is NOT in plain-string memory (0 UTF-8/UTF-16LE matches on distinctive phrases). Real source is `0002.en.pac/fftpack/world_wldmes_bin.en.bin` (3.96MB, PSX-encoded). The existing `MesDecoder.cs` handles the byte→char mapping. Needed: (a) parse the file's record structure to split rumor entries, (b) emit `claude_bridge/rumors.json` at mod startup, (c) wire `read_rumor` to look up the current row's body. Same pattern as `EventScriptLookup`. Errand metadata (quester / days / fee) likely in a parallel nxd layout — `tools/Nex/Layouts/ffto/Proposal*.layout` or similar; needs a separate extraction path.

- [ ] **scan_tavern: surface `cursorRow` on TavernRumors / TavernErrands** — `scan_tavern` currently reports ">=30 entries" placeholder because the bridge doesn't expose a cursor-row byte on these screens. Find the byte (likely a u8 on the heap widget) so the wrap-detection works cleanly.


- [ ] **Warriors' Guild: add `WarriorsGuild` screen state** — Sub-actions Recruit and Rename. Memory scan for shopSubMenuIndex values. ValidPaths: Recruit/Rename/Leave/CursorUp/Down.

- [ ] **Warriors' Guild: `Recruit` screen + flow** — Pick job/class + name new unit. Depends on party menu integration (new hire joins roster) and text input (naming).

- [ ] **Warriors' Guild: `Rename` screen + flow** — Pick existing unit + enter new name. Depends on party menu navigation + text input state.


- [ ] **Poachers' Den: add `PoachersDen` screen state** — Sub-actions Process Carcasses and Sell Carcasses. Memory scan for shopSubMenuIndex values. ValidPaths: Process/Sell/Leave/CursorUp/Down.

- [ ] **Poachers' Den: `ProcessCarcasses` screen + ValidPaths** — ScrollUp/Down/Select/Cancel. `ui=<carcass name>`; empty state when zero carcasses. Depends on carcass-name widget scraping.

- [ ] **Poachers' Den: `SellCarcasses` screen + payload** — Same nav as Process. Surface `heldCount` and `salePrice` per row. Depends on carcass-name widget scraping.


- [ ] **Save Game menu** — encountered at Warjilis (Dorter has 4 shops, no Save). Needs its own scan; verify if it shows up as a 5th shopTypeIndex value or a distinct flag. Add the index to the shop name mapping in CommandWatcher.cs.


- [ ] **Midlight's Deep stage selector** [LOW PRIORITY] — Midlight's Deep (location ID 22) is a special late-game dungeon. When you press Enter on the node, a vertical list of named stages appears (captured 2026-04-14: NOISSIM, TERMINATION, DELTA, VALKYRIES, YROTCIV, TIGER, BRIDGE, VOYAGE, HORROR, ...). The right pane renders a flavor-text description of the highlighted stage. This UI is structurally similar to Rumors/Errands but with its own screen name: `Midlight's_Deep` with `ui=<stage name>`. ValidPaths needed: ScrollUp/Down / Enter (commits to that stage → battle) / Back. Memory scans needed: the stage-name list (probably UE4 heap like shop items), the cursor row index (probably 0x141870704 reused), and a state discriminator for "inside Midlight's Deep node selector" vs just-standing-on-the-node. Defer until main story shopping/party/battle loops are stable — this only matters for end-game content.


- [ ] **Cursor item label inside Outfitter_Buy** — the `ui` field should show the currently-hovered item name (e.g. `ui=Oak Staff`). Memory scan needed for the item-cursor-index, then map index → item name via the shop's stock list. Same for Outfitter_Sell (your inventory) and Outfitter_Fitting (slot picker → item picker).


- [ ] **Cursor character label inside Outfitter_Fitting** — when picking which character to equip, ui should show `ui=Ramza` etc.


- [ ] **Confirm dialog detection** — most sub-actions have a "Buy 3 Potions for 60 gil?" yes/no modal. Memory scan for a flag that distinguishes confirm-modal-open vs item-list state, so input doesn't cascade into accidental purchase.



### Action helpers — TODO

- [ ] **`buy_item <name> [qty]` action** — current `buy` helper exists but probably needs updating once Outfitter_Buy item-cursor is mapped. Should: enter Buy submenu if not already, locate item by name in stock, navigate cursor, set quantity, confirm.


- [ ] **`sell_item <name> [qty]` action** — same, but for Sell submenu.


- [ ] **`equip_item <unit> <slot> <name>` action** — Outfitter_Fitting flow: pick character, pick slot, pick item, confirm.


- [ ] **`hire_unit [job]` action** — Warriors' Guild Recruit. May need to surface the random recruit's stats so Claude can decide.


- [ ] **`dismiss_unit <name>` action** — Warriors' Guild Dismiss.


- [ ] **`rename_unit <old> <new>` action** — Warriors' Guild Rename. Text input is hard; deferred until we have a key-to-character mapping helper.


- [ ] **`process_carcass <name>` action** — Poachers' Den Process Carcasses (turns monster carcass into rare item).


- [ ] **`sell_carcass <name>` action** — Poachers' Den Sell Carcasses.


- [ ] **`read_rumors` / `read_errands` action** — Tavern dialogue scrape. Returns the current rumor/errand text so Claude can react and decide.


- [ ] **`save_game [slot]` / `load_game [slot]` actions** — wraps the Save Game flow once detected.



### Shop-stock data — TODO

- [ ] **Read shop stock from memory** — each shop has an inventory of items it sells, varying by location and story progress. Find the stock array in memory so `buy_item` can reference items by name without hardcoding per-shop tables.





### Documentation

---

## 10.5. State Naming Convention (P1)


---

## 10.6. Party Menu (P1)

Captured 2026-04-14 from user screenshots. State machine already exists in `ScreenStateMachine.cs` but detection from memory only fires `PartyMenu` — all nested screens are inferred from key history and drift easily. Needs real memory-driven detection + ui labels + data surfacing.

### Party Menu hierarchy

```
PartyMenu ──Enter on unit──► CharacterStatus ──Enter on sidebar──► EquipmentAndAbilities / JobSelection / CombatSets
   │                              │                                     │
   │                              │                                     └──Enter on ability slot──► SecondaryAbilities / ReactionAbilities / SupportAbilities / MovementAbilities (Primary is job-locked, no-op)
   │                              │                                     └──Enter on equipment slot──► EquippableWeapons / EquippableShields / EquippableHeadware / EquippableCombatGarb / EquippableAccessories
   │                              └──Enter on Job──► JobSelection ──Enter──► JobActionMenu ──Right+Enter──► JobChangeConfirmation
   │                                                                    └──Left+Enter──► LearnAbilities (TBD)
   └──Tab change──► Inventory / Chronicle / Options
```

### Detection — TODO

- [ ] **`PartyMenuInventory` tab** — captured 2026-04-14 (SS3). Full item catalog the player owns across all categories (weapons, shields, helms, armor, accessories, consumables, carcasses, ...). Screenshot shows Weapons tab with columns `Item Name | Equipped/Held`. Right pane shows hover'd item's full description + WP/element/range/effect. State name: `PartyMenuInventory` with `ui=<item name>`. ValidPaths: ScrollUp/Down (Up/Down), NextCategory/PrevCategory (Right/Left cycle the sub-tab icons), ChangeDetailPage (V cycles the right-pane 1/3 stats → 2/3 bonuses+effects → 3/3 flags+jobs), Back (Escape → WorldMap), NextTab/PrevTab (wraps to Chronicle/Units). Memory scans needed: currently-hovered item identifier (to drive `ui=<item name>`), active inventory category, cursor row, page number.
  - **2026-04-15 session 16 partial investigation.** Attempted state-machine-only category tracking first; reverted because the game has **more than 6 categories** (a Carcasses sub-tab exists at minimum, possibly a generic bag / Beastmaster loot row too) and the labels don't match our guesses (game shows `Headwear`, not `Helms`). Mapping all categories requires live screenshot-per-position, not implemented this session.
  - Memory-hunt findings from the same session:
    - **Static item-name pool confirmed** around `0x3F18000`. Records pack `{ptr:8, length:4, utf16_chars, padding}`. Verified: Oak Staff at `0x3F18D0C`, Ragnarok at `0x3F18914`, White Staff immediately after Oak. Names are plain UTF-16 (not PSX-encoded), survive restart, single copy per item.
    - **Pointer-to-name search** (`FC 8C F1 03 00 00 00 00` = LE pointer to Oak Staff's record) found 1 heap match at `0x7886CE250` — but Ragnarok was hovered at the time, so this was **residual widget data** from a prior hover, not the current one. Implication: widget does NOT store a live pointer to the static name pool that tracks current hover.
    - **Direct UTF-16 string search** for "Ragnarok" while Ragnarok was hovered: only 1 hit (the static pool at `0x3F18914`). No heap copy of the current hovered item's name string gets allocated. The game likely renders by item ID → lookup into the static pool at render time.
    - **Direct UTF-16 string search** for "Oak Staff" AFTER hover moved to Gokuu's Pole: 2 hits (static + an old heap copy at `0x788671E28` for the previously-hovered item). Heap copies appear to be per-hover widget buffers that persist briefly, not stable.
  - **Recommended next approach**: heap-oscillation resolver on a hover byte (item ID or row index), same technique as `ResolveJobCursor` / `ResolvePickerCursor`. Oscillate Up/Down on the item list with cursor at a known stable position, find a byte that tracks the row index, then look up the item ID for that (category, row) pair in the static pool. Significant session to build; inventory items are variable-length per category so decoding the pool structure matters. Two prior sessions (2026-04-10 and 2026-04-14) hit similar walls on the inventory count hunt; this hover-ID hunt is narrower but still non-trivial.


- [ ] **`EquippableItemList` ui= cursor decode** — currently `EquipmentItemList`; needs cursor decode for the item list row. (`JobActionMenu` and `JobChangeConfirmation` ui= labels already shipped 2026-04-15.)


### Data surfacing — TODO

- [~] **Unit summary: HP/maxHP/MP/maxMP in `PartyMenu` roster entries** — **partial** session 26 2026-04-17. Wired `HoveredUnitArray.ReadStatsIfMatches` into the roster assembly path; confirmed live that the first 4 units get accurate HP/MaxHp/Mp/MaxMp (Ramza 719/719 MP 138/138 verified at Lv99). The other 10 units return null because the hovered-unit heap array only populates entries near the currently-hovered cursor — the array name ("hovered-unit") was accurate and the earlier class-level comment ("mirrors every active roster slot") was optimistic. Full roster-wide HP/MP requires the formula path: recompute from FFTPatcher job-base + equipment-bonus formulas using `ItemData.cs`. Deferred to a later session.


- [ ] **Full stat panel on `CharacterStatus` (verbose-only)** — the header shows attack/defense/magick/evade/movement/jump/zodiac/element stats. Toggled by `1` key (`statsExpanded` flag already shipped). Surface the actual numbers ONLY in `screen -v` JSON, NOT the compact line — these are build-planning data, not every-turn signals (per "What Goes In Compact vs Verbose vs Nowhere" principle). Decode in this order: Move, Jump, PA, MA, then evade/parry. Skip element resistances unless a build-decision flow demands them.


### ValidPaths — TODO

- [ ] **`Equippable_*` screens — `ChangePage` only** — the Tab key cycles item categories (Weapons / Shields / Helms / etc.) per the `<V> Change Page` hint. Add a named `ChangePage` validPath wrapping that key. ScrollUp/Down/Select/Cancel are the raw arrow / Enter / Escape keys and don't need named wrappers. **Note (2026-04-15 session 16 live test):** the actual tab-cycle keys on equipment pickers are **A** (previous) and **D** (next), NOT Tab. They wrap. Every equipment picker has at least 2 tabs — R/L Hand has 3 (`Equippable Weapons` / `Equippable Shields` / `All Weapons & Shields`), Helm/Body/Accessory have 2 (`Equippable *` / `All *`). Only the `All Weapons & Shields` tab displays grayed-out non-equippable items; all other tabs list only equippable items. The A/D keys may also control the PartyMenuInventory category strip (was previously assumed to be Right/Left — re-verify).



- [ ] **`EquippableWeapons` picker — full surface (session 16, active task).** Replace the stale `ui=<stale battle menu label>` with memory-backed cursor tracking + structured item list. Design per TODO §"What Goes In Compact vs Verbose vs Nowhere":
    - **Compact one-liner:** `[EquippableWeapons] viewedUnit=<name> equippedWeapon=<current> pickerTab=<tab name> ui=<hovered item>` — plus `willUnequipFrom=<unitName>` inserted before `ui=` when the hovered item is currently equipped on a DIFFERENT unit (decision aid for "am I about to steal from someone?").
    - **Below compact:** list of items in the current tab with a cursor marker:
        ```
        Equippable Weapons (6):
          cursor->  Ragnarok        Knight's Sword  WP24  Eq=self
                    Materia Blade   Knight's Sword  WP10
                    …
        ```
        `Eq=self` marker appears only when equipped on viewed unit; `Eq=<UnitName>` when equipped elsewhere; absent when unequipped.
    - **Nowhere in compact:** Held count (always ≥1 in list, zero decision value), Hit%/parry/element/range (hovering in-game reveals them), attribute bonuses / equipment effects / attack effects (verbose only), description text (flavor only), weapon-type flags (dual-wield / two-handed — verbose only).
    - **Verbose (`screen -v`):** `availableWeapons[]` JSON array, one record per row in the current tab. Fields: `row`, `name`, `type`, `wp`, `hit` (or `parry`), `element`, `range`, `attributeBonuses`, `equipmentEffects`, `attackEffects`, `canDualWield`, `canWieldTwoHanded`, `equippableByCurrentUnit`, `equippedCount`, `heldCount`, `equippedOn: [<unit names>]`. Dumped once per picker entry; tab switch re-queries. Fields for `attributeBonuses/equipmentEffects/attackEffects/canDualWield/canWieldTwoHanded` remain null until the `ItemInfo` extension (TODO §0) lands.
    - **Row-index resolver.** Live-verified 2026-04-15: the hovered row index lives at heap `0x12ECCF6B0` (plus 3 aliased copies at +0x78, +0xE0, +0x120). Build `ResolveEquippableItemCursor` mirroring `ResolveJobCursor` / `ResolvePickerCursor` — oscillate Down/Up, two-step verify, cache the address. Invalidate on every Up/Down (UE4 widget heap shuffles per row change).
    - **State-machine picker-tab field.** Track tab index via A (0x41) / D (0x44) key history, similar to the Options/Chronicle indexes. Reset on picker entry.
    - **Per-item equipped-on lookup.** Use the existing `RosterReader.ReadLoadout` decoder — for each item in the list, scan the roster for any unit whose `weapon/leftHand/shield/helm/body/accessory` name matches. Cheap (14 units × 6 slots = 84 comparisons per list render). Produces both the `Eq=<Unit>` compact marker and the verbose `equippedOn` array.
    - **Generalization hook:** this row-resolver + list-decode pattern transfers to (a) Outfitter_Buy / Outfitter_Sell cursor tracking, (b) PartyMenuInventory hover tracking. Those should be follow-ups using the same template.
    - Sub-items (check off as shipped this session):
      - [ ] `ResolveEquippableItemCursor` heap oscillation resolver.
      - [ ] Auto-trigger on first `screen` call after picker entry (gate on `MenuDepth == 2` + screen name in the EquippableWeapons/Shields/Headware/CombatGarb/Accessories set).
      - [ ] Invalidate cache on every Up/Down/A/D on a picker screen.
      - [ ] State-machine `PickerTab` index + A/D handling.
      - [ ] Compact `equippedWeapon=`, `pickerTab=`, `ui=<hovered item>` population via `screen.UI`.
      - [ ] Compact list rendering block in fft.sh with cursor marker + `Eq=` marker.
      - [ ] `willUnequipFrom=<unit>` marker logic.
      - [ ] `availableWeapons[]` verbose JSON array with the fields above (null for unpopulated ItemInfo fields).
      - [ ] Unit tests for state-machine PickerTab + the picker-tab enum + the RosterReader equipped-on lookup helper.
      - [ ] Live-verify cursor stays correct through Up→Down→A→D→Enter/Escape on Ramza's weapon picker.


- [ ] **Per-job equippability table** — added 2026-04-15 session 16. Source available in repo: `FFTHandsFree/Wiki/weapons.txt` and `armor.txt` have authoritative per-type job lists ("Knight's swords can be wielded by Ramza's gallant knight, knights, dark knights, ..."). Port to C# alongside `CharacterData.cs`.
  - **Use cases:** validation for `change_right_hand_to <name>` etc.; `availableWeapons[]` verbose catalog showing what Claude *could* equip; "All Weapons & Shields" tab grayed-state hints.
  - **Won't unblock `ui=<hovered item>`** — investigated 2026-04-15 session 16. Game's picker list is sorted by **per-player inventory storage order**, not item ID. Live-verified on Ramza: Equippable Weapons shows Ragnarok (id 36), Materia Blade (32), Chaos Blade (37), Blood Sword (23), Excalibur (35), Save the Queen (34) — equipped item first, then a non-ID order, includes BOTH Knight's Swords AND regular Swords (Ramza's job allows both). Inventory-order is the same UE4 pointer-chain blocker logged in `project_inventory_investigation.md`. Until that's solved, row index → name is impossible.





**EquipmentAndAbilities action helpers** — declarative one-liners that wrap the full nav flow. All helpers are locked to the EquipmentAndAbilities state (error elsewhere), idempotent (no-op if already equipped), and validate via `screen.availableAbilities`/inventory.

- [ ] **`change_right_hand_to <itemName>` helper** — stub, blocked on inventory reader wiring to picker row index.
- [ ] **`change_left_hand_to <itemName>` helper** — stub.
- [ ] **`change_helm_to <itemName>` helper** — stub.
- [ ] **`change_garb_to <itemName>` helper** — stub. (Game calls this "Combat Garb"/"Chest".)
- [ ] **`change_accessory_to <itemName>` helper** — stub.
- [ ] **`remove_equipment <slotName>` helper** — stub.
- [ ] **`dual_wield_to <leftWeapon> <rightWeapon>` helper** — requires Dual Wield support ability equipped.
- [ ] **`swap_unit_to <name>` helper** — from any nested PartyMenu screen, Q/E-cycle to named unit.
- [ ] **`clear_abilities` helper** — set Secondary/Reaction/Support/Movement all to (none).
  - [ ] `clear_abilities` — future, sets Secondary/Reaction/Support/Movement all to (none).



### Scan findings — what doesn't work for PartyMenu detection

Documented 2026-04-14 after spending ~45 min trying to find the tab-index and sidebar-index bytes via memory diff.

- **`module_snap` (main-module writable 0x140000000 range) does NOT contain PartyMenu UI state.** 4-way snapshots cycling through all 4 tabs produced only encounter-counter + camera-rotation false positives. Same result for CharacterStatus sidebar (Equipment→Job→CombatSets).
- **`heap_snapshot` (UE4 heap) contains the values but they're not stable.** Diffing gave 2029 candidates with the right shape (e.g. 0/1/2 for sidebar); strict filter narrowed to 36. Live re-verification showed ZERO of the 36 actually tracked the sidebar when cycled — the widget heap is reallocated per keypress, so addresses mean different things at different times.
- **What works:** `ScreenStateMachine` driven by key history (Q/E for tab, Up/Down for sidebar) reliably produces Tab + SidebarIndex. Use that to set `screen.Name` and `screen.UI` instead of scanning memory. This is what the current implementation does.
- **If you need this later (e.g. for robust recovery after state-machine drift):** consider (a) hooking the game's widget render function via DLL detour, (b) finding the PartyMenu widget's vtable and reading a stable field offset from each instance, or (c) parsing the `[FFT File Logger]` output which shows distinct `.uib` loads per screen (e.g. `ffto_bravestory_top.uib` loads when entering JobSelection). Naive byte-diff is the wrong tool.

### Known gaps

- [ ] **Rumors/Errands body text scrape depends on this** — Tavern Errands open the party menu for assigning units. Don't wire up errand acceptance until PartyMenu navigation is solid.


- [ ] **Recruit / Rename at Warriors' Guild depend on this** — both flows transition into the party menu for unit selection or text input. Same dependency.


- [ ] **Text input (for Rename)** — FFT lets you rename units letter-by-letter via an on-screen keyboard. Need a whole separate text-input state + action helper.



---

## 10.7. Chronicle Sub-Screen Inner States — DROPPED 2026-04-15 session 15

The Chronicle is lore content (Encyclopedia, factions map, cutscene replays, bestiary, errand log, lectures). All inner-state surfacing was reviewed against the "What Goes In Compact vs Verbose vs Nowhere" principle and dropped:

- A human player rarely consults Chronicle mid-playthrough.
- Each Chronicle interaction is a one-shot read with no downstream decision.
- Without inner-state surface, Claude can still navigate by cursor + raw `screen` calls if curiosity strikes.
- Outer boundary detection (each tile becomes its own screen state) is already shipped in §10.6 and is sufficient.

Reconsider any individual sub-screen only when a concrete decision flow requires it (e.g. "Claude needs to look up a faction location to plan a route").

`OptionsSettings` boundary detection stays per its existing entry. Inner Settings nav is unrelated to gameplay decisions and stays out of scope.

---

## 11. ValidPaths — Complete Screen Coverage (P2)

- [ ] Settlement menu, Outfitter, Tavern, Warriors' Guild, Poachers' Den


- [ ] Save/Load screens


---

## 12. Known Issues / Blockers


### Missing Screen States
- [ ] **Battle_Cutscene** — Mid-battle cutscenes. Need to distinguish from regular cutscenes.


- [ ] **SaveScreen / LoadScreen** — Indistinguishable from TitleScreen with static addresses.


- [ ] **Settlement** — Indistinguishable from TravelList with static addresses. Could use location-based heuristic.


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
- [x] **Reorder rules — ALREADY DONE (verified session 44 2026-04-18)** — ScreenDetectionLogic.cs review shows specific rules already precede the TitleScreen catch-all: PartyMenu (party==1) at line 233, EncounterDialog (encounterFlag!=0) at line 239, Cutscene (IsRealEvent + rawLocation==255) at line 244, WorldMap hover-location at line 251, and finally TitleScreen at line 264. LocationMenu check runs later at line 335 (after shop sub-actions) but before any catch-all. LoadGame at line 418 is in the in-battle fallback section by design. Incremental fixes across sessions 21-26 closed this without a dedicated sweep.


- [ ] **Remove `encA/encB`-dependent rules** — replace Battle_Victory / Battle_Desertion / EncounterDialog discriminators with stable signals (`paused`, `submenuFlag`, `acted/moved` combos).


- [ ] **Add `Battle_ChooseLocation` discriminator** — requires location-type annotation (which location IDs are multi-battle campaign grounds vs villages). Add to `project_location_ids_verified.md`.


- [x] **Scope `menuCursor` interpretation — ALREADY DONE (verified session 44 2026-04-18)** — CommandWatcher.cs:5025 gates the menuCursor → "Move"/"Abilities"/"Wait"/... label mapping on `screen.Name == "BattleMyTurn" || screen.Name == "BattleActing"`, which is STRICTER than the original "submenuFlag==0 && team==0" proposal (the screen name is already derived from those flags + more). BattleAbilities uses `_battleMenuTracker.CurrentItem` at line 5061; BattleAttacking/BattleCasting use `TargetingLabelResolver.ResolveOrCursor` at line 5048 (session 44). No scope violations remain.


- [ ] **Memory scan for WorldMap vs TravelList discriminator** — these are byte-identical in current 18 inputs. Need a menu-depth or focused-widget address.





### Coordinate System
- Grid coords and world coords are different systems
- Camera auto-rotates when entering Move mode or Attack targeting — always re-read rotation

### Bugs Found 2026-04-12
- [ ] **Ability list navigation: use counter-delta instead of brute-force scroll** — Currently presses Up×N to reset then Down×index. Could use counter-delta approach. **Session 31 attempt REVERTED live** after it broke Lloyd's Jump targeting: the cursor counter at `0x140C0EB20` reports NEGATIVE deltas on Up-wrap (observed: expected +3, got 0; expected +6, got -6; expected +9, got -24 — retry math exploded). The Down loop counter-delta works fine because it never wraps; the Up-reset can't because its whole purpose is to wrap-past-the-top. Future attempts need either (a) a different counter that's monotonic under wrap, (b) a stable cursor-position memory read, or (c) a retry heuristic that detects the negative-delta case and falls back to blind presses.


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

## 14. Mod Separation

- [ ] **Extract FFTHandsFree into its own Reloaded-II mod** — All the GameBridge code is piggybacked onto FFTColorCustomizer. Needs its own standalone mod project for public distribution.



---

## Low Priority / Deferred

- [ ] **Re-enable strict mode** [Execution] — Disabled. Re-enable once all gameplay commands are tested.

- [ ] **Remove `gameOverFlag==0` requirement from post-battle rules** — treat as sticky, use other signals. Deferred 2026-04-17 because reproducing requires losing a real battle to trigger GameOver — not cheap to set up. Re-prioritize once we're running battles regularly and this misdetection actually blocks a session (Cutscene→LoadGame collision after GameOver is the main documented symptom).

- [ ] **Live-verify JP Next Mettle costs** — Wiki-sourced values (Tailwind 150, Chant 300, Steel 200, Shout 600, Ultima 4000) are populated in [AbilityJpCosts.cs:38-42](ColorMod/GameBridge/AbilityJpCosts.cs#L38-L42). Can't verify on the current save because Ramza (only Mettle user — Gallant Knight is his unique class) has them all maxed: `nextJp=null` on his EqA confirms no unlearned abilities. To verify: either load a fresh-game save, or advance the main story on a new save to a point where Ramza still has unlearned Mettle entries. IC-remaster costs might differ slightly from Wiki values. Deferred until a suitable save exists.

- [ ] **Re-enable Ctrl fast-forward during enemy turns** [Execution] — Tested both continuous hold and pulse approaches. Neither visibly sped up animations. Low priority.



---

## Sources & References

- [fft-map-json](https://github.com/rainbowbismuth/fft-map-json) — Pre-parsed map terrain data (122 MAP JSON files)
- [FFT Move-Find Item Guide by FFBeowulf](https://gamefaqs.gamespot.com/ps/197339-final-fantasy-tactics/faqs/32346) — ASCII height maps for MAP identification
- [FFHacktics Wiki](https://ffhacktics.com/wiki/) — PSX memory maps, terrain format, scenario tables


---

## Completed — Archive

Completed `[x]` items live in [COMPLETED_TODO.md](COMPLETED_TODO.md). During the session-end `/handoff`, move newly-completed items from here into that file. Partial `[~]` items stay in this file in their original section.

