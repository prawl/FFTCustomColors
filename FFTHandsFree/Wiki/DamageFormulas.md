# FFT Damage Formulas & Combat Math

Quick reference for an AI playing Final Fantasy Tactics (War of the Lions).

## Physical Damage (Attack Command)

Base formula depends on weapon type. All results are floored (integer math).

| Weapon Type | Formula | Notes |
|---|---|---|
| Sword, Rod, Crossbow, Spear | PA * WP | Standard physical |
| Knight Sword, Katana | (PA * Br / 100) * WP | Brave-scaling; high Br = big damage |
| Knife, Bow, Ninja Blade | ((PA + Sp) / 2) * WP | Speed-scaling |
| Staff, Pole | MA * WP | Uses Magic Attack |
| Book, Instrument, Cloth | ((PA + MA) / 2) * WP | Hybrid stat |
| Gun | WP * WP | Ignores PA entirely; ignores evasion |
| Magic Gun | (CFa/100) * (TFa/100) * WP * Q | Q=14 (60%), 18 (30%), or 24 (10%); faith-based |
| Bare Fist | (PA * Br / 100) * PA | PA used as both stat and "WP" |
| Axe, Flail, Bag | Random(1..PA) * WP | Random damage; min=WP, max=PA*WP |

**PA** = Physical Attack, **MA** = Magic Attack, **Sp** = Speed, **Br** = Brave, **WP** = Weapon Power,
**CFa** = Caster Faith, **TFa** = Target Faith.

**Brawler** support ability: bare fist damage * 1.5.

**Critical Hit**: randomly boosts the relevant stat (PA/MA/Sp) before formula is applied.

## Magic Damage

```
Damage = K * MA * (CFa / 100) * (TFa / 100)
```

- **K** = spell power (e.g., Fire=14, Fire2=18, Fire3=24, Holy=50)
- Both caster and target Faith multiply as percentages
- Example: MA 10, CFa 75, TFa 60, Fire2 (K=18) -> 18 * 10 * 0.75 * 0.60 = 81

**Geomancy** is special: damage = [(PA + 2) / 2] * MA, ignores Faith entirely.

**Status spells**: base hit% + MA bonus, then multiplied by (CFa/100) * (TFa/100). Low Faith targets resist status magic.

## Special Ability Formulas

| Ability | Formula |
|---|---|
| Jump (non-polearm) | PA * WP |
| Jump (polearm) | PA * WP * 1.5 |
| Throw | Sp * WP (retains element) |
| Holy Sword skills | PA * (WP + K) where K varies by skill |
| Martial Arts (Monk) | (PA * Br / 100) * PA (same as bare fist formula) |

## Brave Mechanic

- **Range**: 0-100 (percentage)
- **Affects**: Bare fist damage, Knight Sword/Katana damage, reaction ability trigger rate
- **Reaction abilities**: Br = flat % chance to proc (e.g., 70 Br = 70% chance)
- **Treasure**: common item chance = Br%, rare item chance = (100 - Br)%
- **Warning**: permanent Brave <= 5 causes unit to leave party
- Raised by: Steel (+5, 100% success), Praise/Preach (+4, variable), Ramza's Cheer Up (+5)

## Faith Mechanic

- **Range**: 0-100 (percentage)
- **Affects**: all magic damage/healing dealt AND received
- **Double-edged**: high Faith = more magic damage output BUT more magic damage taken
- **Low Faith tank**: a unit with 30 Faith takes only 30% of normal magic damage
- **Also affects**: buff spell success (Haste, Protect, etc. are harder to land on low-Faith targets)
- **Warning**: permanent Faith >= 95 causes unit to leave party (becomes devoted)
- **Innocent status**: treated as Faith 0 -- immune to all magic

## Element System

Eight elements: Fire, Ice, Lightning, Water, Wind, Earth, Dark, Holy.

