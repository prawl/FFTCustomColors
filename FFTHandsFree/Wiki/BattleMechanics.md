# FFT Battle Mechanics & Terrain Reference

## CT (Charge Time) & Turn Order

Every unit starts battle at 0 CT. Each clock tick, a unit gains CT equal to its **Speed** stat. A unit takes its turn when CT reaches **100**.

| Status | CT per tick |
|--------|------------|
| Normal | Speed |
| Haste  | Speed * 3/2 |
| Slow   | Speed / 2 (rounded down) |

**Ticks to act** = 100 / effective Speed. A unit with Speed 10 acts every 10 ticks; Speed 5 acts every 20 ticks.

### CT Cost After Acting

| Actions taken | CT deducted |
|---------------|-------------|
| Move + Act    | 100         |
| Move only OR Act only | 80 |
| Wait (neither)| 60         |

Doing less costs less CT, meaning you get your next turn sooner. Waiting without moving or acting is the fastest way to cycle back.

## Turn Structure

On your turn you may **Move** and **Act** in any order, and both are optional. You must end with **Wait** (choose facing direction). Possible flows:

- Move -> Act -> Wait
- Act -> Move -> Wait
- Move -> Wait (skip action)
- Act -> Wait (skip movement)
- Wait only (costs only 60 CT)

## Facing Direction & Evasion

Facing determines which evasion checks apply when attacked. **This is the single most important tactical consideration for Wait.**

| Attack direction | Evasion checks applied | Effective evasion |
|-----------------|----------------------|-------------------|
| **Front** | Class evade + Shield + Accessory + Weapon parry | Highest |
| **Side** | Shield + Accessory + Weapon parry | Medium |
| **Back** | Accessory only | Very low |

Formula (physical, front): `hit% = base * (100-ClassEv)/100 * (100-ShieldEv)/100 * (100-AccessoryEv)/100 * (100-WeaponEv)/100`
From the back, only accessory evasion applies -- most attacks will land.

**AI priority**: Always face toward the nearest/most dangerous enemy. Minimize exposure of your back to enemy units.

## Height & Elevation

### Movement
- **Move stat**: horizontal tiles a unit can traverse per turn.
- **Jump stat**: max height difference a unit can climb/descend. If elevation difference > Jump, the tile is unreachable.
- Movement abilities (Ignore Height, Teleport, Fly) bypass these restrictions.

### Combat advantages of high ground
- Physical attacks from above can reach targets that cannot counter-attack back (vertical tolerance is ~3 downward but ~2 upward for melee).
- Bows/guns gain effective range from elevation.
- Some reaction abilities (Counter) fail if attacker is above a height threshold relative to target.

### Vertical Tolerance
Abilities have a vertical tolerance (noted as "vX"). A spell with effect "2v3" hits tiles within 2 horizontal range AND within 3 height units of the target tile. Units outside the vertical tolerance are not hit even if within horizontal range.

## Terrain Types

| Terrain | Effect |
|---------|--------|
| Normal ground | No special effect |
| Shallow water | Costs 2 Move points per tile (1 tile = 2 Move); during rain, marsh/swamp also costs 2 |
| Deep water | Cannot act while submerged; requires Move in Water ability |
| Poisonous marsh | Inflicts Poison on entry (1/8 max HP damage per turn end) |
| Lava | Damages units; requires Move on Lava ability to traverse |
| Rooftops/walls | Normal traversal if Jump allows; tactical high ground |

**Float** status prevents all terrain damage (poison water, lava).

## Charge Time Spells

