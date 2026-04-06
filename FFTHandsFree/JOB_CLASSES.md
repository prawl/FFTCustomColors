<!-- This file should not be longer than 200 lines, if so prune me. -->
# FFT Job Classes — Strategy Reference

Quick reference for Claude to make informed decisions about job changes, ability purchases, and party composition.

## Job Prerequisites & Unlock Tree

```
Squire ──► Knight (Sq2) ──► Monk (Kn3) ──► Geomancer (Mo4)
  │                │           │               │
  │                │           └► Dragoon (Th4) ├► Dancer (Geo5+Dra5, Female)
  │                │                            └► Bard (Sum5+Ora5, Male)
  │                ├► Samurai (Kn4+Mo5+Dra2)
  │                └► Dark Knight (Kn master+BM master+Sam8+Geo8+Nin8+Dra8+20 kills)
  │
  ├► Archer (Sq2) ──► Thief (Ar3) ──► Ninja (Ar4+Th5+Geo2)
  │                                └► Dragoon (Th4)
  │
Chemist ► White Mage (Ch2) ──► Mystic (WM3) ──► Orator (My3)
  │       │                                        └► Summoner (TM3) → Bard
  │       └► Time Mage (BM3) → Summoner
  │
  └► Black Mage (Ch2) ──► Time Mage (BM3)
                         └► Arithmetician (WM5+BM5+TM4+My4)

Mime: Sq8+Ch8+Sum5+Ora5+Geo5+Dra5
Onion Knight: Sq6+Ch6
```

## Job Stats & Equipment

| Job | Move | Jump | HP | PAtk | MAtk | Speed | Equipment |
|-----|------|------|----|------|------|-------|-----------|
| Squire | 4 | 3 | C | C | C* | C | Knife, Sword, Axe, Flail, Hat, Clothes |
| Chemist | 3 | 3 | C | B | C* | C | Knife, Gun, Hat, Clothes |
| Knight | 3 | 3 | A | A | C* | C | Sword, Knight's Sword, Shield, Helm, Armor, Robe |
| Archer | 3 | 3 | B | B | C* | C | Crossbow, Bow, Shield, Hat, Clothes |
| Monk | 3 | 4 | B | A | C* | B | Clothes |
| White Mage | 3 | 3 | C | F | B | C | Staff, Hat, Clothes, Robe |
| Black Mage | 3 | 3 | D | F | A | C | Rod, Clothes, Robe |
| Time Mage | 3 | 3 | D | D | B | C | Staff, Clothes, Robe |
| Mystic | 3 | 3 | D | B | D | C | Rod, Staff, Book, Pole, Hat, Clothes, Robe |
| Summoner | 3 | 3 | D | D | A | D | Rod, Staff, Hat, Clothes, Robe |
| Thief | 4 | 4 | C | D | D* | B | Knife, Hat, Clothes |
| Orator | 3 | 3 | C | B | C* | C | Knife, Gun, Hat, Clothes, Robe |
| Geomancer | 4 | 3 | B | B | C | C | Sword, Axe, Shield, Hat, Clothes, Robe |
| Dragoon | 3 | 4 | A | A | F* | C | Polearm, Shield, Helm, Armor, Robe |
| Samurai | 3 | 3 | C | A | C | C | Katana, Helm, Armor, Robe |
| Ninja | 4 | 4 | F | B | D* | C | Knife, Ninja Blade, Flail, Hat, Clothes |
| Arithmetician | 3 | 3 | D | C | C | F | Book, Pole, Hat, Clothes, Robe |
| Bard | 3 | 3 | F | F | F* | C | Instrument, Hat, Clothes |
| Dancer | 3 | 3 | F | C | F/C* | C | Knife, Cloth, Hat, Clothes |
| Dark Knight | 3 | 3 | C | A | C | C | Sword, Knight's Sword, Axe, Flail, Shield, Helm, Armor, Clothes, Robe |
| Mime | 4 | 4 | C | Var | Var | A | Nothing |

## Essential Abilities Per Job

Priority abilities to learn first (best JP value).

