# Battle Memory Map (IC Remaster)

## Status: Work In Progress
Last updated during Mt. Germinas random battle, 2026-04-02.

## Key Discoveries

### Formation Screen
- **Space bar** opens "Commence Battle?" confirmation
- **Right arrow** moves unit placement cursor
- **Enter** places a unit on selected tile
- Battle stat arrays (HP/MP) are NOT populated until battle starts
- Roster data (0x1411A18D0) remains accessible and valid throughout

### Data Sources During Battle

#### 1. Roster Array (0x1411A18D0, stride 0x258)
Still readable during battle. Contains persistent unit data:
| Offset | Size | Field | Ramza |
|--------|------|-------|-------|
| +0x00 | byte | spriteSet | 3 |
| +0x01 | byte | unitIndex | 0 |
| +0x02 | byte | job | 3 (Knight) |
| +0x07 | byte | secondary ability | 0 |
| +0x08 | byte | reaction ability | 178 (Bonecrusher) |
| +0x0A | byte | support ability | 213 (Concentration) |
| +0x0C | byte | movement ability | 232 (Move+3) |
| +0x1C | byte | exp | 99 |
| +0x1D | byte | level | 99 |
| +0x1E | byte | brave | 94 |
| +0x1F | byte | faith | 75 |
| +0x230 | uint16 | nameId | 1 |

#### 2. Condensed Battle Struct (0x14077D2A0, variable stride)
Contains real-time battle stats. All values are uint16 LE unless noted.
Each unit entry is followed by a variable-length ability list (uint16 IDs, terminated by 0xFFFF).
**Stride is NOT fixed** — varies per unit based on ability count.

| Offset | Size | Field | Ramza | Enemy(Lv95) | Notes |
|--------|------|-------|-------|-------------|-------|
| +0x00 | uint16 | level | 99 | 95 | Confirmed ✓ |
| +0x02 | uint16 | ??? | 0 | 1 | |
| +0x04 | uint16 | nameId | 1 | 3 | Confirmed ✓ |
| +0x06 | uint16 | ??? | 7 | 7 | Same for both units |
| +0x08 | uint16 | exp | 99 | 49 | Confirmed ✓ |
| +0x0A | uint16 | ??? | 16 | 0 | CT? (16 for Ramza, 0 for enemy) |
| +0x0C | uint16 | HP | 719 | 629 | Confirmed ✓ |
| +0x0E | uint16 | (padding) | 0 | 0 | |
| +0x10 | uint16 | maxHP | 719 | 629 | Confirmed ✓ |
| +0x12 | uint16 | MP | 138 | 17 | Confirmed ✓ |
| +0x14 | uint16 | (padding) | 0 | 0 | |
| +0x16 | uint16 | maxMP | 138 | 17 | Confirmed ✓ |
| +0x18 | uint16 | ??? | 100 | 40 | PA? (differs between units) |
| +0x1A | uint16 | ??? | 64 | 34 | MA? (differs between units) |
| +0x1C | uint16 | ??? | 100 | 100 | Same — constant? |
| +0x1E | uint16 | ??? | 250 | 250 | Same — constant? |
| +0x20 | uint16 | ??? | 100 | 100 | Same — constant? |
| +0x22 | uint16 | (zero) | 0 | 0 | |
| +0x24 | uint16 | (zero) | 0 | 0 | |
| +0x26 | uint16 | (zero) | 0 | 0 | |
| +0x28+ | uint16[] | ability list | ... | ... | FFFF-terminated |

#### 3. Battle HP Array (0x141024EC8, uint32 x 21)
Parallel array of current HP for battlefield units (populated only during active battle):
```
Index 0: 118 (0x76)
Index 1: 485 (0x1E5)
Index 2: 488 (0x1E8)
Index 3: 486 (0x1E6)
Index 4: 487 (0x1E7)
Index 5-20: 0
```
Note: These values don't match the condensed struct HP values — indexing differs.

