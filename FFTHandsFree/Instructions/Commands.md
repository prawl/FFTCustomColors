<!-- This file should not be longer than 200 lines, if so prune me. -->
# Command Reference

All commands are bash functions from `fft.sh`. Two ways to use them:

```bash
# Interactive shell — source once, helpers stay loaded for the rest of the shell:
source ./fft.sh
screen
battle_move 6 5

# Per-call invocation — each command is its own process (LLM agents using a
# Bash-tool-per-call pattern want this; otherwise you'd have to prefix every
# call with `source ./fft.sh && ...`):
./fft screen
./fft battle_move 6 5
./fft battle_ability "Attack" 7 5
```

The `./fft` wrapper sources `fft.sh` and dispatches to the named helper. It
exists because each Bash tool call from an agent driver is a fresh shell —
function definitions don't persist across calls. The wrapper handles the
re-source for you so the agent only types one command per call.

## State & Info (always available)

| Command | Description |
|---------|-------------|
| `screen` | Current screen name, location, status. On PartyMenuUnits it renders the 5-column grid the game shows with a `cursor->` marker on the highlighted row, and `ui=<name>` tells you exactly which unit is hovered. |
| `screen -v` | Verbose mode. On PartyMenuUnits, dumps the raw roster JSON (every unit with slot, displayOrder, name, level, job, brave/faith, JP, equipment) plus grid metadata (gridCols, gridRows, cursorRow, cursorCol, hoveredName). Use when you need to scan for gear gaps, compare stats across the party, or pick by an attribute rather than by grid position. On EquipmentAndAbilities it expands the detail panel. |
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

### Bundled turn

`execute_turn` bundles move + ability + wait into one round-trip. Aborts at first non-completed sub-step.

```bash
execute_turn 6 5                     # move only — DOES NOT END THE TURN
execute_turn 6 5 Attack 7 5          # move + attack
execute_turn 6 5 Cure 10 9           # move + heal ally
execute_turn 6 5 Shout               # move + self-target
execute_turn '' '' Cure 10 9         # no move, ability only
execute_turn 6 5 Attack 7 5 N        # + face North
execute_turn 6 5 Attack 7 5 '' --nowait   # skip wait
```

**Move-only does NOT end your turn.** `execute_turn 6 5` (just two args) returns to `BattleMyTurn` with your Act and Wait still available — and enemies whose CT comes up between your Move and your next command CAN act and hit you in that gap. To actually end the turn after a move, follow with `battle_wait`, or bundle the wait via `execute_turn 6 5 '' '' '' ''` style — easiest is just to call `battle_wait` next. Same gotcha applies to a bare `battle_move`.

### Dev tools

| Command | Description |
|---------|-------------|
| `buff_ramza [hp]` | Make Ramza (first player-side battle slot) invincible for one battle. HP/MaxHP=999 (or arg), PA=255, all elements absorbed. Does NOT touch level/exp/brave/faith — enemy scaling unchanged. Call AFTER battle array is populated (post-formation). Per-battle only; static array resets each battle. |
| `buff_all [hp]` | Same as `buff_ramza` but buffs every roster-matched player-team slot. For multi-party story battles. |
| `kill_enemies` | Insta-win the current battle. Discovers the master HP table at runtime (~0x14184xxxx, stride 0x200), writes HP=0 + dead-bit to every enemy slot while leaving player slots alone. End your turn to trigger BattleVictory. Call on BattleMyTurn after a `scan_move` (planner needs a recent roster snapshot to tell players from enemies). Undead with Reraise may re-animate — retry if one survives. |

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
  - `battle.activeUnit` — your unit: jobName, move, jump, pa, ma, hp, brave, faith, plus equipped reaction/support/movement
  - `battle.units[]` — all units: name, team, jobName, level, position, hp, distance, statuses, **reaction/support** (when fingerprinted from heap struct), abilities (with validTargetTiles, bestCenters, bestDirections per ability)
  - `ValidMoveTiles.tiles[]` — reachable tiles with height (`h`) for high ground
  - `AttackTiles.attackTiles[]` — adjacent tiles with arrow key, occupant info (hp, jobName if enemy). The `arrow` field (`Up/Down/Left/Right`) is the **post-rotation** key to press, NOT an absolute compass direction. Camera rotation may make "Up" point south on the map.
  - `RecommendedFacing.facing` — optimal facing direction with arc breakdown (front/side/back counts)

## Reading the screen-line header

The compact one-line header at the top of every response looks like:

`[BattleMyTurn] ui=Move Ramza(Gallant Knight) (8,10) HP=719/719 MP=138/138 curLoc=The Siedge Weald t=358ms[scan_move]`

