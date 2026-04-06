<!-- This file should not be longer than 200 lines, if so prune me. -->
# Battle Movement

How to move units around the battlefield accurately using the bridge system.

## Prerequisites

Before you can move, the system needs to know three things:
1. **Which map you're on** — determined automatically from your world map location
2. **Where all units are** — scanned via C+Up unit cycling
3. **Which tiles are reachable** — computed via BFS using map terrain data

All three are handled by a single command: `scan_move`.

## Step-by-Step: Moving a Unit

### 1. Scan the battlefield

```bash
scan_move           # uses the unit's actual Move/Jump stats
scan_move 7 3       # override with Move=7, Jump=3 (if base stats are wrong)
```

This returns:
- **All unit positions** (allies and enemies) with team, HP, level
- **The loaded map** (e.g. MAP074) and how it was identified
- **Valid movement tiles** — every tile the active unit can reach
- **Camera rotation** — needed for arrow key direction mapping

### 2. Pick a target tile

Choose a tile from the `ValidMoveTiles` list in the response. The tiles are grid coordinates like `6,5`.

**Tactical considerations:**
- Move adjacent to an enemy if you plan to attack (1 tile away)
- Don't move onto an enemy's tile — you can't
- Consider elevation and terrain for positioning
- Think about which enemies can reach you after you move

### 3. Execute the move

```bash
move_grid 6 5       # move cursor to grid position (6,5) and confirm
```

The mod handles everything internally:
1. Opens Move mode from the action menu
2. Reads the camera rotation
3. Computes the arrow key sequence from current position to target
4. Presses the arrows one at a time
5. Confirms the move

You'll get back a confirmation like `(1,4)->(6,5) CONFIRMED`.

### 4. Act or Wait

After moving, the action menu reappears. You can:
- **Attack**: select Abilities -> Attack -> target an adjacent enemy
- **Wait**: `battle_wait` (handles menu nav + facing confirmation)

## How Map Detection Works

The system identifies the battle map automatically:

1. **While on the world map**, the mod saves your current location ID (e.g. 26 = The Siedge Weald)
2. **When battle starts**, it looks up the location in `random_encounter_maps.json` (e.g. location 26 -> MAP074)
3. **MAP074.json** is loaded — contains every tile's height, walkability, and terrain type
4. **BFS runs** from the active unit's position using Move/Jump stats and the map data

The location is only saved when you're actually stopped on the world map or at an encounter dialog — not during travel animation (which flickers through intermediate nodes).

If the automatic lookup fails, fingerprint detection compares unit positions against all 122 maps as a fallback.

## How Grid Navigation Works

The cursor moves on an absolute grid that doesn't change with camera rotation. But which **arrow key** moves which **grid direction** depends on rotation:

| rot | Right    | Left     | Up       | Down     |
|-----|----------|----------|----------|----------|
| 0   | (0, +1)  | (0, -1)  | (-1, 0)  | (+1, 0)  |
| 1   | (+1, 0)  | (-1, 0)  | (0, +1)  | (0, -1)  |
| 2   | (0, -1)  | (0, +1)  | (+1, 0)  | (-1, 0)  |
| 3   | (-1, 0)  | (+1, 0)  | (0, -1)  | (0, +1)  |

The mod reads rotation from `0x14077C970 % 4` and translates the grid delta into the correct arrow keys automatically. You just say `move_grid x y`.

## Key Addresses

| Address | What |
|---------|------|
| 0x140C64A54 | Grid cursor X (absolute) |
| 0x140C6496C | Grid cursor Y (absolute) |
| 0x14077C970 | Camera rotation (value % 4) |
| 0x14077D2A2 | Team of cursor-hovered unit (0=ally, 1=enemy) |
| 0x1407FC620 | Action menu cursor (0=Move, 1=Abilities, 2=Wait) |

## Valid Tile Computation (BFS)

The BFS uses map JSON data for exact results:
- **Movement cost**: every tile costs 1 (no terrain penalties)
- **Jump check**: `|height_A - height_B| <= jumpStat`
- **Height formula**: `display = tile.height + tile.slope_height / 2`
- **Blocked tiles**: `no_walk` flag (trees, walls) or enemy-occupied
- **Starting tile excluded** from results

Verified 6/6 tile counts match in-game exactly.

## Gotchas

- **Move/Jump stats from the UI buffer are base values** (no equipment modifiers). If the count seems wrong, override with `scan_move <move> <jump>`.
- **Entering Move mode resets a previous move.** Only enter Move once per turn.
- **Camera can auto-rotate** when entering Move mode. The mod reads rotation after opening Move, not before.
- **Enemy positions come from C+Up scanning** at the start of scan_move. If an enemy moved since last scan, positions may be stale.
- **The cursor can go anywhere on the grid** — it's not limited to valid tiles. But confirming on an invalid tile does nothing.

## Quick Reference

```bash
# Full turn: scan -> move -> wait
scan_move 7 3
move_grid 6 5
battle_wait

# Just scan to see the battlefield
scan_move

# End turn without moving
battle_wait
```
