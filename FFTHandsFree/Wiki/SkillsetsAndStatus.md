# Skillset IDs and Inflict Status Table

Source: FFHacktics Wiki.

## Skillset IDs

Each job references a skillset by ID. Skillset entries are 25 bytes (0x19): 3 flag bytes + 16 action ability slots + 6 R/S/M slots.

### Generic Job Skillsets

| ID | PSX Name / WotL Name |
|----|---------------------|
| 05 | Basic Skill / Fundaments (Squire) |
| 06 | Item / Items (Chemist) |
| 07 | Battle Skill / Arts of War (Knight) |
| 08 | Charge / Aim (Archer) |
| 09 | Punch Art / Martial Arts (Monk) |
| 0A | White Magic / White Magicks |
| 0B | Black Magic / Black Magicks |
| 0C | Time Magic / Time Magicks |
| 0D | Summon Magic / Summon |
| 0E | Steal (Thief) |
| 0F | Talk Skill / Speechcraft (Orator) |
| 10 | Yin Yang Magic / Mystic Arts |
| 11 | Elemental / Geomancy |
| 12 | Jump (Dragoon) |
| 13 | Draw Out / Iaido (Samurai) |
| 14 | Throw (Ninja) |
| 15 | Math Skill / Arithmeticks |
| 16 | Sing / Bardsong |
| 17 | Dance |
| 18 | Mimic |

### Special Character Skillsets

| ID | Name |
|----|------|
| 19-1C | Guts / Mettle (Ramza Ch1-Ch4) |
| 1D | Holy Sword (Agrias) |
| 1E | Mighty Sword / Unyielding Blade (Meliadoul) |
| 20 | Dark Sword / Fell Sword (Gaffgarion) |
| 25-26 | Snipe / Aimed Shot (Mustadio) |
| 29 | Limit (Cloud) |
| 2A | White-aid / Priest Magicks |
| 2B | Dragon (Reis) |
| 2D | Truth / Sky Mantra (Rapha) |
| 2E | Un-truth / Nether Mantra (Marach) |
| 2F | Starry Heaven / Astrology (Orran) |
| 45 | Magic Sword / Spellblade (Beowulf) |
| 46 | Sword Skill / Swordplay (Orlandeau) |
| 4B | Destroy Sword / Blade of Ruin (Dark Knight) |

### Built-In Skillsets

| ID | Name |
|----|------|
| 01 | Attack |
| 02 | Defend |
| 03 | Equip Change |

## Skillset Data Layout (25 bytes per entry)

| Offset | Size | Field |
|--------|------|-------|
| 0x00 | 1 | Ability flags 1-8 (bit = add 0x100 to ability ID in corresponding slot) |
| 0x01 | 1 | Ability flags 9-16 |
| 0x02 | 1 | R/S/M flags (0x80=slot1 +0x100, etc.) |
| 0x03-0x12 | 16 | Action ability IDs (slots 1-16, low byte only) |
| 0x13-0x18 | 6 | Reaction/Support/Movement ability IDs (slots 1-6) |

## Inflict Status Table

Abilities and weapons reference an Inflict Status ID to determine what status they apply. Three application modes:

- **All or Nothing** — Either all listed statuses apply or none do
- **Cancel** — Removes the listed statuses
- **Separate** — Each status has independent 25% chance
- **Random** — One status chosen at random, 100% if formula allows

### Status Infliction Rules

| Formula Type | Status Hit Rate |
|-------------|----------------|
| Damage only (no hit%) | 25% |
| Hit% only (no damage) | Equal to hit% |
| Hit% + damage | 25% |
| 100% formula | 100% for All/Nothing, 25% each for Separate, 100% for Random |
| NS formula | Cannot inflict status |
| Curative + status | Status must be inflictable for cure to work |

### Key Inflict Status IDs

