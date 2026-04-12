using System;
using System.Collections.Generic;
using System.Linq;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Splash shape of an ability. Determines how the calculator turns
    /// (HR, VR, AoE, HoE) into a concrete set of affected tiles.
    /// </summary>
    public enum AbilityShape
    {
        /// <summary>
        /// Default. The calculator infers shape from (HRange, AoE):
        /// AoE=1 + numeric HR → point-target; AoE&gt;1 + numeric HR → radius.
        /// Covers 95%+ of abilities.
        /// </summary>
        Auto,
        /// <summary>
        /// Cardinal line from caster. HR is the total line length; AoE is
        /// ignored (always 1-wide). Caster picks direction by clicking a seed
        /// tile (one of the 4 cardinal neighbors). Shockwave, Divine Ruination.
        /// </summary>
        Line,
        /// <summary>
        /// Diamond splash centered on the caster instead of a clicked tile.
        /// HRange="Self". Cyclone, Chakra, Purification.
        /// </summary>
        SelfRadius,
        /// <summary>
        /// Hits every unit of the target type on the map, regardless of range.
        /// All Bardsong, all Dance. Not yet implemented.
        /// </summary>
        FullField,
        /// <summary>
        /// 3-row falling-damage cone. Only Abyssal Blade. Deferred.
        /// </summary>
        Cone,
    }

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
        bool Arithmetickable = false, // Can be cast via Arithmeticks
        AbilityShape Shape = AbilityShape.Auto  // Splash shape; default infers from (HRange, AoE)
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
        /// Decode the 2-byte learned-action-ability bitfield from a roster slot job block.
        /// Bits are stored MSB-first: bit 7 of byte0 = ability index 0, bit 0 of byte0 = ability
        /// index 7, bit 7 of byte1 = ability index 8, etc. Returns the set of ability indices
        /// (0-15) that are marked learned.
        ///
        /// Confirmed empirically 2026-04-11 by purchasing Stop (Time Magicks idx 4) and
        /// Thundaga (Black Magicks idx 6) and watching the exact MSB positions flip.
        /// </summary>
        public static HashSet<int> DecodeLearnedBitfield(byte byte0, byte byte1)
        {
            var result = new HashSet<int>();
            for (int i = 0; i < 8; i++)
            {
                // MSB-first: ability index i corresponds to bit (7 - i) of the byte.
                if ((byte0 & (0x80 >> i)) != 0) result.Add(i);
                if ((byte1 & (0x80 >> i)) != 0) result.Add(i + 8);
            }
            return result;
        }

        /// <summary>
        /// Given a skillset name and the two learned-ability bitfield bytes from the
        /// roster slot, return the subset of that skillset's abilities the character
        /// has actually learned (preserving the skillset's ability order).
        ///
        /// Returns an empty list for unknown skillset names.
        /// </summary>
        public static List<ActionAbilityInfo> GetLearnedAbilitiesFromBitfield(
            string skillsetName, byte byte0, byte byte1)
        {
            if (!Skillsets.TryGetValue(skillsetName, out var skillset))
                return new List<ActionAbilityInfo>();

            var learnedIndices = DecodeLearnedBitfield(byte0, byte1);
            var result = new List<ActionAbilityInfo>();
            for (int i = 0; i < skillset.Count; i++)
            {
                if (learnedIndices.Contains(i))
                    result.Add(skillset[i]);
            }

            if (skillsetName == "Jump")
                result = CollapseJumpAbilities(result);

            return result;
        }

        /// <summary>
        /// Collapse individual Horizontal Jump +N / Vertical Jump +N entries into a
        /// single "Jump" entry whose HR = highest learned Horizontal range and
        /// VR = highest learned Vertical range. In-game, Jump is one menu entry
        /// whose effective range is determined by the highest learned levels.
        /// Non-Jump abilities in the list are passed through unchanged.
        /// </summary>
        public static List<ActionAbilityInfo> CollapseJumpAbilities(List<ActionAbilityInfo> learnedJumpAbilities)
        {
            int maxHR = 0;
            int maxVR = 0;
            var others = new List<ActionAbilityInfo>();

            foreach (var a in learnedJumpAbilities)
            {
                if (a.Name.StartsWith("Horizontal Jump") && int.TryParse(a.HRange, out int hr))
                    maxHR = Math.Max(maxHR, hr);
                else if (a.Name.StartsWith("Vertical Jump"))
                    maxVR = Math.Max(maxVR, a.VRange);
                else
                    others.Add(a);
            }

            var result = new List<ActionAbilityInfo>();

            if (maxHR > 0 || maxVR > 0)
            {
                result.Add(new(0, "Jump", 0, maxHR.ToString(), maxVR, 1, 0, "enemy",
                    $"Jump to target tile and attack on landing. Range {maxHR}, vertical reach {maxVR}.",
                    CastSpeed: 0));
            }

            result.AddRange(others);
            return result;
        }

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
                new(0x1C6, "Focus",          0, "Self", 0, 1, 0, "self",  "Increases the user's physical attack power.",
                    CastSpeed: 0),
                new(0x1C7, "Rush",           0, "1",    1, 1, 0, "enemy", "Attack by ramming into the enemy's body.",
                    CastSpeed: 0),
                new(0x1C8, "Throw Stone",    0, "4",   99, 1, 0, "enemy", "Lob a stone at a distant foe.",
                    CastSpeed: 0),
                new(0x1C9, "Salve",          0, "1",    2, 1, 0, "ally",  "Removes several status ailments.",
                    CastSpeed: 0, AddedEffect: "Removes: Blindness, Silence, Poison"),
            },
            ["Items"] = new()
            {
                new(0x19, "Potion",          0, "4", 0, 1, 0, "ally", "Use a potion to restore HP or inflict damage on the undead.",
                    CastSpeed: 0, AddedEffect: "Restores 30 HP"),
                new(0x1A, "Hi-Potion",       0, "4", 0, 1, 0, "ally", "Use a Hi-Potion to restore HP. A more potent draught than a Potion.",
                    CastSpeed: 0, AddedEffect: "Restores 70 HP"),
                new(0x1B, "X-Potion",        0, "4", 0, 1, 0, "ally", "Use an X-Potion to restore HP. A more potent draught than a Hi-Potion.",
                    CastSpeed: 0, AddedEffect: "Restores 150 HP"),
                new(0x1C, "Ether",           0, "4", 0, 1, 0, "ally", "Use an ether to restore MP.",
                    CastSpeed: 0, AddedEffect: "Restores 20 MP"),
                new(0x1D, "Hi-Ether",        0, "4", 0, 1, 0, "ally", "Use a Hi-Ether to restore MP. A more potent draught than an Ether.",
                    CastSpeed: 0, AddedEffect: "Restores 50 MP"),
                new(0x1E, "Elixir",          0, "4", 0, 1, 0, "ally", "Use an elixir to fully restore HP and MP.",
                    CastSpeed: 0, AddedEffect: "Fully restores HP and MP"),
                new(0x20, "Antidote",        0, "4", 0, 1, 0, "ally", "Use an antidote to nullify poison.",
                    CastSpeed: 0, AddedEffect: "Removes Poison"),
                new(0x21, "Eye Drop",        0, "4", 0, 1, 0, "ally", "Use eye drops when vision has been magickally compromised.",
                    CastSpeed: 0, AddedEffect: "Removes Blind"),
                new(0x22, "Echo Herbs",      0, "4", 0, 1, 0, "ally", "Use echo herbs to restore a unit's power of speech, permitting them to cast magicks once again.",
                    CastSpeed: 0, AddedEffect: "Removes Silence"),
                new(0x23, "Maiden's Kiss",   0, "4", 0, 1, 0, "ally", "Use a maiden's kiss to change a unit that has been transformed into a toad back to its original form.",
                    CastSpeed: 0, AddedEffect: "Removes Toad"),
                new(0x24, "Gold Needle",     0, "4", 0, 1, 0, "ally", "Use a gold needle to change a petrified unit back to normal. Breaks after one use.",
                    CastSpeed: 0, AddedEffect: "Removes Stone"),
                new(0x25, "Holy Water",      0, "4", 0, 1, 0, "ally", "Use holy water to lift the curse of undeath from a unit.",
                    CastSpeed: 0, AddedEffect: "Removes Undead, Vampire"),
                new(0x26, "Remedy",          0, "4", 0, 1, 0, "ally", "Use a remedy to cure various status effects.",
                    CastSpeed: 0, AddedEffect: "Removes Poison, Blindness, Silence, Toad, Stone, Confusion, Oil, Sleep"),
                new(0x1F, "Phoenix Down",    0, "4", 0, 1, 0, "ally", "Use phoenix down to restore life to a fallen unit. Vanishes after one use.",
                    CastSpeed: 0, AddedEffect: "Removes KO"),
            },
            ["Arts of War"] = new()
            {
                new(0x30, "Rend Helm",       0, "1",      0, 1, 0, "enemy", "Destroys the item equipped on the target's head.",
                    CastSpeed: 0, AddedEffect: "Destroys Headgear"),
                new(0x31, "Rend Armor",      0, "1",      0, 1, 0, "enemy", "Destroys the item equipped on the target's body.",
                    CastSpeed: 0, AddedEffect: "Destroys Armor"),
                new(0x32, "Rend Shield",     0, "1",      0, 1, 0, "enemy", "Destroys the target's equipped shield.",
                    CastSpeed: 0, AddedEffect: "Destroys Shield"),
                new(0x33, "Rend Weapon",     0, "1",      0, 1, 0, "enemy", "Destroys the target's equipped weapon.",
                    CastSpeed: 0, AddedEffect: "Destroys Weapon"),
                new(0x37, "Rend MP",         0, "1",      0, 1, 0, "enemy", "Reduces the target's MP.",
                    CastSpeed: 0, AddedEffect: "-50% of Target's Max MP"),
                new(0x34, "Rend Speed",      0, "1",      0, 1, 0, "enemy", "Reduces the target's Speed.",
                    CastSpeed: 0, AddedEffect: "-2 Speed"),
                new(0x35, "Rend Power",      0, "1",      0, 1, 0, "enemy", "Reduces the target's physical attack power.",
                    CastSpeed: 0, AddedEffect: "-3 Physical Attack"),
                new(0x36, "Rend Magick",     0, "1",      0, 1, 0, "enemy", "Reduces the target's magickal attack power.",
                    CastSpeed: 0, AddedEffect: "-3 Magick Attack"),
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
                new(0x00, "Cyclone",         0, "1",  0, 2, 0, "enemy/AoE", "Turn in a circle, attacking with backhand blows.",
                    CastSpeed: 0),
                new(0x78, "Pummel",          0, "1",  0, 1, 0, "enemy", "Strike many times in quick succession.",
                    CastSpeed: 0),
                new(0x79, "Aurablast",       0, "3",  99, 1, 0, "enemy", "Employ one's martial spirit to strike a distant foe.",
                    CastSpeed: 0),
                new(0x7A, "Shockwave",       0, "8",  0, 1, 2, "enemy", "Release spiritual energy mighty enough to rend the earth.",
                    CastSpeed: 0, Element: "Earth", Shape: AbilityShape.Line),
                new(0x7B, "Doom Fist",       0, "1",  0, 1, 0, "enemy", "Invite slow, certain death with blows to pressure points.",
                    CastSpeed: 0, AddedEffect: "Applies Doom"),
                new(0x7C, "Purification",    0, "Self", 0, 2, 0, "ally/AoE", "Release positive energy to remove status ailments.",
                    CastSpeed: 0, AddedEffect: "Removes Stone, Blindness, Confusion, Silence, Berserk, Toad, Poison, Sleep, Immobilize, Disable"),
                new(0x7D, "Chakra",          0, "Self", 0, 2, 0, "ally/AoE", "Draw out the energy within the body's chakra points to restore HP and MP.",
                    CastSpeed: 0),
                new(0x7E, "Revive",          0, "1",  0, 1, 0, "ally", "Calls back dead units with a loud cry.",
                    CastSpeed: 0, AddedEffect: "Removes KO"),
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
                new(0x00, "Aim +1",          0, "5",  0, 1, 0, "enemy", "Spends ever so slightly more time aiming to deal more damage.",
                    AddedEffect: "Potency: +1"),
                new(0x00, "Aim +2",          0, "5",  0, 1, 0, "enemy", "Spends slightly more time aiming to deal more damage.",
                    AddedEffect: "Potency: +2"),
                new(0x00, "Aim +3",          0, "5",  0, 1, 0, "enemy", "Spends somewhat more time aiming to deal more damage.",
                    AddedEffect: "Potency: +3"),
                new(0x00, "Aim +4",          0, "5",  0, 1, 0, "enemy", "Spends moderately more time aiming to deal more damage.",
                    AddedEffect: "Potency: +4"),
                new(0x00, "Aim +5",          0, "5",  0, 1, 0, "enemy", "Spends significantly more time aiming to deal more damage.",
                    AddedEffect: "Potency: +5"),
                new(0x00, "Aim +7",          0, "5",  0, 1, 0, "enemy", "Spends considerably more time aiming to deal more damage.",
                    AddedEffect: "Potency: +7"),
                new(0x00, "Aim +10",         0, "5",  0, 1, 0, "enemy", "Spends ridiculously more time aiming to deal more damage.",
                    AddedEffect: "Potency: +10"),
                new(0x00, "Aim +20",         0, "5",  0, 1, 0, "enemy", "Spends next to an eternity aiming to deal more damage.",
                    AddedEffect: "Potency: +20"),
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
                new(0x00, "Throw Shuriken",        0, "5", 0, 1, 0, "enemy", "Attack by throwing shuriken from inventory.",
                    CastSpeed: 0),
                new(0x00, "Throw Bombs",           0, "5", 0, 1, 0, "enemy", "Attack by throwing bombs from inventory.",
                    CastSpeed: 0),
                new(0x00, "Throw Daggers",         0, "5", 0, 1, 0, "enemy", "Attack by throwing knives from inventory.",
                    CastSpeed: 0),
                new(0x00, "Throw Swords",          0, "5", 0, 1, 0, "enemy", "Attack by throwing swords from inventory.",
                    CastSpeed: 0),
                new(0x00, "Throw Flails",          0, "5", 0, 1, 0, "enemy", "Attack by throwing flails from inventory.",
                    CastSpeed: 0),
                new(0x00, "Throw Katana",          0, "5", 0, 1, 0, "enemy", "Attack by throwing katana from inventory.",
                    CastSpeed: 0),
                new(0x00, "Throw Ninja Blades",    0, "5", 0, 1, 0, "enemy", "Attack by throwing ninja blades from inventory.",
                    CastSpeed: 0),
                new(0x00, "Throw Axes",            0, "5", 0, 1, 0, "enemy", "Attack by throwing axes from inventory.",
                    CastSpeed: 0),
                new(0x00, "Throw Polearms",        0, "5", 0, 1, 0, "enemy", "Attack by throwing polearms from inventory.",
                    CastSpeed: 0),
                new(0x00, "Throw Poles",           0, "5", 0, 1, 0, "enemy", "Attack by throwing poles from inventory.",
                    CastSpeed: 0),
                new(0x00, "Throw Knight's Swords", 0, "5", 0, 1, 0, "enemy", "Attack by throwing knight's swords from inventory.",
                    CastSpeed: 0),
                new(0x00, "Throw Books",           0, "5", 0, 1, 0, "enemy", "Attack by throwing books from inventory.",
                    CastSpeed: 0),
            },
            ["Jump"] = new()
            {
                new(0x00, "Horizontal Jump +1",  0, "2",  0, 1, 0, "enemy", "Grants Jump a horizontal range of 2 tiles. Horizontal Jump abilities do not stack; only the one with the highest value is applied.",
                    CastSpeed: 0),
                new(0x00, "Horizontal Jump +2",  0, "3",  0, 1, 0, "enemy", "Grants Jump a horizontal range of 3 tiles. Horizontal Jump abilities do not stack; only the one with the highest value is applied.",
                    CastSpeed: 0),
                new(0x00, "Horizontal Jump +3",  0, "4",  0, 1, 0, "enemy", "Grants Jump a horizontal range of 4 tiles. Horizontal Jump abilities do not stack; only the one with the highest value is applied.",
                    CastSpeed: 0),
                new(0x00, "Horizontal Jump +4",  0, "5",  0, 1, 0, "enemy", "Grants Jump a horizontal range of 5 tiles. Horizontal Jump abilities do not stack; only the one with the highest value is applied.",
                    CastSpeed: 0),
                new(0x00, "Horizontal Jump +7",  0, "8",  0, 1, 0, "enemy", "Grants Jump a horizontal range of 8 tiles. Horizontal Jump abilities do not stack; only the one with the highest value is applied.",
                    CastSpeed: 0),
                new(0x00, "Vertical Jump +2",    0, "0",  2, 1, 0, "enemy", "Grants Jump a vertical range of 2. Horizontal range equals the unit's Jump attribute. Vertical Jump abilities do not stack; only the one with the highest value is applied.",
                    CastSpeed: 0),
                new(0x00, "Vertical Jump +3",    0, "0",  3, 1, 0, "enemy", "Grants Jump a vertical range of 3. Horizontal range equals the unit's Jump attribute. Vertical Jump abilities do not stack; only the one with the highest value is applied.",
                    CastSpeed: 0),
                new(0x00, "Vertical Jump +4",    0, "0",  4, 1, 0, "enemy", "Grants Jump a vertical range of 4. Horizontal range equals the unit's Jump attribute. Vertical Jump abilities do not stack; only the one with the highest value is applied.",
                    CastSpeed: 0),
                new(0x00, "Vertical Jump +5",    0, "0",  5, 1, 0, "enemy", "Grants Jump a vertical range of 5. Horizontal range equals the unit's Jump attribute. Vertical Jump abilities do not stack; only the one with the highest value is applied.",
                    CastSpeed: 0),
                new(0x00, "Vertical Jump +6",    0, "0",  6, 1, 0, "enemy", "Grants Jump a vertical range of 6. Horizontal range equals the unit's Jump attribute. Vertical Jump abilities do not stack; only the one with the highest value is applied.",
                    CastSpeed: 0),
                new(0x00, "Vertical Jump +7",    0, "0",  7, 1, 0, "enemy", "Grants Jump a vertical range of 7. Horizontal range equals the unit's Jump attribute. Vertical Jump abilities do not stack; only the one with the highest value is applied.",
                    CastSpeed: 0),
                new(0x00, "Vertical Jump +8",    0, "0",  8, 1, 0, "enemy", "Grants Jump a vertical range of 8. Horizontal range equals the unit's Jump attribute. Vertical Jump abilities do not stack; only the one with the highest value is applied.",
                    CastSpeed: 0),
            },
            ["Iaido"] = new()
            {
                new(0x00, "Ashura",          0, "1", 0, 3, 0, "enemy/AoE", "A technique that releases the spirit in the user's katana, causing an unseen specter-sword to slash at the target.",
                    CastSpeed: 0),
                new(0x00, "Kotetsu",         0, "1", 0, 3, 0, "enemy/AoE", "A technique that releases the spirit in the user's katana, loosing a wave of howling dark spirits.",
                    CastSpeed: 0),
                new(0x00, "Bizen Osafune",   0, "1", 0, 3, 0, "enemy/AoE", "A technique that releases the spirit in the user's katana, sending the whispering dead to feed on the target's MP.",
                    CastSpeed: 0, AddedEffect: "Lowers MP"),
                new(0x00, "Murasame",        0, "1", 0, 3, 0, "ally/AoE", "A technique that releases the spirit in the user's katana, raining tears of an enlightened soul to restore HP.",
                    CastSpeed: 0),
                new(0x00, "Ame-no-Murakumo", 0, "1", 0, 3, 0, "enemy/AoE", "A technique that releases the spirit in the user's katana, releasing a phantom of pure mist to attack the target.",
                    CastSpeed: 0, AddedEffect: "Applies Slow"),
                new(0x00, "Kiyomori",        0, "1", 0, 3, 0, "ally/AoE", "A technique that releases the spirit in the user's katana, bestowing the protection of its effervescent life force.",
                    CastSpeed: 0, AddedEffect: "Applies Protect, Shell"),
                new(0x00, "Muramasa",        0, "1", 0, 3, 0, "enemy/AoE", "A technique that releases the spirit in the user's katana. Only a living soul will quell its tumult.",
                    CastSpeed: 0, AddedEffect: "Applies Confusion, Doom"),
                new(0x00, "Kiku-ichimonji",  0, "4", 0, 8, 0, "enemy/AoE", "A technique that releases the spirit in the user's katana, wreaking havoc with its all-consuming hatred.",
                    CastSpeed: 0),
                new(0x00, "Masamune",        0, "1", 0, 3, 0, "ally/AoE", "A technique that releases the spirit in the user's katana, bestowing physical healing and increased speed.",
                    CastSpeed: 0, AddedEffect: "Applies Regen, Haste"),
                new(0x00, "Chirijiraden",    0, "1", 0, 3, 0, "enemy/AoE", "A technique that releases the spirit in the user's katana, which pursues the living as a band of blue flame.",
                    CastSpeed: 0),
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
                new(0x00, "Sinkhole",        0, "5", 99, 2, 0, "enemy/AoE", "Manifests the power harbored by the soil or wasteland at one's feet. Deals damage to distant units and has a chance of inflicting Immobilize.",
                    CastSpeed: 0, AddedEffect: "Applies Immobilize (25%)"),
                new(0x00, "Torrent",         0, "5", 99, 2, 0, "enemy/AoE", "Manifests the power harbored by the canal, river, lake, ocean, or waterfall at one's feet. Deals water damage to distant units and has a chance of inflicting Toad.",
                    CastSpeed: 0, Element: "Water", AddedEffect: "Applies Toad (25%)"),
                new(0x00, "Tanglevine",      0, "5", 99, 2, 0, "enemy/AoE", "Manifests the power harbored by the grassland, underbrush, or tanglevine at one's feet. Deals damage to distant units and has a chance of inflicting Stop.",
                    CastSpeed: 0, AddedEffect: "Applies Stop (25%)"),
                new(0x00, "Contortion",      0, "5", 99, 2, 0, "enemy/AoE", "Manifests the power harbored by the gravel, flagstone, stone wall, earthen wall, or gravestone at one's feet. Deals damage to distant units and has a chance of inflicting Stone.",
                    CastSpeed: 0, AddedEffect: "Applies Stone (25%)"),
                new(0x00, "Tremor",          0, "5", 99, 2, 0, "enemy/AoE", "Manifests the power harbored by the stone outcropping or basalt at one's feet. Deals earth damage to distant units and has a chance of inflicting Confusion.",
                    CastSpeed: 0, Element: "Earth", AddedEffect: "Applies Confusion (25%)"),
                new(0x00, "Wind Slash",      0, "5", 99, 2, 0, "enemy/AoE", "Manifests the power harbored by the book, tree, brick, bridge, furnishings, iron plate, moss, or coffin at one's feet. Deals wind damage to distant units and has a chance of inflicting Disable.",
                    CastSpeed: 0, Element: "Wind", AddedEffect: "Applies Disable (25%)"),
                new(0x00, "Will-o'-the-Wisp",0, "5", 99, 2, 0, "enemy/AoE", "Manifests the power harbored by the wooden floor, carpet, coffer, stairs, or wooden deck at one's feet. Deals fire damage to distant units and has a chance of inflicting Sleep.",
                    CastSpeed: 0, Element: "Fire", AddedEffect: "Applies Sleep (25%)"),
                new(0x00, "Quicksand",       0, "5", 99, 2, 0, "enemy/AoE", "Manifests the power harbored by the marsh, swamp, or poisonous fen at one's feet. Deals water damage to distant units and has a chance of inflicting Doom.",
                    CastSpeed: 0, Element: "Water", AddedEffect: "Applies Doom (25%)"),
                new(0x00, "Sandstorm",       0, "5", 99, 2, 0, "enemy/AoE", "Manifests the power harbored by the sand, stalactites, or salt flat at one's feet. Deals wind damage to distant units and has a chance of inflicting Blindness.",
                    CastSpeed: 0, Element: "Wind", AddedEffect: "Applies Blindness (25%)"),
                new(0x00, "Snowstorm",       0, "5", 99, 2, 0, "enemy/AoE", "Manifests the power harbored by the snow at one's feet. Deals ice damage to distant units and has a chance of inflicting Silence.",
                    CastSpeed: 0, Element: "Ice", AddedEffect: "Applies Silence (25%)"),
                new(0x00, "Wind Blast",      0, "5", 99, 2, 0, "enemy/AoE", "Manifests the power harbored by the roof or chimney at one's feet. Deals wind damage to distant units and has a chance of inflicting Slow.",
                    CastSpeed: 0, Element: "Wind", AddedEffect: "Applies Slow (25%)"),
                new(0x00, "Magma Surge",     0, "5", 99, 2, 0, "enemy/AoE", "Manifests the power harbored by the lava or machinery at one's feet. Deals fire damage to distant units and has a chance of causing instant KO.",
                    CastSpeed: 0, Element: "Fire", AddedEffect: "Instant KO (25%)"),
            },
            ["Bardsong"] = new()
            {
                new(0x00, "Seraph Song",     0, "Self", 0, 99, 0, "ally/AoE", "A song pleading for angelic protection. Restores MP.",
                    CastSpeed: 17, AddedEffect: "Restores 20 + Magic Attack MP"),
                new(0x00, "Life's Anthem",   0, "Self", 0, 99, 0, "ally/AoE", "A song praising the vibrance of life. Restores HP.",
                    CastSpeed: 17, AddedEffect: "Restores 10 + Magic Attack HP"),
                new(0x00, "Rousing Melody",  0, "Self", 0, 99, 0, "ally/AoE", "A song of encouragement and inspiration. Increases Speed.",
                    CastSpeed: 13, AddedEffect: "+1 Speed (50% per target)"),
                new(0x00, "Battle Chant",    0, "Self", 0, 99, 0, "ally/AoE", "A gallant song of battles fought and won. Increases physical attack power.",
                    CastSpeed: 13, AddedEffect: "+1 Physical Attack (50% per target)"),
                new(0x00, "Magickal Refrain",0, "Self", 0, 99, 0, "ally/AoE", "A song of the laws of sorcery and of the source of magick. Increases magickal attack power.",
                    CastSpeed: 10, AddedEffect: "+1 Magick Attack (50% per target)"),
                new(0x00, "Nameless Song",   0, "Self", 0, 99, 0, "ally/AoE", "A thoroughly mysterious song passed down through many an age. Bestows various types of protection.",
                    CastSpeed: 10, AddedEffect: "Applies Reraise, Regen, Protect, Shell, Haste (50% per target)"),
                new(0x00, "Finale",          0, "Self", 0, 99, 0, "ally/AoE", "The ultimate song. Increases allies' CT to 100.",
                    CastSpeed: 5, AddedEffect: "Sets CT to 100 (50% per target)"),
            },
            // Dance: Dancer (female-only) job command. Counterpart to Bardsong — affects
            // all enemies on the field instead of allies. Sourced from FFT wiki.
            ["Dance"] = new()
            {
                new(0x00, "Witch Hunt",      0, "Self", 0, 99, 0, "enemy/AoE", "A dance whose esoteric moves reduce enemies' MP.",
                    CastSpeed: 17, AddedEffect: "Reduces MP: PA + (PA * Br / 100)"),
                new(0x00, "Mincing Minuet",  0, "Self", 0, 99, 0, "enemy/AoE", "A dance whose fervent steps damage enemies' HP.",
                    CastSpeed: 17, AddedEffect: "Reduces HP: PA + (PA * Br / 100)"),
                new(0x00, "Slow Dance",      0, "Self", 0, 99, 0, "enemy/AoE", "A dance whose sedate pace reduces enemies' Speed.",
                    CastSpeed: 13, AddedEffect: "-1 Speed (50% per target)"),
                new(0x00, "Polka",           0, "Self", 0, 99, 0, "enemy/AoE", "A quick, vivacious dance that reduces enemies' physical attack power.",
                    CastSpeed: 13, AddedEffect: "-1 Physical Attack (50% per target)"),
                new(0x00, "Heathen Frolick", 0, "Self", 0, 99, 0, "enemy/AoE", "An exotic dance that clouds the mind, reducing enemies' magickal attack power.",
                    CastSpeed: 10, AddedEffect: "-1 Magick Attack (50% per target)"),
                new(0x00, "Forbidden Dance", 0, "Self", 0, 99, 0, "enemy/AoE", "A mesmerizing dance that inflicts status effects.",
                    CastSpeed: 10, AddedEffect: "Applies Blind, Confusion, Silence, Toad, Poison, Slow, Stop, Sleep (50% per target)"),
                new(0x00, "Last Waltz",      0, "Self", 0, 99, 0, "enemy/AoE", "The ultimate dance. Drops the CT of all enemies to zero.",
                    CastSpeed: 5, AddedEffect: "Sets CT to 0 (34% per target)"),
            },
            ["Arithmeticks"] = new()
            {
                new(0x00, "Target CT",           0, "Self", 0, 1, 0, "self", "Base arithmetick algorithm on the target's CT.",
                    CastSpeed: 0),
                new(0x00, "Target Level",        0, "Self", 0, 1, 0, "self", "Base arithmetick algorithm on the target's level.",
                    CastSpeed: 0),
                new(0x00, "Target EXP",          0, "Self", 0, 1, 0, "self", "Base arithmetick algorithm on the target's EXP.",
                    CastSpeed: 0),
                new(0x00, "Target Elevation",    0, "Self", 0, 1, 0, "self", "Base arithmetick algorithm on the height of the target's current tile.",
                    CastSpeed: 0),
                new(0x00, "Prime",               0, "Self", 0, 1, 0, "self", "An algorithm for targeting units whose specified attribute is a prime number.",
                    CastSpeed: 0),
                new(0x00, "Multiple of 5",       0, "Self", 0, 1, 0, "self", "An algorithm for targeting units whose specified attribute is a multiple of 5.",
                    CastSpeed: 0),
                new(0x00, "Multiple of 4",       0, "Self", 0, 1, 0, "self", "An algorithm for targeting units whose specified attribute is a multiple of 4.",
                    CastSpeed: 0),
                new(0x00, "Multiple of 3",       0, "Self", 0, 1, 0, "self", "An algorithm for targeting units whose specified attribute is a multiple of 3.",
                    CastSpeed: 0),
            },
            ["Holy Sword"] = new()
            {
                new(0x1DD, "Judgment Blade",      0, "2", 99, 2, 0, "enemy/AoE", "Channels holy energy through one's sword. Attacks distant units and has a chance of inflicting Stop.",
                    CastSpeed: 0, AddedEffect: "Applies Stop"),
                new(0x1DE, "Cleansing Strike",    0, "3",  2, 1, 0, "enemy", "Channels holy energy through one's sword. Attacks a distant unit and has a chance of inflicting Doom.",
                    CastSpeed: 0, AddedEffect: "Applies Doom"),
                new(0x1DF, "Northswain's Strike", 0, "3",  1, 1, 0, "enemy", "Channels holy energy through one's sword. Attacks a distant unit and has a chance of causing instant KO.",
                    CastSpeed: 0, AddedEffect: "Applies KO"),
                new(0x1E0, "Hallowed Bolt",       0, "3", 99, 2, 1, "enemy/AoE", "Channels holy energy through one's sword. Attacks distant units and has a chance of inflicting Silence.",
                    CastSpeed: 0, AddedEffect: "Applies Silence"),
                new(0x00,  "Divine Ruination",    0, "5",  0, 1, 2, "enemy", "Channels holy energy through one's sword. Attacks units in a straight line and has a chance of inflicting Confusion.",
                    CastSpeed: 0, AddedEffect: "Applies Confusion", Shape: AbilityShape.Line),
            },
            // Darkness: Dark Knight skillset. All abilities require an equipped sword.
            // Sourced from FFT wiki.
            ["Darkness"] = new()
            {
                new(0x00, "Sanguine Sword",    0, "3", 0, 1, 0, "enemy", "Absorb HP from the target.",
                    CastSpeed: 0, AddedEffect: "Absorb HP: PA x WP x 80%"),
                new(0x00, "Infernal Strike",   0, "3", 0, 1, 0, "enemy", "Absorb MP from the target.",
                    CastSpeed: 0, AddedEffect: "Absorb MP: PA x WP x 80%"),
                new(0x00, "Crushing Blow",     0, "3", 0, 2, 0, "enemy/AoE", "Inflict damage with a sinister sword.",
                    CastSpeed: 0, AddedEffect: "Applies Stop (25%)"),
                new(0x00, "Abyssal Blade",     0, "3", 0, 3, 2, "enemy/AoE", "Sacrifice own HP to deal damage to others, with the nearest units suffering the most damage.",
                    CastSpeed: 0, AddedEffect: "Self-damage: 20% of max HP"),
                new(0x00, "Unholy Sacrifice",  0, "1", 0, 3, 0, "enemy/AoE", "Sacrifice own HP to deal extensive damage to all units in range.",
                    CastSpeed: 0, Element: "Dark", AddedEffect: "Applies Slow; Self-damage: 30% of max HP"),
            },
        };
    }
}
