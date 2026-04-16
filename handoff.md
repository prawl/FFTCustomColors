# Session Handoff — 2026-04-16 (Session 22)

Delete this file after reading.

## TL;DR

**Party menu system overhaul: compact views, ValidPaths for all 51 states,
Helpers section, compound navigation, C# bridge actions.** Memory writes
proven (gil change live-verified). Authoritative screen detection prototyped
but stashed — chain-from-EqA viewedUnit lag unsolved.

**Tests: 2046 passing** (net -2 from removing dead EquipmentScreen/JobScreen states).

**Commits (6):**
1. `21845de` — Compact EqA view, ValidPaths cleanup, Helpers section, Q/E unit cycling
2. `5aa2124` — Enrich execute_action responses + compound navigation helpers
3. `2977b7b` — Complete ValidPaths coverage + CharacterStatus verbose + compound nav cursor fix
4. `2699cc7` — New helpers: party_summary, check_unit, swap_unit, enter_shop, save_and_travel
5. `e426bb1` — Add auto_place_units, open_picker helpers + EnterLocation delay fix
6. `06ee3ac` — C# bridge actions: open_eqa, open_job_selection, open_character_status, auto_place_units

**Stashed (not committed):**
`git stash list` → "WIP: authoritative screen state + SM grid index sync"
- `CommandWatcher.cs`: authoritative screen name + TTL system, enrichment uses SM grid index
- `NavigationActions.cs`: SetViewedGridIndex after keys fire, ScreenMachine property
- `ScreenStateMachine.cs`: SetViewedGridIndex public method, ModLogger import
- **Issue**: first call from WorldMap works perfectly (Cloud/Orlandeau verified).
  Chain-from-inside-EqA shows correct gear but stale viewedUnit name (lag by one call).
  Root cause: escape storm drift checks reset SM state between SetViewedGridIndex
  and the enrichment read. Fix: either suppress drift checks during bridge actions,
  or move enrichment to run synchronously after SetViewedGridIndex before any detection.

## What landed, grouped by theme

### 1. Compact EqA view (commit 21845de)

```
[EquipmentAndAbilities] ui=Ragnarok viewedUnit=Ramza(Gallant Knight)
  Equip: Ragnarok / Round Shield / Grand Helm / Maximillian / Bracer
  Abilities: Mettle / Items / Counter / Concentration / Jump +2
```
- Two summary lines below one-liner (Equip + Abilities)
- Full grid moved to verbose only (`screen -v`)
- `viewedUnit` includes job: `viewedUnit=Ramza(Gallant Knight)`
- Removed `status=completed` from compact output

### 2. ValidPaths for all 51 states (commits 21845de, 2977b7b)

- Every screen that detection can produce now has ValidPaths
- Added: LoadGame, PartySubScreen, BattleVictory, BattleWaiting
- Chronicle sub-screens (10) + OptionsSettings: Back + ReturnToWorldMap
- Fixed equippable picker keys: Tab→A(prev)/D(next)
- Removed dead states: EquipmentScreen, JobScreen
- Added Q/E PrevUnit/NextUnit to JobSelection

### 3. Helpers section (commits 21845de, 2699cc7, e426bb1)

`execute_action` responses now show a Helpers section below ValidPaths
listing available shell helpers for the current screen:

- **WorldMap/PartyMenu**: open_eqa, open_job_selection, open_character_status,
  party_summary, check_unit, save_and_travel, enter_shop
- **EquipmentAndAbilities**: all ability change/list/remove helpers, swap_unit, open_picker
- **CharacterStatus**: open_eqa, open_job_selection, dismiss_unit, swap_unit, check_unit
- **JobSelection**: change_job_to, swap_unit
- **BattleFormation/EncounterDialog**: auto_place_units
- **GameOver**: load

### 4. Enriched execute_action responses (commit 5aa2124)

`execute_action` now uses `_fmt_screen_compact` (same renderer as `screen`)
so responses show full context: `ui=`, `viewedUnit=`, Equip/Abilities lines.
Before: `[EquipmentAndAbilities] status=completed`. After: full compact view.