### Squire
- **JP Boost** (250 JP, Support) — 50% more JP per action, THE most important ability for grinding
- **Focus** (300 JP) — +1 PAtk, can sit and gain JP
- **Move +1** (200 JP, Movement)

### Chemist
- **Phoenix Down** (90 JP) — revive KO'd units, essential to avoid permadeath
- **Throw Items** (350 JP, Support) — Range 4 item use
- **Treasure Hunter** (100 JP, Movement)

### Knight
- **Parry** (200 JP, Reaction) — weapon evasion, always active (not Bravery-dependent)
- **Equip Heavy Armor** (500 JP, Support) — lets any job wear heavy armor

### Archer
- **Concentration** (400 JP, Support) — attacks bypass evasion

### Monk
- **Counter** (300 JP, Reaction) — staple for aggressive units
- **Revive** (500 JP) — KO removal
- **Chakra** (350 JP) — HP/MP restore

### White Mage
- **Cure/Cura/Curaga** — tiered healing
- **Raise** (200 JP) — KO removal with healing
- **Holy** (600 JP) — strongest Arithmeticks damage spell

### Black Mage
- **Thunder line** — most reliable element
- **Toad** (500 JP) — battlefield control
- **Magick Boost** (400 JP, Support)

### Time Mage
- **Haste** (100 JP) — best buff in the game
- **Swiftspell** (1000 JP, Support) — faster casting
- **Teleport** (650 JP, Movement) — best movement ability

### Thief
- **Steal Heart** (150 JP) — Charm effect
- **Poach** (200 JP, Support) — monster item farming
- **Move +2** (560 JP, Movement)

### Summoner
- **Golem** (500 JP) — party-wide physical damage ward
- **Halve MP** (1000 JP, Support)

### Ninja
- **Dual Wield** (1000 JP, Support) — two weapons, doubles attacks
- **Reflexes** (400 JP, Reaction) — doubles evasion, always active

### Samurai
- **Shirahadori** (700 JP, Reaction) — Bravery% physical evasion, works on ranged
- **Doublehand** (900 JP, Support) — doubles weapon power

### Dark Knight
- **Crushing Blow** (300 JP) — guaranteed hit, range 3, chance of Stop
- **Vehemence** (400 JP, Support) — +50% PAtk (but +50% damage taken)

### Arithmetician
- **All Arithmeticks** (2550 JP total) — game-breaking instant map-wide spells
- **Soulbind** (300 JP, Reaction)

## Key Strategic Notes

### Damage Formulas
- **Barehanded (Monk)**: PAtk² scaled by Bravery — raises Bravery = massive damage
- **Geomancy**: (PAtk+2)/2 × MAtk — needs both stats
- **Magick**: MAtk × AbilityPower × Faith/100 × TargetFaith/100
- **Iaido**: MAtk × AbilityPower (straight multiplication)

### Important Mechanics
- **Bravery**: affects Reaction ability trigger rates, barehanded damage, Treasure Hunter
- **Faith**: affects magick damage dealt AND received — high Faith = glass cannon caster
- **Charging**: casting magick makes you vulnerable — Swiftspell reduces this
- **Zodiac compatibility**: affects success rates and damage between units
- **Permanent stat changes**: Bravery/Faith changes in battle are partially permanent
- **Permadeath**: units KO'd for 3 turns in battle are permanently lost (use Phoenix Down!)

### Gender-Locked Jobs
- **Bard**: Male only (Summoner 5 + Orator 5)
- **Dancer**: Female only (Geomancer 5 + Dragoon 5)
- **Dark Knight Movement**: Males get Jump +3, Females get Move +3

### Recommended Builds
- **Physical DPS**: Knight/Dark Knight + Dual Wield or Doublehand + Counter/Shirahadori
- **Mage**: Black Mage (highest MAtk) + Arithmeticks secondary + Swiftspell
- **Tank**: Knight/Dragoon + Equip Heavy Armor + Parry + Move +1
- **Support**: White Mage/Chemist + Items secondary + Throw Items + Auto-Potion
- **Broken**: Arithmetician casting from Black Mage job (CT 4 Holy with Excalibur)