Many magic spells and some abilities (Archer's Aim) have a **charge time** -- a separate hidden CT counter that must fill before the spell resolves.

Key rules:
- Charge CT is based on the **ability's own speed value**, NOT the caster's Speed stat.
- **Haste/Slow do NOT affect** spell charge time (including Jump).
- Caster is **locked in place** while charging. If the caster takes damage, the spell is **cancelled**.
- Spells target a **tile** -- if you target a unit and it moves, the spell hits where the unit *was* (or follows the unit, depending on targeting type).
- Very slow spells can take longer than the caster's next turn, effectively costing 2 turns.

## AoE Targeting

Abilities have **range** (how far you can place the center) and **effect area** (how many tiles it hits from center).

| Shape | Pattern | Examples |
|-------|---------|----------|
| Point | Single tile | Basic attacks, most single-target spells |
| Diamond | Plus/diamond expanding from center | Fire, Blizzard, Thunder series |
| Line | Straight line from caster | Some enemy abilities |
| Cross | X-shape from center | Rare; specific abilities |
| All | Entire map | Certain summons, Math Skill |

Each AoE also has **vertical tolerance** -- units on tiles too far above/below the target elevation are excluded.

## Battle Win/Loss Conditions

**Standard**: Defeat all enemies. Most random encounters and many story battles.

**Special objectives** (story battles):
- Defeat a specific boss unit
- Protect an NPC/guest (they must survive)
- Defeat all enemies AND keep NPC alive

**Loss conditions**:
- Ramza is KO'd and his death counter expires -> instant Game Over
- All player units are KO'd/Stoned/Crystalized -> Game Over

## Guest Units

- **AI-controlled** allies that fight on your side. You cannot give them orders.
- Guests are **immune to permadeath** -- they show stars instead of a death counter when KO'd.
- Guest AI is similar to enemy AI: they pick targets and abilities autonomously (often recklessly).
- Once a guest officially joins your roster, they lose guest protection and CAN permadeath.

**Tactical note**: Protect guests aggressively in "save NPC" battles. They will charge into danger.

## Experience & JP Gain

### EXP Formula
```
EXP = 10 + (Target Level - Actor Level) + Kill Bonus
```
- **Kill bonus**: +10 EXP for the first kill of a target (no bonus for repeat kills of revived units).
- Self-targeting actions: always 10 EXP.
- Higher-level targets give more EXP; lower-level targets give less (can be 0, minimum 1).

### JP Formula
```
JP = 8 + (Job Level * 2) + floor(Character Level / 4)
```
- JP gained goes to the **active job** and a smaller amount to all other unlocked jobs.
- **JP Boost** support ability: +50% JP per action.
- Successful actions grant JP; self-targeting abilities (like Yell/Accumulate) always count as successful.

## Battle Rewards

- **Gil**: Earned at battle end based on enemies defeated.
- **Stealing**: Thief ability; can steal equipment, gil, or EXP from enemies.
- **Poaching**: Thief support ability. Killing a monster with Poach equipped sends its carcass to the Poachers' Den (available Chapter 3+). Common drop: 7/8 chance. Rare drop: 1/8 chance. Poached monsters leave no corpse (no crystal/chest).
- **Treasure/crystals from dead units**: When a unit's death counter expires, they become a crystal (recover HP/abilities) or treasure chest (random item).
- **War Trophies**: Some story battles award unique equipment.
- **Move-Find Item**: Movement ability that finds hidden items on specific tiles.

## Permadeath: The Crystal Countdown

When a unit is KO'd, a **3-count death timer** appears (3 hearts). The counter decreases by 1 each time the KO'd unit's turn would have come up (based on their Speed/CT).

| Counter | Status |
|---------|--------|
| 3 | Just KO'd -- can revive with Phoenix Down, Raise, Revive |
| 2 | Still revivable |
| 1 | Last chance to revive |
| 0 | **Permanently gone** -- becomes Crystal or Treasure Chest |

**Prevention**:
- Revive with Phoenix Down, Raise, Arise, or Revive before counter hits 0.
- Reraise status auto-revives on KO (no counter starts).
- Faster units' counters expire faster (high Speed = less time to save them).

**Ramza special case**: If Ramza crystallizes, it is an immediate **Game Over**. Always prioritize reviving Ramza.

**Guests are immune**: Guest units show stars instead of a counter and cannot permanently die while they have guest status.
