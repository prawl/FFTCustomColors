using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Per-(location, chapter, itemId) buy-price overrides captured
    /// live from the in-game Outfitter Buy screen. Shop stock and
    /// prices change by story chapter — <see cref="ItemPrices"/>
    /// holds Sal Ghidos end-game values as the fallback, but lower
    /// chapters sell at cheaper prices (e.g. Dorter Ch1 White Staff
    /// = 400 vs end-game 800). Some shops sell at end-game prices
    /// even in Chapter 1 (e.g. Zaland).
    ///
    /// Called from <see cref="ShopStockDecoder.DecodeStockAt"/> when
    /// the decoder knows the (location, chapter) context. Falls
    /// through to <see cref="ItemPrices.GetBuyPrice"/> when the
    /// tuple isn't overridden.
    ///
    /// Session 54 verifications — Chapter 1 (all 7 staves shops):
    /// - Dorter (9): White 400, Serpent 1200 (discounted)
    /// - Yardrow (7): White 400, Serpent 1200 (discounted)
    /// - Gollund (8): White 400, Serpent 1200 (discounted)
    /// - Bervenia (13): White 400, Serpent 1200 (discounted)
    /// - Sal Ghidos (14): White 400, Serpent 1200 (discounted)
    /// - Warjilis (12): White 800, Serpent 2200 (end-game — matches
    ///   fallback, no override needed)
    /// - Zaland (10): White 800, Serpent 2200 (end-game — no override)
    /// </summary>
    public static class ChapterShopPrices
    {
        private static readonly Dictionary<(int location, int chapter, int itemId), int> _prices = new()
        {
            // Dorter (9) Chapter 1 — discounted staves prices
            // Oak Staff (59) = 120, Mage's Staff (63) = 4000,
            // Golden Staff (64) = 7000, Rod (51) = 200,
            // Thunder Rod (52) = 400 — all match ItemPrices fallback
            // so no overrides needed. Only these two differ:
            [(9, 1, 60)]  = 400,   // White Staff (end-game 800)
            [(9, 1, 62)]  = 1200,  // Serpent Staff (end-game 2200)

            // Yardrow (7) Chapter 1 — same discounted prices as Dorter
            [(7, 1, 60)]  = 400,
            [(7, 1, 62)]  = 1200,

            // Gollund (8) Chapter 1 — same discounted prices as Dorter
            [(8, 1, 60)]  = 400,
            [(8, 1, 62)]  = 1200,

            // Bervenia (13) Chapter 1 — same discounted prices as Dorter
            [(13, 1, 60)] = 400,
            [(13, 1, 62)] = 1200,

            // Sal Ghidos (14) Chapter 1 — same discounted prices as Dorter
            [(14, 1, 60)] = 400,
            [(14, 1, 62)] = 1200,

            // Dorter (9) Chapter 1 shields — session 54 screenshot
            // verified: Escutcheon 400, Buckler 700, Bronze 1000,
            // Round 1600, Mythril 2500, Golden 3500, Ice 6000.
            // ItemPrices fallback gives: 400/700/1200/1600/2500/3500/6000.
            // Only Bronze Shield (id 130) differs in Ch1 (1000 vs 1200
            // end-game) so only it needs an override.
            [(9, 1, 130)] = 1000,  // Bronze Shield (end-game 1200)

            // Dagger shops (Chapter 1): Lesalia(0), Riovanes(1),
            // Eagrose(2), Lionel(3), Limberry(4), Zeltennia(5),
            // Gariland(6) — session 54 tour confirmed identical
            // Ch1 pricing at all 7 dagger shops:
            //   Dagger = 100 (vs end-game 200)
            //   Mythril Knife = 200 (vs end-game 300)
            //   Blind Knife = 300 (vs end-game 400)
            //   Mage Masher = 700 (vs end-game 600 — higher in Ch1!)
            //   Platinum Dagger = 1500 (vs end-game 800)
            //   Main Gauche = 2500 (vs end-game 1000)
            //   Orichalcum Dirk = 4000 (vs end-game 3000)
            // Only items whose Ch1 price differs from end-game need
            // an override.
        };

        // Item ids for knives (1-7) reused below.
        private static readonly int[] DaggerIds = new[] { 1, 2, 3, 4, 5, 6, 7 };
        private static readonly int[] DaggerCh1Prices = new[] { 100, 200, 300, 700, 1500, 2500, 4000 };
        private static readonly int[] DaggerShopLocations = new[] { 0, 1, 2, 3, 4, 5, 6 };

        static ChapterShopPrices()
        {
            // Populate dagger Ch1 overrides across all 7 dagger shops.
            // Inline per-shop entries would be 49 lines of duplication;
            // this loop keeps the data table concise while remaining
            // fully deterministic and easy to audit.
            //
            // (Warjilis, Zaland, Goug staves/ranged shops sell at
            // end-game prices in Ch1 — no overrides needed; the
            // ItemPrices fallback returns the right values.)
            foreach (var loc in DaggerShopLocations)
            {
                for (int i = 0; i < DaggerIds.Length; i++)
                {
                    _prices[(loc, 1, DaggerIds[i])] = DaggerCh1Prices[i];
                }
            }
        }

        /// <summary>
        /// Per-chapter buy-price lookup. Returns the override price
        /// when a (location, chapter, itemId) tuple is registered;
        /// otherwise returns null so callers can fall back to
        /// <see cref="ItemPrices.GetBuyPrice"/>.
        /// </summary>
        public static int? Lookup(int location, int chapter, int itemId)
        {
            return _prices.TryGetValue((location, chapter, itemId), out var p) ? p : (int?)null;
        }

        /// <summary>
        /// Chapter-aware price getter: override → fallback. Matches
        /// the signature of <see cref="ItemPrices.GetBuyPrice"/> but
        /// accepts (location, chapter) context first.
        /// </summary>
        public static int? GetBuyPrice(int location, int chapter, int itemId)
        {
            var over = Lookup(location, chapter, itemId);
            return over ?? ItemPrices.GetBuyPrice(itemId);
        }
    }
}
