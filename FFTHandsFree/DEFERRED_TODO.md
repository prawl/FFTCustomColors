<!-- This file is exempt from the 200 line limit. -->
# FFT Hands-Free — Deferred TODO (post-V1)

> Split out of [TODO.md](TODO.md) on 2026-04-22 as part of the V1 battle-automation push. Everything here is real work — just not on the critical path to Claude being fully autonomous in battle. Re-promote individual items back to TODO.md once battle V1 ships.

## 0. Urgent Bugs


### Session 55 — shop stock follow-ups (2026-04-22)

- [ ] 🎯 **Active-widget pointer-chain walk for live shop record** — Session 55 live-verified at Ch1-early Dorter that the registry's "Ch1" data is actually for a LATER chapter state. Real Ch1-early Dorter shows 2 items per equipment tab (Rod+Oak Staff for Weapons; Escutcheon for Shields; Leather Cap+Plumed Hat for Headwear; Clothing+Leather Clothing for Body; Battle Boots for Accessories) and a totally different consumables set (status cures: Potion+Antidote+Eye Drops+Echo Herbs+Maiden's Kiss+Gold Needle+Phoenix Down). Decoder math is correct — when given the right bitmap manually (e.g. `00 02 02 00 00 00 00 00` for Weapons), it returns the right items at the live address (e.g. `0x15B60B5E0`). Fix path: find a stable vtable signature for the OutfitterBuy widget, walk pointer chain to active bitmap field, read live bytes directly. Session 54 notes mention shields=`80 92 93 CA FD 7F`, helms=`80 92 95 CA FD 7F` as IdArray vtable signatures — start there. See `memory/project_shop_stock_active_widget_hunt.md` and `memory/project_ch1_dorter_actual_stock.md`.

- [ ] **LiveShopScanner v1 too noisy — replace count-anchor heuristic** — Session 55 added experimental `seed_shop_stock description=auto` mode (commit `746144a`, `LiveShopScanner.cs`). It scans the active-widget heap range `0x15A000000..0x15D000000` for byte sequences matching `[bitmap N bytes][count u32]` where popcount==count and bits decode to valid item IDs. Live test at end-game Yardrow: false positives on `01 00 00 00` matches in random heap memory; same address returned for 3 different categories (impossible). Default seed_shop_stock still uses registry (works only at the chapter state baked in). Fix: replace with proper vtable-anchored read once the OutfitterBuy widget's vtable is found.

- [ ] ⚠ UNVERIFIED **Register Shields at non-Dorter shops** — session 54 live-confirmed Yardrow shields tab is EMPTY and only Dorter has a shield stock. Other 13 settlements not checked. Per-shop screenshot verification needed to either register each with `Ch1Shields` bitmap or leave out (if empty like Yardrow). See `ShopBitmapRegistry.cs` shields entry.

- [ ] ⚠ UNVERIFIED **Helms/Body/Accessories/Consumables at 13 non-verified shops** — session 54 registered all 15 settlements by analogy but only Dorter + Yardrow were screenshot-verified. Travel to each remaining shop (Lesalia/Riovanes/Eagrose/Lionel/Limberry/Zeltennia/Gariland/Gollund/Zaland/Goug/Warjilis/Bervenia/Sal Ghidos) and confirm each category's stock matches before relying on the auto-mode output.

- [ ] 🎯 **Goug 8-item weapons (Mythril Gun missing)** — Goug Ch1 displays 8 items (Bowgun/Knightslayer/Crossbow/Poison Bow/Hunting Bow/Gastrophetes/Romandan Pistol + Mythril Gun 17000). Only 7-bit bitmap `00 00 00 20 F8 01 00 00` exists in memory (missing Mythril Gun at id 72). Investigation needed: (a) walk heap widget pointer chain from the 7-bit record, (b) check for a second "chapter upgrade" record concatenated at render time, (c) verify if Mythril Gun is a true chapter-locked upgrade that won't show in end-game saves anyway. Registry currently excludes Goug weapons — users get "no record registered" error. See `memory/project_shop_stock_SHIPPED.md`. Likely solved by the active-widget pointer-chain walk above.

- [ ] **Per-chapter price verification at non-Dorter staves shops** — registered Ch1 discount prices (White Staff 400, Serpent Staff 1200) at Dorter/Yardrow/Gollund/Bervenia/Sal Ghidos based on Dorter/Yardrow screenshots. Other 3 staves shops (Gollund/Bervenia/Sal Ghidos) assumed to match Dorter without verification. Check when visiting each.

