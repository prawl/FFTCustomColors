using System;
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
        /// Negative cache: when a locate misses or returns wrong-
        /// sized data, remember the failure timestamp so we don't
        /// re-attempt the expensive (~150-600 MB) memory scan on
        /// every subsequent screen call. Without this, a flaky
        /// category like Consumables would burn 4 broad searches
        /// per screen poll forever, and after 5+ polls the
        /// cumulative ~2-4 GB of scans crashes the game (kernel
        /// OOM or access violation against a region freed mid-
        /// scan). Verified live: removing this back-off → game
        /// crashes within ~5 OutfitterBuy polls.
        /// </summary>
        private static readonly ConcurrentDictionary<(int, int, ShopStockDecoder.Category), DateTime> _missCache = new();

        /// <summary>
        /// How long to suppress retries after a failed locate.
        /// 30 seconds is enough that mass-polling can't crash the
        /// game but short enough that a tab switch or heap
        /// reorganization that brings the record into searchable
        /// memory gets picked up on the next manual screen call.
        /// Public so tests + future callers can tune it.
        /// </summary>
        public static readonly TimeSpan MissBackoff = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Cap on cold (cache-miss) locates per <see cref="DecodeAll"/>
        /// call. Set to 0 by default — the screen-assembly path does
        /// no AoB scans at all. Categories surface only after being
        /// seeded by the dedicated <c>shop_stock</c> bridge action
        /// (which writes via <see cref="SeedCache"/>). Rationale:
        /// even narrow-only scans cost ~150 MB per call, and
        /// cumulative pressure across screen polls crashes the
        /// game (live-verified at Ch1 Dorter — auto-locate during
        /// screen assembly killed the game within ~10 polls of
        /// changing the save state). Pass -1 to disable the cap
        /// entirely (used by the dedicated shop_stock action).
        /// </summary>
        public const int DefaultMaxColdLocatesPerCall = 0;

        /// <summary>
        /// Decode every registered category for a (location, chapter)
        /// tuple and return a dict keyed by category name ready to
        /// serialize onto <c>screen.stockItems</c>. Categories that
        /// fail to locate (e.g. the active-widget heap copy isn't
        /// currently open) are silently skipped rather than surfacing
        /// as empty lists — an empty dict is a clearer "no stock
        /// resolved" signal than a dict full of empty category lists.
        ///
        /// Cold-cache categories are resolved at most
        /// <paramref name="maxColdLocatesPerCall"/> per invocation
        /// (default 1) to bound per-call memory-scan cost. Pass -1
        /// to disable the cap (used by the manual shop_stock action
        /// where the caller explicitly accepts the search cost).
        /// </summary>
        public static Dictionary<string, List<ShopStockItem>> DecodeAll(
            ShopStockDecoder decoder,
            int location,
            int chapter,
            int maxColdLocatesPerCall = DefaultMaxColdLocatesPerCall)
        {
            var result = new Dictionary<string, List<ShopStockItem>>();
            int coldLocatesUsed = 0;
            foreach (var cat in ShopBitmapRegistry.RegisteredCategoriesFor(location, chapter))
            {
                var bmp = ShopBitmapRegistry.Lookup(location, chapter, cat);
                if (bmp == null) continue;

                int expectedCount = ShopStockDecoder.FormatForCategory(cat)
                    == ShopStockDecoder.RecordFormat.IdArray
                        ? CountIdArrayIds(bmp)
                        : CountBits(bmp);

                bool budgetExhausted = maxColdLocatesPerCall >= 0
                                       && coldLocatesUsed >= maxColdLocatesPerCall;
                var (stock, didColdLocate) = ResolveOneCategory(
                    decoder, location, chapter, cat, bmp, expectedCount,
                    allowColdLocate: !budgetExhausted);

                if (didColdLocate) coldLocatesUsed++;
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
        ///
        /// When <paramref name="allowColdLocate"/> is false, only
        /// the cached path runs; cache misses return empty
        /// immediately without spending a memory scan. Used by
        /// <see cref="DecodeAll"/>'s per-call budget enforcement.
        ///
        /// Returns the decoded stock + whether a cold locate ran
        /// (so the caller can charge the budget).
        /// </summary>
        private static (List<ShopStockItem> stock, bool didColdLocate) ResolveOneCategory(
            ShopStockDecoder decoder,
            int location,
            int chapter,
            ShopStockDecoder.Category cat,
            byte[] bmp,
            int expectedCount,
            bool allowColdLocate)
        {
            var key = (location, chapter, cat);

            // Cached path: re-read the previously good address.
            // ValidateAgainstExpected inside DecodeStockAt drops
            // the result to empty if the bytes have shifted.
            if (_addressCache.TryGetValue(key, out var cachedAddr))
            {
                var cached = decoder.DecodeStockAt(cachedAddr, cat, location, chapter, expectedCount);
                if (cached.Count == expectedCount) return (cached, didColdLocate: false);

                // Cache hit went bad — invalidate and fall through
                // to re-locate. Common cause: the heap region got
                // reused for unrelated data, or the game moved the
                // record across a tab switch.
                _addressCache.TryRemove(key, out _);
            }

            // Negative-cache: was this key recently a miss? Skip
            // the expensive scan if so — game crashes if we burn
            // gigabytes of memory scans on repeated cold polls.
            if (_missCache.TryGetValue(key, out var lastMiss)
                && DateTime.UtcNow - lastMiss < MissBackoff)
            {
                return (new List<ShopStockItem>(), didColdLocate: false);
            }

            // Per-call budget exhausted: caller is asking us to
            // defer cold work to the next screen call.
            if (!allowColdLocate)
                return (new List<ShopStockItem>(), didColdLocate: false);

            // Cold locate: spend the search budget.
            long recAddr = LocateRecord(decoder, cat, bmp);
            if (recAddr == 0)
            {
                _missCache[key] = DateTime.UtcNow;
                return (new List<ShopStockItem>(), didColdLocate: true);
            }

            var fresh = decoder.DecodeStockAt(recAddr, cat, location, chapter, expectedCount);
            if (fresh.Count == expectedCount)
            {
                _addressCache[key] = recAddr;
                _missCache.TryRemove(key, out _);
            }
            else
            {
                _missCache[key] = DateTime.UtcNow;
            }

            return (fresh, didColdLocate: true);
        }

        /// <summary>
        /// Clear both the address cache and the miss-suppression
        /// cache. Exposed for tests and for any future invalidation
        /// hook (e.g. when chapter advances and prices may have
        /// changed). Tests rely on this to start from a clean
        /// state since both caches are static and survive across
        /// individual test cases.
        /// </summary>
        public static void ClearCache()
        {
            _addressCache.Clear();
            _missCache.Clear();
        }

        /// <summary>
        /// Seed the cache with a known-good record address for a
        /// (location, chapter, category) tuple. Called by the
        /// dedicated <c>shop_stock</c> bridge action after a
        /// successful locate so future <c>screen.stockItems</c>
        /// resolutions reuse the address (skipping the broad-
        /// search that the screen-assembly path won't run on its
        /// own). Also clears any pending miss back-off — a fresh
        /// locate proves the data is reachable.
        /// </summary>
        public static void SeedCache(int location, int chapter, ShopStockDecoder.Category cat, long recordAddr)
        {
            var key = (location, chapter, cat);
            _addressCache[key] = recordAddr;
            _missCache.TryRemove(key, out _);
        }

        /// <summary>
        /// Tests-only peek: how many entries are currently cached.
        /// Lets the cache-hit/miss tests assert the cache filled
        /// after the first decode.
        /// </summary>
        public static int CachedEntryCount => _addressCache.Count;

        /// <summary>
        /// Format-aware record locator. Bitmap categories search for
        /// the bitmap+count pair; id-array categories (shields/helms)
        /// search for the non-zero id prefix + terminator. The
        /// resolver path always passes <c>narrowOnly=true</c> to
        /// skip the broad memory-mapped scan that contributes most
        /// to game-stability risk under repeated polling.
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
                return decoder.LocateIdArrayRecord(expectedIds, narrowOnly: true);
            }

            return decoder.LocateBitmapRecord(bmp, CountBits(bmp), narrowOnly: true);
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
