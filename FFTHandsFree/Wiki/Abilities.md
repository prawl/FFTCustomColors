# FFT Abilities Reference

## How Abilities Work

Each unit equips up to 5 ability slots: **2 Action** (one locked to current job), **1 Reaction**, **1 Support**, **1 Movement**. Abilities are learned by spending JP earned in battle. Once learned, an ability can be equipped regardless of current job.

### JP System
- Every action in battle earns JP for the acting unit's current job (8-20 JP per action).
- Nearby allies of the same job earn a smaller amount (~25% of base).
- **JP Boost** (Squire, 250 JP) increases JP earned by ~50%. Learn this first on every unit.
- JP is per-character per-job. Spending JP on one job does not reduce another.

## Key Action Abilities by Job

### Physical Jobs

| Job | Ability | JP | MP | Notes |
|-----|---------|---:|---:|-------|
| Squire | Throw Stone | 100 | - | Ranged 4, knockback |
| Squire | Focus | 200 | - | Self PA+1 permanent per use |
| Squire | Tailwind | 150 | - | Ally Speed+1 permanent |
| Squire | Steel | 200 | - | Ally Brave+5 (temp) |
| Squire | Shout | 600 | - | Self Speed/PA/Brave+1 (Ramza only) |
| Knight | Rend Weapon | 300 | - | Destroy target weapon |
| Knight | Rend Armor | 300 | - | Destroy target armor |
| Knight | Rend Speed | 400 | - | Lower target Speed |
| Knight | Rend Power | 400 | - | Lower target PA |
| Monk | Chakra | 200 | - | AoE heal HP+MP, range 1 |
| Monk | Revive | 320 | - | Resurrect adjacent ally |
| Monk | Aurablast | 400 | - | Ranged attack, line 3 |
| Monk | Shockwave | 300 | - | Line Earth damage |
| Monk | Pummel | 200 | - | Multi-hit melee |
| Thief | Steal Weapon | 500+ | - | Takes equipped weapon |
| Thief | Steal Heart | 200 | - | Charm (opposite gender) |
| Ninja | Throw (various) | 200-400 | - | Throw equipped weapon types for damage |
| Samurai | Kiyomori | 400 | - | AoE Protect+Shell on allies |
| Samurai | Masamune | 600 | - | AoE Haste+Regen on allies |
| Dragoon | Jump (H1-H8) | 200-800 | - | Jump attacks, untargetable while airborne |

### Magic Jobs

| Job | Ability | JP | MP | Notes |
|-----|---------|---:|---:|-------|
| White Mage | Cure | 50 | 6 | Single heal ~80 HP |
| White Mage | Cura | 100 | 10 | AoE heal |
| White Mage | Curaga | 200 | 16 | Strong AoE heal |
| White Mage | Raise | 200 | 10 | Revive partial HP |
| White Mage | Arise | 400 | 20 | Revive full HP |
| White Mage | Reraise | 420 | 16 | Auto-revive buff |
| White Mage | Protect/Shell | 70 | 6 | Halve phys/mag damage |
| White Mage | Holy | 600 | 56 | Massive holy damage |
| White Mage | Esuna | 150 | 18 | Cure most status |
| Black Mage | Fire/Blizzard/Thunder | 50 | 6 | Tier 1 elemental, AoE 2 |
| Black Mage | Fira/Blizzara/Thundara | 160 | 12 | Tier 2 elemental |
| Black Mage | Firaga/Blizzaga/Thundaga | 400 | 24 | Tier 3 elemental |
| Black Mage | Flare | 600 | 60 | Non-elemental, single target |
| Time Mage | Haste | 100 | 8 | Speed up ally |
| Time Mage | Slow | 80 | 8 | Slow enemy |
| Time Mage | Stop | 330 | 14 | Freeze enemy |
| Time Mage | Quick | 800 | 24 | Grant extra turn |
| Time Mage | Meteor | 1500 | 70 | Random AoE massive damage |
| Summoner | Golem | 400 | 40 | Party-wide phys shield |
| Summoner | Bahamut | 1000 | 60 | Massive AoE damage |
| Summoner | Carbuncle | 200 | 30 | Party Reflect |
| Mystic | Hesitation | 300 | 10 | Inflict Don't Act |
| Mystic | Induration | 500 | 16 | Inflict Petrify |
| Orator | Entice | 400 | - | Recruit enemy (humans) |
| Orator | Mimic Darlavon | 500 | - | AoE Sleep |
| Arithmetician | (formulas) | 200-350 | - | Cast any learned spell instantly, no MP, full map AoE |