#### 4. Battle MP Array (0x14102223C, uint32 x ~21)
```
Index 0: 207 (0xCF)
Index 1: 210 (0xD2)
Index 2: 208 (0xD0)
Index 3: 209 (0xD1)
Index 4+: 0
```

#### 5. Max HP Array (0x14108E4E8)
Returned all 0xFF during this session — may need recalculation or different base.

#### 6. Unit Existence Slots (0x14077CA30, uint32 x 10)
9 entries of 0xFF (255) = active units, then 0xFFFFFFFF terminator.
**9 total units in battle** (likely 5 player + 4 enemy).

#### 7. Active-Unit / Turn Region (0x14077CA60)
This region contains data about the current turn state. Values shift when turns change.
| Address | Ramza's Turn | After Ramza | Notes |
|---------|-------------|-------------|-------|
| +0x60 | 16 | 16 | CT of acting unit? |
| +0x64 | 7 | 0 | Changes on turn change |
| +0x68 | 0 | 3 | |
| +0x6C | 3 (Job) | 10 | Job of acting unit? |
| +0x80 | 3 | 3 | |
| +0x8C | 0 | 1 | **Flag: went 0→1 on turn end** (Action Taken?) |
| +0x94 | 401 | 401 | Unit/Name ID? |
| +0x9C | 0 | 1 | **Flag: went 0→1 on turn end** (Movement Taken?) |

#### 8. Battle Navigation & Menu Cursor
- **Space bar** starts battle from formation screen
- **Action menu cursor**: `0x1407FC620` (byte, 0-4)
  - 0: Move
  - 1: Abilities (submenu: Attack, Mettle/secondary)
  - 2: Wait
  - 3: Status
  - 4: Auto-Battle
- Menu cursor resets to 0 (Move) when a new turn starts
- **Wait sequence**: Navigate cursor to 2, Enter (select), Enter (confirm facing)
- **Turn detection**: Poll `0x14077D2A2` (Team), `0x14077CA8C` (Act), `0x14077CA9C` (Move)
  - Team=0 AND Act=0 AND Mov=0 → friendly unit's turn, action menu is open

#### Proven Auto-Wait Loop (tested 5 consecutive turns):
```
1. Poll until Team=0, Act=0, Mov=0 (friendly turn detected)
2. Read cursor at 0x1407FC620
3. Press Down until cursor == 2 (Wait)
4. Enter (select Wait)
5. Sleep 1.5s
6. Enter (confirm facing)
7. Repeat from step 1
```

#### 9. Movement Tile List (0x140C66315, 7 bytes per entry)
When in Move mode, this contains all valid movement tiles (the blue overlay):
| Offset | Size | Field |
|--------|------|-------|
| +0x00 | byte | X coordinate (tile grid, typically 0-15) |
| +0x01 | byte | Y coordinate (tile grid, typically 0-15) |
| +0x02 | byte | Elevation/height? (varies, 14-24 range observed) |
| +0x03 | byte | Always 1 |
| +0x04-0x06 | 3 bytes | Always 0 |

Stride = 7 bytes per tile entry. Tiles appear to be listed in some order (not row/column sorted).

Example entries during Move mode:
```
X=6  Y=13  elev=16
X=10 Y=14  elev=24
X=11 Y=9   elev=16
X=7  Y=15  elev=20
```

#### 10. Cursor Tile Index (0x140C64E7C, byte)
Index into the movement tile list. Increments as cursor moves between tiles (0→1→2...).
Can be used with the tile list to determine which tile the cursor is hovering on.

#### 11. Turn Order Queue Discovery
The condensed struct at 0x14077D2A0 is a **rolling turn-order queue**, NOT a fixed per-unit array.
- Slot 0 = current/next acting unit
- Slot 1 = unit after that
- When a turn ends, the queue rotates: new units appear, old ones shift out
- The queue UPDATES IN REAL TIME: Ramza's HP changed from 719→521 after taking damage

