using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Registry of known (location, chapter, category) → expected bitmap
    /// signatures, used by <see cref="ShopStockDecoder"/>'s auto-mode to
    /// locate the active shop's stock record without the caller passing
    /// bitmap hex manually.
    ///
    /// Bitmaps are captured live by opening the relevant shop tab and
    /// searching for the displayed items' bit pattern. As new
    /// (location, chapter) tuples are verified they get added here.
    /// Missing entries fall back to the pattern-arg path.
    ///
    /// Current coverage — Chapter 1 (all 15 settlements visited):
    /// - Staves/rods set (7 items: Rod, Thunder Rod, Oak/White/Serpent/
    ///   Mage's/Golden Staff) — Yardrow(7), Gollund(8), Dorter(9),
    ///   Zaland(10), Warjilis(12), Bervenia(13), Sal Ghidos(14).
    ///   All 7 share bitmap `00 06 76 00 00 00 00 00`. Prices differ
    ///   per shop (Ch1 discount at 5, end-game at 2) — see
    ///   <see cref="ChapterShopPrices"/>.
    /// - Dagger set (7 items, ids 1-7: Dagger, Mythril Knife, Blind
    ///   Knife, Mage Masher, Platinum Dagger, Main Gauche, Orichalcum
    ///   Dirk) — Lesalia(0), Riovanes(1), Eagrose(2), Lionel(3),
    ///   Limberry(4), Zeltennia(5), Gariland(6). Uses Daggers
    ///   category (offset 1) and 4-byte bitmap format at address
    ///   0x3CE95C0 (all 7 dagger shops share the single record).
    /// - Ranged set (8 items: Bowgun, Knightslayer, Crossbow, Poison
    ///   Bow, Hunting Bow, Gastrophetes, Romandan Pistol, Mythril
    ///   Gun) — Goug(11). The only matching in-memory bitmap
    ///   `00 00 00 20 F8 01 00 00` decodes to 7 items missing
    ///   Mythril Gun, so this shop stays unregistered rather than
    ///   return wrong data.
    ///
    /// 14 of 15 settlements registered (7 staves + 7 daggers). Goug
    /// remains unregistered due to the 7-bit bitmap missing the 8th
    /// item. Shields/Helms/Body/Accessories/Consumables also use a
    /// different scheme not cracked yet.
    /// </summary>
    public static class ShopBitmapRegistry
    {
        // Shared bitmap for the Chapter-1 "standard staves+rods" set.
        // Decodes (offset+42) to ids 51 (Rod), 52 (Thunder Rod), 59
        // (Oak Staff), 60 (White Staff), 62 (Serpent Staff), 63
        // (Mage's Staff), 64 (Golden Staff).
        private static readonly byte[] Ch1Staves = new byte[] { 0x00, 0x06, 0x76, 0x00, 0x00, 0x00, 0x00, 0x00 };

        // Goug's Ch1 stock: 7 ranged weapons. Bitmap bits map (offset
        // +42) to ids 71 (Romandan Pistol), 77 (Bowgun), 78
        // (Knightslayer), 79 (Crossbow), 80 (Poison Bow), 81
        // (Hunting Bow), 82 (Gastrophetes).
        private static readonly byte[] Ch1Ranged = new byte[] { 0x00, 0x00, 0x00, 0x20, 0xF8, 0x01, 0x00, 0x00 };

        // Daggers Ch1 — 7 items (ids 1-7: Dagger, Mythril Knife,
        // Blind Knife, Mage Masher, Platinum Dagger, Main Gauche,
        // Orichalcum Dirk). Stored as a 4-byte bitmap at
        // `0x3CE95C0` (dagger record shared by all 7 dagger shops).
        // Bitmap `7F 00 00 00` (bits 0-6 set) decodes at offset 1
        // to ids 1-7. Padded to 8 bytes so callers can pass a
        // uniform byte array through the decoder.
        private static readonly byte[] Ch1Daggers = new byte[] { 0x7F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        // Shields Ch1 — 7 items (ids 128-134: Escutcheon, Buckler,
        // Bronze/Round/Mythril/Golden/Ice Shield). Session 54 tour
        // at Dorter's Shields tab revealed shield records use a u8
        // id-array format (NOT bitmap) — the active widget stores
        // IDs directly as `80 81 82 83 84 85 86 00` where each
        // byte is an FFTPatcher item id and 0 terminates the list.
        // LocateIdArrayRecord finds the record via AoB; decoder
        // dispatches on RecordFormat.IdArray.
        private static readonly byte[] Ch1Shields = new byte[] { 0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x00 };

        // Helms Ch1 — 7 items (ids 157-163: Leather Cap, Plumed Hat,
        // Red Hood, Headgear, Wizard's Hat, Green Beret, Headband).
        // Same u8 id-array format as shields. Dorter's Helms tab
        // (tab 3) shows these 7 hats not metal helms; FFTPatcher
        // classifies them as "hat" subtype.
        private static readonly byte[] Ch1Hats = new byte[] { 0x9D, 0x9E, 0x9F, 0xA0, 0xA1, 0xA2, 0xA3, 0x00 };

        // Consumables Ch1 — 6 items (ids 240-244, 246: Potion,
        // Hi-Potion, X-Potion, Ether, Hi-Ether, Antidote — Elixir
        // at 245 is skipped). Uses Bitmap8 format with bit offset
        // 240. Bitmap `5F 00 00 00 00 00 00 00` = bits 0-4 + bit 6.
        // Session 54 tour confirmed at Dorter + Yardrow Consumables
        // tabs both sell these 6 items identical.
        private static readonly byte[] Ch1Consumables = new byte[] { 0x5F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        // Body Ch1 — 7 items (ids 186-192: Clothing, Leather
        // Clothing, Leather Plate, Ringmail, Mythril Vest, Adamant
        // Vest, Wizard Clothing). Bitmap4 format at offset 186.
        // Session 54 Yardrow tab verified.
        private static readonly byte[] Ch1Body = new byte[] { 0x7F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        // Accessories Ch1 — 7 items (ids 208-214: Battle/Spiked/
        // Germinas/Rubber/Winged Boots + Hermes Shoes + Red
        // Shoes). Bitmap4 at offset 208. Session 54 Yardrow tab
        // verified (same pattern as Body but different offset).
        private static readonly byte[] Ch1Accessories = new byte[] { 0x7F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        /// <summary>
        /// Location IDs match the values read from
        /// <c>0x14077D208</c> (u8). Chapter IDs are 1-based (Chapter 1
        /// = 1, Chapter 4 = 4).
        /// </summary>
        private static readonly Dictionary<(int location, int chapter, ShopStockDecoder.Category category), byte[]> _map = new()
        {
            // Staves-selling shops (verified live session 54 via screenshot at each)
            [(7,  1, ShopStockDecoder.Category.Weapons)] = Ch1Staves,   // Yardrow
            [(8,  1, ShopStockDecoder.Category.Weapons)] = Ch1Staves,   // Gollund
            [(9,  1, ShopStockDecoder.Category.Weapons)] = Ch1Staves,   // Dorter
            [(10, 1, ShopStockDecoder.Category.Weapons)] = Ch1Staves,   // Zaland
            [(12, 1, ShopStockDecoder.Category.Weapons)] = Ch1Staves,   // Warjilis
            [(13, 1, ShopStockDecoder.Category.Weapons)] = Ch1Staves,   // Bervenia
            [(14, 1, ShopStockDecoder.Category.Weapons)] = Ch1Staves,   // Sal Ghidos

            // Goug (11) Ch1 weapons: shop displays 8 items
            // (Bowgun/Knightslayer/Crossbow/Poison Bow/Hunting Bow/
            //  Gastrophetes/Romandan Pistol/Mythril Gun — 7 crossbows
            // + 2 guns). The only matching in-memory record decodes
            // to 7 items (missing Mythril Gun). NOT registered
            // because auto-mode would silently return wrong data.
            // See project_shop_stock_SHIPPED.md for details.

            // Dagger shops — shared bitmap 7F (ids 1-7) in 4-byte
            // format. Session 54 tour verified 7 dagger-selling
            // Chapter-1 settlements: Lesalia(0), Riovanes(1),
            // Eagrose(2), Lionel(3), Limberry(4), Zeltennia(5),
            // Gariland(6). All 7 share the same dagger record at
            // `0x3CE95C0` (in the game data region). LocateBitmapRecord
            // tries 8-byte then 4-byte format and finds this record
            // via the 4-byte fallback.
            [(0, 1, ShopStockDecoder.Category.Daggers)] = Ch1Daggers,   // Lesalia
            [(1, 1, ShopStockDecoder.Category.Daggers)] = Ch1Daggers,   // Riovanes
            [(2, 1, ShopStockDecoder.Category.Daggers)] = Ch1Daggers,   // Eagrose
            [(3, 1, ShopStockDecoder.Category.Daggers)] = Ch1Daggers,   // Lionel
            [(4, 1, ShopStockDecoder.Category.Daggers)] = Ch1Daggers,   // Limberry
            [(5, 1, ShopStockDecoder.Category.Daggers)] = Ch1Daggers,   // Zeltennia
            [(6, 1, ShopStockDecoder.Category.Daggers)] = Ch1Daggers,   // Gariland

            // Shields Ch1 (u8 id-array format) — session 54 live-
            // verified at Dorter (9). All 15 settlements probably
            // share the same Ch1 shields stock but only Dorter is
            // verified; others can be added after similar screenshot
            // verification.
            [(9, 1, ShopStockDecoder.Category.Shields)] = Ch1Shields,   // Dorter

            // Helms (Hats) Ch1 (u8 id-array format) — session 54
            // live-verified at Dorter + Yardrow. IDs 157-163 are
            // softer headgear (hats), not metal helms; FFTPatcher
            // lumps them together with `Headwear` as the tab label.
            // Live-verified at Dorter + Yardrow; other staves shops
            // likely carry the same hat stock but the decoder will
            // return "record not found" at those shops if they
            // don't (fail-safe: we wouldn't return wrong data).
            [(0, 1, ShopStockDecoder.Category.Helms)] = Ch1Hats,        // Lesalia (assumed)
            [(1, 1, ShopStockDecoder.Category.Helms)] = Ch1Hats,        // Riovanes (assumed)
            [(2, 1, ShopStockDecoder.Category.Helms)] = Ch1Hats,        // Eagrose (assumed)
            [(3, 1, ShopStockDecoder.Category.Helms)] = Ch1Hats,        // Lionel (assumed)
            [(4, 1, ShopStockDecoder.Category.Helms)] = Ch1Hats,        // Limberry (assumed)
            [(5, 1, ShopStockDecoder.Category.Helms)] = Ch1Hats,        // Zeltennia (assumed)
            [(6, 1, ShopStockDecoder.Category.Helms)] = Ch1Hats,        // Gariland (assumed)
            [(7, 1, ShopStockDecoder.Category.Helms)] = Ch1Hats,        // Yardrow ✓verified
            [(8, 1, ShopStockDecoder.Category.Helms)] = Ch1Hats,        // Gollund (assumed)
            [(9, 1, ShopStockDecoder.Category.Helms)] = Ch1Hats,        // Dorter ✓verified
            [(10, 1, ShopStockDecoder.Category.Helms)] = Ch1Hats,       // Zaland (assumed)
            [(11, 1, ShopStockDecoder.Category.Helms)] = Ch1Hats,       // Goug (assumed)
            [(12, 1, ShopStockDecoder.Category.Helms)] = Ch1Hats,       // Warjilis (assumed)
            [(13, 1, ShopStockDecoder.Category.Helms)] = Ch1Hats,       // Bervenia (assumed)
            [(14, 1, ShopStockDecoder.Category.Helms)] = Ch1Hats,       // Sal Ghidos (assumed)

            // Consumables Ch1 (Bitmap8 format at offset 240) —
            // session 54 tour live-verified at Dorter + Yardrow.
            // 6 items across 7 bits (with Elixir skipped at id 245).
            // Registered for all 15 settlements on the assumption
            // they carry the same Ch1 consumables.
            [(0,  1, ShopStockDecoder.Category.Consumables)] = Ch1Consumables, // Lesalia
            [(1,  1, ShopStockDecoder.Category.Consumables)] = Ch1Consumables, // Riovanes
            [(2,  1, ShopStockDecoder.Category.Consumables)] = Ch1Consumables, // Eagrose
            [(3,  1, ShopStockDecoder.Category.Consumables)] = Ch1Consumables, // Lionel
            [(4,  1, ShopStockDecoder.Category.Consumables)] = Ch1Consumables, // Limberry
            [(5,  1, ShopStockDecoder.Category.Consumables)] = Ch1Consumables, // Zeltennia
            [(6,  1, ShopStockDecoder.Category.Consumables)] = Ch1Consumables, // Gariland
            [(7,  1, ShopStockDecoder.Category.Consumables)] = Ch1Consumables, // Yardrow ✓verified
            [(8,  1, ShopStockDecoder.Category.Consumables)] = Ch1Consumables, // Gollund
            [(9,  1, ShopStockDecoder.Category.Consumables)] = Ch1Consumables, // Dorter ✓verified
            [(10, 1, ShopStockDecoder.Category.Consumables)] = Ch1Consumables, // Zaland
            [(11, 1, ShopStockDecoder.Category.Consumables)] = Ch1Consumables, // Goug
            [(12, 1, ShopStockDecoder.Category.Consumables)] = Ch1Consumables, // Warjilis
            [(13, 1, ShopStockDecoder.Category.Consumables)] = Ch1Consumables, // Bervenia
            [(14, 1, ShopStockDecoder.Category.Consumables)] = Ch1Consumables, // Sal Ghidos

            // Body armor Ch1 (Bitmap4 at offset 186) — session 54
            // Yardrow Body tab verified. Registered for all 15
            // settlements on the assumption they carry identical
            // Ch1 body stock.
            [(0,  1, ShopStockDecoder.Category.Body)] = Ch1Body,  // Lesalia
            [(1,  1, ShopStockDecoder.Category.Body)] = Ch1Body,  // Riovanes
            [(2,  1, ShopStockDecoder.Category.Body)] = Ch1Body,  // Eagrose
            [(3,  1, ShopStockDecoder.Category.Body)] = Ch1Body,  // Lionel
            [(4,  1, ShopStockDecoder.Category.Body)] = Ch1Body,  // Limberry
            [(5,  1, ShopStockDecoder.Category.Body)] = Ch1Body,  // Zeltennia
            [(6,  1, ShopStockDecoder.Category.Body)] = Ch1Body,  // Gariland
            [(7,  1, ShopStockDecoder.Category.Body)] = Ch1Body,  // Yardrow ✓verified
            [(8,  1, ShopStockDecoder.Category.Body)] = Ch1Body,  // Gollund
            [(9,  1, ShopStockDecoder.Category.Body)] = Ch1Body,  // Dorter ✓verified
            [(10, 1, ShopStockDecoder.Category.Body)] = Ch1Body,  // Zaland
            [(11, 1, ShopStockDecoder.Category.Body)] = Ch1Body,  // Goug
            [(12, 1, ShopStockDecoder.Category.Body)] = Ch1Body,  // Warjilis
            [(13, 1, ShopStockDecoder.Category.Body)] = Ch1Body,  // Bervenia
            [(14, 1, ShopStockDecoder.Category.Body)] = Ch1Body,  // Sal Ghidos

            // Accessories Ch1 (Bitmap4 at offset 208) — session 54
            // Yardrow + Dorter Accessories tabs verified.
            [(0,  1, ShopStockDecoder.Category.Accessories)] = Ch1Accessories,  // Lesalia
            [(1,  1, ShopStockDecoder.Category.Accessories)] = Ch1Accessories,  // Riovanes
            [(2,  1, ShopStockDecoder.Category.Accessories)] = Ch1Accessories,  // Eagrose
            [(3,  1, ShopStockDecoder.Category.Accessories)] = Ch1Accessories,  // Lionel
            [(4,  1, ShopStockDecoder.Category.Accessories)] = Ch1Accessories,  // Limberry
            [(5,  1, ShopStockDecoder.Category.Accessories)] = Ch1Accessories,  // Zeltennia
            [(6,  1, ShopStockDecoder.Category.Accessories)] = Ch1Accessories,  // Gariland
            [(7,  1, ShopStockDecoder.Category.Accessories)] = Ch1Accessories,  // Yardrow ✓verified
            [(8,  1, ShopStockDecoder.Category.Accessories)] = Ch1Accessories,  // Gollund
            [(9,  1, ShopStockDecoder.Category.Accessories)] = Ch1Accessories,  // Dorter ✓verified
            [(10, 1, ShopStockDecoder.Category.Accessories)] = Ch1Accessories,  // Zaland
            [(11, 1, ShopStockDecoder.Category.Accessories)] = Ch1Accessories,  // Goug
            [(12, 1, ShopStockDecoder.Category.Accessories)] = Ch1Accessories,  // Warjilis
            [(13, 1, ShopStockDecoder.Category.Accessories)] = Ch1Accessories,  // Bervenia
            [(14, 1, ShopStockDecoder.Category.Accessories)] = Ch1Accessories,  // Sal Ghidos
        };

        /// <summary>
        /// Look up the expected bitmap for a given (location, chapter,
        /// category) tuple. Returns null when the tuple isn't
        /// registered — callers should either fall back to the
        /// pattern-arg path or surface a clear "not mapped" error.
        /// </summary>
        public static byte[]? Lookup(int location, int chapter, ShopStockDecoder.Category category)
        {
            return _map.TryGetValue((location, chapter, category), out var bmp) ? bmp : null;
        }

        /// <summary>
        /// True when a (location, chapter, category) tuple has a
        /// registered bitmap. Exposed so callers can probe before
        /// attempting auto-mode.
        /// </summary>
        public static bool HasMapping(int location, int chapter, ShopStockDecoder.Category category)
            => _map.ContainsKey((location, chapter, category));

        /// <summary>
        /// Enumerate the registered categories for a given
        /// (location, chapter) tuple, ordered by the canonical
        /// Outfitter tab order (Weapons, Daggers, Shields, Helms,
        /// Body, Accessories, Consumables). Used by screen-assembly
        /// to populate <c>screen.stockItems</c> with every category
        /// the shop carries without needing to know which tab is
        /// currently active.
        /// </summary>
        public static List<ShopStockDecoder.Category> RegisteredCategoriesFor(int location, int chapter)
        {
            var result = new List<ShopStockDecoder.Category>();
            // Canonical Outfitter tab order. Matches the in-game tab
            // sequence so iteration order is predictable.
            var order = new[]
            {
                ShopStockDecoder.Category.Weapons,
                ShopStockDecoder.Category.Daggers,
                ShopStockDecoder.Category.Shields,
                ShopStockDecoder.Category.Helms,
                ShopStockDecoder.Category.Body,
                ShopStockDecoder.Category.Accessories,
                ShopStockDecoder.Category.Consumables,
            };
            foreach (var cat in order)
            {
                if (_map.ContainsKey((location, chapter, cat)))
                    result.Add(cat);
            }
            return result;
        }
    }
}
