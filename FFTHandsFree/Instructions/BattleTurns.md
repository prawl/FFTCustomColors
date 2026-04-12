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
| `Battle_Moving` | Move mode, selecting a tile (battleMode=2) | Pick a tile or cancel |
| `Battle_Abilities` | Abilities submenu open (Attack/Mettle/Items) | Pick a skillset or cancel |
| `Battle_Attacking` | Instant targeting: basic Attack, Throw, Items, Iaido, Aim (battleMode=4) | Pick a target or cancel |
| `Battle_Casting` | Cast-time magick targeting: Fire, Cure, Haste, Summons, etc. (battleMode=1) | Pick a target or cancel |
| `Battle_Mettle` | Mettle ability list (Focus/Rush/Shout/...) | Pick an ability or cancel |
| `Battle_Items` | Items list (Potion/Phoenix Down/...) | Pick an item or cancel |
| `Battle_<Skillset>` | Any other skillset ability list | Pick an ability or cancel |
| `Battle_AlliesTurn` | NPC ally is acting | Wait — poll `screen` until `Battle_MyTurn` |
| `Battle_EnemiesTurn` | Enemy is acting | Wait — poll `screen` until `Battle_MyTurn` |
| `Battle_Paused` | Pause menu open | Resume, retry, or flee |

The `ui=` field shows the current cursor position at each level:
- `Battle_MyTurn ui=Abilities` — cursor on Abilities in action menu
- `Battle_Abilities ui=Mettle` — cursor on Mettle in abilities submenu
- `Battle_Mettle ui=Shout` — cursor on Shout in ability list

## Step-by-Step: A Full Turn

### 1. Scan the battlefield

```bash
scan_move           # uses the unit's actual Move/Jump stats
```

Returns structured JSON:
- **battle.activeUnit** — your unit's stats: jobName, move, jump, pa, ma, hp, brave, faith
- **battle.units[]** — all units with:
  - **name** — story characters ("Ramza", "Agrias", etc.) AND generic player recruits ("Kenrick", "Lloyd", "Wilham"). Enemy names and monster names are null (not readable from memory yet).
  - jobName, team, level, x, y, hp/maxHp, ct, speed, distance
  - **statuses** (e.g. `["Protect","Shell","Poison"]`)
  - **lifeState** — "dead", "crystal", or null (alive)
  - **abilities** (per-unit) — each ability entry: `{name, mp, horizontalRange, verticalRange, areaOfEffect, heightOfEffect, target, effect, castSpeed, element, addedEffect}`. Populated for active player (learned + equipped skillsets), AND for monster enemies (fixed per class from `MonsterAbilities.cs`). Enemy human abilities are per-encounter randomized and NOT yet readable.
- **battle.turnOrder[]** — Combat Timeline order. Each entry has name, team, level, hp/maxHp, x, y, ct, isActive.
- **ValidMoveTiles.tiles[]** — reachable tiles with `{x, y, h}` (h = height for high ground)
- **AttackTiles.attackTiles[]** — 4 cardinal tiles with `{x, y, arrow, occupant}`. Occupied tiles include hp, maxHp, jobName
- **RecommendedFacing.facing** — optimal Wait direction with `{direction, front, side, back}` arc counts

**Duplicate units get numbered suffixes** — if you have 3 Bonesnatches on the field, they render as `(Bonesnatch #1)`, `(Bonesnatch #2)`, `(Bonesnatch #3)` so you can reference specific instances ("focus Bonesnatch #3 first"). Numbering is by scan order and stable within a single scan.

**Scan is blocked during unsafe states.** scan_move returns `status=blocked` during `Battle_Acting`, `Battle_AlliesTurn`, `Battle_EnemiesTurn` because C+Up cycling can corrupt game state mid-animation. Wait for `Battle_MyTurn` before scanning.

### 2. Move

```bash
battle_move 6 5     # move to grid position (6,5)
```

Pick a tile from the `ValidMoveTiles.tiles[]` array. Each tile has `{x, y, h}` — prefer higher `h` values for high ground advantage. The mod handles Move mode, rotation, arrow keys, and confirmation.

### 3. Act — Use an Ability

Use `battle_ability` for ALL offensive, healing, and support actions (including basic Attack):

```bash
battle_ability "Attack" 7 5          # basic attack at tile (7,5)
battle_ability "Throw Stone" 4 8     # ranged ability at tile
battle_ability "Cure" 10 9           # heal ally at tile
battle_ability "Ifrit" 6 7           # AoE centered on tile
battle_ability "Shout"               # self-target (no coordinates)
battle_ability "Chakra"              # self-radius AoE (no coordinates)
battle_ability "Phoenix Down" 3 4    # raise dead ally
```

