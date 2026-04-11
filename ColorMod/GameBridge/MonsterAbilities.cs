using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Maps monster class names to their fixed ability loadout.
    ///
    /// Unlike human enemies (whose action abilities vary per-instance by encounter
    /// setup), monster abilities are fixed per class. A Goblin always has the same
    /// abilities across every battle it appears in.
    ///
    /// Verified empirically 2026-04-10: two Goblins in the same random encounter
    /// with different HP and level had identical ability loadouts (Tackle, Eye Gouge).
    /// See memory/project_unit_ability_region.md for the investigation.
    ///
    /// This dict is populated from in-game inspection of hovered monsters. Each
    /// entry lists the monster's action abilities in their menu order. Reaction,
    /// support, and movement abilities are NOT included — only the action abilities
    /// that would appear in the battle action menu.
    ///
    /// GENUS POOLS (for reference when filling in missing tiers — source: FFT wiki):
    /// - Skeleton: Chop, Ice Anima, Thunder Anima, Water Anima, Wind Anima
    /// - Ghost (Ghoul/Ghast/Revenant): Ectoplasm, Oily Touch, Drain Touch, Sleep Touch, Zombie Touch
    /// - Panther (Red Panther/Coeurl/Vampire Cat): Claw, Cat Scratch, Venom Fang, Blaster, Blood Drain
    /// - Eye (Floating Eye/Ahriman/Plague Horror): Wing Buffet, Bewitching Gaze, Dread Gaze, Beam, Doom
    /// - Aevis (Jura Aevis/Steelhawk/Cockatrice): Talon Dive, Beak, Peck, Glitterlust, Featherbomb
    /// - Chocobo (Chocobo/Black/Red): Choco Beak, Choco Pellets, Choco Meteor, Choco Cure, Choco Esuna
    /// - Goblin (Goblin/Black Goblin/Gobbledygook): Tackle, Eye Gouge, Spin Punch, Goblin Punch, Bloodfeast
    /// - Bomb (Bomb/Grenade/Exploder): Bite, Bomblet, Self-Destruct, Flame Attack, Spark
    /// - Bull (Wisenkin/Sekhret/Minotaur): Pickaxe, Feral Spin, Beef Up, Earthsplitter, Breathe Fire
    /// - Malboro (Malboro/Ochu/Great Malboro): Tentacles, Lick, Goo, Bad Breath, Malboro Spores
    /// - Mindflayer (Piscodaemon/Squidraken/Mindflayer): Tentacles, Ink, Dischord, Mind Blast, Level Drain
    /// - Dragon: Charge, Tail Sweep, Fire Breath, Ice Breath, Thunder Breath
    /// - Behemoth: Gore, Heave, Gigaflare, Twister, Almagest
    /// - Hydra: Tri-Attack, Tri-Breath, Tri-Flame, Tri-Thunder, Dark Whisper
    /// - Treant: Leaf Rain, Life Nymph, Magick Nymph, Guardian Nymph, Shell Nymph
    /// - Pig: Reckless Charge, Snort, Toot, Squeal, Bequeath Bacon
    ///
    /// Tiers do NOT simply layer — empirically Minotaur (tier 3 Bull) has
    /// Pickaxe+Feral Spin while Sekhret (tier 2) has Pickaxe+Earthsplitter+Beef Up.
    /// Each tier has its own specific subset. Do NOT auto-fill without confirmation.
    /// </summary>
    public static class MonsterAbilities
    {
        private static readonly Dictionary<string, string[]> ClassToAbilities = new()
        {
            // === Goblin family (pool: Tackle, Eye Gouge, Spin Punch, Goblin Punch, Bloodfeast)
            ["Goblin"] = new[] { "Tackle", "Eye Gouge" },                                   // verified
            ["Black Goblin"] = new[] { "Tackle", "Spin Punch" },                            // verified
            ["Gobbledygook"] = new[] { "Tackle", "Eye Gouge", "Goblin Punch" },             // wiki

            // === Bomb family (pool: Bite, Bomblet, Self-Destruct, Flame Attack, Spark)
            ["Bomb"] = new[] { "Bite", "Self-Destruct" },                                   // verified
            ["Grenade"] = new[] { "Bite", "Bomblet", "Self-Destruct" },                     // verified
            ["Exploder"] = new[] { "Bite", "Self-Destruct", "Spark" },                      // verified

            // === Aevis/Bird family (pool: Talon Dive, Beak, Peck, Glitterlust, Featherbomb)
            ["Steelhawk"] = new[] { "Talon Dive", "Glitterlust" },                          // verified
            ["Cockatrice"] = new[] { "Talon Dive", "Beak", "Featherbomb" },                 // verified
            // Jura Aevis: TBD — pool candidates above

            // === Eye/Ahriman family (pool: Wing Buffet, Bewitching Gaze, Dread Gaze, Beam, Doom)
            ["Floating Eye"] = new[] { "Wing Buffet" },                                     // wiki
            ["Ahriman"] = new[] { "Wing Buffet", "Dread Gaze", "Bewitching Gaze" },         // verified + wiki
            ["Plague Horror"] = new[] { "Wing Buffet", "Bewitching Gaze", "Doom" },         // wiki

            // === Bull family (pool: Pickaxe, Feral Spin, Beef Up, Earthsplitter, Breathe Fire)
            ["Wisenkin"] = new[] { "Pickaxe" },                                             // verified
            ["Sekhret"] = new[] { "Pickaxe", "Earthsplitter", "Beef Up" },                  // verified
            ["Minotaur"] = new[] { "Pickaxe", "Feral Spin" },                               // verified

            // === Mindflayer family (pool: Tentacles, Ink, Dischord, Mind Blast, Level Drain)
            ["Piscodaemon"] = new[] { "Tentacles" },                                        // verified
            ["Squidraken"] = new[] { "Tentacles", "Ink", "Dischord" },                      // verified
            ["Mindflayer"] = new[] { "Tentacles", "Ink", "Mind Blast" },                    // verified

            // === Malboro family (pool: Tentacles, Lick, Goo, Bad Breath, Malboro Spores)
            ["Malboro"] = new[] { "Tentacles", "Lick" },                                    // verified
            ["Ochu"] = new[] { "Tentacles", "Goo" },                                        // wiki
            ["Great Malboro"] = new[] { "Tentacles", "Bad Breath" },                        // wiki

            // === Panther family (pool: Claw, Cat Scratch, Venom Fang, Blaster, Blood Drain)
            ["Red Panther"] = new[] { "Claw", "Venom Fang" },                               // wiki
            ["Coeurl"] = new[] { "Claw", "Cat Scratch", "Venom Fang" },                     // verified
            ["Vampire Cat"] = new[] { "Claw", "Cat Scratch", "Blaster" },                   // wiki

            // === Chocobo family (pool: Choco Beak, Choco Pellets, Choco Meteor, Choco Cure, Choco Esuna)
            ["Chocobo"] = new[] { "Choco Beak", "Choco Cure" },                             // wiki
            ["Black Chocobo"] = new[] { "Choco Beak", "Choco Esuna", "Choco Pellets" },     // wiki
            ["Red Chocobo"] = new[] { "Choco Beak", "Choco Pellets", "Choco Meteor" },      // verified

            // === Ghost family (pool: Ectoplasm, Oily Touch, Drain Touch, Sleep Touch, Zombie Touch)
            ["Ghoul"] = new[] { "Ectoplasm", "Sleep Touch" },                               // wiki
            ["Ghast"] = new[] { "Ectoplasm", "Oily Touch" },                                // verified
            ["Revenant"] = new[] { "Ectoplasm", "Drain Touch" },                            // wiki

            // === Skeleton family (pool: Chop, Ice Anima, Thunder Anima, Water Anima, Wind Anima)
            ["Skeleton"] = new[] { "Chop", "Thunder Anima" },                               // wiki
            ["Bonesnatch"] = new[] { "Chop", "Water Anima" },                               // wiki
            ["Skeletal Fiend"] = new[] { "Chop", "Ice Anima" },                             // wiki

            // === Behemoth family (pool: Gore, Heave, Gigaflare, Twister, Almagest)
            // All three tiers share base kit Gore+Heave. Tier differentiation is purely
            // Beastmaster-learned magick (Gigaflare / Twister / Almagest) which enemies don't have.
            ["Behemoth"] = new[] { "Gore", "Heave" },                                       // verified
            ["Behemoth King"] = new[] { "Gore", "Heave" },                                  // wiki
            ["Dark Behemoth"] = new[] { "Gore", "Heave" },                                  // wiki

            // === Dragon family (pool: Charge, Tail Sweep, Fire Breath, Ice Breath, Thunder Breath)
            // Per IC remaster: Red Dragon has Fire Breath by default. Dragon tier 1 only has
            // Charge by default (Tail Sweep is Beastmaster-learned in all versions).
            ["Dragon"] = new[] { "Charge" },                                                // wiki
            ["Blue Dragon"] = new[] { "Charge", "Ice Breath" },                             // wiki
            ["Red Dragon"] = new[] { "Charge", "Fire Breath" },                             // wiki (IC remaster)

            // === Pig family (pool: Reckless Charge, Snort, Toot, Squeal, Bequeath Bacon)
            ["Pig"] = new[] { "Reckless Charge" },                                          // wiki
            ["Swine"] = new[] { "Reckless Charge", "Toot" },                                // wiki
            ["Wild Boar"] = new[] { "Reckless Charge", "Snort" },                           // wiki

            // === Treant family (pool: Leaf Rain, Life Nymph, Guardian Nymph, Shell Nymph, Magick Nymph)
            ["Dryad"] = new[] { "Leaf Rain" },                                              // wiki
            ["Treant"] = new[] { "Leaf Rain", "Life Nymph" },                               // wiki
            ["Elder Treant"] = new[] { "Leaf Rain", "Guardian Nymph", "Shell Nymph" },      // wiki

            // === Hydra family (pool: Tri-Attack, Tri-Flame, Tri-Thunder, Tri-Breath, Dark Whisper)
            ["Hydra"] = new[] { "Tri-Attack" },                                             // wiki
            ["Greater Hydra"] = new[] { "Tri-Attack", "Tri-Flame" },                        // wiki
            ["Tiamat"] = new[] { "Tri-Flame", "Tri-Thunder", "Tri-Breath" },                // wiki
        };

        /// <summary>
        /// Returns the ability list for a monster class, or null if not known.
        /// </summary>
        public static string[]? GetAbilities(string? className)
        {
            if (string.IsNullOrEmpty(className)) return null;
            return ClassToAbilities.TryGetValue(className, out var abilities) ? abilities : null;
        }
    }
}
