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

### Session 28 — battle-task findings

- [ ] **`screen` / `BattleMyTurn` response should include the active unit's name** — 2026-04-17 session 28 live at The Siedge Weald. `screen` on `BattleMyTurn` reports only `[BattleMyTurn] ui=Move curLoc=... obj=...`. Whose turn is it? Unknown from the response alone — Claude has to guess or `scan_move` (~heavy). The active unit's name (and ideally job / HP / MP) is the single most useful piece of context for deciding the next action. `scan_move` already surfaces this (`Wilham(Monk) (10,10) HP=477/477 MP=115/115`) — it should appear on the compact `screen` line too for any `BattleMyTurn` / `BattleActing` / `BattleMoving` / `BattleAttacking` / `BattleAbilities` / `Battle_*` state. Example desired compact format: `[BattleMyTurn] Wilham(Monk) (10,10) HP=477 ui=Move`. Likely lives alongside the existing `ui=` populator in `ScreenDetectionLogic` / `CompactScreenFormatter`.

- [ ] **`battle_move` reports "NOT CONFIRMED (timeout)" but the unit actually moved** — 2026-04-17 session 28 live-repro. Called `battle_move 10 9` with Wilham at (10,10). Helper returned `[BattleMoving] failed: (10,10)->(10,9) NOT CONFIRMED (timeout)`. BUT: immediately after, mod logs show `Unit=(10,9)` — so the move *succeeded* in-game. The game registered the move, the cursor-confirmation poll missed it, and the shell reports `BattleMoving` still (because the `F` confirm didn't advance state machine OR the state machine didn't observe the transition in time). Claude then assumes failure and tries to recover, but the turn is already half-consumed. Two possible root causes: (a) F-key timing — press happens, but the poll window closes before the game's transition animation reaches the state-machine; (b) state-machine's notion of "confirmed" requires a different signal than what the game emits on a same-tile-move. Screenshot at `screenshot_20260417_091931.png` confirms cursor was positioned on target tile, game was still rendering move selection UI. Reproduces reliably on turn 1 after `auto_place_units` settles. Captured during session 28 battle-task verification pass.

- [ ] **`ui=` missing from `BattleMoving` and `BattleAttacking` compact responses** — 2026-04-17 session 28 live-verify of TODO line 225 (session 22 item). Data points captured this session: `BattleMyTurn ui=Move` ✅, `BattleAbilities ui=Attack` ✅, `BattleMoving` **no ui** ❌, `BattleAttacking` **no ui** ❌. Code path exists in `ScreenDetectionLogic.DetectScreen` but either the UI populator doesn't set it for these two states, or the compact formatter drops it. Verification was blocked further by the `battle_move` timeout bug (above). Check `ScreenDetectionLogic.cs` for the branch that builds compact output for `BattleMoving` / `BattleAttacking` — these should surface the target-tile cursor position (`ui=(x,y)` or similar) so Claude sees "am I about to confirm on the right tile?".

### Session 27 — next-up follow-ups

- [ ] **New-recruit name resolves to "Reis" instead of Crestian** — 2026-04-17 session 27: user recruited a new generic character named Crestian at the Warriors' Guild. `NameTableLookup` resolves her slot-4 name bytes to "Reis" (matching an existing party member slot 6, Lv92 Dragonkin). The name in memory likely differs from what the lookup returned — possibly a collision in the PSX-compatible decoder, or a stale anchor-match pointing at the wrong table. Screenshots confirm game renders "Crestian" on her CharacterStatus header while shell says "Reis". Downstream: two units named "Reis" in roster, `GetSlotByDisplayOrder(14)` sometimes returns the wrong Reis for `viewedUnit` resolution, and `open_character_status Crestian` fails with "not found in roster". Test: recruit a generic with a name outside `CharacterData.StoryCharacterName`'s known set; verify NameTableLookup returns the actual recruited name (typed by player at the Guild) from the live name table rather than falling back to a story-character collision.

- [ ] **JP Next live-verify on Lv1 fresh recruit** — carryover from earlier attempt. Crestian (Lv1 Squire, JP 137) is the ideal test candidate for verifying the `Next: N` display. Fundaments cheapest unlearned should be Rush (80 JP) if nothing learned, or whichever unlearned ability is cheapest given what her 150→137 JP spend was (she must have learned Rush already; Next should then be Throw Stone at 90 JP). Blocked this session because the name-resolution bug (above) makes `open_character_status Crestian` fail, and navigating manually sometimes hits the wrong Reis due to displayOrder-vs-name ambiguity. Fix the name lookup first, then this becomes a one-line verification.

- [ ] **Remove the chain-guard hard block; keep the auto-delay** — 5 prior attempts to block chained shell calls have all caused collateral false-positives without stopping real races. Evidence from session 27: chained Bash calls like `source ./fft.sh && right && sleep 0.6 && screen` worked reliably across dozens of iterations — no detectable races, no missed keypresses, SM states matched game state, memory reads verified cleanly. The single-threaded bridge already sequences game-affecting commands, and the bridge-side auto-delay (`[CHAIN WARNING]` path) handles the narrow case where two key-sending commands arrive too fast. The hard-exit chain guard is belt-and-suspenders that catches legitimate flows.
  - **What to remove:** the disk `claude_bridge/fft_done.flag` kill-path in `fft.sh` that produces `[NO] Only call one command at a time. Do not chain commands.` and the `kill -9 $$` it triggers. Keep `_track_key_call` as a telemetry counter if useful for the `SessionCommandLog`.
  - **What to keep:** the bridge-side auto-delay (already active). If a genuine race surfaces, the session log captures it and we add a targeted fix for that specific case rather than a global block.
  - **Expected wins:** faster iteration (no 3-call splits when 1 works), no false "[NO]" during debugging, cleaner read-heavy flows. Cost: theoretical loss of a safety net that hasn't actually caught real bugs in live sessions.
  - **Audit before removing:** grep the codebase for every `_FFT_DONE`, `fft_done.flag`, `_fft_reset_guard`, `_is_key_sending` reference — 34 sites were rewritten in session 25 to reset this guard, they all become no-ops when the block is removed. Also re-read `feedback_no_auto_loops.md` / `feedback_one_at_a_time.md` memory notes — those constraints are about unbounded loops and one-step-at-a-time gameplay pacing (good), not about chained Bash helpers (bad block). Leave those intact.

- [ ] **Wire the dry-run nav harness into the real `NavigateToCharacterStatus`** — session 27 shipped `NavigationPlanner.PlanNavigateToCharacterStatus` as a pure function, plus a `dry_run_nav` bridge action that prints the key sequence without firing. The live `NavigationActions.NavigateToCharacterStatus` still has its own hardcoded copy of the plan. Next: make the live method *consume* the planner (so the plan can be logged before each live run, and any plan bug fixed in one place). Also add a `--dry-run` or `dryRun=true` flag path to `open_eqa` / `open_character_status` / `open_job_selection` helpers so users can call `open_eqa Agrias dry-run` to preview without firing. Unblocks the crashy chain-nav scenario safely.

- [ ] **JobCursor resolver: find a byte that passes liveness on this save** — session 27 added a liveness probe to `ResolveJobCursor` that catches widget-counter candidates that track oscillation but not real nav. On the current save, ALL 32 candidates failed liveness (see `memory/project_jobcursor_liveness.md`). Drift correction is plumbed but can't fire without a live-tracking byte. Next-session approaches: (a) try heap_snapshot instead of module-memory snapshot (widget state lives in UE4 heap), (b) narrow the 2-step verify to +1 AND +1 (not +1 and +2) in case the byte is a "changed count" rather than absolute position, (c) try multi-key-sequence verify (Right×3, expect +3) to catch candidates that only respond to the first key.

- [ ] **EqA row resolver: re-fire on detect-drift events** (session 24 carryover) — auto-resolver already fires once on EqA entry (`_eqaRowAutoResolveAttempted` latch, session 24 pt.3) and sets `ScreenMachine.SetEquipmentCursor(resolvedRow)`. But it ONLY runs once per entry. If the SM drifts DURING an EqA session (e.g. after a picker open/close), the stale cursor persists. Consider re-firing the resolver on detect-drift events (menuDepth re-read after a picker exit).

- [ ] **Zodiac: try heap-struct hunt (the 0x258 slot is confirmed empty)** — session 27 ruled out the static roster slot across 9 encodings (`memory/project_zodiac_not_in_slot.md`). Three productive next attempts documented: (a) oscillation diff while cycling PartyMenu sort order (if a "sort by zodiac" option exists), (b) reverse from battle damage math — set up a zodiac-opposite attacker/target pair, read damage modifier to back out both zodiacs, (c) dump HoveredUnitArray struct beyond +0x37 (currently we only decode HP/MP); zodiac may live in the per-hover widget at a higher offset.

- [ ] **Shop item-ID: retry with widget vtable walk** — session 27 confirmed the ID byte is not findable via snapshot-diff or contiguous-AoB on this save (`memory/project_shop_itemid_deadend.md`). Next path: find the OutfitterBuy widget's vtable via AoB, walk to its `HighlightedItem` field. Alternative: mod-side hook on the shop UI render callback to log the item ID being displayed. Either path is multi-session work; `find_toggle` bridge action (shipped session 27) is the reusable infra for the first fresh attempt.

### Session 23 — state stability + helper hardening

- [ ] **Verify open_* compound helpers across CHAIN calls** — Fresh-state runs work after this session's fixes (`open_character_status Agrias` from WorldMap → correct unit). But chained calls (open_eqa Cloud → open_eqa Agrias) still produce the viewedUnit-lag bug. SM-sync changes in `82ccb65` may or may not have resolved this; needs explicit live test sequence cycling 3 different units through each open_* helper and verifying state matches each request. Source: `NavigationActions.cs` `NavigateToCharacterStatus` rewrite, ~line 4419.

- [ ] **Second SaveSlotPicker entry point from BattlePaused** — session 25 shipped the PartyMenuOptions → Save path. A parallel entry exists from BattlePaused → Save (user mentioned). Scoped out of session 25 because verifying from BattlePaused requires entering a real battle. Next session: confirm BattlePaused menu has a Save option, determine its cursor index, and wire the SM transition.

- [~] **`return_to_world_map` from battle states** — Session 26 added a state-guard refusing from Battle* / EncounterDialog / Cutscene / GameOver with a clear error pointing to the right recovery helper (battle_flee / execute_action ReturnToWorldMap). That closes the footgun. BattleVictory / BattleDesertion are NOT blocked because Escape/Enter on those screens legitimately advances toward WorldMap; they still need a live-verify at some point but the unsafe path is closed. Safe from all non-battle states (verified EqA/JobSelection/PartyMenuUnits tree + all non-Units tabs session 24; SaveSlotPicker verified session 26).


- [ ] **Per-key detection verification (replace blind sleeps)** — Long-term fix for compound nav reliability. Each transition key should poll detection until expected state appears, instead of fixed sleep. Bigger refactor; defer until current 350ms/1000ms approach proves stable across more scenarios.

### Session 22 — bridge actions + authoritative detection

- [ ] **C# bridge action viewedUnit lag on chain calls** — `open_eqa Cloud` from WorldMap works perfectly (correct gear + name). But `open_eqa Agrias` from inside Cloud's EqA shows Agrias's gear but Cloud's name. Root cause: escape storm drift checks in DetectScreen reset `_savedPartyRow/_savedPartyCol` after `SetViewedGridIndex` sets them. Stashed fix in `git stash list`. Approach: suppress drift checks during bridge action execution (add a flag to CommandWatcher, check it in the drift-check blocks at lines ~4280-4395). Two lines of code. **Session 23 update: SM-sync from `SendKey` (commit 82ccb65) may have changed the symptom — re-test before applying the stashed approach.** **Session 24 update (2nd attempt):** live-reproduced the chain failure — `open_eqa Agrias` from Cloud's EqA fires escapes + Down + Enter that land on WorldMap and drive the WorldMap cursor (not the party grid), ending at LocationMenu instead of Agrias's EqA. Tried a fix in `NavigateToCharacterStatus` (replace detection-polling escape loop with a 2-consecutive-WorldMap-read confirmation). First attempt (unconditional 6 escapes) broke fresh-state by incorrectly assuming Escape-on-WorldMap is a no-op (it actually opens PartyMenu → toggling). Reverted. Second attempt (per-escape poll with 2-consecutive-WorldMap confirmation): not live-tested — during test setup the game CRASHED after landing on Cloud's EqA, possibly from the EqA-row auto-resolver firing at a bad animation moment. **Reverted all NavigationActions changes; code matches commit c5bfb01.** Key lesson: chain-nav + auto-resolvers together form a fragile timing sandwich; fixes need a safer repro harness than "kick it and pray" before retrying. Stash still exists for reference.