- [ ] **Chapter byte for shop auto-mode chapter discrimination** — session 54 auto-mode defaults `chapter=1` unless caller passes `unitIndex`. When Chapter 2+ save is available, snapshot-diff the transition to find the byte that goes `1 → 2`, then wire auto-read into `shop_stock`. See `memory/project_chapter_byte_hunt_deferred.md`.


### Session 49 — follow-ups (2026-04-20)


- [ ] **Hunt chapter byte for Ramza job disambiguation** — needs chapter-transition event for snapshot/diff. `CharacterData.GetRamzaJob(0x01)` currently picks Squire, but 0x01 can mean Ch2 Squire or something else in Ch3. At next chapter advance: snapshot at end of old chapter, snapshot at start of new chapter, diff for a byte going oldCh → newCh. See `memory/project_chapter_byte_hunt_deferred.md`.


### Session 46 — follow-ups (2026-04-19)


- [ ] **UserInputMonitor: live-verify under user-driven play** — Scaffold committed (0a19777) + bootstrap wire-up staged in working tree (ModBootstrapper.cs uncommitted) + deployed this session. Log confirms `[UserInputMonitor] started`. NOT verified that user keystrokes actually flow to the SM (user stepped away before testing). Next session: focus the game window, press Down on BattlePaused via keyboard (not bridge), then `screen` and confirm `ui=` tracks the user's cursor row. If it works, commit ModBootstrapper.cs. If it breaks things (double-counts bridge keys / lags / fires during non-game focus), revert ModBootstrapper.cs and debug the de-dup / focus-check logic in `UserInputMonitor.cs`.

- [ ] **WorldMap vs TravelList memory discriminator (optional)** — Session 46 confirmed they're byte-identical in current detection inputs (`hover=254, moveMode=255, party=0, ui=1, slot0=0xFFFFFFFF, slot9=0xFFFFFFFF` for both). Currently handled via `ResolveAmbiguousScreen` 4-arg overload: SM wins when `KeysSinceLastSetScreen==0 && !LastSetScreenFromKey`. Works but relies on SM staying in sync. A memory byte that distinguishes them would be more robust. Heap-diff between snapshots at identical external state might find one — prior attempt found 1.5M changed bytes because the "WorldMap" snapshot was accidentally PartyMenu. Retry with strict pre/post-snapshot visual confirmation.


### 🛠 Dev tooling — speed Ramza through battles for state-collection playthroughs


### Session 45 — new follow-ups (2026-04-19)


- [ ] ⚠ UNVERIFIED — **Re-check Dorter event 38 bubble count after the 2026-04-26 decoder rewrite** — `MesDecoder.DecodeBoxes` was reworked (commit `bcd6cc4`): trailing `0xFE`-run length = bubble count, sentences distribute by character balance, single AND multi-byte F8 are intra-bubble whitespace. Live-verified at event 045 (Eagrose Castle, FE×3 → 127/114/90 char split, FE×4 → 110/108/104/76) but event 38 (the original 45-bubble repro) hasn't been re-tested. Next time the cutscene runs: count game bubbles, compare to bridge's `boxCount`. See `project_dialogue_decoder_2026_04_26.md`.

- [ ] **Curated speakers for additional events** — `DialogueSpeakerOverrides.cs` (commit `cb465b7`) ships hand-curated `(eventId, boxIdx) → speaker` for event 045 only (29 boxes, all rotations covered). Every other event still falls through to the .mes-decoded speaker, which means `[narrator]` for any box without an `0xE3 0x08` marker. Process for adding a new event: play the cutscene, note who speaks each box from the on-screen portrait, append to `DialogueSpeakerOverrides._events`. Boxes 27 + 28 of event 045 are inferred-not-confirmed (the Larg/Dycedarg conspiracy exchange "The king's life hangs by a thread..." / "Indeed, my dear friend..."); spot-check on next playthrough and flip if wrong.

