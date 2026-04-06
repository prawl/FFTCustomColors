<!-- This file should not be longer than 200 lines, if so prune me. -->
# Battle Movement

How to move units around the battlefield and attack enemies using the bridge system.

## Turn Structure

Each unit gets **one turn** consisting of:

1. **Move** (optional, once) — move to a reachable tile
2. **Act** (optional, once) — use an ability or basic Attack
3. **Wait** (required) — end the turn, choose facing direction

You can do Move and Act in any order, or skip either. But you MUST Wait to end the turn.
After acting, Abilities becomes grayed out — you cannot act again.
After moving, you cannot enter Move mode again.
**After attacking, you are NOT done.** You still need to Wait to end your turn.

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

## Screen States During a Turn

- **Battle_MyTurn** (`ui=Move`) — Fresh turn, action menu open, cursor on Move
- **Battle_MyTurn** (`ui=Abilities`) — Action menu open, cursor on Abilities
- **Battle_Acting** (`ui=Abilities`) — You've already acted (moved or used ability), menu shows remaining options. **This is still your turn** — you can still Wait or use remaining actions.
- **Battle_Moving** — Move mode active, cursor on the grid selecting a tile
- **Battle_Targeting** — Attack/ability targeting active, cursor selecting a target tile
- **Battle** — Enemy turn or animation playing, not your turn

**Key distinction:** `Battle_Acting` does NOT mean an enemy is acting. It means your unit has partially completed their turn (e.g., moved but hasn't waited yet, or selected Abilities).

## Gotchas

- **Move/Jump stats from the UI buffer are base values** (no equipment modifiers). If the count seems wrong, override with `scan_move <move> <jump>`.
- **Entering Move mode resets a previous move.** Only enter Move once per turn.
- **Camera auto-rotates** when entering Move or Attack targeting. Never trust rotation from before entering — always read it after.
- **Enemy positions come from C+Up scanning** at the start of scan_move. If an enemy moved since last scan, positions may be stale.
- **The cursor can go anywhere on the grid** in both Move and Attack targeting — it's not limited to valid/highlighted tiles. Confirming on an invalid tile does nothing.
- **Use empirical rotation detection** for both movement and attack targeting. Press one arrow, read cursor delta, derive rotation. Don't rely on the rotation address alone — it can change between modes.

## Attacking

### Attack Flow (Manual via bash)
1. From action menu, navigate to Abilities (Down if cursor on Move) → **Enter**
2. Select Attack (top option) → **Enter**
3. Screen becomes `Battle_Targeting` — cursor is on Ramza's tile
4. **Empirical rotation detection**: press Right, read cursor delta, undo with Left
5. Compute which arrow key reaches the enemy tile (same rotation table as movement)
6. Press that arrow key — verify cursor is on the enemy's grid position
7. **Enter** (select target) → **Enter** (confirm "Target this tile? Yes")
8. Attack animation plays
9. **You still need to Wait after attacking!**

### AttackTiles (from scan_move response)
`scan_move` returns `AttackTiles` showing the 4 cardinal tiles with arrow key mapping:
```
"AttackTiles": "7,5=Down(empty) 5,5=Up(ENEMY) 6,6=Right(ENEMY) 6,4=Left(ENEMY)"
```
**WARNING**: The rotation in AttackTiles is from scan time. Camera may auto-rotate when entering targeting mode. Always use empirical rotation detection in targeting mode.

### Attack Range
- Sword: 1 tile, 4 cardinal directions (no diagonal)
- The targeting cursor is free — it can move anywhere on the grid, not just valid tiles
- Confirming on a tile outside attack range does nothing

## Waiting (Ending Your Turn)

To Wait, navigate the menu cursor to Wait (index 2) and press Enter twice (select + confirm facing).

**KNOWN BUG: `battle_wait` is unreliable.** It assumes the cursor is on Move and presses Down once, but after attacking the cursor is on Abilities (index 1). This causes it to misfire. Until fixed, manually navigate:

1. Check `ui=` in screen response to know current cursor position
2. Press Down the right number of times to reach Wait (index 2)
3. **Enter** (select Wait) → **Enter** (confirm facing direction)

| Current ui | Downs to Wait |
|-----------|---------------|
| Move (0) | 2 |
| Abilities (1) | 1 |
| Wait (2) | 0 |

## Quick Reference

```bash
# Full turn: scan -> move -> attack -> wait
scan_move 7 3                   # ALWAYS scan first
move_grid 6 5                   # move adjacent to enemy
# Attack: navigate to Abilities→Attack, empirical rotation, target, confirm
# Wait: navigate cursor to Wait(2), Enter, Enter

# Attack without moving (enemy already adjacent)
# scan_move, then attack, then wait

# Just wait (skip move and action)
# Navigate cursor to Wait(2), Enter, Enter
```