#### 12. UI Buffer Extended (0x1407AC7C0, uint16 values)
Full layout of the UI stat display buffer (cursor-based, shows selected unit):
| Index | Offset | Field | Ramza Value |
|-------|--------|-------|-------------|
| 0 | +0x00 | Level | 99 |
| 2 | +0x04 | NameId | 1 |
| 3 | +0x06 | ??? | 7 |
| 4 | +0x08 | Exp | 99 |
| 5 | +0x0A | CT | 16 |
| 6 | +0x0C | HP (stale) | 719 |
| 8 | +0x10 | MaxHP | 719 |
| 9 | +0x12 | MP | 138 |
| 11 | +0x16 | MaxMP | 138 |
| 12 | +0x18 | PA? | 100 |
| 18 | +0x24 | **Move** | 4 |
| 19 | +0x26 | **Jump** | 4 |
| 21 | +0x2A | **Job** | 3 |
| 22 | +0x2C | **Brave** | 94 |
| 23 | +0x2E | **Faith** | 75 |

#### 13. Team Allegiance (Condensed Struct +0x02)
- **0 = friendly** (confirmed: Ramza Id=1 has Team=0)
- **1 = enemy** (confirmed: enemy Id=2 has Team=1)

#### 14. Unit Position

**Live Position via Condensed Turn Queue (CONFIRMED WORKING):**
- **X: `0x14077D360`** (uint16 LE) — current unit's tile X coordinate
- **Y: `0x14077D362`** (uint16 LE) — current unit's tile Y coordinate
- These update when ANY unit (friend or enemy) rotates to slot 0 of the turn queue
- Background polling at 100ms catches each unit's position as they take their turn
- After one full round of turns, all units have live positions
- Verified across multiple battles with 6+ units, all unique positions

**Starting Position via Heap Battle Struct:**
- Found via `search_bytes` for stat pattern `exp level origBrave brave origFaith faith`
- Heap struct has starting X at +0x1A and starting Y at +0x23 from stat base
- These are formation/initial positions — do NOT update when units move
- Located in heap memory (addresses like 0x416...), change per session
- Active and saved copies exist at stride 0x800

