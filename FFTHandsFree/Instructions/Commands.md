<!-- This file should not be longer than 200 lines, if so prune me. -->
# Command Reference

All commands are bash functions from `fft.sh`. Source it once per session:

```bash
source ./fft.sh
```

## State & Info (always available)

| Command | Description |
|---------|-------------|
| `screen` | Current screen name, location, status. On PartyMenu it renders the 5-column grid the game shows with a `cursor->` marker on the highlighted row, and `ui=<name>` tells you exactly which unit is hovered. |
| `screen -v` | Verbose mode. On PartyMenu, dumps the raw roster JSON (every unit with slot, displayOrder, name, level, job, brave/faith, JP, equipment) plus grid metadata (gridCols, gridRows, cursorRow, cursorCol, hoveredName). Use when you need to scan for gear gaps, compare stats across the party, or pick by an attribute rather than by grid position. On EquipmentAndAbilities it expands the detail panel. |
| `scan_units` | All unit positions, HP, teams, stats |
| `scan_move` | Units + valid movement tiles + attack tiles |
| `scan_move <mv> <jmp>` | Override Move/Jump stats for tile calculation |

## Battle Actions

| Command | Description |
|---------|-------------|
| `battle_move <x> <y>` | Move active unit to grid tile |
| `battle_ability "<name>" <x> <y>` | Use any ability on a target tile |
| `battle_ability "<name>"` | Use self-target ability (Shout, Chakra, etc.) |
| `battle_attack <x> <y>` | Shortcut for `battle_ability "Attack" x y` |
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
| `running` | Check if FFT_enhanced.exe is alive (fast, no bridge call) |
| `boot` | Launch game if not running, advance past title. Safe from any state. |
| `restart` | Full cycle: kill, rebuild, redeploy, relaunch, boot through title |
| `logs` | Tail the live mod log (`claude_bridge/live_log.txt`) |
| `logs 100` | Last 100 lines |
| `logs grep <pattern>` | Grep the whole live log |

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
  - `battle.units[]` — all units: name, team, jobName, level, position, hp, distance, statuses, abilities (with validTargetTiles, bestCenters, bestDirections per ability)
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
scan_move                 # See units, tiles, abilities, targets
battle_move 6 5           # Move to tile
battle_ability "Attack" 7 5  # Use ability on target
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
- **PartyMenu**: OpenUnits/OpenInventory/OpenChronicle/OpenOptions + CursorUp/Down/Left/Right + SelectUnit (opens CharacterStatus for whoever the cursor is on — not necessarily Ramza). `ui=<name>` in the screen header names the hovered unit. The `screen.roster` payload lists every member sorted in display order with equipment + stats — use it to decide who to inspect before navigating the grid.
- **CharacterStatus**: sidebar (Equipment & Abilities / Job / Combat Sets), Open dialog, Hold-B to open DismissUnit, Back
- **EquipmentAndAbilities**: Q/E to cycle units, left column = equipment slots (Enter → EquippableWeapons/Shields/Headware/CombatGarb/Accessories picker), right column = ability slots

When in doubt, call `screen` or `execute_action` with any name — the error will list available actions.

## Known Gotchas

**First turn of a new battle** may return stale cached scan data. Workaround: `battle_wait` through the first turn to force a fresh scan.

**`battle_flee` can leave screen state stuck** reporting `Battle_MyTurn` for several seconds after the player is back on the world map. Travel commands may still fail during this window even though you ARE on the world map. If you see a "stuck" state after flee, run `restart` to cleanly re-sync.

**Formation screen lies about its state** for 3-6 seconds after `execute_action Fight`. Screen detection reports `TravelList` during this transition — don't trust it. Just `sleep 3` and blindly send the standard formation key sequence: Enter → Enter → Space → Enter.

**Strict mode blocks raw key input** (`enter`, `space`, etc). Disable with `strict 0` for formation transitions or menu scraping, re-enable with `strict 1` after. For play sessions always keep strict mode ON to catch command typos.

**Scan during enemy/animation turns is blocked.** `scan_move` returns `status=blocked` during Battle_Acting / Battle_AlliesTurn / Battle_EnemiesTurn because C+Up cycling can corrupt game state mid-animation. Allowed states: Battle_MyTurn, Battle_Moving, Battle_Attacking, Battle_Abilities, Battle_Waiting, Battle_Paused.

**Ramza's fingerprint varies per save** (his job/equipment change as you play). Don't rely on fingerprint for him — the roster lookup by nameId=1 is authoritative.

**Mod-forced battles skip unit struct search.** Grogh Heights, Dugeura Pass, Tchigolith Fenlands, Mandalia Plain story battles spawn unit structs at addresses outside the hardcoded heap range. Fingerprints fail → every enemy shows as `(?)`. Flee those battles and move on.

**Use `restart` (fft.sh helper), not manual taskkill.** The helper handles the full rebuild+deploy+launch+boot cycle. Manual sequences often skip steps and leave the mod out of sync with the game.

**Only one command at a time per bash call.** The bridge is single-threaded and chaining multiple commands with `&&` can race. Send one, wait for response, send the next.
