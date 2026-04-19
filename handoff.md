# Session Handoff — 2026-04-18 → 2026-04-19 (Session 44)

Delete this file after reading.

## TL;DR

**State-detection sweep: 5 screen-classification bugs fixed (BattleSequence, BattleChoice, GameOver, BattleStatus ui, BattleAttacking ui fallback) + 2 new pure-class resolvers wired + `god_ramza` dev helper. BattleVictory detection attempted and REVERTED (team=3 is necessary but not sufficient). Tests 3201 → 3242 passing across 20 commits, 0 regressions.**

The headline is **BattleChoice detection via .mes 0xFB marker + runtime modal flag** — a novel two-part discriminator that fuses script-data pre-scan (offline, per-session) with a memory-byte runtime check (at detection time). It cleanly distinguishes the choice modal from the narration prefix of the same event, which pure byte-fingerprint inspection could not.

User direction late-session: **refocus all work on state-detection bugs. Bad detection blocks everything else.** TODO §0 has a new 🔴 top-priority section consolidating 15 known state bugs with cross-references.

## Commits

| Commit | Subject |
|---|---|
| `8ee63d0` | BattleAttacking ui=(x,y) fallback + IsPartyTree live-verify + 2 closed myths |
| `249f660` | Tavern cursor re-locator technique + Task 7 progress |
| `856d288` | Element decode 3-of-5 fields live-confirmed on varied enemies |
| `5c0b59e` | Close 3 stale TODOs via code audit |
| `fe6e952` | Deprioritize 2 tasks per user direction |
| `5954d80` | Task 21: BattlePaused cursor resolver + label map |
| `afff421` | fft.sh: Fix obj= regression — empty-field collapse in tab-split |
| `daec206` | Task 23: TavernRumors/Errands cursor resolver |
| `e458f66` | TODO: Mark Task 23 as ~partial |
| `cd0d754` | Task 24: BattleSequence discriminator — 0x14077D1F8 flag |
| `29c547c` | Task 25: AbilityJpCosts coverage floor tests |
| `4dee442` | Task 26: Dedup duplicate TODO entries |
| `941646b` | Task 27: TargetingLabelResolver — null-out cursor fallback when X/Y are -1 |
| `392681b` | Task 30: Characterization-test sentinel (meta-test) |
| `a01422c` | Task 29: BattleChoice detection via .mes 0xFB marker |
| `8107123` | Task 29 pt2: BattleChoice needs BOTH eventHasChoice AND runtime modal flag |
| `c1a793c` | Session 44 extras: god_ramza helper + TODO state-priority consolidation |
| `ad507fb` | Task: BattleVictory detection via battleTeam=3 (REVERTED) |
| `a778601` | Revert BattleVictory — team=3 persists into post-V dialogue |
| `377bfd8` | GameOver detection: drop `!actedOrMoved` requirement + god_ramza trim level |

## What landed, grouped by theme

### Screen-detection fixes

- **BattleSequence discriminator** (`cd0d754`) — found `0x14077D1F8` via 3-way module-snapshot intersect across 2 safe WorldMap locations vs Orbonne minimap. 955 candidates from intersect; 2 verified live, 1 wired. New `battleSequenceFlag` param on `ScreenDetectionLogic.Detect`. 11 tests.
- **BattleChoice detector** (`a01422c` + `8107123`) — two-part signal: (1) `eventHasChoice` pre-scanned at `EventScriptLookup` load (is 0xFB anywhere in the .mes bytes?); (2) `choiceModalFlag` at `0x140CBC38D` read live (is the modal drawn right now, not the narration prefix?). Live-verified at Mandalia event 016. 4 tests.
- **GameOver detection** (`377bfd8`) — dropped the `!actedOrMoved` requirement. The enemy kill-action leaves `battleActed=1, battleMoved=1` set at the GameOver frame; `gameOverFlag` at `0x140D3A10C` is authoritative and doesn't need action-state disambiguation. Live-verified at Siedge Weald.
- **BattleStatus ui=\<activeUnit\>** (`31cbe68` from earlier) — new `BattleStatusUiResolver` pulls from `_cachedActiveUnitName`. Root-cause bug fix: CommandWatcher:6190 EqA-promote block was unconditionally renaming `screen.Name` from `BattleStatus` → `EquipmentAndAbilities` whenever the equipment mirror matched the active unit. Excluded BattleStatus from promotion.
- **BattleAttacking ui=(x,y) fallback** (`8ee63d0`) — `TargetingLabelResolver.ResolveOrCursor` returns the ability label when latched, else the cursor tile "(x,y)", else null (for cursor=-1 uninitialized).
- **fft.sh obj= regression** (`afff421`) — bash `IFS=$'\t' read` collapses adjacent empty tab fields, shifting every field after the first empty one LEFT. Fix: `\x01` (non-whitespace) delimiter both in JS emit and bash IFS.