- [ ] **Orlandeau primary skillset label** — `GetPrimarySkillsetByJobName` at CommandWatcher.cs line ~3168 maps "Thunder God" → "Holy Sword". His actual combined skillset (Holy Sword + Unyielding Blade + Fell Sword) should show as "Swordplay" or the specific sub-skillset the cursor is on.

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

- [ ] **Apply mirror technique to JobSelection** — Same idea for JobSelection grid cells. When hovering a class, the info panel displays class data (JP, Lv, prereqs). Find where that hovered-class data lives by navigating to a known cell, snapshotting, navigating away, diffing. Target: a single byte that holds the hovered class index (or u16 class ID). **Session 24 findings:** module-memory snapshot+diff with tight 0→1 ∩ 2→3 intersect returned EMPTY — no main-module byte cleanly tracks cursor index. Heap has the data (existing `resolve_job_cursor` finds it, session reported "Resolved job cursor: 0x13648E264 (32 candidates)"), but the resolved addresses shuffle per launch. **The real missing piece** isn't the byte — it's plumbing the existing resolver's output back into `ScreenMachine.CursorRow/CursorCol` correction when detection reads that the hovered class differs from the SM's cursor. Currently `CommandWatcher.cs ~5400` reads `memRow` from the resolved address but only uses it for display, not for SM drift correction. Live-observed desync: after Right×3 + Down, screenshot showed cursor on Archer (r0,c3) while shell reported cursor=(r0,c0) — the resolver would catch this if we compared hoveredClass-from-widget vs SM cursor position and snap. Next-session work: wire the existing mem-read into an SM snap when they diverge.











### Earlier open items

- [ ] **JP Next: populate Mettle/Fundaments ability costs** — JP Next infra shipped session 19 (commit fe8d41e) for 13 skillsets but Mettle/Fundaments remain unpopulated because their IC-remaster ability IDs aren't in ABILITY_COSTS.md. Add Mettle ability costs once verified in-game (Heal/Tailwind/Chant/Steel/Shout/Ultima).


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

- [ ] **BFS: handle slope-direction-dependent height checks** [Movement] — Wilham's computed tiles have 9 extras vs game. Steep cliff transitions not handled by current BFS. Investigate slope-direction-dependent height costs. (Ally traversal penalty already shipped + TDD-verified on Kenrick.)

- [ ] **Read valid movement tiles from game memory (decode heap tile indices)** [Movement] — Would supersede BFS entirely (BFS has known gaps — see the sibling "slope-direction-dependent height" TODO; Wilham at Siedge Weald overcounts by 9 tiles). Goal: scan memory when entering Move mode, read the game's own valid-tile list, return it instead of BFS output. Prior investigation summary (detailed context in `memory/project_movement_bitmap.md`, `memory/project_valid_tiles.md`, `memory/feedback_move_navigation.md`, `docs/MEMORY_SCAN_WAVE1.md`, `FFTHandsFree/BATTLE_MEMORY_MAP.md §6 & §9`, `FFTHandsFree/BATTLE_COORDINATES.md`):
  - **Ruled out — static addresses:**
    - `0x140C66315` (7 bytes/entry, `[X][Y][elev][flag][0][0][0]`) — this is the cursor traversal **PERIMETER** of the movement diamond, NOT all valid tiles (~15 entries, ~8 unique coords — the outline). Don't re-chase.
    - `0x140C6F400` (stride 0x88, ~111 structs, flag at `+0x1D`) — volatile per-frame rendering struct. Baseline has ~95 flags=1 already; Move mode adds ~16-19 more. Counts vary 13-19 across reads. Not stable enough.
    - `0x14077CA5C` — Move mode flag (0xFF during Move mode, 0x00 otherwise). Useful as a gate but doesn't contain the tile list.
  - **Promising — heap bitmap (2026-04-13 session):** Session found a **2-byte-per-row bitmap** at heap `0x7FFD378E3478` (address shifts per launch). Format: one u16 LE per Y coordinate, each bit = one X coordinate. Verified for Wilham at (10,6) Move=4 Jump=4 → 6 valid tiles: rows y=6,7,8 all read `0x0006`. Related: movement-calc struct at `0x3D89D20` had `(unitX, unitY, move, jump, tileCount)` populated — likely holds a pointer to the bitmap nearby. The bit↔X mapping was not fully confirmed (bits 1,2 → x=9,10 or x=10,9; need more data points to lock down direction + offset).
  - **Proposed next approach:** Add a mod-side `find_move_bitmap` bridge action that, entering Move mode: (a) reads `tileCount` from the static condensed area, (b) scans the heap for a struct whose header reads `(unitX, unitY, move, jump, tileCount)` matching the active unit (verified to exist at `0x3D89D20`-class addresses), (c) follows its pointer (or a fixed offset from it) to the bitmap, (d) decodes and caches the anchor offset. Expose via a debug action first to dump candidates; once the pointer walk is stable, wire the result into `PopulateBattleTileData` as the primary tile source with BFS as fallback.
  - **Verification plan (TDD):** pure-function `MoveBitmapDecoder.Decode(byte[] bitmap, int rows, int bitsPerRow) → List<(x,y)>` with unit tests for the known Wilham case (6 tiles, 3 rows × 0x0006). Bitmap extraction stays in the live-memory path (integration-tested in-game). Iterate by comparing decoded output to the game's visible blue-tile render via screenshot for 3-4 unit/map combos before declaring done.
  - **Blocking uncertainties:** (i) bit direction (x = bit vs x = mapWidth − bit); (ii) whether the struct pointer stays valid across cancel-and-re-enter Move mode; (iii) multi-team behavior — does an enemy's Move mode allocate a separate bitmap or overwrite the same slab?





- [x] **Live-verify `SkillsetItemLookup` wiring in a battle** — Session 19 wired the infra (commit 0c25e29) but didn't verify in-game because the live save was out of battle. Enter a random encounter with a Chemist or Ninja or Samurai active, run `scan_move`, confirm `heldCount` and `unusable` fields populate on Items/Throw/Iaido abilities. **Session 28 (2026-04-17) VERIFIED:** Siedge Weald encounter with Ramza (Gallant Knight / Items secondary). `response.json` contains `heldCount` on every Items ability: Potion=4, Hi-Potion=1, X-Potion=94, Ether=99, Antidote=97, Echo Herbs=99, Gold Needle=99, Holy Water=99, Remedy=X, Phoenix Down=X. No `unusable:true` occurrences because all stocks > 0 (expected). **Follow-up:** shell compact renderer (`scan_move`/`screen`) does NOT surface `heldCount` yet — Claude can only see it by grepping raw JSON. Add a tiny annotation on Items list lines, e.g. `Potion [x4] {Restores 30 HP}`, and mark any `unusable:true` entries visibly (e.g. `Potion [OUT] {Restores 30 HP}`).

- [ ] **Cone abilities — Abyssal Blade** [AoE] — Deferred. 3-row falling-damage cone with stepped power bands. Low value (only 1 ability uses this shape) and requires map-relative orientation. Skip for now.



### Tier 2 — Core tactical depth


### Tier 2.5 — Navigation completeness

- [ ] **Chocobo riding: detect mounted state** [Combat] — Find memory flag that indicates a unit is currently riding a chocobo. Possibly a status byte or a separate "mount" field in the battle unit struct.

- [ ] **Chocobo riding: adjust Move stat when mounted** [Combat] — Mounted units use the chocobo's Move, not their own. Override `Move` in scan_move when the mount flag is set.

- [ ] **Chocobo riding: surface chocobo-specific actions** [Combat] — Mounted units have different action menu (Choco Attack, Choco Cure, etc.). Populate ability list accordingly.



### Tier 3 — Robustness

Turn-state recovery, edge case handlers, multi-unit battle reliability.

- [ ] **Add PreToolUse hook to block `| node` in bash commands** [Enforcement] — Claude should never pipe command output through node for parsing. All shell helpers (screen, execute_action, battle_attack, etc.) handle formatting internally. A Claude Code PreToolUse hook on Bash can detect `| node` in the command string and block it with a reminder to use the formatted helpers. Pending testing the unified screen command first.



- [ ] **Live-verify `execute_action` responses include `ui=` field across all battle screens** [State] — UI is set for all battle screens in DetectScreen. Code path exists; needs in-game verification across Battle_MyTurn/Battle_Acting/Battle_Moving/Battle_Attacking. Observed stale 2026-04-12.


- [ ] **battle_ability first-scan null/null for secondary skillset** [Execution] — Primary detection works (Martial Arts secondaryIdx=9 for Lloyd verified); auto-scan catches misses on retry; all-skillsets fallback works. Remaining: first scan sometimes returns null/null before auto-scan fires — investigate race and eliminate the initial miss.


- [ ] **LoS option A: read game's projected hit% from memory during targeting** [Abilities] — Most practical approach. If hit% address can be found, 0% = blocked by LoS. Earlier investigation found hit% at `statBase-62` in attacker's heap struct but `SearchBytesInAllMemory` misses the right copy. Pick up from the damage-preview hunt (see separate task).

- [ ] **LoS option B: compute LoS from map height data** [Abilities] — Fallback if memory read fails. Walk the straight-line path from attacker to target in the map grid and check if any intermediate tile's height blocks the projectile. Requires per-map terrain data already loaded.

- [ ] **LoS option C: enter targeting, check if game rejects tile, cancel if blocked** [Abilities] — Brute-force fallback. Slow but reliable. Use only as last resort if A and B both fail.


- [ ] **Equipment IDs stale across battles** [State] — Roster equipment at `+0x0E` reads the save-state equipment, not the current in-battle loadout. Need to find the live equipment address.


- [ ] **Active unit name/job stale across battles** [State] — After restarting a battle with different equipment/jobs, the name/job display doesn't refresh between battles.


- [ ] **battle_move reports NOT CONFIRMED for valid moves** [Movement] — Navigation succeeds but F key confirmation doesn't transition. Timeout increased from 5s to 8s for long-distance moves.


- [ ] **Detect disabled/grayed action menu items** [Movement] — Need to find a memory flag or detect from cursor behavior.


- [ ] **Live-test `battle_retry` from GameOver screen** [Execution] — Code path exists, GameOver detection fixed. Needs in-game verification after losing a battle.


- [ ] **Re-enable Ctrl fast-forward during enemy turns** [Execution] — Tested both continuous hold and pulse approaches. Neither visibly sped up animations. Low priority.


- [ ] **Find IC remaster deathCounter offset** [State] — KO'd units have 3 turns before crystallizing. PSX had this at ~0x58-0x59. Find IC equivalent in battle unit struct.

- [ ] **Find IC remaster element affinity bytes** [State] — Four fields: elementAbsorb, elementNull, elementHalf, elementWeak. Each is an 8-bit mask over the 8 elements (Fire/Ice/Lightning/Water/Wind/Earth/Holy/Dark). Surface on scan_move so Claude can avoid healing a Death-absorbing undead or buffing a shielded element.

- [ ] **Find IC remaster chargingAbility + chargeCt bytes** [State] — Units charging a spell show in the Combat Timeline. Find which ability ID is queued and remaining CT for each charging unit. Needed to avoid wasted Silence/Stop attempts.

- [ ] **Find IC remaster facing byte per unit** [State] — Direction the unit is facing (N/E/S/W). Needed for backstab targeting (Attack from behind = +50% hit rate and bonus damage). PSX had this at +0x1A area.


- [ ] **Read death counter for KO'd units** [State] — KO'd units have 3 turns before crystallizing. Need to find the IC equivalent of PSX offset ~0x58-0x59.


- [ ] **Detect charging/casting units** [Abilities] — Units charging a spell show in the Combat Timeline. Need to read charging state, which spell, and remaining CT from memory.



### Tier 4 — Known hard problems

- [ ] **Unit names — enemies** [Identity] — Enemy display names not found in memory. May need NXD table access or glyph-based lookup.


- [ ] **Zodiac byte memory hunt for generics/Ramza** [Identity] — Story-character zodiacs shipped session 19 via hardcoded nameId table (`ZodiacData.cs`, commit 1674bb6). Generics and Ramza return null. Hunt attempted offsets 0x00-0x100 with 4 anchor points (Agrias=Cancer, Mustadio=Libra, Orlandeau=Scorpio, Cloud=Aquarius); no match found. Retry strategies: (a) nibble-packed encoding — search half-bytes instead of bytes, (b) outside the 0x258 slot stride — try a parallel array, (c) non-zero-indexed ordering — try +1, +3, *2 variants.