Field-by-field:
- `[ScreenName]` — current detected screen (BattleMyTurn, BattleMoving, WorldMap, ...)
- `[ACTED]` / `[MOVED]` — appears when you've consumed your Action / Move this turn (only Wait/Status remain). Action menu in-game greys out the consumed slot.
- `ui=Move` (or `ui=Abilities`, `ui=Wait`) — which action-menu slot the cursor is on. On battle screens, this reflects the highlighted entry; outside battle it's unrelated. `ui=Move` on a fresh BattleMyTurn is the default starting position; nothing to act on.
- Active unit `Name(Job) (X,Y)` — the unit whose turn it is, and where they stand.
- `t=Nms[action]` — bridge round-trip time and the action that produced it. Colored green/yellow/red by `FFT_SLOW_MS` (default 800ms warn, 2× red). A `!` suffix marks over-warn, `!!` marks over-red — speed regression, not an error. Use `session_tail slow 1500` for the bug-finder filter on the JSONL log.

## Strict Mode

When strict mode is ON (`strict 1`):
- Only commands listed above are allowed
- Raw key presses and sequences are blocked
- Forces clean, well-tested command usage

Always enable strict mode for play sessions.

## Typical Battle Flow

```bash
screen                    # Check: BattleMyTurn?
scan_move                 # See units, tiles, abilities, targets
battle_move 6 5           # Move to tile
battle_ability "Attack" 7 5  # Use ability on target
battle_wait               # End turn + wait for next turn
# Repeat when BattleMyTurn returns
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

- **BattleMyTurn**: Move, Attack, Wait, Pause
- **WorldMap**: PartyMenuUnits, TravelList
- **Cutscene**: Advance
- **EncounterDialog**: Fight, Flee
- **PartyMenuUnits**: OpenUnits/OpenInventory/OpenChronicle/OpenOptions + CursorUp/Down/Left/Right + SelectUnit (opens CharacterStatus for whoever the cursor is on — not necessarily Ramza). `ui=<name>` in the screen header names the hovered unit. The `screen.roster` payload lists every member sorted in display order with equipment + stats — use it to decide who to inspect before navigating the grid.
- **CharacterStatus**: sidebar (Equipment & Abilities / Job / Combat Sets), Open dialog, Hold-B to open DismissUnit, Back
- **EquipmentAndAbilities**: Q/E to cycle units, left column = equipment slots (Enter → EquippableWeapons/Shields/Headware/CombatGarb/Accessories picker), right column = ability slots

When in doubt, call `screen` or `execute_action` with any name — the error will list available actions with their descriptions (session 47: `Name — Desc` format, aliases coalesced e.g. `Back/Leave/Exit`).

### Action aliases

Some verbs have alternate spellings to match how users naturally type. All of these reach the same underlying action:

- **Back / Leave / Exit** — exit the current screen. Defined on any screen that has at least one of them.
- **Yes / Confirm / OK** — affirmative on confirm modals (ShopConfirmDialog, BattleCrystalMoveConfirm, BattleAbilityAcquireConfirm). Yes / OK both commit via the safer cursor-then-Enter sequence; Confirm is plain Enter.
- **No** — cancel on confirm modals (Escape).

## Known Gotchas

**First turn of a new battle** may return stale cached scan data. Workaround: `battle_wait` through the first turn to force a fresh scan.

**`battle_flee` can leave screen state stuck** reporting `BattleMyTurn` for several seconds after the player is back on the world map. Travel commands may still fail during this window even though you ARE on the world map. If you see a "stuck" state after flee, run `restart` to cleanly re-sync.

**Formation screen lies about its state** for 3-6 seconds after `execute_action Fight`. Screen detection reports `TravelList` during this transition — don't trust it. Just `sleep 3` and blindly send the standard formation key sequence: Enter → Enter → Space → Enter.

**Strict mode blocks raw key input** (`enter`, `space`, etc). Disable with `strict 0` for formation transitions or menu scraping, re-enable with `strict 1` after. For play sessions always keep strict mode ON to catch command typos.

**Scan during enemy/animation turns is blocked.** `scan_move` returns `status=blocked` during BattleActing / BattleAlliesTurn / BattleEnemiesTurn because C+Up cycling can corrupt game state mid-animation. Allowed states: BattleMyTurn, BattleMoving, BattleAttacking, BattleAbilities, BattleWaiting, BattlePaused.

**Ramza's fingerprint varies per save** (his job/equipment change as you play). Don't rely on fingerprint for him — the roster lookup by nameId=1 is authoritative.

**Mod-forced battles skip unit struct search.** Grogh Heights, Dugeura Pass, Tchigolith Fenlands, Mandalia Plain story battles spawn unit structs at addresses outside the hardcoded heap range. Fingerprints fail → every enemy shows as `(?)`. Flee those battles and move on.

**Use `restart` (fft.sh helper), not manual taskkill.** The helper handles the full rebuild+deploy+launch+boot cycle. Manual sequences often skip steps and leave the mod out of sync with the game.

**Only one command at a time per bash call.** The bridge is single-threaded and chaining multiple commands with `&&` can race. Send one, wait for response, send the next.
