using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Reads the player's inventory (owned-count-per-item) from the static
    /// u8 array at 0x1411A17C0. Each byte is the count owned of the item
    /// whose FFTPatcher canonical ID equals the byte's index.
    ///
    /// The array sits exactly 272 bytes (0x110) before the roster base at
    /// 0x1411A18D0 — so it's part of the same persistent save-state region,
    /// NOT heap-allocated widget data. Stable within a session, likely
    /// stable across sessions (verify when this reader first runs on a
    /// fresh boot).
    ///
    /// Discovered 2026-04-15 session 18 via 2-snapshot buy-diff:
    /// buying 1 Dagger flipped byte at 0x1411A17C1 from 3 → 4 and no
    /// other candidate in a 175-byte diff. See memory note
    /// project_inventory_store_CRACKED.md.
    ///
    /// This unblocks: Items/Throw/Iaido skillset held counts, shop buy/sell
    /// "own" column, EquippableWeapons picker availability, change_*_to
    /// validation, and PartyMenuInventory full listing.
    /// </summary>
    public class InventoryReader
    {
        public const long InventoryBase = 0x1411A17C0;
        public const int InventorySize = 272;

        private readonly MemoryExplorer _explorer;

        public InventoryReader(MemoryExplorer explorer)
        {
            _explorer = explorer;
        }

        /// <summary>
        /// An item owned by the player: ID, count, and display metadata
        /// pulled from ItemData.cs. Name is null for IDs we haven't mapped
        /// yet (items above ItemData.cs's coverage). SellPrice is null for
        /// items without a known buy price (story drops, unique gear).
        /// </summary>
        public class InventoryEntry
        {
            public int ItemId { get; set; }
            public int Count { get; set; }
            public string? Name { get; set; }
            public string? Type { get; set; }
            public int? SellPrice { get; set; }
        }

        /// <summary>
        /// Read all 272 bytes as a raw array. bytes[id] = owned count.
        /// Returns null if the read failed.
        /// </summary>
        public byte[]? ReadRaw()
        {
            if (_explorer == null) return null;
            try
            {
                var bytes = _explorer.Scanner.ReadBytes((nint)InventoryBase, InventorySize);
                if (bytes == null || bytes.Length < InventorySize) return null;
                return bytes;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Read the inventory and return every non-zero entry, with item
        /// metadata from ItemData.cs if available. Ordered by item ID.
        /// Skips entries whose count is 0.
        /// </summary>
        public List<InventoryEntry> ReadAll()
        {
            var bytes = ReadRaw();
            if (bytes == null) return new List<InventoryEntry>();
            return DecodeRaw(bytes);
        }

        /// <summary>
        /// Pure decode from a raw 272-byte inventory array to structured
        /// entries. Extracted for unit testing — the memory-read side is
        /// handled by ReadAll(). Ordered by item ID; zero counts skipped.
        /// SellPrice is populated via <see cref="ItemPrices.GetSellPrice"/>
        /// (null for items without a known buy price).
        /// </summary>
        public static List<InventoryEntry> DecodeRaw(byte[] bytes)
        {
            var results = new List<InventoryEntry>();
            if (bytes == null) return results;

            for (int id = 0; id < bytes.Length; id++)
            {
                int count = bytes[id];
                if (count == 0) continue;
                var info = ItemData.GetItem(id);
                results.Add(new InventoryEntry
                {
                    ItemId = id,
                    Count = count,
                    Name = info?.Name,
                    Type = info?.Type,
                    SellPrice = ItemPrices.GetSellPrice(id),
                });
            }
            return results;
        }

        /// <summary>
        /// Return only entries that have a known sell price — i.e. items
        /// the player can sell at an Outfitter for a known amount of gil.
        /// Excludes story drops and unique equipment whose sell price is
        /// unknown. Use this for the Outfitter Sell screen listing.
        /// </summary>
        public List<InventoryEntry> ReadSellable()
        {
            var all = ReadAll();
            return all.FindAll(e => e.SellPrice.HasValue);
        }

        /// <summary>
        /// Read the owned count for a specific item ID. Returns 0 if the
        /// item is not owned, or null if the read failed entirely.
        /// </summary>
        public int? GetCount(int itemId)
        {
            if (itemId < 0 || itemId >= InventorySize) return 0;
            var bytes = ReadRaw();
            if (bytes == null) return null;
            return bytes[itemId];
        }

        /// <summary>
        /// Filter ReadAll() by one or more type strings (e.g. "sword",
        /// "knightsword", "helmet"). Useful for picker rendering where
        /// you want only items that fit a specific equipment slot.
        /// </summary>
        public List<InventoryEntry> ReadByType(params string[] types)
        {
            var all = ReadAll();
            if (types == null || types.Length == 0) return all;
            var typeSet = new HashSet<string>(types, System.StringComparer.OrdinalIgnoreCase);
            var filtered = new List<InventoryEntry>();
            foreach (var e in all)
            {
                if (e.Type != null && typeSet.Contains(e.Type))
                    filtered.Add(e);
            }
            return filtered;
        }
    }
}
