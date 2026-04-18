using System.Collections.Generic;
using System.Linq;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Static buy-price lookup for items sold in Outfitters. Sourced from
    /// <c>FFTHandsFree/SHOP_ITEMS.md</c> (Sal Ghidos end-game stock, which
    /// is the largest published list). The source dictionary is keyed by
    /// <b>item name</b> (not ID) so we don't have to guess FFTPatcher IDs.
    /// At static initialization we resolve each name to an ItemData.Items
    /// entry and build an id→price lookup. If a name doesn't match any
    /// ItemData entry it's silently dropped — these get logged-once on
    /// first use via <see cref="UnresolvedNames"/> for diagnosis.
    ///
    /// Items NOT in this table are either:
    /// <list type="bullet">
    /// <item>Story drops / unique equipment (Ragnarok, Excalibur, Save the Queen)</item>
    /// <item>IC-remaster-exclusive items we haven't mapped to ItemData yet</item>
    /// <item>Quest items that aren't sold in any shop</item>
    /// </list>
    /// Callers should treat missing entries as "no buy price" and render a
    /// placeholder (e.g. <c>sell=?</c>) rather than omitting the item.
    ///
    /// Sell price in FFT is half the buy price (integer division). Use
    /// <see cref="GetSellPrice"/> for the sell side; it encapsulates the
    /// /2 formula and lets us override specific items later if a research
    /// session proves the formula varies per category.
    ///
    /// This is a stopgap until we locate the in-memory price table
    /// (previous sessions searched and found the master ID list at
    /// 0x5D9B52C0 but the price array is still unknown). When that lands,
    /// this static table becomes the fallback path.
    /// </summary>
    public static class ItemPrices
    {
        /// <summary>
        /// Ground-truth sell prices keyed by ItemData name. When populated,
        /// these OVERRIDE the buy/2 estimate in <see cref="GetSellPrice"/>.
        /// Captured live from the game's Outfitter Sell screen (Sale Price
        /// column), so these match what the game actually pays you.
        ///
        /// Why this exists: buy/2 is inaccurate (live-verified 2026-04-15
        /// that Dagger shows 50 in-game but we compute 100). No consistent
        /// formula covers all items. Populating this table incrementally is
        /// the simplest path to ground-truth pricing without a memory hunt.
        ///
        /// Add entries as you verify them from the game UI. Names must match
        /// ItemData entries exactly (same resolver as BuyPricesByName) —
        /// UnresolvedNames catches typos at test time.
        /// </summary>
        private static readonly Dictionary<string, int> SellPricesByName = new()
        {
            // Live-verified sell prices from Outfitter Sell screen captures.
            // Grown incrementally as cities are visited — session 33 (Gariland)
            // added 7 daggers/swords, session 38 (Yardrow) added 5 mid-tier
            // swords. Sell/buy ratios are wildly inconsistent (9-19% for the
            // Yardrow sword set) so buy/2 is not a usable default; keep
            // extending this table empirically.
            //
            // Live-verified starting set (Gariland Outfitter Sell, 2026-04-15):
            ["Dagger"]             = 50,
            ["Mythril Knife"]      = 250,
            ["Blind Knife"]        = 400,
            ["Mage Masher"]        = 750,
            ["Assassin's Dagger"]  = 2_500,
            ["Broadsword"]         = 100,
            ["Longsword"]          = 250,
            // Live-verified expansion (Yardrow Outfitter Sell, session 38).
            // Sell/buy ratios here are wildly inconsistent (9-19% of buy) —
            // confirms there's no simple sell-price formula. Keep extending
            // this table rather than trying to derive one.
            ["Iron Sword"]         = 450,   // buy 4_000 → 11% ratio
            ["Mythril Sword"]      = 800,   // buy 5_000 → 16%
            ["Blood Sword"]        = 1_250, // buy 8_000 → 15%
            ["Coral Sword"]        = 900,   // buy 10_000 → 9%
            ["Ancient Sword"]      = 2_500, // buy 13_000 → 19%
        };

        /// <summary>
        /// Buy prices keyed by EXACT ItemData name. The name must match
        /// the string used in <see cref="ItemData.Items"/> — comparisons
        /// are case-sensitive. Add new entries here when extending coverage.
        /// </summary>
        private static readonly Dictionary<string, int> BuyPricesByName = new()
        {
            // --- Knives / Daggers ---
            ["Dagger"]            = 200,
            ["Mythril Knife"]     = 300,
            ["Blind Knife"]       = 400,
            ["Mage Masher"]       = 600,
            ["Platinum Dagger"]   = 800,
            ["Main Gauche"]       = 1_000,
            ["Orichalcum Dirk"]   = 3_000,
            ["Assassin's Dagger"] = 6_000,
            ["Air Knife"]         = 10_000,

            // --- Ninja Blades ---
            ["Ninja Blade"]      = 3_000,
            ["Kunai"]            = 5_000,
            ["Kodachi"]          = 7_000,
            ["Ninja Longblade"]  = 10_000,
            ["Spellbinder"]      = 16_000,

            // --- Swords ---
            ["Broadsword"]     = 1_500,
            ["Longsword"]      = 2_500,
            ["Iron Sword"]     = 4_000,
            ["Mythril Sword"]  = 5_000,
            ["Blood Sword"]    = 8_000,
            ["Coral Sword"]    = 10_000,
            ["Ancient Sword"]  = 13_000,
            ["Sleep Blade"]    = 16_000,
            ["Platinum Sword"] = 20_000,
            ["Diamond Sword"]  = 25_000,
            ["Icebrand"]       = 30_000,
            ["Runeblade"]      = 35_000,

            // --- Katanas ---
            ["Ashura"]          = 1_600,
            ["Kotetsu"]         = 3_000,
            ["Osafune"]         = 5_000,    // shop list calls this "Bizen Osafune"
            ["Murasame"]        = 7_000,
            ["Ama-no-Murakumo"] = 8_000,    // shop list calls this "Ame-no-Murakumo"
            ["Kiyomori"]        = 10_000,
            ["Muramasa"]        = 15_000,
            ["Kiku-ichimonji"]  = 22_000,

            // --- Axes ---
            ["Battle Axe"]   = 1_500,
            ["Giant's Axe"]  = 3_000,

            // --- Rods ---
            ["Rod"]          = 200,
            ["Thunder Rod"]  = 400,
            ["Flame Rod"]    = 400,
            ["Ice Rod"]      = 400,
            ["Poison Rod"]   = 500,
            ["Wizard's Rod"] = 8_000,

            // --- Staves ---
            ["Oak Staff"]     = 120,
            ["White Staff"]   = 800,
            ["Serpent Staff"] = 2_200,
            ["Mage's Staff"]  = 4_000,
            ["Golden Staff"]  = 7_000,

            // --- Flails ---
            ["Iron Flail"]     = 1_200,
            ["Flame Mace"]     = 4_000,    // shop list calls this "Flail of Flame"
            ["Morning Star"]   = 9_000,

            // --- Books ---
            ["Battle Folio"]  = 3_000,
            ["Bestiary"]      = 6_000,
            ["Papyrus Codex"] = 10_000,

            // --- Poles ---
            ["Cypress Pole"]      = 1_000,
            ["Battle Bamboo"]     = 1_400,
            ["Musk Pole"]         = 2_400,
            ["Iron Fan"]          = 4_000,
            ["Gokuu Pole"]        = 7_500,  // shop list writes "Gokuu's Pole" with apostrophe
            ["Eight-fluted Pole"] = 20_000,

            // --- Instruments ---
            ["Lamia's Harp"]     = 5_000,
            ["Bloodstring Harp"] = 10_000,

            // --- Cloths ---
            ["Damask Cloth"]  = 7_000,
            ["Cashmere"]      = 15_000,
            // --- Bags ---
            // SHOP_ITEMS.md lists "Catskin Bag" (53k) / "Proudhide Bag" (52k) /
            // "Hardscale Bag" (58k) — those are PSX/PSP names. IC remaster has
            // different bag names in ItemData: Croakadile / Fallingstar /
            // Pantherskin / Hydrascale. The IC-specific bag prices need
            // verification from a live shop read before we can populate them.
            // Leaving bags out until that confirmation lands.

            // --- Shields ---
            ["Escutcheon"]      = 400,
            ["Buckler"]         = 700,
            ["Bronze Shield"]   = 1_200,
            ["Round Shield"]    = 1_600,
            ["Mythril Shield"]  = 2_500,
            ["Golden Shield"]   = 3_500,
            ["Ice Shield"]      = 6_000,
            ["Flame Shield"]    = 6_500,
            ["Aegis Shield"]    = 10_000,
            ["Diamond Shield"]  = 12_000,
            ["Platinum Shield"] = 16_000,
            ["Crystal Shield"]  = 21_000,

            // --- Helms / Hats ---
            ["Leather Cap"]       = 150,
            ["Plumed Hat"]        = 350,
            ["Red Hood"]          = 800,
            ["Headgear"]          = 1_200,
            ["Wizard's Hat"]      = 1_800,
            ["Green Beret"]       = 3_000,
            ["Headband"]          = 5_000,
            ["Celebrant's Miter"] = 6_000,
            ["Black Cowl"]        = 7_000,
            ["Gold Hairpin"]      = 12_000,
            ["Lambent Hat"]       = 16_000,
            ["Thief's Cap"]       = 35_000,

            // --- Body Armor / Clothing / Robes ---
            ["Clothing"]         = 150,
            ["Leather Clothing"] = 300,
            ["Leather Plate"]    = 500,
            ["Ringmail"]         = 900,
            ["Mythril Vest"]     = 1_500,
            ["Adamant Vest"]     = 1_600,
            ["Wizard Clothing"]  = 1_900,
            ["Brigandine"]       = 2_500,
            ["Jujitsu Gi"]       = 4_000,
            ["Power Garb"]       = 7_000,
            ["Gaia Gear"]        = 10_000,
            ["Black Garb"]       = 12_000,
            ["Hempen Robe"]      = 1_200,
            ["Silken Robe"]      = 2_400,
            ["Wizard's Robe"]    = 4_000,
            ["Chameleon Robe"]   = 5_000,
            ["White Robe"]       = 9_000,
            ["Black Robe"]       = 13_000,
            ["Luminous Robe"]    = 30_000,

            // --- Accessories: Shoes / Boots ---
            ["Battle Boots"]   = 1_000,
            ["Spiked Boots"]   = 1_200,
            ["Germinas Boots"] = 5_000,
            ["Rubber Boots"]   = 1_500,
            ["Winged Boots"]   = 2_500,
            ["Hermes Shoes"]   = 7_000,
            ["Red Shoes"]      = 10_000,

            // --- Accessories: Cloaks ---
            ["Shoulder Cape"]      = 300,
            ["Leather Cloak"]      = 800,
            ["Mage's Cloak"]       = 2_000,
            ["Elven Cloak"]        = 8_000,
            ["Vampire Cape"]       = 15_000,
            ["Featherweave Cloak"] = 20_000,

            // --- Accessories: Gauntlets / Bracelets ---
            // ItemData uses singular forms (Gauntlet / Glove / Bracer); shop list
            // lists them as plurals. Name-matched against ItemData directly.
            ["Power Gauntlet"]    = 5_000,  // shop list: "Power Gauntlets"
            ["Magepower Glove"]   = 20_000, // shop list: "Magepower Gloves"
            ["Bracer"]            = 50_000, // shop list: "Bracers"
            ["Diamond Bracelet"]  = 5_000,
            ["Jade Armlet"]       = 10_000,
            ["Japa Mala"]         = 15_000,

            // --- Accessories: Rings / Armbands ---
            ["Nu Khai Armband"]    = 10_000,
            ["Guardian Bracelet"]  = 7_000,
            ["Reflect Ring"]       = 10_000,
            ["Protect Ring"]       = 5_000,
            ["Magick Ring"]        = 10_000,
            ["Angel Ring"]         = 20_000,

            // --- Consumables: Chemist items ---
            // ItemData uses "Hi-Potion"/"Hi-Ether"; SHOP_ITEMS.md writes
            // them as "High Potion"/"High Ether". Name-matched against ItemData.
            ["Potion"]        = 50,
            ["Hi-Potion"]     = 200,   // shop list: "High Potion"
            ["X-Potion"]      = 700,
            ["Ether"]         = 200,
            ["Hi-Ether"]      = 600,   // shop list: "High Ether"
            ["Antidote"]      = 50,
            ["Eye Drops"]     = 50,
            ["Echo Herbs"]    = 50,
            ["Maiden's Kiss"] = 50,
            ["Gold Needle"]   = 100,
            ["Holy Water"]    = 2_000,
            ["Remedy"]        = 350,
            ["Phoenix Down"]  = 300,

            // --- Consumables: Throwables ---
            ["Shuriken"]        = 50,
            ["Fuma Shuriken"]   = 300,
            ["Yagyu Darkrood"]  = 1_000,
            ["Flameburst Bomb"] = 250,
            ["Snowmelt Bomb"]   = 250,
            ["Spark Bomb"]      = 250,
        };

        /// <summary>
        /// Resolved id→buy-price table, built once at static init by walking
        /// <see cref="ItemData.Items"/> and matching each entry's Name against
        /// <see cref="BuyPricesByName"/>. Names that don't match any ItemData
        /// entry land in <see cref="UnresolvedNames"/> for diagnostics.
        /// </summary>
        public static readonly IReadOnlyDictionary<int, int> BuyPrices;

        /// <summary>
        /// Resolved id→sell-price OVERRIDE table. Populated from
        /// <see cref="SellPricesByName"/> — ground-truth values captured
        /// from the game UI. When an entry exists here, it supersedes the
        /// buy/2 estimate in <see cref="GetSellPrice"/>.
        /// </summary>
        public static readonly IReadOnlyDictionary<int, int> SellPriceOverrides;

        /// <summary>
        /// Names in either <see cref="BuyPricesByName"/> or
        /// <see cref="SellPricesByName"/> that didn't resolve to any
        /// ItemData entry. Populated at static init for diagnostic use.
        /// Empty in the steady state; non-empty means either a typo or an
        /// item ItemData doesn't know about yet.
        /// </summary>
        public static readonly IReadOnlyCollection<string> UnresolvedNames;

        static ItemPrices()
        {
            // Build name → id map from ItemData so we can invert both
            // by-name tables. Case-sensitive exact match; duplicates in
            // ItemData would resolve to the LAST-seen ID (none expected
            // in the canonical table).
            var idByName = new Dictionary<string, int>();
            foreach (var kv in ItemData.Items)
            {
                idByName[kv.Value.Name] = kv.Key;
            }

            var buyResolved = new Dictionary<int, int>(BuyPricesByName.Count);
            var sellResolved = new Dictionary<int, int>(SellPricesByName.Count);
            var unresolved = new List<string>();
            foreach (var kv in BuyPricesByName)
            {
                if (idByName.TryGetValue(kv.Key, out var id))
                    buyResolved[id] = kv.Value;
                else
                    unresolved.Add(kv.Key);
            }
            foreach (var kv in SellPricesByName)
            {
                if (idByName.TryGetValue(kv.Key, out var id))
                    sellResolved[id] = kv.Value;
                else
                    unresolved.Add(kv.Key);
            }

            BuyPrices = buyResolved;
            SellPriceOverrides = sellResolved;
            UnresolvedNames = unresolved;
        }

        /// <summary>
        /// Returns the buy price for an item, or null if the item is not
        /// in the known-shop-stock table (story drops, unique equipment,
        /// unmapped IC items, etc.).
        /// </summary>
        public static int? GetBuyPrice(int itemId)
        {
            return BuyPrices.TryGetValue(itemId, out var price) ? price : (int?)null;
        }

        /// <summary>
        /// Returns the sell price for an item. Checks <see cref="SellPriceOverrides"/>
        /// first (ground-truth values captured from the game UI); falls back
        /// to buy/2 if no override exists. Null if the item has neither a
        /// ground-truth sell price nor a known buy price.
        ///
        /// Ground-truth values are incrementally populated as they're
        /// live-verified at the Outfitter Sell screen. Each override
        /// completely supersedes the estimate for that item.
        /// </summary>
        public static int? GetSellPrice(int itemId)
        {
            if (SellPriceOverrides.TryGetValue(itemId, out var sell))
                return sell;
            var buy = GetBuyPrice(itemId);
            return buy.HasValue ? buy.Value / 2 : (int?)null;
        }

        /// <summary>
        /// Returns true if <see cref="GetSellPrice"/> returned a ground-truth
        /// override (not an estimate). Consumers can use this to render a
        /// "verified" vs "estimated" indicator.
        /// </summary>
        public static bool IsSellPriceGroundTruth(int itemId)
        {
            return SellPriceOverrides.ContainsKey(itemId);
        }
    }
}
