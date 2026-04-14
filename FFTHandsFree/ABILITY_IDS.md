<!-- This file should not be longer than 200 lines, if so prune me. -->
# Ability & Equipment IDs (IC Remaster)

Roster field offsets: reaction +0x08, support +0x0A, movement +0x0C.
Bytes +0x09, +0x0B, +0x0D = 0x01 when corresponding slot is equipped.

## Action ability metadata
- **Player action abilities** — full metadata (range, AoE, target, element, effect, MP cost, cast speed, added effects, reflectable, arithmetickable) is in `ColorMod/GameBridge/ActionAbilityLookup.cs`. Skillsets: Mettle, Fundaments, Arts of War, Aim, Martial Arts, White/Black/Time Magicks, Mystic Arts, Summon, Steal, Speechcraft, Geomancy, Jump, Iaido, Throw, Items, Arithmeticks, Bardsong, Dance, Darkness (Dark Knight), plus Story character primaries.
- **Monster action abilities** — fixed per class. `MonsterAbilities.cs` maps class name → ability list. `MonsterAbilityLookup.cs` provides full metadata for ~60 monster abilities (Tackle, Pickaxe, Talon Dive, Tentacles, etc). Abilities are elemental-typed and include added status effects. ~47 monster classes covered.
- Monsters verified empirically: two units of the same class with different HP/level have identical ability loadouts. Tier progression does NOT strictly add abilities — e.g. Sekhret (Bull tier 2) has 3 abilities but Minotaur (tier 3) only has 2 with a different second ability.

## Secondary Ability Index (+0x07)

Stores index into character's personal unlocked ability list (NOT a universal ID). Story characters with unique primary abilities shift indices.

| Idx | Ability | Job |
|-----|---------|-----|
| 5 | Fundaments | Squire |
| 6 | Items | Chemist |
| 7 | Arts of War | Knight |
| 8 | Aim | Archer |
| 9 | Martial Arts | Monk |
| 10 | White Magicks | White Mage |
| 11 | Black Magicks | Black Mage |
| 12 | Time Magicks | Time Mage |
| 13 | Summon | Summoner |
| 14 | Steal | Thief |
| 15 | Speechcraft | Orator |
| 16 | Mystic Arts | Mystic |
| 17 | Geomancy | Geomancer |
| 18 | Jump | Dragoon |
| 19 | Iaido | Samurai |
| 20 | Throw | Ninja |
| 21 | Arithmeticks | Arithmetician |
| 22 | Bardsong/Dance | Bard(M)/Dancer(F) |

## Reaction Abilities (+0x08)

| ID | Hex | Ability | ID | Hex | Ability |
|----|-----|---------|----|-----|---------|
| 167 | A7 | Magick Surge | 183 | B7 | Gil Snapper |
| 168 | A8 | Speed Surge | 185 | B9 | Auto-Potion |
| 169 | A9 | Vanish | 186 | BA | Counter |
| 170 | AA | Vigilance | 188 | BC | Cup of Life |
| 171 | AB | Dragonheart | 189 | BD | Mana Shield |
| 172 | AC | Regenerate | 190 | BE | Soulbind |
| 174 | AE | Faith Surge | 191 | BF | Parry |
| 175 | AF | Crit: Recover HP | 192 | C0 | Earplugs |
| 176 | B0 | Crit: Recover MP | 193 | C1 | Reflexes |
| 177 | B1 | Crit: Quick | 194 | C2 | Sticky Fingers |
| 178 | B2 | Bonecrusher | 195 | C3 | Shirahadori |
| 179 | B3 | Magick Counter | 196 | C4 | Archer's Bane |
| 180 | B4 | Counter Tackle | 197 | C5 | First Strike |
| 181 | B5 | Nature's Wrath |
| 182 | B6 | Absorb MP |

## Support Abilities (+0x0A)