- [ ] **Zodiac compatibility damage multiplier** [Combat] — Once zodiac is readable for every unit, wire `ZodiacData.GetOpposite` + Good/Bad compatibility tables into damage preview calculations. Multipliers per wiki: Best 1.5x, Good 1.25x, Bad 0.75x, Worst 0.5x. Requires projected-damage preview work (separate task) to ship first.


- [ ] **Fix Move/Jump stat reading** [Movement] — UI buffer shows base stats, not effective (equipment bonuses missing).



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


- [ ] **Location address unreliable** — 0x14077D208 stores last-passed-through node, not standing position.



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



### Unit Facing Direction
- [ ] Read unit facing direction from memory


- [ ] Use facing data for backstab targeting



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

- [ ] **Tavern: `read_rumor` action (scrape body text from widget)** — Scrape the highlighted rumor's body text pane, like `read_dialogue` does for cutscenes. UE4 widget scrape; same FString challenges as shop items.

- [ ] **Tavern: `read_errands` action (scrape body text + metadata)** — Return description, quester, party size, days, fee. Widget scrape.


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



- [ ] **Session command log for post-hoc review (back burner)** — added 2026-04-15 session 16 as a future observability item. Record every command Claude issues during a play session: command ID, timestamp, action name, source screen, target screen (if validPath), success/partial/failed status, error message if any, and round-trip latency. One line per command, append-only file under `claude_bridge/` (rotate per session start). Enables "where did Claude get stuck?" review after a long play — pinpoint the exact command sequence where a helper failed, a screen mis-detected, or a batched nav drifted. Out-of-scope for this session; capture the requirement while it's fresh.



**EquipmentAndAbilities action helpers** — declarative one-liners that wrap the full nav flow. All helpers are locked to the EquipmentAndAbilities state (error elsewhere), idempotent (no-op if already equipped), and validate via `screen.availableAbilities`/inventory.

- [ ] **`change_right_hand_to <itemName>` helper** — stub, blocked on inventory reader wiring to picker row index.
- [ ] **`change_left_hand_to <itemName>` helper** — stub.
- [ ] **`change_helm_to <itemName>` helper** — stub.
- [ ] **`change_garb_to <itemName>` helper** — stub. (Game calls this "Combat Garb"/"Chest".)
- [ ] **`change_accessory_to <itemName>` helper** — stub.
- [ ] **`remove_equipment <slotName>` helper** — stub.
- [ ] **`dual_wield_to <leftWeapon> <rightWeapon>` helper** — requires Dual Wield support ability equipped.
- [ ] **`swap_unit_to <name>` helper** — from any nested PartyMenu screen, Q/E-cycle to named unit.
- [ ] **`unequip_all` helper** — clear every equipment slot.
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
- Battle_Paused false positives: pauseFlag=1 stale after facing confirmation
- Settlement/shop screens not detected yet
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
- [ ] **Tighten `TitleScreen` rule** — require full uninit sentinels (`slot0==0xFFFFFFFF && battleMode==255 && eventId==0xFFFF && ui==0`). Current rule catches too much.


- [ ] **Reorder rules** — specific rules (PartyMenu via `party==1`, EncounterDialog, LoadGame, LocationMenu) must run BEFORE the TitleScreen catch-all.


- [ ] **Remove `encA/encB`-dependent rules** — replace Battle_Victory / Battle_Desertion / EncounterDialog discriminators with stable signals (`paused`, `submenuFlag`, `acted/moved` combos).


- [ ] **Add `Battle_ChooseLocation` discriminator** — requires location-type annotation (which location IDs are multi-battle campaign grounds vs villages). Add to `project_location_ids_verified.md`.


- [ ] **Scope `menuCursor` interpretation** — only treat as action-menu index when `submenuFlag==0 && team==0`. Inside submenus, rely on `_battleMenuTracker`.


- [ ] **Memory scan for WorldMap vs TravelList discriminator** — these are byte-identical in current 18 inputs. Need a menu-depth or focused-widget address.





### Coordinate System
- Grid coords and world coords are different systems
- Camera auto-rotates when entering Move mode or Attack targeting — always re-read rotation

### Bugs Found 2026-04-12
- [ ] **Ability list navigation: use counter-delta instead of brute-force scroll** — Currently presses Up×N to reset then Down×index. Could use counter-delta approach.


- [ ] **Detect rain/weather on battle maps** — Rain boosts Lightning spells by 25%.


- [ ] **Post-battle memory values stuck at 255 after auto-battle** — All memory addresses stayed at 255/0xFFFFFFFF permanently. May require game restart.


- [ ] **Fix stale location address (255) after restart breaking battle-map auto-detect** — Location ID lookup + random encounter maps + fingerprint fallback already shipped. Remaining bug: after game restart, `0x14077D208` reads 255 which defaults to the wrong map. Need a fallback read or forced re-read on first post-restart scan.



### Bugs Found 2026-04-12 Session 2
- [ ] **DetectScreen reports Battle_Casting when actually in Battle_Moving** [State] — battleMode flickers to 1 during move mode, causing DetectScreen to return Battle_Casting instead of Battle_Moving. This breaks `execute_action Cancel` and other commands that check screen state. The battle_move confirmation poll was fixed to ignore Battle_Casting (2026-04-13), but the general DetectScreen path still has this issue — any command that calls DetectScreen while in move mode can get the wrong state. Root cause: battleMode=1 (Casting) takes priority over battleMode=2 (Moving) in detection order, and the flicker isn't filtered. Observed 2026-04-12, 2026-04-13.


- [ ] **Static array at 0x140893C00 is stale mid-turn** [State] — HP AND positions don't update during/after moves or attacks within a turn. Only refreshes at turn boundaries. Killed a Skeleton (HP 535→0 on screen) but array still read 535. Moved Ramza but array still showed old position. Need to find the live data source the game UI reads from.


- [ ] **Damage/hit% preview during targeting** [State] — The game displays projected damage and hit% when hovering a target. Extensive investigation 2026-04-12:
  - **Found via probe_status:** In attacker's heap struct, hit% at statBase-62 (u16), damage at statBase-96 (u16). Verified across 3 targets (Kenrick 570/48%, Lloyd 342/50%, Wilham 364/95%). Offsets consistent for hit%, damage shifted by 4 bytes for one target.
  - **Two heap copies exist:** One in 0x416xxx range (found by `SearchBytesInAllMemory`, PAGE_READWRITE PRIVATE) — has HP/stats but NOT preview data. Another in 0x130xxx-0x15Axxx range (found by `SearchBytesAllRegions`) — this copy HAS preview data at the offsets above.
  - **Problem:** `SearchBytesInAllMemory` only scans PAGE_READWRITE PRIVATE memory, missing the copy with preview data. `SearchBytesAllRegions` finds it but is slow (scans from addr 0) and returns too many false matches.
  - **Approach needed:** Use `SearchBytesInAllMemory` with `broadSearch: true` flag (already added — scans all readable memory with address range filter). Search for HP+MaxHP of the attacker, verify level byte, read at statBase-62 and statBase-96. Must exclude the 0x416xxx copy (no preview data) — filter by checking hit% > 0.
  - **Also found at low static address** (0x60823C one session, different next) via `search_all` with unique 10-byte pattern. Address shifts between restarts. Reading from this address crashed the game — likely in a protected code segment.
  - **Code exists but disabled:** `ReadDamagePreview()` in NavigationActions.cs has the search + offset logic. Currently returns (0,0) because the broad search finds the wrong copy. Fix: add address range filter to skip 0x416xxx and target the 0x130-0x15A range.


- [ ] **Screen detection shows Cutscene during ability targeting** [State] — While in targeting mode for Aurablast (selecting a target tile), screen detection reports "Cutscene" instead of "Battle_Attacking" or "Battle_Casting". This causes key commands to fail because they check screen state. Observed 2026-04-13.


- [ ] **Failed battle_move reports ui=Abilities instead of ui=Move** [State] — After battle_move fails validation, the response shows ui=Abilities but the in-game cursor is still on Move. The scan that runs before the move might be changing the reported ui state. Observed 2026-04-13.


- [ ] **battle_ability selects wrong ability from list** [Execution] — battle_ability "Aurablast" selected Pummel instead. The ability list navigation (Up×N to top, Down×index) is picking the wrong index. The learned ability list may not match the hardcoded index, or the scroll navigation is off-by-one. Observed 2026-04-13.


- [ ] **Abilities submenu remembers cursor position** [Execution] — After battle_ability navigates to a skillset (e.g. Martial Arts for Revive), then escapes, the submenu cursor stays on that skillset. Next battle_attack enters Martial Arts instead of Attack. Need to verify/navigate to correct submenu item rather than assuming cursor is at index 0. Observed 2026-04-13.


- [ ] **battle_ability response says "Used" for cast-time abilities** [State] — Abilities with ct>0 (Haste ct=50) are queued, not instant. Response says "Used Haste" but spell is only queued in Combat Timeline. Unit still needs to Wait. Response should say "Queued" for ct>0 abilities. Observed 2026-04-13.


- [ ] **Detect auto-end-turn abilities (Jump)** [Execution] — Jump auto-ends the turn (unit leaves the battlefield). battle_ability should detect this by checking if the screen transitioned past the active unit's turn after confirming. If so, report "turn ended automatically" instead of leaving Claude to issue a redundant Wait. Observed 2026-04-13.



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



---

## Sources & References

