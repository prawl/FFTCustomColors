# FFT Party Management, Recruiting, and Progression

AI-player reference for Final Fantasy Tactics (War of the Lions). Covers roster, recruiting, stats, builds, and economy.

## Roster and Deployment Limits

| Aspect | Limit | Notes |
|--------|-------|-------|
| Roster size (PSX) | 16 units | Must dismiss to recruit more |
| Roster size (WotL/PSP) | 24 units | Fits all story chars + 6 generics |
| Roster size (Ivalice Chronicles/Steam) | 50 units | Greatly expanded |
| Deploy per battle | 4-5 units | Most battles allow 5; some story battles restrict to 3-4 |
| Ramza | Always required | Cannot be removed from story battles |

## Formation Screen

- Before each battle, choose which units to deploy on blue starting tiles.
- Place units by selecting a unit then choosing a tile. Space confirms, Enter starts battle.
- Unit facing can be set during placement (matters for back-attack vulnerability).
- Guest/story NPCs auto-deploy and do not consume a player slot.

## Recruiting Units

| Method | When Available | Details |
|--------|---------------|---------|
| Warriors' Guild (Outfitter) | Chapter 1+ | Hire generic units at current story level. Costs ~500-1500 gil. |
| Orator "Entice" | Orator job unlocked | Recruit enemy humans in battle (not bosses/story chars). |
| Monster recruitment | Orator + "Tame" | Recruit monsters; requires Orator with Beast Tongue support ability. |
| Story characters | Automatic | Agrias (Ch2), Mustadio (Ch2), Orlandu (Ch4), etc. join via plot. |

## Brave and Faith

These two hidden stats profoundly affect unit performance.

### What They Do

| Stat | Affects | High Value Good For | Low Value Good For |
|------|---------|--------------------|--------------------|
| Brave | Physical reaction triggers, Bare Fist damage, treasure find rate | Physical fighters, reaction-based builds | Nothing (avoid low Brave) |
| Faith | Magic damage dealt AND received, healing received | Mages, healers | Magic-immune physical tanks |

### Permanent Changes

In-battle Brave/Faith changes are **1/4 permanent**: for every 4 points changed in battle, 1 point sticks after battle.

| Ability | Job | Effect | Permanent per cast |
|---------|-----|--------|-------------------|
| Steel (Cheer Up) | Ramza Ch2+ | +5 Brave, 100% hit | +1.25 per cast |
| Praise | Orator | +4 Brave, can miss | +1 per cast |
| Preach | Orator | +4 Faith, can miss | +1 per cast |
| Intimidate | Orator | -20 Brave | -5 per cast |
| Enlighten | Orator | -20 Faith | -5 per cast |

### Optimal Target Values

| Build | Brave | Faith | Why |
|-------|-------|-------|-----|
| Physical DPS | 97 | 03 | Max physical, immune to magic |
| Mage / Healer | 70 | 90-94 | Max magic power; 95+ = unit leaves party |
| Balanced | 97 | 50 | Good reactions, moderate magic resistance |

### Danger Thresholds

- **Brave <= 5 (permanent)**: Unit leaves party forever.
- **Brave < 10 (in battle)**: Chicken status -- unit flees uncontrollably.
- **Faith > 94 (permanent)**: Unit leaves party to "live a life of religious devotion."
- Story characters (Ramza, Agrias, etc.) are immune to leaving.

## Job Leveling Strategy

### Priority Abilities (Learn First on Everyone)

| Ability | Job | JP Cost | Why |
|---------|-----|---------|-----|
| JP Boost | Squire | 250 | +50% JP gain on all actions. Learn ASAP. |
| Throw Stone | Squire | 90 | Free ranged JP farm tool. |
| Potion/Phoenix Down | Chemist | 30/90 | Emergency healing/revival for any unit. |

### Stat Growth by Job (Level-Up Matters)

Leveling in a job permanently affects stat growth. Level in jobs with strong growth.

| Role | Best Leveling Jobs | Key Stats |
|------|--------------------|-----------|
| Physical | Monk, Knight, Ninja | HP, PA, Speed |
| Magical | Summoner, Black Mage, Time Mage | MP, MA |
| Speed | Ninja, Thief | Speed (most important stat in game) |

### Recommended Job Progression

