# FFT Zodiac Compatibility & Elemental System

Quick reference for an AI playing Final Fantasy Tactics (WotL / PSX).

## Zodiac Compatibility Multipliers

| Compatibility | Damage/Healing | Status Hit Rate | When |
|---------------|---------------|-----------------|------|
| Best | x1.50 | x1.50 | Opposite sign, opposite gender |
| Good | x1.25 | x1.25 | Triangle partners (see chart) |
| Neutral | x1.00 | x1.00 | No relationship |
| Bad | x0.75 | x0.75 | Square partners (see chart) |
| Worst | x0.50 | x0.50 | Opposite sign, same gender |

**Does NOT affect:** Evasion, fall damage, Sing, Dance, Item, fixed-rate abilities.

Best/Worst only triggers between opposite-sign pairs and depends on gender match. Same sign = neutral.

## Zodiac Compatibility Chart

| Sign | Opposite (Best/Worst) | Good (+25%) | Bad (-25%) |
|------|----------------------|-------------|------------|
| Aries | Libra | Leo, Sagittarius | Cancer, Capricorn |
| Taurus | Scorpio | Virgo, Capricorn | Leo, Aquarius |
| Gemini | Sagittarius | Libra, Aquarius | Virgo, Pisces |
| Cancer | Capricorn | Scorpio, Pisces | Aries, Libra |
| Leo | Aquarius | Aries, Sagittarius | Taurus, Scorpio |
| Virgo | Pisces | Taurus, Capricorn | Gemini, Sagittarius |
| Libra | Aries | Gemini, Aquarius | Cancer, Capricorn |
| Scorpio | Taurus | Cancer, Pisces | Leo, Aquarius |
| Sagittarius | Gemini | Aries, Leo | Virgo, Pisces |
| Capricorn | Cancer | Taurus, Virgo | Aries, Libra |
| Aquarius | Leo | Gemini, Libra | Taurus, Scorpio |
| Pisces | Virgo | Cancer, Scorpio | Gemini, Sagittarius |

Pattern: Fire signs (Aries/Leo/Sagittarius) are Good with each other. Earth (Taurus/Virgo/Capricorn) Good with each other. Air (Gemini/Libra/Aquarius) Good with each other. Water (Cancer/Scorpio/Pisces) Good with each other.

## Story Character Zodiac Signs

| Character | Sign | Gender | Notes |
|-----------|------|--------|-------|
| Ramza | Player choice | M | Set by birthday at game start |
| Delita | Sagittarius | M | Guest/NPC |
| Alma | Leo | F | |
| Ovelia | Taurus | F | Guest |
| Agrias | Cancer | F | |
| Mustadio | Libra | M | |
| Rapha (Rafa) | Pisces | F | |
| Marach (Malak) | Gemini | M | |
| Cidolfus Orlandeau | Scorpio | M | |
| Meliadoul | Capricorn | F | |
| Beowulf | Libra | M | |
| Reis | Pisces | F | |
| Cloud | Aquarius | M | |
| Construct 8 (Worker 8) | Gemini | -- | Monster type |
| Boco | Aries | -- | Monster type |
| Lavian | Aries | F | |
| Alicia | Pisces | F | |
| Rad | Capricorn | M | |
| Olan | Cancer | M | Guest |

**Key bosses:** Wiegraf, Gaffgarion, Argath, Milleuda = **Virgo**. Elmdor = **Gemini**.
Most hard bosses are Virgo -- pick Ramza as Capricorn or Taurus for Good compatibility (+25% damage vs them).

Serpentarius (13th sign): Only held by Elidibus (Deep Dungeon boss). Neutral to all signs.

---

## Elemental System

Eight elements: **Fire, Ice, Lightning, Water, Wind, Earth, Holy, Dark**

### Elemental Modifier Effects

| Modifier | Effect | How Acquired |
|----------|--------|-------------|
| Weak | Take +100% damage (2x) | Innate to monster types |
| Half | Take -50% damage (0.5x) | Equipment, innate |
| Null | Take 0 damage | Equipment, innate |
| Absorb | Heal by damage amount | Equipment, innate |
| Strengthen | Deal +25% damage with element | Equipment (worn by caster) |

Priority when stacking: Absorb > Null > Half > Weak. Absorb always wins if present.

### Oil Status

- Inflicted by: Oilskin (Chemist item), some abilities
- Effect: Makes target **Weak to Fire** (2x fire damage)
- Removed after any fire attack hits, otherwise lasts until end of battle
- **PSX bug:** Oil does nothing in original PS1 version. Works correctly in WotL.
- Stacks with Strengthen: Oil (2x) + Fire Strengthen (1.25x) = massive fire damage

### Key Elemental Equipment

**Shields:**

| Shield | Absorb | Half | Weak | Strengthen |
|--------|--------|------|------|-----------|
| Flame Shield | Fire | Ice | Water | -- |
| Ice Shield | Ice | Fire | Lightning | -- |
| Venetian Shield | -- | Fire, Ice, Lightning | -- | -- |
| Kaiser Shield | -- | -- | -- | Fire, Ice, Lightning |

**Armor & Robes:**

| Equipment | Effect |
|-----------|--------|
| Rubber Suit | Null Lightning |
| Black Robe | Strengthen Fire, Ice, Lightning |
| White Robe | Half Fire, Ice, Lightning |
| Sage's Robe | Half ALL elements |
| Chameleon Robe | Absorb Holy |
| Minerva Bustier | Null Fire, Lightning, Wind, Dark; Half Ice, Water, Earth, Holy |

**Accessories:**

| Accessory | Effect |
|-----------|--------|
| Sage's Ring | Absorb ALL elements; Strengthen ALL elements |
| 108 Gems (Japa Mala) | Immune: Undead, Vampire, Toad, Poison; Strengthen ALL elements |

### Spells and Abilities by Element

| Element | Key Abilities |
|---------|--------------|
| Fire | Fire, Fira, Firaga, Fire Breath, Flame Attack |
| Ice | Blizzard, Blizzara, Blizzaga, Ice Breath |
| Lightning | Thunder, Thundara, Thundaga, Lightning Stab |
| Water | Water Ball (Rapha/Marach skills) |
| Wind | Wind Slash (Ninja), Tornado (Summoner) |
| Earth | Quake, Pitfall, Earth Slash, Titan (Summon) |
| Holy | Holy (White Magic). Note: Holy Sword skills (Hallowed Bolt, Judgment Blade, etc.) are NOT Holy-elemental despite names -- they use weapon element (usually none). AI treats them as Holy though. |
| Dark | Dark Holy, Shadowblade, Unholy Darkness |

### Tactical Tips

1. **Chameleon Robe vs Wiegraf:** The AI treats Holy Sword skills as Holy-elemental even though they aren't. The Chameleon Robe absorbs Holy, so Wiegraf's AI refuses to use Holy Sword, falling back to much weaker Martial Arts attacks instead.
2. **Flame Shield + Ice Shield** on two units covers Fire and Ice absorb respectively.
3. **Black Robe on casters:** +25% to Fire/Ice/Lightning spells is a huge damage boost.
4. **Sage's Ring** is the best accessory for elemental builds (absorb + strengthen all).
5. **Rubber Suit** trivializes Lightning-heavy fights.
6. **Lightning in rain:** Lightning spells deal +25% in rainy weather.
7. **Undead are weak to Fire and Holy** -- exploit with Holy (White Magic), Phoenix Down, or fire spells. Note: Holy Sword skills are NOT actually Holy-elemental despite names.
8. **Kaiser Shield on casters:** Strengthens Fire/Ice/Lightning while providing shield evasion.