### Special: Arithmetician
Arithmetician combines a **targeting formula** (CT, Level, EXP, Height) with a **number** (Prime, 3, 4, 5) to cast any learned spell on all matching units for free. Extremely powerful but slow unit speed.

## All Reaction Abilities

| Ability | Job | JP | Effect |
|---------|-----|---:|--------|
| Counter Tackle | Squire | 180 | Counter adjacent phys with Rush |
| Parry | Knight | 200 | Block phys attacks with weapon |
| Counter | Monk | 300 | Counter phys with standard Attack |
| First Strike | Monk | 1300 | Preemptive strike vs adjacent human attacker |
| Critical: Recover HP | Monk | 500 | Full HP restore when critical |
| Auto-Potion | Chemist | 400 | Auto-use potion from inventory when hit |
| Cup of Life | Arithmetician | 200 | Excess healing distributed to all allies |
| Soulbind | Arithmetician | 300 | Return half damage taken, heal same amount |
| Absorb MP | Mystic | 250 | Recover MP equal to spell MP used on you |
| Nature's Wrath | Geomancer | 300 | Counter with Geomancy skill |
| Archer's Bane | Archer | 450 | High evasion vs ranged attacks |
| Speed Surge | Archer | 900 | Permanently +1 Speed when hit (battle only) |
| Sticky Fingers | Thief | 200 | Catch thrown items, take no damage |
| Gil Snapper | Thief | 200 | Gain gil equal to damage taken |
| Vigilance | Thief | 200 | Evasion boost after being hit |
| Reflexes | Ninja | 400 | Greatly increase all evasion rates |
| Vanish | Ninja | 1000 | Gain Invisible when hit |
| Shirahadori | Samurai | 700 | Block phys = Brave% (up to 97%) |
| Bonecrusher | Samurai | 200 | Counter in critical with max-HP-based damage |
| Dragonheart | Dragoon | 600 | Gain Reraise when hit |
| Earplugs | Orator | 300 | Resist Speechcraft |
| Regenerate | White Mage | 400 | Gain Regen when hit |
| Magick Counter | Black Mage | 800 | Counter spell with same spell |
| Mana Shield | Time Mage | 400 | Damage taken from MP instead of HP |
| Critical: Quick | Time Mage | 800 | Instant next turn when critical |
| Critical: Recover MP | Summoner | 400 | Full MP restore when critical |
| Magick Surge | Bard | 500 | +MA when hit (stacks, battle only) |
| Faith Surge | Bard | 700 | +Faith when hit by magic (battle only) |
| Strength Surge | Dancer | 600 | +PA when hit (stacks, battle only) |
| Bravery Surge | Dancer | 700 | +Brave when hit (battle only) |

**Activation chance = Brave%** (except Parry/Reflexes which are passive evasion boosts).

## All Support Abilities

| Ability | Job | JP | Effect |
|---------|-----|---:|--------|
| JP Boost | Squire | 250 | +50% JP earned |
| Equip Axes | Squire | 170 | Equip axes on any job |
| Beastmaster | Squire | 200 | Adjacent monsters gain hidden ability |
| Reequip | Chemist | 50 | Change equipment mid-battle |
| Safeguard | Chemist | 250 | Equipment cannot be stolen/broken |
| Throw Items | Chemist | 350 | Use items at range 4 |
| Equip Shields | Knight | 250 | Equip shields on any job |
| Equip Swords | Knight | 400 | Equip swords on any job |
| Equip Heavy Armor | Knight | 500 | Equip helms+armor on any job |
| Brawler | Monk | 200 | Stronger barehanded attacks |
| Equip Crossbows | Archer | 350 | Equip crossbows on any job |
| Concentration | Archer | 400 | Ignore target evasion (phys) |
| Poach | Thief | 200 | Kill monsters to get items |
| Beast Tongue | Orator | 100 | Use Speechcraft on monsters |
| Tame | Orator | 500 | Recruit critical monsters |
| Equip Guns | Orator | 800 | Equip guns on any job |
| Defense Boost | Mystic | 400 | Reduce phys damage taken |
| Attack Boost | Geomancer | 400 | +33% physical damage |
| Equip Polearms | Dragoon | 400 | Equip polearms on any job |
| Equip Katana | Samurai | 400 | Equip katana on any job |
| Doublehand | Samurai | 900 | Two-hand a 1H weapon for +2x damage |
| Dual Wield | Ninja | 1000 | Wield weapon in each hand |
| Magick Defense Boost | White Mage | 400 | Reduce magic damage taken |
| Magick Boost | Black Mage | 400 | +33% magic damage dealt |
| Swiftspell | Time Mage | 1000 | Reduce spell cast time |
| Halve MP | Summoner | 1000 | Half MP cost for all spells |
| EXP Boost | Arithmetician | 350 | +50% EXP earned |
| Evasive Stance | Squire | 50 | Adds Evasive Stance command (high evasion until next turn) |

