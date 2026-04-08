using System;
using System.Collections.Generic;
using System.Linq;

namespace FFTColorCustomizer.GameBridge
{
    public record ActionAbilityInfo(
        int Id,
        string Name,
        int MpCost,
        string HRange,      // Horizontal Range: "Self", "1", "3", "4", etc.
        int VRange,          // Vertical Range (99 = Unlimited)
        int AoE,             // Area of Effect in tiles (1 = single target)
        int HoE,             // Height of Effect (vertical AoE tolerance)
        string Target,       // "self", "ally", "enemy", "AoE", "ally/AoE", "enemy/AoE"
        string Effect,       // Description
        int CastSpeed = 0,   // Cast Speed (0 = instant, >0 = charge time)
        string? Element = null,       // Element: "Fire", "Ice", "Lightning", "Holy", etc. null = non-elemental
        string? AddedEffect = null,   // "Adds: Haste", "Removes: Poison, Blindness, Silence", etc.
        bool Reflectable = false,     // Can be reflected by Reflect status
        bool Arithmetickable = false  // Can be cast via Arithmeticks
    );

    /// <summary>
    /// Lookup tables for action abilities and helpers for filtering by learned status.
    /// Action abilities are the ones that appear in the Abilities submenu during battle
    /// (e.g. Fire, Cure, Focus, Tailwind — NOT reaction/support/movement).
    /// </summary>
    public static class ActionAbilityLookup
    {
        /// <summary>
        /// Parse a FFFF-terminated uint16 LE byte array into a set of ability IDs.
        /// Used to read the learned ability list from the condensed struct.
        /// </summary>
        public static HashSet<int> ParseLearnedIdsFromBytes(byte[] bytes)
        {
            var ids = new HashSet<int>();
            if (bytes == null || bytes.Length < 2) return ids;

            for (int i = 0; i + 1 < bytes.Length; i += 2)
            {
                int val = bytes[i] | (bytes[i + 1] << 8);
                if (val == 0xFFFF) break;
                ids.Add(val);
            }
            return ids;
        }

        /// <summary>
        /// Filter a skillset's abilities to only those the unit has learned.
        /// </summary>
        public static List<ActionAbilityInfo> FilterLearned(
            List<ActionAbilityInfo> skillsetAbilities,
            HashSet<int> learnedIds)
        {
            return skillsetAbilities.Where(a => learnedIds.Contains(a.Id)).ToList();
        }

        /// <summary>
        /// Get the full ability list for a skillset by name.
        /// Returns all abilities in that skillset (whether learned or not).
        /// </summary>
        public static List<ActionAbilityInfo>? GetSkillsetAbilities(string skillsetName)
        {
            return Skillsets.TryGetValue(skillsetName, out var list) ? list : null;
        }

        /// <summary>
        /// Look up an action ability by its IC remaster ID.
        /// Returns null if the ID is not in any known skillset.
        /// </summary>
        public static ActionAbilityInfo? GetById(int id)
        {
            return AllAbilities.TryGetValue(id, out var info) ? info : null;
        }

        /// <summary>
        /// Given a list of learned ability IDs (from the condensed struct FFFF-terminated list),
        /// return all matching ability infos with names and properties.
        /// Excludes Attack (0x22) since it's always available.
        /// </summary>
        public static List<ActionAbilityInfo> ResolveLearnedAbilities(HashSet<int> learnedIds)
        {
            var result = new List<ActionAbilityInfo>();
            foreach (var id in learnedIds)
            {
                if (id == ATTACK_ID) continue; // Attack is always available, don't list it
                if (AllAbilities.TryGetValue(id, out var info))
                    result.Add(info);
            }
            return result;
        }

        public const int ATTACK_ID = 0x22;

        /// <summary>
        /// The condensed struct ability list only reflects the active (first-scanned) unit.
        /// During C+Up cycling, the list doesn't update for hovered units.
        /// Only read abilities for unit index 0 (the active unit).
        /// </summary>
        public static bool ShouldReadAbilities(int unitIndex) => unitIndex == 0;