### Pure-class resolvers + label maps (new)

- **`BattlePauseMenuLabels`** (`5954d80`) — 6-item pause menu. 11 tests.
- **`BattlePauseCursorResolver`** (in CommandWatcher, same commit) — same template as `ResolvePickerCursor`.
- **`ResolveTavernCursor`** (`daec206`) — identical template; gated on `ScreenMachine.CurrentScreen` (not `screen.Name`) because detection returns "LocationMenu" and the outer SM-override happens AFTER resolver runs.
- **`BattleStatusUiResolver`** (`31cbe68`) — pure 1-line map with 2 tests.

### Infrastructure / helpers

- **`god_ramza` bash helper** (`c1a793c` + `377bfd8`) — writes endgame gear (Ragnarok / Kaiser Shield / Grand Helm / Maximillian / Bracer) + Brave/Faith 95 to Ramza's roster slot. Level/EXP intentionally NOT changed (leveling to 99 scaled random encounters to Lv99 enemies and killed the party).
- **Characterization-test sentinel** (`392681b`) — meta-test that pins count of characterization tests in the suite, enforcing the session-43 "pin the bug, flip on fix" convention.
- **AbilityJpCosts coverage floor** (`29c547c`) — 3 regression guards (min-entry count, every JP-purchasable skillset resolves to ≥1 non-null, zero unresolved names).
- **TargetingLabelResolver.ResolveOrCursor -1 fix** (`941646b`) — returns null when cursor is uninitialized so ui= cleanly drops rather than rendering "(-1,-1)".

### Things closed without code

- **2 SaveSlotPicker-from-BattlePaused entries closed as myth** — pause menu has 6 items, no Save. User correction mid-session.
- **3 detection-ordering tasks closed via code audit** — already shipped incrementally in sessions 21-26, never marked done.
- **TODO dedup** (`4dee442`) — deathCounter ×3, chargingAbility ×2, element-sigils ×2 → 1 each. Net -11 lines.

## Technique discoveries worth propagating

### 1. `.mes` script-data pre-scan as a runtime discriminator

The best session-44 insight: **the game's own script data tells us what each event does**. `EventScriptLookup` loads every `event*.en.mes` at mod init and decodes dialogue lines. Adding a one-line byte scan (`Array.IndexOf(bytes, 0xFB)`) at load gives us a per-event `HasChoice` flag — zero runtime cost, works offline. This is the cleanest type of discriminator: it answers "what kind of screen is this?" from data the mod already loads rather than from memory fishing.

Potentially applicable to other state splits: Cutscene vs BattleDialogue (if different .mes byte patterns correlate with "in-battle" vs "out-of-battle"). Worth scanning all 100+ event files for other-marker patterns next session.

### 2. Two-part discriminator pattern (offline data × runtime flag)

When a single byte isn't enough to tell states apart, combine a session-stable offline signal (script contents, location whitelist, job tags) with a runtime byte. BattleChoice uses `eventHasChoice` (offline) AND `choiceModalFlag` (runtime) — each alone has false positives; together they're precise. Same pattern applies for BattleSequence (location whitelist + `0x14077D1F8` flag). Memory note: `project_battle_sequence_discriminator.md`.

### 3. `batch_read` atomic N-address reads (avoid Schrödinger-bit noise)

Sequential `read_address` calls through the bridge cause heap churn — each read triggers widget activity that shifts candidate bytes, so a byte that read `1` on attempt 1 might read `0` on attempt 2 for reasons unrelated to game state. `batch_read` with an `addresses: [{addr,size,label}]` array is atomic server-side. Used during BattleChoice narrow-down to cleanly filter 992 candidates → 174 → 14 without thrashing.

### 4. Triple-intersect re-locator for heap cursor bytes

