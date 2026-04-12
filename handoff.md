# Session Handoff — 2026-04-12 (Session 5)

Delete this file after reading.

## What Happened This Session

17 commits. Biggest session ever. Eliminated C+Up scanning, built real-time battle tracking, unified the command interface, and battle-tested everything with live combat.

## Current State

**Branch:** `auto-play-with-claude`
**Tests:** 1667 passing (up from 1662)
**Game:** Loaded at The Siedge Weald (loc 26) in a random encounter. 1 enemy remaining (Skeleton, Undead, 157/693 HP). Party: Ramza(GK) 419/719, Kenrick(Archer) 496/496, Lloyd(Dragoon) 628/628, Wilham(Summoner) 49/373 [Critical].

## Major Changes

### 1. C+Up Scanning Eliminated (THE big win)
All unit data now read from the **static battle array** at `0x140893C00` (stride `0x200`). Grid positions at `+0x33/+0x34`, HP at `+0x14`, stats at `+0x22-0x25`, status at `+0x45`. Filter active units via `+0x12 == 1`. Zero keyboard input, ~15ms per scan vs 5-10 seconds before. See `BATTLE_MEMORY_MAP.md` section 8 for full field table.

### 2. Real-Time Battle Event Tracking
`BattleTracker` polls the static array every 100ms. Detects:
- HP changes with exact damage/heal amounts
- Unit movement (from→to coordinates)  
- Kills (HP drops to 0)
Events logged and stored for 30 seconds. Example: `[BattleTracker] 235 damage to (6,9): 693→458/693`

### 3. Unified `screen` Command
One command for everything. In battle shows:
- Active unit header with name, class, position, HP/MP
- Abilities with named target tiles: `Attack → (8,9)<Black Goblin> (7,10)<Treant>`
- All units with positions, HP, statuses, distance
- `screen -v` adds PA/MA/Spd/CT/Br/Fa and passive abilities per unit

Old commands deprecated: `scan_units`, `scan_move`, `state` all redirect to `screen`.

### 4. Battle Flow Streamlined
```
screen              → see everything, decide
battle_attack 7 11  → compact one-liner: "[Cutscene] ui=Abilities Attacked (7,11) from (8,10)"
execute_action Wait → auto-shows next turn's full screen
```
Three commands per turn. No JSON parsing, no stale cache errors.

### 5. Nine Bug Fixes
- Dead units filtered from attack ability targets (only revival abilities show them)
- Scan cache removed — battle_attack/ability/move auto-scan if needed
- False MISSED detection removed (was always wrong, BattleTracker handles it)
- Gun/bow/crossbow minimum range enforced (MinRange=3)
- Auto-cancel on failed attacks (EscapeToMyTurn instead of leaving game stuck)
- Screen header shows actual state `[Battle_MyTurn]` not `[Battle]`
- Active unit identification via IsActive flag (was picking first team=0)
- Active unit name populated in scan_move response
- Formatted battle action responses (compact one-liner instead of raw JSON)

## Battle Flow for Next Session

```bash
source ./fft.sh
screen              # see battlefield
battle_attack 9 8   # attack target (from screen's ability tiles)
execute_action Wait  # ends turn, auto-shows next screen
# repeat
```

Key commands:
- `screen` — universal state (battle view during Battle_MyTurn, basic info otherwise)
- `screen -v` — verbose (adds stats, passives per unit)
- `battle_attack <x> <y>` — basic attack
- `battle_ability "<name>" <x> <y>` — use ability on target
- `battle_ability "<name>"` — self-target ability (Focus, Shout)
- `battle_move <x> <y>` — move to tile
- `execute_action Wait` — end turn (auto-shows next screen)
- `execute_action Cancel` — back out of menus

## Next 4 Tasks (picked from testing)

1. **Cutscene false detection after attacks** — Ranged attacks show `[Cutscene]` in response instead of battle state. eventId=401 fires during attack animations and gets misdetected. Confusing output on every attack.

2. **Jump rotation detection fails** — Lloyd's Jump fails every time with "Could not detect rotation." Jump is his strongest ability (378 damage) but unusable. The rotation detection doesn't work for Jump targeting mode.

3. **Remove old dead heap scanning code** — `ScanHeapForPositions`, `RefreshPositionsFromKnownAddresses`, `ReadPositionFromHeap` in BattleTracker are 200+ lines of unused code since we read from the static array. Clean it out.

4. **Verify minimum range per weapon type** — Set bow/gun/crossbow MinRange=3 based on one observation. Need to test actual min range for each weapon type in-game. Bows might be 2, crossbows might be different.

## Known Bugs (from TODO.md)

- `battle_ability` selects wrong skillset for secondary abilities (Lloyd's Aurablast → Attack instead of Martial Arts). CacheLearnedAbilities fix deployed but not verified yet.
- `battle_attack` response shows `[Cutscene]` during attack animation (eventId=401 false detection)
- Line-of-sight blocking not detected (arrows blocked by trees)
- Gun minimum range shows targets too close (now filtered with MinRange=3)
- `execute_action` responses still show "ERROR: Friendly turn after Xms" (should be info, not error)

## Static Battle Array Field Map (Quick Reference)

Base: `0x140893C00`, stride `0x200`. Players at positive offsets, enemies at negative.
Filter active: `+0x12 == 1`.

| Offset | Field | Verified |
|--------|-------|----------|
| +0x0C | Exp (byte) | ✓ |
| +0x0D | Level (byte) | ✓ |
| +0x0E | origBrave (byte) | ✓ |
| +0x10 | origFaith (byte) | ✓ |
| +0x12 | inBattleFlag (u16, 1=active) | ✓ |
| +0x14 | HP (u16) | ✓ real-time |
| +0x16 | MaxHP (u16) | ✓ |
| +0x18 | MP (u16) | ✓ |
| +0x1A | MaxMP (u16) | ✓ |
| +0x22 | PA total (byte) | ✓ |
| +0x23 | MA total (byte) | ✓ |
| +0x24 | Speed (byte) | ✓ |
| +0x25 | CT (byte) | ✓ |
| +0x33 | Grid X (byte) | ✓ 10/10 |
| +0x34 | Grid Y (byte) | ✓ 10/10 |
| +0x45 | Status (5 bytes) | ✓ |

## Files Changed This Session

| File | Changes |
|------|---------|
| `NavigationActions.cs` | C+Up eliminated, static array scan, IsActive flag, dead unit filter, EscapeToMyTurn, MinRange, formatted output |
| `BattleTracker.cs` | Real-time HP/position polling, BattleEvent tracking, TrackedSlot struct |
| `CommandWatcher.cs` | Cache removal, auto-scan inline, CacheLearnedAbilities on scan |
| `AbilityTargetCalculator.cs` | IsRevivalAbility, MinRange filter in GetValidTargetTiles |
| `ActionAbilityLookup.cs` | MinRange field on ActionAbilityInfo record |
| `ItemData.cs` | BuildAttackAbilityInfo sets MinRange for gun/bow/crossbow |
| `CommandBridgeModels.cs` | ScannedUnitResponse, AbilityWithTiles models |
| `fft.sh` | Unified screen command, deprecated old commands, formatted action responses, auto-screen after Wait, execute_action timeout increase |
| `BATTLE_MEMORY_MAP.md` | Full static array field table |
| `TODO.md` | Cleaned (527→280 lines), added NEXT 5, logged 12 bugs from testing |
| `Tests/AbilityTargetCalculatorTests.cs` | 5 new IsRevivalAbility tests |
