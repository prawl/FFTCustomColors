# Ability Formula Table (FFHacktics Reference)

Every ability uses a Formula ID (byte at Secondary Ability Data offset 0x08). This table maps each ID to its exact calculation. Source: FFHacktics Wiki.

**Legend:** NS=No status infliction, F=Caster Faith * Target Faith / 10000, PE=Physical evasion, ME=Magic evasion, NE=No evasion, Rdm=Random

## Physical / Weapon Formulas

| ID | Formula | Key Abilities | Notes |
|----|---------|---------------|-------|
| 01 | Weapon formula | Basic Attack, Choco Attack, Tackle | Uses equipped weapon's own formula. Elemental. Affected by Charge |
| 03 | WP^2 | (guns) | NS |
| 04 | Magic Gun | Romandan Pistol, Blaze Gun | Casts Fire/Ice/Bolt 1/2/3 by element. Faith-based |
| 05 | Weapon formula | (variant) | |
| 06 | Absorb HP (Weapon) | | NS |
| 07 | Heal (Weapon) | | NS |
| 2D | PA*(WP+Y), 100% status | Judgment Blade, Cleansing Strike, Holy Explosion | Holy Sword skills |
| 2E | Break equip + PA*WP | Shellbust Stab, Hellcry Punch | NS, misses if no equipment |
| 2F | Absorb MP (PA*WP) | Dark Sword | NS PE, harms user vs Undead |
| 30 | Absorb HP (PA*WP) | Night Sword | NS PE, harms user vs Undead |
| 63 | SP*WP | Throw | Throwing items formula |
| 64 | PA*WP (PA*3/2 if spear, PA*Brave/100 if bare) | Jump | NS |

## Magic Formulas

| ID | Formula | Key Abilities | Notes |
|----|---------|---------------|-------|
| 08 | F(MA*Y) | Fire/Fira/Firaga, Blizzard, Thunder, Holy, Flare, Meteor, all Summons, Ultima | Main magic damage formula |
| 09 | (Y/100)% damage, Hit F(MA+X)% | Demi, Demi 2, Lich | Gravity-type percentage damage |
| 0A | Hit F(MA+X)% | Poison, Frog, Slow, Stop, Blind, Sleep, Petrify, Break, Faith, Innocent | Status-only. **Can be evaded even if Evadeable unchecked** — don't use for buffs |
| 0B | Hit F(MA+X)% | Reraise, Regen, Protect, Shell, Haste, Float, Reflect, Wall, Esuna | Buff formula |
| 0C | Heal F(MA*Y) | Cure/Cura/Curaga/Curaja, Moogle, Fairy | NS. Damages Undead (non-elemental) |
| 0D | Heal (Y)%, Hit F(MA+X)% | Raise, Arise | Status must be inflictable for healing. Damages Undead |
| 0E | Dmg (Y)%, Hit F(MA+X)%, 100% status | Death | Status applied first. If immune, no damage. Heals Undead |
| 0F | Absorb MP (Y)%, Hit F(MA+X)% | Spell Absorb, Aspel | NS. Harms user vs Undead |
| 10 | Absorb HP (Y)%, Hit F(MA+X)% | Life Drain, Drain | NS. Harms user vs Undead |
| 12 | Set Quick, Hit F(MA+X)% | Quick | NS. Quick is not a Status |
| 14 | Set Golem, Hit CasFaith/100*(MA+X)% | Golem | NS |
| 15 | Set CT=0, Hit F(MA+X)% | Return 2 | NS |
| 16 | Damage MP (Target current MP), Hit F(MA+X)% | Mute | NS |
| 17 | Dmg (Target HP - 1), Hit F(MA+X)% | Gravi 2 | NS |

## Physical Skill Formulas

