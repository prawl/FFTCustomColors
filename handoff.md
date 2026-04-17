# Session Handoff — 2026-04-16 (Session 23)

Delete this file after reading.

## TL;DR

**Massive state-stability and helper-coverage session.** Eliminated the
SM-drift desyncs that have plagued every session since shop/battle
transitions started showing the wrong screen. Added validation guards
to every helper so misuse from the wrong state fails fast instead of
firing keys into random UI. Built five new helpers covering common
workflow gaps. State detection now reliably reports the right screen
across the full PartyMenu tree, shop exit, and battle entry/exit.

**Tests: 2046 passing** (no net change — all session work was either
covered by existing tests or live-only verification).

**Commits (12):**
1. `5dcd234` — Gate stale unitsTabFlag on menuDepth==0 (fixes shop-exit desync)
2. `e634a35` — Fix SM-drift racing animation lag on party-tree transitions
3. `588af0a` — Fix DelayBetweenMs not propagated from PathEntry to CommandRequest
4. `f356fb3` — Block EqA-promote on PartyMenuInventory/Chronicle/Options
5. `e9eef6f` — Exempt non-Units PartyMenu tabs + JobSelection from world-map drift snap
6. `aef8514` — Fill helpers for remaining states (CharacterDialog, Battle*, shops)
7. `82ccb65` — Wire SM sync into NavigationActions.SendKey + rewrite NavigateToCharacterStatus
8. `4fd29ae` — Bump NavigationActions KEY_DELAY 150→350ms + 1s PartyMenu open delay
9. `b08bb04` — Add state-validation guards to all helpers
10. `ca00160` — Add return_to_world_map, view_unit, unequip_all, travel_safe, scan_inventory
11. `d0e9bc6` — Helpers for all states, SM-drift fix, compact formatting improvements (early session)

## What landed, grouped by theme

### 1. State-stability fixes (the big one)

The class of "detection says X, game shows Y" desyncs that have plagued
every session is largely fixed. Five distinct bugs collapsed:

- **Stale `unitsTabFlag` after shop exit** (`5dcd234`) — `0x140D3A41E`
  stays latched at 1 on WorldMap after `battle_flee` or `Outfitter →
  Leave → Leave`, causing detection to return "PartyMenu" on WorldMap.
  Added `menuDepth` parameter to `ScreenDetectionLogic.Detect` and
  skip the unitsTabFlag rule when `menuDepth==0`.

- **SM-drift racing animation lag** (`e634a35`) — During the 50-200ms
  CharacterStatus → EqA animation, `menuDepth` reads 0 even though SM
  is correctly on EqA. The drift checks would then snap the correct SM
  back to WorldMap. Fix: gate drift checks on `smJustTransitioned`
  (SM.CurrentScreen != _smScreenBeforeKeys) so the SM gets to ride out
  its own animation window.

- **EqA-promote heuristic stomping non-EqA screens** (`f356fb3`) — The
  equipment-mirror heuristic that promotes any screen to EqA when the
  mirror matches a roster unit was firing on PartyMenuInventory/
  Chronicle/Options because the mirror stays populated. Extended the
  guard to include all non-EqA party-tree screens.

- **World-map drift snap stomping legit party-tree screens** (`e9eef6f`)
  — JobSelection/PartyMenuChronicle/PartyMenuOptions all read
  `menuDepth=0` legitimately. The drift detector was snapping the SM
  back to WorldMap whenever it saw 3 consecutive reads of (SM in tree
  + raw=TravelList + menuDepth=0). Exempted these screens by checking
  `smOnNonUnitsPartyTab` and `smOnJobSelection`.

- **DelayBetweenMs silently dropped on validpaths** (`588af0a`) — THE
  big one. `ExecuteValidPath` was converting PathEntry keys to command
  keys but never copying `path.DelayBetweenMs`. Every validpath with
  custom inter-key delay (ReturnToWorldMap 800ms, OpenChronicle 500ms,
  EnterLocation 500ms, etc.) was running at the bridge default,
  dropping keys mid-animation. One-line fix.

### 2. Helper completion + guards (`aef8514`, `b08bb04`)

