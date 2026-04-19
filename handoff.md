# Session Handoff — 2026-04-19 (Session 45)

Delete this file after reading.

## TL;DR

**Big session: 8 commits, tests 3242 → 3283, two major features shipped (live dialogue box + crystal/treasure state detection), one critical bug fixed (BFS + corpses), one state-leak bug fixed (enemy-turn preempting player submode rules). All live-verified at Dorter and Zeklaus battles.**

The headline: **live in-game dialogue text now surfaces under the screen line**, box-by-box, matching what's on the TV so the user can pace the story without screenshots. Decoder evolved across the session from "wrong bytes labelled as boundaries" → "FE=bubble, F8=line-wrap, 2+F8 also a boundary" after a live Dorter walkthrough gave us 45 ground-truth bubbles to diff against.

## Commits

| Commit | Subject |
|---|---|
| `eb53261` | auto_place_units: accept BattleDialogue/Cutscene as end states |
| `207f66b` | Surface current dialogue box on BattleDialogue/Cutscene/BattleChoice |
| `9cc1f8a` | MesDecoder: flip F8/FE roles — FE is the bubble boundary, F8 is intra-line wrap |
| `b0bea6c` | MesDecoder: 2+ consecutive 0xF8 is a bubble boundary too |
| `3e12c31` | ScreenDetectionLogic: gate BattleMoving/Attacking/Waiting on battleTeam==0 |
| `155b8e6` | BFS: treat corpses like allies — pass-through, no stop |
| `d083c11` | Detect 4 crystal-pickup states: MoveConfirm / Reward / AcquireConfirm / LearnedBanner |
| `663f630` | Split BattleRewardObtainedBanner from BattleCrystalMoveConfirm |

## What landed, grouped by theme

### Live dialogue rendering (the headliner)

- **`DialogueProgressTracker`** — pure serial counter, bumps on `advance_dialogue` and `execute_action Advance`, resets on eventId change (including return-to-prior event). 6 tests.
- **`MesDecoder.DecodeBoxes`** — new splitter with fully-corrected byte rules after live walkthrough:
  - **0xFE** = bubble boundary (any run length = 1 boundary)
  - **0xF8** (single) = intra-bubble line wrap
  - **0xF8+** (2+ consecutive) = bubble boundary (same speaker continuation)
  - **Speaker change** (`0xE3 0x08 ... 0xE3 0x00`) = implicit boundary
- **`DetectedScreen.CurrentDialogueLine`** — new payload populated only on `BattleDialogue` / `Cutscene` / `BattleChoice` (state-gated both server- and shell-side).
- **fft.sh compact render** — prints under the header line, e.g. `Swordsman [0/22]: I said I know naught of it`.

**Live-verified correctness:** Dorter event 34 boxes 0-1 matched in-game bubbles exactly. Zeklaus event 40 boxes 0-5 matched exactly. Dorter event 38 walkthrough gave 45 real bubbles vs 37 decoded (still under-splits a few multi-bubble paragraphs — see "Remaining decoder gaps" below).

### Battle state-detection fixes

- **Enemy/ally turn preempts player submodes** (3e12c31) — the `battleMode==1/2/4/5` rules (BattleMoving/Attacking/Waiting) were firing regardless of whose turn it was. During enemy pathing the bridge reported `BattleMoving` as if the player was picking a tile. Fix: promote the team-owner rules to run BEFORE the submode rules. Live-verified at Zeklaus — Cornell's ally turn correctly rendered `[BattleAlliesTurn]`, enemy turn correctly rendered `[BattleEnemiesTurn]`.
- **Crystal-pickup sequence** (d083c11 + 663f630) — 4 new states detected, live-captured fingerprints in `memory/project_crystal_states_undetected.md`:
  - `BattleCrystalMoveConfirm` — Yes/No "Move to this tile and obtain/open?" (triggers on crystal OR chest)
  - `BattleCrystalReward` — "Abilities Conferred by the Crystal" Acquire/Restore chooser
  - `BattleAbilityAcquireConfirm` — "Acquire this ability?" Yes/No
  - `BattleAbilityLearnedBanner` — "Ability learned!" banner (encA=0)
  - `BattleRewardObtainedBanner` — "Obtained X!" chest loot banner (encA=1)
