# Session Handoff — 2026-04-16 (Session 21)

Delete this file after reading.

## TL;DR

**EncounterDialog detection shipped and cross-session verified.** Dedicated
memory flag at `0x140D87830` replaces the broken encA/encB noise counters.
Also: comprehensive battle state verification (11/13 states screenshot-verified),
BattleSequence scaffolding built (disabled pending memory discriminator),
several new detection bugs discovered and filed.

**Tests: 2048 passing** (up from 2044 at session start). 4 net new tests
(replaced 1 old EncounterDialog test with 5 new ones).

## What landed, grouped by theme

### 1. EncounterDialog detection (the headline)

Wired `0x140D87830` into `ScreenAddresses[28]` as a dedicated encounter
flag. Reads 10 during encounter dialog, 0 otherwise. Reverts to 0 after
fleeing. Replaces the old encA/encB gap-based approach which was disabled
in session 20 due to sticky noise counters.

Changes:
- `ScreenDetectionLogic.cs`: new `encounterFlag` parameter, two rules
  re-enabled (at named location + while traveling)
- `CommandWatcher.cs`: address added to ScreenAddresses, wired through
  both DetectScreen call sites + dump_detection_inputs diagnostics
- `ScreenDetectionTests.cs`: 5 new tests (flag=0 no-trigger, at-location,
  while-traveling, not-in-battle, not-in-partymenu)

Cross-session verified at TheSiedgeWeald: 2 encounters detected correctly,
both reverted to WorldMap after flee, no false triggers on WorldMap/
PartyMenu/LocationMenu navigation.

### 2. Battle state verification

Screenshot-verified 11 battle states against live game:

| State | Result |
|---|---|
| BattleMyTurn | ✅ correct |
| BattleMoving | ✅ correct |
| BattleAbilities | ✅ correct |
| BattleAttacking | ✅ correct |
| BattleWaiting | ✅ correct |
| BattleStatus | ✅ correct |
| BattlePaused | ✅ correct |
| BattleFormation | ✅ correct |
| BattleEnemiesTurn | ✅ correct |
| GameOver | ✅ correct |
| EncounterDialog | ✅ correct |
| BattleVictory | ❌ misdetects as BattlePaused |
| BattleDesertion | ❌ misdetects as BattlePaused |

BattleActing too transient to catch. BattleAlliesTurn needs guest allies.
BattleDialogue and Cutscene blocked by sticky gameOverFlag bug.

### 3. BattleSequence scaffolding (disabled)

Built full infrastructure for multi-stage campaign sub-selector minimap
(e.g. Orbonne Monastery Vaults). **Detection rule disabled** because the
screen is byte-identical to WorldMap across all 29 detection inputs when
standing at a whitelist location.

What's in place (ready to enable when discriminator found):
- `BattleSequenceLocations` HashSet: 8 locations (Riovanes, Lionel,
  Limberry, Zeltennia, Ziekden, Mullonde, Orbonne, Fort Besselat)
- `NavigationPaths`: CommenceBattle (Enter) + PartyMenu (Escape)
- SM sync: maps to GameScreen.Unknown
- LocationSaveLogic: saves location on BattleSequence
- Detection rule (commented out): checks slot0!=255, !onWorldMapByMoveMode,
  rawLocation in whitelist, locationMenuFlag=0, tab flags=0

### 4. OutfitterSell inventory verified

`screen.inventory` on OutfitterSell was shipped in session 18 but flagged
⚠ UNVERIFIED. Live-verified session 21: 146 sellable items displayed
grouped by type with counts and estimated prices. Removed UNVERIFIED flag.

### 5. Dorter shop stock captured via buy-diff

Proved the buy-1-of-everything technique for discovering per-shop stock:
- Read inventory before buying (snapshot A)
- Buy 1 of each item at Dorter Outfitter
- Read inventory after (snapshot B)
- Diff: 120 items incremented + 10 items at 99 cap = 130 total items

Dorter's complete stock: 130 items across all categories. Confirmed stock
is per-location AND per-chapter (not global).

Also found global weapons master list at `0xEA01DC` (stable main-module
address, ~120 weapon IDs, FF-terminated). Shield stock found in heap at
`0x15B8C42E8` but shifts on restart.

## What's NOT done (top priorities for next session)

### 1. BattleVictory/BattleDesertion misdetect as BattlePaused

