<!-- This file should not be longer than 200 lines, if so prune me. -->
# Battle Turns

How to fight battles using the bridge commands.

## Available Commands

```bash
source ./fft.sh          # Load helpers (required once per session)
screen                   # Check current screen state + (during battle) full unit scan + valid moves + AoE rankings
scan_move                # Same as `screen` from a battle state — alias kept for muscle memory
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
| `BattleMyTurn` | Your unit's turn, action menu open | Choose: move, attack, or wait |
| `BattleActing` | You've partially acted (moved or used ability) | Finish your turn (attack or wait) |
| `BattleMoving` | Move mode, selecting a tile (battleMode=2) | Pick a tile or cancel |
| `BattleAbilities` | Abilities submenu open (Attack/Mettle/Items) | Pick a skillset or cancel |
| `BattleAttacking` | Instant targeting: basic Attack, Throw, Items, Iaido, Aim (battleMode=4) | Pick a target or cancel |
| `BattleCasting` | Cast-time magick targeting: Fire, Cure, Haste, Summons, etc. (battleMode=1) | Pick a target or cancel |
| `Battle_Mettle` | Mettle ability list (Focus/Rush/Shout/...) | Pick an ability or cancel |
| `Battle_Items` | Items list (Potion/Phoenix Down/...) | Pick an item or cancel |
| `Battle_<Skillset>` | Any other skillset ability list | Pick an ability or cancel |
| `BattleAlliesTurn` | NPC ally is acting | Wait — poll `screen` until `BattleMyTurn` |
| `BattleEnemiesTurn` | Enemy is acting | Wait — poll `screen` until `BattleMyTurn` |
| `BattlePaused` | Pause menu open | Resume, retry, or flee |
| `BattleVictory` | All enemies defeated | Battle ends — no further actions |
| `BattleDesertion` | Your party wiped OR the protagonist crystallized; battle abandoned | Battle ended in a non-Victory non-GameOver way; the game returns you to WorldMap. Common trigger: Ramza KO'd long enough to crystallize (3-turn deathCounter expires). |
| `GameOver` | All player units KO'd | Battle ends — game over screen |

The `ui=` field shows the current cursor position at each level:
- `BattleMyTurn ui=Abilities` — cursor on Abilities in action menu
- `BattleAbilities ui=Mettle` — cursor on Mettle in abilities submenu
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
  - **statuses** (e.g. `["Protect","Shell","Poison"]`) — alive-only effects. **Crystal/Dead/Treasure/Petrify never appear here** — they're surfaced separately as `lifeState`. So `[Dead,Regen,Protect,Shell]` (PSX-shaped) is now `lifeState=dead` + `statuses=[Regen,Protect,Shell]` — buffs that persisted on the corpse.
  - **lifeState** — `null` (alive), `"dead"` (KO'd, recoverable with Phoenix Down or Raise within ~3 turns), `"crystal"` (crystallized, **permanently gone**), `"treasure"` (turned into a loot chest, **permanently gone**), `"petrified"` (Stone, can't act/be attacked until Gold Needle). The render surfaces this as a distinct ` DEAD` / ` CRYSTAL` / ` TREASURE` / ` STONE` suffix on the unit row — visually separate from the bracketed `[Status]` block so you can't confuse a recoverable KO with a permanent loss.
  - **abilities** (per-unit) — each ability entry: `{name, mp, horizontalRange, verticalRange, areaOfEffect, heightOfEffect, target, effect, castSpeed, element, addedEffect}`. Populated for active player (learned + equipped skillsets), AND for monster enemies (fixed per class from `MonsterAbilities.cs`). Enemy human abilities are per-encounter randomized and NOT yet readable.
- **battle.turnOrder[]** — Combat Timeline order. Each entry has name, team, level, hp/maxHp, x, y, ct, isActive.
- **ValidMoveTiles.tiles[]** — reachable tiles with `{x, y, h}` (h = height for high ground)
- **AttackTiles.attackTiles[]** — 4 cardinal tiles with `{x, y, arrow, occupant}`. Occupied tiles include hp, maxHp, jobName
- **RecommendedFacing.facing** — optimal Wait direction with `{direction, front, side, back}` arc counts

**Duplicate units get numbered suffixes** — if you have 3 Bonesnatches on the field, they render as `(Bonesnatch #1)`, `(Bonesnatch #2)`, `(Bonesnatch #3)` so you can reference specific instances ("focus Bonesnatch #3 first"). Numbering is by scan order and stable within a single scan.

**Scan is blocked during unsafe states.** scan_move returns `status=blocked` during `BattleActing`, `BattleAlliesTurn`, `BattleEnemiesTurn` because C+Up cycling can corrupt game state mid-animation. Wait for `BattleMyTurn` before scanning.

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

**Per-tile annotation tags** (suffix on the `<…>` block):
- `>rear` / `>side` — backstab-arc relative to the target's facing. Rear = backstab bonus, side = modest. Front is implicit (no tag).
- `+absorb` / `=null` / `~half` / `!weak` / `^strengthen` — element-affinity. Surfaces only when the ability has an element AND the target has that affinity. Sigil meaning: `+` heals, `=` no damage, `~` half damage, `!` double damage, `^` strengthens own outgoing element.
- `!blocked` — the bridge's line-of-sight check thinks terrain blocks the projectile / ranged attack from the caster's CURRENT tile to this target. **Treat as a hint, not a verdict** — the LoS calculator can be conservative (especially at edge cases like tile-edge heights); the game itself is authoritative. If you really want to attack, try anyway — the bridge will let the game decide. The tag is most reliable on clear longish-range shots; flag false-negatives in `playtest_logs/`.
- `[REVIVE]` / `[REVIVE-ENEMY!]` / `[KO]` / `[KO-ALLY!]` — revive-ability intent (Phoenix Down etc.) on the target's lifeState. `REVIVE` = canonical (dead ally), `REVIVE-ENEMY!` = resurrects an enemy (usually bad), `KO` = undead-status enemy (kill move), `KO-ALLY!` = undead-status ally (kills your own).
- `[TOO CLOSE]` — basic-Attack cardinal tile that's NOT in range because your weapon's MinRange > 1 (bow / gun / crossbow can't hit d=1 cardinals).

**About the `<Caster SELF>` marker:** When you see `Tailwind → (8,10)<Ramza SELF>`, the `SELF` tag means "your tile is one of the valid targets" — NOT "this is a self-only ability." Self-targetable buffs like Tailwind / Steel / Salve / Cure / X-Potion can land on you OR on an ally. You can call them either way:
- `battle_ability "Tailwind"` (no coords) → bridge auto-fills your tile (default-self), since the marker promises it's a valid target.
- `battle_ability "Tailwind" 5 4` → cast on the ally at (5,4).
True self-only abilities (Shout, Focus, Chakra) have `R:Self` in the rendered ability line — those NEVER take coords.

**Choosing a target:** Pick from the ability's valid targets in scan_move. For AoE, use the `best:` line to find the placement that hits the most enemies with fewest allies caught. For point-target, pick tiles marked with `*`.

### 4. End turn

```bash
battle_wait         # end turn, auto-faces optimal direction, waits for next friendly turn
```

`battle_wait` handles everything: navigates to Wait in the menu, faces the optimal direction (minimizes enemies at your back using arc-based threat scoring), holds Ctrl to fast-forward through enemy/ally turns, and returns when it's your turn again.

**Note:** Auto-facing only works after a move (`battle_move`), because the rotation is detected empirically during grid navigation. If you skip the move and just wait, it accepts the default facing direction.

The `RecommendedFacing` in scan_move shows you the recommended direction before you act, with arc counts (front/side/back) so you can understand the reasoning.

## Waiting for Other Turns

`battle_wait` auto-waits through enemy/ally turns. When you see `BattleAlliesTurn` or `BattleEnemiesTurn`, poll `screen` until `BattleMyTurn` returns.

## Why might a skillset show no abilities?

The `primary=X secondary=Y` sub-line on the screen header lists the active unit's two equipped skillsets. The ability dump below lists every ability LEARNED in those skillsets. If you see `primary=Speechcraft` but no Speechcraft abilities, the unit hasn't spent JP on any of them yet — the skillset is equipped but empty. Generic recruits often start with zero learned abilities in their primary; the secondary may have more if they've used it before. Spend JP via `open_eqa <unit>` between battles to learn abilities.

## Multi-unit party turn-cycling

With multiple player units, each takes their own turn in CT order. After `battle_wait` or `execute_turn`, the next `BattleMyTurn` may be a **different** unit with different stats, abilities, and starting position — not the one you just moved.

When the active unit changes, the response prepends a loud banner to `Info`:

```
=== TURN HANDOFF: Kenrick(Thief) → Lloyd(Orator) (10,9) HP=432/432 ===
```

Treat this as a hard reset: the prior unit's `validPaths`, ability list, position, and reachable tiles are stale. Rescan / re-plan for the new active unit before issuing further commands. Same-unit returns (rare, e.g. only one player unit on the field) emit no banner.

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
- **`scan_move` shows attack range from your CURRENT position, not post-move.** If the scan reports `Attack → (no targets in range)` and an enemy is several tiles away, try `battle_move` first to close the distance, then re-scan: targets that were out of bow/spell range from `(8,10)` may be in range from `(4,10)`. The bridge doesn't pre-compute "best post-move attack tile" — that's the agent's call to make. Same applies to AoE centers, ability range, and the `Attack tiles:` cardinal panel.