- **Discriminator:** `moveMode` separates player picking a tile (255) from crystal modals (0). `encA` separates the sub-states (0=learned, 1=obtained, 2=MoveConfirm, 4=Reward, 7=AcquireConfirm).

### BFS fix: corpses pass-through, don't block

- **Bug:** `NavigationActions.ComputeValidTiles` caller dropped dead units via `if (u.Hp <= 0) continue;` so BFS treated corpse tiles as empty — scan_move listed `(3,1)` as movable, battle_move succeeded but game wouldn't let Ramza stop there.
- **Fix:** classify by `StatusDecoder.GetLifeState`:
  - `dead` → allyPositions (pass-through, no stop)
  - `crystal` / `treasure` → skip entirely (tile IS walkable + stoppable after pickup modal)
  - `alive` → existing team-based routing
- Same fix applied to `GetEnemyPositions` / `GetAllyPositions` for downstream consumers.

### Other infrastructure

- **`AutoPlaceUnitsEndState`** (eb53261) — pure predicate, accepts `BattleDialogue` and `Cutscene` as "battle has started" so the auto_place helper exits early at story battles (~19s instead of ~40s).

## The technique of the session

**User-driven live walkthrough as decoder validation.** The dialogue decoder went through 3 iterations of wrong byte rules (F8-as-boundary → FE≥2-as-boundary → FE-as-boundary) each discovered by comparing decoded output against a single Dorter event 38 walkthrough (45 real bubbles typed out by the user). Offline byte inspection alone couldn't distinguish "FE means boundary" from "F8 means boundary" — but the ground-truth count told us instantly. Generalizable to all state-discriminator work: live bubble-by-bubble ground truth > armchair byte analysis.

## Remaining gaps (prioritized for next session)

### 🔴 1. BattleVictory detection — fingerprint captured, fix drafted, not shipped

Live-captured at Zeklaus win: **`encA=255` + `encB=255`** is a unique sentinel. All other states this session had encA in [0..7]. Session 44 tried `battleTeam==3` and reverted because it persisted into post-V dialogue; encA=255 is transient to the banner itself.

Memory note: `memory/project_battle_victory_encA255.md`. Proposed rule:

```csharp
if (encA == 255 && encB == 255 && battleMode == 0 && paused == 0)
    return "BattleVictory";
```

Needs a TDD pass + commit. Untested against BattleDesertion (need a low-Brave/Faith unit to trigger).

### 🟡 2. Dialogue decoder under-splits multi-bubble paragraphs (event 38 case)

Dorter event 38: 45 real bubbles vs 37 decoded. Boxes 0, 5, 7 bundle 2-3 real bubbles each. Byte pattern unclear — possibly a 3rd boundary byte we haven't seen, or a stop-char inside text. Event 40 decoded cleanly (9 boxes ≈ 7 real + 2 untested post-battle). **Collect more .mes ground-truth data to narrow.**

### 🟡 3. Raw Enter doesn't bump dialogue tracker

When strict mode is off, user's raw `enter` advances the game but not our counter → `[0/22]` while game is on real bubble 8. Fix: hook `SendKey(VK_ENTER)` in NavigationActions when current detected screen is BattleDialogue/Cutscene/BattleChoice; bump the tracker there.

### 🟡 4. Event 41 starts mid-.mes-file (dialogue offset on scene re-entry)

Post-Zeklaus scene (event 41) opens with "Ramza: These sand rats are long in the slaying" but the .mes file's box 0 is the PRE-battle Corpse Brigade Knight line. The game has some dialogue-offset byte we haven't identified. Our counter resets to 0 which is wrong for these compound scenes. Deferred until we can track the real current-bubble byte — possibly the hunt that session 33 started on TavernRumors cursor.

### 🟡 5. Chest "Obtained X" sub-states share fingerprint with crystal banner

Now split (encA=0 vs 1 vs 2) but the next chest/crystal encountered may have a different encA than captured this session — encA is probably a widget-stack-depth byte, not a dedicated semantic flag. Needs cross-session stability verification.

### 🟢 6. auto_place_units flaky at story battles

Crashed twice at Dorter formation before working on 3rd try. Likely races the formation-load animation. Memory note `feedback_auto_place_crashes_dorter.md`.

### 🟢 7. Speed-run: kill_enemies_to_1hp helper blocked

