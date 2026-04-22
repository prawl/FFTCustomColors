using System.Collections.Generic;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Scans the active-widget heap region for shop bitmap records
    /// without needing pre-baked bitmaps. Each candidate record has
    /// the shape <c>[bitmap N bytes][count u32]</c> where the count
    /// equals popcount(bitmap) AND every set bit decodes to a valid
    /// item ID in some category's expected range.
    ///
    /// Why this exists: the registry-based <see cref="ShopBitmapRegistry"/>
    /// works only when the player's current shop happens to match the
    /// pre-captured (chapter, location) bitmap. Live verification at
    /// Ch1-early Dorter showed the registry is for a LATER chapter
    /// state — early-Ch1 has different (smaller) stocks per category.
    /// Rather than maintain a per-chapter table, scan the active-widget
    /// heap for whatever shop record is actually live.
    ///
    /// Trade-off: one full scan per call (~32 MB region), classifies
    /// all categories at once. Result feeds <see cref="ShopStockResolver.SeedCache"/>.
    /// </summary>
    public static class LiveShopScanner
    {
        // The active-widget heap region observed across session 54-55.
        // Live shop records cluster tightly in 0x15A000000..0x15D000000
        // (sample addresses: 0x15A800AD8, 0x15B544E10, 0x15B569C20,
        // 0x15B60B5E0, 0x15B918570, 0x15C1EF0F8). Outside this window
        // we hit .NET managed heap (0x114Axxxxx etc.) where the loose
        // popcount/anchor validators fire false positives on
        // unrelated managed objects.
        //
        // Static-table records (registry-style) at 0x3xxxxxxx-0x6xxxxxxx
        // are also excluded — they hold stale chapter snapshots, not
        // the live shop the player is browsing.
        private const long ActiveWidgetMin = 0x15A000000L;
        private const long ActiveWidgetMax = 0x15D000000L;

        /// <summary>
        /// Scan result: one record per detected shop bitmap.
        /// </summary>
        public sealed class FoundRecord
        {
            public ShopStockDecoder.Category Category { get; set; }
            public long Address { get; set; }
            public byte[] Bitmap { get; set; } = new byte[8];
            public int ItemCount { get; set; }
        }

        /// <summary>
        /// Walk the active-widget heap and return all shop record
        /// candidates classified by category. Multiple records per
        /// category are possible (heap fragments + transient duplicates);
        /// caller picks one (typically lowest-address per category).
        /// </summary>
        public static List<FoundRecord> ScanAll(MemoryExplorer explorer)
        {
            var results = new List<FoundRecord>();

            // For each registered category, build a tight signature
            // we can search for. The popcount-equals-count constraint
            // alone is too noisy (matches lots of unrelated data),
            // so we anchor on the count being a valid shop count
            // (1..8) and validate by attempting decode.
            //
            // We can't single-pass the whole heap easily because each
            // category has a different format/offset. Instead, run
            // per-category scans using bitmap-shape-anchored patterns
            // narrowed to the active-widget heap range.
            foreach (var cat in new[]
            {
                ShopStockDecoder.Category.Weapons,
                ShopStockDecoder.Category.Daggers,
                ShopStockDecoder.Category.Shields,
                ShopStockDecoder.Category.Helms,
                ShopStockDecoder.Category.Body,
                ShopStockDecoder.Category.Accessories,
                ShopStockDecoder.Category.Consumables,
            })
            {
                var found = ScanCategory(explorer, cat);
                if (found != null) results.Add(found);
            }

            return results;
        }

        /// <summary>
        /// Per-category scan using a category-specific search strategy.
        /// IdArray categories anchor on the first valid id byte (=
        /// category offset). Bitmap categories use a popcount filter.
        /// Returns the lowest-address valid record for the category, or
        /// null if none found.
        /// </summary>
        public static FoundRecord? ScanCategory(MemoryExplorer explorer, ShopStockDecoder.Category cat)
        {
            int offset = ShopStockDecoder.OffsetForCategory(cat);
            var format = ShopStockDecoder.FormatForCategory(cat);
            int idRangeMax = offset + (format == ShopStockDecoder.RecordFormat.Bitmap4 ? 32 : 64);

            // For IdArray categories, search for the lowest valid id
            // byte (= offset). Patterns like `80 ?? ?? ?? ?? ?? ?? 00`
            // for shields where the first item is Escutcheon (128).
            // We only know offset; the actual first item could be
            // ANY id in the category range.
            //
            // Simpler approach: try every plausible "first id" in the
            // range as the anchor byte. For shields range 128..134,
            // try anchors 0x80..0x86. Each match is a candidate.
            if (format == ShopStockDecoder.RecordFormat.IdArray)
                return ScanIdArrayCategory(explorer, cat, offset, idRangeMax);

            return ScanBitmapCategory(explorer, cat, offset, format, idRangeMax);
        }

        private static FoundRecord? ScanIdArrayCategory(
            MemoryExplorer explorer,
            ShopStockDecoder.Category cat,
            int offset,
            int idRangeMax)
        {
            // Try each plausible first-id as a 1-byte anchor. Read
            // 8 bytes from each match and validate the decode.
            // First-id candidates: offset..offset+7 (top 8 ids in
            // range — most shops carry the lowest tier first).
            for (int firstId = offset; firstId < offset + 8 && firstId < idRangeMax; firstId++)
            {
                var anchor = new byte[] { (byte)firstId };
                var hits = explorer.SearchBytesInAllMemory(
                    anchor, maxResults: 200,
                    minAddr: ActiveWidgetMin, maxAddr: ActiveWidgetMax,
                    broadSearch: false);

                long bestAddr = long.MaxValue;
                byte[]? bestBytes = null;
                int bestCount = 0;

                foreach (var (addr, _) in hits)
                {
                    var bytes = ReadEightBytes(explorer, (long)addr);
                    if (bytes == null) continue;

                    if (!IsValidIdArrayRecord(bytes, offset, idRangeMax, out int count)) continue;

                    if ((long)addr < bestAddr)
                    {
                        bestAddr = (long)addr;
                        bestBytes = bytes;
                        bestCount = count;
                    }
                }

                if (bestBytes != null)
                {
                    return new FoundRecord
                    {
                        Category = cat,
                        Address = bestAddr,
                        Bitmap = bestBytes,
                        ItemCount = bestCount,
                    };
                }
            }

            return null;
        }

        private static FoundRecord? ScanBitmapCategory(
            MemoryExplorer explorer,
            ShopStockDecoder.Category cat,
            int offset,
            ShopStockDecoder.RecordFormat format,
            int idRangeMax)
        {
            // Bitmap categories are harder to anchor — the bitmap can
            // start with any byte 0x00..0xFF. Strategy: search for
            // every possible bitmap byte 0x01..0xFF as a single-byte
            // anchor at potentially-record positions, then validate.
            //
            // Cost: up to 255 narrow searches in the active-widget
            // range. Too expensive for an interactive bridge call.
            //
            // Better: anchor on the *count* field. For a real shop
            // record, count is 1..8 stored as little-endian u32 with
            // bytes 1-3 being zero. So bytes at the count position
            // look like `01 00 00 00`..`08 00 00 00`. Search for
            // these 4-byte patterns; the bitmap is the 8 bytes
            // BEFORE the match (Bitmap8) or 4 bytes before (Bitmap4).
            //
            // Bitmap8 layout: [bitmap 8B][count u32 4B][...]
            //   Pattern position: count starts at +8 from record.
            // Bitmap4 layout: [bitmap 4B][count u32 4B][...]
            //   Pattern position: count starts at +4 from record.
            int bitmapWidth = format == ShopStockDecoder.RecordFormat.Bitmap4 ? 4 : 8;

            long bestAddr = long.MaxValue;
            byte[]? bestBytes = null;
            int bestCount = 0;

            for (int countCandidate = 1; countCandidate <= 8; countCandidate++)
            {
                var countPattern = new byte[]
                {
                    (byte)countCandidate, 0x00, 0x00, 0x00,
                };

                var hits = explorer.SearchBytesInAllMemory(
                    countPattern, maxResults: 1000,
                    minAddr: ActiveWidgetMin, maxAddr: ActiveWidgetMax,
                    broadSearch: false);

                foreach (var (countAddr, _) in hits)
                {
                    long recordAddr = (long)countAddr - bitmapWidth;
                    if (recordAddr < ActiveWidgetMin) continue;

                    var bitmap8 = ReadEightBytes(explorer, recordAddr);
                    if (bitmap8 == null) continue;

                    if (!IsValidBitmapRecord(bitmap8, format, offset, idRangeMax, countCandidate))
                        continue;

                    if (recordAddr < bestAddr)
                    {
                        bestAddr = recordAddr;
                        bestBytes = bitmap8;
                        bestCount = countCandidate;
                    }
                }

                // Early exit: if we found a clean record at one
                // count value, don't bother trying higher counts.
                // Real shop has exactly one count value.
                if (bestBytes != null) break;
            }

            if (bestBytes == null) return null;

            return new FoundRecord
            {
                Category = cat,
                Address = bestAddr,
                Bitmap = bestBytes,
                ItemCount = bestCount,
            };
        }

        /// <summary>
        /// Validate that an IdArray record's bytes form a plausible
        /// shop list: ids in expected range, 0-terminated, all
        /// pointing to real items.
        /// </summary>
        private static bool IsValidIdArrayRecord(byte[] bytes, int offset, int idRangeMax, out int count)
        {
            count = 0;
            int idMax = offset + 31; // id-array categories span ~32 ids
            if (idRangeMax > idMax) idMax = idRangeMax;

            bool sawTerminator = false;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (b == 0)
                {
                    sawTerminator = true;
                    break;
                }
                // Must be in category's id range.
                if (b < offset || b > idMax) return false;
                // Must be a known item.
                if (ItemData.GetItem(b) == null) return false;
                // Sequential or increasing ids (real shops sort by tier).
                if (i > 0 && b <= bytes[i - 1]) return false;
                count++;
            }

            // At least 1 item, terminator before byte 8 (or 8-item
            // record fills all slots).
            if (count == 0) return false;
            if (count > 8) return false;
            if (count < 8 && !sawTerminator) return false;
            return true;
        }

        /// <summary>
        /// Validate that a bitmap record's bytes form a plausible
        /// shop list: popcount matches expected count, all set bits
        /// decode to real items in the category's range.
        /// </summary>
        private static bool IsValidBitmapRecord(
            byte[] bytes,
            ShopStockDecoder.RecordFormat format,
            int offset,
            int idRangeMax,
            int expectedCount)
        {
            // For Bitmap4, only bytes 0-3 are bitmap; bytes 4-7 are
            // the count field (and may be non-zero) so don't decode
            // them.
            int bitmapByteCount = format == ShopStockDecoder.RecordFormat.Bitmap4 ? 4 : 8;

            int popcount = 0;
            for (int b = 0; b < bitmapByteCount; b++)
            {
                byte v = bytes[b];
                for (int bit = 0; bit < 8; bit++)
                {
                    if ((v & (1 << bit)) == 0) continue;
                    int id = b * 8 + bit + offset;
                    if (id > idRangeMax) return false;
                    if (ItemData.GetItem(id) == null) return false;
                    popcount++;
                }
            }

            if (popcount != expectedCount) return false;
            if (popcount == 0) return false;
            return true;
        }

        private static byte[]? ReadEightBytes(MemoryExplorer explorer, long addr)
        {
            try
            {
                var hex = explorer.ReadBlock((nint)addr, 8);
                if (string.IsNullOrEmpty(hex)) return null;
                hex = hex.Replace(" ", "").Replace("-", "");
                if (hex.Length < 16) return null;
                var bytes = new byte[8];
                for (int i = 0; i < 8; i++)
                    bytes[i] = System.Convert.ToByte(hex.Substring(i * 2, 2), 16);
                return bytes;
            }
            catch { return null; }
        }
    }
}
