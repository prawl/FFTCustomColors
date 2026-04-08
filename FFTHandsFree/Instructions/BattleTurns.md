<!-- This file should not be longer than 200 lines, if so prune me. -->
# Battle Turns

How to fight battles using the bridge commands.

## Available Commands

```bash
source ./fft.sh          # Load helpers (required once per session)
screen                   # Check current screen state
scan_units               # Scan all unit positions, HP, teams
scan_move                # Scan units + compute valid movement tiles
battle_move <x> <y>      # Move active unit to grid tile (x,y)
battle_attack <x> <y>    # Attack target at tile (x,y)
battle_wait              # End turn (menu nav + facing + wait for next friendly turn)
battle_flee              # Quit battle, return to world map
battle_retry             # Retry battle from pause menu
battle_retry_formation   # Retry battle with formation screen
execute_action <name>    # Execute a validPath action (Move, Abilities, Wait, etc.)
```

## Turn Structure

Each unit gets **one turn** consisting of:

1. **Move** (optional, once) — move to a reachable tile
2. **Act** (optional, once) — use an ability or basic Attack
3. **Wait** (required) — end the turn, choose facing direction

You can do Move and Act in any order, or skip either. But you MUST Wait to end the turn.
**After attacking, you are NOT done.** You still need to Wait.

## Screen States

| State | Meaning | What to do |
|-------|---------|------------|
| `Battle_MyTurn` | Your unit's turn, action menu open | Choose: move, attack, or wait |
| `Battle_Acting` | You've partially acted (moved or used ability) | Finish your turn (attack or wait) |
| `Battle_Moving` | Move mode, selecting a tile | Pick a tile or cancel |
| `Battle_Targeting` | Attack targeting, selecting a target | Pick a target or cancel |
| `Battle_AlliesTurn` | NPC ally is acting | Wait — poll `screen` until `Battle_MyTurn` |
| `Battle_EnemiesTurn` | Enemy is acting | Wait — poll `screen` until `Battle_MyTurn` |
| `Battle_Paused` | Pause menu open | Resume, retry, or flee |

## Step-by-Step: A Full Turn

### 1. Scan the battlefield

```bash
scan_move           # uses the unit's actual Move/Jump stats
```

Returns structured JSON:
- **battle.activeUnit** — your unit's stats: jobName, move, jump, pa, ma, hp, brave, faith
- **battle.units[]** — all units with **name** (story characters like "Ramza", "Agrias"), team, jobName, level, x, y, hp/maxHp, ct, speed, distance, **statuses** (e.g. `["Protect","Shell","Poison"]`)
- **battle.turnOrder[]** — Combat Timeline order (who acts next). Each entry has name, team, level, hp/maxHp, x, y, ct, isActive. Derived from C+Up scan order which follows the game's timeline.
- **ValidMoveTiles.tiles[]** — reachable tiles with `{x, y, h}` (h = height for high ground)
- **AttackTiles.attackTiles[]** — 4 cardinal tiles with `{x, y, arrow, occupant}`. Occupied tiles include hp, maxHp, jobName
- **RecommendedFacing.facing** — optimal Wait direction with `{direction, front, side, back}` arc counts

### 2. Move

```bash
battle_move 6 5     # move to grid position (6,5)
```

Pick a tile from the `ValidMoveTiles.tiles[]` array. Each tile has `{x, y, h}` — prefer higher `h` values for high ground advantage. The mod handles Move mode, rotation, arrow keys, and confirmation.

### 3. Attack

```bash
battle_attack 7 5   # attack tile (7,5)
```

Pick a target from `AttackTiles` in the scan_move response — any tile with `"occupant": "enemy"`. These tiles include the target's hp, maxHp, and jobName so you can assess threats. The mod handles menu navigation, rotation detection, cursor movement, and confirmation.

### 4. End turn

```bash
battle_wait         # end turn, auto-faces optimal direction, waits for next friendly turn
```

`battle_wait` handles everything: navigates to Wait in the menu, faces the optimal direction (minimizes enemies at your back using arc-based threat scoring), holds Ctrl to fast-forward through enemy/ally turns, and returns when it's your turn again.

**Note:** Auto-facing only works after a move (`battle_move`), because the rotation is detected empirically during grid navigation. If you skip the move and just wait, it accepts the default facing direction.

The `RecommendedFacing` in scan_move shows you the recommended direction before you act, with arc counts (front/side/back) so you can understand the reasoning.

To manually control facing (e.g. for testing or overriding), use direct key presses:

```bash
# Navigate to Wait manually, then pick facing direction
execute_action Wait   # or use battle_wait for full auto
up                    # face a direction
down                  # face a direction
left                  # face a direction
right                 # face a direction
enter                 # confirm facing
```

## Waiting for Other Turns

When you see `Battle_AlliesTurn` or `Battle_EnemiesTurn`, it's not your turn. You can:
- Poll `screen` to watch the state change
- Call `scan_units` to see HP changes
- Comment on what's happening ("Agrias just took a hit!")
- Wait for `Battle_MyTurn` to come back

`battle_wait` handles this automatically after ending your turn.

## Quick Reference

```bash
# Full turn: scan -> move -> attack -> wait
scan_move
battle_move 6 5
battle_attack 7 5
battle_wait

# Attack without moving (enemy already adjacent)
scan_move
battle_attack 2 4
battle_wait

# Just wait (skip move and action)
battle_wait

# Retry a lost battle
battle_retry

# Flee from battle
battle_flee
```

## How Map Detection Works

1. Your world map location is saved when you stop on a node
2. Battle starts — location is looked up in `random_encounter_maps.json` (e.g. location 26 -> MAP074)
3. MAP074.json is loaded with tile heights, walkability, terrain
4. BFS runs from the active unit using Move/Jump stats

If auto-lookup fails, use `set_map <id>` to manually load the correct map.

## How Grid Navigation Works

The cursor moves on an absolute grid. Arrow key → grid direction depends on camera rotation:

| rot | Right    | Left     | Up       | Down     |
|-----|----------|----------|----------|----------|
| 0   | (0, +1)  | (0, -1)  | (-1, 0)  | (+1, 0)  |
| 1   | (+1, 0)  | (-1, 0)  | (0, +1)  | (0, -1)  |
| 2   | (0, -1)  | (0, +1)  | (+1, 0)  | (-1, 0)  |
| 3   | (-1, 0)  | (+1, 0)  | (0, -1)  | (0, +1)  |

The mod reads rotation automatically. You just say `battle_move x y`.

## Unit Teams

- **PLAYER** (team 0) — your units, you control these
- **ENEMY** (team 1) — enemies to defeat
- **ALLY** (team 2) — NPC guest units (Agrias, Gaffgarion, etc.), act on their own

## Gotchas

- **Always scan first.** `scan_move` before any move or attack.
- **battle_wait ends your turn AND waits.** It polls until the next friendly turn.
- **Move/Jump stats from UI are base values** (no equipment). Override with `scan_move <move> <jump>`.
- **Camera auto-rotates** when entering Move or Attack targeting. The mod handles this.
- **Confirming on an invalid tile does nothing.** Always pick from valid tile lists.
