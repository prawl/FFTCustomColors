# Session Handoff — 2026-04-12 (Session 4)

Delete this file after reading.

## What Happened This Session

9 commits, 1662 tests passing (up from 1649). Major features: enemy passive ability reverse engineering, C+Up scan reliability improvements, documentation updates, screen state additions, and extensive bug bash.

## Current State

**Branch:** `auto-play-with-claude`
**Tests:** 1662 passing
**Game:** Loaded at The Siedge Weald (loc 26) in a random encounter. Ramza/Kenrick/Lloyd/Wilham vs 6 enemies. One Gobbledygook dead (killed by Lloyd's Jump). C+Up scan working after restart + retry mechanism added. Map auto-detects as MAP074.

## Priority for Next Session: Eliminate C+Up Scanning

### Why

C+Up scanning is the biggest reliability problem. It requires holding the C key (via SendInput) while pressing Up to cycle through units in the Combat Timeline. This:
- Fails intermittently after game restarts (C key doesn't register)
- Disrupts the camera/cursor visually
- Takes 5-10 seconds per scan (500ms per unit × 10 units)
- Can corrupt game state if done during animations

### What C+Up Provides

Only ONE thing that can't be gotten from memory alone: **grid positions for non-active units**.

Everything else already has non-C+Up sources:
- Active unit position → tile list at `0x140C66315` entry[0], or condensed struct
- Active unit stats → condensed struct at `0x14077D2A0`
- Active unit abilities → condensed struct +0x28 (FFFF-terminated list)
- All unit stats (HP, MP, level, CT, team) → `ScanTurnQueue` walks condensed struct
- Class fingerprints → heap search by HP pattern, +0x69
- Passive abilities → heap struct bitfields +0x74/+0x78
- Roster matching → static battle array by HP

### The Problem with Grid Positions

The game uses TWO coordinate systems:
- **Grid coords**: Used by the map JSON, BFS pathfinding, cursor navigation, and in-game display. Read from `0x140C64A54` (X) / `0x140C6496C` (Y) — the cursor grid position, updated only when C+Up snaps to a unit.
- **World coords**: Used internally by the engine. Stored in heap struct at +0x23 (X) and +0x1A (Y), and in condensed struct at `0x14077D360`/`0x14077D362`. Does NOT match grid coords.

Verified data points (world → grid):

| Unit | World X (+0x23) | World Y (+0x1A) | Grid X | Grid Y |
|------|-----------------|-----------------|--------|--------|
| Gobbledygook | 3 | 13 | 5 | 11 |
| Skeleton | 4 | 12 | 3 | 6 |
| Treant | 3 | 9 | 2 | 10 |
| Lloyd | 4 | 11 | 8 | 10 |

No simple linear formula connects them. The relationship is likely tile-specific (isometric projection).

### Proposed Solution: Build a World→Grid Lookup Table

**The user's insight:** The map JSON has all tiles with grid coordinates. If we can find the world coordinate for each map tile, we can build a bidirectional lookup table.

**Approach A — One-time C+Up calibration:**
1. On the FIRST scan of a battle, do C+Up normally to get grid positions for all units
2. Match each unit to its heap struct by HP pattern
3. Read the world coords from the heap struct (+0x23, +0x1A)
4. Build a world→grid mapping table for all occupied tiles
5. For ALL SUBSEQUENT scans in the same battle, skip C+Up entirely:
   - Walk the condensed struct turn queue for stats
   - Read world positions from heap structs
   - Convert world→grid using the mapping table
   - Only re-calibrate if a unit moves to a tile not in the table

**Approach B — Map-based world→grid table:**
1. Find a way to compute world coords for EVERY tile on the map, not just occupied ones
2. Pre-compute the full world→grid lookup from the map JSON
3. Never need C+Up at all

Approach A is practical now. Approach B requires understanding the world↔grid formula (may be derivable from the map's height/slope data and isometric projection math).

### Approach A Implementation Plan

1. Add `WorldToGridMap` dictionary to the scan cache
2. On first scan: C+Up as normal, but also read heap world coords for each unit
3. Store `{(worldX, worldY) → (gridX, gridY)}` entries
4. On subsequent scans: walk turn queue + heap structs, lookup grid positions from the map
5. Fall back to C+Up only if a position isn't in the map (unit moved to unknown tile)

## What We Built This Session

### Enemy Passive Ability Reading (major feature)
- Reverse-engineered IC remaster heap struct bitfields for equipped passives:
  - **Reaction:** 4 bytes at heap struct +0x74, base ID 166, MSB-first
  - **Support:** 5 bytes at heap struct +0x78, base ID 198, MSB-first
  - **Movement:** 3 bytes at +0x7D, base TBD
- Player units read from roster (+0x08/+0x0A/+0x0C byte IDs)
- `PassiveAbilityDecoder.cs` with 11 unit tests
- Shell renders: `equip: R:Parry | S:Equip Swords | M:Movement +3`
- Verified: Knight(Parry ✓, Equip Swords ✓), Archer(Gil Snapper ✓, Evasive Stance ✓)

### Documentation Updates
- `BattleTurns.md` — Full `battle_ability` section with all 6 targeting types
- `Commands.md` — Added `battle_ability` to command table
- `BATTLE_MEMORY_MAP.md` — Passive Ability Bitfields section, world coord offsets

### Screen States
- `Battle_Dialogue` — Mid-battle character dialogue (needs live calibration)
- `Battle_Formation` — validPaths (place units, commence)
- `Battle_Abilities` / `Battle_<Skillset>` — validPaths for submenu/ability list navigation

### Bug Fixes
- JS crash on AoE abilities with no validTargetTiles (Ultima out of range)
- Stale active unit name in `screen` — HP comparison suppresses wrong names
- `battle_ability` timeout increased to 15s
- C+Up scan retry when only 1 unit detected (longer hold, foreground focus)

### Bugs Found During Bug Bash

1. **C+Up scan fails after restart** — Sometimes finds only 1 unit. Retry mechanism added but root cause (input delivery) not fixed. Best fix: eliminate C+Up.
2. **Location address stale after restart** — `0x14077D208` reads 255. Fixed by writing `last_location.txt` manually, but should auto-persist.
3. **Jump ends turn immediately** — No Wait/facing step needed after Jump. Claude tries to `battle_wait` and fails.
4. **False MISS detection** — Gun attack reports "MISSED" when HP check reads stale condensed struct data.
5. **eventId=401 mid-battle** — Fires after battle actions, detected as `Cutscene` instead of `Battle_Dialogue`.
6. **Map auto-detection uses stale location** — Random encounter map lookup uses loc=2 (stale) instead of loc=26 (correct). Both happen to map to MAP074 by coincidence.

### Coordinate System Discovery

- **Grid cursor** (`0x140C64A54`, `0x140C6496C`) — Map-compatible coordinates. Only updates during C+Up cycling or when entering Move mode.
- **Condensed struct world pos** (`0x14077D360`, `0x14077D362`) — Different coordinate system. NOT map-compatible. Max Y exceeds map bounds.
- **Heap struct world pos** (+0x23 = X, +0x1A = Y from struct base) — Same as condensed struct world coords. NOT map-compatible.
- **Tile list** (`0x140C66315`) — Entry[0] = active unit grid position. Only valid during Battle_MyTurn with tile list populated. Can be stale.
- **Map JSON tiles** — Use grid coordinates (match grid cursor).

## Files Changed This Session

| File | Changes |
|------|---------|
| `ColorMod/GameBridge/PassiveAbilityDecoder.cs` | NEW — Decodes reaction/support bitfields |
| `ColorMod/GameBridge/NavigationActions.cs` | Read passives, C+Up retry, passive fields on ScannedUnit |
| `ColorMod/GameBridge/BattleTracker.cs` | Updated passive ability field comments |
| `ColorMod/GameBridge/ScreenDetectionLogic.cs` | Added Battle_Dialogue detection |
| `ColorMod/GameBridge/NavigationPaths.cs` | Paths for Dialogue, Formation, Abilities, Skillsets |
| `ColorMod/Utilities/CommandWatcher.cs` | HP comparison for stale active unit |
| `Tests/GameBridge/PassiveAbilityDecoderTests.cs` | NEW — 11 tests |
| `Tests/GameBridge/ScreenDetectionTests.cs` | Battle_Dialogue tests |
| `Tests/GameBridge/CutsceneDetectionTests.cs` | Updated for Battle_Dialogue |
| `FFTHandsFree/BATTLE_MEMORY_MAP.md` | Passive Ability Bitfields, world coord notes |
| `FFTHandsFree/Instructions/BattleTurns.md` | battle_ability docs |
| `FFTHandsFree/Instructions/Commands.md` | battle_ability in command table |
| `FFTHandsFree/TODO.md` | Multiple items done, bugs logged |
| `fft.sh` | AoE JS fix, battle_ability timeout |

## Key Memory Addresses Discovered

| Address/Offset | What | Notes |
|---|---|---|
| Heap +0x74 | Reaction ability bitfield (4 bytes) | Base ID 166, MSB-first |
| Heap +0x78 | Support ability bitfield (5 bytes) | Base ID 198, MSB-first |
| Heap +0x7D | Movement ability bitfield (3 bytes) | Base TBD |
| Heap +0x23 | World X coordinate (byte) | NOT grid coords |
| Heap +0x1A | World Y coordinate (byte) | NOT grid coords |
| `0x14077D360/362` | Condensed struct world X/Y | Same as heap world coords |