`battle_attack` still works as a shortcut for basic Attack only.

**How scan_move shows abilities:** Each scanned unit's abilities include metadata and valid targets. The shell renders them in compact format:

```
  Attack R:1 -> enemy hits=2: *(7,5)«Goblin» *(6,6)«Skeleton»  (2 empty)
  Cure R:4 -> ally MP 6 hits=1: *(10,9)«Ramza» (3,4)«Goblin»  (8 empty)
  Ifrit R:4 AoE:2 -> enemy (Fire) MP 24 centers=12
    best: (6,7) e:Goblin,Skeleton  (5,8) e:Goblin a:Ramza
  Shockwave R:3 -> enemy seeds=4
    best: East→(8,6) e:Goblin,Skeleton  North→(7,7) e:Goblin
  Shout R:Self AoE:1 -> self/AoE MP 0
  Chakra R:Self AoE:2 -> ally/AoE
  Chant R:Self AoE:99 -> ally/AoE
```

**Reading the format:**
- **Point-target** (AoE=1): `hits=N` counts intent-matching tiles. `*` marks good targets. `«Name»` shows occupants.
- **Radius AoE** (AoE>1): `centers=N` valid aim points. `best:` ranks top placements by `(enemies - allies)`.
- **Line AoE**: `seeds=N` clickable tiles. `best:` ranks cardinal directions by hits.
- **Self-only** (R:Self AoE:1): No targets — just use the ability name with no coordinates.
- **Self-radius** (R:Self AoE>1): Hits tiles around caster. No coordinates needed.
- **Full-field** (AoE:99): Hits all allies/enemies. Bard/Dancer songs. No targeting.

**Choosing a target:** Pick from the ability's valid targets in scan_move. For AoE, use the `best:` line to find the placement that hits the most enemies with fewest allies caught. For point-target, pick tiles marked with `*`.

### 4. End turn

```bash
battle_wait         # end turn, auto-faces optimal direction, waits for next friendly turn
```

`battle_wait` handles everything: navigates to Wait in the menu, faces the optimal direction (minimizes enemies at your back using arc-based threat scoring), holds Ctrl to fast-forward through enemy/ally turns, and returns when it's your turn again.

**Note:** Auto-facing only works after a move (`battle_move`), because the rotation is detected empirically during grid navigation. If you skip the move and just wait, it accepts the default facing direction.

The `RecommendedFacing` in scan_move shows you the recommended direction before you act, with arc counts (front/side/back) so you can understand the reasoning.

## Waiting for Other Turns

`battle_wait` auto-waits through enemy/ally turns. When you see `Battle_AlliesTurn` or `Battle_EnemiesTurn`, poll `screen` until `Battle_MyTurn` returns.

## Quick Reference

```bash
# Full turn: scan -> move -> ability -> wait
scan_move
battle_move 6 5
battle_ability "Attack" 7 5
battle_wait

# Use a spell after moving
scan_move
battle_move 4 3
battle_ability "Fire" 5 4
battle_wait

# Heal an ally (no move)
scan_move
battle_ability "Cure" 10 9
battle_wait

# Self-buff (no move, no target)
scan_move
battle_ability "Shout"
battle_wait

# Just wait (skip move and action)
battle_wait
```

## Unit Teams

- **PLAYER** (team 0) — your units, you control these
- **ENEMY** (team 1) — enemies to defeat
- **ALLY** (team 2) — NPC guest units (Agrias, Gaffgarion, etc.), act on their own

## Gotchas

- **Always scan first.** `scan_move` before any move or ability.
- **battle_wait ends your turn AND waits.** It polls until the next friendly turn.
- **Pick targets from scan data.** Point-target: use `*`-marked tiles. AoE: use `best:` placements.
- **First-turn scan returns stale data.** Workaround: `battle_wait` through first turn, then rescan.
- **battle_ability validates range.** If target is out of range, it returns an error instead of wasting the action.
- **Enemy names are not readable.** Enemy units show job names only (e.g. "Goblin", "Knight"), not their individual names. For "defeat X" objectives, you'll need to identify targets by other means.
- **Mod-forced battles break class detection.** Grogh Heights, Dugeura Pass, Tchigolith Fenlands, Mandalia Plain — all enemies show as `(?)`. Flee these.