| ID | Abilities/Items | Mode | Statuses |
|----|----------------|------|----------|
| 01 | Antidote | Cancel | Poison |
| 02 | Eye Drop | Cancel | Blind |
| 03 | Echo Herbs | Cancel | Silence |
| 04 | Maiden's Kiss | Cancel | Frog |
| 05 | Gold Needle | Cancel | Petrify |
| 06 | Holy Water | Cancel | Undead, Vampire |
| 07 | Remedy | Cancel | Petrify, Blind, Confuse, Silence, Oil, Frog, Poison, Sleep |
| 08 | Phoenix Down | Cancel | Dead |
| 20 | Raise, Arise, Revive | Cancel | Dead |
| 21 | Reraise | All/Nothing | Reraise |
| 22 | Regen | All/Nothing | Regen |
| 23 | Protect, Protectja | All/Nothing | Protect |
| 24 | Shell, Shellja | All/Nothing | Shell |
| 25 | Wall, Kiyomori | All/Nothing | Protect + Shell |
| 26 | Esuna, Purification | Cancel | Petrify, Blind, Confuse, Silence, Berserk, Frog, Poison, Sleep, Don't Move, Don't Act |
| 27 | Poison, Venom Fang | All/Nothing | Poison |
| 29 | Death, Suffocate | All/Nothing | Dead |
| 2A | Haste, Hasteja | All/Nothing | Haste |
| 2B | Slow, Slowja | All/Nothing | Slow |
| 2C | Stop, Shadowbind | All/Nothing | Stop |
| 2D | Immobilize, Leg Shot | All/Nothing | Don't Move |
| 2E | Float | All/Nothing | Float |
| 2F | Reflect, Carbuncle | All/Nothing | Reflect |
| 30 | Blind, Eye Gouge | All/Nothing | Darkness |
| 33 | Zombie, Zombie Touch | All/Nothing | Undead |
| 34 | Silence | All/Nothing | Silence |
| 35 | Berserk | All/Nothing | Berserk |
| 36 | Confuse | All/Nothing | Confuse |
| 37 | Dispel, Dispelna | Cancel | Float, Reraise, Transparent, Regen, Protect, Shell, Haste |
| 38 | Disable, Arm Shot | All/Nothing | Don't Act |
| 39 | Sleep | All/Nothing | Sleep |
| 3A | Break, Petrify | All/Nothing | Petrify |
| 3B | Nameless Song | Random | Reraise, Regen, Protect, Shell, Haste |
| 3C | Forbidden Dance | Random | Blind, Confuse, Silence, Frog, Poison, Slow, Stop, Sleep |
| 3D | Doom, Death Sentence | All/Nothing | Death Sentence |
| 3E | Steal Heart, Charm | All/Nothing | Charm |
| 3F | Entice | All/Nothing | Invite |
| 50 | Dispelna | Cancel | Petrify, Confuse, Silence, Vampire, Frog, Poison, Stop, Sleep, Don't Move, Don't Act |
| 51 | Celestial Stasis | All/Nothing | Stop + Don't Move + Don't Act |
| 55 | Vampire | All/Nothing | Blood Suck |
| 5C | Aegis | All/Nothing | Reraise + Regen + Protect + Shell + Haste |
| 64 | Choco Esuna | Cancel | Petrify, Blind, Silence, Poison, Stop, Don't Move, Don't Act |
| 69 | Bad Breath, Parasite | Separate | Petrify, Blind, Confuse, Silence, Oil, Frog, Poison, Sleep |
| 6A | Grand Cross | Separate | Petrify, Blind, Confuse, Silence, Berserk, Frog, Poison, Slow, Sleep |

### Holy Sword Status Effects

| ID | Ability | Mode | Status |
|----|---------|------|--------|
| 75 | Judgment Blade | Separate | Stop |
| 76 | Cleansing Strike | Separate | Death Sentence |
| 77 | Northswain's Strike | Separate | Dead |
| 78 | Hallowed Bolt | Separate | Silence |
| 79 | Divine Ruination | Separate | Confuse |