        // Flat lookup: ability ID → info (built lazily from all skillsets)
        private static Dictionary<int, ActionAbilityInfo>? _allAbilities;
        private static Dictionary<int, ActionAbilityInfo> AllAbilities
        {
            get
            {
                if (_allAbilities == null)
                {
                    var dict = new Dictionary<int, ActionAbilityInfo>();
                    foreach (var skillset in Skillsets.Values)
                        foreach (var ability in skillset)
                            dict.TryAdd(ability.Id, ability);
                    _allAbilities = dict;
                }
                return _allAbilities;
            }
        }

        // ============================================================
        // Skillset lookup tables
        // Sources: Wiki/Abilities.md, Wiki/SkillsetsAndStatus.md
        // ============================================================

        private static readonly Dictionary<string, List<ActionAbilityInfo>> Skillsets = new()
        {
            // Verified in-game 2026-04-08: IDs from condensed struct FFFF-terminated list
            //                          ID    Name            MP  HRng VR AoE HoE Target     Effect
            ["Mettle"] = new()
            {
                // All values verified in-game 2026-04-08 (VRange 99 = Unlimited)
                new(0x41, "Focus",       0, "Self",  0, 1, 0, "self",   "Increases one's own physical attack power for the duration of battle"),
                new(0x55, "Rush",        0, "1",     1, 1, 0, "enemy",  "Attacks by ramming an adjacent unit, and has a chance of knocking it back. The damage dealt varies greatly"),
                new(0x8B, "Throw Stone", 0, "4",    99, 1, 0, "enemy",  "Lobs a stone at a distant unit, and has a chance of knocking it back. The damage dealt varies greatly"),
                new(0x9F, "Salve",       0, "1",     2, 1, 0, "ally",   "A battlefield treatment that removes Poison, Blindness, and Silence from an adjacent unit",
                    AddedEffect: "Removes: Poison, Blindness, Silence"),
                new(0xAE, "Tailwind",    0, "3",    99, 1, 0, "ally",   "Words of encouragement that increase the speed of a distant unit for the duration of battle"),
                new(0xB5, "Chant",       0, "1",     3, 1, 0, "ally",   "Sacrifices HP to restore the HP of an adjacent unit"),
                new(0xCD, "Steel",       0, "3",    99, 1, 0, "ally",   "Steels the will of a distant unit, increasing bravery for the duration of battle"),
                new(0xD7, "Shout",       0, "Self",  0, 1, 0, "self",   "A hearty battle cry that increases one's own bravery, speed, and physical and magickal attack power for the duration of battle"),
                new(0xE7, "Ultima",     10, "4",    99, 2, 1, "AoE",    "Almighty magick that attacks distant foes",
                    CastSpeed: 20, Reflectable: false, Arithmetickable: false),
            },
            // NOTE: IDs below are estimated from PSX data — need in-game verification
            //                              ID    Name            MP  HRng  VR AoE HoE Target      Effect
            ["Fundaments"] = new()
            {
                new(0x1C6, "Focus",          0, "Self", 0, 1, 0, "self",     "Increases own physical attack power for the duration of battle"),
                new(0x1C7, "Rush",           0, "1",    0, 1, 0, "enemy",    "Attacks by ramming an adjacent unit, chance of knocking it back"),
                new(0x1C8, "Throw Stone",    0, "4",    0, 1, 0, "enemy",    "Lobs a stone at a distant unit, chance of knocking it back"),
                new(0x1C9, "Tailwind",       0, "3",    0, 1, 0, "ally",     "Increases the speed of a distant unit for the duration of battle"),
                new(0x1CA, "Chant",          0, "1",    0, 1, 0, "ally",     "Sacrifices HP to restore the HP of an adjacent unit"),
                new(0x1CB, "Steel",          0, "3",    0, 1, 0, "ally",     "Increases bravery of a distant unit for the duration of battle"),
            },
            ["Items"] = new()
            {
                new(0x19, "Potion",          0, "4",  0, 1, 0, "ally",   "Restore 30 HP"),
                new(0x1A, "Hi-Potion",       0, "4",  0, 1, 0, "ally",   "Restore 70 HP"),
                new(0x1B, "X-Potion",        0, "4",  0, 1, 0, "ally",   "Restore 150 HP"),
                new(0x1C, "Ether",           0, "4",  0, 1, 0, "ally",   "Restore 20 MP"),
                new(0x1D, "Hi-Ether",        0, "4",  0, 1, 0, "ally",   "Restore 50 MP"),
                new(0x1E, "Elixir",          0, "4",  0, 1, 0, "ally",   "Restore all HP and MP"),
                new(0x1F, "Phoenix Down",    0, "4",  0, 1, 0, "ally",   "Revive KO'd unit"),
                new(0x20, "Antidote",        0, "4",  0, 1, 0, "ally",   "Cure Poison"),
                new(0x21, "Eye Drop",        0, "4",  0, 1, 0, "ally",   "Cure Blind"),
                new(0x22, "Echo Herbs",      0, "4",  0, 1, 0, "ally",   "Cure Silence"),
                new(0x23, "Maiden's Kiss",   0, "4",  0, 1, 0, "ally",   "Cure Frog"),
                new(0x24, "Gold Needle",     0, "4",  0, 1, 0, "ally",   "Cure Petrify"),
                new(0x25, "Holy Water",      0, "4",  0, 1, 0, "ally",   "Cure Undead and Vampire"),
                new(0x26, "Remedy",          0, "4",  0, 1, 0, "ally",   "Cure most negative statuses"),
            },
            ["Arts of War"] = new()
            {
                new(0x30, "Rend Helm",       0, "1",  0, 1, 0, "enemy",  "Destroy target's headgear"),
                new(0x31, "Rend Armor",      0, "1",  0, 1, 0, "enemy",  "Destroy target's armor"),
                new(0x32, "Rend Shield",     0, "1",  0, 1, 0, "enemy",  "Destroy target's shield"),
                new(0x33, "Rend Weapon",     0, "1",  0, 1, 0, "enemy",  "Destroy target's weapon"),
                new(0x34, "Rend Speed",      0, "1",  0, 1, 0, "enemy",  "Lower target's Speed"),
                new(0x35, "Rend Power",      0, "1",  0, 1, 0, "enemy",  "Lower target's physical attack"),
                new(0x36, "Rend Magick",     0, "1",  0, 1, 0, "enemy",  "Lower target's magickal attack"),
                new(0x37, "Rend MP",         0, "1",  0, 1, 0, "enemy",  "Reduce target's MP"),
            },
            ["White Magicks"] = new()
            {
                new(0x48, "Cure",            6, "4",  2, 2, 1, "ally/AoE",   "Restore HP"),
                new(0x49, "Cura",           10, "4",  2, 2, 1, "ally/AoE",   "Restore more HP"),
                new(0x4A, "Curaga",         16, "4",  2, 2, 1, "ally/AoE",   "Restore even more HP"),
                new(0x4B, "Curaja",         20, "4",  2, 3, 1, "ally/AoE",   "Restore massive HP"),
                new(0x4C, "Raise",          10, "4",  1, 1, 0, "ally",       "Revive KO'd unit with ~50% HP"),
                new(0x4D, "Arise",          20, "4",  1, 1, 0, "ally",       "Revive KO'd unit with 100% HP"),
                new(0x4E, "Reraise",        16, "4",  1, 1, 0, "ally",       "Auto-revive when KO'd"),
                new(0x4F, "Regen",           8, "4",  1, 1, 0, "ally",       "Gradually restore HP over time"),
                new(0x50, "Protect",         6, "4",  1, 1, 0, "ally",       "Reduce physical damage taken"),
                new(0x51, "Protectja",      24, "4",  2, 2, 1, "ally/AoE",   "Reduce physical damage taken (area)"),
                new(0x52, "Shell",           6, "4",  1, 1, 0, "ally",       "Reduce magick damage taken"),
                new(0x53, "Shellja",        24, "4",  2, 2, 1, "ally/AoE",   "Reduce magick damage taken (area)"),
                new(0x54, "Wall",           24, "4",  1, 1, 0, "ally",       "Grant Protect and Shell"),
                new(0x55, "Esuna",          18, "4",  1, 1, 0, "ally",       "Remove most negative statuses"),
                new(0x56, "Holy",           56, "5",  1, 1, 0, "enemy",      "Holy-element magick damage"),
            },
            ["Black Magicks"] = new()
            {
                new(0x5B, "Fire",            6, "4",  2, 2, 1, "enemy/AoE",  "Fire-element magick damage"),
                new(0x5C, "Fira",           12, "4",  2, 2, 1, "enemy/AoE",  "Stronger fire-element magick damage"),
                new(0x5D, "Firaga",         24, "4",  2, 2, 1, "enemy/AoE",  "Strongest fire-element magick damage"),
                new(0x5E, "Thunder",         6, "4",  2, 2, 1, "enemy/AoE",  "Lightning-element magick damage"),
                new(0x5F, "Thundara",       12, "4",  2, 2, 1, "enemy/AoE",  "Stronger lightning-element magick damage"),
                new(0x60, "Thundaga",       24, "4",  2, 2, 1, "enemy/AoE",  "Strongest lightning-element magick damage"),
                new(0x61, "Blizzard",        6, "4",  2, 2, 1, "enemy/AoE",  "Ice-element magick damage"),
                new(0x62, "Blizzara",       12, "4",  2, 2, 1, "enemy/AoE",  "Stronger ice-element magick damage"),
                new(0x63, "Blizzaga",       24, "4",  2, 2, 1, "enemy/AoE",  "Strongest ice-element magick damage"),
                new(0x64, "Poison",          6, "4",  1, 1, 0, "enemy",      "Inflict Poison status"),
                new(0x65, "Toad",           14, "4",  1, 1, 0, "enemy",      "Turn target into a frog"),
                new(0x66, "Gravity",        24, "4",  1, 1, 0, "enemy",      "Reduce target's HP by 25%"),
                new(0x67, "Graviga",        32, "4",  1, 1, 0, "enemy",      "Reduce target's HP by 50%"),
                new(0x68, "Death",          24, "4",  1, 1, 0, "enemy",      "Inflict instant KO"),
                new(0x69, "Flare",          60, "5",  1, 1, 0, "enemy",      "Non-elemental magick damage"),
            },
            ["Martial Arts"] = new()
            {
                new(0x78, "Pummel",          0, "1",  0, 1, 0, "enemy",    "Physical damage to adjacent unit"),
                new(0x79, "Aurablast",       0, "3",  0, 1, 0, "enemy",    "Ranged physical damage"),
                new(0x7A, "Shockwave",       0, "3",  0, 1, 0, "enemy",    "Ranged physical damage"),
                new(0x7B, "Doom Fist",       0, "1",  0, 1, 0, "enemy",    "Inflict Death Sentence on adjacent unit"),
                new(0x7C, "Purification",    0, "1",  0, 1, 0, "ally",     "Remove negative statuses from adjacent unit"),
                new(0x7D, "Chakra",          0, "1",  0, 2, 0, "self/AoE", "Restore own HP and MP, heals adjacent allies"),
                new(0x7E, "Revive",          0, "1",  0, 1, 0, "ally",     "Revive KO'd adjacent unit"),
            },
            ["Time Magicks"] = new()
            {
                new(0x85, "Haste",           8, "4",  1, 1, 0, "ally",       "Increase target's Speed by 50%"),
                new(0x86, "Hasteja",        30, "4",  2, 2, 1, "ally/AoE",   "Increase Speed by 50% (area)"),
                new(0x87, "Slow",            8, "4",  1, 1, 0, "enemy",      "Decrease target's Speed by 50%"),
                new(0x88, "Slowja",         30, "4",  2, 2, 1, "enemy/AoE",  "Decrease Speed by 50% (area)"),
                new(0x89, "Stop",           14, "4",  1, 1, 0, "enemy",      "Freeze target in time"),
                new(0x8A, "Immobilize",     10, "4",  1, 1, 0, "enemy",      "Prevent target from moving"),
                new(0x8B, "Float",           8, "4",  1, 1, 0, "ally",       "Levitate target, avoiding terrain effects"),
                new(0x8C, "Reflect",        12, "4",  1, 1, 0, "ally",       "Reflect magick spells back at caster"),
                new(0x8D, "Quick",          24, "4",  1, 1, 0, "ally",       "Grant target an immediate extra turn"),
                new(0x8E, "Gravity",         4, "4",  1, 1, 0, "enemy",      "Reduce target's HP by 25%"),
                new(0x8F, "Graviga",        10, "4",  1, 1, 0, "enemy",      "Reduce target's HP by 50%"),
                new(0x90, "Meteor",         70, "5",  2, 3, 2, "enemy/AoE",  "Massive non-elemental magick damage"),
            },
            ["Holy Sword"] = new()
            {
                new(0x1DD, "Judgment Blade",      0, "3", 0, 2, 0, "enemy/AoE", "Weapon-based damage + may inflict Stop"),
                new(0x1DE, "Cleansing Strike",    0, "3", 0, 2, 0, "enemy/AoE", "Weapon-based damage + may inflict Doom"),
                new(0x1DF, "Northswain's Strike", 0, "3", 0, 2, 0, "enemy/AoE", "Weapon-based damage + may inflict Death"),
                new(0x1E0, "Hallowed Bolt",       0, "3", 0, 2, 0, "enemy/AoE", "Weapon-based damage + may inflict Silence"),
            },
            ["Aim"] = new()
            {
                new(0x38, "Leg Shot",        0, "5",  0, 1, 0, "enemy",  "Inflict Immobilize on distant target"),
                new(0x39, "Arm Shot",        0, "5",  0, 1, 0, "enemy",  "Inflict Disable on distant target"),
                new(0x3A, "Seal Evil",       0, "5",  0, 1, 0, "enemy",  "Petrify undead target"),
            },
            ["Steal"] = new()
            {
                new(0x98, "Steal Gil",       0, "1",  0, 1, 0, "enemy",  "Steal money from adjacent unit"),
                new(0x99, "Steal Heart",     0, "1",  0, 1, 0, "enemy",  "Charm adjacent unit (opposite sex only)"),
                new(0x9A, "Steal Helm",      0, "1",  0, 1, 0, "enemy",  "Steal headgear from adjacent unit"),
                new(0x9B, "Steal Armor",     0, "1",  0, 1, 0, "enemy",  "Steal body armor from adjacent unit"),
                new(0x9C, "Steal Shield",    0, "1",  0, 1, 0, "enemy",  "Steal shield from adjacent unit"),
                new(0x9D, "Steal Weapon",    0, "1",  0, 1, 0, "enemy",  "Steal weapon from adjacent unit"),
                new(0x9E, "Steal Accessory", 0, "1",  0, 1, 0, "enemy",  "Steal accessory from adjacent unit"),
                new(0x9F, "Steal EXP",       0, "1",  0, 1, 0, "enemy",  "Steal experience from adjacent unit"),
            },
            ["Geomancy"] = new()
            {
                new(0xA1, "Geomancy",        0, "4",  0, 2, 0, "enemy/AoE", "Terrain-based magick (ability auto-selected by terrain type)"),
            },
        };
    }
}
