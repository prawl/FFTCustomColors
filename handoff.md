# Session Handoff — 2026-04-17 (Sessions 25 + 26)

Delete this file after reading.

## TL;DR

Two sessions back-to-back, **5 commits**, **+49 tests (2059 → 2108)**, all on branch `auto-play-with-claude`. Session 25 shipped SaveSlotPicker state tracking + fixed a chain-guard pipe-subshell bypass. Session 26 shipped 12 items across 4 commits: session observability log, per-key-type KEY_DELAY, TitleScreen tightening, Tavern + TavernRumors + TavernErrands state, partial HP/MP in roster, detection rule reorder, working `save` C# action (end-to-end live-verified), `return_to_world_map` battle-state guard, plus eventId/hover TODO cleanups.

Zero regressions. All shipped features live-verified in-game except HP/MP (partial — only 4 of 14 units populate) and return_to_world_map from BattleVictory/Desertion (not blocked, just unverified). Two fresh memory notes added documenting detection-collision patterns that bit us twice and will bite again.

**Commits (oldest → newest):**
1. `c847d42` — Session 25: SaveSlotPicker state + chain-guard pipe fix + fft_resync forbidden-state guard
2. `473ac53` — Session 26 pt.1: session cmd log + KEY_DELAY split + TitleScreen tightening
3. `d1e7160` — Session 26 pt.2: Tavern + TavernRumors + TavernErrands state tracking
4. `7ac9f22` — Session 26 pt.3: HP/MP in PartyMenu roster (partial)
5. `134da68` — Session 26 pt.4: save action + detection cleanup + return_to_world_map guard

## What landed, grouped by theme

### State machine coverage (`c847d42`, `d1e7160`)