- [fft-map-json](https://github.com/rainbowbismuth/fft-map-json) — Pre-parsed map terrain data (122 MAP JSON files)
- [FFT Move-Find Item Guide by FFBeowulf](https://gamefaqs.gamespot.com/ps/197339-final-fantasy-tactics/faqs/32346) — ASCII height maps for MAP identification
- [FFHacktics Wiki](https://ffhacktics.com/wiki/) — PSX memory maps, terrain format, scenario tables


---

## Completed — Archive

Items that are fully shipped (checked `[x]`). Partial (`[~]`) items stay above in their original section.

### Session 27 (2026-04-17) — chain-nav fixed + HpMpCache + planner wiring + detailedStats

Commits: `b5890f4` (HpMpCache), `7af47c9` (SetJobCursor + resolver liveness), `0b495d8` (find_toggle + shop-item dead-end), `f3a3694` (NavigationPlanner + dry_run_nav), `e95038e` (remove chain-guard hard block), `32497e9` (Mettle/Fundaments JP tests), `d6f2264` (wire planner into live, fix chain-nav timing), `374c225` (detailedStats verbose payload), `2937da4` (new-recruit name-lookup TODO).

**Features landed:**

- [x] **`HpMpCache` persists 4-slot HP/MP across reads** — commit `b5890f4`. Disk-backed (`claude_bridge/hp_mp_cache.json`) keyed by (slotIndex, equipment signature). Observations from HoveredUnitArray get cached; falls through when live read returns null. Equipment change invalidates entry. Net: Ramza/Kenrick/Lloyd/Wilham HP/MP consistently visible in roster output (matches session 26 ceiling, now cached against flicker). Slots 4-13 remain unreachable — HoveredUnitArray is a fixed 4-slot slab anchored on Ramza's widget, not a roving hover window. 6 new unit tests (HpMpCacheTests). Memory note `project_hovered_unit_array_partial.md` updated with session 27 findings.
- [x] **`SetJobCursor` API + resolver liveness check** — commit `7af47c9`. New `SM.SetJobCursor(row, col)` bounds-checked setter for drift correction. `ResolveJobCursor` adds a post-resolve liveness probe (press Right, read byte, expect baseline+1) to filter widget-counter candidates that pass oscillation but don't track real navigation. Live-observed this session: all 32 candidates failed liveness on Ramza's save, saving SM from corruption by dead bytes. Resolver gate also fixed from `menuDepth==2` (never fired — JobSelection is menuDepth=0) to `KeysSinceLastSetScreen>=1` (animation-lag signal). Silent bug since feature first wired. CommandWatcher JobSelection read path now snaps SM via `SetJobCursor` when mem disagrees. 3 new SM tests. Memory note `project_jobcursor_liveness.md`.
- [x] **`find_toggle` bridge action** — commit `0b495d8`. Exposes `MemoryExplorer.FindToggleCandidates` to shell/node queries as a reusable infra action. Given 3 snapshot labels (baseline/advanced/back) and an expected delta, returns heap addresses that went a→b→a. Reusable for future cursor-byte hunts without baking a dedicated resolver. Allowlist test updated.
- [x] **Shop item-ID hunt — dead-end documented** — commit `0b495d8`. Row cursor at `0x141870704` confirmed working (0/1/2 u32 as scroll). But 3-way snapshot-diff + delta=1 returned ZERO candidates for a hovered-item-ID byte. Master item pool at `0x5D9B52C0` reads all zeros this session (lazy-loaded or drifted). UTF-16 name/description search finds only the static name pool (1 match), no heap copy of the hovered name. Contiguous u8/u16/u32 ID sequence search: 0 matches. Row→item mapping is blocked pending widget vtable walk, save-file decode, or mod-side render-callback hook. Memory note `project_shop_itemid_deadend.md` lists approaches tried so next session doesn't redo them.
- [x] **`NavigationPlanner` + `dry_run_nav` bridge action** — commit `f3a3694`. Pure function `PlanNavigateToCharacterStatus(currentScreen, displayOrder, rosterCount)` returns a typed list of key steps with per-step settle time, rationale, and optional `EarlyExitOnScreen`/`GroupId` hints for the escape-storm detection-poll. `dry_run_nav` action executes the planner without firing any keys, logs the plan to `SessionCommandLog` + stderr. Addresses session 24 footgun: "two prior attempts crashed the game" on chain-nav testing — now the plan is observable before committing to a live run. 10 new unit tests lock in the key sequence and settle times.
- [x] **Chain-guard hard block REMOVED** — commit `e95038e`. Five prior attempts to block chained shell calls all produced collateral false-positives (piped helpers killed, debugging sequences blocked). Live-tested this session: chained Bash calls work reliably — the single-threaded bridge sequences game-affecting commands, and bridge-side auto-delay handles the narrow race case. `_fft_guard` and `_fft_reset_guard` neutered to no-ops; the disk `fft_done.flag` path + `kill -9 $$` deleted. Counter + `[CHAIN INFO]` telemetry retained. Rebranded `[CHAIN WARN]` → `[CHAIN INFO]` to match non-blocking role. Live-verified: 3 chained `screen` reads, piped + second call, 2 chained `esc` — all succeed.
- [x] **Mettle/Fundaments JP Next regression tests** — commit `32497e9`. Costs were already populated in `AbilityJpCosts.CostByName` (commit `c5bfb01`); this session adds 5 explicit regression tests so a future edit can't silently un-populate them. Tests: Fundaments nothing-learned → Rush=80; Fundaments Rush-learned → Throw Stone=90; Mettle nothing-learned → Rush=80; Mettle cheap-four-learned → Tailwind=150; guard that every Mettle-exclusive ability has a populated cost.
- [x] **NavigationPlanner wired into live `NavigateToCharacterStatus` + chain-nav timing fix** — commit `d6f2264`. Live method now consumes the planner as single source of truth. Escape-storm settle bumped 300→500ms, final open-PartyMenu escape 500→700ms. Root cause of session 24 crash: at 300ms, SM's TravelList→WorldMap override fires mid-transition (SM predicts WorldMap via key-count BEFORE game finishes rendering the exit), causing 2-read confirm to agree falsely. Manual stepping with ~500ms works. Live-verified across 5 chain hops: WorldMap→Ramza EqA, Ramza→Kenrick, Kenrick→Agrias, Agrias→Cloud, Cloud→Agrias — including the exact session 24 crash repro. Memory note `feedback_chain_nav_timing.md`.
- [x] **`detailedStats` verbose payload on CharacterStatus** — commit `374c225`. New `UnitStatsAggregate` record (camelCase JSON: hpBonus/mpBonus/weaponPower/weaponRange/weaponEvade/weaponElement/leftHandPower/shieldPhysicalEvade/shieldMagicEvade) derived from roster equipment IDs + ItemData constants. Wiki-independent (PSX vs IC concerns don't apply to ItemData's bundled values). Left-hand slot auto-routes to shield fields when the item is a shield (no WP). Live-verified: Ramza hpBonus=350 (Grand Helm 150 + Maximillian 200), Agrias shieldPE=75 + shieldME=50 (Escutcheon in left hand), Mustadio all-zero (unequipped). 5 new unit tests (UnitStatsAggregatorTests). Move/Jump/Speed/PA/MA intentionally NOT included — they need the full FFT per-job formula path where wiki-PSX values may differ from IC remaster (memory note `feedback_wiki_psx_vs_ic.md`).
- [x] **New-recruit name-lookup blocker recorded** — commit `2937da4`. User recruited Crestian (Lv1 Squire, JP 137) to live-test JP Next. `NameTableLookup` resolves her slot-4 name bytes to "Reis" (collides with existing Lv92 Dragonkin Reis). Two entangled TODOs added at top of §0 with the verification path for when the name lookup is fixed. Infrastructure (JP Next + costs) is correct per unit tests; the live-verify needed to wait.

**Session discoveries — memory notes saved:**

- `feedback_wiki_psx_vs_ic.md` — Wiki values are PSX-sourced; prefer memory reads over wiki-formula re-implementations.
- `project_jobcursor_liveness.md` — JobSelection cursor resolver needs liveness check; oscillation+axis-verify alone accepts widget counters that don't track real nav.
- `project_shop_itemid_deadend.md` — Shop item-ID byte unfindable via snapshot diff or AoB; needs widget vtable walk or save-file decode.
- `project_zodiac_not_in_slot.md` — Zodiac byte is NOT in the 0x258 roster slot (any encoding). Heap widget or save-file only. 4-anchor cross-ref confirms.
- `feedback_chain_nav_timing.md` — Chain-nav escape storm needs ≥500ms per escape + 700ms for final open. Below that, SM predicts state transitions before game renders them.
- Updated `project_hovered_unit_array_partial.md` with session 27 findings on the fixed-slab behavior.

**Investigated / not-shipped:**

- **Zodiac byte in roster slot** — ruled out. 9 encodings × 4 known-zodiac anchors returned zero matches. Next attempts documented: PartyMenu sort cycle diff, reverse from battle damage math, HoveredUnitArray struct dump beyond +0x37.
- **Shop item-ID in snapshot-reachable memory** — ruled out. `TakeHeapSnapshot` filters to <4MB PAGE_READWRITE private/mapped; shop widget data lives outside this. `find_toggle` action shipped as the reusable infra when future work finds a different path.
- **JobCursor drift correction firing** — infrastructure ready, liveness check shipped, but no live-tracking byte exists on this save (32 candidates all rejected). Memory note documents next-attempt approaches.

### Session 26 (2026-04-17) — save action + detection cleanup + Tavern + HP/MP

Commits: `473ac53` (session cmd log + KEY_DELAY split + TitleScreen tightening), `d1e7160` (Tavern + TavernRumors + TavernErrands state tracking), `7ac9f22` (HP/MP partial), `134da68` (save action + detection reorder + return_to_world_map guard).

**Features landed:**

- [x] **`SessionCommandLog` per-session JSONL observability** — commit `473ac53`. New `SessionCommandLog` class writes `claude_bridge/session_<stamp>.jsonl`; one line per command with id/timestamp/action/source→target/status/error/latencyMs. Never throws (observability must not take down command processing). Wired into `CommandWatcher` main + error paths. 7 unit tests.
- [x] **Per-key-type KEY_DELAY split** — commit `473ac53`. New `KeyDelayClassifier.DelayMsFor(vk)` returns 200ms for cursor keys (Up/Down/Left/Right), 350ms for transition/tab-cycle/unknown. `NavigationActions.SendKey` uses the classifier. Shaves 1-2s off nav-heavy flows (open_eqa party grid nav). 9 unit tests.
- [x] **`TitleScreen` rule tightened** — commit `473ac53`. Removed the loose `rawLocation==255 → TitleScreen` fallback at the end of the out-of-battle branch. Residuals now return "Unknown" so callers see ambiguity instead of being mislabeled as TitleScreen after GameOver/post-battle stale. Added 2 coverage tests.
- [x] **`Tavern` + `TavernRumors` + `TavernErrands` SM tracking** — commit `d1e7160`. Live-scanned at Sal Ghidos: all three are byte-identical to LocationMenu in detection inputs. SM-based solution: `GameScreen.Tavern/TavernRumors/TavernErrands` enum values, `SM.TavernCursor` (0=Rumors, 1=Errands) with wrap on Up/Down, `HandleTavern`/`HandleTavernSubScreen` for Enter/Escape. `ResolveAmbiguousScreen` extended to map (SM=Tavern sub, detected=LocationMenu) → SM name. `screen` query path now syncs SM to detection for non-party-tree transitions so the SM doesn't stay stuck at WorldMap. NavigationPaths for both sub-states (Select deliberately omitted from Errands). 9 new SM tests + 3 detection tests. Live-verified full round-trip.
- [x] **HP/MaxHp/Mp/MaxMp in roster** (partial `[~]`) — commit `7ac9f22`. Wired `HoveredUnitArray.ReadStatsIfMatches` into the roster assembly; first 4 units populate correctly (Ramza Lv99 HP 719/719 MP 138/138 verified). Other 10 units return null because the "hovered-unit array" is per-hover, not roster-wide. Full roster needs formula-based recompute (TODO §10.6).
- [x] **Detection rule reorder** — commit `134da68`. `party==1 / encounterFlag / eventId` authoritative rules now run BEFORE hover/location heuristics. Prevents "opened PartyMenu while hovering a map location" from misdetecting as WorldMap, and EncounterDialog from losing to hover-based rules. 2 new coverage tests.
- [x] **`eventId` filter verification** — commit `134da68`. Confirmed the out-of-battle rules already use `< 400 && != 0xFFFF` (the in-battle `< 200` is intentional — eventId address aliases as active-unit nameId during combat). 2 new tests covering Orbonne eventId=302.
- [x] **`save` C# action implemented** — commit `134da68`. Drives from any non-battle state to SaveSlotPicker via: escape-to-WorldMap (2-consecutive-read confirm for the Escape-on-WorldMap toggle gotcha) → open PartyMenu → Q-cycle to Options tab (tab-count derived from SM) → Up×5 → Enter. State-guarded against Battle* / Encounter / Cutscene / GameOver. Live-verified from WorldMap, PartyMenuOptions, and 3-level-nested EqA.
- [x] **`return_to_world_map` battle-state guard** — commit `134da68`. Helper refuses from 13 unsafe states (Battle* / Encounter / Cutscene / GameOver / Formation / Sequence / EnemiesTurn / AlliesTurn) with clear pointers to `battle_flee` or `execute_action ReturnToWorldMap`. Closes the footgun where Escape on BattlePaused would resume the battle.
- [x] **Fix Battle_Dialogue / Cutscene eventId filter** — commit `134da68`. Already fixed in prior sessions; session 26 added 2 coverage tests for Orbonne eventId=302 to prove the behavior holds.
- [x] **Add hover to ScreenDetectionLogic inputs** — closed stale. `hover` has been a parameter of `Detect` for a while (line 65); TODO entry was stale.

### Session 25 (2026-04-17) — SaveSlotPicker + chain-guard pipe-subshell fix

Commit: `c847d42`.

**Features landed:**

- [x] **Track SaveSlotPicker state (PartyMenuOptions → Save)** — commit `c847d42`. New `GameScreen.SaveSlotPicker`, `HandlePartyMenu` Enter-on-OptionsIndex-0 transition, Escape back to PartyMenuOptions. Detection override: SaveSlotPicker and TravelList are byte-identical across all 28 detection inputs (memory hunt documented in `memory/project_saveslotpicker_vs_travellist.md`). `ScreenDetectionLogic.ResolveAmbiguousScreen(SM, detected)` maps (SM=SaveSlotPicker, detected=TravelList) → "SaveSlotPicker". Wired into 3 CommandWatcher paths (screen query, key-sending fallback, execute_action path lookup). ScrollUp/Down/Select/Cancel ValidPaths via `SaveGame_Menu` alias. 7 new tests.
- [x] **Chain guard pipe-subshell fix** — commit `c847d42`. `_FFT_DONE` shell var was bypassed when helpers were piped (`screen | tail`) because the function ran in the pipe's subshell. Added disk flag `claude_bridge/fft_done.flag` that survives subshells, cleared at `source` time. 34 composite-helper reset sites rewritten to call new `_fft_reset_guard` helper. Live-verified: piped-first-call + second-call blocks correctly with `[NO]` + exit 137.
- [x] **`fft_resync` forbidden-state guard** — commit `c847d42`. Helper refuses from Battle*/EncounterDialog/Cutscene/BattleSequence/BattleFormation/GameOver because its escape storm mis-handles those transitions. Block-list (not allow-list) so new non-battle screens are automatically safe.

### Session 24 (2026-04-16 / 2026-04-17) — TODO cleanup sweep + ModStateFlags + Ctrl focus-leak

Commits: `91fa2cb` (rename + guards + UI backfill), `5cf018a` (shell UX + chain guard), `c5bfb01` (labels + JP + auto-resolvers + docs), `9a4acf9` (ModStateFlags + AbilitiesAndJobs.md + fft_resync + Ctrl focus-leak fix).

**Core features landed:**

- [x] **Rename `PartyMenu` → `PartyMenuUnits`** — commit `91fa2cb`. Enum member, string literals, shell whitelists, tests, Instructions docs. Sibling tabs (Inventory/Chronicle/Options) unchanged. Live-verified.
- [x] **`world_travel_to status=rejected` surfaces reason** — commit `91fa2cb`. `fft.sh` renderer only surfaced errors on `failed`; added `[REJECTED] <reason>` branch. Live-verified.
- [x] **Construct 8 locked ability slots** — commit `91fa2cb`. `JobGridLayout.LockedAbilityUnits` set + shell `_change_ability` guard. 2 new unit tests. Live-verified on all four slots.
- [x] **WorldMap `ui=` shows hovered location name** — commit `91fa2cb`. `GetLocationName(hover)` when `hover < 255`. Live-verified.
- [x] **`ui=<element>` backfilled** — commit `91fa2cb`. Cutscene/CharacterDialog → "Advance", CombatSets → "Combat Sets". Remaining holdouts (TravelList, TitleScreen) intentional.
- [x] **EnterLocation delay per-location tuning** — commit `91fa2cb`. Live-verified at Dorter, Gariland, Lesalia with 500ms default. No per-location tuning needed.
- [x] **EqA compact format single-line** — commit `5cf018a`. Already single-line (`Equip:` / `Abilities:` rows); verbose keeps the grid. Ticked after live verification.
- [x] **JobSelection unlock-requirements text** — commit `5cf018a`. Already fully wired via `JobGridLayout.JobPrereqs` + `GetUnlockRequirements`. Ticked after live verification (Bard on Ramza → `requires=Summoner Lv. 5, Orator Lv. 5 (male only)`).
- [x] **`unequip_all` per-slot progress + runtime docs** — commit `5cf018a`. Header comment documents ~25-30s runtime + ≥35s Bash-timeout recommendation; per-slot progress `N/5 <label>: <item> → removing...`. Live-verified on Cloud (5 empty slots).
- [x] **`remove_equipment` position-agnostic entry** — commit `5cf018a`. Reads `cursorCol`; col 0 proceeds, col 1 auto-Lefts, else refuses. Live-verified.
- [x] **Block improper chained `fft` calls** — commit `5cf018a`. `_is_key_sending` classifier + `_track_key_call` counter; second key-sending call without `_FFT_ALLOW_CHAIN=1` triggers `[CHAIN WARN]` to stderr. Composites annotated. Live-verified.
- [x] **`return_to_world_map` from PartyMenuInventory/Chronicle/Options** — commit `5cf018a`. Live-verified from all three non-Units tabs.
- [x] **Orlandeau primary skillset label** — commit `c5bfb01`. "Thunder God" → "Swordplay" (was "Holy Sword"). Live-verified.
- [x] **JP Next: Mettle costs populated** — commit `c5bfb01`. Tailwind 150, Chant 300, Steel 200, Shout 600, Ultima 4000. Wiki-sourced; in-game verification still pending but values surface correctly.
- [x] **EqA `ui=` auto-resolver on entry** — commit `c5bfb01`. `_eqaRowAutoResolveAttempted` latch fires `DoEqaRowResolve` once per EqA entry. Clears on EqA exit. Live-verified: `[CommandBridge] auto EqA row: 0 (unequip 36 → 0)`.
- [x] **Mime hardcoded-Locked → skillset-union proxy** — commit `c5bfb01`. Checks viewed unit + party for Summon/Speechcraft/Geomancy/Jump. Live-verified on Orlandeau: `state=Visible requires=Squire Lv. 8...`. 3 new unit tests.
- [x] **Story scene handling docs** — commit `c5bfb01`. `Rules.md` "Story Scenes" section + existing `CutsceneDialogue.md`. Covers both the TODO "Story scene handling" behavior item and the "StoryScenes.md" doc item.
- [x] **AbilitiesAndJobs.md** — commit `9a4acf9`. New 95-line `Instructions/AbilitiesAndJobs.md`. Ability slots, JP economy, unlock tree, state fields, cross-class reasoning, story-character locks, gotchas, command mapping.
- [x] **ModStateFlags helper class** — commit `9a4acf9`. Disk-backed named-flag store in `claude_bridge/mod_state.json`. Bridge actions `get_flag`/`set_flag`/`list_flags` + shell helpers. 8 unit tests. Live-verified set → disk → get round-trip.
- [x] **Ctrl fast-forward focus-leak fix** — commit `9a4acf9`. Root cause: `SendInputKeyDown` called `SetForegroundWindow` + global `SendInput` every tick, hijacking user's terminal Ctrl state when tabbed away. Fix: `IsGameForeground()` + `ctrlHeldGlobally` state; global Ctrl released when focus leaves game, re-asserted when it returns. PostMessage path keeps DirectInput signal alive. Live-verified by user: "It works. It interrupted me but it didn't continue to hold ctrl."
- [x] **`fft_resync` state-reset helper** — commit `9a4acf9`. Shell helper + C# `reset_state_machine` bridge action. Escapes to WorldMap with 2-consecutive-confirm, then clears SM + every auto-resolve latch. ~5s vs `restart`'s ~45s; preserves mod memory. Live-verified.

**Investigated / not-shipped:**

- Chronicle/Options tab discriminator — NO stable byte. `0x140900824` looked promising (9/6 within session) but failed restart test (becomes a nav-history counter). Notes added to TODO entry for next attempt.
- JobSelection mirror-technique byte hunt — NO stable byte. Module-memory 0→1 ∩ 2→3 intersect empty. Real gap is plumbing existing `resolve_job_cursor` output into SM correction. Notes added.
- Mod-owned memory state flags (user's idea) — VIABLE for within-session ephemeral state, infeasible for save-aware or cross-restart use. Full viability report in session log. Led to building ModStateFlags with disk backing.



- [x] **Stale `unitsTabFlag`/`inventoryTabFlag` after shop exit** — FIXED commit `5dcd234`. `0x140D3A41E` was latching at 1 on WorldMap after shop exit, causing detection to return "PartyMenu" instead of "WorldMap". Added `menuDepth` parameter to `ScreenDetectionLogic.Detect`; rule now skipped when `menuDepth==0` (outer screen confirmed).

- [x] **SM-Drift racing animation lag on party-tree transitions** — FIXED commit `e634a35`. The drift check fired during the 50-200ms CharacterStatus → EqA animation when menuDepth still reads 0, snapping the correct SM back to WorldMap. Now gated on `smJustTransitioned` (SM.CurrentScreen != _smScreenBeforeKeys); SM rides out its own animation window.

- [x] **`DelayBetweenMs` not propagated from PathEntry to CommandRequest** — FIXED commit `588af0a`. `ExecuteValidPath` was converting PathEntry keys but never copying `path.DelayBetweenMs`. Every validpath with custom timing (ReturnToWorldMap 800ms, OpenChronicle 500ms, etc.) was running at the bridge default. One-line fix at `CommandWatcher.cs:2195`. Verified ReturnToWorldMap from JobSelection now lands on WorldMap with 852ms between Escapes.

- [x] **EqA-promote heuristic stomping non-EqA party-tree screens** — FIXED commit `f356fb3`. The equipment-mirror promote heuristic was firing on PartyMenuInventory/Chronicle/Options because the mirror stays populated (same roster visible). Extended `detectionSaysWorldSide` guard to include all non-EqA party-tree screens.

- [x] **World-map drift snap stomping legit party-tree screens** — FIXED commit `e9eef6f`. JobSelection/PartyMenuChronicle/PartyMenuOptions all read `menuDepth=0` legitimately. Drift detector was snapping SM back to WorldMap whenever it saw 3 reads of (SM in tree + raw=TravelList + menuDepth=0). Exempted via `smOnNonUnitsPartyTab` and `smOnJobSelection`.

- [x] **Helpers for all detectable states** — DONE commit `aef8514`. `_show_helpers` cases added for CharacterDialog, BattleMoving/Attacking/Casting/Abilities, BattleAlliesTurn/EnemiesTurn/Waiting, shop interiors (Outfitter/Tavern/WarriorsGuild/PoachersDen/SaveGame). Every state detection can produce now has both ValidPaths and helpers.

- [x] **NavigationActions.SendKey wired to SM.OnKeyPressed** — DONE commit `82ccb65`. Compound nav helpers (open_eqa, open_character_status, etc.) were driving the game without notifying the state machine. Added `NavigationActions.ScreenMachine` property + `OnKeyPressed(vk)` call inside `SendKey`. `NavigateToCharacterStatus` rewritten to fail-fast on bad unit name, drop wrap-to-(0,0) for fresh PartyMenu open, sync SM.RosterCount/GridRows before nav.

- [x] **NavigationActions KEY_DELAY 150ms → 350ms + 1s post-Escape** — DONE commit `4fd29ae`. 150ms was too fast — Down keys dropped during PartyMenu open animation. 350ms matches manual-test pace.

- [x] **State-validation guards on all helpers** — DONE commit `b08bb04`. Every helper now validates `_current_screen` against an allowed-state regex BEFORE firing keys. `_require_state` helper prints "[helper] ERROR: cannot run from <state>. Allowed states: ..." and returns 1 on rejection. Verified live: `change_job_to Knight` from Outfitter rejected without firing keys.

- [x] **`return_to_world_map` helper** — DONE commit `ca00160`. Universal escape, iterates Escape with detection until WorldMap. Up to 8 attempts. Wired into helpers list for PartyMenu, CharacterStatus, JobSelection, EquipmentAndAbilities. Live-verified from EqA.

- [x] **`view_unit <name>` helper** — DONE commit `ca00160`. Read-only roster dump (name/job/lv/jp/brave/faith/zodiac/equip). No nav, no key presses. Works on any screen exposing roster data.

- [x] **`unequip_all <unit>` helper** — DONE commit `ca00160`. Strips all 5 equipment slots, skips already-empty, reports counts. Live-verified on Cloud — all 5 slots stripped successfully.

- [x] **`travel_safe <id>` helper** — DONE commit `ca00160`. world_travel_to with auto-flee on encounters. Polls 10s, auto-Flees up to 5 times. Live-verified Dorter → TheSiedgeWeald.

- [x] **`scan_inventory` helper** — DONE commit `ca00160`. Opens PartyMenuInventory in verbose mode, dumps full inventory grouped by category. Live-verified — 210 unique items, 2305 owned across 33 categories.

- [x] **`save_game` / `load_game` stubs** — DONE commit `ca00160`. Helper functions added with clear "NOT IMPLEMENTED" message. Underlying C# `save`/`load` actions need real implementations (currently return "Not implemented yet").

- [x] **`start_encounter` helper** — DONE earlier session 23. Validates battleground (IDs 24-42), C+Enter to trigger, 2s wait for animation, Enter to accept Fight. Lands on BattleFormation. Live-verified.

- [x] **`battle_flee` validation** — DONE earlier session 23. Rejects from BattleFormation/EncounterDialog/WorldMap/PartyMenu with friendly message "Can't battle_flee from X. Start the battle first and try again."

- [x] **`fft()` error surfacing** — DONE earlier session 23. Failed commands now print `[FAILED] <error message>` instead of silently returning 0. Unescapes `\u0027` → `'` for readable apostrophes.

- [x] **Compact format: `loc=` → `curLoc=`, gil only on shop screens, WorldMap ui= suppressed** — DONE earlier session 23 (commit `d0e9bc6`). Cleaner one-liner, less noise.

- [x] **JobSelection compact: state/cursor/requires** — DONE commit `d0e9bc6`. `state=Unlocked`, `cursor=(r0,c2)`, `requires=...` for Visible cells.

- [x] **JobSelection verbose: 3-row job grid** — DONE commit `d0e9bc6`. Renders the static layout with `cursor->` row marker and `[ClassName]` cell brackets.

- [x] **EncounterDialog `ui=Fight`** — DONE commit `d0e9bc6`. Hardcoded since cursor always defaults to Fight on encounter prompts.

- [x] **Cutscene `eventId=N`** — DONE commit `d0e9bc6`. Surfaces eventId in compact line.

- [x] **`execute_action` guard via `_fft_guard`** — DONE earlier session 23 (commit `d0e9bc6`). Chained execute_action calls now blocked instead of firing into races.

### Session 19 (2026-04-15) — verification + queued tasks

- [x] **Suppress `ui=Move` outside battle** — VERIFIED already fixed. `CommandWatcher.cs:3328` sets `UI = null` at detector construction; `BattleMyTurn`/`BattleActing` block at 3405 is the only path that writes Move/Abilities/Wait/Status/AutoBattle labels. Non-battle screens show their own context labels (Ramza, Equipment & Abilities, etc.) or stay null. Live-verified 2026-04-15 on WorldMap, PartyMenu, CharacterStatus — no leak observed.

- [x] ⚠ UNVERIFIED — **JP Next on CharacterStatus header** — SHIPPED commit fe8d41e. `AbilityJpCosts.cs` (13 skillsets + blanket Geomancy/Bardsong/Dance) → `RosterReader.ComputeNextJp(slotIndex, primarySkillset)` → `screen.nextJp` → fft.sh header rendering as `Next N`. 10 unit tests. Live-verify deferred: save has no unit with partially-learned priced primary skillset (all Lv99 generics + story chars on Limit/Holy Sword/etc).

- [x] **Zodiac sign per unit (story chars)** — SHIPPED commit 1674bb6. `ZodiacData.cs` covers 13 story characters by nameId. Ramza and generics return null pending memory-hunt for the roster zodiac byte. Live-verified on Agrias (Cancer glyph ✓). 11 unit tests. Generic zodiac still TODO — hunted 0x00-0x100+ offsets with 4 anchor points (Agrias/Mustadio/Orlandeau/Cloud canonical values), no match found; encoding may be nibble-packed, outside the 0x258 stride, or non-standard.

- [x] ⚠ UNVERIFIED — **Wire `SkillsetItemLookup` into scan_move ability surfacing** — SHIPPED commit 0c25e29. `AbilityEntry` gained `HeldCount`/`Unusable` fields. `ScanMove` reads inventory bytes once per scan and probes Items/Iaido/Throw for each ability. Live-verify deferred: requires a battle with a Chemist/Ninja/Samurai active.

### Session 18 (2026-04-15) — verified via audit agents

- [x] ⚠ UNVERIFIED — **`change_reaction_ability_to <name>` helper** — shipped session 13.
- [x] ⚠ UNVERIFIED — **`change_support_ability_to <name>` helper** — shipped session 13.
- [x] ⚠ UNVERIFIED — **`change_movement_ability_to <name>` helper** — shipped session 13.
- [x] ⚠ UNVERIFIED — **`change_secondary_ability_to <skillsetName>` helper** — shipped session 13.
- [x] ⚠ UNVERIFIED — **`remove_ability <name>` helper** — shipped session 13. Unequip a passive by re-pressing Enter on the already-equipped entry.
- [x] **`change_job_to <jobName>` helper** — DONE 2026-04-15 commit c25f0f4. Routes through JobSelection → JobActionMenu → JobChangeConfirmation. Refuses on Locked/Visible cells. Live-verified Ramza Gallant Knight ↔ Chemist ↔ Monk.
- [x] ⚠ UNVERIFIED — **`JobActionMenu` ui= label** — DONE 2026-04-15. `ui=Learn Abilities` / `ui=Change Job` driven by `JobActionIndex`.
- [x] ⚠ UNVERIFIED — **`JobChangeConfirmation` ui= label** — DONE 2026-04-15. `ui=Confirm` / `ui=Cancel` driven by `JobChangeConfirmSelected`.
- [x] ⚠ UNVERIFIED — **JobSelection cursor row-cross desync** — FIXED session 15 commit 5fdefa6. Widget heap reallocates per row cross (`0x11EC34D3C` → `0x1370CF4A0` after single Down). `InvalidateJobCursorOnRowCross` clears resolved address on every Up/Down key while on JobSelection, forcing re-resolve. Horizontal movement doesn't trigger it.

- [x] ⚠ UNVERIFIED — **JobSelection cell state (Locked / Visible / Unlocked) three-state classification** — SHIPPED session 15 commit 129f279. `screen.jobCellState` populated via party-wide skillset union proxy; `ui=` reflects state; `change_job_to` refuses on Locked/Visible. (Follow-up leaf tasks — live verification, Mime prereq, unlock text, etc. — broken out into individual open items above.)

- [x] ⚠ UNVERIFIED — **Normalize screen state names to CamelCase (no underscores)** — DONE commit 3087140. All `Battle_*` and `Outfitter_*` state names drop underscores. `ScreenDetectionLogic.cs` returns `OutfitterSell`/`BattleAttacking`/etc. Zero underscored state names remain.

- [x] **Full sell inventory inline at Outfitter_Sell** — DONE session 18 (commits 9287e5e, 93b5579). `InventoryReader.ReadSellable()` + `ItemPrices.cs` + `CommandWatcher` populates `screen.inventory` on OutfitterSell with `{name, heldCount, sellPrice, type}`. Verified + estimated sell-price distinction (`sell=` vs `sell~`). Live-verified session 21: 146 sellable items, grouped by type, with counts and prices.

- [x] ⚠ UNVERIFIED — **Full equipment picker inline at Outfitter_Fitting** — DONE commit 93b5579. `screen.inventory` (ReadAll) surfaces on OutfitterFitting. Slot-type filter deferred (requires Fitting picker-depth state tracking).

- [x] ⚠ UNVERIFIED — **Read player inventory for Sell** — DONE commit 0438aca. Inventory store cracked at `0x1411A17C0` (272 bytes, flat u8 array indexed by FFTPatcher item ID). `InventoryReader.cs` + 10 unit tests.

- [x] ⚠ UNVERIFIED — **`ReturnToWorldMap` validPath on every PartyMenu-tree screen** — DONE commit 34b5927. `NavigationPaths.cs` has 15+ entries across PartyMenu, PartyMenuInventory, CharacterStatus, EquipmentAndAbilities, JobSelection, JobActionMenu, pickers, Chronicle, Options with graduated Escape counts (1–5).

- [x] ⚠ UNVERIFIED — **Delete `Battle_AutoBattle` rule** — DONE. `ScreenDetectionLogic.cs:16` — UI label on Battle_MyTurn handles cursor=4.

- [x] ⚠ UNVERIFIED — **Collapse `Battle_Casting` into `Battle_Attacking`** — DONE. `ScreenDetectionLogic.cs:17,289` — cast-time and instant collapse into BattleAttacking; ct>0 tracked client-side.

- [x] ⚠ UNVERIFIED — **Add `LoadGame` rule** — DONE. `ScreenDetectionLogic.cs:235-240` — `slot0==255 && paused==0 && battleMode==0 && !atNamedLocation → LoadGame`.

- [x] ⚠ UNVERIFIED — **Add `LocationMenu` rule** — DONE. `ScreenDetectionLogic.cs:172-178` — `atNamedLocation → LocationMenu`.

- [x] ⚠ UNVERIFIED — **Rename `clearlyOnWorldMap` to `atNamedLocation`** — DONE. `ScreenDetectionLogic.cs:39` — zero `clearlyOnWorldMap` references remain.

### Earlier sessions

- [x] **EquipmentAndAbilities Abilities column surfaces `ui=(none)` on slots with no equipped ability** — DONE 2026-04-15 session 17 (commit e8aaa9f). Slot-aware fallback now emits `Right Hand (none)` / `Left Hand (none)` / `Headware (none)` / `Combat Garb (none)` / `Accessory (none)` on the equipment column and `Primary (none — skillset table missing for this job)` / `Secondary (empty)` / `Reaction (empty)` / `Support (empty)` / `Movement (empty)` on the ability column. Also populated 17 story-class primaries in `GetPrimarySkillsetByJobName` so Cloud/Mustadio/Rapha/etc no longer hit the Primary fallback at all. Live-verified on Cloud (`ui=Limit`) and his empty Secondary row (`ui=Secondary (empty)`). — logged 2026-04-14 session 13. Repro: open Cloud's EquipmentAndAbilities (Cloud's Primary is blank because "Soldier" isn't in `GetPrimarySkillsetByJobName`), cursor Right into the Abilities column. `ui=(none)` surfaces — bare and uninformative. Better: (a) populate the missing story-class primaries (Soldier=Limit, Dragonkin=Dragon, Steel Giant=Work, Machinist=Snipe, Skyseer/Netherseer=Sky/Nether Mantra, Divine Knight=Unyielding Blade, Templar=Spellblade, Thunder God=All Swordskills, Sky Pirate=Sky Pirating, Game Hunter=Hunting — verify each in-game before adding). (b) Change the `(none)` fallback in `CommandWatcher` EquipmentAndAbilities ability-cursor branch to `Primary (none)` / `Secondary (none)` / `Reaction (empty)` / `Support (empty)` / `Movement (empty)` so the row intent is at least visible. (c) Consider: Primary row should never surface `(none)` anyway — it's job-locked, so we should always know the primary skillset name from the job; a blank means our job-name map is incomplete.



- [x] **Extend `ItemInfo` with attributeBonus / equipmentEffects / attackEffects / dualWield / twoHanded fields, then populate** — DONE 2026-04-15 session 17 (commits 0752b3f + latest). `ItemInfo` record grew 6 fields (`AttributeBonuses`, `EquipmentEffects`, `AttackEffects`, `CanDualWield`, `CanWieldTwoHanded`, `Element`). Populated ~30 hero items: Materia Blade, Defender, Save the Queen, Excalibur, Ragnarok, Chaos Blade, Aegis/Escutcheon+/Kaiser/Ice/Flame Shield, Gold Hairpin, Thief's Cap, Ribbon, Genji Glove/Armor, Bracer, Power Gauntlet, Magepower Glove, Rubber Suit, White/Black/Lordly Robe, Hermes/Battle/Germinas/Rubber/Red/Spiked Boots, Angel Ring, Cursed Ring, Reflect Ring, Chantage, Nu Khai Armband, Mirage Vest. Surfaced through `UiDetail` + fft.sh detail panel. 12 unit tests in `Tests/Utilities/BuildUiDetailTests.cs`. Still to-do: remaining ~200 items unpopulated (NXD bulk-port is a follow-up; hero-item coverage is the hot path). — added 2026-04-14. The game's item info panel has 3 pages (verified live in Outfitter Try-then-Buy on Ragnarok): page 1 = WP/evade/range (already in ItemInfo), page 2 = Attribute Bonuses (e.g. PA+1, MA+2), Equipment Effects (e.g. "Permanent Shell"), Standard Attack Effects (e.g. on-hit Petrify), page 3 = Weapon Type flags (Can Dual Wield / Can Wield Two-handed) and Eligible Jobs. Without these fields the `uiDetail` description is incomplete for many items and Claude can't tell that Ragnarok grants permanent Shell etc. Strategy: extend `ItemInfo` record with the new fields, populate the ~30 most-used hero items by hand from the FFHacktics wiki (Ragnarok, Excalibur, Chaos Blade, Maximillian, Crystal Mail, Bracer, Chantage, etc.), then bulk-populate the rest from the game's NXD item table in a follow-up. Skip Eligible Jobs for now (low value, lots of data). Surface the new fields in `UiDetail` and render in fft.sh below the existing stats line.



- [x] ⚠ UNVERIFIED — **`screen -v` doesn't include the new EquipmentAndAbilities/picker payloads** — DONE 2026-04-15 session 17. EqA detail panel renders new `ItemInfo` fields inline (Bonuses / Effects / On-hit / weapon-type flags / element). Pickers (Secondary/Reaction/Support/Movement) carry `Job` + `Description` per `AvailableAbility`, rendered as `- <name>  (<job>)  [equipped]` + wrapped description under `screen -v`. Compact stays single-line. — added 2026-04-14. Compact `screen` shows the three-column Equipment/Abilities/Detail layout + cursor marker on EquipmentAndAbilities, and the `Available skillsets/reactions/supports/movement (N):` list on pickers. Verbose mode (`screen -v`) currently only changes PartyMenu output (full roster grid). It should ALSO surface fuller detail when -v is set on EquipmentAndAbilities / pickers — e.g. show the full long-form description (we currently wrap at 40 chars in compact, could be 80+ in verbose), expand all picker entries with their stats (Job + Description preview per row), maybe show all three pages of the in-game item info panel (Attribute Bonuses, Equipment Effects, Standard Attack Effects, Eligible Jobs) once `ItemInfo` carries that data. Implementation: add `if (verbose)` branches in fft.sh's EquipmentAndAbilities and picker rendering blocks.



- [x] **Block `world_travel_to` to current location** — DONE 2026-04-15 session 17 (commit e8aaa9f). `NavigationActions.TravelTo` reads world location byte at 0x14077D208 before any keys fire; same-location returns `status=rejected`. Live-verified. — Calling world_travel_to with the location ID of the current standing node opens the travel list with the cursor on the current node, and the blind "press Enter to confirm" flow selects it. The game then gets stuck in an undefined state (travel modal opens, input routing goes to a subwindow, subsequent Enter presses are swallowed). Detect and refuse: if `locationId == currentLocationId` (where currentLocationId is the WorldMap cursor hover OR the last-arrived location), return `{status: "rejected", error: "Already at <name>. Use execute_action EnterLocation to enter the location menu."}`. 2026-04-14 — observed breaking the Dorter shop run.


- [x] **Pre-press `C` (or middle mouse button) to recenter cursor before `EnterLocation`** — DONE 2026-04-15 session 17 (commit e8aaa9f). `GetWorldMapPaths` emits `[C, Enter]` with `DelayBetweenMs = 200`. Live-verified (`keysProcessed=2`, lands on LocationMenu). — User discovered 2026-04-14: the game binds `C` / middle-mouse to "recenter WorldMap cursor on current node". This is the clean fix for the "Enter does nothing because cursor drifted" problem. Implementation: in the `EnterLocation` ValidPath handler (NavigationPaths.cs → GetWorldMapPaths), prepend a `C` key press before the Enter. Single key, deterministic, no memory-reading needed. This supersedes the "Block EnterLocation when cursor isn't on the current settlement" TODO below — just always recenter first and the edge case disappears.


- [x] **Locked/unrevealed locations** — DONE 2026-04-15 session 17 (commits 0752b3f + latest). `0x1411A10B0` is NOT a bitmask — it's **1 byte per location** (0x01 unlocked, 0x00 locked), verified live. Two features: (a) `TravelTo` refuses travel to locked locations with a clear error, (b) `screen.unlockedLocations` surfaces the full array on WorldMap/TravelList (bytes 0..52). Live-verified: 50 unlocked IDs in endgame save, location 35 correctly excluded. — Read unlock bitmask at 0x1411A10B0 and skip locked locations.


- [x] **Ability picker state machine desync: Enter equipped an ability but state machine incorrectly returned to EquipmentAndAbilities** — FIXED 2026-04-14 session 13. Root cause: `ScreenStateMachine.HandleAbilityPicker` treated both `VK_RETURN` and `VK_ESCAPE` as picker-close events, but in the real game `Enter` only equips (picker stays open, shows checkmark); only `Escape` actually closes the picker. Fix: removed Enter from the close-transitions in HandleAbilityPicker; the picker now stays open on Enter and consumers (fft.sh helpers, Claude navigation) must send Escape to close. Verified live by running `change_reaction_ability_to` helper which cleanly equips + Escape-closes back to EquipmentAndAbilities.



- [x] **JobSelection auto-resolver race during screen transitions** — FIXED 2026-04-15 session 15 via the MenuDepth==2 memory gate (commit 3d8638b). The auto-resolver now waits until `screen.MenuDepth` reads 2 (game-confirmed inner panel render) before firing its 6 Right/Left keys. State machine still flips to JobScreen synchronously on Enter, but the resolver no longer races the open animation. Live-verified: batched `Down → Enter → Down → Enter` from a clean PartyMenu lands cleanly on the target unit's JobSelection with no outer-cursor drift.



- [x] **State machine drifts from reality on PartyMenu entry** [Detection] — PARTIALLY FIXED 2026-04-14 session 13 via memory-backed drift recovery. Memory byte `0x14077CB67` (menuDepth) cleanly distinguishes outer party-menu-tree screens (WorldMap/PartyMenu/CharacterStatus = 0) from inner panels (EquipmentAndAbilities/ability picker = 2). CommandWatcher.DetectScreen now runs a debounced check — if the state machine thinks we're on an inner panel but menuDepth reads 0 for 3 consecutive reads, snaps back to CharacterStatus (with `MarkKeyProcessed()` to prevent cascade into the older PartyMenu stale-state recovery). Live-verified in session 13 — helpers that used to desync now self-correct. Still outstanding: the inner-panel mid-restart case (restart happens while player is already on EqA or a picker) isn't directly covered — the byte reads 2 correctly, but the state machine has no way to know WHICH inner panel (EqA vs which picker). Lower priority because the common case (restart on PartyMenu/CharacterStatus) is now fixed.



- [x] ~~**Block `EnterLocation` when WorldMap cursor isn't on the current settlement**~~ — Superseded 2026-04-14 by the `C`-key recenter fix above. Leaving the strikethrough for history: the original symptom was `EnterLocation` silently no-oping because the cursor had drifted off the node. Instead of refusing the action, we just recenter before pressing Enter. Keeping the TODO marked done so the fix implementation is tracked in one place.


- [x] **PartyManagement.md** — Written 2026-04-13.


- [x] **Shopping.md** — Written 2026-04-14. See `FFTHandsFree/Instructions/Shopping.md`.


- [x] **FormationScreen.md** — Written 2026-04-13.


- [x] **SaveLoad.md** — Written 2026-04-13.


- [x] ⚠ UNVERIFIED — LocationMenu detection (locationMenuFlag at 0x140D43481) — mapped 2026-04-14


- [x] ⚠ UNVERIFIED — LocationMenu shop type (Outfitter/Tavern/WarriorsGuild/PoachersDen) via shopTypeIndex at 0x140D435F0 — mapped 2026-04-14


- [x] ⚠ UNVERIFIED — ShopInterior detection (insideShopFlag at 0x141844DD0) — mapped 2026-04-14, partially reliable (doesn't always fire on a fresh process)


- [x] ⚠ UNVERIFIED — Outfitter sub-actions: Outfitter_Buy / Outfitter_Sell / Outfitter_Fitting via shopSubMenuIndex at 0x14184276C (values 1/4/6) — mapped 2026-04-14



- [x] ⚠ UNVERIFIED — **Rename `ShopInterior` → `SettlementMenu`** — DONE 2026-04-14. ❗ **But done WRONG.** The rename was applied to the WRONG layer — see follow-up below.


- [x] ⚠ UNVERIFIED — **Gil in state** [P0 quick win] — DONE 2026-04-14. Gil at 0x140D39CD0 surfaces on shop-adjacent screens (WorldMap, PartyMenu, LocationMenu, ShopInterior, Outfitter_Buy/Sell/Fitting) via ShopGilPolicy.


- [x] ⚠ UNVERIFIED — **Format gil with thousands separators** — DONE 2026-04-14. `_fmt_gil` helper in fft.sh renders via `printf "%'d"` under `LC_ALL=en_US.UTF-8`. JSON unchanged.


- [x] ⚠ UNVERIFIED — **Shop list cursor row index** — DONE 2026-04-14. `0x141870704` (u32) tracks the currently-highlighted row inside Outfitter_Buy/Sell/Fitting. Row 0 = top item, increments per ScrollDown. Persists across sub-action cycling. Found via 4-way module_snap diff at Dorter Outfitter (rows Oak→White→Serpent→Oak).


- [x] **Shopping.md instruction guide** — DONE 2026-04-14. Initial version covers detection and ValidPaths flow; will need revisions as action helpers (`buy_item`, etc.) land.



- [x] ⚠ UNVERIFIED — **`PartyMenu` top-level tabs** — DONE 2026-04-14. Uses `ScreenStateMachine.Tab` (driven by Q/E key history, now wraps both directions) to resolve detection to `PartyMenu` / `PartyMenuInventory` / `PartyMenuChronicle` / `PartyMenuOptions`. Memory scan for a tab-index byte was inconclusive — heap diff found 2029 candidates with the right 0/1/2/3 shape but none survived re-verification (UE4 widget heap reallocates per keypress). State-machine-driven detection is the working answer. Each tab has its OWN screen name (not just `PartyMenu ui=<tab>`) because the content differs entirely per tab:
  - `PartyMenu` — Units tab (the roster grid; covered below)
  - `PartyMenuInventory` — Inventory tab (item catalog; covered below)
  - `PartyMenuChronicle` — Chronicle tab (lore/events browser; covered below)
  - `PartyMenuOptions` — Options tab (save/load/settings; covered below)
  Shared ValidPaths across all four: `NextTab` (E wraps), `PrevTab` (Q wraps), `WorldMap` (Escape back out).


- [x] ⚠ UNVERIFIED — **Full roster grid on `PartyMenu` (Units tab)** — DONE 2026-04-14 (see "Data surfacing" entry below for delivery notes). 5-col grid with cursor marker, name/level/job/brave/faith. `navHints` block was NOT shipped and not needed — Claude reads cursor + grid and plans navigation directly. Roster capacity (`14/50`) shipped. HP/MP per unit still missing — separate item, deferred.


- [x] ⚠ UNVERIFIED — **`PartyMenuChronicle` tab** — DONE 2026-04-14. State machine tracks `ChronicleIndex` (0-9 flat) over the 3-4-3 grid (Encyclopedia/StateOfRealm/Events / Auracite/Reading/Collection/Errands / Stratagems/Lessons/AkademicReport). `screen.UI` surfaces tile name (`Encyclopedia`, `Auracite`, etc.). Verified row transitions live: Encyc→Auracite, SoR→Reading, Events→Collection, Errands→Akademic (last col wraps left), Akademic→Collection (up). Memory hunt for the cursor address failed (UE4 widget heap reallocates per keypress producing false positives — same wall as PartyMenuInventory — see `project_shop_stock_array.md`). Each tile opens its own sub-screen via Enter, surfaces as `ChronicleEncyclopedia`/`ChronicleStateOfRealm`/etc. Sub-screens currently model only the boundary (Escape back) — inner-state navigation (Encyclopedia tabs, scrollable lists, etc.) is deferred to §10.7 below.


- [x] ⚠ UNVERIFIED — **`PartyMenuOptions` tab** — DONE 2026-04-14. State machine tracks `OptionsIndex` (0-4 vertical, wraps both directions). `screen.UI` surfaces action name (`Save`, `Load`, `Settings`, `Return to Title`, `Exit Game`). Enter on Settings opens new `OptionsSettings` screen (boundary only). Save/Load/ReturnToTitle/ExitGame Enter actions don't open sub-screens via the state machine — those flows are handled by their own existing systems (`save`/`load` actions, title-screen/quit sequences not yet modelled).


- [x] ⚠ UNVERIFIED — **`CharacterStatus` sidebar** — DONE 2026-04-14. `screen.UI` populated from `ScreenStateMachine.SidebarIndex` (now wraps both directions). Reads "Equipment & Abilities" / "Job" / "Combat Sets". No memory scan needed — sidebar is purely keyboard-driven and the state machine tracks Up/Down reliably.


- [x] ⚠ UNVERIFIED — **Equipment Effects toggle (`R` key on `EquipmentAndAbilities`)** — DONE 2026-04-14. State machine tracks `EquipmentEffectsView` (toggled by `R`); CommandWatcher surfaces it as `equipmentEffectsView` boolean on the screen response. Resets when leaving the screen. Effects panel TEXT scrape (e.g. "Permanent Shell", "Immune Blindness") still TODO — needs a memory scan or widget hook. Sub-bullets below also done:
  - Default view: `ui=<highlighted item or ability name>` (current spec).
  - Effects view: new sub-state or a flag like `EquipmentAndAbilities ui=EquipmentEffects view=Effects`. The bottom-right hint reads `[R] Equipment Effects` in the default view and `[R] View Equipment` in the effects view — confirming it's a binary toggle on the same screen.
  - ValidPaths: add `ToggleEffectsView` action that wraps the `R` key. Detection: needs a memory scan for the view flag (binary). Scrape the effects panel text as its own payload field when the flag is on.


- [x] ⚠ UNVERIFIED — **Full stats panel toggle (`1` key on `CharacterStatus`)** — DONE 2026-04-14. State machine tracks `StatsExpanded` (toggled by `1`); CommandWatcher surfaces it as `statsExpanded` boolean. Resets when leaving CharacterStatus. The actual stat NUMBERS (Move/Jump/PA/MA/PE/ME/WP-R/WP-L/Parry/etc.) still TODO — needs a memory scan for each stat's address.
  - Model as a view flag on `CharacterStatus`: `statsExpanded: true/false`.
  - When expanded, surface the full stat block in the screen response (NOT just the cursor label). This supersedes the "Full stat panel on CharacterStatus" entry below — the data IS already rendered numbers, just needs scraping when the flag is on.
  - ValidPaths: add `ToggleStatsPanel` action that wraps the `1` key.
  - Detection: needs a memory scan for the stats-expanded flag (binary).


- [x] **Character dialog (spacebar on `CharacterStatus`)** — DONE 2026-04-14. New state `CharacterDialog` detects via state machine. Only Enter advances (Escape is a no-op on dialogs in this game). Detection live-verified.


- [x] **Dismiss Unit flow (hold B on `CharacterStatus`)** — DONE 2026-04-14. Added `hold_key <vk> <durationMs>` action in CommandWatcher and `dismiss_unit` shell helper in fft.sh. When VK_B is held ≥3s on CharacterStatus, the state machine transitions to DismissUnit. Cursor defaults to Back (safe). `ui=Back/Confirm` reflects the toggle. Live-verified on Kenrick. Action helper `dismiss_unit <name>` (find unit, navigate to status, hold B, confirm) still TODO — current `dismiss_unit` only fires the held key, doesn't navigate.


- [x] ⚠ UNVERIFIED — **Rename `Equipment_Screen` → `EquipmentAndAbilities`** — DONE 2026-04-14 (screen name only; `GameScreen.EquipmentScreen` enum still legacy, renamed in the ScreenDetectionLogic → CommandWatcher mapper). The `ui=<highlighted item name>` inner-cursor work (Ragnarok / Escutcheon / Mettle / etc.) is still TODO — requires decoding the game's cursor position inside the two-column panel.


- [x] **New states for ability slots** — DONE 2026-04-14. `SecondaryAbilities` / `ReactionAbilities` / `SupportAbilities` / `MovementAbilities` detected via state machine routing on EquipmentAndAbilities Enter (right column rows 1-4). Row 0 (Primary action) is intentionally a no-op — the primary skillset is job-locked. All 4 pickers live-verified. Inner `ui=<skillset/ability name>` cursor decoding still TODO — needs the picker's selected-row address. The slot details below are now historical context:
  - `SecondaryAbilities` — ui=<skillset name> (Items, Arts of War, Aim, Martial Arts, White Magicks, Black Magicks, Time Magicks, ...). Screenshot showed Mettle + Items + Arts of War + Aim + Martial Arts + White/Black/Time Magicks. Primary action (row 0) is job-locked — no picker opens; change job via JobSelection to change the primary skillset.
  - `ReactionAbilities` — ui=<ability name> (Parry, Counter, Auto-Potion, ...).
  - `SupportAbilities` — ui=<ability name> (Magick Defense Boost, Equip Heavy Armor, Concentration, ...).
  - `MovementAbilities` — ui=<ability name> (Movement +3, Move-Find Item, Teleport, ...).


- [x] **New states for equipment slots** — DONE 2026-04-14. `EquippableWeapons` / `EquippableShields` / `EquippableHeadware` / `EquippableCombatGarb` / `EquippableAccessories` detected via state machine routing on EquipmentAndAbilities Enter (left column rows 0-4). All 5 pickers live-verified. `EquipmentSlot` enum added to ScreenStateModels.cs; CommandWatcher uses `CurrentEquipmentSlot` (captured at Enter time) to surface the correct slot-specific screen name. Inner `ui=<item name>` decoding still TODO. Slot details below are historical context:
  - `EquippableWeapons` — ui=<weapon name> (Ragnarok, Materia Blade, Chaos Blade, Blood Sword, Excalibur, Save the Queen, ...). Columns show Equipped/Held counts.
  - `EquippableShields` — ui=<shield name> (Escutcheon, Aegis Shield, ...).
  - `EquippableHeadware` — ui=<helm name> (Grand Helm, Crystal Helm, ...).
  - `EquippableCombatGarb` — ui=<armor name> (Maximillian, Carabini Mail, ...).
  - `EquippableAccessories` — ui=<accessory name> (Bracers, Genji Gauntlet, Reflect Ring, ...).


- [x] ⚠ UNVERIFIED — **Rename `Job_Screen` → `JobSelection` + `ui=<job name>`** — DONE 2026-04-14 (rename) and 2026-04-15 (ui= inner-cursor work — commits 4a0b53e + 777c189). Heap rescan-on-entry resolver finds the cursor byte; `screen.UI` surfaces the hovered class with cell-state aware labels (`<name>`, `<name> (not unlocked)`, or `(locked)`). `screen.cursorRow`, `screen.cursorCol`, `screen.jobCellState`, `screen.viewedUnit` all populated. `GameScreen.JobScreen` enum name still legacy.


- [x] ⚠ UNVERIFIED — **`CombatSets` state** — DONE 2026-04-14 (boundary detection only). Pressing Enter on the third sidebar item now transitions to `CombatSets` in the state machine; Escape returns to CharacterStatus. Inner navigation NOT modeled — user explicitly opted to defer (loadouts feature not in use). Add Up/Down/Enter handlers when needed.



- [x] **Full roster grid on `PartyMenu` (Units tab)** — 2026-04-14 landed slot-indexed list with slot, name, level, job, brave, faith. Empty-slot rule verified = `unitIndex != 0xFF && level > 0`. **Display order solved 2026-04-14 (session 13)**: roster byte `+0x122` (1 byte per slot) holds each unit's 0-indexed grid position under the game's current Sort option (default: Time Recruited). Discovered by dumping all 14 active slots' first 600 bytes and scanning for a strictly-monotonic ranking — offset 290 (0x122) was a perfect `[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13]`. Verified live per-slot: Ramza s0=0, Kenrick s1=1, ..., Mustadio s11=4 (displays 5th in grid, before Reis s6 which has DisplayOrder=12). **NOW DELIVERED:** sorted list matches visible grid, cursor `(row, col)` tracked by state machine, `ui=<hovered name>`, drill-in to any unit surfaces that unit's real loadout/abilities/stats. fft.sh compact mode renders a 5-col grid matching the game's layout with `cursor->` gutter; `screen -v` dumps raw JSON with `gridCols`, `gridRows`, `cursorRow`, `cursorCol`, `hoveredName`, and `displayOrder` per unit. See `RosterReader.DisplayOrder`, `RosterReader.GetSlotByDisplayOrder`, `ScreenStateMachine.ViewedGridIndex`.
  - **HP/MP still not in roster.** Scanned Ramza's full 0x258 bytes for his displayed HP (719 = 0x02CF) and MP (138 = 0x008A): zero matches. Theory: runtime-computed from job base + equipment bonuses, OR stored in a separate per-slot live stats table in the UE4 heap. Partial answer via the hovered-unit heap array (BATTLE_MEMORY_MAP.md §19) — populated for a handful of units only. Future work: recompute from FFTPatcher formulas OR widget pointer-chain walk. Separate item below.
  - **Custom sort modes (Job / Level) — not yet tested.** The `+0x122` byte is re-written by the game when the player changes the Sort option, so display order stays accurate under any sort — but `IsRamza` in the state machine still assumes grid-pos-0 === Ramza, which breaks under Level sort (multiple lv99 units tie; game picks one deterministically). If non-default sort becomes a goal, resolve `IsRamza` via slot identity instead of grid position. Documented inline in `ScreenStateMachine.HandlePartyMenu`.


- [x] ⚠ UNVERIFIED — **Viewed-unit identification on EquipmentAndAbilities** — DONE 2026-04-14 (session 13). Resolved purely from the state machine: cursor (row, col) on PartyMenu → grid index (row × 5 + col) → roster slot whose `+0x122` byte equals that grid index (see `ScreenStateMachine.ViewedGridIndex`, `RosterReader.GetSlotByDisplayOrder`). Zero heap scan, zero AoB — the display-order byte lives in the stable roster array at `0x1411A18D0`. Previous plans (a/b/c above in the history) are no longer needed. The hovered-unit heap array from BATTLE_MEMORY_MAP.md §19 is still useful IF we want runtime HP/MP (not stored in the roster), but it's now a separate concern.


- [x] **Element resistance grid** — DROPPED 2026-04-15 session 15. Niche planning data; Claude doesn't pick equipment reactively to elements between battles. Reconsider only if a build-optimization flow emerges.


- [x] **Equipped items with stat totals on `EquipmentAndAbilities`** — DROPPED 2026-04-15 session 15. Equipment stats are derivable from individual `ItemInfo` records; aggregating them server-side is convenience that doesn't change a decision. If Claude needs the total, it can compute from per-item data already surfaced.


- [x] **JP totals per job on `JobSelection`** — DROPPED 2026-04-15 session 15 per the "What Goes In Compact vs Verbose vs Nowhere" principle above. Claude doesn't need 19 JP values to make a job-change decision; hovering a cell already shows Lv + JP in-game (info panel). Reconsider only if a concrete decision flow emerges that needs the full grid in one round trip.


- [x] ⚠ UNVERIFIED — **Ability list with learned/unlearned inside picker screens** — DONE 2026-04-14. `screen.availableAbilities` surfaces the full learned list for SecondaryAbilities (unlocked skillsets), ReactionAbilities (19 for Ramza), SupportAbilities (23), MovementAbilities (12). SecondaryAbilities puts the equipped skillset first (matches game's default cursor); other pickers use canonical ID-sorted order with the equipped ability marked in place. Decoded via roster byte 2 of the per-job bitfield at +0x32+jobIdx*3+2 (MSB-first over each job's ID-sorted passive list — see `ABILITY_IDS.md` and `RosterReader.ReadLearnedPassives`). JP cost + "unlearned-but-could-be-learned" still TODO — requires a separate learnable-set, not just learned-set.



- [x] ⚠ UNVERIFIED — **`PartyMenu` tab switch actions** — `OpenInventory`, `OpenChronicle`, `OpenOptions`, `OpenUnits` all wired in `NavigationPaths.cs` across every PartyMenu tab (each tab gets a no-op entry for its own name and key sequences for the other three). Verified 2026-04-15 session 16 — stale TODO item; already landed alongside the Chronicle/Options work in session 13.


- [x] **`EquipmentAndAbilities` directional semantics** — DROPPED 2026-04-15 session 15. `FocusEquipmentColumn` / `FocusAbilitiesColumn` were going to wrap Left/Right with named aliases. Left/Right are unambiguous; the named version adds noise to validPaths without changing any decision.


- [x] ⚠ UNVERIFIED — **`JobSelection` validPaths (grid nav, Select, Back)** — DONE 2026-04-15 session 15 (already present in the JobScreen response).


- [x] ⚠ UNVERIFIED — Chronicle tab + sub-tile detection — done 2026-04-14, see §10.6 / §10.7



- [x] ⚠ UNVERIFIED — **Memory scan for shop-type discriminator** — DONE 2026-04-14. shopTypeIndex at 0x140D435F0 distinguishes Outfitter/Tavern/WarriorsGuild/PoachersDen at LocationMenu. Outfitter sub-actions (Buy/Sell/Fitting) further split by shopSubMenuIndex at 0x14184276C. Save Game and other shop sub-actions still TODO — see Section 10.


- [x] ⚠ UNVERIFIED — **scan_move disrupts targeting mode** [State] — Fixed 2026-04-13: removed Battle_Attacking and Battle_Casting from scan-safe screens.


- [x] **EncounterDialog detection: wire 0x140D87830 as encounter flag** — DONE session 21. Wired into ScreenAddresses[28], added `encounterFlag` param to `Detect()`, re-enabled both EncounterDialog rules. Cross-session verified: flag=10 during encounter, 0 after flee, no false triggers on WorldMap/PartyMenu/LocationMenu. 5 new tests (2048 total).


- [x] **PartyMenu tab desync: delay/confirm approaches** — SUPERSEDED by session 20 SM-first architecture. Party-tree key responses now use SM state directly, bypassing detection entirely. Multi-press tab jumps no longer cause stale reads.


- [x] **PartyMenu tab desync: find non-Units tab discriminator memory byte** — **Inventory flag DONE** session 20 commit 5da81c2: `0x140D3A38E` = 1 on Inventory. Chronicle/Options discriminator NOT FOUND — deferred. Session 20 also overhauled detection architecture (SM-first for party tree) which eliminated most tab-related drift.


- [x] **PartyMenu screen identity drift (a)** — FIXED session 20. SM-first architecture eliminates stale-byte reads for party-tree transitions. Tab flags (41E/38E) wired into detection as fallback. EqA mirror promotion guarded by tab flags + world-side detection + SM-in-party-tree check.
