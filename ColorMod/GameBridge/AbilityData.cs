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
        public record AbilityInfo(byte Id, string Name, int JpCost, string Job);

        public static readonly Dictionary<byte, AbilityInfo> ReactionAbilities = new()
        {
            [0xB4] = new(0xB4, "Counter Tackle", 180, "Squire"),
            [0xB9] = new(0xB9, "Auto-Potion", 400, "Chemist"),
            [0xBF] = new(0xBF, "Parry", 200, "Knight"),
            [0xA8] = new(0xA8, "Speed Surge", 900, "Archer"),
            [0xC4] = new(0xC4, "Archer's Bane", 450, "Archer"),
            [0xAC] = new(0xAC, "Regenerate", 400, "White Mage"),
            [0xB3] = new(0xB3, "Magick Counter", 800, "Black Mage"),
            [0xAF] = new(0xAF, "Critical: Recover HP", 500, "Monk"),
            [0xBA] = new(0xBA, "Counter", 300, "Monk"),
            [0xC5] = new(0xC5, "First Strike", 1300, "Monk"),
            [0xAA] = new(0xAA, "Vigilance", 200, "Thief"),
            [0xB7] = new(0xB7, "Gil Snapper", 200, "Thief"),
            [0xC2] = new(0xC2, "Sticky Fingers", 200, "Thief"),
            [0xB6] = new(0xB6, "Absorb MP", 250, "Mystic"),
            [0xB1] = new(0xB1, "Critical: Quick", 800, "Time Mage"),
            [0xBD] = new(0xBD, "Mana Shield", 400, "Time Mage"),
            [0xB5] = new(0xB5, "Nature's Wrath", 300, "Geomancer"),
            [0xAB] = new(0xAB, "Dragonheart", 600, "Dragoon"),
            [0xC0] = new(0xC0, "Earplugs", 300, "Orator"),
            [0xB0] = new(0xB0, "Critical: Recover MP", 400, "Summoner"),
            [0xB2] = new(0xB2, "Bonecrusher", 200, "Samurai"),
            [0xC3] = new(0xC3, "Shirahadori", 700, "Samurai"),
            [0xA9] = new(0xA9, "Vanish", 1000, "Ninja"),
            [0xC1] = new(0xC1, "Reflexes", 400, "Ninja"),
            [0xBC] = new(0xBC, "Cup of Life", 200, "Arithmetician"),
            [0xBE] = new(0xBE, "Soulbind", 300, "Arithmetician"),
            [0xA7] = new(0xA7, "Magick Surge", 500, "Bard"),
            [0xAE] = new(0xAE, "Faith Surge", 700, "Bard"),
        };

        public static readonly Dictionary<byte, AbilityInfo> SupportAbilities = new()
        {
            [0xCC] = new(0xCC, "Equip Axes", 170, "Squire"),
            [0xDE] = new(0xDE, "Beastmaster", 200, "Squire"),
            [0xDF] = new(0xDF, "Evasive Stance", 50, "Squire"),
            [0xCF] = new(0xCF, "JP Boost", 250, "Squire"),
            [0xDA] = new(0xDA, "Throw Items", 350, "Chemist"),
            [0xDB] = new(0xDB, "Safeguard", 250, "Chemist"),
            [0xE0] = new(0xE0, "Reequip", 50, "Chemist"),
            [0xC6] = new(0xC6, "Equip Heavy Armor", 500, "Knight"),
            [0xC7] = new(0xC7, "Equip Shields", 250, "Knight"),
            [0xC8] = new(0xC8, "Equip Swords", 400, "Knight"),
            [0xCA] = new(0xCA, "Equip Crossbows", 350, "Archer"),
            [0xD5] = new(0xD5, "Concentration", 400, "Archer"),
            [0xD4] = new(0xD4, "Magick Defense Boost", 400, "White Mage"),
            [0xD3] = new(0xD3, "Magick Boost", 400, "Black Mage"),
            [0xD8] = new(0xD8, "Brawler", 200, "Monk"),
            [0xD7] = new(0xD7, "Poach", 200, "Thief"),
            [0xD2] = new(0xD2, "Defense Boost", 400, "Mystic"),
            [0xE2] = new(0xE2, "Swiftspell", 1000, "Time Mage"),
            [0xD1] = new(0xD1, "Attack Boost", 400, "Geomancer"),
            [0xCB] = new(0xCB, "Equip Polearms", 400, "Dragoon"),
            [0xCD] = new(0xCD, "Equip Guns", 800, "Orator"),
            [0xD6] = new(0xD6, "Tame", 500, "Orator"),
            [0xD9] = new(0xD9, "Beast Tongue", 100, "Orator"),
            [0xCE] = new(0xCE, "Halve MP", 1000, "Summoner"),
            [0xC9] = new(0xC9, "Equip Katana", 400, "Samurai"),
            [0xDC] = new(0xDC, "Doublehand", 900, "Samurai"),
            [0xDD] = new(0xDD, "Dual Wield", 1000, "Ninja"),
            [0xD0] = new(0xD0, "EXP Boost", 350, "Arithmetician"),
            [0xE4] = new(0xE4, "HP Boost", 2000, "Dark Knight"),
            [0xE5] = new(0xE5, "Vehemence", 400, "Dark Knight"),
        };

        public static readonly Dictionary<byte, AbilityInfo> MovementAbilities = new()
        {
            [0xE6] = new(0xE6, "Movement +1", 200, "Squire"),
            [0xFD] = new(0xFD, "Treasure Hunter", 100, "Chemist"),
            [0xE9] = new(0xE9, "Jump +1", 200, "Archer"),
            [0xED] = new(0xED, "Lifefont", 300, "Monk"),
            [0xE7] = new(0xE7, "Movement +2", 560, "Thief"),
            [0xEA] = new(0xEA, "Jump +2", 500, "Thief"),
            [0xF4] = new(0xF4, "Ignore Weather", 200, "Mystic"),
            [0xEE] = new(0xEE, "Manafont", 350, "Time Mage"),
            [0xF2] = new(0xF2, "Teleport", 650, "Time Mage"),
            [0xFA] = new(0xFA, "Levitate", 540, "Time Mage"),
            [0xF5] = new(0xF5, "Ignore Terrain", 220, "Geomancer"),
            [0xF8] = new(0xF8, "Lavawalking", 150, "Geomancer"),
            [0xEC] = new(0xEC, "Ignore Elevation", 700, "Dragoon"),
            [0xF7] = new(0xF7, "Swim", 300, "Samurai"),
            [0xF6] = new(0xF6, "Waterwalking", 420, "Ninja"),
            [0xEF] = new(0xEF, "Accrue EXP", 400, "Arithmetician"),
            [0xF0] = new(0xF0, "Accrue JP", 400, "Arithmetician"),
            [0xFB] = new(0xFB, "Fly", 900, "Bard"),
            [0xEB] = new(0xEB, "Jump +3", 600, "Dark Knight"),
            [0xE8] = new(0xE8, "Movement +3", 1000, "Bard"),
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
    }
}
