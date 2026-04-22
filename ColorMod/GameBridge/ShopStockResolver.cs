using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Thin dispatcher that assembles the per-category stock list
    /// for a given (location, chapter) by walking the registered
    /// categories in <see cref="ShopBitmapRegistry"/> and calling
    /// into the memory-backed <see cref="ShopStockDecoder"/> for
    /// each one.
    ///
    /// Session 55: split out of the <c>shop_stock</c> bridge action
    /// so <c>screen.stockItems</c> can populate the full shop
    /// catalog during screen assembly without rewriting the decode
    /// loop. The heavy "locate record in memory" step stays on
    /// <c>ShopStockDecoder</c> — this class just coordinates.
    /// </summary>
    public static class ShopStockResolver
    {
        /// <summary>
        /// Decode every registered category for a (location, chapter)
        /// tuple and return a dict keyed by category name ready to
        /// serialize onto <c>screen.stockItems</c>. Categories that
        /// fail to locate (e.g. the active-widget heap copy isn't
        /// currently open) are silently skipped rather than surfacing
        /// as empty lists — an empty dict is a clearer "no stock
        /// resolved" signal than a dict full of empty category lists.
        /// </summary>
        public static Dictionary<string, List<ShopStockItem>> DecodeAll(
            ShopStockDecoder decoder,
            int location,
            int chapter)
        {
            var result = new Dictionary<string, List<ShopStockItem>>();
            foreach (var cat in ShopBitmapRegistry.RegisteredCategoriesFor(location, chapter))
            {
                var bmp = ShopBitmapRegistry.Lookup(location, chapter, cat);
                if (bmp == null) continue;

                long recAddr = LocateRecord(decoder, cat, bmp);
                if (recAddr == 0) continue;

                int expectedCount = ShopStockDecoder.FormatForCategory(cat)
                    == ShopStockDecoder.RecordFormat.IdArray
                        ? CountIdArrayIds(bmp)
                        : CountBits(bmp);
                var stock = decoder.DecodeStockAt(recAddr, cat, location, chapter, expectedCount);
                if (stock.Count == 0) continue;

                result[cat.ToString()] = stock;
            }
            return result;
        }

        /// <summary>
        /// Format-aware record locator. Bitmap categories search for
        /// the bitmap+count pair; id-array categories (shields/helms)
        /// search for the non-zero id prefix + terminator.
        /// </summary>
        private static long LocateRecord(
            ShopStockDecoder decoder,
            ShopStockDecoder.Category cat,
            byte[] bmp)
        {
            if (ShopStockDecoder.FormatForCategory(cat) == ShopStockDecoder.RecordFormat.IdArray)
            {
                int nz = CountIdArrayIds(bmp);
                var expectedIds = new byte[nz];
                System.Array.Copy(bmp, 0, expectedIds, 0, nz);
                return decoder.LocateIdArrayRecord(expectedIds);
            }

            return decoder.LocateBitmapRecord(bmp, CountBits(bmp));
        }

        /// <summary>
        /// Count set bits in a bitmap — the expected-count argument
        /// used by <see cref="ShopStockDecoder.LocateBitmapRecord"/>.
        /// Exposed as public so tests can pin the contract without
        /// going through memory.
        /// </summary>
        public static int CountBits(byte[] bmp)
        {
            if (bmp == null) return 0;
            int count = 0;
            foreach (var b in bmp)
                for (int bit = 0; bit < 8; bit++)
                    if ((b & (1 << bit)) != 0) count++;
            return count;
        }

        /// <summary>
        /// Count non-zero bytes in an id-array up to the first zero
        /// terminator. Used to size the search pattern passed to
        /// <see cref="ShopStockDecoder.LocateIdArrayRecord"/>.
        /// </summary>
        public static int CountIdArrayIds(byte[] bmp)
        {
            if (bmp == null) return 0;
            int count = 0;
            foreach (var b in bmp)
            {
                if (b == 0) break;
                count++;
            }
            return count;
        }
    }
}
