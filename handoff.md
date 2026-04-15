# Session Handoff — 2026-04-15 (Session 15)

Delete this file after reading.

## TL;DR

Sessions 14 and 15 had a clear arc: 14 set up the JobSelection cursor-resolver infrastructure; 15 used it to ship the full 17 ACs (almost). The other major win was codifying a **payload design principle** ("compact vs verbose vs nowhere") that killed at least one feature before it was started, and led to a sweep that trimmed dead/low-value items from the PartyMenu + Chronicle TODO sections.

13 commits this session, all on `auto-play-with-claude` (not pushed yet). Tests: **1914 passing** (was 1849, +65). Two real bugs surfaced and partially fixed; one bug was diagnosed wrong, fix shipped, then reverted same session — left a useful lesson.

## Commits this session (13, on `auto-play-with-claude`)

```
3d8638b revert b453fb1 + gate JobSelection auto-resolver on MenuDepth==2
b453fb1 state machine: PartyMenu cursor snaps to (0,0) on Escape from CharacterStatus  ← REVERTED
292f9e5 TODO: trim PartyMenu + Chronicle stories per design principle
3f3855f TODO: design principle for compact vs verbose vs nowhere
5fdefa6 JobSelection: re-resolve cursor address on Up/Down (row-cross desync fix)
a787103 screen: viewedUnit= on unit-scoped panels
129f279 JobSelection: three-state cell classification (Locked / Visible / Unlocked)
76c7d11 TODO: JobSelection cell state is Locked/Visible/Unlocked, not filter
d442e0f TODO: JobSelection grid must filter to unlocked classes per unit  ← superseded
777c189 JobSelection: story-character + gender-aware grid resolution
c25f0f4 fft.sh: change_job_to <ClassName> helper
4a0b53e bridge: resolve_job_cursor for JobSelection ui= + cursorRow/Col tracking
2ddd879 state machine: JobSelection grid layout is 6/7/6 per row (Ramza verified)
```

## What landed, in order

### 1. JobSelection grid layout discovery + state machine fix (`2ddd879`)

Live verification proved Ramza's JobSelection grid is **6/7/6 cells per row**, not the 8/6/6 the state machine assumed. Row 1 has 7 cells (Geomancer at the seventh position). The cursor byte is a **flat linear index 0..18**, NOT `row*6+col` as the earlier memory note said.

- New `JobGridLayout.cs` — per-character class-name tables + `IndexToRowCol` / `RowColToIndex` / `GetRowWidth` / `EnumerateCells`.
- `ScreenStateMachine.HandleJobScreen` now uses per-row widths via `JobGridLayout.GetRowWidth`. New `ClampJobColumnToRow` handles vertical transitions between rows of different widths.
- 16 unit tests for the layout + per-row widths.

### 2. `resolve_job_cursor` heap rescan-on-entry resolver (`4a0b53e`)

Mirrored `ResolvePickerCursor` for the JobSelection grid. Right/Left oscillation (matches the horizontal-first grid), 2-step delta verification, address-priority ranking. Auto-fires on first `screen` call per JobSelection visit; gated by `_jobCursorResolveAttempted` to prevent re-fire on every screen call.

`screen.CursorRow`, `screen.CursorCol`, `screen.UI` (hovered class name) all populate from the resolved heap byte. Visible ~1.5s cursor flash on first entry; subsequent reads are single-byte.

### 3. `change_job_to <ClassName>` helper (`c25f0f4`)

The "final deliverable" from session 14's AC list. Routes through JobSelection grid → JobActionMenu → JobChangeConfirmation → Confirm → dialog dismiss. Uses the resolved cursor position (from #2) so the walk starts from the unit's actual current job, not (0,0). Coordinates hardcoded for the Ramza 6/7/6 layout. Live-verified Ramza round-trip Gallant Knight ↔ Chemist ↔ Monk.

Landing screen after a successful change is usually `EquipmentAndAbilities` (the game auto-opens it post-change so you can re-equip), sometimes `CharacterStatus` if no gear conflicts. Helper accepts both.

### 4. Story-character + gender-aware grid resolution (`777c189`)

User correction: every story character has their own unique class at (0,0), not Squire. Agrias → Holy Knight, Mustadio → Machinist, Orlandeau → Thunder God, etc. Generics get Squire. Refactored `JobGridLayout`:

- New `StoryCharacterUniqueClass` map (13 entries: Ramza, Agrias, Mustadio, Rapha, Marach, Beowulf, Construct 8, Orlandeau, Meliadoul, Reis, Cloud, Luso, Balthier).
- New `JobGridLayout.ForUnit(unitName, isFemale)` builds a per-unit layout by patching (0,0) and (2,4) on a shared template.
- Gender derived from generic job ID parity (odd=male, even=female).
- `CommandWatcher` now passes the viewed unit's name + jobId into `ForUnit`.

Live-verified Agrias's JobSelection shows `ui=Holy Knight`.

### 5. `screen.viewedUnit` on unit-scoped panels (`a787103`)

Added a JSON field + compact one-liner marker identifying whose nested panel is currently shown. Populated on CharacterStatus, EquipmentAndAbilities, JobSelection, all four ability pickers, all five equippable pickers, JobActionMenu, JobChangeConfirmation, CombatSets, CharacterDialog, DismissUnit. Omitted on PartyMenu (cursor moves are ABOUT-to-view, different concept) and on screens unrelated to a single unit.

```
[JobSelection] viewedUnit=Agrias ui=Holy Knight loc=6(MagickCityofGariland) status=completed
```

### 6. JobSelection row-cross desync fix (`5fdefa6`)

The JobSelection widget heap reallocates per row cross — confirmed live: a resolved address `0x11EC34D3C` shuffled to `0x1370CF4A0` after a single Down. Fix: `InvalidateJobCursorOnRowCross` clears `_resolvedJobCursorAddr` + `_jobCursorResolveAttempted` whenever Up/Down fires while on JobScreen, forcing re-resolve on the next screen call. Horizontal movement is unaffected.

### 7. Three-state cell classification (`129f279`)

User insight: every grid cell is physically rendered, but state varies per (unit, class):
- **Locked** — no party member has this class. Shadow silhouette, no info.
- **Visible** — someone has it but viewed unit doesn't. Normal cell, change refused.
- **Unlocked** — viewed unit can change to it.

Proxy: a class is "unlocked for a unit" if that unit has any action-ability bit set in the corresponding job's learned bitfield (+0x32+jobIdx*3 bytes 0-1). Party-wide unlock = union across all roster slots. Squire/Chemist/own story class are always Unlocked. Mime is hardcoded Locked under the proxy (its bitfield is empty in this remaster).