C# `EnrichUnitScopedScreen()` method populates viewedUnit, ui, loadout, and
abilities on key-press responses for unit-scoped screens.

### 5. Compound navigation helpers (commits 5aa2124, 2699cc7, e426bb1)

- `open_eqa [unit]` — WorldMap → unit's EqA in one command
- `open_job_selection [unit]` — WorldMap → unit's JobSelection
- `open_character_status [unit]` — WorldMap → unit's CharacterStatus
- `party_summary` — formatted one-line-per-unit roster
- `check_unit <name>` — quick stat dump for one unit
- `swap_unit <name>` — Q/E cycle to named unit on unit-scoped screens
- `enter_shop` — WorldMap → Outfitter (validates settlement ID 0-14)
- `save_and_travel <id>` — save then travel (validates WorldMap)
- `auto_place_units` — formation placement + commence battle
- `open_picker <unit> <slot>` — navigate to equipment picker

### 6. C# bridge actions (commit 06ee3ac)

Moved compound navigation from shell multi-call chaining to single C#
bridge actions. Each action handles the entire navigation internally:
- `open_eqa`, `open_job_selection`, `open_character_status`: escape to
  WorldMap → PartyMenu → cursor to unit → target screen
- `auto_place_units`: 4 units × (Enter×2) + Space + Enter + poll for battle
- NavigateToCharacterStatus: detects current screen, uses detection between
  escapes to stop at WorldMap, wrap-resets cursor, navigates to target

### 7. CharacterStatus verbose view (commit 2977b7b)

```
[CharacterStatus] ui=Equipment & Abilities viewedUnit=Cloud(Soldier)
  Cloud  Soldier  Lv 91  JP 44  Brave 70  Faith 65  Zodiac: Aquarius
  HP --/--  MP --/--  PA --  MA --  Speed --  Move --  Jump --
  Equip: Materia Blade / Thief's Cap / Black Garb / Featherweave Cloak
```
Placeholder `--` values for HP/MP/PA/MA/Speed/Move/Jump until memory reads land.

### 8. Memory writes proven

Wrote 9,999,999 to gil address `0x140D39CD0` (4 bytes LE) — game updated
live on the UI. Confirms full read/write memory access works.

## What's NOT done (top priorities for next session)

### 1. Authoritative screen detection (STASHED, not committed)

The chain-from-EqA viewedUnit lag: `open_eqa Cloud` then `open_eqa Agrias`
shows correct Agrias gear but `viewedUnit=Cloud` (stale by one call).

Root cause: the escape storm (6 Escapes to reach WorldMap) triggers SM drift
checks that reset `_savedPartyRow/_savedPartyCol` after `SetViewedGridIndex`
sets them. By the time `_detectScreen()` runs, the SM grid index has been
overwritten.

**Fix approaches:**
- (a) Suppress drift checks during bridge-action execution (add a flag)
- (b) Move the `_detectScreen()` call to run BEFORE any drift checks
- (c) Don't use the escape storm — instead use `SetScreen(WorldMap)` directly
  on the SM before navigating, bypassing the escape keys entirely
- (d) Write the authoritative state to a C# field that the enrichment code
  reads INSTEAD of ViewedGridIndex (the approach in the stash, needs the
  viewedUnit name→slot lookup to also cover the loadout/abilities code)

Approach (a) is probably simplest — 2 lines of code.

### 2. equip_item helper (blocked)

Can't navigate to a specific item in the equipment picker by name because
we can't read which item is highlighted (no `ui=<item>` on Equippable* screens).
`open_picker <unit> <slot>` gets you TO the picker. The picker cursor tracking
(heap oscillation resolver) is the next unlock.

### 3. buy_ability / purchase_ability

Same picker cursor issue, BUT ability lists have deterministic order
(AbilityData.cs canonical picker orders). Could compute scroll distance
from the target ability's position in the list. Also needs JP validation.

