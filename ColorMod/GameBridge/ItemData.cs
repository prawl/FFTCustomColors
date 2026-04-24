using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Static lookup table for FFT item IDs, names, types, and key stats.
    /// Data sourced from FFTPatcher (Glain/FFTPatcher) PSP/WotL binary data.
    ///
    /// IDs match FFTPatcher canonical offsets (0-315). Verified 2026-04-14 via
    /// live dumps of Ramza (Ragnarok=36, Escutcheon=143, Grand Helm=156,
    /// Maximillian=185, Bracer=218) and Kenrick (Chaos Blade=37, Kaiser
    /// Shield=141, Crystal Helm=154, Crystal Mail=182, Bracer=218) — every
    /// equipped ID read from roster +0x0E..+0x1A matched the FFTPatcher key
    /// for the displayed item name. An earlier version of this comment
    /// claimed a Game != FFTPatcher discrepancy; that was based on a wrong
    /// slot-offset assumption, not a real encoding difference.
    ///
    /// Equipment slot layout in roster (u16 LE each, 0xFF = empty):
    ///   +0x0E: Helm
    ///   +0x10: Body armor
    ///   +0x12: Accessory
    ///   +0x14: Right-hand weapon
    ///   +0x16: Left-hand weapon (dual-wield) / empty
    ///   +0x18: reserved / empty
    ///   +0x1A: Left-hand shield
    /// </summary>
    public record ItemInfo(
        int Id,
        string Name,
        string Type,           // "knife", "ninjablade", "sword", "knightsword", "katana", "axe",
                               // "rod", "staff", "flail", "gun", "crossbow", "bow", "instrument",
                               // "book", "polearm", "pole", "bag", "cloth", "throwing", "bomb",
                               // "fellsword", "shield", "helmet", "hat", "hairadornment",
                               // "armor", "clothing", "robe", "shoes", "armguard", "ring",
                               // "armlet", "cloak", "perfume", "liprouge", "chemistitem"
        int WeaponPower = 0,   // WP for weapons
        int Range = 0,         // weapon range (0=default melee)
        int WeaponEvade = 0,   // weapon evade %
        int PhysicalEvade = 0, // physical evade % for shields/cloaks
        int MagicEvade = 0,    // magic evade % for shields/cloaks
        int HpBonus = 0,       // HP bonus for armor/helm/hat
        int MpBonus = 0,       // MP bonus for armor/helm/hat
        // Extended info-panel fields — mirror the 3 pages of the in-game
        // item inspector (Outfitter Try-then-Buy, EquipmentAndAbilities
        // `R` toggle). Populated for top hero items and anything else
        // where the effect is decision-changing. See TODO §0.
        // All nullable — empty means "no entry on that page" (not "unknown").
        string? AttributeBonuses = null,   // "PA+1", "MA+2", "Speed+1", "PA+1, MA+1" — additive stat mods.
        string? EquipmentEffects = null,   // "Permanent Shell", "Auto-Haste", "Immune Blindness", "Auto-Reraise". Passive effects while equipped.
        string? AttackEffects = null,      // "On hit: adds Petrify (25%)", "Drain HP on hit" — weapon-only.
        bool CanDualWield = false,         // Weapon can be paired with another via Dual Wield support.
        bool CanWieldTwoHanded = false,    // Weapon is compatible with Doublehand support (double ATK on basic attacks).
        string? Element = null             // "Holy", "Ice", "Fire", "Lightning", "Dark" — for weapons with an elemental property.
    );

    public static class ItemData
    {
        private static readonly string[] SubTypeNames = {
            "none", "knife", "ninjablade", "sword", "knightsword", "katana", "axe",
            "rod", "staff", "flail", "gun", "crossbow", "bow", "instrument",
            "book", "polearm", "pole", "bag", "cloth", "shield", "helmet",
            "hat", "hairadornment", "armor", "clothing", "robe", "shoes",
            "armguard", "ring", "armlet", "cloak", "perfume", "throwing",
            "bomb", "chemistitem", "fellsword", "liprouge"
        };

        public static readonly Dictionary<int, ItemInfo> Items = new()
        {
            // ============================================================
            // KNIVES (IDs 1-10)
            // ============================================================
            [1] = new(1, "Dagger", "knife", WeaponPower: 3, Range: 1, WeaponEvade: 5),
            [2] = new(2, "Mythril Knife", "knife", WeaponPower: 4, Range: 1, WeaponEvade: 5),
            [3] = new(3, "Blind Knife", "knife", WeaponPower: 4, Range: 1, WeaponEvade: 5),
            [4] = new(4, "Mage Masher", "knife", WeaponPower: 4, Range: 1, WeaponEvade: 5),
            [5] = new(5, "Platinum Dagger", "knife", WeaponPower: 5, Range: 1, WeaponEvade: 10),
            [6] = new(6, "Main Gauche", "knife", WeaponPower: 6, Range: 1, WeaponEvade: 40),
            [7] = new(7, "Orichalcum Dirk", "knife", WeaponPower: 7, Range: 1, WeaponEvade: 5),
            [8] = new(8, "Assassin's Dagger", "knife", WeaponPower: 7, Range: 1, WeaponEvade: 5),
            [9] = new(9, "Air Knife", "knife", WeaponPower: 10, Range: 1, WeaponEvade: 5),
            [10] = new(10, "Zwill Straightblade", "knife", WeaponPower: 12, Range: 1, WeaponEvade: 10),

            // ============================================================
            // NINJA BLADES (IDs 11-18)
            // ============================================================
            [11] = new(11, "Ninja Blade", "ninjablade", WeaponPower: 8, Range: 1, WeaponEvade: 5),
            [12] = new(12, "Kunai", "ninjablade", WeaponPower: 9, Range: 1, WeaponEvade: 5),
            [13] = new(13, "Kodachi", "ninjablade", WeaponPower: 10, Range: 1, WeaponEvade: 5),
            [14] = new(14, "Ninja Longblade", "ninjablade", WeaponPower: 12, Range: 1, WeaponEvade: 5),
            [15] = new(15, "Spellbinder", "ninjablade", WeaponPower: 13, Range: 1, WeaponEvade: 5),
            [16] = new(16, "Sasuke's Blade", "ninjablade", WeaponPower: 14, Range: 1, WeaponEvade: 15),
            [17] = new(17, "Iga Blade", "ninjablade", WeaponPower: 15, Range: 1, WeaponEvade: 10),
            [18] = new(18, "Koga Blade", "ninjablade", WeaponPower: 15, Range: 1, WeaponEvade: 5),

            // ============================================================
            // SWORDS (IDs 19-32)
            // ============================================================
            [19] = new(19, "Broadsword", "sword", WeaponPower: 4, Range: 1, WeaponEvade: 5),
            [20] = new(20, "Longsword", "sword", WeaponPower: 5, Range: 1, WeaponEvade: 10),
            [21] = new(21, "Iron Sword", "sword", WeaponPower: 6, Range: 1, WeaponEvade: 5),
            [22] = new(22, "Mythril Sword", "sword", WeaponPower: 7, Range: 1, WeaponEvade: 8),
            [23] = new(23, "Blood Sword", "sword", WeaponPower: 8, Range: 1, WeaponEvade: 5),
            [24] = new(24, "Coral Sword", "sword", WeaponPower: 8, Range: 1, WeaponEvade: 5),
            [25] = new(25, "Ancient Sword", "sword", WeaponPower: 9, Range: 1, WeaponEvade: 5),
            [26] = new(26, "Sleep Blade", "sword", WeaponPower: 9, Range: 1, WeaponEvade: 5),
            [27] = new(27, "Platinum Sword", "sword", WeaponPower: 12, Range: 1, WeaponEvade: 10),
            [28] = new(28, "Diamond Sword", "sword", WeaponPower: 10, Range: 1, WeaponEvade: 10),
            [29] = new(29, "Icebrand", "sword", WeaponPower: 13, Range: 1, WeaponEvade: 10),
            [30] = new(30, "Runeblade", "sword", WeaponPower: 14, Range: 1, WeaponEvade: 15),
            [31] = new(31, "Nagnarok", "sword", WeaponPower: 1, Range: 1, WeaponEvade: 50),
            [32] = new(32, "Materia Blade", "sword", WeaponPower: 10, Range: 1, WeaponEvade: 10,
                EquipmentEffects: "Required for Cloud's Limit skillset",
                CanDualWield: true, CanWieldTwoHanded: true),

            // ============================================================
            // KNIGHT'S SWORDS (IDs 33-37)
            // ============================================================
            [33] = new(33, "Defender", "knightsword", WeaponPower: 16, Range: 1, WeaponEvade: 60,
                EquipmentEffects: "Strengthens Wind, Fire, Lightning, Water, Earth, Ice elements",
                CanDualWield: true, CanWieldTwoHanded: true),
            [34] = new(34, "Save the Queen", "knightsword", WeaponPower: 18, Range: 1, WeaponEvade: 30,
                EquipmentEffects: "Auto-Protect",
                CanDualWield: true, CanWieldTwoHanded: true),
            [35] = new(35, "Excalibur", "knightsword", WeaponPower: 21, Range: 1, WeaponEvade: 35,
                EquipmentEffects: "Auto-Haste",
                Element: "Holy",
                CanDualWield: true, CanWieldTwoHanded: true),
            [36] = new(36, "Ragnarok", "knightsword", WeaponPower: 24, Range: 1, WeaponEvade: 20,
                EquipmentEffects: "Auto-Shell",
                CanDualWield: true, CanWieldTwoHanded: true),
            [37] = new(37, "Chaos Blade", "knightsword", WeaponPower: 40, Range: 1, WeaponEvade: 20,
                EquipmentEffects: "Auto-Regen",
                AttackEffects: "On hit: chance to add Stone",
                CanDualWield: true, CanWieldTwoHanded: true),

            // ============================================================
            // KATANAS (IDs 38-47)
            // ============================================================
            [38] = new(38, "Ashura", "katana", WeaponPower: 7, Range: 1, WeaponEvade: 15),
            [39] = new(39, "Kotetsu", "katana", WeaponPower: 8, Range: 1, WeaponEvade: 15),
            [40] = new(40, "Osafune", "katana", WeaponPower: 9, Range: 1, WeaponEvade: 15),
            [41] = new(41, "Murasame", "katana", WeaponPower: 10, Range: 1, WeaponEvade: 15),
            [42] = new(42, "Ama-no-Murakumo", "katana", WeaponPower: 11, Range: 1, WeaponEvade: 15),
            [43] = new(43, "Kiyomori", "katana", WeaponPower: 12, Range: 1, WeaponEvade: 15),
            [44] = new(44, "Muramasa", "katana", WeaponPower: 14, Range: 1, WeaponEvade: 15),
            [45] = new(45, "Kiku-ichimonji", "katana", WeaponPower: 15, Range: 1, WeaponEvade: 15),
            [46] = new(46, "Masamune", "katana", WeaponPower: 18, Range: 1, WeaponEvade: 15),
            [47] = new(47, "Chirijiraden", "katana", WeaponPower: 25, Range: 1, WeaponEvade: 15),

            // ============================================================
            // AXES (IDs 48-50)
            // ============================================================
            [48] = new(48, "Battle Axe", "axe", WeaponPower: 9, Range: 1),
            [49] = new(49, "Giant's Axe", "axe", WeaponPower: 12, Range: 1),
            [50] = new(50, "Slasher", "axe", WeaponPower: 16, Range: 1),

            // ============================================================
            // RODS (IDs 51-58)
            // ============================================================
            [51] = new(51, "Rod", "rod", WeaponPower: 3, Range: 1, WeaponEvade: 20),
            [52] = new(52, "Thunder Rod", "rod", WeaponPower: 3, Range: 1, WeaponEvade: 20),
            [53] = new(53, "Flame Rod", "rod", WeaponPower: 3, Range: 1, WeaponEvade: 20),
            [54] = new(54, "Ice Rod", "rod", WeaponPower: 3, Range: 1, WeaponEvade: 20),
            [55] = new(55, "Poison Rod", "rod", WeaponPower: 3, Range: 1, WeaponEvade: 20),
            [56] = new(56, "Wizard's Rod", "rod", WeaponPower: 4, Range: 1, WeaponEvade: 20),
            [57] = new(57, "Dragon Rod", "rod", WeaponPower: 5, Range: 1, WeaponEvade: 20),
            [58] = new(58, "Rod of Faith", "rod", WeaponPower: 5, Range: 1, WeaponEvade: 20),

            // ============================================================
            // STAVES (IDs 59-66)
            // ============================================================
            [59] = new(59, "Oak Staff", "staff", WeaponPower: 3, Range: 1, WeaponEvade: 15),
            [60] = new(60, "White Staff", "staff", WeaponPower: 3, Range: 1, WeaponEvade: 15),
            [61] = new(61, "Healing Staff", "staff", WeaponPower: 4, Range: 1, WeaponEvade: 15),
            [62] = new(62, "Serpent Staff", "staff", WeaponPower: 5, Range: 1, WeaponEvade: 15),
            [63] = new(63, "Mage's Staff", "staff", WeaponPower: 4, Range: 1, WeaponEvade: 15),
            [64] = new(64, "Golden Staff", "staff", WeaponPower: 6, Range: 1, WeaponEvade: 15),
            [65] = new(65, "Zeus Mace", "staff", WeaponPower: 6, Range: 1, WeaponEvade: 15),
            [66] = new(66, "Staff of the Magi", "staff", WeaponPower: 7, Range: 1, WeaponEvade: 15),

            // ============================================================
            // FLAILS (IDs 67-70)
            // ============================================================
            [67] = new(67, "Iron Flail", "flail", WeaponPower: 9, Range: 1),
            [68] = new(68, "Flame Mace", "flail", WeaponPower: 11, Range: 1),
            [69] = new(69, "Morning Star", "flail", WeaponPower: 16, Range: 1),
            [70] = new(70, "Scorpion Tail", "flail", WeaponPower: 23, Range: 1),

            // ============================================================
            // GUNS (IDs 71-76) -- Range 8
            // ============================================================
            [71] = new(71, "Romandan Pistol", "gun", WeaponPower: 6, Range: 8, WeaponEvade: 5),
            [72] = new(72, "Mythril Gun", "gun", WeaponPower: 8, Range: 8, WeaponEvade: 5),
            [73] = new(73, "Stoneshooter", "gun", WeaponPower: 16, Range: 8, WeaponEvade: 5),
            [74] = new(74, "Glacial Gun", "gun", WeaponPower: 20, Range: 8, WeaponEvade: 5),
            [75] = new(75, "Blaze Gun", "gun", WeaponPower: 21, Range: 8, WeaponEvade: 5),
            [76] = new(76, "Blaster", "gun", WeaponPower: 22, Range: 8, WeaponEvade: 5),

            // ============================================================
            // CROSSBOWS (IDs 77-82) -- Range 4
            // ============================================================
            [77] = new(77, "Bowgun", "crossbow", WeaponPower: 3, Range: 4, WeaponEvade: 5),
            [78] = new(78, "Knightslayer", "crossbow", WeaponPower: 3, Range: 4, WeaponEvade: 5),
            [79] = new(79, "Crossbow", "crossbow", WeaponPower: 4, Range: 4, WeaponEvade: 5),
            [80] = new(80, "Poison Bow", "crossbow", WeaponPower: 4, Range: 4, WeaponEvade: 5),
            [81] = new(81, "Hunting Bow", "crossbow", WeaponPower: 6, Range: 4, WeaponEvade: 5),
            [82] = new(82, "Gastrophetes", "crossbow", WeaponPower: 10, Range: 4, WeaponEvade: 5),

            // ============================================================
            // BOWS (IDs 83-91) -- Range 5
            // ============================================================
            [83] = new(83, "Longbow", "bow", WeaponPower: 4, Range: 5),
            [84] = new(84, "Silver Bow", "bow", WeaponPower: 5, Range: 5),
            [85] = new(85, "Ice Bow", "bow", WeaponPower: 5, Range: 5),
            [86] = new(86, "Lightning Bow", "bow", WeaponPower: 6, Range: 5),
            [87] = new(87, "Windslash Bow", "bow", WeaponPower: 8, Range: 5),
            [88] = new(88, "Mythril Bow", "bow", WeaponPower: 7, Range: 5),
            [89] = new(89, "Artemis Bow", "bow", WeaponPower: 10, Range: 5),
            [90] = new(90, "Yoichi Bow", "bow", WeaponPower: 12, Range: 5),
            [91] = new(91, "Perseus Bow", "bow", WeaponPower: 16, Range: 5),

            // ============================================================
            // INSTRUMENTS (IDs 92-94) -- Range 3
            // ============================================================
            [92] = new(92, "Lamia's Harp", "instrument", WeaponPower: 10, Range: 3, WeaponEvade: 10),
            [93] = new(93, "Bloodstring Harp", "instrument", WeaponPower: 13, Range: 3, WeaponEvade: 10),
            [94] = new(94, "Faerie Harp", "instrument", WeaponPower: 15, Range: 3, WeaponEvade: 10),

            // ============================================================
            // BOOKS (IDs 95-98) -- Range 3
            // ============================================================
            [95] = new(95, "Battle Folio", "book", WeaponPower: 7, Range: 3, WeaponEvade: 15),
            [96] = new(96, "Bestiary", "book", WeaponPower: 8, Range: 3, WeaponEvade: 15),
            [97] = new(97, "Papyrus Codex", "book", WeaponPower: 9, Range: 3, WeaponEvade: 15),
            [98] = new(98, "Omnilex", "book", WeaponPower: 11, Range: 3, WeaponEvade: 15),

            // ============================================================
            // POLEARMS (IDs 99-106) -- Range 2
            // ============================================================
            [99] = new(99, "Javelin", "polearm", WeaponPower: 8, Range: 2, WeaponEvade: 10),
            [100] = new(100, "Spear", "polearm", WeaponPower: 9, Range: 2, WeaponEvade: 10),
            [101] = new(101, "Mythril Spear", "polearm", WeaponPower: 10, Range: 2, WeaponEvade: 10),
            [102] = new(102, "Partisan", "polearm", WeaponPower: 11, Range: 2, WeaponEvade: 10),
            [103] = new(103, "Obelisk", "polearm", WeaponPower: 12, Range: 2, WeaponEvade: 10),
            [104] = new(104, "Holy Lance", "polearm", WeaponPower: 14, Range: 2, WeaponEvade: 10),
            [105] = new(105, "Dragon Whisker", "polearm", WeaponPower: 17, Range: 2, WeaponEvade: 10),
            [106] = new(106, "Javelin (Strong)", "polearm", WeaponPower: 30, Range: 2, WeaponEvade: 10),

            // ============================================================
            // POLES (IDs 107-114) -- Range 2
            // ============================================================
            [107] = new(107, "Cypress Pole", "pole", WeaponPower: 6, Range: 2, WeaponEvade: 20),
            [108] = new(108, "Battle Bamboo", "pole", WeaponPower: 7, Range: 2, WeaponEvade: 20),
            [109] = new(109, "Musk Pole", "pole", WeaponPower: 8, Range: 2, WeaponEvade: 20),
            [110] = new(110, "Iron Fan", "pole", WeaponPower: 9, Range: 2, WeaponEvade: 20),
            [111] = new(111, "Gokuu Pole", "pole", WeaponPower: 10, Range: 2, WeaponEvade: 20),
            [112] = new(112, "Ivory Pole", "pole", WeaponPower: 11, Range: 2, WeaponEvade: 20),
            [113] = new(113, "Eight-fluted Pole", "pole", WeaponPower: 12, Range: 2, WeaponEvade: 20),
            [114] = new(114, "Whale Whisker", "pole", WeaponPower: 16, Range: 2, WeaponEvade: 20),

            // ============================================================
            // BAGS (IDs 115-118)
            // ============================================================
            [115] = new(115, "Croakadile Bag", "bag", WeaponPower: 10, Range: 1),
            [116] = new(116, "Fallingstar Bag", "bag", WeaponPower: 20, Range: 1),
            [117] = new(117, "Pantherskin Bag", "bag", WeaponPower: 12, Range: 1),
            [118] = new(118, "Hydrascale Bag", "bag", WeaponPower: 14, Range: 1),

            // ============================================================
            // CLOTHS (IDs 119-121) -- Range 2
            // ============================================================
            [119] = new(119, "Damask Cloth", "cloth", WeaponPower: 8, Range: 2, WeaponEvade: 50),
            [120] = new(120, "Cashmere", "cloth", WeaponPower: 10, Range: 2, WeaponEvade: 50),
            [121] = new(121, "Wyrmweave Silk", "cloth", WeaponPower: 15, Range: 2, WeaponEvade: 50),

            // ============================================================
            // THROWING (IDs 122-124)
            // ============================================================
            [122] = new(122, "Shuriken", "throwing", WeaponPower: 4),
            [123] = new(123, "Fuma Shuriken", "throwing", WeaponPower: 7),
            [124] = new(124, "Yagyu Darkrood", "throwing", WeaponPower: 10),

            // ============================================================
            // BOMBS (IDs 125-127)
            // ============================================================
            [125] = new(125, "Flameburst Bomb", "bomb", WeaponPower: 8),
            [126] = new(126, "Snowmelt Bomb", "bomb", WeaponPower: 8),
            [127] = new(127, "Spark Bomb", "bomb", WeaponPower: 8),

            // ============================================================
            // SHIELDS (IDs 128-143)
            // ============================================================
            [128] = new(128, "Escutcheon", "shield", PhysicalEvade: 10, MagicEvade: 3),
            [129] = new(129, "Buckler", "shield", PhysicalEvade: 13, MagicEvade: 3),
            [130] = new(130, "Bronze Shield", "shield", PhysicalEvade: 16),
            [131] = new(131, "Round Shield", "shield", PhysicalEvade: 19),
            [132] = new(132, "Mythril Shield", "shield", PhysicalEvade: 22, MagicEvade: 5),
            [133] = new(133, "Golden Shield", "shield", PhysicalEvade: 25),
            [134] = new(134, "Ice Shield", "shield", PhysicalEvade: 28,
                EquipmentEffects: "Absorbs Ice; halves Fire; weak to Lightning"),
            [135] = new(135, "Flame Shield", "shield", PhysicalEvade: 31,
                EquipmentEffects: "Absorbs Fire; halves Ice; weak to Water"),
            [136] = new(136, "Aegis Shield", "shield", PhysicalEvade: 10, MagicEvade: 50,
                AttributeBonuses: "MA+1"),
            [137] = new(137, "Diamond Shield", "shield", PhysicalEvade: 34, MagicEvade: 15),
            [138] = new(138, "Platinum Shield", "shield", PhysicalEvade: 37, MagicEvade: 10),
            [139] = new(139, "Crystal Shield", "shield", PhysicalEvade: 40, MagicEvade: 15),
            [140] = new(140, "Genji Shield", "shield", PhysicalEvade: 43),
            [141] = new(141, "Kaiser Shield", "shield", PhysicalEvade: 46, MagicEvade: 20,
                EquipmentEffects: "Strengthens Fire, Ice, Lightning"),
            [142] = new(142, "Venetian Shield", "shield", PhysicalEvade: 50, MagicEvade: 25),
            [143] = new(143, "Escutcheon (strong)", "shield", PhysicalEvade: 75, MagicEvade: 50,
                EquipmentEffects: "Best-in-slot defense (rare treasure)"),

            // ============================================================
            // HELMETS (IDs 144-156)
            // ============================================================
            [144] = new(144, "Leather Helm", "helmet", HpBonus: 10),
            [145] = new(145, "Bronze Helm", "helmet", HpBonus: 20),
            [146] = new(146, "Iron Helm", "helmet", HpBonus: 30),
            [147] = new(147, "Barbut", "helmet", HpBonus: 40),
            [148] = new(148, "Mythril Helm", "helmet", HpBonus: 50),
            [149] = new(149, "Golden Helm", "helmet", HpBonus: 60),
            [150] = new(150, "Close Helmet", "helmet", HpBonus: 70),
            [151] = new(151, "Diamond Helm", "helmet", HpBonus: 80),
            [152] = new(152, "Platinum Helm", "helmet", HpBonus: 90),
            [153] = new(153, "Circlet", "helmet", HpBonus: 100),
            [154] = new(154, "Crystal Helm", "helmet", HpBonus: 120),
            [155] = new(155, "Genji Helm", "helmet", HpBonus: 130),
            [156] = new(156, "Grand Helm", "helmet", HpBonus: 150),

            // ============================================================
            // HATS (IDs 157-168)
            // ============================================================
            [157] = new(157, "Leather Cap", "hat", HpBonus: 8),
            [158] = new(158, "Plumed Hat", "hat", HpBonus: 16, MpBonus: 5),
            [159] = new(159, "Red Hood", "hat", HpBonus: 24, MpBonus: 8),
            [160] = new(160, "Headgear", "hat", HpBonus: 32),
            [161] = new(161, "Wizard's Hat", "hat", HpBonus: 40, MpBonus: 12),
            [162] = new(162, "Green Beret", "hat", HpBonus: 48),
            [163] = new(163, "Headband", "hat", HpBonus: 56),
            [164] = new(164, "Celebrant's Miter", "hat", HpBonus: 64, MpBonus: 20),
            [165] = new(165, "Black Cowl", "hat", HpBonus: 72),
            [166] = new(166, "Gold Hairpin", "hat", HpBonus: 80, MpBonus: 50,
                EquipmentEffects: "Best hat for casters (MP+50)"),
            [167] = new(167, "Lambent Hat", "hat", HpBonus: 88, MpBonus: 15),
            [168] = new(168, "Thief's Cap", "hat", HpBonus: 100,
                AttributeBonuses: "Speed+2"),

            // ============================================================
            // HAIR ADORNMENTS (IDs 169-171)
            // ============================================================
            [169] = new(169, "Cachusha", "hairadornment", HpBonus: 20),
            [170] = new(170, "Barette", "hairadornment", HpBonus: 20),
            [171] = new(171, "Ribbon", "hairadornment", HpBonus: 10,
                EquipmentEffects: "Immune to most status ailments"),

            // ============================================================
            // ARMOR (IDs 172-185)
            // ============================================================
            [172] = new(172, "Leather Armor", "armor", HpBonus: 10),
            [173] = new(173, "Linen Cuirass", "armor", HpBonus: 20),
            [174] = new(174, "Bronze Armor", "armor", HpBonus: 30),
            [175] = new(175, "Chainmail", "armor", HpBonus: 40),
            [176] = new(176, "Mythril Armor", "armor", HpBonus: 50),
            [177] = new(177, "Plate Mail", "armor", HpBonus: 60),
            [178] = new(178, "Golden Armor", "armor", HpBonus: 70),
            [179] = new(179, "Diamond Armor", "armor", HpBonus: 80),
            [180] = new(180, "Platinum Armor", "armor", HpBonus: 90),
            [181] = new(181, "Carabineer Mail", "armor", HpBonus: 100),
            [182] = new(182, "Crystal Mail", "armor", HpBonus: 110),
            [183] = new(183, "Genji Armor", "armor", HpBonus: 150,
                EquipmentEffects: "Rare steal from Elmdore"),
            [184] = new(184, "Mirror Mail", "armor", HpBonus: 130),
            [185] = new(185, "Maximillian", "armor", HpBonus: 200),

            // ============================================================
            // CLOTHING (IDs 186-199)
            // ============================================================
            [186] = new(186, "Clothing", "clothing", HpBonus: 5),
            [187] = new(187, "Leather Clothing", "clothing", HpBonus: 10),
            [188] = new(188, "Leather Plate", "clothing", HpBonus: 18),
            [189] = new(189, "Ringmail", "clothing", HpBonus: 24),
            [190] = new(190, "Mythril Vest", "clothing", HpBonus: 30),
            [191] = new(191, "Adamant Vest", "clothing", HpBonus: 36),
            [192] = new(192, "Wizard Clothing", "clothing", HpBonus: 42, MpBonus: 15),
            [193] = new(193, "Brigandine", "clothing", HpBonus: 50),
            [194] = new(194, "Jujitsu Gi", "clothing", HpBonus: 60),
            [195] = new(195, "Power Garb", "clothing", HpBonus: 70),
            [196] = new(196, "Gaia Gear", "clothing", HpBonus: 85, MpBonus: 10),
            [197] = new(197, "Ninja Gear", "clothing", HpBonus: 20),
            [198] = new(198, "Black Garb", "clothing", HpBonus: 100),
            [199] = new(199, "Rubber Suit", "clothing", HpBonus: 150, MpBonus: 30,
                EquipmentEffects: "Negates Lightning"),

            // ============================================================
            // ROBES (IDs 200-207)
            // ============================================================
            [200] = new(200, "Hempen Robe", "robe", HpBonus: 10, MpBonus: 10),
            [201] = new(201, "Silken Robe", "robe", HpBonus: 20, MpBonus: 16),
            [202] = new(202, "Wizard's Robe", "robe", HpBonus: 30, MpBonus: 22),
            [203] = new(203, "Chameleon Robe", "robe", HpBonus: 40, MpBonus: 28),
            [204] = new(204, "White Robe", "robe", HpBonus: 50, MpBonus: 34,
                EquipmentEffects: "Strengthens Holy element"),
            [205] = new(205, "Black Robe", "robe", HpBonus: 60, MpBonus: 30,
                EquipmentEffects: "Strengthens Fire, Ice, Lightning"),
            [206] = new(206, "Luminous Robe", "robe", HpBonus: 75, MpBonus: 50),
            [207] = new(207, "Lordly Robe", "robe", HpBonus: 100, MpBonus: 80,
                EquipmentEffects: "Auto-Protect + Auto-Shell"),

            // ============================================================
            // SHOES (IDs 208-214)
            // ============================================================
            [208] = new(208, "Battle Boots", "shoes",
                AttributeBonuses: "Move+1"),
            [209] = new(209, "Spiked Boots", "shoes",
                AttributeBonuses: "Jump+1"),
            [210] = new(210, "Germinas Boots", "shoes",
                AttributeBonuses: "Move+1, Jump+1"),
            [211] = new(211, "Rubber Boots", "shoes",
                EquipmentEffects: "Immune Don't Move, Lightning"),
            [212] = new(212, "Winged Boots", "shoes"),
            [213] = new(213, "Hermes Shoes", "shoes",
                EquipmentEffects: "Auto-Haste"),
            [214] = new(214, "Red Shoes", "shoes",
                AttributeBonuses: "MA+1, Move+1"),

            // ============================================================
            // ARMGUARDS (IDs 215-218)
            // ============================================================
            [215] = new(215, "Power Gauntlet", "armguard",
                AttributeBonuses: "PA+1"),
            [216] = new(216, "Genji Glove", "armguard",
                AttributeBonuses: "PA+2, MA+2"),
            [217] = new(217, "Magepower Glove", "armguard",
                AttributeBonuses: "MA+2"),
            [218] = new(218, "Bracer", "armguard",
                AttributeBonuses: "PA+3"),

            // ============================================================
            // RINGS (IDs 219-222)
            // ============================================================
            [219] = new(219, "Reflect Ring", "ring",
                EquipmentEffects: "Auto-Reflect"),
            [220] = new(220, "Protect Ring", "ring"),
            [221] = new(221, "Magick Ring", "ring"),
            [222] = new(222, "Cursed Ring", "ring",
                AttributeBonuses: "PA+1, MA+1, Speed+1",
                EquipmentEffects: "Wearer becomes Undead (magic heals become damage)"),

            // ============================================================
            // ARMLETS (IDs 223-228)
            // ============================================================
            [223] = new(223, "Angel Ring", "armlet",
                EquipmentEffects: "Auto-Reraise; Immune Death, Blindness"),
            [224] = new(224, "Diamond Bracelet", "armlet"),
            [225] = new(225, "Jade Armlet", "armlet"),
            [226] = new(226, "Japa Mala", "armlet"),
            [227] = new(227, "Nu Khai Armband", "armlet",
                EquipmentEffects: "Immune Petrify, Stop"),
            [228] = new(228, "Guardian Bracelet", "armlet"),

            // ============================================================
            // CLOAKS (IDs 229-235)
            // ============================================================
            [229] = new(229, "Shoulder Cape", "cloak", PhysicalEvade: 10, MagicEvade: 10),
            [230] = new(230, "Leather Cloak", "cloak", PhysicalEvade: 15, MagicEvade: 15),
            [231] = new(231, "Mage's Cloak", "cloak", PhysicalEvade: 18, MagicEvade: 18),
            [232] = new(232, "Elven Cloak", "cloak", PhysicalEvade: 25, MagicEvade: 25),
            [233] = new(233, "Vampire Cape", "cloak", PhysicalEvade: 28, MagicEvade: 28),
            [234] = new(234, "Featherweave Cloak", "cloak", PhysicalEvade: 40, MagicEvade: 30),
            [235] = new(235, "Invisibility Cloak", "cloak", PhysicalEvade: 35),

            // ============================================================
            // PERFUMES (IDs 236-239)
            // ============================================================
            [236] = new(236, "Chantage", "perfume",
                EquipmentEffects: "Permanent Reraise + Regen (female only)"),
            [237] = new(237, "Cherche", "perfume"),
            [238] = new(238, "Septieme", "perfume"),
            [239] = new(239, "Sortilege", "perfume"),

            // ============================================================
            // CHEMIST ITEMS (IDs 240-253)
            // ============================================================
            [240] = new(240, "Potion", "chemistitem"),
            [241] = new(241, "Hi-Potion", "chemistitem"),
            [242] = new(242, "X-Potion", "chemistitem"),
            [243] = new(243, "Ether", "chemistitem"),
            [244] = new(244, "Hi-Ether", "chemistitem"),
            [245] = new(245, "Elixir", "chemistitem"),
            [246] = new(246, "Antidote", "chemistitem"),
            [247] = new(247, "Eye Drops", "chemistitem"),
            [248] = new(248, "Echo Herbs", "chemistitem"),
            [249] = new(249, "Maiden's Kiss", "chemistitem"),
            [250] = new(250, "Gold Needle", "chemistitem"),
            [251] = new(251, "Holy Water", "chemistitem"),
            [252] = new(252, "Remedy", "chemistitem"),
            [253] = new(253, "Phoenix Down", "chemistitem"),

            // ============================================================
            // IC REMASTER (Deluxe Edition) — IDs 256-260 live-verified 2026-04-23
            // by equipping each ID on Ramza's weapon slot and reading the game's
            // rendered name. IC renamed/replaced the PSP fell-sword block here.
            // 256: Materia Blade+ (was Chaosbringer in PSP)
            // 257-260: Deluxe bonus set (read off Ramza after claiming the
            //          Deluxe entitlement in the title menu).
            // Stats (WP, HP bonus, evade) not yet captured from the in-game
            // info panel — add when a stat-gathering pass is done on EqA.
            // ============================================================
            [256] = new(256, "Materia Blade+", "sword"),
            [257] = new(257, "Akademy Blade", "sword"),
            [258] = new(258, "Akademy Beret", "hat"),
            [259] = new(259, "Akademy Tunic", "clothing"),
            [260] = new(260, "Ring of Aptitude", "ring",
                EquipmentEffects: "JP Boost"),

            // ============================================================
            // PSP/WotL IDs 261-277 — REMOVED in IC Remaster (Deluxe Edition)
            // Each ID was probed 2026-04-23 by writing the u16 to a weapon
            // slot and opening PartyMenu. Result: all except 262 rendered as
            // "nothing equipped" (empty placeholder in the item table). ID
            // 262 crashes the game when PartyMenu attempts to render it —
            // it's been fully removed from the item table, not just emptied.
            // Keeping the PSP names below so tooling doesn't break on legacy
            // saves/references, but DO NOT write these IDs to equipment on
            // IC Deluxe — inventory writes silently drop, equipment writes
            // either render empty or crash. See feedback_invalid_item_id_crashes.
            // ============================================================
            [261] = new(261, "Moonblade [IC:unused]", "sword", WeaponPower: 20, Range: 1, WeaponEvade: 15),
            [262] = new(262, "Onion Sword [IC:CRASHES]", "sword", WeaponPower: 50, Range: 1, WeaponEvade: 15),
            [263] = new(263, "Ras Algethi [IC:unused]", "gun", WeaponPower: 12, Range: 8, WeaponEvade: 5),
            [264] = new(264, "Fomalhaut [IC:unused]", "gun", WeaponPower: 18, Range: 8, WeaponEvade: 5),
            [265] = new(265, "Francisca [IC:unused]", "axe", WeaponPower: 24, Range: 1),
            [266] = new(266, "Golden Axe [IC:unused]", "axe", WeaponPower: 30, Range: 1),
            [267] = new(267, "Orochi [IC:unused]", "ninjablade", WeaponPower: 20, Range: 1, WeaponEvade: 5),
            [268] = new(268, "Moonsilk Blade [IC:unused]", "ninjablade", WeaponPower: 26, Range: 1, WeaponEvade: 5),
            [269] = new(269, "Nirvana [IC:unused]", "staff", WeaponPower: 5, Range: 1, WeaponEvade: 15),
            [270] = new(270, "Dreamwaker [IC:unused]", "staff", WeaponPower: 5, Range: 1, WeaponEvade: 15),
            [271] = new(271, "Stardust Rod [IC:unused]", "rod", WeaponPower: 5, Range: 3, WeaponEvade: 20),
            [272] = new(272, "Crown Scepter [IC:unused]", "rod", WeaponPower: 5, Range: 1, WeaponEvade: 20),
            [273] = new(273, "Vesper [IC:unused]", "flail", WeaponPower: 36, Range: 1),
            [274] = new(274, "Sagittarius Bow [IC:unused]", "bow", WeaponPower: 24, Range: 5),
            [275] = new(275, "Durandal [IC:unused]", "knightsword", WeaponPower: 26, Range: 1, WeaponEvade: 40),
            [276] = new(276, "Gae Bolg [IC:unused]", "polearm", WeaponPower: 24, Range: 2, WeaponEvade: 15),
            [277] = new(277, "Gungnir [IC:unused]", "polearm", WeaponPower: 29, Range: 2, WeaponEvade: 15),

            // ============================================================
            // PSP/WotL EXCLUSIVE - SHIELDS (IDs 288-289)
            // ============================================================
            [288] = new(288, "Onion Shield", "shield", PhysicalEvade: 80, MagicEvade: 75),
            [289] = new(289, "Reverie Shield", "shield", PhysicalEvade: 50, MagicEvade: 25),

            // ============================================================
            // PSP/WotL EXCLUSIVE - HELMS/HATS (IDs 292-295)
            // ============================================================
            [292] = new(292, "Vanguard Helm", "helmet", HpBonus: 150, MpBonus: 20),
            [293] = new(293, "Onion Helm", "helmet", HpBonus: 200),
            [294] = new(294, "Acacia Hat", "hat", HpBonus: 120, MpBonus: 20),
            [295] = new(295, "Brass Coronet", "hat", HpBonus: 60, MpBonus: 100),

            // ============================================================
            // PSP/WotL EXCLUSIVE - ARMOR/CLOTHING/ROBES (IDs 300-305)
            // ============================================================
            [300] = new(300, "Grand Armor", "armor", HpBonus: 170),
            [301] = new(301, "Onion Armor", "armor", HpBonus: 250),
            [302] = new(302, "Minerva Bustier", "clothing", HpBonus: 120),
            [303] = new(303, "Mirage Vest", "clothing", HpBonus: 120,
                EquipmentEffects: "Auto-Reraise"),
            [304] = new(304, "Brave Suit", "clothing", HpBonus: 160, MpBonus: 40),
            [305] = new(305, "Sage's Robe", "robe", HpBonus: 120, MpBonus: 100),

            // ============================================================
            // PSP/WotL EXCLUSIVE - ACCESSORIES (IDs 308-313)
            // ============================================================
            [308] = new(308, "Gaius Caligae", "shoes"),
            [309] = new(309, "Brigand's Gloves", "armguard"),
            [310] = new(310, "Onion Gloves", "armguard"),
            [311] = new(311, "Empyreal Armband", "armlet"),
            [312] = new(312, "Tynar Rouge", "liprouge"),
            [313] = new(313, "Sage's Ring", "ring"),
        };

        /// <summary>
        /// Look up an item by its FFTPatcher offset ID.
        /// NOTE: IC Remaster may use different IDs - see class-level comment.
        /// </summary>
        public static ItemInfo? GetItem(int id) =>
            Items.TryGetValue(id, out var item) ? item : null;

        /// <summary>
        /// Look up an item by name (case-insensitive).
        /// Useful for reverse-lookups when the ID scheme is uncertain.
        /// </summary>
        public static ItemInfo? GetItemByName(string name)
        {
            foreach (var kvp in Items)
            {
                if (kvp.Value.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }
            return null;
        }

        /// <summary>
        /// Returns a human-readable equipment type label for display.
        /// </summary>
        public static string GetTypeLabel(string type) => type switch
        {
            "knife" => "Knife",
            "ninjablade" => "Ninja Blade",
            "sword" => "Sword",
            "knightsword" => "Knight's Sword",
            "katana" => "Katana",
            "axe" => "Axe",
            "rod" => "Rod",
            "staff" => "Staff",
            "flail" => "Flail",
            "gun" => "Gun",
            "crossbow" => "Crossbow",
            "bow" => "Bow",
            "instrument" => "Instrument",
            "book" => "Book",
            "polearm" => "Polearm",
            "pole" => "Pole",
            "bag" => "Bag",
            "cloth" => "Cloth",
            "throwing" => "Throwing",
            "bomb" => "Bomb",
            "fellsword" => "Fell Sword",
            "shield" => "Shield",
            "helmet" => "Helmet",
            "hat" => "Hat",
            "hairadornment" => "Hair Adornment",
            "armor" => "Armor",
            "clothing" => "Clothing",
            "robe" => "Robe",
            "shoes" => "Shoes",
            "armguard" => "Armguard",
            "ring" => "Ring",
            "armlet" => "Armlet",
            "cloak" => "Cloak",
            "perfume" => "Perfume",
            "liprouge" => "Lip Rouge",
            "chemistitem" => "Chemist Item",
            _ => type
        };

        /// <summary>
        /// Returns whether the item type is a weapon (hand-held offensive equipment).
        /// </summary>
        public static bool IsWeapon(string type) => type is
            "knife" or "ninjablade" or "sword" or "knightsword" or "katana" or
            "axe" or "rod" or "staff" or "flail" or "gun" or "crossbow" or
            "bow" or "instrument" or "book" or "polearm" or "pole" or
            "bag" or "cloth" or "fellsword";

        /// <summary>
        /// Returns the attack range for a weapon given its item ID.
        /// For ranged weapons (bows, guns, crossbows) returns their actual range.
        /// For melee weapons, non-weapons, empty slots, or unknown IDs returns 1.
        /// </summary>
        public static int GetAttackRange(int itemId)
        {
            if (itemId == 0xFF || itemId == 0xFFFF)
                return 1;
            if (!Items.TryGetValue(itemId, out var item))
                return 1;
            if (!IsWeapon(item.Type))
                return 1;
            return item.Range > 0 ? item.Range : 1;
        }

        /// <summary>
        /// Returns the attack range for a unit based on their equipment list.
        /// Finds the first weapon in the equipment slots and returns its range.
        /// Returns 1 (melee) if no weapon is found or equipment is null/empty.
        /// </summary>
        public static int GetWeaponRangeFromEquipment(List<int>? equipment)
        {
            if (equipment == null || equipment.Count == 0)
                return 1;
            foreach (var id in equipment)
            {
                if (!Items.TryGetValue(id, out var item))
                    continue;
                if (IsWeapon(item.Type))
                    return item.Range > 0 ? item.Range : 1;
            }
            return 1;
        }

        /// <summary>
        /// Returns the first weapon ItemInfo found in the equipment list, or
        /// null if unarmed / no known weapon. Used by the scan pipeline to
        /// surface the active unit's weapon name, element, and on-hit effect
        /// in the compact banner. S60.
        /// </summary>
        public static ItemInfo? GetEquippedWeapon(List<int>? equipment)
        {
            if (equipment == null || equipment.Count == 0) return null;
            foreach (var id in equipment)
            {
                if (!Items.TryGetValue(id, out var item)) continue;
                if (IsWeapon(item.Type)) return item;
            }
            return null;
        }

        /// <summary>
        /// Compose the short "weapon tag" shown in the active-unit banner:
        /// weapon name + optional on-hit effect. Returns empty string when
        /// no weapon is equipped or the equipment list is unknown.
        /// Examples:
        ///   "Chaos Blade onHit:chance to add Stone"
        ///   "Iron Flail"
        ///   "" (unarmed)
        /// </summary>
        public static string ComposeWeaponTag(List<int>? equipment)
        {
            var weapon = GetEquippedWeapon(equipment);
            if (weapon == null) return string.Empty;
            var effect = weapon.AttackEffects;
            if (!string.IsNullOrEmpty(effect))
            {
                // Strip a leading "On hit: " prefix if present — the tag format
                // already uses "onHit:" so the prefix would read as
                // "onHit:On hit: ..." (redundant).
                const string prefix = "On hit: ";
                if (effect!.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                    effect = effect.Substring(prefix.Length);
                return $"{weapon.Name} onHit:{effect}";
            }
            return weapon.Name;
        }

        /// <summary>
        /// Builds an ActionAbilityInfo for the basic Attack command using the
        /// equipped weapon's range. Falls back to range 1 for melee/unknown.
        /// </summary>
        public static ActionAbilityInfo BuildAttackAbilityInfo(List<int>? equipment)
        {
            int range = GetWeaponRangeFromEquipment(equipment);
            int minRange = 0;
            int vRange = 0;
            bool isRanged = false;
            // Ranged weapon minimum ranges:
            //   Guns: min 2 (can't hit adjacent)
            //   Bows: min 2 (can't hit adjacent)
            //   Crossbows: min 3, max 4
            if (equipment != null)
            {
                foreach (var id in equipment)
                {
                    if (Items.TryGetValue(id, out var item))
                    {
                        if (item.Type == "gun") { minRange = 2; isRanged = true; break; }
                        if (item.Type == "bow") { minRange = 2; isRanged = true; break; }
                        if (item.Type == "crossbow") { minRange = 3; isRanged = true; break; }
                    }
                }
            }
            // Ranged weapons have effectively unlimited vertical reach (arc
            // trajectory). Melee weapons (VR=0) fall back to caster Jump in
            // AbilityTargetCalculator, which correctly caps melee reach.
            // Session 50 root-cause: VR=0 on bows was the source of the
            // session-48 12-vs-18 attack-tile shortfall at Zeklaus — enemies
            // on elevated tiles were rejected by the jump-based zDelta filter.
            if (isRanged) vRange = 99;
            return new ActionAbilityInfo(
                ActionAbilityLookup.ATTACK_ID, "Attack", 0,
                range.ToString(), vRange, 1, 0, "enemy",
                "Attacks with the equipped weapon, or bare fists if no weapon is equipped.",
                MinRange: minRange);
        }

        /// <summary>
        /// Returns whether the item type is protective equipment (head/body/accessory).
        /// </summary>
        public static bool IsEquipment(string type) => type is
            "shield" or "helmet" or "hat" or "hairadornment" or
            "armor" or "clothing" or "robe" or "shoes" or
            "armguard" or "ring" or "armlet" or "cloak" or "perfume" or "liprouge";
    }
}