- **Helpers for all detectable states** — Added `_show_helpers` cases
  for every screen that can produce a meaningful helper menu:
  CharacterDialog, BattleMoving/Attacking/Casting/Abilities,
  BattleAlliesTurn/EnemiesTurn/Waiting, shop interiors
  (Outfitter/Tavern/WarriorsGuild/PoachersDen/SaveGame).
- **State validation guards** — Every helper now validates
  `_current_screen` against an allowed-state regex BEFORE firing keys.
  Calling `change_job_to Knight` from Outfitter prints
  `[change_job_to] ERROR: cannot run from Outfitter. Allowed states:
  JobSelection` and returns 1. No keys fired. Verified live.

### 3. Compound nav rewrite (`82ccb65`, `4fd29ae`)

- **NavigationActions.SendKey now notifies the SM** — Added
  `ScreenStateMachine` property + `OnKeyPressed(vk)` call so compound
  nav helpers (open_eqa, open_character_status, etc.) keep the SM in
  sync. Without this, the SM stayed at PartyMenu while the game walked
  deep into EqA, and detection enrichment read the wrong ViewedGridIndex.
- **`NavigateToCharacterStatus` rewrite** — Moved unit lookup before
  nav (fail-fast on bad name). Dropped the wrap-to-(0,0) for the
  WorldMap path (cursor is already there). Sync RosterCount AND
  GridRows on the SM before nav so the wrap math matches actual party
  size (default 17 produced GridRows=4 for a 14-unit party).
- **KEY_DELAY 150ms → 350ms + 1s post-Escape** — Game's PartyMenu open
  animation eats keys at 150ms intervals. 350ms matches the manual-test
  pace that worked reliably. 1s post-Escape lets PartyMenu open
  before nav keys fire.

### 4. New strategic helpers (`ca00160`)

- `return_to_world_map` — Universal escape. Iterates Escape with
  detection until WorldMap. Up to 8 attempts.
- `view_unit <name>` — Read-only roster dump (name/job/lv/jp/brave/
  faith/zodiac/equip). No nav, no key presses. Works on any screen
  that exposes roster data (PartyMenu tree).
- `unequip_all <unit>` — Strip all 5 equipment slots from a unit.
  Skips already-empty slots. Reports removed/skipped counts.
- `travel_safe <id>` — `world_travel_to` with auto-flee on encounters.
  Polls 10s, auto-Flees up to 5 times.
- `scan_inventory` — Open PartyMenuInventory in verbose mode, dump
  full inventory grouped by category.
- `save_game` / `load_game` — Stubs for the in-game UI flow versions
  (UNIMPLEMENTED — flagged in helper output).

### 5. Compact formatting improvements (`d0e9bc6`)

