<!-- This file should not be longer than 200 lines, if so prune me. -->
# Abilities and Jobs

How the job and ability system works, and how to reason about it in gameplay.

## The core loop

Units earn **JP** (Job Points) during battle. JP spent in the Job Selection menu learns abilities. Learned abilities can be equipped into the unit's five slots. Equipping an ability from a different job = **cross-class utility**, the heart of FFT's team-building.

## The five ability slots

Every unit has these on their EquipmentAndAbilities (EqA) screen:

1. **Primary Action** (locked to current job) — the job's signature skillset. Knight → Arts of War, Summoner → Summon, etc. Can't be changed without changing the unit's job.
2. **Secondary Action** — pick ANY other learned skillset. Monk with Black Magicks secondary = caster with melee body.
3. **Reaction** — passive trigger on events (being hit, taking a step, etc.). Counter, Auto-Potion, Hamedo, etc.
4. **Support** — always-on passive. Dual Wield, Magick Boost, Concentration, Martial Arts (lets non-Monks use bare-hands damage scaling).
5. **Movement** — passive tied to tile movement. Move +1/+2/+3, Swim, Waterwalking, Ignore Elevation.

See Wiki/Abilities.md for the full cost tables per skillset.

## JP economy

- **Every battle → JP** for the unit's current job. Amount scales with actions taken.
- **Only the unit's CURRENT job earns JP.** Switching to Monk in Chapter 3 starts Monk at 0 JP even if the unit was Lv 8 Knight — levels stay, JP is per-job.
- **Onion Knight is the exception** — it has no abilities but gains stats from having mastered other jobs. It's a meta-class for min-maxed late game.
- **JP Boost (Squire support) doubles JP gain** for whoever equips it — stack with low-level grinding.

## Unlock tree — in short

Starting classes (Squire, Chemist) are always available. Every generic class except starters requires leveling earlier classes. See Wiki/Jobs.md for the full unlock tree.

**Practical tips:**
- Squire and Chemist are abandoned by most players but each has valuable passives (Counter Tackle, JP Boost, Auto-Potion).
- The deep late-game classes (Mime, Dark Knight, Bard/Dancer) require mastering multiple earlier trees — don't rush them.
- **Story characters** come with a unique class that only they can use (Ramza's Gallant Knight, Agrias's Holy Knight, etc.). They CAN re-class to generics, but their unique class is usually strong enough to stick with.

## What gets SURFACED in state

When you're on `EquipmentAndAbilities` (or `CharacterStatus`):
- `screen.abilities.primary/secondary/reaction/support/movement` — currently equipped ability names.
- `screen.abilities.learnedSecondary` — full list of skillsets this unit has unlocked (has learned at least one ability in).
- `screen.abilities.learnedReaction/Support/Movement` — passive lists.
- `screen.nextJp` — JP cost of the cheapest UNLEARNED ability in the unit's primary skillset. Null when the unit has maxed or the skillset has no purchasable costs.

On `JobSelection`:
- `screen.jobCellState` — `Unlocked` / `Visible` / `Locked` for the hovered cell.
- `screen.jobUnlockRequirements` — requirement text on Visible cells (e.g. `Squire Lv. 2, Chemist Lv. 3`). Empty on Unlocked/Locked.

## How to reason

- **Before every battle, check `screen -v` on PartyMenu** for gear/ability gaps. `ui=<name>` + the verbose roster show who's under-equipped.
- **When planning a job change**, weigh: (a) is the new class useful? (b) is there a skillset the unit needs that this change would unlock? (c) how much JP will we lose by resetting to 0 in the new class?
- **Cross-class synergy is the game.** A Knight with Black Magicks secondary is tanky AND ranged. A Monk with Martial Arts support keeps bare-hands damage when you equip them as an Archer. Think in terms of primary+secondary+reaction combos.
- **Reaction choice matters more than most abilities.** Counter doubles your damage output over a long battle. Auto-Potion is insurance. Hamedo pre-empts adjacent attacks (huge vs. melee-heavy enemies).
- **Support slot is passive slot.** Don't leave it on a weak default (Equip Axes, etc.) — pick something that scales: Magick Boost for casters, Dual Wield for ninjas/samurai, Concentration for Aim users, Martial Arts for fist classes.
- **Movement slot is quiet but crucial.** Move +1 is better than almost any Movement upgrade for positioning-heavy maps. Jump +1/+2 matters on vertical terrain. Teleport lets a single mage reposition across gaps.

## Story characters — ability locks

Some story characters have hard-coded ability slots that cannot be changed:

- **Construct 8** — defaults are fixed; picker opens but Enter on alternatives is a no-op. The `change_secondary/reaction/support/movement_ability_to` helpers refuse with a clear error. See `JobGridLayout.LockedAbilityUnits`.

Most other story characters behave like generics — can re-equip freely from their learned lists.

## Gotchas

- **JP costs in the game are WotL values**, different from PSX canonical guides. If you've seen "Focus 200 JP" online and the game shows 300, it's the WotL rebalance. See `ABILITY_COSTS.md` for verified values.
- **Mettle vs. Fundaments** — they look the same but Mettle (Ramza's Gallant Knight primary) extends Fundaments with Tailwind/Chant/Steel/Shout/Ultima. Other units with "Squire" as their class get plain Fundaments (4 abilities). Ramza unique = more.
- **Bard is male-only, Dancer is female-only.** The opposite-gender cell on the JobSelection grid is always locked. Unit tests in `JobGridLayoutTests` enforce this.
- **Mime has no learnable abilities** in its skillset — its gimmick is copying the previous unit's action. It's an endgame class you unlock for bragging rights and niche strategies.
- **Dark Knight has a kill requirement** (20+ enemy kills by the target unit). Random encounter grinding works. The kill counter is persistent per unit.

## Commands that touch this system

| Command | Effect |
|---|---|
| `open_eqa [unit]` | Jump straight to a unit's EquipmentAndAbilities screen |
| `open_job_selection [unit]` | Jump to a unit's JobSelection (for job changes) |
| `change_secondary_ability_to <name>` | Equip a named skillset to the secondary slot |
| `change_reaction_ability_to <name>` | Equip a named reaction |
| `change_support_ability_to <name>` | Equip a named support |
| `change_movement_ability_to <name>` | Equip a named movement |
| `remove_equipment` | Unequip whatever the cursor is on (equipment column only) |
| `unequip_all <unit>` | Strip all 5 equipment slots from a unit |
| `view_unit <name>` | Read-only dump of a unit's job, stats, equipment, abilities |

## See also

- `CutsceneDialogue.md` — unrelated, story scenes.
- `Commands.md` — full command reference.
- `../Wiki/Jobs.md` — stat multipliers, unlock tree (data reference).
- `../Wiki/Abilities.md` — ability cost tables per skillset.
- `../ABILITY_COSTS.md` — IC-remaster WotL JP costs.