| ID | Formula | Key Abilities | Notes |
|----|---------|---------------|-------|
| 24 | (PA+Y)/2 * MA | Pitfall, Water Ball, Local Quake, Demon Fire, Blizzard, Lava Ball | Geomancy |
| 25 | Break equip, Hit (PA+WP+X)% | Head/Armor/Shield/Weapon Break | NS. Else weapon strike |
| 26 | Steal equip, Hit (SP+X)% | Steal Helmet/Armor/Shield/Weapon/Accessory | Miss if no equipment |
| 27 | Steal Gil (Level*SP), Hit (SP+X)% | Gil Taking | NS |
| 28 | Steal Exp, Hit (SP+X)% | Steal Exp | |
| 29 | Opposite sex: Hit (MA+X)% | Steal Heart, Allure | Miss vs same sex. Charm itself ignores sex |
| 2A | Hit (MA+X)%, affect Brave/Faith(Y) | Praise, Threaten, Preach, Enlighten, Negotiate, Persuade | All Talk Skills. Blockable by Finger Guard |
| 2B | Hit (PA+Y)%, -PA/MA/SP(X) | Speed/Power/Mind Break | NS |
| 2C | Damage MP (Y)%, Hit (PA+Y)% | Magic Break | NS |
| 31 | (PA+Y)/2 * PA | Spin Fist, Wave Fist, Earth Slash, Choco Ball | Monk AoE skills |
| 32 | Rdm(1..X) * (PA*3 + Y) | Repeating Fist | NS NE |
| 33 | Hit (PA+X)% | Stigma Magic (Purification) | |
| 34 | Heal PA*Y, HealMP PA*Y/2 | Chakra | NS. Does NOT harm Undead (unique) |
| 35 | Heal (Y)%, Hit (PA+X)% | Revive | Status must be inflictable. Damages Undead |
| 36 | +PA(Y) | Focus (Accumulate) | NS. Self-buff |
| 37 | Rdm(1..Y) * PA | Dash, Throw Stone, Cat Kick | NS. Has innate Knockback. No elements |
| 39 | +SP(Y) | Yell (Tailwind) | NS |
| 3A | +Brave(Y) | Cheer Up (Steel) | NS |
| 3B | +Brave(X) + PA/MA/SP(Y) | Scream (Shout) | NS |
| 3C | Heal (MaxHP*2/5), DmgCaster (MaxHP/5) | Wish, Energy | NS. Always heals even Undead |

## Status / Hit-Rate Formulas

| ID | Formula | Key Abilities | Notes |
|----|---------|---------------|-------|
| 1A | Hit F(MA+Y)%, -PA/MA/SP(X) | Speed/Power/Mind Ruin | NS. Stat-reduction (hardcoded per slot) |
| 1B | Damage MP (Y)%, Hit F(MA+X)% | Magic Ruin | NS |
| 1C | Hit (X)% | Angel Song, Life Song, Battle Song, all Songs | NS. Song slots are hardcoded |
| 1D | Hit (X)% | Witch Hunt, Slow Dance, all Dances | NS. Dance slots are hardcoded |
| 38 | 100% | Seal, Shadow Stitch, Grand Cross, most monster statuses | Separate Status caps at 25% |
| 3D | Hit (MA+X)% | Blaster, Mind Blast, Death Sentence | Accuracy reduced by Magic Defense UP |
| 3F | Hit (SP+X)% | Leg Aim, Arm Aim | Archer Aim skills |
| 40 | Undead only: Hit (SP+X)% | Seal Evil | Miss vs non-Undead |
| 41 | Hit (MA+X)% | Galaxy Stop (Celestial Stasis) | Not Faith-based. 0% vs same Zodiac |
| 50 | Hit (MA+X)% | Secret Fist, Eye Gouge, Poison Nail, Beak | PE. Reduced by Defense UP |
| 51 | Hit (MA+X)% | Choco Esuna, Protect Spirit | Modified only by Zodiac |

## Special / Monster Formulas

| ID | Formula | Key Abilities | Notes |
|----|---------|---------------|-------|
| 1E | (MA+Y)*MA/2, Rdm(1,X) hits | Heaven Thunder, Asura, all Truth skills | Multi-hit. MA-based |
| 1F | (100-CasF)*(100-TarF)*(MA+Y)*MA/2, Rdm(1,X) hits | All "Back" Truth skills | Inverse Faith — low Faith = more damage |
| 20 | MA*Y | Draw Out katana skills | May display "Broken" |
| 42 | PA*Y, self-damage PA*Y/X | Destroy, Compress, Dispose, Crush | NS NE |
| 43 | CasMaxHP - CasCurHP | Shock, Blade Beam, Ulmaguest, Lifebreak | NS. More damage when caster is hurt |
| 44 | Target current MP | Difference | NS |
| 45 | TarMaxHP - TarCurHP | Climhazzard | NS. More damage when target is hurt |
| 47 | Absorb HP (Y)%, 100% status | Blood Suck | Elmdor's version has hardcoded message |
| 4E | MA*Y | Cloud limits, Dragon breaths, Choco Meteor | Main monster/special damage formula |
| 52 | CasMaxHP - CasCurHP, 100% status, self-damage | Self Destruct | NS |
| 5E | (MA+Y)/2*MA, X+1 hits, status | Triple Thunder/Flame, Dark Whisper | Always hits X+1 times (fixed multi-hit) |

## Item Formulas

| ID | Formula | Key Abilities | Notes |
|----|---------|---------------|-------|
| 48 | Heal Z*10 | Potion, Hi-Potion, X-Potion | NS. Z from item table. Harms Undead |
| 49 | HealMP Z*10 | Ether, Hi-Ether | NS. Does NOT harm Undead |
| 4A | Heal 100% HP+MP | Elixir | NS. OHKOs Undead |
| 4B | Heal Rdm(1..9), 100% status | Phoenix Down | Glitchy — heals ~1 HP. OHKOs Undead <999 HP |