| Modifier | Effect | Stacking |
|---|---|---|
| Weak | Damage * 2 | Does not stack with itself |
| Normal | Damage * 1 | -- |
| Half | Damage / 2 | Does not stack |
| Null | Damage = 0 | -- |
| Absorb | Heals instead of damaging | -- |
| Boost (equipment) | Outgoing elemental damage * 1.25 | Does not stack |

Evaluation order: Weak -> Half -> Absorb. Equipping redundant element gear (e.g., two halve-fire items) has no additional effect.

**Key equipment**: Gaia Gear (absorb+boost Earth), Rubber Shoes (absorb Lightning), Chameleon Robe (absorb Holy), Sage's Ring (absorb+boost Fire/Ice/Lightning/Wind/Earth).

## Zodiac Compatibility

Multipliers: **Best x1.5**, **Good x1.25**, **Bad x0.75**, **Worst x0.5**. Affects damage, healing, and status hit rates. Does NOT affect evasion or item use.

**Opposite sign rule**: opposite signs + opposite gender = Best; opposite signs + same gender = Worst.

| Sign | Good With | Bad With | Opposite |
|---|---|---|---|
| Aries | Leo, Sagittarius | Cancer, Capricorn | Libra |
| Taurus | Virgo, Capricorn | Leo, Aquarius | Scorpio |
| Gemini | Libra, Aquarius | Virgo, Pisces | Sagittarius |
| Cancer | Scorpio, Pisces | Aries, Libra | Capricorn |
| Leo | Aries, Sagittarius | Taurus, Scorpio | Aquarius |
| Virgo | Taurus, Capricorn | Gemini, Sagittarius | Pisces |
| Libra | Gemini, Aquarius | Cancer, Capricorn | Aries |
| Scorpio | Cancer, Pisces | Leo, Aquarius | Taurus |
| Sagittarius | Aries, Leo | Virgo, Pisces | Gemini |
| Capricorn | Taurus, Virgo | Aries, Libra | Cancer |
| Aquarius | Gemini, Libra | Taurus, Scorpio | Leo |
| Pisces | Cancer, Scorpio | Gemini, Sagittarius | Virgo |

Same sign + same gender = neutral. Same sign + opposite gender = Good.

**Boss tip**: Virgo is the zodiac of several hard bosses (Wiegraf, Elmdore, Altima). Capricorn or Taurus Ramza gets Good compatibility offense against them.

## Evasion

Evasion is checked as sequential independent rolls (multiplicative, not additive).

### Physical Evasion by Direction

| Source | Front | Side | Back |
|---|---|---|---|
| Class Evade (C-Ev) | Yes | No | No |
| Shield (S-Ev) | Yes | Yes | No |
| Weapon Parry (requires Parry ability) | Yes | Yes | No |
| Accessory/Mantle (A-Ev) | Yes | Yes | Yes |

Back attacks bypass almost everything except accessory evasion.

### Magic Evasion
- Shield magic evade + Accessory magic evade (always apply regardless of direction)
- Feather Mantle: 40% physical, 30% magic evasion (best accessory for dodge)
- Guns ignore all physical evasion

## CT (Charge Time) System

- Each tick: unit gains CT equal to Speed
- Turn triggers when CT >= 100
- After turn: CT penalty depends on actions taken

| Action | CT Cost |
|---|---|
| Move + Act | CT - 100 |
| Move only or Act only | CT - 80 |
| Neither (Wait) | CT - 60 |

If remaining CT > 60, it is capped at 60. Haste multiplies effective Speed by 1.5; Slow multiplies by 0.5.

## Height / Elevation

No direct damage multiplier from height difference. Height affects:
- **Range**: bows gain +1 range per 2h height advantage
- **Melee reach**: most weapons hit 2h up, 3h down
- **Poles/Spears**: reach 3h up, 4h down (but horizontal range drops to 1)
- **Jump**: Dragoon Jump damage is fixed (no height bonus)
- **Fall damage**: falling from height causes HP loss based on distance fallen