- [ ] ⚠ UNVERIFIED — **Live-pointer dialog speaker hunt — re-attempt with proper widget locator** — 2026-04-26 hunt found the active speaker pointer in session 1 at heap `0x133D1FA70` (mirror `0x133D1FB90`) → ASCII string in module table near `0x4E17629000`, verified Well-dressed Man / Dycedarg / Duke Larg in event 045. But session 2 (next game launch) the pointer was stale (heap address moves per session) AND the engine string-table layout was different (FString-with-length-prefix vs the simple null-separated table session 1 had). Pointer-search-then-validate discovery accumulates ~200 candidates that are all static FString-header references, not the dynamic widget. To crack: (a) walk the UE4 widget hierarchy from a known-stable static pointer, OR (b) decode the FText/FName indirection layer to find the widget's name field, OR (c) AOB-scan for a unique widget shape + extract its FString member. Code parked: `DialogueSpeakerReader` + `IsAddressReadable` + `ReadLiveSpeakerName` stub remain in `CommandWatcher.cs` for re-enabling. Memory addresses + structure notes in `project_dialogue_speaker_pointer.md`.

- [ ] **Event 41 starts mid-.mes file (compound event offset)** — Post-Zeklaus cutscene shows Ramza's "These sand rats are long in the slaying" but the .mes file's box 0 is the pre-battle Corpse Brigade Knight line. The game has a dialogue-offset byte somewhere. Our tracker resets to 0 on eventId change which is wrong for compound events that bundle pre+during+post battle text. Either: (a) find the offset byte in memory, or (b) scan for a "[START_MARKER]" in the file that signals post-battle text. Deferred.


### 🔴 State Detection — TOP PRIORITY (consolidated 2026-04-18)

User direction session 44: **refocus on state-related tasks. Bad state detection blocks everything else**. These are the known screen/state-detection bugs, ordered roughly by blast radius. Items cross-reference their detailed entries below.


- [ ] **LoadGame/SaveGame from title misdetect as TravelList** — Session 21: both file pickers have party=0, ui=1, matches TravelList rule. Currently works around via shell helpers but detection itself is wrong.

- [ ] **Chronicle/Options tab correction disabled** — Transient flag-clears caused spurious PartyMenuChronicle detection. Heap-diff scan or mod-side write-back cache needed.


### Session 43 — next-up follow-ups

- [ ] **Live-verify Gollund row 4 unmapped-error path via UI** — Session 42 wired Gollund row 3 → corpus #20 "The Haunted Mine" + row 4 → unmapped. Live-verified row 3 + UI cross-reference session 42. Row 4 verified via bridge but not UI (the "At Bael's End" body renders from a different data source we can't decode). When that source lands, seed Gollund row 4 and all 8 uniform cities' row 3.


### Session 33 batch 2 — deferred (needs live battle / environment I can't verify)


- [ ] **PreToolUse hook to block `| node` in Bash commands** — needs explicit per-hook user approval (per `feedback_no_hooks_without_approval.md`). Defer until user green-lights.


### Session 33 — next-up follow-ups (from 6-task batch attempt)

- [ ] **Wire TavernRumors cursorRow to screen response** — Session 33 found `0x13090F968` at Dorter. **Session 44 (2026-04-18)** confirmed the same `+0x28` widget offset holds at Bervenia — byte shifted to `0x13091F968` (widget base `0x13091F940`, +0x10000 from session 33). Triple-diff intersection (row0→1, 1→2, 2→3) + live-read verification is a RELIABLE per-session re-locator technique (yields ~5-7 candidates, narrowed to 1 by reading at current cursor). Widget header structure (self-pointer / count / tag / cursor at +0x28) is stable across both sessions. Still no stable anchor for AUTO-relocation at runtime — direct pointer-search for widget-base bytes returned 0 hits, confirming UE4 Slate vtable walk is needed. Memory note `project_tavern_rumor_cursor.md` updated with the full technique + next approaches.


### Session 33 — Tavern Scope B (decoder shipped; per-city mapping partial)

- [ ] **Some rumor bodies NOT in `world_wldmes.bin`** — "At Bael's End" (Dorter row 3) doesn't match any substring in the corpus. Searched all 318 .bin files in 0000-0004 pac dirs with the word "Bael" encoded PSX-style → zero hits. Likely lives in a UE4 `.locres` file or the `0002.en.pac/fftpack/world_snplmes_bin.en.bin` (same-ish size, different encoding — includes `D1/D2/D3` multibyte sequences suggesting kanji/Japanese layered with English). Next session: (a) try extracting text from `world_snplmes_bin.en.bin` with a multibyte decoder, (b) check UE4 `Paks/*.pak` for locres files, (c) scan bigger pac dirs (0005-0011).