**Movement Tile List (only in Move mode):**
- Entry at index 0 (`0x140C66315`) is the unit's current position (7 bytes: X Y elevation flag 0 0 0)
- Only populated when the active unit's Move mode is open
- Cursor index at `0x140C64E7C` starts at 0 (the unit's own tile)

**BattleTracker (C# class) combines all three:**
1. Background polls condensed struct every 100ms → catches live positions for all units as they take turns
2. Heap scan every 3s → finds starting positions from formation data
3. Tile[0] read → available for active friendly unit during Move mode

## Answers to Critical Battle Automation Questions

| Question | Answer | How to Read |
|----------|--------|-------------|
| How many enemies? | Count units with Team=1 in turn queue | Condensed struct +0x02 for each unit |
| How many friendlies? | Count units with Team=0 | Condensed struct +0x02 |
| Unit coordinates? | Condensed struct position (live, all units) | 0x14077D360 (X), 0x14077D362 (Y) — updates per turn via background poll |
| Whose turn? | Turn queue slot 0 | 0x14077D2A0 (Level, Id, HP, etc.) |
| Who's next? | Turn queue slot 1 | 0x14077D2E0 |
| Is unit alive? | HP > 0 | Condensed struct +0x0C |
| Where can unit move? | Movement tile list | 0x140C66315, stride 7, flag=0 terminates |
| Which tile hovering? | Cursor index into tile list | 0x140C64E7C (byte index) |
| Total units in battle | Existence slots (0xFF entries) | 0x14077CA30, uint32 per slot, FFFF terminates |

## Screen Detection (Code in CommandWatcher.cs DetectScreen method)

### Confirmed Reliable Flags
| Address | Flag | Description |
|---------|------|-------------|
| 0x140D3A41E | partyFlag | 1 = Party Menu, 0 = not |
| 0x140D4A264 | uiFlag | 1 = UI overlay present (travel list, party menu, battle), 0 = clean world map |
| 0x14077CA30 | unitSlot0 | 255 = in battle (unit exists), other = not in battle |
| 0x14077CA54 | unitSlot9 | 0xFFFFFFFF = battle terminator |
| 0x14077D208 | location | 255 = title screen or battle, 0-42 = world map location |
| 0x140900824/828 | encA/encB | Different values = encounter dialog active |

### Screen Detection Logic
```
1. inBattle = (slot0==255 AND slot9==0xFFFFFFFF)
2. if inBattle AND team==0 AND act==0 AND mov==0 → Battle_MyTurn
3. if inBattle → Battle
4. if location==255 → TitleScreen
5. if encA != encB → EncounterDialog
6. if partyFlag==1 → PartyMenu
7. if uiFlag==1 → TravelList
8. else → WorldMap
```

### Known State Detection Problems
1. **FIXED: Battle detection required location>=255** — removed location check. Battle now detected correctly via unitSlot0==255 && unitSlot9==0xFFFFFFFF alone.
2. **FIXED: Turn detection** — battleTeam at 0x14077D2A2 reliably shows 0=friendly turn, 1=enemy turn. Previous false positive was caused by the location>=255 check preventing battle detection entirely on some maps.
3. **Game Over not detected**: Game over screen reads as [Battle]. Need a game-over indicator address.
4. **Settlement/Shop not reliably detected**: 0x140D45561 was tried but stays stale after leaving shop. Removed for now.
5. **Menu cursor (0x1407FC620)**: Works correctly in the action menu but resets/changes meaning inside submenus.

## Action Menu Layout
```
0: Move
1: Abilities (submenu: Attack, Mettle/secondary)
2: Wait
3: Status
4: Auto-Battle
```
After acting, Abilities is grayed but still selectable (cursor passes through it).
Menu cursor at 0x1407FC620 tracks position 0-4 correctly in main action menu.

## Attack Sequence (Proven)
```
1. Down to menu=1 (Abilities), Enter
2. Enter (select Attack - first item in submenu)
3. Direction key to target tile with enemy
4. Enter (select target)
5. Enter (confirm attack)
6. Navigate to Wait (menu=2), Enter
7. Enter (confirm facing)
```
Note: Need TWO Enters to confirm attack (select target + confirm).
Note: Must target a tile with an enemy — attacking empty tiles wastes the turn.

#### 15. Movement Tile Validity (Map-Based BFS — EXACT)

Valid movement tiles are computed via BFS using pre-parsed JSON map data (fft-map-json repo).

**Map-Based BFS (primary, 100% accurate):**
- Load map JSON via `set_map <mapNumber>` (e.g. `set_map 74` for MAP074)
- Map files: `claude_bridge/maps/MAP###.json` (copied from fft-map-json repo)
- Grid coordinates = map tile coordinates (identity mapping, no transform)
- Height formula: `display = height + slope_height / 2`
- Jump check: `|display_height_A - display_height_B| ≤ jumpStat`
- All terrain costs 1 movement point (no swamp penalty)
- `no_walk` flag = impassable (trees, walls, obstacles)
- Enemy-occupied tiles block movement (can't move through)
- Starting tile excluded from results
- **Verified 6/6 tile counts match in-game exactly**

**`scan_move` action (recommended workflow):**
1. Scans all units via C+Up cycling (gets positions + teams)
2. Computes valid tiles using map BFS + enemy blocking
3. Returns `ValidMoveTiles` in response with tile coordinates
4. Usage: `scan_move <move> <jump>` (e.g. `scan_move 7 3`)

**Known issue:** Move/Jump stats at `0x1407AC7E4`/`0x1407AC7E6` show base values (before equipment modifiers). Must pass correct values as parameters.

**Fallback BFS (if no map loaded):**
- Terrain grid: `0x140C65000` (72 entries × 7 bytes, 8×9 grid)
- Height formula: `min(b0, b1)` — rendering approximation, NOT exact
- ~12 false positives (trees, obstacles, approximate heights)
- Self-correcting via `mark_blocked X Y` cache

**Response:** `screen.Tiles` contains grid (X,Y) pairs during `Battle_Moving`.

#### 16. Tile Height Display

| Address | What | Encoding |
|---|---|---|
| `0x140C6492C` | Current cursor tile Height × 10 | uint32, e.g. 25 = display 2.5 |
| `0x14077CA5C` | Move mode flag | 0xFF = Move mode, 0x00 = not |

**Height encodings in the IC remaster:**
- Display value: `0x140C6492C / 10` = "Height X.X" shown on screen
- Tile list elevation byte (offset +2 in 7-byte entry): `raw / 2` = display height
- Rendering vertex Z float: `tile_h * 15 + 124` (tile_h in half-units)
- Terrain grid b0/b1: rendering approximation, does NOT match actual game heights

#### 17. What Was Investigated for Tile Validity (40+ agents, 4 waves)

| Approach | Result |
|---|---|
| Rendering struct diff (0x140C6F400, stride 0x88, flag +0x1D) | Exact count (19) in first session but struct layout changes per restart, counts vary 12-19 |
| Tile outline path (0x140C66315) | Only 8 perimeter points, not all interior tiles |
| Terrain grid flags (0x140C65000) | No valid/invalid distinction in any byte |
| Heap stride-32 grid (0x142FEA99C) | Address changes per session, volatile |
| Passability bitmap | Not found anywhere in module or heap |
| Float height array | Heights not stored as standalone float array |

#### 18. Map File Data (fft-map-json)

Pre-parsed terrain data from PSX GNS/mesh files. 122 maps available.

**Source:** `c:/Users/ptyRa/Dev/fft-map-json/data/MAP###.json`
**Deploy to:** `claude_bridge/maps/MAP###.json` (BuildLinked.ps1 wipes this — must re-copy)

**JSON structure per tile:**
```json
{
  "x": 0, "y": 0,
  "height": 2, "slope_height": 1,
  "depth": 0, "no_walk": false, "no_cursor": false,
  "surface_type": "Grassland", "slope_type": "Flat 0"
}
```

**Key formulas:**
- Display height: `height + slope_height / 2`
- Grid coords = map tile coords (identity mapping)
- Maps have `lower` and `upper` levels (lower is primary)
- `starting_locations` array has team spawn positions

**Map identification:**
- Story battles and random encounters at the same world map location use DIFFERENT maps
- Terrain fingerprinting (dimensions + 5 tile heights) uniquely identifies all 122 maps
- Location 26 (Siedge Weald) random encounter = MAP074 (11×12, grassland/swamp/bridge)
- Full location→map table not yet built; currently must specify map number manually

## Still Unmapped
- **Game over detection** — no known flag
- **Facing direction** — not found (PSX at +0x49)
- **Current statuses** — PSX at +0x58 to +0x5C, not yet located
- **Travel list tab identification** — no reliable memory address found, use hover ID range instead
- **Effective Move/Jump stats** — UI buffer shows base values, not post-equipment effective values

## PSX Reference
See BATTLE_STATS_PSX_REFERENCE.md for the complete PSX field layout.
The remaster rearranges and expands fields (bytes → uint16) but preserves the same data.

## Formation Screen Navigation
1. Arrow keys move placement cursor on blue tiles
2. Enter places a unit on selected tile
3. "Battle Member Remaining: N" shows unplaced units
4. **Space bar** → "Commence Battle?" confirmation
5. Enter confirms to start battle
