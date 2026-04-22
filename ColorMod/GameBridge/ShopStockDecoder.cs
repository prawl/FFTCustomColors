using System;
using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Decodes the current shop's stock from in-memory bitmap records.
    ///
    /// Session 53 cracked the encoding: each shop/category has an 8-byte
    /// bitmap where `bit N of byte B` represents FFTPatcher item id
    /// <c>B*8 + N + categoryOffset</c>. The bitmap record is 128 bytes and
    /// lives in a static game-data table whose base ASLR-relocates each
    /// launch; we locate the active record by AoB-searching for a known
    /// bitmap signature of the currently-open shop.
    ///
    /// Session 54 (this file): verified the same encoding works for
    /// Shields (offset=128) and proved the preamble/count structure is
    /// consistent across categories. The game stores bitmaps for all
    /// (shop, chapter, category) tuples in the static table — we match
    /// by the known-bitmap signature for a specific (shop, chapter)
    /// pair. Once we have a tag that uniquely identifies a shop, this
    /// can be replaced with a tag-based lookup; for now the MVP is:
    /// pick the active shop via caller, look up expected bitmap, verify
    /// count, decode.
    /// </summary>
    public sealed class ShopStockDecoder
    {
        private readonly MemoryExplorer _explorer;

        public ShopStockDecoder(MemoryExplorer explorer)
        {
            _explorer = explorer;
        }

        /// <summary>
        /// Item-ID offset per category — only meaningful for bitmap-
        /// format categories (weapons/daggers). Shield/helm/body/
        /// accessory/consumable records use a different storage
        /// (u8-id-array) where the offset is meaningless.
        /// </summary>
        public enum Category
        {
            Weapons = 0,
            Shields = 1,
            Helms = 2,
            Body = 3,
            Accessories = 4,
            Consumables = 5,
            Daggers = 6
        }

        /// <summary>
        /// Format used by a category's shop record. Session 54
        /// revealed two distinct encodings:
        /// <list>
        /// <item><c>Bitmap8</c> — 8-byte bitmap + 4-byte count
        ///   (staves/ranged at offset 42).</item>
        /// <item><c>Bitmap4</c> — 4-byte bitmap + 4-byte count
        ///   (daggers at offset 1).</item>
        /// <item><c>IdArray</c> — 8-byte u8-id array, 0-terminated
        ///   (shields at offset 0, others TBD).</item>
        /// </list>
        /// </summary>
        public enum RecordFormat
        {
            Bitmap8 = 0,
            Bitmap4 = 1,
            IdArray = 2
        }

        public static int OffsetForCategory(Category cat) => cat switch
        {
            Category.Weapons => 42,
            Category.Shields => 128,
            Category.Helms => 135,
            Category.Body => 186,   // clothing starts at id 186
            Category.Accessories => 208,   // shoes/accessories start at id 208
            Category.Consumables => 240,   // chemistitem starts at id 240
            Category.Daggers => 1,
            _ => 0
        };

        /// <summary>
        /// Record format used by each category. Session 54 live-
        /// verified values; unverified categories default to
        /// Bitmap8 (the Dorter-weapons canonical scheme).
        /// </summary>
        public static RecordFormat FormatForCategory(Category cat) => cat switch
        {
            Category.Weapons => RecordFormat.Bitmap8,
            Category.Daggers => RecordFormat.Bitmap4,
            Category.Shields => RecordFormat.IdArray,
            Category.Helms => RecordFormat.IdArray,
            // Body: Bitmap4 format (like daggers) at offset 186.
            // Session 54 Yardrow Body tab verified: bitmap
            // `7F 00 00 00` + count=7 decodes to ids 186-192
            // (Clothing, Leather Clothing, Leather Plate, Ringmail,
            // Mythril Vest, Adamant Vest, Wizard Clothing).
            Category.Body => RecordFormat.Bitmap4,
            // Accessories: Bitmap4 format at offset 208.
            // Session 54 Yardrow Accessories tab verified: bitmap
            // `7F 00 00 00` + count=7 decodes to ids 208-214
            // (Battle/Spiked/Germinas/Rubber/Winged Boots + Hermes
            // Shoes + Red Shoes).
            Category.Accessories => RecordFormat.Bitmap4,
            // Consumables: 8-byte bitmap + u64 count + pad u32.
            // Session 54 tour confirmed Dorter/Yardrow Ch1 consumables
            // bitmap `5F 00 00 00 00 00 00 00` (bits 0-4 and 6) at
            // offset 240 decodes to ids 240-244, 246 = Potion,
            // Hi-Potion, X-Potion, Ether, Hi-Ether, Antidote. Uses
            // Bitmap8 format but with an 8-byte count (vs 4-byte
            // for weapons). Close enough that reusing Bitmap8
            // semantics works; the extra 4 bytes of count are just
            // padding the locator ignores.
            Category.Consumables => RecordFormat.Bitmap8,
            _ => RecordFormat.Bitmap8
        };

        /// <summary>
        /// Decode an 8-byte bitmap into a list of item IDs using the
        /// offset formula <c>id = byte_index * 8 + bit + offset</c>.
        /// </summary>
        public static List<int> DecodeBitmap(byte[] bitmap, int idOffset)
        {
            var ids = new List<int>();
            if (bitmap == null) return ids;
            for (int b = 0; b < bitmap.Length; b++)
            {
                byte v = bitmap[b];
                if (v == 0) continue;
                for (int bit = 0; bit < 8; bit++)
                {
                    if ((v & (1 << bit)) != 0)
                        ids.Add(b * 8 + bit + idOffset);
                }
            }
            return ids;
        }

        /// <summary>
        /// Decode an 8-byte u8-ID array into a list of item IDs. Zero
        /// bytes terminate (not valid stock ids). Session 54 end-of-
        /// game tour revealed shields and helms use this format
        /// (rather than bitmaps): each byte is a direct FFTPatcher
        /// item id, 0 marks end-of-list. Up to 8 items per record.
        /// Example: <c>80 81 82 83 84 85 86 00</c> decodes to ids
        /// 128-134 (7 items, the 8th slot terminates).
        /// </summary>
        public static List<int> DecodeIdArray(byte[] idArray)
        {
            var ids = new List<int>();
            if (idArray == null) return ids;
            foreach (var b in idArray)
            {
                if (b == 0) break;
                ids.Add(b);
            }
            return ids;
        }

        /// <summary>
        /// Locate a bitmap record for a given category+expected-bitmap
        /// signature in the static game-data table by AoB search.
        /// Returns the address of the bitmap start (where count=expectedCount
        /// follows at +0x08 for 8-byte bitmaps or +0x04 for 4-byte
        /// bitmaps). Returns 0 if not found.
        ///
        /// Session 54 cracked the bitmap-count pairing. Session with
        /// Sal-Ghidos-through-Gariland tour revealed TWO bitmap widths:
        /// - 8-byte: staves/rods (Dorter-style, bitmap+offset 42)
        /// - 4-byte: daggers (Gariland-style, bitmap+offset 1)
        /// Whichever format matches first wins; callers supply the
        /// expected bitmap padded to 8 bytes (high 4 zero for daggers).
        /// </summary>
        public long LocateBitmapRecord(byte[] expectedBitmap, int expectedCount)
        {
            if (expectedBitmap == null || expectedBitmap.Length != 8) return 0;

            // Format A (8-byte bitmap + 4-byte count) — staves, ranged, etc.
            var pat8 = new byte[12];
            Array.Copy(expectedBitmap, 0, pat8, 0, 8);
            pat8[8] = (byte)(expectedCount & 0xFF);
            pat8[9] = (byte)((expectedCount >> 8) & 0xFF);
            pat8[10] = 0;
            pat8[11] = 0;

            // Format B (4-byte bitmap + 4-byte count) — daggers. Only
            // meaningful when high 4 bytes of bitmap are zero, i.e.
            // all ids fall in a 32-bit range from the offset base.
            byte[]? pat4 = null;
            bool canTry4Byte = expectedBitmap[4] == 0 && expectedBitmap[5] == 0
                              && expectedBitmap[6] == 0 && expectedBitmap[7] == 0;
            if (canTry4Byte)
            {
                pat4 = new byte[8];
                Array.Copy(expectedBitmap, 0, pat4, 0, 4);
                pat4[4] = (byte)(expectedCount & 0xFF);
                pat4[5] = (byte)((expectedCount >> 8) & 0xFF);
                pat4[6] = 0;
                pat4[7] = 0;
            }

            // Two-phase search. Phase 1 (narrow) covers private/heap
            // up to 4MB regions. Phase 2 (broad) covers readable
            // memory-mapped + large heap regions. Try 8-byte format
            // first (canonical), fall back to 4-byte.
            List<(nint address, string context)> hits;

            hits = _explorer.SearchBytesInAllMemory(pat8, 4, 0L, 0x800000000L, broadSearch: false);
            if (hits.Count == 0)
                hits = _explorer.SearchBytesInAllMemory(pat8, 4, 0L, 0x800000000L, broadSearch: true);

            if (hits.Count == 0 && pat4 != null)
            {
                hits = _explorer.SearchBytesInAllMemory(pat4, 4, 0L, 0x800000000L, broadSearch: false);
                if (hits.Count == 0)
                    hits = _explorer.SearchBytesInAllMemory(pat4, 4, 0L, 0x800000000L, broadSearch: true);
            }

            if (hits.Count == 0) return 0;

            // Prefer lowest address (static table wins over heap).
            long best = long.MaxValue;
            foreach (var (addr, _) in hits)
                if ((long)addr < best) best = (long)addr;
            return best == long.MaxValue ? 0 : best;
        }

        /// <summary>
        /// Locate an id-array shop record (shields/helms/body/
        /// accessories/consumables format). Session 54 tour revealed
        /// the active widget stores 8 u8 ids directly (0-terminated)
        /// preceded by a vtable and count. Caller supplies the first
        /// N sorted expected ids; we search for that byte sequence
        /// followed by a 0 terminator.
        /// </summary>
        public long LocateIdArrayRecord(byte[] expectedIds)
        {
            if (expectedIds == null || expectedIds.Length == 0 || expectedIds.Length > 8) return 0;
            // Build search pattern: ids + 0 terminator.
            var pattern = new byte[expectedIds.Length + 1];
            Array.Copy(expectedIds, 0, pattern, 0, expectedIds.Length);
            pattern[expectedIds.Length] = 0;

            var hits = _explorer.SearchBytesInAllMemory(pattern, 4, 0L, 0x800000000L, broadSearch: false);
            if (hits.Count == 0)
                hits = _explorer.SearchBytesInAllMemory(pattern, 4, 0L, 0x800000000L, broadSearch: true);
            if (hits.Count == 0) return 0;

            // Prefer lowest address (static table over heap). For
            // id-array categories the static records are at
            // `0x579xxxx`-ish range; heap copies drift per tab
            // switch so prefer the deterministic static one.
            long best = long.MaxValue;
            foreach (var (addr, _) in hits)
                if ((long)addr < best) best = (long)addr;
            return best == long.MaxValue ? 0 : best;
        }

        /// <summary>
        /// Read an 8-byte bitmap from a given address. Handles the
        /// space-delimited format returned by <see cref="MemoryExplorer.ReadBlock"/>.
        /// </summary>
        public byte[]? ReadBitmap(long addr)
        {
            var hex = _explorer.ReadBlock((nint)addr, 8);
            if (string.IsNullOrEmpty(hex)) return null;
            hex = hex.Replace(" ", "").Replace("-", "");
            if (hex.Length < 16) return null;
            var bytes = new byte[8];
            for (int i = 0; i < 8; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        /// <summary>
        /// High-level: decode the stock at a given record address for
        /// a given category, returning {id, name, price, type}
        /// tuples. Dispatches to the right decode based on
        /// <see cref="FormatForCategory"/> — bitmap categories use
        /// <see cref="DecodeBitmap"/>, id-array categories use
        /// <see cref="DecodeIdArray"/>. When <paramref name="location"/>
        /// and <paramref name="chapter"/> are supplied (non-negative),
        /// prices go through
        /// <see cref="ChapterShopPrices.GetBuyPrice"/> so chapter-
        /// specific overrides take effect; otherwise the end-game
        /// fallback <see cref="ItemPrices.GetBuyPrice"/> is used.
        /// </summary>
        public List<ShopStockItem> DecodeStockAt(long recordAddr, Category cat, int location = -1, int chapter = -1, int expectedCount = -1)
        {
            var result = new List<ShopStockItem>();
            var raw = ReadBitmap(recordAddr);
            if (raw == null) return result;

            List<int> ids;
            var format = FormatForCategory(cat);
            if (format == RecordFormat.IdArray)
            {
                ids = DecodeIdArray(raw);
            }
            else if (format == RecordFormat.Bitmap4)
            {
                // Bitmap4: only the low 4 bytes are bitmap bits; the
                // high 4 bytes are the record's count field. Decoding
                // all 8 bytes as bitmap produces phantom items at
                // offset-sensitive positions (e.g. Gariland
                // Accessories picking up Potion/Hi-Potion/X-Potion
                // from the count=7 field at byte 4). Zero the high 4
                // before decoding so the count bytes can't masquerade
                // as set bits.
                var bitmapOnly = new byte[8];
                System.Array.Copy(raw, 0, bitmapOnly, 0, 4);
                ids = DecodeBitmap(bitmapOnly, OffsetForCategory(cat));
            }
            else
            {
                ids = DecodeBitmap(raw, OffsetForCategory(cat));
            }

            foreach (var id in ids)
            {
                var item = ItemData.GetItem(id);
                if (item == null) continue;
                int? price = (location >= 0 && chapter >= 0)
                    ? ChapterShopPrices.GetBuyPrice(location, chapter, id)
                    : ItemPrices.GetBuyPrice(id);
                result.Add(new ShopStockItem
                {
                    Id = id,
                    Name = item.Name,
                    Type = item.Type,
                    BuyPrice = price
                });
            }

            return ValidateAgainstExpected(result, expectedCount);
        }

        /// <summary>
        /// Session 55 fix: when caller passes <paramref name="expectedCount"/>
        /// (>=0), reject decoded stock lists whose item count
        /// doesn't match. Mismatches indicate the located record
        /// is a false positive (e.g. transient memory region whose
        /// bytes 4-7 leak into bitmap bits as phantom IDs at decode
        /// time). Returning empty rather than partial/wrong data
        /// lets callers distinguish "no stock" from "wrong stock".
        /// Verified live at Lesalia/Warjilis Consumables: 8-item
        /// false positives (real 6 + 2 phantom weapons) get dropped
        /// to empty rather than surfacing wrong data. When
        /// <paramref name="expectedCount"/> is negative, the input
        /// list is returned unchanged (preserves the un-validated
        /// API for callers that don't have a registry expected
        /// count to compare against).
        /// </summary>
        public static List<ShopStockItem> ValidateAgainstExpected(List<ShopStockItem> stock, int expectedCount)
        {
            if (expectedCount < 0) return stock;
            if (stock.Count != expectedCount) return new List<ShopStockItem>();
            return stock;
        }
    }

    /// <summary>
    /// Single row in a decoded shop stock list.
    /// </summary>
    public sealed class ShopStockItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public int? BuyPrice { get; set; }
    }
}
