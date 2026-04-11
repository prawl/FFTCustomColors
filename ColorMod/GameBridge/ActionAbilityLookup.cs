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
        /// Skillsets that share ability pools. The game uses Mettle IDs for all units
        /// regardless of whether they're a Squire (Fundaments) or have Mettle equipped.
        /// </summary>
        private static readonly Dictionary<string, string[]> SkillsetAliases = new()
        {
            ["Fundaments"] = new[] { "Fundaments", "Mettle" },
            ["Mettle"] = new[] { "Mettle", "Fundaments" },
        };

        /// <summary>
        /// Filter a list of abilities to only those belonging to the given skillsets.
        /// Used to exclude abilities from unequipped skillsets (e.g. a Monk shouldn't
        /// show Mettle abilities even if the unit learned them as a Squire).
        /// </summary>
        public static List<ActionAbilityInfo> FilterBySkillsets(
            List<ActionAbilityInfo> abilities, IEnumerable<string> equippedSkillsets)
        {
            var allowed = new HashSet<string>();
            foreach (var s in equippedSkillsets)
            {
                allowed.Add(s);
                if (SkillsetAliases.TryGetValue(s, out var aliases))
                    foreach (var a in aliases)
                        allowed.Add(a);
            }
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
            // All values verified in-game 2026-04-11 by user. VRange 99 = Unlimited.
            //                           Id    Name          MP  HRng VR AoE HoE Target       Effect                                      CastSpeed  Element     AddedEffect  Reflectable  Arithmetickable
            ["White Magicks"] = new()
            {
                new(0x48, "Cure",            6, "4",    99, 2, 1, "ally/AoE",   "Magickal effect that restores HP.",                       CastSpeed: 25,                                           Reflectable: true, Arithmetickable: true),
                new(0x49, "Cura",           10, "4",    99, 2, 1, "ally/AoE",   "Magickal effect that restores more HP.",                  CastSpeed: 20,                                           Reflectable: true, Arithmetickable: true),
                new(0x4A, "Curaga",         16, "4",    99, 2, 2, "ally/AoE",   "Magickal effect that restores even more HP.",             CastSpeed: 15,                                           Reflectable: true, Arithmetickable: true),
                new(0x4B, "Curaja",         20, "4",    99, 2, 3, "ally/AoE",   "Magickal effect that restores massive HP.",               CastSpeed: 10,                                           Reflectable: false, Arithmetickable: false),
                new(0x4C, "Raise",          10, "4",    99, 1, 0, "ally",       "Magickal effect that revives a KO'd unit with ~50% HP.", CastSpeed: 25,                                           Reflectable: true, Arithmetickable: true),
                new(0x4D, "Arise",          20, "4",    99, 1, 0, "ally",       "Magickal effect that revives a KO'd unit with 100% HP.", CastSpeed: 10,                                           Reflectable: true, Arithmetickable: true),
                new(0x4E, "Reraise",        16, "3",    99, 1, 0, "ally",       "Magickal effect that grants auto-revive when KO'd.",     CastSpeed: 15,                                           Reflectable: true, Arithmetickable: true),
                new(0x4F, "Regen",           8, "3",    99, 2, 0, "ally/AoE",   "Magickal effect that gradually restores HP over time.",  CastSpeed: 25, AddedEffect: "Grants Regen",               Reflectable: true, Arithmetickable: true),
                new(0x50, "Protect",         6, "3",    99, 2, 0, "ally/AoE",   "Magickal effect that reduces physical damage taken.",    CastSpeed: 34, AddedEffect: "Grants Protect",             Reflectable: true, Arithmetickable: true),
                new(0x51, "Protectja",      20, "3",    99, 2, 3, "ally/AoE",   "Magickal effect that reduces physical damage taken in an area.", CastSpeed: 20, AddedEffect: "Grants Protect",    Reflectable: false, Arithmetickable: false),
                new(0x52, "Shell",           6, "3",    99, 2, 0, "ally/AoE",   "Magickal effect that reduces magickal damage taken.",    CastSpeed: 34, AddedEffect: "Grants Shell",               Reflectable: true, Arithmetickable: true),
                new(0x53, "Shellja",        20, "3",    99, 2, 3, "ally/AoE",   "Magickal effect that reduces magickal damage taken in an area.", CastSpeed: 20, AddedEffect: "Grants Shell",      Reflectable: false, Arithmetickable: false),
                new(0x54, "Wall",           24, "3",    99, 1, 0, "ally",       "Magickal effect that grants Protect and Shell.",         CastSpeed: 25, AddedEffect: "Grants Protect and Shell",  Reflectable: true, Arithmetickable: true),
                new(0x55, "Esuna",          18, "3",    99, 2, 2, "ally/AoE",   "Magickal effect that removes most negative statuses.",   CastSpeed: 34, AddedEffect: "Removes most negative statuses", Reflectable: true, Arithmetickable: true),
                new(0x56, "Holy",           56, "5",    99, 1, 0, "enemy",      "Magickal effect that deals Holy-element damage.",        CastSpeed: 17, Element: "Holy",                          Reflectable: true, Arithmetickable: true),
            },
            ["Black Magicks"] = new()
            {
                new(0x5B, "Fire",            6, "4",    99, 2, 1, "enemy/AoE",  "Magick that ignites searing flames, dealing fire damage to distant units.",        CastSpeed: 25, Element: "Fire",      Reflectable: true,  Arithmetickable: true),
                new(0x5C, "Fira",           12, "4",    99, 2, 2, "enemy/AoE",  "Magick that ignites ferocious flames, dealing fire damage to distant units.",      CastSpeed: 20, Element: "Fire",      Reflectable: true,  Arithmetickable: true),
                new(0x5D, "Firaga",         24, "4",    99, 2, 3, "enemy/AoE",  "Magick that ignites incinerating flames, dealing fire damage to distant units.",   CastSpeed: 15, Element: "Fire",      Reflectable: true,  Arithmetickable: true),
                new(0x00, "Firaja",         48, "4",    99, 3, 3, "enemy/AoE",  "Magick that ignites infernal flames, dealing fire damage to distant units.",       CastSpeed: 10, Element: "Fire",      Reflectable: false, Arithmetickable: false),
                new(0x5E, "Thunder",         6, "4",    99, 2, 1, "enemy/AoE",  "Magick that calls down scouring lightning bolts, dealing lightning damage to distant units.", CastSpeed: 25, Element: "Lightning", Reflectable: true,  Arithmetickable: true),
                new(0x5F, "Thundara",       10, "4",    99, 2, 2, "enemy/AoE",  "Magick that calls down fierce lightning bolts, dealing lightning damage to distant units.",       CastSpeed: 20, Element: "Lightning", Reflectable: true,  Arithmetickable: true),
                new(0x60, "Thundaga",       24, "4",    99, 2, 3, "enemy/AoE",  "Magick that calls down crushing lightning bolts, dealing lightning damage to distant units.",    CastSpeed: 15, Element: "Lightning", Reflectable: true,  Arithmetickable: true),
                new(0x00, "Thundaja",       48, "4",    99, 3, 3, "enemy/AoE",  "Magick that calls down cataclysmic lightning bolts, dealing lightning damage to distant units.", CastSpeed: 10, Element: "Lightning", Reflectable: false, Arithmetickable: false),
                new(0x61, "Blizzard",        6, "4",    99, 2, 1, "enemy/AoE",  "Magick that conjures bone-chilling icicles, dealing ice damage to distant units.",                CastSpeed: 25, Element: "Ice",       Reflectable: true,  Arithmetickable: true),
                new(0x62, "Blizzara",       12, "4",    99, 2, 2, "enemy/AoE",  "Magick that conjures frostbite icicles, dealing ice damage to distant units.",                    CastSpeed: 20, Element: "Ice",       Reflectable: true,  Arithmetickable: true),
                new(0x63, "Blizzaga",       24, "4",    99, 2, 3, "enemy/AoE",  "Magick that conjures glacial icicles, dealing ice damage to distant units.",                      CastSpeed: 15, Element: "Ice",       Reflectable: true,  Arithmetickable: true),
                new(0x00, "Blizzaja",       48, "4",    99, 3, 3, "enemy/AoE",  "Magick that conjures absolute-zero icicles, dealing ice damage to distant units.",                CastSpeed: 10, Element: "Ice",       Reflectable: false, Arithmetickable: false),
                new(0x64, "Poison",          6, "4",    99, 2, 0, "enemy/AoE",  "Magick that induces poisoning within the body, causing a gradual loss of HP.",                    CastSpeed: 34,                       AddedEffect: "Inflicts Poison",               Reflectable: true,  Arithmetickable: true),
                new(0x65, "Toad",           12, "3",    99, 1, 0, "enemy",      "Magick that turns its target into a toad, or reverts a toad to its natural form.",                CastSpeed: 20,                       AddedEffect: "Inflicts Toad",                 Reflectable: true,  Arithmetickable: true),
                new(0x68, "Death",          24, "4",    99, 1, 0, "enemy",      "Magick that offers up the target's soul to the spirits of the dead for an instant KO.",           CastSpeed: 10,                       AddedEffect: "Inflicts KO",                   Reflectable: true,  Arithmetickable: true),
                new(0x69, "Flare",          60, "5",    99, 1, 0, "enemy",      "Magick that converts energy into heat, scorching the battlefield with searing temperatures.",     CastSpeed: 15,                                                                     Reflectable: true,  Arithmetickable: true),
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
            // Time Magicks: Haste verified in-game by user 2026-04-11. Rest sourced from
            // FFT wiki (MP, Range, Effect/AoE, Speed, descriptions). HoE values mirror the
            // tier progression used for the Fire/Ice/Lightning families (tier 1=1, tier 2=3
            // for ja-tiers). VR set to 99 (unlimited) as the default for Time Magicks —
            // wiki notes Hasteja/Slowja have "improved vertical range" over Haste/Slow but
            // doesn't list exact values, so both stay at 99 until empirically verified.
            ["Time Magicks"] = new()
            {
                new(0x85, "Haste",           8, "3",    99, 2, 0, "ally/AoE",   "Magick that speeds the passage of time, granting Haste to distant units.",                                                                 CastSpeed: 50, AddedEffect: "Grants Haste",    Reflectable: true,  Arithmetickable: true),
                new(0x86, "Hasteja",        30, "3",    99, 2, 3, "ally/AoE",   "Magick that speeds the passage of time, granting Haste to distant units. Improved vertical range over Haste.",                             CastSpeed: 15, AddedEffect: "Grants Haste",    Reflectable: true,  Arithmetickable: true),
                new(0x87, "Slow",            8, "3",    99, 2, 0, "enemy/AoE",  "Magick that delays the passage of time, inflicting Slow on distant units.",                                                                CastSpeed: 50, AddedEffect: "Inflicts Slow",   Reflectable: true,  Arithmetickable: true),
                new(0x88, "Slowja",         30, "3",    99, 2, 3, "enemy/AoE",  "Magick that delays the passage of time, inflicting Slow on distant units. Improved vertical range over Slow.",                             CastSpeed: 15, AddedEffect: "Inflicts Slow",   Reflectable: true,  Arithmetickable: true),
                new(0x89, "Stop",           14, "3",    99, 2, 0, "enemy/AoE",  "Magick that halts the passage of time, inflicting Stop on distant units.",                                                                 CastSpeed: 15, AddedEffect: "Inflicts Stop",   Reflectable: true,  Arithmetickable: true),
                new(0x8A, "Immobilize",     10, "3",    99, 2, 0, "enemy/AoE",  "Magick that creates an anomaly in space to hinder movement, inflicting Immobilize on distant units.",                                      CastSpeed: 34, AddedEffect: "Inflicts Immobilize", Reflectable: true, Arithmetickable: true),
                new(0x8B, "Float",           8, "4",    99, 2, 0, "ally/AoE",   "Magick that warps space, granting Float to distant units.",                                                                                CastSpeed: 50, AddedEffect: "Grants Float",    Reflectable: true,  Arithmetickable: true),
                new(0x8C, "Reflect",        12, "4",    99, 1, 0, "ally",       "Magick that creates a magick-repelling field, granting Reflect to a distant unit.",                                                        CastSpeed: 50, AddedEffect: "Grants Reflect",  Reflectable: true,  Arithmetickable: true),
                new(0x8D, "Quick",          24, "4",    99, 1, 0, "ally",       "Magick that hastens the localized passage of time, allowing the next turn of a distant unit to come immediately.",                         CastSpeed: 25,                                 Reflectable: true,  Arithmetickable: true),
                new(0x8E, "Gravity",        24, "4",    99, 2, 0, "enemy/AoE",  "Magick that conjures gravitational forces, reducing the HP of distant units by a small proportion (25%) of their maximum.",               CastSpeed: 17,                                 Reflectable: true,  Arithmetickable: true),
                new(0x8F, "Graviga",        50, "4",    99, 2, 0, "enemy/AoE",  "Magick that conjures powerful gravitational forces, reducing the HP of distant units by a large proportion (50%) of their maximum.",      CastSpeed: 12,                                 Reflectable: true,  Arithmetickable: true),
                new(0x90, "Meteor",         70, "4",    99, 4, 0, "enemy/AoE",  "Magick that warps space-time, causing an enormous meteor to descend and smite distant units.",                                             CastSpeed: 10,                                 Reflectable: true,  Arithmetickable: true),
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
            // Steal skillset: Steal Gil verified in-game by user 2026-04-11. All abilities
            // are instant (Speed "Now" = CastSpeed 0). Range is 1 except Steal Heart which
            // is range 3. Success rates generally scale with the thief's Speed stat.
            // None of the steal abilities are reflectable or arithmetickable.
            ["Steal"] = new()
            {
                new(0x98, "Steal Gil",       0, "1",  1, 1, 0, "enemy",  "Pilfer gil from the target. Amount: Level x Speed. Success rate: 200 + Speed."),
                new(0x99, "Steal Heart",     0, "3",  1, 1, 0, "enemy",  "Capture the target's heart, enthralling them. Success rate: 50 + Magic Attack. Must target the opposite sex or a monster.",
                    AddedEffect: "May inflict Charm"),
                new(0x9A, "Steal Helm",      0, "1",  1, 1, 0, "enemy",  "Purloin the target's equipped helmet. Success rate: 40 + Speed."),
                new(0x9B, "Steal Armor",     0, "1",  1, 1, 0, "enemy",  "Purloin the target's equipped armor. Success rate: 35 + Speed."),
                new(0x9C, "Steal Shield",    0, "1",  1, 1, 0, "enemy",  "Purloin the target's equipped shield. Success rate: 35 + Speed."),
                new(0x9D, "Steal Weapon",    0, "1",  1, 1, 0, "enemy",  "Purloin the target's equipped weapon. Success rate: 30 + Speed."),
                new(0x9E, "Steal Accessory", 0, "1",  1, 1, 0, "enemy",  "Purloin the target's equipped accessory. Success rate: 40 + Speed."),
                new(0x9F, "Steal EXP",       0, "1",  1, 1, 0, "enemy",  "Filch experience points from the target. Amount: 5 + Speed. Success rate: 70 + Speed."),
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
            // Speechcraft: Entice verified in-game by user 2026-04-11 (Range 3, VR 3, AoE 1,
            // HoE 0). Mimic Darlavon also verified (Range 3, VR 3, AoE 2, HoE 3). Rest sourced
            // from FFT wiki. All Speechcraft abilities are instant (CastSpeed 0) and cost 0 MP.
            // Range 3 and AoE 1 are the norm (Mimic Darlavon is the exception with AoE 2).
            // Not reflectable, not arithmetickable. Requires Beast Tongue to use on monsters.
            ["Speechcraft"] = new()
            {
                new(0x00, "Entice",          0, "3",  3, 1, 0, "enemy",  "A speechcraft skill that persuades a foe to become an ally. At the end of battle, that unit will officially join your party. Success rate: 20 + Magic Attack.",
                    AddedEffect: "Applies Traitor"),
                new(0x00, "Stall",           0, "3",  3, 1, 0, "enemy",  "A speechcraft skill that persuades a foe to wait before acting. Resets the target's CT count. Success rate: 30 + Magic Attack."),
                new(0x00, "Praise",          0, "3",  3, 1, 0, "ally",   "A speechcraft skill that praises the target's feats and strengths, increasing his or her Bravery by 4. Success rate: 50 + Magic Attack."),
                new(0x00, "Intimidate",      0, "3",  3, 1, 0, "enemy",  "A speechcraft skill that plays upon the doubts and fears of the target, decreasing his or her Bravery by 20. Success rate: 90 + Magic Attack."),
                new(0x00, "Preach",          0, "3",  3, 1, 0, "ally",   "A speechcraft skill that explains the nature of divine miracles, deepening belief and increasing Faith by 4. Success rate: 50 + Magic Attack."),
                new(0x00, "Enlighten",       0, "3",  3, 1, 0, "enemy",  "A speechcraft skill that decreases Faith by 20 using cold logic and secular doctrine. Success rate: 90 + Magic Attack."),
                new(0x00, "Condemn",         0, "3",  3, 1, 0, "enemy",  "A speechcraft skill that convinces the target that his or her demise is soon at hand. Success rate: 30 + Magic Attack.",
                    AddedEffect: "Applies Doom"),
                new(0x00, "Beg",             0, "3",  3, 1, 0, "enemy",  "A speechcraft skill that spins tales in return for gil. Amount: Level x Speed. Success rate: 90 + Magic Attack."),
                new(0x00, "Insult",          0, "3",  3, 1, 0, "enemy",  "A speechcraft skill that infuriates with foul invective. Success rate: 40 + Magic Attack.",
                    AddedEffect: "Applies Berserk"),
                new(0x00, "Mimic Darlavon",  0, "3",  3, 2, 3, "enemy/AoE", "A speechcraft skill that uses dull tales to induce slumber. Success rate: 40 + Magic Attack.",
                    AddedEffect: "Applies Sleep"),
            },
            ["Mystic Arts"] = new()
            {
                new(0x00, "Umbra",           4, "4", 99, 2, 1, "enemy/AoE", "Manipulates the faculties to temporarily rob sight, inflicting Blindness on distant units.",
                    CastSpeed: 50, AddedEffect: "Applies Blindness", Reflectable: true, Arithmetickable: true),
                new(0x00, "Empowerment",     2, "4", 99, 1, 0, "enemy", "Manipulates the balance of creation to absorb MP from a distant unit.",
                    CastSpeed: 50, AddedEffect: "Absorb 33% of Target's Max MP"),
                new(0x00, "Invigoration",   16, "4", 99, 1, 0, "enemy", "Manipulates the balance of creation to absorb HP from a distant unit.",
                    CastSpeed: 50, AddedEffect: "Absorb 25% of Target's Max HP"),
                new(0x00, "Belief",          6, "4", 99, 1, 0, "ally",  "Temporarily fills the soul with a zealous fervor, granting Faith Status to a distant unit.",
                    CastSpeed: 25, AddedEffect: "Applies Faith", Reflectable: true, Arithmetickable: true),
                new(0x00, "Disbelief",       6, "4", 99, 1, 0, "enemy", "Temporarily drains the soul of piety, inflicting Atheist on a distant unit.",
                    CastSpeed: 25, AddedEffect: "Applies Atheist", Reflectable: true, Arithmetickable: true),
                new(0x00, "Corruption",     20, "4", 99, 1, 0, "enemy", "Manipulates the laws of creation to transform the flesh, inflicting Undead status on a distant unit.",
                    CastSpeed: 20, AddedEffect: "Applies Undead", Reflectable: true, Arithmetickable: true),
                new(0x00, "Quiescence",     16, "4", 99, 2, 1, "enemy/AoE", "Manipulates the faculties to temporarily rob speech, inflicting Silence on distant units.",
                    CastSpeed: 34, AddedEffect: "Applies Silence", Reflectable: true, Arithmetickable: true),
                new(0x00, "Fervor",         16, "4", 99, 1, 0, "enemy", "Imparts a thirst for destruction, inflicting Berserk on a distant unit.",
                    CastSpeed: 20, AddedEffect: "Applies Berserk", Reflectable: true, Arithmetickable: true),
                new(0x00, "Trepidation",    20, "4", 99, 1, 0, "enemy", "Instills cowardice, reducing a distant unit's bravery for the duration of battle.",
                    CastSpeed: 25, AddedEffect: "-30 Bravery", Reflectable: true, Arithmetickable: true),
                new(0x00, "Delirium",       20, "4", 99, 1, 0, "enemy", "Divests the mind of rational thought, inflicting Confusion on a distant unit.",
                    CastSpeed: 20, AddedEffect: "Applies Confusion", Reflectable: true, Arithmetickable: true),
                new(0x00, "Harmony",        34, "4", 99, 1, 0, "enemy", "Restores the balance of creation, removing beneficial status effects from a distant unit.",
                    CastSpeed: 34, AddedEffect: "Removes Float/Reraise/Invisibility/Regen/Protect/Shell/Haste/Faith/Reflect", Arithmetickable: true),
                new(0x00, "Hesitation",     10, "4", 99, 2, 1, "enemy/AoE", "Manipulates the faculties to temporarily restrict actions, inflicting Disable on distant units.",
                    CastSpeed: 20, AddedEffect: "Applies Disable", Reflectable: true, Arithmetickable: true),
                new(0x00, "Repose",         24, "4", 99, 2, 1, "enemy/AoE", "Manipulates body and mind to cause a deep slumber, inflicting Sleep on distant units.",
                    CastSpeed: 17, AddedEffect: "Applies Sleep", Reflectable: true, Arithmetickable: true),
                new(0x00, "Induration",     16, "4", 99, 1, 0, "enemy", "Turn the target's body into stone.",
                    CastSpeed: 10, AddedEffect: "Applies Stone", Reflectable: true, Arithmetickable: true),
            },
            // Summons: Moogle verified in-game by user 2026-04-11. Rest sourced from FFT
            // wiki (MP, Range, AoE, Speed, descriptions, elements). Per the wiki: summons
            // ignore Magick Evasion and auto-distinguish friend from foe — offensive ones
            // only hit enemies, defensive ones only help allies. Wiki doesn't list HoE;
            // using the values already in the file (tier-scaled) for now.
            // Reflectable and Arithmetickable both false for all summons — summons don't
            // trigger Reflect and aren't castable via Arithmeticks.
            ["Summon"] = new()
            {
                new(0x00, "Moogle",         8, "4",    99, 3, 2, "ally/AoE",   "Summons Moogle to cast Moogle Charm, which soothes injuries with a purifying breeze, restoring a small amount of HP to distant allies.", CastSpeed: 34),
                new(0x00, "Shiva",         24, "4",    99, 3, 2, "enemy/AoE",  "Summons Shiva to unleash Diamond Dust, which punishes with a frigid breath, dealing ice damage to distant foes.",                        CastSpeed: 25, Element: "Ice"),
                new(0x00, "Ramuh",         24, "4",    99, 3, 2, "enemy/AoE",  "Summons Ramuh to unleash Judgment Bolt, which rains down bolts of lightning, dealing lightning damage to distant foes.",                 CastSpeed: 25, Element: "Lightning"),
                new(0x00, "Ifrit",         24, "4",    99, 3, 2, "enemy/AoE",  "Summons Ifrit to unleash Hellfire, which scorches the battlefield, dealing fire damage to distant foes.",                                CastSpeed: 25, Element: "Fire"),
                new(0x00, "Titan",         30, "4",    99, 3, 2, "enemy/AoE",  "Summons Titan to unleash Gaia's Wrath, which levels the land with a mighty blow, dealing earth damage to distant foes.",                 CastSpeed: 20, Element: "Earth"),
                new(0x00, "Golem",         40, "Auto", 99, 1, 0, "ally/AoE",   "Summons Golem to invoke Earthen Wall, which makes all allies immune to physical damage up to an amount equal to the summoner's maximum HP. Certain attacks ignore Earthen Wall.", CastSpeed: 34),
                new(0x00, "Carbuncle",     30, "4",    99, 3, 2, "ally/AoE",   "Summons Carbuncle to invoke Ruby Light, which grants Reflect to distant allies.",                                                        CastSpeed: 25, AddedEffect: "Grants Reflect"),
                new(0x00, "Bahamut",       60, "4",    99, 4, 3, "enemy/AoE",  "Summons Bahamut to unleash Megaflare, a fearsome breath attack that devastates distant foes.",                                           CastSpeed: 10),
                new(0x00, "Odin",          50, "4",    99, 4, 3, "enemy/AoE",  "Summons Odin to perform Zantetsuken, a sword attack that cleaves through distant foes.",                                                 CastSpeed: 12),
                new(0x00, "Leviathan",     48, "4",    99, 4, 3, "enemy/AoE",  "Summons Leviathan to unleash Tsunami, which engulfs the battlefield in a massive tidal wave, dealing water damage to distant foes.",     CastSpeed: 12, Element: "Water"),
                new(0x00, "Salamander",    48, "4",    99, 3, 3, "enemy/AoE",  "Summons Salamander to unleash Wyrmfire, which scorches the battlefield with savage flames, dealing fire damage to distant foes.",        CastSpeed: 12, Element: "Fire"),
                new(0x00, "Sylph",         26, "4",    99, 3, 2, "enemy/AoE",  "Summons Sylph to invoke Whispering Wind, which employs the lingering life force of windswept leaves, inflicting Silence on distant foes.", CastSpeed: 20, AddedEffect: "Inflicts Silence"),
                new(0x00, "Faerie",        28, "4",    99, 3, 2, "ally/AoE",   "Summons Faerie to invoke Fey Light, which bathes the battlefield in warming rays, restoring a moderate amount of HP to distant allies.", CastSpeed: 25),
                new(0x00, "Lich",          40, "4",    99, 3, 3, "enemy/AoE",  "Summons Lich to unleash Descending Darkness, an attack born of shadow, dealing dark damage that reduces the HP of distant foes by a large proportion of their maximum.", CastSpeed: 12, Element: "Dark"),
                new(0x00, "Cyclops",       62, "4",    99, 3, 3, "enemy/AoE",  "Summons Cyclops to unleash Climactic Fear, which smites distant foes with forceful blows that rend the battlefield asunder.",            CastSpeed: 12),
                new(0x00, "Zodiark",       99, "4",    99, 4, 3, "enemy/AoE",  "Summons Zodiark to unleash Final Eclipse, which smites distant foes with radiant beams of focused starlight. Must be learned from a crystal dropped by an enemy summoner who cast it on an ally.", CastSpeed: 10),
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