- `loc=` renamed to `curLoc=` (clarifies "where you stand" vs "what's
  hovered")
- `gil=` only on shop-adjacent screens (no decision value on WorldMap/
  battle)
- WorldMap `ui=` suppressed (cursor always on current location, no
  decision changes)
- TravelList `ui=` blocked (hover byte goes stale at 254 inside the
  list)
- EncounterDialog: `ui=Fight` hardcoded (cursor defaults there)
- Cutscene: `eventId=N` surfaced
- JobSelection compact: `state=Unlocked` / `cursor=(r0,c2)` /
  `requires=...`
- JobSelection verbose: 3-row job grid with `cursor->` row marker and
  `[ClassName]` cell brackets

## Technique discoveries worth propagating

### menuDepth byte (0x14077CB67) is load-bearing

This byte is THE distinguishing signal between outer screens (WorldMap,
PartyMenu, CharacterStatus, JobSelection — all menuDepth=0) and inner
panels (EqA, equipment pickers, ability pickers — menuDepth=2). It's
already in detection inputs but several derived rules weren't gating on
it. Adding a `menuDepth==0` guard to: (a) the unitsTabFlag rule,
(b) the EqA-promote heuristic, (c) the SM-Drift trust-detection block,
all fixed real desyncs.

**Caveat**: menuDepth has 50-200ms animation lag after a panel-opening
key fires. The SM transitions instantly, the byte trails. Any check
that uses `menuDepth==0` to stomp the SM needs an animation-lag escape
hatch (use `smJustTransitioned` or `LastSetScreenFromKey`).

### NavigationActions vs ExecuteKeyCommand: parallel key-fire paths

The codebase has two paths that fire keys at the game:
- `ExecuteKeyCommand` (called by `fft` JSON commands with `keys[]`):
  notifies SM via `ScreenMachine?.OnKeyPressed(vk)` after each press.
- `NavigationActions.SendKey` (called by C# bridge actions like
  open_eqa, battle_flee): used to NOT notify SM. **Now it does**
  (commit 82ccb65). Without the sync, compound C# helpers leave the
  SM in a stale state that breaks subsequent detection enrichment.

### State-validation pattern: `_require_state`

```bash
_require_state <helper_name> "<allowed_states_pipe_separated>" || return 1
```

Fail-fast guard at the top of every helper. Reads current screen,
matches against regex, prints error and returns 1 if mismatch. Cheap
(~50ms for the screen read) and catches "Claude called the wrong
helper from the wrong state" before any keys fire. See the
`_PARTY_NAV_VALID_STATES` constant for the allowed-states list shared
by the open_* compound helpers.

### Per-PathEntry DelayBetweenMs is finally honored

`ExecuteValidPath` at `CommandWatcher.cs:2195` now copies
`path.DelayBetweenMs` to `command.DelayBetweenMs`. This was the silent
killer of every multi-key validpath. If you add a new validpath with
custom timing, set `DelayBetweenMs` on the PathEntry and it will work.

## What's NOT done — top priorities for next session

### 1. Live-verify the open_* compound helpers across more units

Tested `open_character_status Agrias`, `open_eqa Cloud/Mustadio`,
`open_job_selection Cloud` — all worked on FRESH state. But chained
calls (open_eqa Cloud → open_eqa Agrias) still have the viewedUnit-lag
bug from session 22 that the menuDepth fixes didn't address. Next
session: write a test sequence that chains 3 different units through
each helper and verify state matches the requested unit each time.

### 2. KEY_DELAY tuning — try per-key-type instead of blanket 350ms

Bumped to 350ms blanket because manual testing worked at that pace.
But cursor nav keys (Up/Down/Left/Right) probably can fire faster
(150-200ms) — only Enter and Escape need the longer delay because they
trigger screen transitions. Could measure with the `[i=N, +Nms]` log
output and tune per-key.

### 3. Per-key detection verification (replace blind sleeps)

The real fix for compound nav reliability: after each transition key,
poll detection until the expected state appears, instead of a fixed
sleep. Would handle game-side animation variance instead of guessing.
Bigger refactor — defer until current approach stabilizes.

### 4. open_picker / unequip_all over longer chains

`unequip_all Cloud` works through all 5 slots but takes ~25s total.
Background-task timeout cut it off at the last slot in testing.
Caller-side timeout needs to be ≥30s. Document this in the helper
description.

### 5. Carry forward from previous sessions (still open)

- **viewedUnit lag on chain calls** (session 22) — `open_eqa Agrias`
  from inside Cloud's EqA still shows Agrias gear with Cloud name.
  Stash from session 22 may or may not still apply now that
  `OpenCharacterStatus` got rewritten.
- **Orlandeau primary skillset label** (session 22) — "Thunder God"
  → "Holy Sword" mapping is wrong; should be "Swordplay" or composite.
- **save action "Not implemented yet"** — the underlying `save` C#
  action returns a not-implemented error. `save_game`/`load_game`
  shell stubs were added with a clear unimplemented message but the
  actual C# implementation is the blocker.

## Things that DIDN'T work (don't repeat)

1. **Aggressive SM-Snap on (menuDepth=0 + raw=WorldMap + SM in
   party-tree)** — fired during legitimate PartyMenuChronicle/Options/
   JobSelection states because they all have `menuDepth=0`.
   Removed (`e9eef6f`). Use the existing `_worldMapDriftStreak`
   (3-read debounce) for stale-SM recovery instead.

2. **Trusting `KeysSinceLastSetScreen == 0` as "in animation lag"** —
   `OnKeyPressed` increments the counter to 1 (not 0), so the gate
   never triggered. Use `smJustTransitioned` (SM.CurrentScreen !=
   _smScreenBeforeKeys) instead.

3. **150ms KEY_DELAY in NavigationActions** — too fast. Down keys got
   eaten during the PartyMenu open animation. 350ms is the floor.

4. **Wrap-to-(0,0) before grid nav from a fresh PartyMenu open** — not
   needed. Opening PartyMenu from WorldMap always lands at (0,0). The
   wrap was only needed for the "already on PartyMenu" branch where
   cursor could be anywhere.

5. **Default `ScreenMachine.RosterCount = 17`** — produces wrong
   `GridRows` for a 14-unit party. Compound nav helpers must sync
   `ScreenMachine.RosterCount = allSlots.Count` AND
   `ScreenMachine.GridRows = (rosterCount + 4) / 5` before firing nav
   keys, or the SM's wrap math diverges from the game's.

## Things that DID work (repeat this)

1. **menuDepth gate everywhere** — Adding `menuDepth==0` checks to
   stomp-paths fixed several distinct desyncs. When in doubt about
   "should this rule fire on outer/inner screen", check menuDepth.

2. **State guards on every helper** — `_require_state` at the top
   eliminates an entire class of "Claude called X from Y, fired
   surprising keys" bugs. Cheap and catches the problem at the right
   layer (before any I/O).

3. **Step-by-step manual reproduction** — When a compound helper
   misbehaved (open_character_status landing on wrong unit), running
   the exact key sequence one key at a time with screenshots between
   pinpointed the timing issue (KEY_DELAY too short) within minutes.
   Don't try to debug compound helpers as a unit; decompose them.

4. **Timing logs on key fires** — Adding `[i=N, +Nms]` to the key-press
   debug log made it instantly obvious when DelayBetweenMs wasn't
   being honored (200ms instead of configured 800ms). Cheap, high-value.

5. **Reading the existing memory notes BEFORE memory scanning** —
   Saved 30+ minutes when the user mentioned "we've scanned items
   like a dozen times". The notes had the inventory store address
   already cracked.

## Memory notes saved this session

None new — all session findings are in the handoff and TODO. The
existing notes already covered the inventory store, item name pool,
and shop stock array.

## Quick-start commands for next session

```bash
# Baseline check
./RunTests.sh                 # 2046 passing

# Live smoke — full state-stability tour
source ./fft.sh
boot                          # if game isn't running
screen                        # baseline WorldMap

# Test compound nav (the ones that broke this session)
open_character_status Agrias  # → CharacterStatus, viewedUnit=Agrias
return_to_world_map           # → WorldMap
open_eqa Cloud                # → EqA, viewedUnit=Cloud
return_to_world_map

# Test new helpers
view_unit Mustadio            # read-only data dump
party_summary                 # all units
travel_safe 26                # auto-flee enabled travel

# Test guards (should all reject with clear errors)
enter_shop                    # ERROR: at battleground, not settlement
change_job_to Knight          # ERROR: not on JobSelection
auto_place_units              # ERROR: not on BattleFormation
```

## Active TODO top of queue (next-session priority)

1. **Verify open_* helpers handle CHAIN calls** — fresh state works.
   Cloud → Agrias → Mustadio sequence is the unsolved class.
2. **Implement `save` C# action** — currently returns "Not implemented
   yet". Once it works, the `save_game` shell helper can wire up.
3. **Per-key-type KEY_DELAY tuning** — reduce nav key delay (no
   transition) from 350ms; keep transition keys (Enter/Escape) at 350+.
4. **CombatSets state mapping** — user noted this exists in-game but
   isn't documented. ValidPaths and helpers will need this if Claude
   wants to use saved loadouts.
5. **`return_to_world_map` from battle states** — only tested from
   PartyMenu tree. Verify from BattlePaused, GameOver, Victory.

## Insights / lessons captured

- The state machine's job isn't to predict the game — it's to
  disambiguate states the game's memory can't distinguish (CharacterStatus
  vs EqA vs JobSelection all look identical in raw bytes). Detection is
  authoritative for outer screens; SM is authoritative for inner ones.
  Bugs happen when one layer overreaches.

- Animation lag is the hidden enemy. Every "this should work but
  doesn't" almost always traces back to a key firing during a 50-500ms
  animation window. When in doubt, check the timing log.

- One change that touches multiple subsystems (like the SM-sync wire)
  often fixes more bugs than expected, but also breaks unrelated
  things. Test the full nav matrix after these kinds of changes.

- The `_show_helpers` block in fft.sh is the single source of truth
  for "what can Claude do here". Keep it updated as helpers are
  added/restricted; it's how Claude discovers them.