Generalized the TavernRumors hunt from session 33:
1. Snap heap on screen A (cursor at row 0)
2. Move cursor to row 1, snap
3. Move cursor to row 2, snap (etc)
4. Diff adjacent pairs for monotonic transitions (`0→1`, `1→2`, etc.)
5. Intersect the address sets — true cursor bytes appear in ALL; single-diff-only entries are noise
6. Live-read the intersection to pick the real byte

Worked for BattlePaused (3 candidates) and TavernRumors (7 candidates). **Known limitation**: BattlePaused's `ResolveBattlePauseCursor` uses a weaker 2-step verify (mirror of ResolvePickerCursor) that latches onto an initial-state byte rather than a live-tracking byte. Strengthening to a 3-way triple-intersect in code is deferred.

### 5. Pre-seed candidate list BEFORE the transition window closes

User's late-session guidance: "Next time hit the state several times so you can see the transitions." For transient states (Victory banner, ~10s), polling 3-5 times across the window captures the byte's temporal evolution; a single one-shot during the banner can't tell whether a byte is "banner-specific" or just "happens to be 1 during these 10 seconds for some other reason." Carry forward to BattleVictory hunt.

### 6. SM-state gate vs screen-name gate

Detection rules run in a specific order; some screen-name rewrites happen AFTER detection (outer SM-override at response serialization). For resolvers that need to fire during the `DetectScreen` method, gate on `ScreenMachine.CurrentScreen == GameScreen.X` instead of `screen.Name == "X"` — the SM enum is stable within the method, the string gets overridden later. Task 23 had to fix this after the initial wire-in didn't fire.

## What's NOT done — top priorities for next session

### 1. BattleVictory detection — two signals available, not yet combined

**Context:** Victory banner + post-V narration both read `battleTeam=3`. The single-signal rule (ad507fb) over-fired on post-V dialogue and was reverted (a778601).

**User's next-session idea (worth trying first):** "Do we scan in a battle after an attack? If we detect all enemies HP=0 that might help." — yes, `scan_move` already collects every unit's HP/team. Detection could consult the cached battle state: if every non-player unit has `hp==0`, we're in Victory territory. Combined with `battleTeam==3`, that's a clean two-signal rule.

**Also available**: 423 heap candidates saved to `c:/tmp/v_banner_winners.txt` that went `0→1` entering the Victory banner and `1→0` leaving it. Next Victory screen, batch-read these 423 and keep only those that are 1 → should narrow to a handful of banner-specific bytes.

Concrete next-session plan:
1. Start with the "all enemies HP=0" approach — no memory hunt needed, just a detection-rule addition that consults cached `BattleState.Units`
2. If that's insufficient or has edge cases, fall back to the 423-candidate heap hunt on the next Victory screen

### 2. BattleDesertion live-capture — never reached this session

Desertion fires on pre-battle Brave/Faith thresholds. We only saw post-battle Victory captures labeled as Desertion (wrong), never a real Desertion. Need a unit with low Brave/Faith (≤10) to trigger. Fix depends on capturing this fingerprint for real.

### 3. Cutscene vs BattleDialogue — still conflated