**Physical units:** Squire (JP Boost) -> Knight Lv2 -> Monk (main damage Ch1-2) -> Thief Lv3 -> Ninja (endgame DPS)

**Mage units:** Chemist (items) -> Black Mage Lv2 -> Time Mage (Haste) -> Summoner (AoE)

**Support/Orator:** Chemist -> White Mage Lv2 -> Mystic Lv3 -> Orator (Brave/Faith manipulation)

## Party Composition

### Early Game (Chapter 1)

| Slot | Job | Secondary | Support | Notes |
|------|-----|-----------|---------|-------|
| 1 | Ramza (Squire) | Items | JP Boost | Frontline + healing |
| 2 | Knight | Items | JP Boost | Tank, Break abilities |
| 3 | Monk | Items | JP Boost | Best early DPS |
| 4 | Black Mage | Items | JP Boost | AoE damage |
| 5 | Chemist/Archer | -- | JP Boost | Ranged support |

### Mid Game (Chapter 2-3)

- Ramza with Steel to pump Brave on team every battle.
- 1 Monk or Knight as physical anchor.
- 1 Time Mage (Haste is the strongest buff in the game).
- 1 Summoner or Black Mage for AoE.
- 1 flex slot: Thief (steal gear), Orator (recruit/stats), or story character.

### Late Game (Chapter 4)

- Orlandu (Thunder God Cid) trivializes most content. Always deploy.
- Agrias with Excalibur for Holy Sword damage.
- Ninja with Dual Wield for highest physical DPS.
- Arithmetician (Calculator) for instant full-map magic (broken).
- Ramza as support/DPS hybrid.

## Key Archetypes

| Archetype | Job | Secondary | Reaction | Support | Movement |
|-----------|-----|-----------|----------|---------|----------|
| Physical DPS | Ninja | Monk (Martial Arts) | First Strike | Dual Wield | Move+2 |
| Mage Nuke | Black Mage | Summon | -- | Magic AttackUP | Teleport |
| Healer | White Mage | Items | Auto-Potion | Arcane Defense | Move+2 |
| Tank | Knight | Monk (Martial Arts) | Parry | Equip Shield | Move+1 |
| Speed Support | Time Mage | White Magic | -- | Swiftness | Teleport |
| Cheese Build | Arithmetician | Summon/Black | -- | -- | Move+3 |

## Gil Management

| Source | When | Notes |
|--------|------|-------|
| Random battles | Always | Higher level = more gil. Main income source. |
| Sell loot/equipment | Always | Sell duplicates and outdated gear. |
| Steal (Thief) | Thief unlocked | Steal equipment from enemies, sell it. Very profitable. |
| Errands (Propositions) | Chapter 2+ | Send idle roster units on errands from taverns for passive income. |
| Poaching | Chapter 3+ (Fur Shop) | Kill monsters with Poach equipped; sell at Fur Shop. |

### Spending Priority

1. Weapons (biggest damage increase per gil).
2. JP Boost learned on all 5 main units.
3. Armor for frontline units.
4. Accessories (Hermes Shoes = Speed +1, top priority when available).
5. Items (keep 10+ Phoenix Down and Hi-Potion stocked).

## Poaching System

**Setup:** Learn Poach from Thief (Lv1, costs 200 JP). Equip as support ability.

**How it works:** When a unit with Poach equipped kills a monster, the monster vanishes and its carcass appears at the Fur Shop (Poacher's Den) in trade cities (Dorter, Warjilis, Sal Ghidos). Available from Chapter 3.

**Drop rates:** Common item = 7/8 chance. Rare item = 1/8 chance.

**Notable poach targets:**

| Monster | Common Drop | Rare Drop |
|---------|-------------|-----------|
| Pig (Porky) | Maiden's Kiss | Hairband |
| Swine | Chantage (best perfume) | Nagnarock |
| Wild Boar | Ribbon | Fallingstar Bag |
| Red Chocobo | Remedy | Barette |
| Behemoth | Guardian Bracelet | Pantherskin Bag |
| Hydra | Blood Sword | Scorpion Tail |

## Saving Strategy

- **Save before every story battle.** Some are extremely difficult spikes (Riovanes Castle).
- **Use multiple save slots.** At least 2 rotating slots to avoid soft-locks.
- **Never save with only one slot before a multi-battle sequence** (Riovanes, Orbonne).
- **Save at the world map**, not inside battle preparation, to keep retreat option open.
