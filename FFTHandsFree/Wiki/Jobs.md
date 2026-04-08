# FFT Job System Reference

AI-player reference for Final Fantasy Tactics (War of the Lions). All 22 generic jobs.

## Job Unlock Tree

| Job | Requirements | Notes |
|-----|-------------|-------|
| Squire | Default | Starting job |
| Chemist | Default | Starting job |
| Knight | Squire Lv2 | |
| Archer | Squire Lv2 | |
| White Mage | Chemist Lv2 | |
| Black Mage | Chemist Lv2 | |
| Monk | Knight Lv3 | |
| Thief | Archer Lv3 | |
| Mystic (Oracle) | White Mage Lv3 | |
| Time Mage | Black Mage Lv3 | |
| Geomancer | Monk Lv4 | |
| Dragoon | Thief Lv4 | |
| Orator (Mediator) | Mystic Lv3 | |
| Summoner | Time Mage Lv3 | |
| Samurai | Knight Lv4, Monk Lv5, Dragoon Lv2 | |
| Ninja | Archer Lv4, Thief Lv5, Geomancer Lv2 | |
| Arithmetician (Calculator) | White Mage Lv5, Black Mage Lv5, Time Mage Lv4, Mystic Lv4 | |
| Bard | Summoner Lv5, Orator Lv5 | Male only |
| Dancer | Dragoon Lv5, Geomancer Lv5 | Female only |
| Mime | Squire Lv8, Chemist Lv8, Summoner Lv5, Orator Lv5, Geomancer Lv5, Dragoon Lv5 | |
| Onion Knight | Squire Lv6, Chemist Lv6 | WotL only |
| Dark Knight | Master Knight + Black Mage, Dragoon Lv8, Samurai Lv8, Ninja Lv8, Geomancer Lv8, 20 kills | WotL only |

## Stat Multipliers (% applied to base stats while in job)

Higher = stronger in that stat while using the job. 100 = baseline.

| Job | HP | MP | Spd | PA | MA | Move | Jump |
|-----|----|----|-----|----|----|------|------|
| Squire | 100 | 75 | 100 | 90 | 80 | 4 | 3 |
| Chemist | 80 | 75 | 100 | 75 | 80 | 3 | 3 |
| Knight | 120 | 80 | 100 | 120 | 80 | 3 | 3 |
| Archer | 100 | 75 | 100 | 110 | 80 | 3 | 3 |
| Monk | 135 | 80 | 110 | 129 | 100 | 3 | 4 |
| White Mage | 80 | 120 | 110 | 75 | 110 | 3 | 3 |
| Black Mage | 65 | 120 | 100 | 60 | 150 | 3 | 3 |
| Time Mage | 75 | 120 | 100 | 60 | 130 | 3 | 3 |
| Summoner | 65 | 125 | 90 | 50 | 120 | 3 | 3 |
| Thief | 100 | 75 | 110 | 100 | 80 | 4 | 4 |
| Orator (Mediator) | 80 | 90 | 100 | 75 | 100 | 3 | 3 |
| Mystic (Oracle) | 80 | 110 | 100 | 70 | 120 | 3 | 3 |
| Geomancer | 110 | 90 | 100 | 110 | 100 | 4 | 3 |
| Dragoon | 120 | 75 | 100 | 120 | 80 | 3 | 4 |
| Samurai | 110 | 80 | 100 | 128 | 90 | 3 | 3 |
| Ninja | 100 | 50 | 120 | 120 | 60 | 4 | 3 |
| Arithmetician (Calc) | 75 | 100 | 80 | 50 | 100 | 3 | 3 |
| Bard | 70 | 80 | 100 | 50 | 100 | 3 | 3 |
| Dancer | 100 | 80 | 100 | 80 | 100 | 3 | 3 |
| Mime | 140 | 50 | 120 | 110 | 100 | 4 | 4 |
| Onion Knight | 100 | 100 | 100 | 100 | 100 | 3 | 3 |
| Dark Knight | 120 | 80 | 100 | 120 | 100 | 3 | 3 |

## Stat Growth Rankings (for leveling optimization)

Level up in these jobs to permanently build stats. Growth affects the raw stat gain per level.

| Stat | Best Growth Jobs (descending) |
|------|-------------------------------|
| HP | Monk, Knight, White Mage, Geomancer, Dragoon |
| MP | Summoner, White Mage, Black Mage, Time Mage, Mystic, Arithmetician |
| Speed | Ninja, Thief (all others roughly equal) |
| PA | Knight, Ninja, Dragoon, Samurai, Archer, Geomancer |
| MA | All standard jobs grow MA equally (Mime is best if available) |

