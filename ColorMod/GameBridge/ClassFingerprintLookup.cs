using System;
using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Looks up a unit's job/monster class name from an 11-byte fingerprint
    /// read from the heap unit struct at offset +0x69.
    ///
    /// The FFT IC remaster keeps enemy data in heap-allocated unit structs.
    /// The UI buffer for "current hovered unit" is unreliable during C+Up scan,
    /// and the condensed struct has no jobId byte. However, bytes at struct+0x69
    /// through struct+0x73 are a class fingerprint — identical across instances
    /// of the same class, distinct between classes.
    ///
    /// Empirically determined: byte 0 of the fingerprint (struct+0x69) is
    /// per-unit/team variation — two Knights (one player, one enemy) had bytes
    /// 02-0A-78-0F-... and 03-0A-78-0F-... respectively, differing only at byte 0.
    /// The true class signature is bytes 1-10 (10 bytes from struct+0x6A to +0x73).
    /// Stored keys therefore skip byte 0 to support both teams.
    ///
    /// This is our most reliable enemy class identifier. Player units should
    /// still prefer the roster lookup (which is authoritative); this is the
    /// fallback for any unit that can't be matched against the roster.
    ///
    /// See memory/project_class_fingerprint.md for the investigation history.
    /// </summary>
    public static class ClassFingerprintLookup
    {
        // Keys are bytes 1-10 of the 11-byte fingerprint (10 bytes total).
        // Byte 0 is skipped because it varies per-unit/team for the same class.
        //
        // Seeded from two Siedge Weald random encounters (2026-04-10) with
        // user-provided ground truth.
        private static readonly Dictionary<string, string> FingerprintToJob = new()
        {
            // === Monsters ===
            // Undead family (byte 1 = 0x05 for skeletons, 0x07 for ghosts):
            ["05-73-1E-05-55-78-27-6C-07-55"] = "Skeleton",
            ["05-65-1E-03-55-66-27-7D-07-58"] = "Skeletal Fiend",
            ["05-5A-1E-04-55-6A-27-7B-07-57"] = "Bonesnatch",
            ["00-00-00-00-06-01-00-00-00-00"] = "Bonesnatch", // variant — near-zero fingerprint, possibly mod-altered
            ["07-52-1E-60-55-6E-27-5D-07-6A"] = "Ghast",
            ["07-53-1E-7C-55-67-27-5A-07-69"] = "Ghoul",
            ["07-5D-1E-40-55-79-27-61-07-6E"] = "Revenant",
            // Goblin family (tier 1 Goblin, tier 2 Black Goblin, tier 3 Gobbledygook):
            ["07-75-1E-28-55-69-27-62-07-55"] = "Goblin",
            ["06-56-1E-23-55-72-27-67-07-57"] = "Black Goblin",
            // Bomb family:
            ["07-57-1E-1E-5A-73-27-55-07-5E"] = "Grenade",
            ["8C-64-88-43-88-43-84-22-84-22"] = "Grenade",
            // Tree family (tier 1 Dryad, tier 2 Treant):
            ["07-96-1E-A0-5A-63-27-66-07-64"] = "Dryad",
            ["07-82-1E-B4-5A-60-27-59-07-5F"] = "Treant",

            // Malboro family (tier 1 Malboro, tier 2 Ochu):
            ["08-AF-1E-0F-5A-61-26-69-1E-63"] = "Malboro",
            ["08-91-1E-0F-5A-5F-27-6E-1B-6E"] = "Ochu",
            // Floating Eye family (tier 1 Floating Eye, tier 2 Ahriman, tier 3 Plague Horror):
            ["06-50-1E-50-55-68-28-5A-07-59"] = "Floating Eye",
            ["06-4D-1E-8C-55-6C-28-7F-07-78"] = "Plague Horror",
            // Panther family:
            ["06-5B-1E-3C-55-81-27-74-07-69"] = "Coeurl",
            // Chocobo family (tier 1 Chocobo, tier 2 Black Chocobo, tier 3 Red Chocobo):
            ["08-6C-1E-64-4B-77-23-62-07-5E"] = "Chocobo",
            ["07-50-1E-96-55-62-27-96-07-69"] = "Black Chocobo",
            ["04-5B-1E-5A-55-88-27-82-07-63"] = "Red Chocobo",
            // Bomb family (shared by Bomb / Grenade / Exploder variants):
            ["07-55-1E-14-5A-68-27-64-07-5D"] = "Bomb",
            // Bird family (tier 1 Jura Aevis, tier 2 Steelhawk, tier 3 Cockatrice):
            ["07-5A-1E-28-55-71-27-69-07-55"] = "Jura Aevis",
            ["07-55-1E-3C-55-83-27-6C-07-5A"] = "Steelhawk",
            ["07-65-1E-0A-55-87-27-98-07-64"] = "Cockatrice",
            // Bull family (tier 1 Wisenkin, tier 2 Sekhret):
            ["06-87-1E-05-55-6B-27-78-07-64"] = "Wisenkin",
            ["06-97-1E-0A-55-7A-27-AD-07-64"] = "Sekhret",
            // Goblin family (tier 3):
            ["06-62-1E-4B-55-80-27-73-07-5C"] = "Gobbledygook",
            // Squid/Piscodaemon family (tier 1 Piscodaemon, tier 2 Squidraken, tier 3 Mindflayer):
            ["07-6C-1E-78-55-6F-27-5A-07-60"] = "Piscodaemon",
            ["07-73-1E-73-55-65-27-65-07-60"] = "Squidraken",
            ["07-5C-1E-A0-55-70-27-7F-07-5C"] = "Mindflayer",
            // Behemoth family:
            ["05-8C-1E-78-55-75-24-86-07-69"] = "Behemoth",
            // Panther family (tier 1 Red Panther, tier 2 Coeurl, tier 3 Vampire Cat):
            ["06-74-1E-32-55-74-27-62-07-5B"] = "Red Panther",
            ["06-63-1E-46-55-86-27-84-07-55"] = "Vampire Cat",
            // Bomb family (tier 2/3 Exploder):
            ["07-7C-1E-28-5A-64-27-74-07-60"] = "Exploder",
            // Dragon family:
            ["06-85-1E-4B-55-76-27-88-07-64"] = "Dragon",
            ["06-9D-1E-73-55-84-27-93-06-64"] = "Red Dragon",
            // Bull family (Wisenkin tier 1, Minotaur tier 3):
            ["06-A0-1E-08-55-6C-27-98-07-64"] = "Minotaur",

            // === Generic human jobs ===
            ["0C-4B-09-78-64-64-3C-3C-32-96"] = "Black Mage",
            ["09-87-0D-50-64-6E-30-81-32-50"] = "Monk",
            ["0D-46-08-7D-64-5A-46-32-32-7D"] = "Summoner",
            ["0C-50-10-4B-64-64-4B-4B-32-50"] = "Chemist",
            ["0B-64-10-41-64-64-2D-6E-32-50"] = "Archer",
            ["0C-4B-0A-6E-64-64-3C-32-32-78"] = "Mystic",
            ["0A-78-0F-50-64-64-28-78-32-50"] = "Knight",
            ["0B-5A-10-32-5A-6E-32-64-32-3C"] = "Thief",
            ["0C-4B-0A-78-64-64-41-32-32-82"] = "Time Mage",
            ["0A-50-0A-78-64-6E-32-5A-32-6E"] = "White Mage",
            ["0C-4B-0E-5A-64-64-2D-80-32-5A"] = "Samurai",
            ["0A-78-0F-32-64-64-28-78-32-32"] = "Dragoon",
            ["0B-64-0F-4B-64-64-3C-5A-32-50"] = "Squire",
            ["14-37-14-32-64-64-50-1E-32-73"] = "Bard",
            ["06-8C-1E-32-64-78-23-78-28-73"] = "Mime",
            ["0C-46-0D-32-50-78-2B-7A-32-4B"] = "Ninja",

            // === Story character classes ===
            // Construct 8 (Automaton) has a distinctive 0x64/0x00 pattern.
            ["04-73-64-00-50-69-1E-8E-64-00"] = "Automaton",
            // Meliadoul (Divine Knight):
            ["0A-7D-0F-50-64-69-27-78-32-5A"] = "Divine Knight",
            // Isilud (Nightblade — PSX: Assassin variant):
            ["0A-9B-0B-32-64-6E-30-78-32-32"] = "Nightblade",
            // Wiegraf (White Knight — note: his class may change through the story):
            ["0A-C8-0B-96-64-8C-32-7D-32-64"] = "White Knight",
            // Barich (Machinist — story enemy, same class name as Mustadio's but different fingerprint):
            ["08-8C-0B-64-64-7D-32-64-2E-78"] = "Machinist",

            // Note: Ramza's fingerprint differs between saves (saw 5 different
            // patterns now, depending on his current job/equipment/growth).
            // Roster lookup is authoritative for Ramza — fingerprint is unreliable.
        };

        /// <summary>
        /// Fingerprints that are shared between two DIFFERENT classes. Resolved
        /// by team (player vs enemy). Both classes have identical 10-byte signatures
        /// — this is a known limitation of the fingerprint approach.
        /// </summary>
        private static readonly Dictionary<string, (string player, string enemy)> FingerprintByTeam = new()
        {
            // Arithmetician (player) and Ahriman (enemy monster) have identical
            // 11-byte fingerprints. Observed empirically in Zeirchele Falls battle.
            ["06-4B-1E-5F-55-5F-28-8C-07-5F"] = ("Arithmetician", "Ahriman"),
        };

        /// <summary>
        /// Look up a job name from an 11-byte fingerprint. Returns null if unknown.
        /// Uses bytes 1-10 as the key (byte 0 is team/unit variation).
        /// </summary>
        public static string? GetJobName(byte[] fingerprint)
        {
            return GetJobName(fingerprint, team: null);
        }

        /// <summary>
        /// Look up a job name with optional team disambiguation. Some classes share
        /// identical fingerprints — passing the unit's team (0=player, 1=enemy)
        /// lets us resolve the collision.
        /// </summary>
        public static string? GetJobName(byte[] fingerprint, int? team)
        {
            if (fingerprint == null || fingerprint.Length < 11) return null;
            var key = BitConverter.ToString(fingerprint, 1, 10);

            // Team-disambiguated entries first
            if (FingerprintByTeam.TryGetValue(key, out var pair))
            {
                if (team == 0) return pair.player;
                if (team == 1) return pair.enemy;
                // Unknown team — return both for logging
                return $"{pair.player}/{pair.enemy}";
            }

            return FingerprintToJob.TryGetValue(key, out var name) ? name : null;
        }

        /// <summary>
        /// Convert an 11-byte fingerprint to its canonical string key (for logging).
        /// Returns the full 11-byte hex for diagnostics, with byte 0 separated.
        /// </summary>
        public static string ToKey(byte[] fingerprint)
        {
            if (fingerprint == null || fingerprint.Length < 11) return "";
            return BitConverter.ToString(fingerprint, 0, 11);
        }
    }
}
