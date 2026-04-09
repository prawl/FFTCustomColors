using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Static lookup tables for character names, job names, and monster names.
    /// Provides nameId→name (for story characters) and jobId→name (for generic units/monsters).
    /// NameIds are uint16 values at roster offset +0x230 and condensed struct offset +0x04.
    /// JobIds are byte values at roster offset +0x02.
    /// Uses WotL/IC remaster names.
    /// </summary>
    public static class CharacterData
    {
        // =============================================================
        // Name ID → Character Name (story characters)
        // =============================================================
        // These nameIds are from the IC remaster's string table.
        // Verified entries were confirmed by reading memory in-game.
        // Unverified entries are adapted from the PSX UnitNames table
        // and may use different IDs in the IC remaster.
        // Generic/random units get nameIds from the random name pool
        // (typically IDs 100+) — use GetJobName() for those.
        // =============================================================

        /// <summary>
        /// Maps condensed/roster nameId to character name (story characters only).
        /// Generic units use random names from the name pool — use GetJobName() instead.
        /// </summary>
        public static readonly Dictionary<int, string> NameById = new()
        {
            // ── Verified in IC remaster (confirmed in-game) ──
            [1] = "Ramza",
            [2] = "Delita",
            [13] = "Orlandeau",
            [15] = "Reis",
            [22] = "Mustadio",
            [26] = "Marach",
            [30] = "Agrias",
            [31] = "Beowulf",
            [41] = "Rapha",
            [42] = "Meliadoul",
            [50] = "Cloud",
            [117] = "Construct 8",
            [140] = "Balthier",
            [141] = "Luso",

            // ── Unverified: adapted from PSX table, may differ in IC remaster ──
            // PSX table has Ramza at 1-3 (chapter variants), Delita at 4-6, etc.
            // IC remaster appears to use a single ID per character.
            [3] = "Argath",          // PSX: Algus
            [4] = "Ovelia",
            [5] = "Alma",
            [6] = "Tietra",          // PSX: Teta
            [7] = "Zalbag",
            [8] = "Dycedarg",
            [9] = "Larg",            // Duke Larg
            [10] = "Goltanna",       // PSX: Goltana → WotL: Goltanna
            [11] = "Funeral",        // PSX: Funeral → WotL: Funebris
            [12] = "Wiegraf",
            [14] = "Orran",          // PSX: Olan → WotL: Orran
            [16] = "Zalmour",        // PSX: Zalmo → WotL: Zalmour (unverified in IC)
            [17] = "Gaffgarion",     // PSX: Gafgarion → WotL: Gaffgarion (unverified)
            [18] = "Izlude",         // PSX: 0x12=Malak but IC may differ; Izlude is Templar
            [19] = "Folmarv",        // PSX: Vormav → WotL: Folmarv
            [20] = "Loffrey",        // PSX: Rofel → WotL: Loffrey
            [21] = "Cletienne",      // PSX: Kletian → WotL: Cletienne
            [23] = "Barich",         // PSX: Balk → WotL: Barich
            [24] = "Bremondt",       // PSX: Draclau → WotL: Celebrant Bremondt (unverified)
            [25] = "Elmdore",        // Marquis Elmdor (unverified)
            [27] = "Simon",          // Priest Simon (unverified)
            [28] = "Boco",           // Boco the Chocobo
            [29] = "Ladd",           // PSX: Rad → WotL: Ladd
            [32] = "Lavian",
            [33] = "Alicia",
            [34] = "Balmafula",      // PSX: Balmafula → WotL: Valmafra
            [35] = "Celia",          // Assassin
            [36] = "Lettie",         // PSX: Lede → WotL: Lettie
            [37] = "Orran",          // Possible duplicate of 14

            // ── Lucavi / Bosses (from PSX table, unverified in IC) ──
            [49] = "Ajora",          // PSX: 0x31
            [51] = "Zalbag",         // Undead Zalbag variant (PSX: 0x33)
            [52] = "Agrias",         // Guest variant (PSX: 0x34)
            [60] = "Velius",         // PSX: 0x3C → WotL: Belias
            [62] = "Zalera",         // PSX: 0x3E → WotL: Zalera
            [64] = "Hashmal",        // PSX: 0x40 Hashmalum → WotL: Hashmal
            [65] = "Ultima",         // PSX: 0x41 Altima → WotL: Ultima
            [67] = "Cuchulainn",     // PSX: 0x43 Queklain → WotL: Cuchulainn
            [69] = "Adrammelech",    // PSX: 0x45 Adramelk → WotL: Adrammelech
            [72] = "Reis",           // Dragon form (PSX: 0x48)
        };

        // =============================================================
        // Job ID → Job/Class Name (for generic units, monsters, bosses)
        // =============================================================
        // Job IDs are byte values at roster offset +0x02.
        // Generic human units use male/female ID pairs.
        // Monsters and special jobs have unique IDs.
        // =============================================================

        /// <summary>
        /// Maps job ID to human-readable job/class name.
        /// Covers generic jobs, story character jobs, monster types, and Lucavi.
        /// </summary>
        public static readonly Dictionary<int, string> JobNameById = new()
        {
            // ── Generic human jobs (male/female pairs) ──
            [0x01] = "Chemist",
            [0x02] = "Chemist",
            [0x03] = "Knight",
            [0x04] = "Knight",
            [0x05] = "Archer",
            [0x06] = "Archer",
            [0x07] = "Monk",
            [0x08] = "Monk",
            [0x09] = "White Mage",
            [0x0A] = "White Mage",
            [0x0B] = "Black Mage",
            [0x0C] = "Black Mage",
            [0x0D] = "Time Mage",
            [0x0E] = "Time Mage",
            [0x0F] = "Summoner",
            [0x10] = "Summoner",
            [0x11] = "Thief",
            [0x12] = "Thief",
            [0x13] = "Orator",
            [0x14] = "Orator",
            [0x15] = "Mystic",
            [0x16] = "Mystic",
            [0x17] = "Geomancer",
            [0x18] = "Geomancer",
            [0x19] = "Dragoon",
            [0x1A] = "Dragoon",
            [0x1B] = "Samurai",
            [0x1C] = "Samurai",
            [0x1D] = "Ninja",
            [0x1E] = "Ninja",
            [0x1F] = "Arithmetician",
            [0x20] = "Arithmetician",
            [0x21] = "Bard",          // Male only
            [0x22] = "Dancer",         // Female only
            [0x23] = "Mime",
            [0x24] = "Mime",

            // ── Generic Squire ──
            [0x4A] = "Squire",
            [0x4B] = "Squire",

            // ── WotL-exclusive generic jobs ──
            [0xA4] = "Dark Knight",
            [0xA5] = "Dark Knight",
            [0xA6] = "Onion Knight",
            [0xA7] = "Onion Knight",

            // ── Story character unique jobs ──
            [0x4C] = "Holy Knight",         // Agrias
            [0x4D] = "Holy Knight",         // Delita Ch1
            [0x4E] = "Holy Knight",         // Delita Ch2+
            [0x4F] = "Engineer",            // Mustadio (PSX name; WotL: Machinist)
            [0x50] = "Machinist",           // Mustadio WotL
            [0x51] = "Heaven Knight",       // Rafa/Rapha (PSX); WotL: Skyseer
            [0x52] = "Hell Knight",         // Malak/Marach (PSX); WotL: Netherseer
            [0x53] = "Fell Knight",         // Gaffgarion; PSX: Dark Knight
            [0x54] = "Divine Knight",       // Meliadoul
            [0x55] = "Princess",            // Ovelia
            [0x56] = "Cleric",              // Alma
            [0x57] = "Astrologer",          // Orran; PSX: Astrologist
            [0x58] = "Dragonkin",           // Reis (human form)
            [0x59] = "Holy Dragon",         // Reis (dragon form)
            [0x5A] = "Arc Knight",          // Zalbag/Delita (endgame)
            [0x5B] = "Templar",             // Beowulf
            [0x5C] = "Assassin",            // Celia
            [0x5D] = "Assassin",            // Lettie
            [0x5E] = "Thunder God",         // Orlandeau (Sword Saint in WotL)
            [0x5F] = "Sword Saint",         // Cidolfus Orlandeau WotL variant
            [0x60] = "Rune Knight",         // Dycedarg
            [0x32] = "Soldier",             // Cloud
            [0x91] = "Steel Giant",         // Construct 8 / Worker 8

            // ── Ramza's chapter jobs ──
            [0xA0] = "Heretic",             // Ramza Ch4 (Gallant Knight variant)
            [0xA1] = "Squire",              // Ramza Ch2-3 variant

            // ── WotL guest character jobs ──
            [0xA2] = "Sky Pirate",          // Balthier
            [0xA3] = "Game Hunter",         // Luso

            // ── Monster jobs: Chocobos ──
            [0x25] = "Chocobo",
            [0x26] = "Black Chocobo",
            [0x27] = "Red Chocobo",

            // ── Monster jobs: Goblins ──
            [0x28] = "Goblin",
            [0x29] = "Black Goblin",
            [0x2A] = "Gobbledygook",        // WotL name

            // ── Monster jobs: Bombs ──
            [0x2B] = "Bomb",
            [0x2C] = "Grenade",
            [0x2D] = "Exploder",

            // ── Monster jobs: Panthers ──
            [0x2E] = "Red Panther",
            [0x2F] = "Coeurl",
            [0x30] = "Vampire Cat",

            // ── Monster jobs: Skeletons ──
            [0x31] = "Skeleton",
            [0x33] = "Bonesnatch",          // 0x32 = Cloud's Soldier, skip
            [0x34] = "Skeletal Fiend",      // WotL name; PSX: Living Bone

            // ── Monster jobs: Ghosts ──
            [0x35] = "Ghoul",
            [0x36] = "Ghast",               // WotL name; PSX: Gust
            [0x37] = "Revenant",

            // ── Monster jobs: Floating Eyes ──
            [0x38] = "Floating Eye",
            [0x39] = "Ahriman",
            [0x3A] = "Plague Horror",        // WotL name; PSX: Plague

            // ── Monster jobs: Dragons ──
            [0x3B] = "Dragon",               // PSX: Blue Dragon
            [0x3C] = "Blue Dragon",
            [0x3D] = "Red Dragon",

            // ── Monster jobs: Pigs ──
            [0x3E] = "Pig",                  // WotL: Uribo
            [0x3F] = "Swine",                // WotL: Porky
            [0x40] = "Wild Boar",

            // ── Monster jobs: Bulls ──
            [0x41] = "Minotaur",             // WotL: Wisenkin (tier 1)
            [0x42] = "Sacred",               // WotL: Minotaur (tier 2)
            [0x43] = "Sekhret",              // WotL: Sekhret (tier 3)

            // ── Monster jobs: Morbols ──
            [0x44] = "Malboro",              // PSX: Morbol
            [0x45] = "Ochu",
            [0x46] = "Great Malboro",

            // ── Monster jobs: Treants ──
            [0x47] = "Dryad",               // PSX: Woodman
            [0x48] = "Treant",              // PSX: Trent
            [0x49] = "Elder Treant",

            // ── Monster jobs: Hydras ──
            [0x61] = "Hydra",               // Exact IDs may vary
            [0x62] = "Greater Hydra",
            [0x63] = "Tiamat",

            // ── Monster jobs: Piscodaemons ──
            [0x64] = "Piscodaemon",
            [0x65] = "Squidraken",
            [0x66] = "Mindflayer",

            // ── Monster jobs: Juravis ──
            [0x67] = "Juravis",             // WotL: Jura Aevis
            [0x68] = "Steelhawk",           // WotL: Iron Hawk → Steelhawk
            [0x69] = "Cockatrice",

            // ── Monster jobs: Behemoths ──
            [0x6A] = "Behemoth",
            [0x6B] = "Behemoth King",       // PSX: King Behemoth
            [0x6C] = "Dark Behemoth",

            // ── Lucavi / Boss monster jobs ──
            [0x70] = "Belias",              // PSX: Velius
            [0x71] = "Zalera",              // The Death Seraph
            [0x72] = "Adrammelech",         // PSX: Adramelk
            [0x73] = "Hashmal",             // PSX: Hashmalum
            [0x74] = "Ultima",              // PSX: Altima (first form)
            [0x75] = "Ultima",              // PSX: Altima (arch angel form)
            [0x76] = "Cuchulainn",          // PSX: Queklain
            [0x77] = "Archaeodaemon",
            [0x78] = "Ultima Demon",

            // ── Special encounter monsters ──
            [0x82] = "Byblos",              // Rare book monster
            [0x90] = "Reaver",              // Endgame enemy
        };

        /// <summary>
        /// Resolves a condensed/roster nameId to a character name, or null if unknown.
        /// Best for story characters. Generic units use random names from the name pool.
        /// </summary>
        public static string? GetName(int condensedNameId)
        {
            return NameById.TryGetValue(condensedNameId, out var name) ? name : null;
        }

        /// <summary>
        /// Resolves a job ID to a job/class name, or null if unknown.
        /// Works for generic jobs, story character jobs, and monster types.
        /// This is the best way to identify generic units and monsters.
        /// </summary>
        public static string? GetJobName(int jobId)
        {
            // Try IC remaster roster IDs first (74+ range), then fall back to PSX IDs
            if (RosterJobNameById.TryGetValue(jobId, out var rosterName))
                return rosterName;
            return JobNameById.TryGetValue(jobId, out var name) ? name : null;
        }

        /// <summary>
        /// IC remaster roster job IDs (at roster offset +0x02).
        /// These differ from PSX job IDs and overlap with PSX story character IDs.
        /// Verified empirically by reading roster data during battle.
        /// </summary>
        private static readonly Dictionary<int, string> RosterJobNameById = new()
        {
            // Ramza's unique job IDs (take priority over PSX generic Knight=0x03)
            [3] = "Gallant Knight",      // 0x03 — Ramza Ch4, primary=Mettle (game displays "Gallant Knight")
            [0xA0] = "Gallant Knight",  // Ramza variant
            [0xA1] = "Squire",          // Ramza Ch2-3 variant

            // Generic human jobs (IC remaster roster IDs)
            [74] = "Squire",          // 0x4A — verified
            [75] = "Chemist",         // 0x4B — estimated (sequential)
            [76] = "White Mage",      // 0x4C — estimated
            [77] = "Archer",          // 0x4D — verified
            [78] = "Monk",            // 0x4E — verified
            [79] = "Knight",          // 0x4F — verified
            [80] = "Black Mage",      // 0x50 — estimated
            [81] = "Time Mage",       // 0x51 — estimated
            [82] = "Summoner",        // 0x52 — estimated
            [83] = "Thief",           // 0x53 — estimated
            [84] = "Orator",          // 0x54 — estimated
            [85] = "Mystic",          // 0x55 — estimated
            [86] = "Geomancer",       // 0x56 — estimated
            [87] = "Dragoon",         // 0x57 — estimated
            [88] = "Samurai",         // 0x58 — estimated
            [89] = "Ninja",           // 0x59 — verified
            [90] = "Arithmetician",   // 0x5A — estimated
            [91] = "Bard",            // 0x5B — estimated
            [92] = "Dancer",          // 0x5C — estimated
            [93] = "Mime",            // 0x5D — estimated
            // WotL jobs
            [94] = "Dark Knight",     // 0x5E — estimated
            [95] = "Onion Knight",    // 0x5F — estimated
        };

        /// <summary>
        /// Returns a display name for a unit using both nameId and jobId.
        /// For story characters, returns their name (e.g., "Ramza").
        /// For generic units, returns their job name (e.g., "Knight").
        /// Falls back to "Unit (nameId=X, job=Y)" if both lookups fail.
        /// </summary>
        public static string GetDisplayName(int nameId, int jobId)
        {
            // Try story character name first
            var charName = GetName(nameId);
            if (charName != null)
                return charName;

            // Fall back to job/class name
            var jobName = GetJobName(jobId);
            if (jobName != null)
                return jobName;

            return $"Unit (nameId={nameId}, job=0x{jobId:X2})";
        }
    }
}