- **`SaveSlotPicker`** — new `GameScreen` enum value, `HandlePartyMenu` Enter-on-OptionsIndex-0 transition, Escape back to PartyMenuOptions. ScrollUp/Down/Select/Cancel ValidPaths via `SaveGame_Menu` alias. Live-verified: PartyMenuOptions → Select → SaveSlotPicker → Cancel → PartyMenuOptions round-trip with correct screen name + ValidPaths at every step.
- **`Tavern` / `TavernRumors` / `TavernErrands`** — new `GameScreen` values, `SM.TavernCursor` (0=Rumors, 1=Errands) tracks the 2-option cursor with wrap on Up/Down. `HandleTavern` handles Enter (transitions to sub-state based on cursor) + Escape (back to LocationMenu; the farewell dialog is handled by the `Leave` validPath's Enter). `HandleTavernSubScreen` handles Escape back to Tavern. NavigationPaths for both sub-states — `Select` intentionally omitted from `TavernErrands` because it opens a candidate-unit picker that isn't modeled yet and would trap the SM.

### Detection-ambiguity pattern: SM-as-authority (`c847d42`, `d1e7160`)

This is the session's big recurring theme. Two pairs of screens turned out to be **byte-identical** in detection inputs:

- **SaveSlotPicker vs TravelList** — all 28 fields match (memory hunt documented in `memory/project_saveslotpicker_vs_travellist.md`).
- **TavernRumors vs TavernErrands vs LocationMenu** — all 24 fields match (memory note `project_tavern_substates.md`).

Solution: `ScreenDetectionLogic.ResolveAmbiguousScreen(smScreen, detectedName)` — when SM says we're on a specific sub-state and detection returns the collision partner, trust the SM. Wired into 3 CommandWatcher paths (screen query, key-sending fallback, `execute_action` path lookup). Real screen transitions (detection=BattleMyTurn, WorldMap, etc.) always win over stale SM state — only the known-collision pair gets overridden.

**The trick that makes this work**: CommandWatcher's screen-query path also syncs SM to detection for non-party-tree transitions (WorldMap/LocationMenu/Tavern/TravelList). Without this, a fresh `screen` query that saw "Tavern" wouldn't update the SM, so the next Enter key wouldn't fire `HandleTavern`. This pattern generalizes to any future screen with entry points the SM doesn't model.

### Chain-guard pipe-subshell fix (`c847d42`)

`_FFT_DONE` shell var was bypassed when helpers were piped (`screen | tail`) because the piped function runs in the pipe's subshell and the `=1` assignment never propagates back. Fix: disk-backed flag at `claude_bridge/fft_done.flag`, cleared at `source` time, set by `_fft_guard`, read alongside the shell var. Because it survives subshells, piped-first-call + second-call now correctly triggers `[NO] Only call one command at a time` + `kill -9 $$`. 34 composite-helper `_FFT_DONE=0` reset sites rewritten to call new `_fft_reset_guard` (clears both layers atomically).

### State-guard hardening (`c847d42`, `134da68`)

- **`fft_resync`** — refuses from `Battle*|EncounterDialog|Cutscene|BattleSequence|BattleFormation|GameOver`. Block-list (not allow-list) so new non-battle screens are auto-safe. Memory note `feedback_fft_resync_forbidden_states.md` captures the pattern.
- **`return_to_world_map`** — refuses from 13 unsafe states (Battle* + Encounter + Cutscene + GameOver + Formation + Sequence + EnemiesTurn + AlliesTurn) with clear pointers to `battle_flee` / `execute_action ReturnToWorldMap`. Closes a real footgun: Escape on BattlePaused would resume the battle, not exit it.
- **`save` action** — refuses from Battle*/Encounter/Cutscene/GameOver before the escape storm.

### Observability + speed (`473ac53`)

- **`SessionCommandLog`** — append-only JSONL at `claude_bridge/session_<stamp>.jsonl`, one row per command: id/timestamp/action/source→target/status/error/latencyMs. Never throws (observability must not take down command processing). Wired into `CommandWatcher` main + error paths. Hugely valuable for post-hoc "where did Claude get stuck?" review.
- **`KeyDelayClassifier`** — `SendKey` now sleeps 200ms after cursor nav keys (Up/Down/Left/Right) and 350ms after transition keys (Enter/Escape/Tab/letters used for tab cycling). Shaves 1-2s off nav-heavy flows like `open_eqa`'s party grid navigation.

### Detection rule cleanup (`473ac53`, `134da68`)

- **TitleScreen tightening** — removed the loose `rawLocation==255 → TitleScreen` fallback. Residuals now return "Unknown" so callers see ambiguity instead of being silently mislabeled as TitleScreen after GameOver/post-battle stale.
- **Rule reorder** — `party==1 / encounterFlag / eventId` authoritative rules now run BEFORE hover/location heuristics. Prevents "opened PartyMenu while hovering a map location" from misdetecting as WorldMap, and EncounterDialog from losing to hover-based rules.
- **eventId filter** — verified already correct (out-of-battle `< 400 && != 0xFFFF`; in-battle `< 200` intentional because the eventId address aliases as active-unit nameId during combat). Added 2 coverage tests for Orbonne eventId=302.

### `save` C# action end-to-end (`134da68`)

The `save` helper was a "Not implemented yet" stub. Now drives from any non-battle state to SaveSlotPicker:
1. Escape to WorldMap (2-consecutive-read confirm for the Escape-on-WorldMap toggle gotcha — memory note `feedback_worldmap_escape.md` was load-bearing here).
2. Escape-on-WorldMap opens PartyMenu Units tab.
3. Q-cycle to Options tab (count derived from `ScreenMachine.Tab`).
4. Up×5 resets `OptionsIndex` to 0 (Save).
5. Enter → SaveSlotPicker.

Live-verified from WorldMap, PartyMenuOptions, and 3-level-nested EqA. State-guarded so it can't fire from battle/cutscene/etc.

### HP/MP in PartyMenu roster — partial (`7ac9f22`)

Wired `HoveredUnitArray.ReadStatsIfMatches` into the roster assembly. First 4 units populate correctly (Ramza Lv99 HP 719/719 MP 138/138 verified against in-game unit card). Other 10 units return null because the "hovered-unit array" is actually per-hover, not roster-wide — class-level comment was optimistic. Full roster needs formula-based recompute (FFTPatcher job-base + equipment bonuses via `ItemData.cs`). Memory note `project_hovered_unit_array_partial.md` explains.

## Technique discoveries worth propagating

### Detection-collision pairs need SM-as-authority, not memory hunts

Session hunt: found SaveSlotPicker vs TravelList are byte-identical (all 28 fields), then TavernRumors/Errands vs LocationMenu same result (all 24 fields). Both times the real discriminator lives in UE4 widget heap which shuffles per launch. **Don't repeat the snapshot-diff dance for any new collision pair that fits this pattern.** Just:
1. Verify the collision with `detection_dump` on both screens.
2. Record entry/exit in the SM (`HandleX` handlers).
3. Add a row to `ScreenDetectionLogic.ResolveAmbiguousScreen` mapping `(SM=SpecificX, detected=CollisionPartner) → "SpecificX"`.
4. Ensure CommandWatcher syncs SM to detection for transitions into the parent state (so the SM can track cursor + Enter from there).

### Piped helpers bypass shell-var guards — always mirror to disk

`_FFT_DONE` in a shell variable looks fine but silently fails across pipe subshells. Anything that needs to survive subshells (guards, counters, flags) has to live in a file. The disk flag at `claude_bridge/fft_done.flag` + source-time clear pattern generalizes to any cross-subshell state.

### Block-list state guards scale better than allow-lists

`fft_resync` and `return_to_world_map` both moved from allow-list ("only from X") to block-list ("refuse from Y"). New non-battle screens are auto-safe. Pattern:

```bash
case "$cur" in
  Battle*|EncounterDialog|Cutscene|BattleSequence|BattleFormation|GameOver)
    echo "[$helper] ERROR: cannot run from $cur. <recovery hint>"
    return 1
    ;;
esac
```

### HoveredUnitArray is per-hover, not roster-wide

2026-04-14 class comment said "mirrors every active roster slot" — this is wrong. Only ~4 slots around the currently-hovered one populate. The brave/faith doubling sanity check correctly rejects the rest. Don't assume you can read roster-wide HP/MP from this array.

## What's NOT done — top priorities for next session

### 1. Plumb `resolve_job_cursor` into SM drift correction (carried from session 24)

Live-observed JobSelection desync after Right×3+Down: screenshot cursor on Archer (r0,c3), shell reports (r0,c0). Heap resolver already finds the real cursor byte. Fix: `CommandWatcher.cs` ~5400 — when `memRow != ScreenMachine.CursorRow || memCol != ScreenMachine.CursorCol`, snap the SM and log the correction. Would also fix most JobSelection chain-nav bugs.

### 2. Full roster HP/MP via formula (session 26 pt.3 leftover)

Job-base stats + equipment bonuses + level scaling. Need: port `JobBaseStats.cs` (data lookup, FFTPatcher-sourced), wire `ItemData.cs` equipment HP/MP modifiers, recompute per unit in the roster assembly path. Pure-math task; unit-testable without live game. Concrete path forward documented in TODO §10.6.

### 3. Second SaveSlotPicker entry point from BattlePaused

Session 25 shipped PartyMenuOptions → Save. BattlePaused → Save is a parallel entry the user mentioned. Needs a real battle to verify. Check if BattlePaused menu has Save listed, find cursor index, wire the SM transition.

### 4. Chain-nav viewedUnit lag — build dry-run harness before retry

Session 24 attempts crashed the game. Don't retry blind-kick. Approach: unit test simulating the escape-storm → detection-stale sequence, `dry-run` mode on `NavigateToCharacterStatus` that logs the key plan without firing, run against the crashy input to validate BEFORE firing for real.

### 5. BattleVictory / BattleDesertion paths for return_to_world_map

`return_to_world_map` is no longer blocked on those states (Escape/Enter legitimately advances toward WorldMap there). Still needs a live-verify.

## Things that DIDN'T work (don't-repeat list)

1. **Memory snapshot-diff hunt for SaveSlotPicker vs TravelList discriminator.** Tight intersect over 128K bytes produced 4 candidates, all failed live-verification. Main-module has nothing for these UI collisions. Don't burn another hour on it. Use SM-as-authority pattern instead.

2. **Cursor-oscillation diff for TravelList cursor row.** Found 5 "counter" addresses that go +1 on Down but don't decrement on Up — they're keypress counters, not position indices. Not useful as discriminators.

3. **Assuming HoveredUnitArray mirrors all roster slots.** The class-level comment was wrong. Only ~4 slots around the hover populate.

4. **Bare `_FFT_DONE=0` reset inside piped helpers.** Fix requires `_fft_reset_guard` call (clears disk flag too). 34 call sites rewritten.

5. **First `save` implementation with naïve escape-until-WorldMap.** Escape-on-WorldMap toggles into PartyMenu (per `feedback_worldmap_escape.md`), so a single Escape after "I see WorldMap" is actually the menu-open action — can't call it a bug, it's the game. Had to add 2-consecutive-read confirm.

## Things that DID work (repeat-this)

1. **SM-as-authority for detection-collision pairs.** Clean, fast, testable. See SaveSlotPicker and TavernRumors/Errands implementations for the pattern.

2. **`ResolveAmbiguousScreen` + `screen`-query SM sync combo.** This is the load-bearing pair. SM tracks transitions; detection stays authoritative for real screen changes; the override resolves the known-collision cases; the screen-query sync ensures the SM keeps up with transitions it didn't model.

3. **State-guard helpers with clear recovery hints.** Every refusal message names the right alternative (`battle_flee` / `execute_action ReturnToWorldMap` / etc.). Saves a round-trip of "okay, how do I actually get out of here".

4. **Live-verify after every wired transition.** Screenshot + `screen` + `execute_action` at each step. Caught the "first `save` implementation escape-storming into the travel list" bug within 30 seconds.

5. **SessionCommandLog + mod log grep.** The `[SM-Override]` / `[SM-Sync/query]` / `[SM-Drift]` log tags are invaluable for debugging transition flows. Grep them when the SM state doesn't match the game.

## Memory notes saved these sessions

New entries:

- `project_saveslotpicker_vs_travellist.md` — no main-module byte distinguishes these two; use SM-as-authority.
- `project_tavern_substates.md` — same pattern for TavernRumors/TavernErrands vs LocationMenu (24 inputs identical).
- `project_hovered_unit_array_partial.md` — HoveredUnitArray is per-hover, not roster-wide. Partial HP/MP only.
- `feedback_fft_resync_forbidden_states.md` — fft_resync must refuse from battle-adjacent states; escape storm mis-handles them.

All indexed in `MEMORY.md`.

## Quick-start commands for next session

```bash
# Baseline
./RunTests.sh                       # 2108 passing
source ./fft.sh
running                             # check game alive

# Session 25/26 wins to sanity-check
save                                # from any non-battle state → SaveSlotPicker
# escape back out, try from deep nested:
open_eqa                            # → EqA
save                                # should still land on SaveSlotPicker (verified)

# Tavern sub-states
execute_action EnterLocation        # → LocationMenu
# navigate to Tavern, EnterShop → Tavern
# down, enter → TavernErrands (detection should NOT report LocationMenu)
# escape → Tavern

# Piped chain-guard test (in a throwaway bash invocation):
source ./fft.sh && screen | tail -1; screen    # second call should be blocked with [NO]

# Check SessionCommandLog was created
ls "$B" | grep session_              # session_<timestamp>.jsonl present

# Check TODO queue
grep -cE "^- \[ \]" FFTHandsFree/TODO.md    # ~170 open
grep -nE "Session 24 — follow-ups" FFTHandsFree/TODO.md
```

## Top-of-queue TODO items the next session should tackle first

These live in `TODO.md §0`:

1. **Plumb `resolve_job_cursor` output → SM correction** (carryover from session 24).
2. **EqA resolver re-fire on detect-drift events** (carryover; same pattern as JobSelection).
3. **Second SaveSlotPicker entry from BattlePaused** (session 25 follow-up).
4. **Chain-nav viewedUnit lag — build dry-run harness before retry** (session 24 carryover; two prior attempts crashed the game).

## Insights / lessons captured

- **"Same-fingerprint screen pair" is a recurring pattern.** SaveSlotPicker vs TravelList (session 25), TavernRumors vs TavernErrands vs LocationMenu (session 26). The answer is always the SM-as-authority pattern. Don't repeat the snapshot-diff dance for new collisions — go straight to SM + override.

- **Don't trust class-level comments on memory structures.** `HoveredUnitArray` class doc said "mirrors every active roster slot" — it doesn't. Session 26 pt.3 wasted half an hour assuming the comment was accurate. Always verify population live before wiring downstream.

- **Block-list state guards > allow-list state guards.** Two helpers went from allow-list (fragile — new states break them silently) to block-list (robust — new states are auto-allowed). Rule of thumb: if the helper is "safe from everywhere EXCEPT X", use a block-list.

- **Pipes are a real concurrency boundary in bash.** Any shell var guard that matters needs a disk-file mirror. The fix generalizes beyond `_FFT_DONE`.

- **Live-verify every helper, every path.** The first `save` implementation looked correct in code review and would have shipped without the screenshot check. The screenshot caught the escape-storm-into-travel-list bug in seconds. For any helper that drives UI transitions: screenshot before, screenshot after, confirm both screen states.

- **When the user says "do it now" to a rejected file write — ask where instead of retrying.** Global `~/.claude/commands/` vs project-local `.claude/commands/` is a meaningful distinction the user's rejection was signaling. Re-trying the same file in the same location = stubborn instead of curious.
