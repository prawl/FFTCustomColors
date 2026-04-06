<!-- This file should not be longer than 200 lines, if so prune me. -->
# Battle Coordinate System & Camera Rotation

## Map Grid Cursor Position (THE KEY DISCOVERY)

Two addresses store the cursor's actual map grid position:

- **`0x140C64A54`** — Grid X (increments on Right, decrements on Left)
- **`0x140C6496C`** — Grid Y (increments on Up, decrements on Down)

These coordinates:
- Do NOT change with camera rotation (Q/E) — they are absolute map positions
- Arrow keys always map the same way: Right=+X, Left=-X, Up=+Y, Down=-Y
- When entering Move mode, cursor starts on Ramza's position
- Values are small integers (0-15 range, matching FFT map sizes)

### Verified Data Points
| Unit | Grid X | Grid Y | World Struct X | World Struct Y |
|------|--------|--------|---------------|----------------|
| Ramza | 0 | 3 | 7 (stale) | 11 (stale) |
| Enemy | 6 | 2 | 3 | 12 |

### Arrow Key → Grid Delta Mapping (ALL 4 ROTATIONS VERIFIED)

The grid coordinates are absolute (don't change with camera), but which arrow key
changes which axis DOES depend on camera rotation.

Tested by pressing Q while in Move mode to rotate without leaving, then testing
Right and Down at each rotation. All 4 verified in a single clean session.

| rot | Right    | Left     | Up       | Down     |
|-----|----------|----------|----------|----------|
| 0   | (0, +1)  | (0, -1)  | (-1, 0)  | (+1, 0)  |
| 1   | (+1, 0)  | (-1, 0)  | (0, +1)  | (0, -1)  |
| 2   | (0, -1)  | (0, +1)  | (+1, 0)  | (-1, 0)  |
| 3   | (-1, 0)  | (+1, 0)  | (0, -1)  | (0, +1)  |

Shorthand:
- rot=0: Right=+Y, Down=+X, Left=-Y, Up=-X
- rot=1: Right=+X, Down=-Y, Left=-X, Up=+Y
- rot=2: Right=-Y, Down=-X, Left=+Y, Up=+X
- rot=3: Right=-X, Down=+Y, Left=+X, Up=-Y

### Grid = Map Tile Coords (IDENTITY MAPPING — KEY DISCOVERY)

Grid cursor coordinates ARE map file tile coordinates directly. No offset or transform needed.

**Verified:** 5 grid positions matched MAP074 tile data exactly:
- grid(1,4) = MAP074 tile(1,4), height 2+1/2=2.5 ✓
- grid(3,4) = MAP074 tile(3,4), height 3+0/2=3.0 ✓
- grid(1,10) = MAP074 tile(1,10), height 3+1/2=3.5 ✓

**Height formula:** `display = tile.height + tile.slope_height / 2`

**Previous incorrect theory:** Grid = world coords with fixed offset. This was wrong — grid coords map directly to map file indices with no transform.

**Arrow keys move single-axis in grid space** (not diagonally).
The earlier note about diagonal movement was incorrect.

### Navigation Algorithm
1. Enter Move mode — cursor starts on Ramza
2. Read camera rotation from `0x14077C970 % 4`
3. Read Ramza's grid position from `0x140C64A54` (X) and `0x140C6496C` (Y)
4. Get enemy world position from battle state (struct)
5. Compute offset from a known unit, then enemy grid position
6. Navigate cursor toward enemy, checking `battleTeam` after each press
7. When team != 0 (on enemy), back up 1 press
8. Confirm with F

## Coordinate Systems

### Condensed Struct (Cursor-Selected Unit Data)

The struct at `0x14077D2A0` shows the **unit under the cursor**, NOT the active unit.

- X at `0x14077D360` (uint16 LE), Y at `0x14077D362` (uint16 LE)
- Updates when cursor hovers over ANY unit (friend or enemy)
- When cursor is on an empty tile, it keeps showing the LAST unit's data
- Position does NOT update after a unit moves — stays at pre-move position
- `battleTeam` at `0x14077D2A2` = team of cursor-highlighted unit, not whose turn it is

### Tile List (Movement Outline Path — NOT All Valid Tiles)

Address `0x140C66315`, 7 bytes per entry: `[X] [Y] [elevation] [flag] [0] [0] [0]`

- **This is a cursor traversal path tracing the PERIMETER of the movement diamond, NOT all valid tiles**
- Contains ~15 entries with only ~8 unique world coordinates (the diamond boundary)
- Multiple groups separated by 7-byte zero terminators (different units' paths)
- Data is transient (Move mode only) and animated (shifts between reads)
- Elevation byte = tile height in half-units (raw / 2 = display height)
- Uses the SAME world coordinate system as the condensed struct
- `flag=0` with `X=0, Y=0` = group terminator

**For ALL valid movement tiles, use the BFS computation in CommandWatcher.PopulateBattleTileData.**
See BATTLE_MEMORY_MAP.md section 15 for details.

### Camera Rotation

Address: `0x14077C970` (byte), incrementing counter. `value % 4` = rotation 0-3.

- Q and E BOTH increment by 1 (both rotate the same direction)
- The counter does NOT wrap — it just keeps incrementing
- The game may AUTO-ROTATE the camera when entering Move mode, adding extra increments
- Always read rotation AFTER entering Move mode, not before
- Added to screen response as `cameraRotation` (0-3) — requires build+deploy

## Working Approach: Scan-and-Move (PROVEN)

Instead of computing directions from the rotation table, scan for the enemy by pressing
arrow keys and checking when `battleTeam` (0x14077D2A2) changes to non-zero.

### Algorithm (tested and working twice)

```
1. Enter Move mode (path Move)
2. For each direction [Right, Left, Up, Down]:
   a. Press that direction up to 10 times
   b. After each press, read battleTeam at 0x14077D2A2
   c. If team != 0 → enemy found at distance N in this direction
   d. Cancel and try next direction if not found
3. Once enemy found at direction D, distance N:
   a. Enter Move mode again
   b. Press D × (N-1) times to land BESIDE enemy
   c. Confirm move (path ConfirmMove)
```

### Why this works
- The cursor moves on the full map grid, not just valid movement tiles
- When the cursor crosses a unit, the struct updates with that unit's team/stats
- When on empty tiles, the struct keeps showing the previous unit (Ramza)
- So `team != 0` is a reliable signal that we found the enemy

### Limitations
- Only scans along cardinal screen directions — won't find enemies at a diagonal
- For diagonal enemies, would need a 2D sweep (scan Right, then at each Right position scan Up/Down)
- Only finds the first enemy in each direction
- The struct doesn't update for dead units (HP=0), so dead enemies are invisible

### Example output
```
Camera rot: 0
Enemy found at Right x2, pos=(3,12)
Moving Right x1 to get beside enemy...
After move: team=0 (0=beside, not on enemy)
[Battle_MyTurn] — confirmed!
```

## Live Unit Positions

**The game does NOT store live positions in a persistent per-unit array.** Grid cursor (0x140C64A54/0x140C6496C) is the only live position source, and only tracks what the cursor is on.

**IMPORTANT: Entering Move mode RESETS a previous move.** Only enter Move once per turn.

## C+Up Unit Cycling (THE BREAKTHROUGH)

Holding the C key and pressing Up/Down cycles the cursor through all units in turn order.
This gives us **live grid positions for every unit** without scanning or coordinate transforms.

**How to use:**
1. Send C key DOWN (hold)
2. Press Up repeatedly — cursor snaps to each unit in turn order
3. After each press, read:
   - Grid position: `0x140C64A54` (X), `0x140C6496C` (Y)
   - Team: `0x14077D2A2` (0=friendly, 1+=enemy)
   - World position: `0x14077D360` (X), `0x14077D362` (Y)
4. Send C key UP (release)
5. Now we have every unit's live grid position

**Verified data:**
| Unit | Grid | Team | World |
|------|------|------|-------|
| Ramza | (0,1) | 0 | (7,11) |
| Chocobo | (0,2) | 1 | (6,13) |
| Far enemy | (5,2) | 1 | (3,10) |

## Key Corrections

- **`battleTeam` (0x14077D2A2)** = team of cursor-hovered unit, NOT whose turn it is
- **Menu cursor** = `0x1407FC620` (not 0x1407FCCA8)
- **Struct positions are stale after movement** — X/Y at 0x14077D360/62 = last turn start position
- **Arrow keys move cursor on the full map grid**, not just valid movement tiles
