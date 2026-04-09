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
        /// Returns the skillset name that contains the given ability ID, or null if not found.
        /// </summary>
        public static string? GetSkillsetForAbilityId(int id)
        {
            foreach (var (skillsetName, abilities) in Skillsets)
            {
                foreach (var ability in abilities)
                {
                    if (ability.Id == id) return skillsetName;
                }
            }
            return null;
        }

        /// <summary>
        /// Filter a list of abilities to only those belonging to the given skillsets.
        /// Used to exclude abilities from unequipped skillsets (e.g. a Monk shouldn't
        /// show Mettle abilities even if the unit learned them as a Squire).
        /// </summary>
        public static List<ActionAbilityInfo> FilterBySkillsets(
            List<ActionAbilityInfo> abilities, IEnumerable<string> equippedSkillsets)
        {
            var allowed = new HashSet<string>(equippedSkillsets);
            return abilities.Where(a => {
                var skillset = GetSkillsetForAbilityId(a.Id);
                return skillset != null && allowed.Contains(skillset);
            }).ToList();
        }

        /// <summary>
        /// The condensed struct ability list only reflects the active (first-scanned) unit.
        /// During C+Up cycling, the list doesn't update for hovered units.
        /// Only read abilities for unit index 0 (the active unit).
        /// </summary>
        public static bool ShouldReadAbilities(int unitIndex) => unitIndex == 0;

        /// <summary>
        /// All skillsets with their ability lists. Used for looking up which skillset
        /// an ability belongs to and its position within the skillset.
        /// </summary>
        public static IReadOnlyDictionary<string, List<ActionAbilityInfo>> AllSkillsets => Skillsets;

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
            // NOTE: IDs are estimated from PSX data — only Mettle IDs verified in-game.
            // Ability NAMES and ORDER verified by user 2026-04-09.
            //                              ID    Name            MP  HRng  VR AoE HoE Target      Effect
            ["Fundaments"] = new()
            {
                new(0x1C6, "Focus",          0, "Self", 0, 1, 0, "self",     "Increases own physical attack power"),
                new(0x1C7, "Rush",           0, "1",    0, 1, 0, "enemy",    "Ramming attack, chance of knockback"),
                new(0x1C8, "Throw Stone",    0, "4",    0, 1, 0, "enemy",    "Lob a stone, chance of knockback"),
                new(0x1C9, "Salve",          0, "1",    0, 1, 0, "ally",     "Remove Poison, Blindness, and Silence from adjacent unit"),
            },
            ["Items"] = new()
            {
                new(0x19, "Potion",          0, "4",  0, 1, 0, "ally",   "Restore 30 HP"),
                new(0x1A, "Hi-Potion",       0, "4",  0, 1, 0, "ally",   "Restore 70 HP"),
                new(0x1B, "X-Potion",        0, "4",  0, 1, 0, "ally",   "Restore 150 HP"),
                new(0x1C, "Ether",           0, "4",  0, 1, 0, "ally",   "Restore 20 MP"),
                new(0x1D, "Hi-Ether",        0, "4",  0, 1, 0, "ally",   "Restore 50 MP"),
                new(0x1E, "Elixir",          0, "4",  0, 1, 0, "ally",   "Restore all HP and MP"),
                new(0x20, "Antidote",        0, "4",  0, 1, 0, "ally",   "Cure Poison"),
                new(0x21, "Eye Drop",        0, "4",  0, 1, 0, "ally",   "Cure Blind"),
                new(0x22, "Echo Herbs",      0, "4",  0, 1, 0, "ally",   "Cure Silence"),
                new(0x23, "Maiden's Kiss",   0, "4",  0, 1, 0, "ally",   "Cure Frog"),
                new(0x24, "Gold Needle",     0, "4",  0, 1, 0, "ally",   "Cure Petrify"),
                new(0x25, "Holy Water",      0, "4",  0, 1, 0, "ally",   "Cure Undead and Vampire"),
                new(0x26, "Remedy",          0, "4",  0, 1, 0, "ally",   "Cure most negative statuses"),
                new(0x1F, "Phoenix Down",    0, "4",  0, 1, 0, "ally",   "Revive KO'd unit"),
            },
            ["Arts of War"] = new()
            {
                new(0x30, "Rend Helm",       0, "1",  0, 1, 0, "enemy",  "Destroy target's headgear"),
                new(0x31, "Rend Armor",      0, "1",  0, 1, 0, "enemy",  "Destroy target's armor"),
                new(0x32, "Rend Shield",     0, "1",  0, 1, 0, "enemy",  "Destroy target's shield"),
                new(0x33, "Rend Weapon",     0, "1",  0, 1, 0, "enemy",  "Destroy target's weapon"),
                new(0x37, "Rend MP",         0, "1",  0, 1, 0, "enemy",  "Reduce target's MP"),
                new(0x34, "Rend Speed",      0, "1",  0, 1, 0, "enemy",  "Lower target's Speed"),
                new(0x35, "Rend Power",      0, "1",  0, 1, 0, "enemy",  "Lower target's physical attack"),
                new(0x36, "Rend Magick",     0, "1",  0, 1, 0, "enemy",  "Lower target's magickal attack"),
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
                new(0x00, "Firaja",         30, "4",  2, 2, 1, "enemy/AoE",  "Ultimate fire-element magick damage"),
                new(0x5E, "Thunder",         6, "4",  2, 2, 1, "enemy/AoE",  "Lightning-element magick damage"),
                new(0x5F, "Thundara",       12, "4",  2, 2, 1, "enemy/AoE",  "Stronger lightning-element magick damage"),
                new(0x60, "Thundaga",       24, "4",  2, 2, 1, "enemy/AoE",  "Strongest lightning-element magick damage"),
                new(0x00, "Thundaja",       30, "4",  2, 2, 1, "enemy/AoE",  "Ultimate lightning-element magick damage"),
                new(0x61, "Blizzard",        6, "4",  2, 2, 1, "enemy/AoE",  "Ice-element magick damage"),
                new(0x62, "Blizzara",       12, "4",  2, 2, 1, "enemy/AoE",  "Stronger ice-element magick damage"),
                new(0x63, "Blizzaga",       24, "4",  2, 2, 1, "enemy/AoE",  "Strongest ice-element magick damage"),
                new(0x00, "Blizzaja",       30, "4",  2, 2, 1, "enemy/AoE",  "Ultimate ice-element magick damage"),
                new(0x64, "Poison",          6, "4",  1, 1, 0, "enemy",      "Inflict Poison status"),
                new(0x65, "Toad",           14, "4",  1, 1, 0, "enemy",      "Turn target into a frog"),
                new(0x68, "Death",          24, "4",  1, 1, 0, "enemy",      "Inflict instant KO"),
                new(0x69, "Flare",          60, "5",  1, 1, 0, "enemy",      "Non-elemental magick damage"),
            },
            ["Martial Arts"] = new()
            {
                new(0x00, "Cyclone",         0, "1",  0, 2, 0, "enemy/AoE", "Wind damage to adjacent units"),
                new(0x78, "Pummel",          0, "1",  0, 1, 0, "enemy",    "Multi-hit physical damage"),
                new(0x79, "Aurablast",       0, "3",  0, 1, 0, "enemy",    "Ranged physical damage"),
                new(0x7A, "Shockwave",       0, "3",  0, 1, 0, "enemy",    "Line earth damage"),
                new(0x7B, "Doom Fist",       0, "1",  0, 1, 0, "enemy",    "Inflict Death Sentence on adjacent unit"),
                new(0x7C, "Purification",    0, "1",  0, 1, 0, "ally",     "Remove negative statuses from adjacent unit"),
                new(0x7D, "Chakra",          0, "1",  0, 2, 0, "self/AoE", "Restore own HP and MP, heals adjacent allies"),
                new(0x7E, "Revive",          0, "1",  0, 1, 0, "ally",     "Revive KO'd adjacent unit"),
            },
            ["Time Magicks"] = new()
            {
                new(0x85, "Haste",           8, "4",  1, 1, 0, "ally",       "Increase target's Speed"),
                new(0x86, "Hasteja",        30, "4",  2, 2, 1, "ally/AoE",   "Increase Speed (area)"),
                new(0x87, "Slow",            8, "4",  1, 1, 0, "enemy",      "Decrease target's Speed"),
                new(0x88, "Slowja",         30, "4",  2, 2, 1, "enemy/AoE",  "Decrease Speed (area)"),
                new(0x89, "Stop",           14, "4",  1, 1, 0, "enemy",      "Freeze target in time"),
                new(0x8A, "Immobilize",     10, "4",  1, 1, 0, "enemy",      "Prevent target from moving"),
                new(0x8B, "Float",           8, "4",  1, 1, 0, "ally",       "Levitate target"),
                new(0x8C, "Reflect",        12, "4",  1, 1, 0, "ally",       "Reflect magick spells back"),
                new(0x8D, "Quick",          24, "4",  1, 1, 0, "ally",       "Grant immediate extra turn"),
                new(0x8E, "Gravity",         4, "4",  1, 1, 0, "enemy",      "Reduce HP by 25%"),
                new(0x8F, "Graviga",        10, "4",  1, 1, 0, "enemy",      "Reduce HP by 50%"),
                new(0x90, "Meteor",         70, "5",  2, 3, 2, "enemy/AoE",  "Massive non-elemental damage"),
            },
            ["Aim"] = new()
            {
                new(0x00, "Aim +1",          0, "5",  0, 1, 0, "enemy",  "Ranged attack with +1 charge time"),
                new(0x00, "Aim +2",          0, "5",  0, 1, 0, "enemy",  "Ranged attack with +2 charge time"),
                new(0x00, "Aim +3",          0, "5",  0, 1, 0, "enemy",  "Ranged attack with +3 charge time"),
                new(0x00, "Aim +4",          0, "5",  0, 1, 0, "enemy",  "Ranged attack with +4 charge time"),
                new(0x00, "Aim +5",          0, "5",  0, 1, 0, "enemy",  "Ranged attack with +5 charge time"),
                new(0x00, "Aim +7",          0, "5",  0, 1, 0, "enemy",  "Ranged attack with +7 charge time"),
                new(0x00, "Aim +10",         0, "5",  0, 1, 0, "enemy",  "Ranged attack with +10 charge time"),
                new(0x00, "Aim +20",         0, "5",  0, 1, 0, "enemy",  "Ranged attack with +20 charge time"),
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
            ["Throw"] = new()
            {
                new(0x00, "Throw Shuriken",      0, "5",  0, 1, 0, "enemy",  "Throw a shuriken"),
                new(0x00, "Throw Bombs",         0, "5",  0, 1, 0, "enemy",  "Throw a bomb"),
                new(0x00, "Throw Daggers",       0, "5",  0, 1, 0, "enemy",  "Throw a dagger"),
                new(0x00, "Throw Swords",        0, "5",  0, 1, 0, "enemy",  "Throw a sword"),
                new(0x00, "Throw Flails",        0, "5",  0, 1, 0, "enemy",  "Throw a flail"),
                new(0x00, "Throw Katana",        0, "5",  0, 1, 0, "enemy",  "Throw a katana"),
                new(0x00, "Throw Ninja Blades",  0, "5",  0, 1, 0, "enemy",  "Throw a ninja blade"),
                new(0x00, "Throw Axes",          0, "5",  0, 1, 0, "enemy",  "Throw an axe"),
                new(0x00, "Throw Polearms",      0, "5",  0, 1, 0, "enemy",  "Throw a polearm"),
                new(0x00, "Throw Poles",         0, "5",  0, 1, 0, "enemy",  "Throw a pole"),
                new(0x00, "Throw Knight's Swords", 0, "5", 0, 1, 0, "enemy", "Throw a knight's sword"),
                new(0x00, "Throw Books",         0, "5",  0, 1, 0, "enemy",  "Throw a book"),
            },
            ["Jump"] = new()
            {
                new(0x00, "Horizontal Jump +1",  0, "1",  0, 1, 0, "enemy",  "Jump attack range 1"),
                new(0x00, "Horizontal Jump +2",  0, "2",  0, 1, 0, "enemy",  "Jump attack range 2"),
                new(0x00, "Horizontal Jump +3",  0, "3",  0, 1, 0, "enemy",  "Jump attack range 3"),
                new(0x00, "Horizontal Jump +4",  0, "4",  0, 1, 0, "enemy",  "Jump attack range 4"),
                new(0x00, "Horizontal Jump +7",  0, "7",  0, 1, 0, "enemy",  "Jump attack range 7"),
                new(0x00, "Vertical Jump +2",    0, "1",  2, 1, 0, "enemy",  "Jump attack vertical 2"),
                new(0x00, "Vertical Jump +3",    0, "1",  3, 1, 0, "enemy",  "Jump attack vertical 3"),
                new(0x00, "Vertical Jump +4",    0, "1",  4, 1, 0, "enemy",  "Jump attack vertical 4"),
                new(0x00, "Vertical Jump +5",    0, "1",  5, 1, 0, "enemy",  "Jump attack vertical 5"),
                new(0x00, "Vertical Jump +6",    0, "1",  6, 1, 0, "enemy",  "Jump attack vertical 6"),
                new(0x00, "Vertical Jump +7",    0, "1",  7, 1, 0, "enemy",  "Jump attack vertical 7"),
                new(0x00, "Vertical Jump +8",    0, "1",  8, 1, 0, "enemy",  "Jump attack vertical 8"),
            },
            ["Iaido"] = new()
            {
                new(0x00, "Ashura",          0, "Self", 0, 2, 0, "ally/AoE", "AoE heal with katana"),
                new(0x00, "Kotetsu",         0, "Self", 0, 2, 0, "enemy/AoE","AoE damage with katana"),
                new(0x00, "Bizen Osafune",   0, "Self", 0, 2, 0, "enemy/AoE","AoE damage with katana"),
                new(0x00, "Murasame",        0, "Self", 0, 2, 0, "ally/AoE", "AoE heal with katana"),
                new(0x00, "Ame-no-Murakumo", 0, "Self", 0, 2, 0, "ally/AoE", "AoE Haste with katana"),
                new(0x00, "Kiyomori",        0, "Self", 0, 2, 0, "ally/AoE", "AoE Protect+Shell with katana"),
                new(0x00, "Muramasa",        0, "Self", 0, 2, 0, "enemy/AoE","AoE damage+Doom with katana"),
                new(0x00, "Kiku-ichimonji",  0, "Self", 0, 2, 0, "enemy/AoE","AoE damage with katana"),
                new(0x00, "Masamune",        0, "Self", 0, 2, 0, "ally/AoE", "AoE Haste+Regen with katana"),
                new(0x00, "Chirijiraden",    0, "Self", 0, 2, 0, "enemy/AoE","AoE massive damage with katana"),
            },
            ["Speechcraft"] = new()
            {
                new(0x00, "Entice",          0, "3",  0, 1, 0, "enemy",  "Recruit enemy unit"),
                new(0x00, "Stall",           0, "3",  0, 1, 0, "enemy",  "Reduce target's CT"),
                new(0x00, "Praise",          0, "3",  0, 1, 0, "ally",   "Increase target's Brave"),
                new(0x00, "Intimidate",      0, "3",  0, 1, 0, "enemy",  "Lower target's Brave"),
                new(0x00, "Preach",          0, "3",  0, 1, 0, "ally",   "Increase target's Faith"),
                new(0x00, "Enlighten",       0, "3",  0, 1, 0, "enemy",  "Lower target's Faith"),
                new(0x00, "Condemn",         0, "3",  0, 1, 0, "enemy",  "Inflict Death Sentence"),
                new(0x00, "Defraud",         0, "3",  0, 1, 0, "enemy",  "Steal gil from target"),
                new(0x00, "Insult",          0, "3",  0, 1, 0, "enemy",  "Inflict Berserk"),
                new(0x00, "Mimic Darlavon",  0, "3",  0, 2, 0, "enemy/AoE", "AoE Sleep"),
            },
            ["Mystic Arts"] = new()
            {
                new(0x00, "Umbra",           0, "4",  0, 1, 0, "enemy",  "Inflict Blind"),
                new(0x00, "Empowerment",     0, "4",  0, 1, 0, "enemy",  "Drain HP"),
                new(0x00, "Invigoration",    0, "4",  0, 1, 0, "enemy",  "Drain MP"),
                new(0x00, "Belief",          0, "4",  0, 1, 0, "ally",   "Increase Faith"),
                new(0x00, "Disbelief",       0, "4",  0, 1, 0, "enemy",  "Lower Faith"),
                new(0x00, "Corruption",      0, "4",  0, 1, 0, "enemy",  "Inflict Undead"),
                new(0x00, "Quiescence",      0, "4",  0, 1, 0, "enemy",  "Inflict Silence"),
                new(0x00, "Fervor",          0, "4",  0, 1, 0, "enemy",  "Inflict Berserk"),
                new(0x00, "Trepidation",     0, "4",  0, 1, 0, "enemy",  "Inflict Chicken/Coward"),
                new(0x00, "Delirium",        0, "4",  0, 1, 0, "enemy",  "Inflict Confuse"),
                new(0x00, "Harmony",         0, "4",  0, 1, 0, "ally",   "Remove negative statuses"),
                new(0x00, "Hesitation",      0, "4",  0, 1, 0, "enemy",  "Inflict Don't Act"),
                new(0x00, "Repose",          0, "4",  0, 1, 0, "enemy",  "Inflict Sleep"),
                new(0x00, "Induration",      0, "4",  0, 1, 0, "enemy",  "Inflict Petrify"),
            },
            ["Summon"] = new()
            {
                new(0x00, "Moogle",         8, "4",  2, 2, 1, "ally/AoE",   "Summon Moogle — AoE heal"),
                new(0x00, "Shiva",         24, "4",  2, 2, 1, "enemy/AoE",  "Summon Shiva — ice damage"),
                new(0x00, "Ramuh",         24, "4",  2, 2, 1, "enemy/AoE",  "Summon Ramuh — lightning damage"),
                new(0x00, "Ifrit",         24, "4",  2, 2, 1, "enemy/AoE",  "Summon Ifrit — fire damage"),
                new(0x00, "Titan",         30, "4",  2, 2, 1, "enemy/AoE",  "Summon Titan — earth damage"),
                new(0x00, "Golem",         40, "Self",0, 1, 0, "ally",      "Summon Golem — absorb physical damage"),
                new(0x00, "Carbuncle",     30, "4",  2, 2, 1, "ally/AoE",   "Summon Carbuncle — party Reflect"),
                new(0x00, "Bahamut",       60, "4",  2, 3, 1, "enemy/AoE",  "Summon Bahamut — massive non-elemental damage"),
                new(0x00, "Odin",          50, "4",  2, 3, 1, "enemy/AoE",  "Summon Odin — instant KO chance"),
                new(0x00, "Leviathan",     48, "4",  2, 3, 1, "enemy/AoE",  "Summon Leviathan — water damage"),
                new(0x00, "Salamander",    48, "4",  2, 3, 1, "enemy/AoE",  "Summon Salamander — fire damage"),
                new(0x00, "Sylph",         26, "4",  2, 2, 1, "ally/AoE",   "Summon Sylph — AoE heal+drain"),
                new(0x00, "Faerie",        26, "4",  2, 2, 1, "ally/AoE",   "Summon Faerie — AoE heal"),
                new(0x00, "Lich",          40, "4",  2, 3, 1, "enemy/AoE",  "Summon Lich — dark damage"),
                new(0x00, "Cyclops",       62, "4",  2, 3, 1, "enemy/AoE",  "Summon Cyclops — non-elemental damage"),
                new(0x00, "Zodiark",       99, "4",  2, 4, 1, "enemy/AoE",  "Summon Zodiark — ultimate damage"),
            },
            ["Geomancy"] = new()
            {
                new(0x00, "Sinkhole",        0, "4",  0, 2, 0, "enemy/AoE", "Earth terrain attack"),
                new(0x00, "Torrent",         0, "4",  0, 2, 0, "enemy/AoE", "Water terrain attack"),
                new(0x00, "Tanglevine",      0, "4",  0, 2, 0, "enemy/AoE", "Grass terrain attack"),
                new(0x00, "Contortion",      0, "4",  0, 2, 0, "enemy/AoE", "Stone terrain attack"),
                new(0x00, "Tremor",          0, "4",  0, 2, 0, "enemy/AoE", "Earth terrain attack"),
                new(0x00, "Wind Slash",      0, "4",  0, 2, 0, "enemy/AoE", "Wind terrain attack"),
                new(0x00, "Will-o'-the-Wisp",0, "4",  0, 2, 0, "enemy/AoE", "Fire terrain attack"),
                new(0x00, "Quicksand",       0, "4",  0, 2, 0, "enemy/AoE", "Sand terrain attack"),
                new(0x00, "Sandstorm",       0, "4",  0, 2, 0, "enemy/AoE", "Desert terrain attack"),
                new(0x00, "Snowstorm",       0, "4",  0, 2, 0, "enemy/AoE", "Ice terrain attack"),
                new(0x00, "Wind Blast",      0, "4",  0, 2, 0, "enemy/AoE", "High terrain attack"),
                new(0x00, "Magma Surge",     0, "4",  0, 2, 0, "enemy/AoE", "Lava terrain attack"),
            },
            ["Bardsong"] = new()
            {
                new(0x00, "Seraph Song",     0, "Self", 0, 99, 0, "ally/AoE", "AoE MP regen"),
                new(0x00, "Life's Anthem",   0, "Self", 0, 99, 0, "ally/AoE", "AoE HP regen"),
                new(0x00, "Rousing Melody",  0, "Self", 0, 99, 0, "ally/AoE", "AoE Speed+1"),
                new(0x00, "Battle Chant",    0, "Self", 0, 99, 0, "ally/AoE", "AoE PA+1"),
                new(0x00, "Magickal Refrain",0, "Self", 0, 99, 0, "ally/AoE", "AoE MA+1"),
                new(0x00, "Nameless Song",   0, "Self", 0, 99, 0, "ally/AoE", "AoE random positive status"),
                new(0x00, "Finale",          0, "Self", 0, 99, 0, "ally/AoE", "AoE full heal"),
            },
            ["Arithmeticks"] = new()
            {
                new(0x00, "Target CT",           0, "Self", 0, 1, 0, "self", "Target units by CT value"),
                new(0x00, "Target Level",        0, "Self", 0, 1, 0, "self", "Target units by Level"),
                new(0x00, "Target EXP",          0, "Self", 0, 1, 0, "self", "Target units by EXP"),
                new(0x00, "Target Elevation",    0, "Self", 0, 1, 0, "self", "Target units by tile height"),
                new(0x00, "Prime",               0, "Self", 0, 1, 0, "self", "Modifier: prime numbers"),
                new(0x00, "Multiple of 5",       0, "Self", 0, 1, 0, "self", "Modifier: multiples of 5"),
                new(0x00, "Multiple of 4",       0, "Self", 0, 1, 0, "self", "Modifier: multiples of 4"),
                new(0x00, "Multiple of 3",       0, "Self", 0, 1, 0, "self", "Modifier: multiples of 3"),
            },
            ["Holy Sword"] = new()
            {
                new(0x1DD, "Judgment Blade",      0, "3", 0, 2, 0, "enemy/AoE", "Weapon damage + may inflict Stop"),
                new(0x1DE, "Cleansing Strike",    0, "3", 0, 2, 0, "enemy/AoE", "Weapon damage + may inflict Doom"),
                new(0x1DF, "Northswain's Strike", 0, "3", 0, 2, 0, "enemy/AoE", "Weapon damage + may inflict Death"),
                new(0x1E0, "Hallowed Bolt",       0, "3", 0, 2, 0, "enemy/AoE", "Weapon damage + may inflict Silence"),
            },
        };
    }
}
