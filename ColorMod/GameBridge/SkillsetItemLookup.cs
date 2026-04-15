using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Maps consumable-backed ability names to the FFTPatcher inventory
    /// item ID(s) they consume. Used by the battle ability surfacing to
    /// attach a live held-count from <see cref="InventoryReader"/> to
    /// Chemist Items / Ninja Throw / Samurai Iaido entries.
    ///
    /// Three skillsets behave differently:
    ///
    /// <list type="bullet">
    /// <item><b>Items (Chemist)</b>: one ability = one specific item.
    /// Map: ability name → single item ID.
    /// Count = inventory[itemId]. Unusable when count == 0.</item>
    ///
    /// <item><b>Throw (Ninja)</b>: one ability = ONE WEAPON TYPE (knife,
    /// ninjablade, sword, katana, etc.). Throwing consumes any one weapon
    /// of that type. Map: ability name → type string.
    /// Count = sum of inventory[id] for all items where ItemData[id].Type == type.
    /// Unusable when sum == 0.</item>
    ///
    /// <item><b>Iaido (Samurai)</b>: one ability = drawing a specific
    /// katana's power. Each use has a ~1/8 chance to break the katana.
    /// Map: ability name → single katana item ID (same shape as Items).
    /// Count = inventory[katanaId]. Unusable when count == 0.</item>
    /// </list>
    ///
    /// Names must match <see cref="ActionAbilityLookup.AbilitiesBySkillsetName"/>
    /// exactly. A unit test verifies each ability in each skillset has a
    /// corresponding entry here (or is explicitly documented as excluded).
    /// </summary>
    public static class SkillsetItemLookup
    {
        /// <summary>
        /// Chemist Items skillset. Maps the ability name (as used in
        /// ActionAbilityLookup) to the inventory item ID.
        /// </summary>
        public static readonly Dictionary<string, int> ItemsAbilityToItemId = new()
        {
            // FFTPatcher consumable item IDs (240-253 range). These are
            // the IDs that live in the inventory byte array at 0x1411A17C0.
            ["Potion"]        = 240,
            ["Hi-Potion"]     = 241,
            ["X-Potion"]      = 242,
            ["Ether"]         = 243,
            ["Hi-Ether"]      = 244,
            ["Elixir"]        = 245,
            ["Antidote"]      = 246,
            ["Eye Drop"]      = 247,   // ActionAbilityLookup's "Eye Drop" (singular)
            ["Eye Drops"]     = 247,   // ItemData's "Eye Drops" (plural) — accept either
            ["Echo Herbs"]    = 248,
            ["Maiden's Kiss"] = 249,
            ["Gold Needle"]   = 250,
            ["Holy Water"]    = 251,
            ["Remedy"]        = 252,
            ["Phoenix Down"]  = 253,
        };

        /// <summary>
        /// Samurai Iaido skillset. Each ability draws power from a specific
        /// katana item. Uses the same shape as Items. Katana IDs match
        /// <see cref="ItemData"/>'s katana block (38..47).
        /// </summary>
        public static readonly Dictionary<string, int> IaidoAbilityToItemId = new()
        {
            ["Ashura"]          = 38,
            ["Kotetsu"]         = 39,
            ["Osafune"]         = 40,
            ["Murasame"]        = 41,
            ["Ama-no-Murakumo"] = 42,
            ["Kiyomori"]        = 43,
            ["Muramasa"]        = 44,
            ["Kiku-ichimonji"]  = 45,
            ["Masamune"]        = 46,
            ["Chirijiraden"]    = 47,
        };

        /// <summary>
        /// Ninja Throw skillset. Each ability throws any weapon of the
        /// named type. Values are ItemData type strings (see the SubTypeNames
        /// array in <see cref="ItemData"/>).
        /// </summary>
        public static readonly Dictionary<string, string> ThrowAbilityToItemType = new()
        {
            ["Shuriken"]    = "throwing",  // shuriken/ninja-throwing weapons
            ["Bomb"]        = "bomb",
            ["Knife"]       = "knife",
            ["Ninja Blade"] = "ninjablade",
            ["Sword"]       = "sword",
            ["Hammer"]      = "flail",     // FFT groups hammers under flail type
            ["Polearm"]     = "polearm",
            ["Staff"]       = "staff",
            ["Stick"]       = "rod",       // "stick"-named ability actually throws rods
            ["Book"]        = "book",
        };

        /// <summary>
        /// Returns the total held count for a throw-type ability by summing
        /// inventory entries whose type matches. Given an already-read
        /// inventory byte array (272 bytes) this is cheap — one pass.
        /// </summary>
        public static int GetThrowTypeCount(byte[] inventoryBytes, string itemType)
        {
            if (inventoryBytes == null || inventoryBytes.Length == 0) return 0;
            int total = 0;
            for (int id = 0; id < inventoryBytes.Length; id++)
            {
                int count = inventoryBytes[id];
                if (count == 0) continue;
                var info = ItemData.GetItem(id);
                if (info != null && string.Equals(info.Type, itemType, System.StringComparison.OrdinalIgnoreCase))
                    total += count;
            }
            return total;
        }

        /// <summary>
        /// Convenience: given an ability name and a skillset, returns the
        /// held count. Returns null when the skillset/name combination
        /// isn't inventory-gated (regular abilities return null so callers
        /// know to skip the heldCount field entirely).
        /// </summary>
        public static int? TryGetHeldCount(string skillsetName, string abilityName, byte[] inventoryBytes)
        {
            if (inventoryBytes == null) return null;
            switch (skillsetName)
            {
                case "Items":
                    if (ItemsAbilityToItemId.TryGetValue(abilityName, out var itemId))
                        return itemId < inventoryBytes.Length ? inventoryBytes[itemId] : 0;
                    return null;
                case "Iaido":
                    if (IaidoAbilityToItemId.TryGetValue(abilityName, out var katanaId))
                        return katanaId < inventoryBytes.Length ? inventoryBytes[katanaId] : 0;
                    return null;
                case "Throw":
                    if (ThrowAbilityToItemType.TryGetValue(abilityName, out var type))
                        return GetThrowTypeCount(inventoryBytes, type);
                    return null;
                default:
                    return null;
            }
        }
    }
}
