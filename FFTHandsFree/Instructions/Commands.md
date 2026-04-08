<!-- This file should not be longer than 200 lines, if so prune me. -->
# Command Reference

All commands are bash functions from `fft.sh`. Source it once per session:

```bash
source ./fft.sh
```

## State & Info (always available)

| Command | Description |
|---------|-------------|
| `screen` | Current screen name, location, status |
| `scan_units` | All unit positions, HP, teams, stats |
| `scan_move` | Units + valid movement tiles + attack tiles |
| `scan_move <mv> <jmp>` | Override Move/Jump stats for tile calculation |

## Battle Actions

| Command | Description |
|---------|-------------|
| `battle_move <x> <y>` | Move active unit to grid tile |
| `battle_attack <x> <y>` | Attack target at tile |
| `battle_wait` | End turn, face optimal direction (arc scoring, requires prior move), wait for next friendly turn |
| `battle_flee` | Quit battle, return to world map |
| `battle_retry` | Retry battle (from pause menu) |
| `battle_retry_formation` | Retry with formation screen |

## Navigation

| Command | Description |
|---------|-------------|
| `world_travel_to <id>` | Travel to world map location by ID |
| `advance_dialogue` | Advance cutscene text by one box |
| `execute_action <name>` | Execute a validPath action by name |

## Management

| Command | Description |
|---------|-------------|
| `save` | Save the game |
| `load` | Load most recent save |
| `buy <item> <qty>` | Buy item from shop |
| `sell <item> <qty>` | Sell item at shop |
| `change_job <unit_id> <job>` | Change a unit's job |

## Direct Key Presses

Single key press commands for manual control. Useful for menu navigation, facing selection, and testing.

| Command | Description |
|---------|-------------|
| `up` | Press Up arrow |
| `down` | Press Down arrow |
| `left` | Press Left arrow |
| `right` | Press Right arrow |
| `enter` | Press Enter (confirm) |
| `esc` | Press Escape (cancel/back) |
| `space` | Press Space |
| `tab` | Press Tab |

Waiting variants (press key, then wait for screen change):

| Command | Description |
|---------|-------------|
| `enter_wait <screen>` | Press Enter, wait until `<screen>` appears |
| `esc_wait <screen>` | Press Escape, wait until `<screen>` appears |

## System

| Command | Description |
|---------|-------------|
| `strict 1` / `strict 0` | Enable/disable strict mode |
| `set_map <id>` | Manually load a battle map |
| `restart` | Kill game, rebuild, redeploy, relaunch |

## How It Works

1. You call a command (e.g. `battle_move 6 5`)
2. The command writes JSON to `claude_bridge/command.json`
3. The C# mod picks it up, executes it in-game, writes `response.json`
4. The helper reads the response and prints a summary

Every response includes:
- **Screen state** — which screen you're on
- **ValidPaths** — available actions for the current screen
- **Battle data** (when in battle):
  - `battle.activeUnit` — your unit: jobName, move, jump, pa, ma, hp, brave, faith
  - `battle.units[]` — all units: team, jobName, level, position, hp, distance, statuses (e.g. `["Protect","Poison"]`)
  - `ValidMoveTiles.tiles[]` — reachable tiles with height (`h`) for high ground
  - `AttackTiles.attackTiles[]` — adjacent tiles with arrow key, occupant info (hp, jobName if enemy)
  - `RecommendedFacing.facing` — optimal facing direction with arc breakdown (front/side/back counts)

## Strict Mode

When strict mode is ON (`strict 1`):
- Only commands listed above are allowed
- Raw key presses and sequences are blocked
- Forces clean, well-tested command usage

Always enable strict mode for play sessions.

## Typical Battle Flow

```bash
screen                    # Check: Battle_MyTurn?
scan_move                 # See units, tiles, enemies
battle_move 6 5           # Move adjacent to enemy
battle_attack 7 5         # Attack enemy
battle_wait               # End turn + wait for next turn
# Repeat when Battle_MyTurn returns
```

## Typical Exploration Flow

```bash
screen                    # Check: WorldMap?
save                      # Save before traveling
world_travel_to 26        # Head to The Siedge Weald
screen                    # Did we arrive or hit an encounter?
execute_action Flee       # Flee encounter — character auto-continues walking
screen                    # Check again — arrived or another encounter?
# Do NOT re-issue world_travel_to after fleeing — the game continues automatically
```

## Typical Cutscene Flow

```bash
screen                    # Check: Cutscene? eventId=?
advance_dialogue          # Advance text, react to story
advance_dialogue          # Keep going...
screen                    # Did it end? What screen now?
```

## ValidPaths

Every screen has a set of valid actions. Use `execute_action <name>` to run them. The response always shows what's available next. Common ones:

- **Battle_MyTurn**: Move, Attack, Wait, Pause
- **WorldMap**: PartyMenu, TravelList
- **Cutscene**: Advance
- **EncounterDialog**: Fight, Flee
- **PartyMenu**: various sub-menus

When in doubt, call `screen` or `execute_action` with any name — the error will list available actions.
