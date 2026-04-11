using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Static lookup tables for FFT ability IDs, names, JP costs, and job associations.
    /// Ability IDs match the byte values stored in unit data at offsets +0x08 (reaction), +0x0A (support), +0x0C (movement).
    /// JP costs sourced from FFT: War of the Lions / Ivalice Chronicles.
    /// </summary>
    public static class AbilityData
    {
        public record AbilityInfo(byte Id, string Name, int JpCost, string Job, string? Description = null);

        public static readonly Dictionary<byte, AbilityInfo> ReactionAbilities = new()
        {
            [0xB4] = new(0xB4, "Counter Tackle", 180, "Squire", "Upon receiving a physical attack from an adjacent unit, counterattacks with an ability equivalent to Rush. Activates even when no damage is dealt, such as when parried or evaded."),
            [0xB9] = new(0xB9, "Auto-Potion", 400, "Chemist", "Upon receiving damage, uses a potion from the inventory to restore one's own HP. Prioritizes the use of potions of lower potency. Activates even when the damage dealt is zero."),
            [0xBF] = new(0xBF, "Parry", 200, "Knight", "Block physical attacks with the equipped weapon."),
            [0xA8] = new(0xA8, "Speed Surge", 900, "Archer", "Upon losing HP, increases the unit's Speed."),
            [0xC4] = new(0xC4, "Archer's Bane", 450, "Archer", "Dodge arrow and bolt attacks."),
            [0xAC] = new(0xAC, "Regenerate", 400, "White Mage", "Upon receiving damage, grants Regen to the unit."),
            [0xB3] = new(0xB3, "Magick Counter", 800, "Black Mage", "Counterattack using the same magick with which the user was attacked."),
            [0xAF] = new(0xAF, "Critical: Recover HP", 500, "Monk", "If the unit receives damage and is in critical condition, fully restores the unit's HP."),
            [0xBA] = new(0xBA, "Counter", 300, "Monk", "Upon receiving a physical attack, strikes back with the standard Attack command. Activates even when no damage is dealt, such as when parried or evaded."),
            [0xC5] = new(0xC5, "First Strike", 1300, "Monk", "When about to receive certain attacks from a human unit within standard attack range, strikes preemptively and prevents the attack. As this is a special counterattack, the expected outcome will not be displayed."),
            [0xAA] = new(0xAA, "Vigilance", 200, "Thief", "Upon receiving a physical attack, assumes an Evasive Stance, which greatly increases the unit's physical and magickal parry and evasion rates until its next turn. Activates even when no damage is dealt, such as when parried or evaded."),
            [0xB7] = new(0xB7, "Gil Snapper", 200, "Thief", "Upon receiving damage, receives gil equal to the amount of damage taken."),
            [0xC2] = new(0xC2, "Sticky Fingers", 200, "Thief", "The unit catches thrown items that would otherwise strike it, receiving no damage and obtaining the item."),
            [0xB6] = new(0xB6, "Absorb MP", 250, "Mystic", "Upon receiving any type of magick, recovers MP equal to the amount used by the caster."),
            [0xB1] = new(0xB1, "Critical: Quick", 800, "Time Mage", "If the unit receives damage and is in critical condition, the unit's next turn will come immediately."),
            [0xBD] = new(0xBD, "Mana Shield", 400, "Time Mage", "When receiving damage, MP is reduced rather than HP. Usage condition: activates when HP loss is 1 or more."),
            [0xB5] = new(0xB5, "Nature's Wrath", 300, "Geomancer", "Upon receiving a physical attack or geomancy skill, counterattacks with a geomancy skill, including those that the unit has yet to learn."),
            [0xAB] = new(0xAB, "Dragonheart", 600, "Dragoon", "Upon receiving a physical attack, grants the unit Reraise, allowing it to recover from KO status."),
            [0xC0] = new(0xC0, "Earplugs", 300, "Orator", "Greatly increases the likelihood of avoiding the effects of Speechcraft."),
            [0xB0] = new(0xB0, "Critical: Recover MP", 400, "Summoner", "If the unit receives damage and is in critical condition, fully restores the unit's MP."),
            [0xB2] = new(0xB2, "Bonecrusher", 200, "Samurai", "When critically wounded, deals damage equal to the unit's maximum HP to the attacker."),
            [0xC3] = new(0xC3, "Shirahadori", 700, "Samurai", "Evade non-ranged physical attacks. Despite the in-game description, also works against bows, crossbows, guns, and monster attacks."),
            [0xA9] = new(0xA9, "Vanish", 1000, "Ninja", "Upon receiving a physical attack, grants Invisibility to the unit."),
            [0xC1] = new(0xC1, "Reflexes", 400, "Ninja", "Upon receiving an attack, greatly increases physical and magickal parry and evasion rates. Ineffective against certain attacks."),
            [0xBC] = new(0xBC, "Cup of Life", 200, "Arithmetician", "When HP is restored, distribute any excess among one's allies."),
            [0xBE] = new(0xBE, "Soulbind", 300, "Arithmetician", "Split any damage taken with the opponent who inflicted it."),
            [0xA7] = new(0xA7, "Magick Surge", 500, "Bard", "Upon receiving damage, increases the unit's magickal attack for the duration of battle."),
            [0xAE] = new(0xAE, "Faith Surge", 700, "Bard", "Upon receiving magick, increases the unit's Faith for the duration of battle."),
        };

        public static readonly Dictionary<byte, AbilityInfo> SupportAbilities = new()
        {
            [0xCC] = new(0xCC, "Equip Axes", 170, "Squire", "Allows the unit to equip axes, regardless of job."),
            [0xDE] = new(0xDE, "Beastmaster", 200, "Squire", "Adds an ability to all monsters in neighboring tiles with an elevation difference of 3h or less."),
            [0xDF] = new(0xDF, "Evasive Stance", 50, "Squire", "Defend oneself against an attack. Adds the Defend command."),
            [0xCF] = new(0xCF, "JP Boost", 250, "Squire", "Increases the amount of JP earned in battle."),
            [0xDA] = new(0xDA, "Throw Items", 350, "Chemist", "Allows the unit to throw items with the Items command within a range of 4 tiles like a chemist, even if using a different job."),
            [0xDB] = new(0xDB, "Safeguard", 250, "Chemist", "Prevents equipment from being destroyed or stolen."),
            [0xE0] = new(0xE0, "Reequip", 50, "Chemist", "Adds the Reequip command, which allows the unit to use its turn to change equipment during battle."),
            [0xC6] = new(0xC6, "Equip Heavy Armor", 500, "Knight", "Allows the unit to equip helms and armor, regardless of job."),
            [0xC7] = new(0xC7, "Equip Shields", 250, "Knight", "Allows the unit to equip shields, regardless of job."),
            [0xC8] = new(0xC8, "Equip Swords", 400, "Knight", "Allows the unit to equip swords, regardless of job."),
            [0xCA] = new(0xCA, "Equip Crossbows", 350, "Archer", "Allows the unit to equip crossbows, regardless of job."),
            [0xD5] = new(0xD5, "Concentration", 400, "Archer", "Makes attacks unblockable. If an enemy is in the targeted tile, it will always be a hit."),
            [0xD4] = new(0xD4, "Magick Defense Boost", 400, "White Mage", "Increases the unit's defense against magickal attacks."),
            [0xD3] = new(0xD3, "Magick Boost", 400, "Black Mage", "Inflict greater damage with magickal attacks."),
            [0xD8] = new(0xD8, "Brawler", 200, "Monk", "Deliver more powerful unarmed attacks, even if not a monk."),
            [0xD7] = new(0xD7, "Poach", 200, "Thief", "Allows the unit to claim a creature's carcass by defeating it with the standard attack command."),
            [0xD2] = new(0xD2, "Defense Boost", 400, "Mystic", "Increases the unit's defense against physical attacks."),
            [0xE2] = new(0xE2, "Swiftspell", 1000, "Time Mage", "Reduces the time needed to cast magick spells."),
            [0xD1] = new(0xD1, "Attack Boost", 400, "Geomancer", "Increases the unit's physical attack power."),
            [0xCB] = new(0xCB, "Equip Polearms", 400, "Dragoon", "Allows the unit to equip polearms, regardless of job."),
            [0xCD] = new(0xCD, "Equip Guns", 800, "Orator", "Allows the unit to equip guns, regardless of job."),
            [0xD6] = new(0xD6, "Tame", 500, "Orator", "Forces a critically wounded enemy creature to become an ally. At the end of battle, that unit will officially join your party."),
            [0xD9] = new(0xD9, "Beast Tongue", 100, "Orator", "Allows the use of Speechcraft against creatures, even if not an orator."),
            [0xCE] = new(0xCE, "Halve MP", 1000, "Summoner", "Reduces MP consumption by half when using magicks."),
            [0xC9] = new(0xC9, "Equip Katana", 400, "Samurai", "Allows the unit to equip katana, regardless of job."),
            [0xDC] = new(0xDC, "Doublehand", 900, "Samurai", "Wield a weapon with both hands, increasing its destructive power."),
            [0xDD] = new(0xDD, "Dual Wield", 1000, "Ninja", "Allows the unit to wield a weapon in each hand, even if not a ninja. Certain weapons can be equipped in this manner, allowing the unit to attack twice each turn."),
            [0xD0] = new(0xD0, "EXP Boost", 350, "Arithmetician", "Earn more EXP for the same actions."),
            [0xE4] = new(0xE4, "HP Boost", 2000, "Dark Knight", "Increases maximum HP by 20 percent."),
            [0xE5] = new(0xE5, "Vehemence", 400, "Dark Knight", "Increases attack power by 50 percent, and decreases defensive power by 50 percent."),
        };

        public static readonly Dictionary<byte, AbilityInfo> MovementAbilities = new()
        {
            [0xE6] = new(0xE6, "Movement +1", 200, "Squire", "Increases movement by 1, allowing the unit to move a greater distance."),
            [0xFD] = new(0xFD, "Treasure Hunter", 100, "Chemist", "Allows the unit to discover items hidden on tiles upon moving to them. The probability of finding rare items is increased for chemists."),
            [0xE9] = new(0xE9, "Jump +1", 200, "Archer", "Increases jump attribute by 1, allowing the unit to traverse greater elevation differences."),
            [0xED] = new(0xED, "Lifefont", 300, "Monk", "Recovers HP upon moving. Movement cannot be reset for units with this ability."),
            [0xE7] = new(0xE7, "Movement +2", 560, "Thief", "Increases movement by 2, allowing the unit to move a greater distance."),
            [0xEA] = new(0xEA, "Jump +2", 500, "Thief", "Increases jump attribute by 2, allowing the unit to traverse greater elevation differences."),
            [0xF4] = new(0xF4, "Ignore Weather", 200, "Mystic", "Allows the unit to move unimpeded through terrain affected by rainfall, such as marshes, swamps, and poisonous fens."),
            [0xEE] = new(0xEE, "Manafont", 350, "Mystic", "Recovers MP upon moving. Movement cannot be reset for units with this ability."),
            [0xF2] = new(0xF2, "Teleport", 650, "Time Mage", "Allows the unit to warp instantly to destination tiles. May fail when attempting to travel to locations that exceed the unit's movement and jump attributes. Movement cannot be reset for units with this ability."),
            [0xFA] = new(0xFA, "Levitate", 540, "Time Mage", "Grants permanent Float status in battle, allowing movement while levitating at an elevation of 1 above the ground."),
            [0xF5] = new(0xF5, "Ignore Terrain", 220, "Geomancer", "Move unimpeded through watery terrain."),
            [0xF8] = new(0xF8, "Lavawalking", 150, "Geomancer", "Walk across and even stop on the surface of lava."),
            [0xEC] = new(0xEC, "Ignore Elevation", 700, "Dragoon", "Move to tiles of any height, regardless of Jump attribute."),
            [0xF7] = new(0xF7, "Swim", 300, "Samurai", "Allows the unit to swim through and even stop in deep water."),
            [0xF6] = new(0xF6, "Waterwalking", 420, "Ninja", "Allows the unit to move across and even stop on the surface of water. Does not protect against poisonous fens."),
            [0xEF] = new(0xEF, "Accrue EXP", 400, "Arithmetician", "Obtain EXP upon moving."),
            [0xF0] = new(0xF0, "Accrue JP", 400, "Arithmetician", "Obtain JP upon moving."),
            [0xFB] = new(0xFB, "Fly", 900, "Bard", "Allows the unit to move by flying, passing over enemies and obstacles."),
            [0xEB] = new(0xEB, "Jump +3", 600, "Dark Knight", "Increases jump attribute by 3, allowing the unit to traverse greater elevation differences."),
            [0xE8] = new(0xE8, "Movement +3", 1000, "Bard", "Increases movement by 3, allowing the unit to move a greater distance."),
        };

        /// <summary>
        /// Look up any ability by its byte ID across all types.
        /// </summary>
        public static AbilityInfo? GetAbility(byte id)
        {
            if (ReactionAbilities.TryGetValue(id, out var r)) return r;
            if (SupportAbilities.TryGetValue(id, out var s)) return s;
            if (MovementAbilities.TryGetValue(id, out var m)) return m;
            return null;
        }

        /// <summary>
        /// Get the JP cost for an ability, or -1 if unknown.
        /// </summary>
        public static int GetJpCost(byte abilityId)
        {
            var info = GetAbility(abilityId);
            return info?.JpCost ?? -1;
        }

        /// <summary>
        /// Get the job index for reading JP from unit data (+0x80 + index*2).
        /// </summary>
        public static int GetJobJpOffset(string jobName)
        {
            return jobName switch
            {
                "Squire" => 0,
                "Chemist" => 1,
                "Knight" => 2,
                "Archer" => 3,
                "Monk" => 4,
                "White Mage" => 5,
                "Black Mage" => 6,
                "Time Mage" => 7,
                "Summoner" => 8,
                "Thief" => 9,
                "Orator" => 10,
                "Mystic" => 11,
                "Geomancer" => 12,
                "Dragoon" => 13,
                "Samurai" => 14,
                "Ninja" => 15,
                "Arithmetician" => 16,
                "Bard" => 17,
                "Dancer" => 17, // shares slot with Bard
                "Dark Knight" => 19,
                _ => -1
            };
        }

        /// <summary>
        /// Map a skillset name to the job index used for the roster's per-job
        /// learned-ability bitfield (see `project_roster_learned_abilities.md`).
        /// Inverse of the "primary skillset for this job" mapping.
        /// </summary>
        public static int GetJobIdxBySkillsetName(string skillsetName)
        {
            return skillsetName switch
            {
                "Fundaments" => 0,
                "Mettle" => 0, // Mettle is the Squire/Gallant Knight primary
                "Items" => 1,
                "Arts of War" => 2,
                "Aim" => 3,
                "Martial Arts" => 4,
                "White Magicks" => 5,
                "Black Magicks" => 6,
                "Time Magicks" => 7,
                "Summon" => 8,
                "Steal" => 9,
                "Speechcraft" => 10,
                "Mystic Arts" => 11,
                "Geomancy" => 12,
                "Jump" => 13,
                "Iaido" => 14,
                "Throw" => 15,
                "Arithmeticks" => 16,
                "Bardsong" => 17,
                "Dance" => 17,
                "Darkness" => 19,
                _ => -1
            };
        }
    }
}