### 4. Memory-write-based shopping

Gil at `0x140D39CD0`, inventory at `0x1411A17C0` (272 bytes, FFTPatcher item ID).
Could bypass Outfitter UI entirely: write item count + deduct gil. Need item
prices (not yet mapped from memory).

### 5. Orlandeau skillset label bug

`GetPrimarySkillsetByJobName` maps "Thunder God" → "Holy Sword" but his
combined skillset includes Holy Sword + Unyielding Blade + Fell Sword.
Should be "Swordplay" or similar. Data fix in CommandWatcher line ~3168.

## Things that DIDN'T work (don't repeat)

1. **Shell multi-call chaining for compound helpers** — `_FFT_DONE` guard
   kills the process on the second `fft` call. Every shell compound helper
   needed `_FFT_DONE=0` sprinkled between calls. C# bridge actions are the
   right pattern — one round-trip, no guard issues.

2. **Blind escape storm (6 Escapes) without detection** — Rapidly toggling
   PartyMenu open/close eats cursor keys and scrambles SM state. The fixed
   version checks detection between escapes to stop at WorldMap.

3. **Setting SM grid index BEFORE keys fire** — Drift checks during the
   escape storm reset it. Must set AFTER all keys + settling.

4. **Authoritative viewedUnit override** — Multiple code paths resolve
   viewedUnit independently (EnrichUnitScopedScreen, DetectScreen inline,
   BuildScreenFromSM). Overriding in one doesn't fix the others. The SM
   grid index is the single source of truth — sync it once and all paths work.

5. **Tab→A/D on equipment pickers** — The old ChangePage used Tab (VK_TAB),
   but live testing confirmed A (0x41) and D (0x44) are the actual keys.

## Things that DID work (repeat)

1. **C# bridge actions** — `open_eqa Cloud` from WorldMap navigated
   perfectly to Cloud's EqA in one round-trip. No shell chaining, no
   guard resets, no timing guesses. This is the pattern for all compound
   navigation going forward.

2. **Compact EqA view** — Two summary lines give Claude everything needed
   to decide which slot to open. Full grid in verbose only. Matches the
   "compact vs verbose vs nowhere" design philosophy.

3. **Helpers section on execute_action** — Claude sees available shell
   helpers right in the response. No memorization needed.

4. **Memory writes** — WriteProcessMemory works inside the mod. Gil change
   confirmed live. Opens up inventory manipulation, stat changes, and
   potentially cursor position writes for instant navigation.

5. **Wrap-reset cursor navigation** — Up×gridRows + Left×5 guarantees
   cursor at (0,0) regardless of previous position. Works reliably
   for PartyMenu grid navigation.

6. **Screenshots for verification** — Every navigation test was verified
   with a screenshot. Caught wrong-unit bugs (Wilham instead of Cloud,
   Mustadio instead of Cloud) that would have been invisible from output alone.

## Memory notes saved this session

None new — all findings documented in this handoff and TODO.

## Quick start next session

```bash
# Baseline check
./RunTests.sh                # 2046 passing

# Check stashed work
git stash list               # should show authoritative state WIP

# Live smoke — open_eqa from WorldMap
source ./fft.sh
boot                          # if game isn't running
open_eqa Cloud                # should show Cloud's EqA with correct gear
open_eqa Agrias               # BUG: viewedUnit lags (gear correct, name stale)

# Verify helpers
party_summary                 # formatted roster dump
check_unit Ramza              # quick stat sheet
enter_shop                    # WorldMap → Outfitter (at settlement)
```

## Active TODO top of queue (next-session priority)

1. **Fix authoritative viewedUnit lag** — stashed, approach (a): suppress
   drift checks during bridge action execution
2. **equip_item helper** — blocked on picker cursor tracking
3. **buy_ability helper** — deterministic ability order makes this feasible
4. **Memory-write shopping** — bypass Outfitter UI via direct memory writes
5. **JobSelection verbose view** — show job grid with state per cell
6. **auto_place_units live verification** — built but untested in-game