## All Movement Abilities

| Ability | Job | JP | Effect |
|---------|-----|---:|--------|
| Move +1 | Squire | 200 | +1 Move |
| Move +2 | Thief | 560 | +2 Move |
| Move +3 | Bard | 1000 | +3 Move |
| Jump +1 | Archer | 200 | +1 Jump |
| Jump +2 | Thief | 500 | +2 Jump |
| Jump +3 | Dancer | 600 | +3 Jump |
| Ignore Elevation | Dragoon | 700 | Move to any height |
| Fly | Bard/Dancer | 900 | Fly over obstacles and enemies |
| Teleport | Time Mage | 3000 | Warp anywhere (may fail if out of range) |
| Lifefont | Monk | 300 | Heal HP when moving |
| Manafont | Mystic | 350 | Recover MP when moving |
| Treasure Hunter | Chemist | 100 | Find hidden items on tiles |
| Waterwalking | Ninja | 420 | Walk on water |
| Lavawalking | Geomancer | 150 | Walk on lava |
| Ignore Weather | Mystic | 200 | Move through weather terrain |
| Ignore Terrain | Geomancer | 220 | Move through water/rivers |
| Swim | Samurai | 300 | Enter deep water |
| Levitate | Time Mage | 540 | Permanent Float in battle |
| Accrue EXP | Arithmetician | 400 | Earn EXP when moving |
| Accrue JP | Arithmetician | 400 | Earn JP when moving |

## Must-Have Abilities (Priority Order)

### Every Unit Should Learn
1. **JP Boost** (Squire, 250 JP) -- Learn first on everyone, doubles progression speed
2. **Auto-Potion** (Chemist, 400 JP) -- Automatic healing keeps units alive
3. **Move +2** (Thief, 560 JP) or **Move +3** (Bard, 1000 JP) -- Mobility wins battles

### Best Reaction Abilities
- **Shirahadori** (Samurai, 700 JP) -- Near-immune to physical attacks at high Brave (97% block)
- **Reflexes** (Ninja, 400 JP) -- Great all-around evasion boost
- **Auto-Potion** (Chemist, 400 JP) -- Best early-game survival
- **Dragonheart** (Dragoon, 600 JP) -- Free Reraise when hit

### Best Support Abilities
- **Dual Wield** (Ninja, 1000 JP) -- Double attacks, strongest physical DPS option
- **Attack Boost** (Geomancer, 400 JP) -- +33% physical damage
- **Magick Boost** (Black Mage, 400 JP) -- +33% magic damage
- **Halve MP** (Summoner, 1000 JP) -- Essential for casters
- **Concentration** (Archer, 400 JP) -- Never miss physical attacks

### Best Movement Abilities
- **Move +2/+3** -- Raw mobility is king
- **Teleport** (Time Mage, 3000 JP) -- Best late-game, ignores all terrain
- **Fly** (Bard/Dancer, 900 JP) -- Ignores obstacles and height

### Overpowered Combos
- **Ninja + Dual Wield + Attack Boost** -- Highest physical DPS in the game
- **Arithmetician secondary + Halve MP** -- Instant full-map free spells on any job
- **Monk + Shirahadori + Move +3** -- Unkillable melee with self-heal via Chakra
- **Samurai Masamune + White Mage secondary** -- Party Haste+Regen+Healing
