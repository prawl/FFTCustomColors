# FFT Status Effects Reference

## KO / Death / Crystal System

When a unit reaches 0 HP, it is **KO'd** and a **death counter starts at 3**. The unit's CT continues to tick; each time it would get an active turn, the counter drops by 1 instead. When the counter reaches **0**, the unit becomes a **crystal** or **treasure chest** and is **permanently lost**.

- **Crystal**: Another unit can step on it to either learn the dead unit's abilities OR fully restore own HP/MP.
- **Treasure Chest**: Contains a random piece of the dead unit's equipped gear.
- **Ramza dying = instant Game Over** (even if other units live).
- Revival resets the counter. Use **Raise**, **Arise**, or **Phoenix Down** before the counter hits 0.

| Revival Method | HP Restored | Notes |
|---|---|---|
| Phoenix Down (item) | 1 HP | Chemist's Items, or throw from inventory |
| Raise (White Magick) | ~50% max HP | Has cast time, can miss |
| Arise (White Magick) | 100% max HP | Long cast time |
| Reraise (pre-applied) | 10% max HP | Auto-revives when CT reaches 100 |

---

## Negative Status Effects

| Status | Ticks | Effect | Cure |
|---|---|---|---|
| **Poison** | 36 | Takes 1/8 max HP damage at end of each turn. Cancels Regen. | Antidote, Remedy, Esuna |
| **Blind** | Permanent | Physical attacks have doubled miss rate (target evasion x2). | Eye Drops, Remedy, Esuna |
| **Silence** | 36 | Cannot cast any magick (White, Black, Time, Summon, Mystic Arts, etc). | Echo Herbs, Remedy, Esuna |
| **Confuse** | Until cured | Unit takes random actions, may attack allies. Uncontrollable. | Take any HP damage, Remedy, Esuna |
| **Berserk** | Until cured | +50% PA, but auto-attacks nearest unit. No reaction abilities. Uncontrollable. | Esuna |
| **Toad** | Until cured | Can only use basic Attack (weak). Takes 1.5x physical damage. No reactions. | Maiden's Kiss, Remedy, Esuna, Toad spell |
| **Petrify (Stone)** | Until cured | Completely frozen. Cannot gain CT. Treated as dead for win/lose checks. Immune to damage. | Golden Needle, Remedy, Esuna |
| **Stop** | 20 | Frozen in place. No CT gain, no evasion, no reactions. | Wears off. Choco Esuna |
| **Sleep** | 60 | No CT gain, no evasion, no reactions. Takes +50% physical damage. | Take any HP damage, Remedy, Esuna |
| **Immobilize (Don't Move)** | 24 | Cannot move but can still act. | Esuna |
| **Disable (Don't Act)** | 24 | Cannot act or use reactions. Can still move. | Esuna |
| **Slow** | 32 | CT gain rate halved. Cancels Haste. | Haste spell |
| **Charm** | 32 | Treats allies as enemies and vice versa. AI-controlled. | Take any HP damage |
| **Doom (Death Sentence)** | 3 turns | Counter above head counts down each active turn. KO'd when it hits 0. | Choco Esuna, Purification (Monk). Reraise will trigger on KO. |
| **Oil** | Until cured | Takes double damage from Fire-element attacks. | Remedy. Removed after taking fire damage. |
| **Undead** | Until cured | Healing damages, damage heals. Revives after KO timer instead of crystal. | Holy Water |
| **Vampire (Blood Suck)** | Until cured | Can only use Vampire attack. Evasion = 0. No reactions. Monsters immune. | Holy Water |
| **Chicken** | Until Brave >= 10 | Triggered when Brave drops below 10. Runs from enemies. Restores 1 Brave/turn. | Raise Brave above 10 |
| **Traitor** | Permanent | Unit joins enemy side. Cannot be cured in battle. | None |
| **Faith (debuff context)** | 32 | Treated as 100 Faith. Takes MORE magick damage while also dealing more. | Wears off, Dispel |
| **Innocent (Atheist)** | 32 | Treated as 0 Faith. All magick ineffective on and from this unit. | Wears off (cannot be cured) |

---

## Positive Status Effects

| Status | Ticks | Effect | Applied By |
|---|---|---|---|
| **Protect** | 32 | Reduces physical damage taken by 1/3. | Protect spell, equipment |
| **Shell** | 32 | Reduces magick damage taken by 1/3. Also reduces status spell hit rate. | Shell spell, equipment |
| **Haste** | 32 | +50% CT gain rate. Cancels Slow. | Haste spell, equipment |
| **Regen** | 32 | Heals 1/8 max HP at end of each turn. Cancels Poison. | Regen spell, equipment |
| **Reraise** | Permanent | Auto-revive at 10% max HP when KO'd. One use, then removed. | Reraise spell |
| **Float** | Permanent | Immune to Earth damage. Can cross water/lava freely. | Float spell, equipment |
| **Invisible (Transparent)** | Until action | All enemy evasion ignored when attacking. AI ignores invisible units. Removed on any action. | Vanish spell |
| **Reflect** | 32 | Bounces magick to the opposite tile. Affects both ally and enemy spells. | Reflect spell, equipment |
| **Faith (buff context)** | 32 | Treated as 100 Faith. Magick power maximized. | Faith spell |

---

## Status Interactions and Cancellations

| Applying... | Cancels... |
|---|---|
| Poison | Regen |
| Regen | Poison |
| Haste | Slow |
| Slow | Haste |
| Faith | Innocent |
| Innocent | Faith |
| Sleep | Confusion (unit can't act randomly) |
| Petrify | Most other statuses (frozen state overrides) |

---

## Key Cure Methods Summary

| Cure Method | Statuses Removed |
|---|---|
| **Esuna** (White Magick) | Most ailments: Poison, Blind, Silence, Confuse, Berserk, Toad, Petrify, Sleep, Immobilize, Disable |
| **Remedy** (Item) | Petrify, Blind, Confuse, Silence, Oil, Toad, Poison, Sleep |
| **Holy Water** (Item) | Undead, Vampire |
| **Purification / Stigma Magic** (Monk) | Same statuses as Esuna; affects user and adjacent allies |
| **Choco Esuna** (Chocobo) | Removes most statuses including Stop and Doom |
| **Take HP damage** | Sleep, Confuse, Charm |
| **Dispel** (spell) | Removes positive buffs: Protect, Shell, Haste, Regen, Float, Reraise, Reflect, Invisible |

---

## Equipment-Based Status Immunity

| Equipment | Immunities Granted |
|---|---|
| **Ribbon** | Most ailments (Instant KO, Undead, Petrify, Traitor, Blind, Confuse, Silence, Vampire, Berserk, Toad, Poison, Slow, Stop, Charm, Sleep, Don't Move, Don't Act, Death Sentence) |
| **Barette** | Dead, Petrify, Confuse, Vampire, Berserk, Stop, Charm, Traitor, Sleep |
| **108 Gems (Japa Mala)** | Undead, Toad, Vampire, Poison |
| **Jujitsu Gi** | Instant KO immunity, +1 PA |
| **Sorted equipment** | Various pieces grant Float, Reraise, Protect, Shell, Haste innately |

**Support Abilities for Immunity:**
- **Safeguard** -- Prevents equipment breaking/stealing (Knight skills)
- **A Save** -- Reaction ability that can block status infliction

---

## AI Decision Notes

- **Priority threats**: Petrify, Stop, Charm, Confuse, Death Sentence (unit loss risk)
- **Manageable**: Poison, Blind, Silence, Slow (reduce effectiveness but not catastrophic)
- **Sleep is dangerous** on high-value units (60 ticks is very long, but any damage cures it)
- **Berserk** on your own melee units can be beneficial (+50% PA) if positioning allows
- **Reraise** is the best insurance -- apply to Ramza and key units before risky fights
- **Undead** units are healed by Phoenix Down in reverse (it damages them instead)
- When a unit has **Doom**, prioritize finishing the fight or applying Reraise rather than curing
- **Oil + Fire** combo deals massive damage; remove Oil with Remedy immediately