- [ ] **Per-city row→corpus_index table — remaining cities** — As of session 43: 9 of 15 settlements seeded (Dorter/Gariland/Warjilis/Yardrow/Goug/Zaland/Lesalia/Bervenia uniform via `Chapter1UniformRows` + Gollund divergent at row 3 = corpus #20). Remaining: Riovanes(1), Eagrose(2), Lionel(3), Limberry(4), Zeltennia(5), Sal Ghidos(14) — most are battle-locked or story-progression-locked in Chapter 1. Seed them when Chapter 2+ unlocks access. If any city's uniform-rows break (Chapter-2 rumor refresh), split into per-chapter tables. Workflow in `FFTHandsFree/TavernRumorTitleMap.md`.

- [ ] **Candidate city-specific rumors to check at Chapter 2+** — Session 43 `CorpusCityMentionTests.cs` flagged corpus entries that name specific cities but don't appear at those cities' Chapter-1 taverns: #12 Warjilis (Baert Trading Company), #15 Lionel (Cardinal Delacroix), #23 Bervenia+Dorter (Wailing Orte). Re-check each of these cities' tavern lists after Chapter-2 story progress — likely rehost candidates.

- [ ] **Title→corpus auto-match UX** — `read_rumor "<phrase>"` requires Claude to know a distinctive phrase from the rumor. Titles are not in bridge state (not in RAM, not in decoded file). Session 34 `{locationId, unitIndex}` path is the cleanest alternative but requires knowing the cursor row. Remaining options: (a) screenshot + OCR the title region on TavernRumors entry, include as `rumorTitle` in `screen` response; (b) wire the TavernRumors cursor-row byte at `0x13090F968` via the pointer-chain walk (memory note `project_tavern_rumor_cursor.md`) so `screen` surfaces `cursorRow` automatically and `read_rumor` in the UI resolves via CityRumors instead of needing a phrase. (b) still blocked on unmappable Bael's-End class.

- [ ] **Errand metadata (quester / days / fee)** — separate source from rumor bodies. Likely in an NXD layout — `tools/Nex/Layouts/ffto/Book.layout` exists but no `book.nxd` was found in `0004/nxd/` during session 32 exploration. Possible paths: (a) the `Book` layout actually targets a data file not present in this installation, (b) errand metadata is in `world_wldmes_bin.en.bin` alongside the rumor bodies and shares record structure, (c) a different layout file (`Proposal*.layout`?) — session 32 didn't find one. Re-investigate after Scope B decoder is working; can piggyback on the same parser.



### Session 31 — next-up follow-ups (live-verify pending)


- [ ] **Live-verify Reis name-lookup hardening** — Session 31 shipped `SelectBestRosterBase` low-address preference + `Invalidate()` on load action. Pure TDD only. Next recruit of a generic-named unit (Warriors' Guild) should resolve correctly to the new name, not collide with existing roster entries like "Reis".


### Session 27 — next-up follow-ups


- [ ] **New-recruit name resolves to "Reis" instead of Crestian** — 2026-04-17 session 27: user recruited a new generic character named Crestian at the Warriors' Guild. `NameTableLookup` resolves her slot-4 name bytes to "Reis" (matching an existing party member slot 6, Lv92 Dragonkin). The name in memory likely differs from what the lookup returned — possibly a collision in the PSX-compatible decoder, or a stale anchor-match pointing at the wrong table. Screenshots confirm game renders "Crestian" on her CharacterStatus header while shell says "Reis". Downstream: two units named "Reis" in roster, `GetSlotByDisplayOrder(14)` sometimes returns the wrong Reis for `viewedUnit` resolution, and `open_character_status Crestian` fails with "not found in roster". Test: recruit a generic with a name outside `CharacterData.StoryCharacterName`'s known set; verify NameTableLookup returns the actual recruited name (typed by player at the Guild) from the live name table rather than falling back to a story-character collision.

- [ ] **JP Next live-verify on Lv1 fresh recruit** — carryover from earlier attempt. Crestian (Lv1 Squire, JP 137) is the ideal test candidate for verifying the `Next: N` display. Fundaments cheapest unlearned should be Rush (80 JP) if nothing learned, or whichever unlearned ability is cheapest given what her 150→137 JP spend was (she must have learned Rush already; Next should then be Throw Stone at 90 JP). Blocked this session because the name-resolution bug (above) makes `open_character_status Crestian` fail, and navigating manually sometimes hits the wrong Reis due to displayOrder-vs-name ambiguity. Fix the name lookup first, then this becomes a one-line verification.

- [ ] **JobCursor resolver: find a byte that passes liveness on this save** — session 27 added a liveness probe, session 29 strengthened it to a 3-step Right probe AND session 29 pt.13 added bidirectional verify: after 3 Rights expect +3, then 3 Lefts should return to baseline. Change-count widgets (which increment on any nav) now fail phase 2. Still awaits a save where a truly-live cursor byte exists to validate the approach. Remaining to try if current save still 0 candidates: (a) different heap snapshot timing (maybe the byte settles AFTER the 700ms we wait), (b) resolve AFTER a Down/Up nav to stabilize widget state.

- [ ] **Shop item-ID: retry with widget vtable walk** — session 27 confirmed the ID byte is not findable via snapshot-diff or contiguous-AoB on this save (`memory/project_shop_itemid_deadend.md`). Next path: find the OutfitterBuy widget's vtable via AoB, walk to its `HighlightedItem` field. Alternative: mod-side hook on the shop UI render callback to log the item ID being displayed. Either path is multi-session work; `find_toggle` bridge action (shipped session 27) is the reusable infra for the first fresh attempt.


### Session 23 — state stability + helper hardening

- [ ] **Verify open_* compound helpers across CHAIN calls** — Fresh-state runs work after this session's fixes (`open_character_status Agrias` from WorldMap → correct unit). But chained calls (open_eqa Cloud → open_eqa Agrias) still produce the viewedUnit-lag bug. SM-sync changes in `82ccb65` may or may not have resolved this; needs explicit live test sequence cycling 3 different units through each open_* helper and verifying state matches each request. Source: `NavigationActions.cs` `NavigateToCharacterStatus` rewrite, ~line 4419.

- [~] **`return_to_world_map` from battle states** — Session 26 added a state-guard refusing from Battle* / EncounterDialog / Cutscene / GameOver with a clear error pointing to the right recovery helper (battle_flee / execute_action ReturnToWorldMap). That closes the footgun. BattleVictory / BattleDesertion are NOT blocked because Escape/Enter on those screens legitimately advances toward WorldMap; they still need a live-verify at some point but the unsafe path is closed. Safe from all non-battle states (verified EqA/JobSelection/PartyMenuUnits tree + all non-Units tabs session 24; SaveSlotPicker verified session 26).


- [ ] **Per-key detection verification (replace blind sleeps)** — Long-term fix for compound nav reliability. Each transition key should poll detection until expected state appears, instead of fixed sleep. Bigger refactor; defer until current 350ms/1000ms approach proves stable across more scenarios.


### Session 22 — bridge actions + authoritative detection

- [ ] **C# bridge action viewedUnit lag on chain calls** — **Session 31 live repro (2026-04-17):** `open_job_selection Kenrick` from PartyMenuUnits landed on LLOYD's JobSelection, not Kenrick's. `open_eqa Lloyd` from PartyMenuUnits was a silent no-op (screen stayed on PartyMenu with cursor on Ramza). `change_job_to Archer` claimed success ("landed on EquipmentAndAbilities") but Lloyd's job was unchanged — helper returned success without the actual change going through. Pattern: open_* helpers frequently navigate to the WRONG unit, and state-mutation helpers sometimes report success without effecting the change. This blocks live-verify workflows that need to set up specific unit builds (Archer for LoS, Wizard for elemental affinity, etc.). Historical context below from sessions 22-24. `open_eqa Cloud` from WorldMap works perfectly (correct gear + name). But `open_eqa Agrias` from inside Cloud's EqA shows Agrias's gear but Cloud's name. Root cause: escape storm drift checks in DetectScreen reset `_savedPartyRow/_savedPartyCol` after `SetViewedGridIndex` sets them. Stashed fix in `git stash list`. Approach: suppress drift checks during bridge action execution (add a flag to CommandWatcher, check it in the drift-check blocks at lines ~4280-4395). Two lines of code. **Session 23 update: SM-sync from `SendKey` (commit 82ccb65) may have changed the symptom — re-test before applying the stashed approach.** **Session 24 update (2nd attempt):** live-reproduced the chain failure — `open_eqa Agrias` from Cloud's EqA fires escapes + Down + Enter that land on WorldMap and drive the WorldMap cursor (not the party grid), ending at LocationMenu instead of Agrias's EqA. Tried a fix in `NavigateToCharacterStatus` (replace detection-polling escape loop with a 2-consecutive-WorldMap-read confirmation). First attempt (unconditional 6 escapes) broke fresh-state by incorrectly assuming Escape-on-WorldMap is a no-op (it actually opens PartyMenu → toggling). Reverted. Second attempt (per-escape poll with 2-consecutive-WorldMap confirmation): not live-tested — during test setup the game CRASHED after landing on Cloud's EqA, possibly from the EqA-row auto-resolver firing at a bad animation moment. **Reverted all NavigationActions changes; code matches commit c5bfb01.** Key lesson: chain-nav + auto-resolvers together form a fragile timing sandwich; fixes need a safer repro harness than "kick it and pray" before retrying. Stash still exists for reference.



### Session 20 — state detection + EqA resolver


- [ ] **LoadGame/SaveGame from title menu misdetect as TravelList** — Session 21: Both file picker screens (load and save) reached from title/pause menu have party=0, ui=1, slot0=0xFFFFFFFF, slot9=0xFFFFFFFF, battleMode=255, gameOverFlag=0. Matches TravelList rule (party=0, ui=1). Existing LoadGame rule only handles GameOver→LoadGame path (requires gameOverFlag=1, battleMode=0). SaveGame only handled as shop-type label (shopTypeIndex=4). Needs a discriminator byte — or accept the ambiguity since Claude uses `save`/`load` helpers which don't rely on screen detection.

- [ ] **EqA `ui=` shows stale cursor row** — `ui=Right Hand (none)` persists even when the game cursor is elsewhere because the SM's CursorRow only updates on key tracking (which drifts). `resolve_eqa_row` fixes it but costs 4 keypresses so can't run on every `screen` read.

- [ ] **Re-enable Chronicle/Options tab correction when both flags are 0** — Disabled 2026-04-16 because transient flag-clears during screen transitions caused spurious PartyMenuChronicle detection. When a Chronicle-vs-Options discriminator byte is found, re-enable. **Session 24 update:** module-memory snapshot+diff (chron→opts with chron→chron2 and opts→opts2 noise filters) surfaced `0x140900824`/`0x140900828` as promising candidates — stable 9 on Chronicle / 6 on Options within one game session. But they failed the restart test: post-restart Chronicle reads 8 (not 9) while Options stays 6. The value is a widget/load counter driven by navigation history, not a true tab discriminator. Approach for next attempt: (a) try heap diff with the `heap_snapshot` action (module snapshot misses UE4 widget state); (b) try the user's suggestion — pick an unused main-module byte and have the mod write `ScreenMachine.Tab` to it, use it as a write-back cache rather than an authoritative discriminator.


### Earlier open items (EqA / JobSelection)













### Earlier open items

- [ ] **JP Next: live-verify with a partially-learned priced skillset** — Session 19 attempted verification via `change_job_to` but hit two blockers: (a) current save has every Lv99 generic mastered in every priced generic class, (b) `change_job_to` helper's JobSelection state-machine drift causes it to target the wrong unit (tried Kenrick → Arithmetician, landed on Ramza's JobSelection). Next session strategy: start a fresh game OR find a unit whose primary is priced AND partially learned. Alternatively, recruit a new generic at the Warriors' Guild (starts at low level, few learned abilities). Unit tests are solid (10 passing), so the blocker is purely the live state-machine drift in the JobSelection nav flow.



- [ ] **PartyMenu cursor row/col drift (b)** — `_savedPartyRow/Col` carries stale values across tab switches / multi-step nav. Session 16 repro: `OpenUnits → SelectUnit` reported `ui=Orlandeau` and drilled into Orlandeau's stats while game actually opened Ramza's. Quick mitigation: reset CursorRow/Col to 0 on any Q/E tab-switch. Real fix: read cursor from memory. `ResolvePartyMenuCursor` auto-fire disabled session 20 (8 keypresses, never finds byte). 5 candidate row bytes from session 18 UNTESTED.



- [ ] **JobSelection: live-verify Locked-state branch** — current save has a master unit so no cell renders as shadow silhouette. Need a fresh-game save (or temp dismiss-all-but-one) to verify the Locked branch end-to-end. Three-state classification itself shipped (commit 129f279).

- [ ] **JobSelection: improve Mime proxy to a real per-level prereq check** — session 24 moved Mime from hardcoded-Locked to a skillset-union proxy (checks Summon/Speechcraft/Geomancy/Jump unlocked on unit or party; see `JobGridLayout.ClassifyCell`). Proxy has false-positives: Mime renders Visible when the game would still Lock at <Lv 8 Squire/Chemist. Real fix requires per-class-level data (Squire Lv. 8, Chemist Lv. 8, Summoner Lv. 5, Orator Lv. 5, Geomancer Lv. 5, Dragoon Lv. 5 per `JobPrereqs["Mime"]`) — either read per-class job levels from roster memory or parse the JobPrereqs string at classify time.



- [ ] **JobSelection: live-verify generic male/female grids** — Squire at (0,0), Bard at (2,4) for males, Dancer at (2,4) for females — all inferred, not yet live-verified. Verify when a generic is recruited.



---


## 1. Battle Execution — carryover (from deferred split)

### Tier 3 — Robustness

Turn-state recovery, edge case handlers, multi-unit battle reliability.


- [ ] **Add PreToolUse hook to block `| node` in bash commands** [Enforcement] — Claude should never pipe command output through node for parsing. All shell helpers (screen, execute_action, battle_attack, etc.) handle formatting internally. A Claude Code PreToolUse hook on Bash can detect `| node` in the command string and block it with a reminder to use the formatted helpers. Pending testing the unified screen command first.





## 3. Travel System — Polish (P1)

- [ ] **Encounter polling reliability** — Encounters sometimes trigger before polling starts.


- [ ] **Ctrl fast-forward during travel** — Not working.


- [ ] **Resume polling after flee** — Character continues traveling after fleeing. Need to re-enter poll loop.




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


(Shop row→item name: `shop_stock` bridge action ships session 54 — `ShopStockDecoder` + `ShopBitmapRegistry` + `ChapterShopPrices`. Auto-mode returns full stock + prices for all 6 categories at 14 of 15 Ch1 settlements. Follow-ups under §0 Urgent Bugs session 54 block.)

(Full stock list inline at Outfitter_Buy: pending wire-up — `screen.stockItems` population — see §0 session-54 follow-ups.)


(Tavern / TavernRumors / TavernErrands state tracking shipped session 26 pt.2 `d1e7160` — see archive.)

(Scope A shell helpers shipped session 32 — see archive. `enter_tavern`, `read_rumor [idx]`, `read_errand [idx]`, `scan_tavern` all live-verified at Dorter.)

- [ ] **Tavern: return body text from `read_rumor` / `read_errand`** — Session-32 investigation proved the body text is NOT in plain-string memory (0 UTF-8/UTF-16LE matches on distinctive phrases). Real source is `0002.en.pac/fftpack/world_wldmes_bin.en.bin` (3.96MB, PSX-encoded). The existing `MesDecoder.cs` handles the byte→char mapping. Needed: (a) parse the file's record structure to split rumor entries, (b) emit `claude_bridge/rumors.json` at mod startup, (c) wire `read_rumor` to look up the current row's body. Same pattern as `EventScriptLookup`. Errand metadata (quester / days / fee) likely in a parallel nxd layout — `tools/Nex/Layouts/ffto/Proposal*.layout` or similar; needs a separate extraction path.

- [~] **scan_tavern: surface `cursorRow` on TavernRumors / TavernErrands** — Task 23 shipped (session 44 pt 7): `ResolveTavernCursor` method + auto-resolve wiring. Gate uses `ScreenMachine.CurrentScreen == TavernRumors/Errands` (not screen.Name) because no memory byte distinguishes Tavern substates from LocationMenu — SM-override rewrites the name only at response serialization, after the resolver runs. Live-verified at Bervenia: cursorRow populates on first visit, scan_tavern emits concrete counts (was ">=30" placeholder). **Known limitation**: same 2-step verify + 32-candidate-first-match issue as BattlePaused (Task 21) — the latched byte reports initial row correctly but doesn't reliably track live navigation. At Bervenia scan_tavern reported "2 entries" when menu had 4. **Future work**: strengthen discrimination via triple-diff intersect OR N-range value filter (candidate byte values must stay within [0, expected-entry-count)).


- [ ] **Warriors' Guild: `Recruit` screen + flow** — Pick job/class + name new unit. Depends on party menu integration (new hire joins roster) and text input (naming).

- [ ] **Warriors' Guild: `Rename` screen + flow** — Pick existing unit + enter new name. Depends on party menu navigation + text input state.


- [ ] **Poachers' Den: `ProcessCarcasses` screen + ValidPaths** — ScrollUp/Down/Select/Cancel. `ui=<carcass name>`; empty state when zero carcasses. Depends on carcass-name widget scraping.

- [ ] **Poachers' Den: `SellCarcasses` screen + payload** — Same nav as Process. Surface `heldCount` and `salePrice` per row. Depends on carcass-name widget scraping.


- [ ] **Save Game menu** — encountered at Warjilis (Dorter has 4 shops, no Save). Needs its own scan; verify if it shows up as a 5th shopTypeIndex value or a distinct flag. Add the index to the shop name mapping in CommandWatcher.cs.


- [ ] **Midlight's Deep stage selector** [LOW PRIORITY] — Midlight's Deep (location ID 22) is a special late-game dungeon. When you press Enter on the node, a vertical list of named stages appears (captured 2026-04-14: NOISSIM, TERMINATION, DELTA, VALKYRIES, YROTCIV, TIGER, BRIDGE, VOYAGE, HORROR, ...). The right pane renders a flavor-text description of the highlighted stage. This UI is structurally similar to Rumors/Errands but with its own screen name: `Midlight's_Deep` with `ui=<stage name>`. ValidPaths needed: ScrollUp/Down / Enter (commits to that stage → battle) / Back. Memory scans needed: the stage-name list (probably UE4 heap like shop items), the cursor row index (probably 0x141870704 reused), and a state discriminator for "inside Midlight's Deep node selector" vs just-standing-on-the-node. Defer until main story shopping/party/battle loops are stable — this only matters for end-game content.


- [ ] **Cursor item label inside Outfitter_Buy** — the `ui` field should show the currently-hovered item name (e.g. `ui=Oak Staff`). Session 54 infrastructure is ready: cursor row at `0x141870704` (u32), `ShopStockDecoder` returns ordered stock via `DecodeStockAt`. Wire `ui = stock[cursorRow].Name` on `OutfitterBuy`. Same path extends to Outfitter_Sell (map row → player inventory id) and Outfitter_Fitting (row → character / slot / item picker).


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

(Core shop-stock-from-memory work shipped session 54. `shop_stock` bridge action + `ShopStockDecoder` + `ShopBitmapRegistry` cover 14 of 15 settlements × 6 categories. Remaining follow-ups under §0 Urgent Bugs.)






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
- [ ] **`swap_unit_to <name>` shell wrapper** — `UnitCyclePlanner.Plan(fromIndex, toIndex, rosterCount)` pure planner shipped session 47. Still needed: a shell helper that resolves `name → displayOrder` via roster, reads current viewedUnit, feeds both into UnitCyclePlanner, dispatches the Q/E key sequence. One-liner once the planner is available.
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
<!-- Battle_Cutscene: REMOVED (user decision 2026-04-18 session 44).
     Simpler two-state model: Cutscene (pre/post-battle, out of combat) vs
     BattleDialogue (mid-combat scripted text). Do not re-introduce a
     third state for "mid-battle cinematic" — treat those as BattleDialogue. -->




- [ ] **SaveScreen / LoadScreen** — Indistinguishable from TitleScreen with static addresses.


- [ ] **Settlement** — Indistinguishable from TravelList with static addresses. Could use location-based heuristic.



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

- [ ] **Memory scan for WorldMap vs TravelList discriminator** — these are byte-identical in current 18 inputs. Need a menu-depth or focused-widget address.






## 14. Mod Separation

- [ ] **Extract FFTHandsFree into its own Reloaded-II mod** — All the GameBridge code is piggybacked onto FFTColorCustomizer. Needs its own standalone mod project for public distribution.



---


## Low Priority / Deferred


- [ ] **Live-verify JP Next Mettle costs** — Wiki-sourced values (Tailwind 150, Chant 300, Steel 200, Shout 600, Ultima 4000) are populated in [AbilityJpCosts.cs:38-42](ColorMod/GameBridge/AbilityJpCosts.cs#L38-L42). Can't verify on the current save because Ramza (only Mettle user — Gallant Knight is his unique class) has them all maxed: `nextJp=null` on his EqA confirms no unlearned abilities. To verify: either load a fresh-game save, or advance the main story on a new save to a point where Ramza still has unlearned Mettle entries. IC-remaster costs might differ slightly from Wiki values. Deferred until a suitable save exists.

- [ ] **Live-verify Cutscene-over-LoadGame fix** [State] — Session 31 shipped the `eventId ∈ {0, 0xFFFF}` guard on the LoadGame detection rule. Unit tests cover the transition; needs a live repro where `gameOverFlag=1` is sticky (e.g. after a GameOver → load save) while a real cutscene plays. Expected: screen reads `Cutscene` not `LoadGame`. Deferred (user 2026-04-18) — reproduction is expensive (force a party wipe + load + walk into a cutscene) and the unit test coverage plus the underlying symptom being rare (only fires after an un-recovered-from GameOver) makes this low payoff. Do not pick up again without a direct user ask.