11 datapoints collected session 44 but no byte-level discriminator. Event-whitelist approach (per user's earlier suggestion) is the likely path: catalog eventIds as "pre-/post-battle cinematic" vs "mid-battle text" as the playthrough progresses. The `.mes` files are identical across 5 of 8 sample events suggesting FFT re-uses scripts across beats — unlikely to find a purely offline signal. Try: add a second `.mes` byte pattern scan (different marker that correlates with "fights happen during this event") next session.

### 4. BattleSequence sticky-flag after restart

`0x14077D1F8` persists in save state. After `restart` lands back on a BattleSequence location, the byte reads 1 but the game shows plain WorldMap (Enter re-enters minimap). First-frame misfire. Memory note `project_battle_sequence_flag_sticky.md` has 3 fix approaches.

### 5. BattlePaused + scan_tavern resolvers — strengthen discrimination

Both use 2-step Down-Down verify that latches onto a byte that reports the INITIAL cursor correctly but doesn't track live nav. Result: `ui="Data"` fires at row 0 but moving down doesn't update. Fix paths documented in TODO; either (a) 3-way intersect in the resolver, (b) iterate all ~32 candidates scoring by live tracking, or (c) restrict candidates by address range.

### 6. Detection leaks CharacterStatus/CombatSets during battle_wait animations

Session 31 noted `session_tail` shows 15-23s latency rows labeled `sourceScreen=CharacterStatus → targetScreen=BattleMyTurn` during `battle_wait`. False-positive during facing-confirm animations. Repro: run `session_tail slow 1500` during a battle, look for `*→BattleMyTurn` with non-Battle source.

### 7. `scan_move` over-reports abilities + team misclassification on story battles

(Orbonne opening) Session 44 found:
- Ramza's Mettle skillset shows ALL 8 abilities instead of the ~5 learned — filter bug on the abilities-list path
- Units labeled `[ENEMY]` that are actually ALLIES; monster "Ahriman" labeled `[PLAYER]` — team-byte interpretation bug on story battles

Both documented in TODO §0. Fix path: audit scan_move's ability-list population against the roster's learned bitfield (+0x32+jobIdx*3), and audit the team-byte read for story-battle unit structs.

## Things that DIDN'T work (don't-repeat list)

1. **Single-byte BattleVictory rule (`battleTeam==3` alone)** — the byte persists into post-V dialogue, causing false positives on subsequent cutscenes. Reverted in `a778601`. Combine with another signal (see next-session plan above).
2. **Sequential `read_address` calls for candidate verification during cursor hunts** — each call triggers widget activity that shifts the heap, so "reads 1" on one attempt and "reads 0" on the next attempt can both be true without game-state change. Always use `batch_read` for atomic N-address reads.
3. **Hardcoded heap address `0x140C70055` for choice modal flag** — was 1 during choice modal but ALSO 1 during other cutscenes (second-pass validation failed). Needed a 4-pass narrow-down across TWO independent cutscenes to get to `0x140CBC38D`.
4. **Leveling Ramza to 99 via `god_ramza`** — scaled random encounters to Lv 99 enemies, got the party wiped. Removed from `god_ramza`. Gear + Brave/Faith is enough.
5. **Chunked `batch_read` at 5000 addresses/chunk** — crashed the bridge twice. 500/chunk is safer.
6. **Traveling FROM a BattleSequence minimap** — triggers the story battle instead of navigating away. Hold-B dismisses; `restart` lands outside the sub-screen. Memory note `feedback_battle_sequence_exit.md`.
7. **Entering BattleSequence locations with `EnterLocation`** — triggers the next story battle. The minimap auto-opens on cursor arrival; no further action needed. Memory note `feedback_battle_sequence_loc_auto_opens.md`.
8. **`save` before risky tests** — user explicitly forbade. Memory note `feedback_no_autonomous_save.md`. The game auto-saves at checkpoints; any additional save is unwanted.

## Things that DID work (repeat-this list)

1. **User-driven live-capture cadence** — "Go now" / "I'm on X now" / "Now I'm on Y" pacing gave clean datapoint pairs for all the state discriminator hunts. Much more efficient than trying to script state transitions autonomously.
2. **Asking user what screen state they're on** when detection gives a value that doesn't match the screenshot — confirms detection error immediately without ambiguity.
3. **.mes byte scan** — 10-minute hunt produced the cleanest BattleChoice discriminator. Worth attempting for other state splits first before diving into memory hunts.
4. **Commit-per-task cadence** — 20 commits session 44, each self-contained with tests + description. Easy to revert (did it once, for BattleVictory). Easy to cherry-pick.
5. **Live-verify immediately after each detection change** — caught the 2-part BattleChoice failure (flag over-fired on narration) before it could confuse subsequent work.
6. **TODO top-priority consolidation** — the 🔴 State Detection section with cross-references makes "what's still broken" immediately scannable. User noted detection bugs were blocking everything else; top-of-file placement enforces that priority.
7. **Snapshot-first, diff-second methodology** — take the snapshots, move cursor, take more snapshots, THEN think about what to diff. Avoids "oh I needed one more capture" waste.
8. **Characterization-test pattern** — session 43 convention of pinning known-latent bugs as tests (rather than skipping them) continues to pay off. Sentinel test added session 44 to enforce the convention.

## Memory notes saved this session

New:
- `project_battle_sequence_discriminator.md` — the triple-intersect technique + 2 discriminator bytes + next-session options
- `project_battle_sequence_flag_sticky.md` — save-state persistence edge case + 3 fix approaches
- `project_battle_pause_cursor.md` — 3 lockstep bytes at BattlePaused + the re-locator methodology
- `project_battle_choice_cursor.md` — 6-pass narrow-down technique from 2751 → 14 candidates
- `feedback_no_autonomous_save.md` — do not save without explicit user ask
- `feedback_battle_sequence_loc_auto_opens.md` — minimap auto-opens on cursor arrival
- `feedback_battle_sequence_exit.md` — Hold-B or restart to exit minimap

Updated:
- `project_tavern_rumor_cursor.md` — added Bervenia session-44 datapoint confirming +0x28 widget offset pattern

Index: `MEMORY.md` updated with all 7 new pointers.

## Quick-start commands for next session

```bash
# Baseline sanity
./RunTests.sh                               # expect 3242 passing
source ./fft.sh
running                                     # check game alive

# State-detection smoke tests
boot                                        # get to WorldMap
screen                                      # current state (should be correct)

# Verify BattleSequence detection
world_travel_to 18                          # Orbonne — minimap auto-opens
screen                                      # expect [BattleSequence]
restart                                     # back to WorldMap

# Verify BattleChoice detection (requires progress to Mandalia)
# On the "1. Defeat Brigade / 2. Rescue captive" modal:
screen                                      # expect [BattleChoice]
# During pre-choice narration:
screen                                      # expect [BattleDialogue]

# Verify GameOver detection (requires a loss)
# After party wipe:
screen                                      # expect [GameOver]

# god_ramza — only use during state-collection playthroughs
god_ramza                                   # endgame gear + Brave/Faith 95

# Check BattlePaused cursor resolver (known limited)
# In battle, open pause:
screen                                      # ui=Data at row 0 ideally; may be stale
```

## Top-of-queue TODO items the next session should tackle first

From `TODO.md §0 🔴 State Detection — TOP PRIORITY`:

1. **BattleVictory detection** — user's "all enemies HP=0" idea is the top candidate. If insufficient, fall back to batch-read of `c:/tmp/v_banner_winners.txt` (423 heap addresses).
2. **scan_move learned-ability filter** — fresh-game Ramza shows all 8 Mettle abilities instead of ~5. Audit the scan output path against the learned bitfield.
3. **scan_move team misclassification on story battles** — dump the Orbonne opening unit structs, compare team byte to random-encounter battles. The bug breaks autonomous story play.
4. **BattleSequence sticky-flag after restart** — find a truly-runtime companion flag OR implement a 0→1 transition detector.
5. **BattlePaused cursor resolver strengthening** — triple-intersect inside the resolver (like the manual hunt), OR iterate all 32 candidates and score by live-tracking fidelity.

## Insights / lessons captured

- **Offline signals beat memory fishing when they're available.** `.mes` byte scan solved BattleChoice in 10 minutes; 6-pass heap narrow-down took 45+ minutes and still needed a second signal to disambiguate. Always check what the mod already loads before hunting.
- **Detection rules don't compose by accretion — they collide.** Every new rule has to check its ordering against existing rules. BattleChoice over-fired because the narration portion of a choice event has `eventHasChoice=true` trivially; runtime flag was required. BattleVictory over-fired because `battleTeam=3` persists past the banner.
- **Live-verify reveals assumptions that tests miss.** 3242 tests pass but the Victory-screen detection has been wrong across multiple sessions because nobody ever live-captured the fingerprint. Session 44 got two Victory screens in and the existing rule's `!actedOrMoved` assumption fell instantly. Test suites can only codify what you've captured.
- **User-driven live cadence > scripted retry loops.** Every bridge crash this session (3-4) happened during autonomous multi-step bash loops; none during "user says go, I scan once." The bridge is single-tick-at-a-time; respect it.
- **`[x]` completion marker isn't the bookkeeping unit — commit-per-task is.** Having 20 tight commits with self-contained test coverage meant the Revert of BattleVictory (ad507fb) was one `git revert` and detection went back to working state cleanly. No multi-file untangling needed.
- **The user's corrections are cheaper than the bridge's detection accuracy.** Every time the user said "wrong" or "that's stale" or "you captured it correctly" I got instant validation of what would have taken 3+ scans to confirm. Ask more, assume less.
- **Save cadence belongs to the user.** Session 44 triggered one almost-destructive `save` path before the `feedback_no_autonomous_save.md` memory was added. The save-slot picker defaults to "New Save Data" which is rarely what anyone wants. Never save without being explicitly told.
