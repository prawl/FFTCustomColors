using System.Collections.Concurrent;
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
        /// Cache of (location, chapter, category) → resolved record
        /// address. Once a category locates + decodes cleanly we
        /// remember the address so future screen assemblies skip the
        /// expensive AoB search. The validator in
        /// <see cref="ShopStockDecoder.DecodeStockAt"/> tells us
        /// when a cached read returns wrong-sized stock — that's
        /// the invalidation signal (the address has shifted or the
        /// cached region got reused). On invalidation we re-locate
        /// and try once more in the same call.
        ///
        /// Static lifetime is fine: shop bitmap records are stable
        /// within a game session; they only move when the game
        /// reorganizes its heap (rare, and our re-locate handles
        /// it). A boot of the game cycles process state so the
        /// in-memory dictionary clears anyway.
        /// </summary>
        private static readonly ConcurrentDictionary<(int, int, ShopStockDecoder.Category), long> _addressCache = new();

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

                int expectedCount = ShopStockDecoder.FormatForCategory(cat)
                    == ShopStockDecoder.RecordFormat.IdArray
                        ? CountIdArrayIds(bmp)
                        : CountBits(bmp);

                var stock = ResolveOneCategory(decoder, location, chapter, cat, bmp, expectedCount);
                if (stock.Count == 0) continue;

                result[cat.ToString()] = stock;
            }
            return result;
        }

        /// <summary>
        /// Resolve stock for a single category. Tries the cached
        /// record address first; on validation failure invalidates
        /// the cache entry and re-locates once. Returns empty list
        /// when even the fresh locate fails — caller treats empty
        /// as "skip this category" (fail-safe; never returns wrong
        /// data).
        /// </summary>
        private static List<ShopStockItem> ResolveOneCategory(
            ShopStockDecoder decoder,
            int location,
            int chapter,
            ShopStockDecoder.Category cat,
            byte[] bmp,
            int expectedCount)
        {
            var key = (location, chapter, cat);

            // Cached path: re-read the previously good address.
            // ValidateAgainstExpected inside DecodeStockAt drops
            // the result to empty if the bytes have shifted.
            if (_addressCache.TryGetValue(key, out var cachedAddr))
            {
                var cached = decoder.DecodeStockAt(cachedAddr, cat, location, chapter, expectedCount);
                if (cached.Count == expectedCount) return cached;

                // Cache hit went bad — invalidate and fall through
                // to re-locate. Common cause: the heap region got
                // reused for unrelated data, or the game moved the
                // record across a tab switch.
                _addressCache.TryRemove(key, out _);
            }

            // Cache miss (or just-invalidated): locate fresh,
            // decode, validate, and cache only on success.
            long recAddr = LocateRecord(decoder, cat, bmp);
            if (recAddr == 0) return new List<ShopStockItem>();

            var fresh = decoder.DecodeStockAt(recAddr, cat, location, chapter, expectedCount);
            if (fresh.Count == expectedCount)
                _addressCache[key] = recAddr;

            return fresh;
        }

        /// <summary>
        /// Clear the cached record-address lookup. Exposed for
        /// tests and for any future invalidation hook (e.g. when
        /// chapter advances and prices may have changed).
        /// </summary>
        public static void ClearCache() => _addressCache.Clear();

        /// <summary>
        /// Tests-only peek: how many entries are currently cached.
        /// Lets the cache-hit/miss tests assert the cache filled
        /// after the first decode.
        /// </summary>
        public static int CachedEntryCount => _addressCache.Count;

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