Root cause: `slot0=0x67` (not 255) during Victory/Desertion screens.
`unitSlotsPopulated` requires slot0==255, which fails. Both rules fall
through to BattlePaused. Fix: relax the rules to not require
unitSlotsPopulated — use `battleModeActive && actedOrMoved && battleMode==0`
instead. Inputs captured in TODO.

### 2. Cutscene/BattleDialogue misdetect as LoadGame after GameOver

Sticky `gameOverFlag=1` from prior GameOver causes LoadGame rule to
preempt both. Fix: add `eventId < 1 || eventId >= 400` guard to LoadGame
rule so real cutscenes (eventId 1-399) aren't swallowed.

### 3. BattleSequence memory discriminator

All 29 detection inputs are identical between WorldMap and BattleSequence
at whitelist locations. Heap addresses for shop stock shift on restart.
Next step: heap diff scan while ON the minimap vs WorldMap at same
location, or find a stable flag in the main module.

### 4. Per-shop stock list (OutfitterBuy)

Global weapons master at `0xEA01DC` found (stable). Per-shop filter and
non-weapon categories (shields, armor, hats, accessories, consumables)
still in heap. Buy-diff technique works but is expensive in gil/time.

## Things that DIDN'T work (don't repeat)

1. **Location whitelist alone for BattleSequence** — False-triggers on
   WorldMap when the save is at a whitelist location (fresh boot at
   Orbonne detected as BattleSequence). All 29 detection inputs are
   identical between the two screens.

2. **menuDepth as BattleSequence discriminator** — Read 1 on first check,
   0 on second check. Not stable on this screen.

3. **hover==rawLocation as discriminator** — Both WorldMap and
   BattleSequence show hover=254 (cursor not on a location node).

4. **Shop stock heap address from session 19** (`0x5D9B52C0`) — Dead
   after restart. Heap address, not main module.

5. **AoB search for 0B0C0D0E0F** (ninja blade IDs) — 100 matches.
   Too common a byte sequence.

## Things that DID work (repeat)

1. **Dedicated memory flag for EncounterDialog** — `0x140D87830` is a
   clean binary signal (10=encounter, 0=not). No threshold math, no
   noise. This is how all screen detection should work.

2. **Buy-diff technique for shop stock** — Buy 1 of everything, diff
   the 272-byte inventory array. Gives exact per-shop stock in one pass.
   Works even when items are already owned (delta=+1 is the signal).

3. **Screenshot ground truth for state verification** — Every state
   detection was verified against a screenshot. Caught 2 real bugs
   (Victory/Desertion) that would have been missed by code review alone.

4. **8-byte AoB for katana IDs** — Only 17 matches vs 100 for 5-byte
   pattern. Longer patterns with less common byte values work better.

## New states discovered

- **BattleSequence** — Multi-stage campaign sub-selector minimap.
  Scaffolding built, detection disabled. Needs memory discriminator.
- **BattleChoice** — Mid-battle objective choice screen (e.g. "protect
  him at all costs" vs "press on to battle"). Needs investigation.

## Memory notes saved this session

None new — findings documented in TODO and this handoff.

## Quick start next session

```bash
# Baseline check
./RunTests.sh                # 2048 passing

# Live smoke — EncounterDialog verification
source ./fft.sh
boot                          # if game isn't running
world_travel_to 26            # travel to TheSiedgeWeald (may trigger encounter)
screen                        # should show [EncounterDialog] if encounter hit
execute_action Flee            # flee back to WorldMap

# Quick state check
screen                        # [WorldMap]
execute_action PartyMenu      # [PartyMenu]
execute_action ReturnToWorldMap # [WorldMap]
```

## Active TODO top of queue (next-session priority)

1. **BattleVictory/BattleDesertion → BattlePaused bug** — relax
   unitSlotsPopulated requirement (quick code fix + tests)
2. **Cutscene/BattleDialogue → LoadGame bug** — add eventId guard to
   LoadGame rule (quick code fix + tests)
3. **BattleSequence discriminator** — heap diff scan on minimap vs
   WorldMap at Orbonne
4. **Per-shop stock for OutfitterBuy** — try heap search for armor/hat/
   accessory/consumable category lists near `0xEA01DC`
5. **BattleChoice state** — investigate memory during mid-battle
   objective choice screen