| ID | Hex | Ability | ID | Hex | Ability |
|----|-----|---------|----|-----|---------|
| 198 | C6 | Equip Heavy Armor | 213 | D5 | Concentration |
| 199 | C7 | Equip Shields | 214 | D6 | Tame |
| 200 | C8 | Equip Swords | 215 | D7 | Poach |
| 201 | C9 | Equip Katana | 216 | D8 | Brawler |
| 202 | CA | Equip Crossbows | 217 | D9 | Beast Tongue |
| 203 | CB | Equip Polearms | 218 | DA | Throw Items |
| 204 | CC | Equip Axes | 219 | DB | Safeguard |
| 205 | CD | Equip Guns | 220 | DC | Doublehand |
| 206 | CE | Halve MP | 221 | DD | Dual Wield |
| 207 | CF | JP Boost | 222 | DE | Beastmaster |
| 208 | D0 | EXP Boost | 223 | DF | Evasive Stance |
| 209 | D1 | Attack Boost | 224 | E0 | Reequip |
| 210 | D2 | Defense Boost | 226 | E2 | Swiftspell |
| 211 | D3 | Magick Boost | 228 | E4 | HP Boost (mod) |
| 212 | D4 | Magick Def Boost | 229 | E5 | Vehemence (mod) |

## Movement Abilities (+0x0C)

| ID | Hex | Ability | ID | Hex | Ability |
|----|-----|---------|----|-----|---------|
| 230 | E6 | Movement +1 | 242 | F2 | Teleport |
| 231 | E7 | Movement +2 | 244 | F4 | Ignore Weather |
| 232 | E8 | Movement +3 | 245 | F5 | Ignore Terrain |
| 233 | E9 | Jump +1 | 246 | F6 | Waterwalking |
| 234 | EA | Jump +2 | 247 | F7 | Swim |
| 235 | EB | Jump +3 | 248 | F8 | Lavawalking |
| 236 | EC | Ignore Elevation | 250 | FA | Levitate |
| 237 | ED | Lifefont | 251 | FB | Fly |
| 238 | EE | Manafont | 253 | FD | Treasure Hunter |
| 239 | EF | Accrue EXP |
| 240 | F0 | Accrue JP |

## UI Ability Cursor
`0x140C0EB20` — global counter incrementing on cursor move in ability lists. Delta from initial value = cursor position.

## Per-Job Passive Ability Order (for roster byte-2 decode)

Each roster slot has a per-job learned-ability bitfield at `+0x32 + jobIdx*3` (3 bytes per job). **Byte 2** tracks learned **passive** abilities (reaction/support/movement combined), MSB-first, indexed into each job's ID-sorted passive list. Bit `0x80` = first passive in the list below; bit `0x40` = second; etc.

Decoded 2026-04-14 against Ramza's in-game pickers — 100% match on 19 reactions, 23 supports, 12 movements. See `RosterReader.ReadLearnedPassives`.

| jobIdx | Job | Passive IDs (sorted ascending) |
|---|---|---|
| 0 | Squire | B4, CC, CF, DE, DF, E6 |
| 1 | Chemist | B9, DA, DB, E0, FD |
| 2 | Knight | BF, C6, C7, C8 |
| 3 | Archer | A8, C4, CA, D5, E9 |
| 4 | Monk | AF, BA, C5, D8, ED |
| 5 | White Mage | AC, D4 |
| 6 | Black Mage | B3, D3 |
| 7 | Time Mage | B1, BD, E2, F2, FA |
| 8 | Summoner | B0, CE |
| 9 | Thief | AA, B7, C2, D7, E7, EA |
| 10 | Orator | C0, CD, D6, D9 |
| 11 | Mystic | B6, D2, EE, F4 |
| 12 | Geomancer | B5, D1, F5, F8 |
| 13 | Dragoon | AB, CB, EC |
| 14 | Samurai | B2, C3, C9, DC, F7 |
| 15 | Ninja | A9, C1, DD, F6 |
| 16 | Arithmetician | BC, BE, D0, EF, F0 |
| 17 | Bard/Dance | A7, AE, E8, FB |
| 19 | Dark Knight | E4, E5, EB |

(jobIdx 18 is reserved — no passives.)

To read which passives a unit has learned: for each job, read byte at `rosterBase + slot*0x258 + 0x32 + jobIdx*3 + 2`; for each set bit at position `i` (MSB-first, `0x80 >> i`), the ability is `passiveIds[jobIdx][i]`. Classify as reaction/support/movement via the dicts in `AbilityData.cs`.