`screen.jobCellState`, `ui=` reflects state, `change_job_to` refuses cleanly on Locked/Visible. Verified live on Agrias: Holy Knight=Unlocked, Chemist=Unlocked, Dragoon=Visible (Ramza has Jump bit set; Agrias doesn't).

### 8. Design principle for compact vs verbose vs nowhere (`3f3855f`)

After play-testing showed Claude greps past dense compact responses, codified when a payload field earns its spot:

1. Would a human consult this on this screen?
2. Does Claude need it to act HERE, or could they navigate to it?
3. Would not having it cause a worse decision OR wasted round-trips?

Plus a **noise penalty** — every compact field hurts the findability of others. Prefer decision aids (`jobCellState: "Visible"`) over data dumps (19 cells of raw JP).

Heuristic: every-turn signals → compact. Planning data → verbose JSON. Mirrors-what-hovering-shows → nowhere.

Application killed AC5 (per-class Lv/JP grid) before any code was written.

### 9. PartyMenu/Chronicle TODO trim (`292f9e5`)

Swept §10.6 / §10.7 against the new principle. Dropped: element resistance grid, equipment stat aggregates, FocusEquipmentColumn / FocusAbilitiesColumn validPaths, redundant Equippable_* arrow keys, ALL §10.7 Chronicle inner states (lore content, no decision flow needs it). Marked Full Roster Grid + JobSelection ValidPaths as DONE (already shipped, stale wording). Scoped Full Stat Panel as verbose-only.

### 10. JobSelection auto-resolver MenuDepth gate + cursor-snap revert (`3d8638b`)

Two things bundled:

**Resolver gate (kept):** the JobSelection auto-resolver fires 6 raw Right/Left keys to oscillate-find the cursor byte. If the state machine flips to JobScreen synchronously on Enter but the game is still mid-transition (~50-200ms open animation), those raw keys hit the OUTER PartyMenu cursor instead. Gate the trigger on `screen.MenuDepth == 2` (memory-confirmed inner panel render). Live-verified: clean batched nav now lands cleanly.

**b453fb1 revert:** I had committed a fix snapping PartyMenu cursor to (0,0) on Escape from CharacterStatus, based on a single screenshot taken on a fresh restart. Live testing with a non-default cursor position proved the game **preserves** the entry cursor (viewing Orlandeau → Escape → cursor still on Orlandeau). Reverted; restored `_savedPartyRow/Col` restoration logic. Lesson learned: test cursor-preservation behavior with the cursor at a NON-(0,0) position before claiming the game's behavior.

## Decision-design principle (worth knowing before adding payload)

Codified in TODO.md. Quick rule before adding a new field anywhere on `screen`:

> Write one sentence answering "what decision changes if Claude has this?" If you can't, drop it. If the answer is "Claude could plan a turn ahead," verbose. If it's "Claude needs this to pick the next action," compact. If it's "it's nice to have," nowhere.

This rule killed AC5 (per-class Lv/JP) and several PartyMenu data-dump items.

## What's next (priority order)

### 1. PartyMenu cursor state-machine drift (open, partially mitigated)

The remaining bug from this session. Repro: after a sequence of nested-screen visits, state machine's `CursorRow/Col` on PartyMenu can disagree with the in-game cursor (state says Ramza (0,0), game shows Orlandeau (2,0)). The MenuDepth gate (#10 above) eliminated one race. The other remaining race is harder — likely batched commands eating keys during transitions OR resolver bursts that drift the OUTER cursor before they return to focus.

**Best fix candidate:** read the actual PartyMenu cursor byte from memory. Same heap-oscillation technique used for JobSelection. Steps:
1. Write `ResolvePartyMenuCursor` mirroring `ResolveJobCursor` but with Right/Left + cursor wraps to handle the 5-col grid.
2. Auto-trigger on entry to PartyMenu (gate on the appropriate menu-depth signal).
3. Invalidate cache on every cursor key (Up/Down/Left/Right) — the widget probably reallocates per move, just like JobSelection.
4. Replace state-machine CursorRow/Col reads with the resolved memory value when available.

This is the biggest leverage left — it would make `viewedUnit`, `change_job_to`, all picker helpers, and the entire nested PartyMenu tree trustworthy under any nav pattern.

### 2. JobSelection unlock-requirements text scrape (Visible cells)

When a cell is Visible, the game's info panel shows the unlock requirements (e.g. "Squire Lv. 2, Chemist Lv. 3"). We don't surface that today. Two paths:
- **Easy:** hard-code a `JobPrereqs` map (~20 entries) in `CharacterData.cs` and synthesize the requirement text. Risk: WotL prereqs may differ from canonical FFT.
- **Hard:** memory-scan for the widget text (UE4 heap, painful).

(a) is the path of least resistance. Surface as `screen.jobUnlockRequirements` when `jobCellState == "Visible"`.

### 3. Verify Locked-state behavior live (deferred, blocked on save state)

Current save has at least one master unit, so no cell renders as a shadow silhouette for any other unit. Need a fresh-game save (or temporarily dismiss all units except one under-leveled generic) to verify the Locked branch end-to-end. Logic is shipped; verification deferred.

### 4. Verify generic male/female grids live

`JobGridLayout.ForUnit` assumes Squire at (0,0), Bard (male) / Dancer (female) at (2,4) for generics. Inferred from Ramza's grid + standard FFT layout. Verify on a generic when one is recruited.

### 5. PartyMenu tab multi-press desync (§0 TODO, ongoing)

Pre-existing. `OpenChronicle` from Units etc. fires Q/E twice and races the tab-switch animation. Documented since session 13. Workaround: use single-press `NextTab` / `PrevTab`. Real fix needs either a memory-confirmed tab signal or per-key wait-for-game.

### 6. `Next: N` JP-to-next-ability on EquipmentAndAbilities header (§0 TODO, ongoing)

Pre-existing. The game's header shows `Lv. N | Next: N | JP N` for the unit's current job. We surface Lv and JP but omit Next. Compute from the learned-action-abilities bitfield + ability JP costs.

### 7. Equipment-helper expansion in fft.sh

`change_helm_to`, `change_garb_to`, `change_accessory_to`, etc. — stubbed pending an inventory reader. Inventory list isn't yet decoded from memory.

## Memory notes saved this session

- **`project_job_grid_cursor.md`** — updated with verified 6/7/6 layout, flat-linear-index formula, resolver design notes, and the row-cross desync fix.

(One file; the rest of the heap-cursor learnings are now codified in code + the TODO.)

## Things that DIDN'T work (avoid repeating)

- **Snap-to-(0,0) on Escape from CharacterStatus.** I committed b453fb1 thinking the game resets the PartyMenu cursor on Escape. It doesn't — it preserves the entry position. The misdiagnosis came from testing with the cursor already at (0,0). Reverted in the same session. **Always test cursor-preservation behavior with a NON-default cursor position before claiming "the game does X."**
- **Reading Lv + JP for all 19 grid cells.** Started exploring this for AC5; the design principle review killed it. Hovering shows the value; pre-populating doesn't change a decision.
- **Equipping the wrong unit via `change_job_to` after stale state.** When state machine and game cursor disagree on PartyMenu, every downstream helper acts on the wrong unit. Fix is the PartyMenu cursor read above.
- **Trying to rebuild the resolver on every screen call when it fails.** The first version of `ResolveJobCursor` would re-fire its 6-key oscillation every time `_resolvedJobCursorAddr` was 0 — including when the resolver legitimately failed to find a candidate. Burned ~1.5s per `screen` call. Fix: separate `_jobCursorResolveAttempted` flag that doesn't reset on failure (only on screen exit).

## Environment gotchas (carried forward)

- **Heap snapshots are ~200MB each.** Three of them per resolver run = ~600MB churn. Fine for our pattern (once per JobSelection entry); painful in a tight loop.
- **`_inputSimulator.SendKeyPressToWindow` bypasses the state machine.** Resolver uses raw sends so the state machine doesn't double-tick on its dummy keys. **Important:** this is also why resolver bursts can drift the OUTER cursor when fired during a transition window — state machine doesn't know those keys happened, but the game does. The MenuDepth==2 gate is the current mitigation.
- **JobSelection cursor cache shuffles per ROW CROSS, not per session entry.** Up/Down forces re-resolve. Left/Right keeps the cache valid.
- **`_jobCursorResolveAttempted` flag must be cleared on screen exit AND on every Up/Down.** Both clears are needed.
- **`StoryCharacterUniqueClass` map is the source of truth for "what class is at (0,0) for this unit."** When a new story character becomes recruitable, add them.

## Quick start next session

```bash
# Baseline
./RunTests.sh                # 1914 passing

# Live smoke — JobSelection + viewedUnit + cell state
source ./fft.sh
boot
esc                          # → PartyMenu (cursor on Ramza)
fft '{"id":"r","keys":[{"vk":13,"name":"enter"},{"vk":40,"name":"down"},{"vk":13,"name":"enter"}],"delayBetweenMs":500}'
                             # → JobSelection for Ramza
screen                       # first call — triggers auto-resolve (~1.5s flash)
                             # second call — ui=Gallant Knight, cursorRow=0, cursorCol=0,
                             # jobCellState=Unlocked, viewedUnit=Ramza

# Live smoke — change_job_to
change_job_to Chemist        # Ramza → Chemist (verified live this session)
```

## Three principles to internalize before working on this codebase

1. **The CommandWatcher is now ~3800 lines and is officially a god class.** `DetectScreen` alone is ~700 lines. Refactoring into per-screen detector classes is overdue but high-churn. Don't propose it without budget.

2. **Test cursor / state behavior with NON-default values before claiming "the game does X."** The b453fb1 misdiagnosis is the clearest example.

3. **Every payload field has to earn its spot** (§principle in TODO.md). If you can't write a one-sentence "what decision changes if Claude has this," drop it.

## Two more things

The session shipped against a clear backlog (the 17 JobSelection ACs from session 14's handoff). Looking back, ~14 of those 17 are now done or correctly punted. The remaining 3 are AC12 (Job detail payload), AC13-17 (LearnAbilities sub-screen). AC12 was scoped to nothing (job descriptions can be hard-coded if needed). AC13-17 hasn't been touched and needs its own pass through the design principle before starting.

The CommandWatcher god-class problem is going to bite eventually. Next person who has to add a new screen to `DetectScreen` should consider extracting at least the JobSelection block (~80 lines) into its own detector class as a proof-of-pattern. That gives a reference others can follow incrementally.

Good session. Cell-state classification + viewedUnit + change_job_to are the user-visible wins; the design principle codification + TODO trim are the longer-term wins.
