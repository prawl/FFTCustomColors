using System.Collections.Generic;
using System.Linq;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// JP costs for action abilities, sourced from FFTHandsFree/ABILITY_COSTS.md
    /// (IC remaster WotL values). Keyed by ability name and resolved to IDs at
    /// static init via ActionAbilityLookup.Skillsets. Any name that fails to
    /// resolve lands in UnresolvedNames and fails the test suite via a
    /// dedicated test — mirrors the ItemPrices name-keyed pattern.
    ///
    /// Passive (reaction/support/movement) costs are NOT covered here — this
    /// file is specifically for the "Next: N" header computation which only
    /// cares about action abilities in the unit's current primary skillset.
    /// </summary>
    public static class AbilityJpCosts
    {
        /// <summary>Names in CostByName that aren't present in any ActionAbilityLookup skillset.</summary>
        public static readonly List<string> UnresolvedNames = new();

        /// <summary>Raw source data — ability name to JP cost.</summary>
        public static readonly Dictionary<string, int> CostByName = new()
        {
            // Squire / Mettle / Fundaments
            // Focus/Rush/Throw Stone/Salve are common to both Fundaments (generic
            // Squire) and Mettle (Ramza's Gallant Knight unique primary). Values
            // are IC-remaster WotL costs, live-verified session 19.
            ["Focus"] = 300, ["Rush"] = 80, ["Throw Stone"] = 90, ["Salve"] = 150,
            // Mettle-only abilities (Ramza Gallant Knight primary). Values below
            // are sourced from FFTHandsFree/Wiki/Abilities.md (Squire/Mettle rows)
            // which tracks the canonical FFT wiki JP costs. IC-remaster values
            // may differ slightly; flagged for in-game verification whenever a
            // Mettle user has any of these unlearned (Ramza in this save has
            // them maxed so we cannot read them locally). ComputeNextJpForSkillset
            // now returns a non-null "Next: N" for Mettle users with unlearned
            // entries in this set rather than silently skipping them.
            ["Tailwind"] = 150,
            ["Chant"]    = 300,
            ["Steel"]    = 200,
            ["Shout"]    = 600,
            ["Ultima"]   = 4000,

            // Chemist / Items
            ["Potion"] = 30, ["Hi-Potion"] = 200, ["X-Potion"] = 300,
            ["Ether"] = 300, ["Hi-Ether"] = 400, ["Elixir"] = 900,
            ["Antidote"] = 70, ["Eye Drop"] = 80, ["Echo Herbs"] = 120,
            ["Maiden's Kiss"] = 200, ["Gold Needle"] = 250, ["Holy Water"] = 400,
            ["Remedy"] = 700, ["Phoenix Down"] = 90,

            // Knight / Arts of War
            ["Rend Helm"] = 300, ["Rend Armor"] = 350, ["Rend Shield"] = 250,
            ["Rend Weapon"] = 350, ["Rend MP"] = 200, ["Rend Speed"] = 250,
            ["Rend Power"] = 250, ["Rend Magick"] = 250,

            // Archer / Aim
            ["Aim +1"] = 100, ["Aim +2"] = 150, ["Aim +3"] = 200, ["Aim +4"] = 250,
            ["Aim +5"] = 300, ["Aim +7"] = 400, ["Aim +10"] = 700, ["Aim +20"] = 1200,

            // Monk / Martial Arts
            ["Cyclone"] = 150, ["Pummel"] = 300, ["Aurablast"] = 300, ["Shockwave"] = 600,
            ["Doom Fist"] = 300, ["Purification"] = 200, ["Chakra"] = 350, ["Revive"] = 500,

            // White Mage / White Magicks
            ["Cure"] = 50, ["Cura"] = 180, ["Curaga"] = 450, ["Curaja"] = 800,
            ["Raise"] = 200, ["Arise"] = 600, ["Reraise"] = 1000,
            ["Regen"] = 350, ["Protect"] = 70, ["Protectja"] = 600,
            ["Shell"] = 70, ["Shellja"] = 600, ["Wall"] = 400,
            ["Esuna"] = 300, ["Holy"] = 600,

            // Black Mage / Black Magicks
            ["Fire"] = 50, ["Thunder"] = 50, ["Blizzard"] = 50,
            ["Fira"] = 200, ["Thundara"] = 200, ["Blizzara"] = 200,
            ["Firaga"] = 500, ["Thundaga"] = 500, ["Blizzaga"] = 500,
            ["Firaja"] = 350, ["Thundaja"] = 350, ["Blizzaja"] = 350,
            ["Poison"] = 150, ["Toad"] = 500, ["Death"] = 600, ["Flare"] = 350,

            // Time Mage / Time Magicks
            ["Haste"] = 100, ["Hasteja"] = 600, ["Slow"] = 80, ["Slowja"] = 600,
            ["Stop"] = 350, ["Immobilize"] = 100, ["Float"] = 200, ["Reflect"] = 300,
            ["Quick"] = 900, ["Gravity"] = 250, ["Graviga"] = 550, ["Meteor"] = 1500,

            // Summoner / Summon
            ["Moogle"] = 110, ["Shiva"] = 200, ["Ramuh"] = 200, ["Ifrit"] = 200,
            ["Titan"] = 220, ["Golem"] = 500, ["Carbuncle"] = 350, ["Bahamut"] = 1600,
            ["Odin"] = 900, ["Leviathan"] = 860, ["Salamander"] = 860,
            ["Sylph"] = 400, ["Faerie"] = 400,
            ["Lich"] = 600, ["Cyclops"] = 1000, ["Zodiark"] = 0,

            // Thief / Steal
            ["Steal Gil"] = 10, ["Steal Heart"] = 150, ["Steal Helm"] = 350,
            ["Steal Armor"] = 450, ["Steal Shield"] = 350, ["Steal Weapon"] = 600,
            ["Steal Accessory"] = 500, ["Steal EXP"] = 250,

            // Orator / Speechcraft
            ["Entice"] = 100, ["Stall"] = 100, ["Praise"] = 200, ["Intimidate"] = 200,
            ["Preach"] = 200, ["Enlighten"] = 200, ["Condemn"] = 500, ["Beg"] = 100,
            ["Insult"] = 300, ["Mimic Darlavon"] = 300,

            // Mystic / Mystic Arts
            ["Umbra"] = 100, ["Empowerment"] = 200, ["Invigoration"] = 350,
            ["Belief"] = 400, ["Disbelief"] = 400, ["Corruption"] = 300,
            ["Quiescence"] = 170, ["Fervor"] = 400, ["Trepidation"] = 200,
            ["Delirium"] = 400, ["Harmony"] = 800, ["Hesitation"] = 100,
            ["Repose"] = 350, ["Induration"] = 600,

            // Geomancer / Geomancy — all 150 each (applied as blanket in ComputeNextJpForSkillset)

            // Dragoon / Jump — the individual Horizontal/Vertical Jump levels aren't
            // surfaced as separate entries in ActionAbilityLookup (they collapse into
            // a single "Jump" entry via CollapseJumpAbilities). Cost data omitted for
            // now; ComputeNextJpForSkillset returns null for Jump.

            // Samurai / Iaido — skillset uses "Bizen Osafune" and "Ame-no-Murakumo"
            ["Ashura"] = 100, ["Kotetsu"] = 200, ["Bizen Osafune"] = 300, ["Murasame"] = 400,
            ["Ame-no-Murakumo"] = 500, ["Kiyomori"] = 600, ["Muramasa"] = 700,
            ["Kiku-ichimonji"] = 800, ["Masamune"] = 900, ["Chirijiraden"] = 1000,

            // Ninja / Throw — skillset uses "Throw <Category>" plural form
            ["Throw Shuriken"] = 50, ["Throw Bombs"] = 70,
            ["Throw Daggers"] = 100, ["Throw Swords"] = 100, ["Throw Flails"] = 100,
            ["Throw Katana"] = 100, ["Throw Ninja Blades"] = 100, ["Throw Polearms"] = 100,
            ["Throw Poles"] = 100, ["Throw Knight's Swords"] = 100, ["Throw Books"] = 100,
            ["Throw Axes"] = 120,

            // Arithmetician / Arithmeticks — skillset uses "Target <X>" naming
            ["Target CT"] = 250, ["Target Level"] = 350, ["Target EXP"] = 200,
            ["Target Elevation"] = 250,
            ["Prime"] = 300, ["Multiple of 5"] = 200,
            ["Multiple of 4"] = 400, ["Multiple of 3"] = 600,

            // Dark Knight / Darkness
            ["Sanguine Sword"] = 500, ["Crushing Blow"] = 300,
            ["Abyssal Blade"] = 1000, ["Unholy Sacrifice"] = 900,

            // NOTE: Story-character primary skillsets (Holy Sword, Limit,
            // Dragon, Work, Snipe, Sky Mantra, Nether Mantra, Unyielding
            // Blade, Spellblade, Sky Pirating, Hunting, Fell Sword) are
            // intentionally NOT populated. In canon FFT those skillsets
            // are learned via story progression / level-up, not JP
            // purchase — there is no "Next: N" value for them. The
            // game doesn't show a Next value on story-class
            // CharacterStatus headers either. Returning null is correct.
        };

        /// <summary>Default JP cost for every Geomancy ability (all 150).</summary>
        public const int GeomancyCost = 150;

        /// <summary>Default JP cost for every Bardsong/Dance ability (all 100).</summary>
        public const int BardDanceCost = 100;

        /// <summary>Skillset names that get a blanket cost for every entry
        /// (handled in ComputeNextJpForSkillset, not via CostByName).</summary>
        private static readonly Dictionary<string, int> _blanketBySkillset = new()
        {
            ["Geomancy"] = GeomancyCost,
            ["Bardsong"] = BardDanceCost,
            ["Dance"] = BardDanceCost,
        };

        static AbilityJpCosts()
        {
            // Validate every CostByName key resolves to an ability name in some
            // ActionAbilityLookup skillset. Mirrors ItemPrices.UnresolvedNames —
            // a name typo lands as a failing unit test.
            var allNames = new HashSet<string>();
            foreach (var skillset in ActionAbilityLookup.AllSkillsets.Values)
                foreach (var ability in skillset)
                    allNames.Add(ability.Name);

            foreach (var kvp in CostByName)
                if (!allNames.Contains(kvp.Key))
                    UnresolvedNames.Add(kvp.Key);
        }

        /// <summary>
        /// Returns the JP cost of the given ability by name, or null if unknown.
        /// Name-based lookup (not ID-based) because several skillsets — Geomancy,
        /// Bardsong, Dance, Martial Arts, Darkness — contain entries with ID 0x00
        /// pending real-IC-ID verification.
        /// </summary>
        public static int? GetCost(string abilityName) =>
            CostByName.TryGetValue(abilityName, out int c) ? c : (int?)null;

        /// <summary>
        /// Returns the JP cost of the cheapest unlearned ability in the given
        /// skillset. learnedIndices is the 0-15 bitfield index set from
        /// ActionAbilityLookup.DecodeLearnedBitfield. Abilities with unknown
        /// cost are skipped; if every ability is learned or every unlearned
        /// one has unknown cost, returns null.
        ///
        /// This is the "Next: N" value shown in the game's CharacterStatus header.
        /// </summary>
        public static int? ComputeNextJpForSkillset(string skillsetName, HashSet<int> learnedIndices)
        {
            var skillset = ActionAbilityLookup.GetSkillsetAbilities(skillsetName);
            if (skillset == null) return null;

            int? min = null;
            bool blanketSkillset = _blanketBySkillset.TryGetValue(skillsetName, out int blanketCost);

            for (int i = 0; i < skillset.Count; i++)
            {
                if (learnedIndices.Contains(i)) continue;

                int? cost = blanketSkillset
                    ? blanketCost
                    : GetCost(skillset[i].Name);

                if (cost == null) continue;
                if (min == null || cost.Value < min.Value) min = cost;
            }
            return min;
        }
    }
}