`search_bytes` bridge action caps at 100 results and doesn't filter by heap range, so the top-100 main-module matches crowd out the heap entries we need. One-line fix = expose minAddr/maxAddr on `search_bytes`. Scaffold script at `tmp/kill_enemies.sh`. Memory: `project_kill_enemies_helper.md`.

## Story progress

Ramza fought at **Dorter Slums** (won — rescued Corpse Brigade Swordsman), then **Zeklaus Desert Siedge** (won — confronted Wiegraf's old Corpse Brigade, met the "Sand Rat's Sietch" quest line). Objective shifted to **Eagrose Castle** as session ended on WorldMap.

Notable tidbits we picked up from scene dialogue: Gustav Margriff (Dead Men lieutenant) kidnapped Marquis Elmdore without Wiegraf's knowledge. Delita remembered Wiegraf from Eagrose post-war. Sand rats live in the Zeklaus Desert sietch.

## Memory notes written this session

New:
- `project_crystal_states_undetected.md` — 4-state crystal sequence fingerprint table + proposed rule
- `project_battle_victory_encA255.md` — encA=255 Victory discriminator (not yet shipped)
- `project_kill_enemies_helper.md` — speedrun cheat blocked by search_bytes 100-cap
- `feedback_auto_place_crashes_dorter.md` — flaky formation crash warning

Index updated at the bottom of `MEMORY.md`.

## Quick-start commands for next session

```bash
# Baseline sanity
./RunTests.sh                               # expect 3283 passing

source ./fft.sh
running                                     # check game alive
screen                                      # should be WorldMap at Dorter, objective=Eagrose

# Continue story
world_travel_to 2                           # head to Eagrose Castle

# During next battle — verify crystal states on fresh interaction
#   - Kill an enemy with 3+ empty turns to force crystallize
#   - Step onto crystal tile → expect [BattleCrystalMoveConfirm]
#   - Press Yes → expect [BattleCrystalReward]
#   - Pick Acquire → expect [BattleAbilityAcquireConfirm]
#   - Yes → expect [BattleAbilityLearnedBanner]

# When Victory fires — check encA=255 hypothesis
./fft.sh  # source it
fft '{"id":"post-win","action":"dump_detection_inputs"}' >/dev/null && \
  node -e "const d=JSON.parse(require('fs').readFileSync('/c/Program Files (x86)/Steam/steamapps/common/FINAL FANTASY TACTICS - The Ivalice Chronicles/Reloaded/Mods/FFTColorCustomizer/claude_bridge/response.json','utf8')); console.log(d.info);"
# If encA=255, ship the rule via TDD.
```

## Lessons captured

- **Live ground-truth > offline byte analysis.** The MesDecoder went through 3 wrong iterations of the F8/FE split rules. Each was defensible from offline byte inspection alone but only the bubble-by-bubble user walkthrough told us which direction was correct. Save the user-input cost by collecting one walkthrough EARLY when the decoder/discriminator work starts.
- **Flakiness costs tests tests tests.** auto_place_units crashed twice at Dorter formation before working. Had the `AutoPlaceUnitsEndState` predicate been tested in isolation sooner, the crash repro would have been obvious (the 30s poll that never ends explains the hang but not the crash — crash is the game-side race).
- **State discriminators live on a continuum of quality.** Single-byte sentinels like `encA=255` (Victory) are the holy grail. Multi-byte compound rules like the crystal states (moveMode + paused + encA compound) are next-best. Worst is "look at a screenshot" — which we did once for the chest state to find out it shared fingerprint with the crystal state.
- **The user's time is the primary scarce resource.** Every time I asked for live input (bubble count, screen state, what's on screen) the answer was immediate and high-signal. When I spun on offline analysis before asking, I burned context and came back with wrong answers. Ask sooner.
- **Commit-per-task cadence keeps the door open for reverts.** 8 commits this session, each tight and self-contained. The decoder fix required THREE commits because we discovered the rules iteratively — having each iteration as its own commit meant we could trace "when did the behavior change?" cleanly.
- **Screenshots in the loop save hours.** Every screenshot-verify-detection cycle caught a misdetection within 30 seconds that would have taken 15+ minutes of memory diffing to find cold. Lean on them hard during state-discriminator work.

---

Big thanks to Patrick for the live playthrough + ground-truth walkthrough — this session moved from "dialogue pacing is broken" to "decoder works on 2 events, state detection ships 5 new screens, BFS bug fixed" in a single sitting. 🎩