**Leveling strategy**: Gain levels in high-growth jobs (Monk for HP, Knight/Ninja for PA, Ninja for Speed). MA growth is nearly identical across all jobs. Avoid leveling in Chemist, Bard, Orator, Arithmetician (poor growth).

## Innate Abilities & Job Traits

| Job | Innate Ability | Action Skillset | Weapons |
|-----|---------------|-----------------|---------|
| Squire | Counter Tackle (R) | Fundaments | Sword, Axe, Flail, Dagger |
| Chemist | Throw Items (S) | Items | Gun, Dagger |
| Knight | — | Arts of War (Break) | Sword, Knight Sword, Shield |
| Archer | — | Aim | Bow, Crossbow |
| Monk | Brawler (S) | Martial Arts | Bare fists |
| White Mage | — | White Magicks | Staff, Rod |
| Black Mage | — | Black Magicks | Rod |
| Time Mage | — | Time Magicks | Staff |
| Summoner | — | Summon | Staff, Rod |
| Thief | — | Steal | Dagger |
| Orator (Mediator) | — | Speechcraft | Gun, Dagger |
| Mystic (Oracle) | — | Mystic Arts | Staff |
| Geomancer | Lavawalking (S) | Geomancy | Sword, Axe |
| Dragoon | — | Jump | Spear |
| Samurai | — | Iaido (Draw Out) | Katana |
| Ninja | Dual Wield (S) | Throw | Ninja Blade, Dagger |
| Arithmetician | — | Arithmeticks | Staff, Book |
| Bard | — | Bardsong | Harp (male only) |
| Dancer | — | Dance | Cloth (female only) |
| Mime | — | Mimic (auto) | Bare fists |
| Onion Knight | — | None learnable | All equipment at Lv8 |
| Dark Knight | — | Darkness | Sword, Knight Sword, Fell Sword, Axe, Flail |

(R) = Reaction ability, (S) = Support ability

## Key Ability Descriptions

- **Fundaments**: Focus (raise PA), Rush, Stone, Tailwind (raise Speed)
- **Arts of War**: Break enemy equipment (Weapon/Shield/Helm/Armor Break), Rend stats (Speed/Power/Magic)
- **Martial Arts**: Pummel, Aurablast, Shockwave, Chakra (self-heal+MP), Revive
- **Steal**: Steal Gil/Heart/Helmet/Armor/Shield/Weapon/Accessory/EXP
- **Geomancy**: Auto-selects attack based on terrain. No MP cost, no charge time
- **Jump**: Vertical attack, user leaves field during charge. Damage = PA x JP power
- **Iaido**: Chance to break katana for powerful magic-like effects
- **Arithmeticks**: Cast any learned spell instantly, 0 MP, on all units matching a number condition. Extremely powerful
- **Dual Wield**: Attack with weapon in each hand. Best physical damage support ability in the game
- **Throw Items**: Use items at range (default 4 tiles)
- **Bardsong**: AoE buffs affecting all allies (Haste, Regen, Reraise, stat boosts)
- **Dance**: AoE debuffs affecting all enemies (Slow, Stop, stat drain)
- **Mimic**: Automatically copies the last action used by any ally. No MP/item cost

## WotL-Exclusive Jobs

### Onion Knight
- Requires: Squire Lv6 + Chemist Lv6
- Terrible base stats at low job levels, but becomes the strongest job at Lv8
- At Lv8: can equip all equipment including Onion equipment (best gear in game)
- Cannot learn any abilities; must set abilities from other jobs
- Gains massive stat boosts at each job level

### Dark Knight
- Requires: Master Knight + Master Black Mage + Dragoon/Samurai/Ninja/Geomancer all Lv8 + 20 kills
- Hardest job to unlock in the game
- Darkness skillset: HP-draining attacks, dark-element AoE
- Key abilities: Sanguine Sword (drain HP), Infernal Strike (dark AoE)
- Strong physical stats with decent MA for Darkness abilities

## Quick Reference: Best Jobs by Role

| Role | Top Jobs |
|------|----------|
| Physical DPS | Monk, Ninja (Dual Wield), Samurai, Dark Knight |
| Magic DPS | Black Mage, Summoner, Arithmetician |
| Healer | White Mage, Chemist (items) |
| Support/Buff | Bard, Orator, Time Mage (Haste/Slow) |
| Debuff/Control | Mystic, Dancer, Orator |
| Tank | Knight (Break), Dragoon |
| Utility | Thief (Steal), Geomancer (free terrain attacks) |
| Speed | Ninja (highest Speed mult + growth) |
