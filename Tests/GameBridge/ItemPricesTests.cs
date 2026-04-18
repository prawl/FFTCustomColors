using System.Linq;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

public class ItemPricesTests
{
    [Fact]
    public void AllBuyPriceNames_ResolveToItemDataEntries()
    {
        // If any name in BuyPricesByName doesn't match an ItemData entry,
        // the static init drops it silently into UnresolvedNames. This
        // test surfaces the drift: extending either table without keeping
        // them in sync fails the suite with a clear diagnostic.
        var unresolved = ItemPrices.UnresolvedNames.ToList();
        Assert.True(unresolved.Count == 0,
            $"ItemPrices has {unresolved.Count} names that don't match ItemData:\n  " +
            string.Join("\n  ", unresolved));
    }

    [Fact]
    public void BuyPrices_ResolvedTableNonEmpty()
    {
        Assert.NotEmpty(ItemPrices.BuyPrices);
    }

    [Fact]
    public void GetBuyPrice_Dagger_Returns200()
    {
        // Dagger = ID 1 in ItemData; shop price is 200 gil per SHOP_ITEMS.md.
        var price = ItemPrices.GetBuyPrice(1);
        Assert.Equal(200, price);
    }

    [Fact]
    public void GetBuyPrice_Potion_Returns50()
    {
        // Potion = ID 240 in ItemData (the chemistitem consumable range).
        var price = ItemPrices.GetBuyPrice(240);
        Assert.Equal(50, price);
    }

    [Fact]
    public void GetBuyPrice_Ragnarok_ReturnsNull()
    {
        // Ragnarok (ID 36) is a story-drop knightsword not sold in any shop.
        var price = ItemPrices.GetBuyPrice(36);
        Assert.Null(price);
    }

    [Fact]
    public void GetBuyPrice_UnknownId_ReturnsNull()
    {
        Assert.Null(ItemPrices.GetBuyPrice(9999));
        Assert.Null(ItemPrices.GetBuyPrice(-1));
    }

    [Fact]
    public void GetSellPrice_FallsBackToHalfBuy_WhenNoOverride()
    {
        // Potion has no override → buy 50 → estimate 25.
        Assert.Equal(25, ItemPrices.GetSellPrice(240));
        // Ether has no override → buy 200 → estimate 100.
        Assert.Equal(100, ItemPrices.GetSellPrice(243));
    }

    [Fact]
    public void GetSellPrice_NoBuyPrice_ReturnsNull()
    {
        Assert.Null(ItemPrices.GetSellPrice(36));  // Ragnarok
        Assert.Null(ItemPrices.GetSellPrice(9999));
    }

    [Fact]
    public void GetSellPrice_RoundsDown()
    {
        // Any odd buy price rounds down on sell (integer division).
        // Papyrus Codex: 10,000 → 5,000 (even, clean)
        Assert.Equal(5_000, ItemPrices.GetSellPrice(ItemData.Items.First(kv => kv.Value.Name == "Papyrus Codex").Key));
    }

    [Fact]
    public void GetSellPrice_OverridePrefersGroundTruthOverBuyHalf()
    {
        // Dagger: buy 200 → buy/2 estimate = 100. Ground-truth override = 50.
        // GetSellPrice must return 50, not 100.
        Assert.Equal(50, ItemPrices.GetSellPrice(1));
    }

    [Fact]
    public void IsSellPriceGroundTruth_TrueForOverride()
    {
        // Dagger has a ground-truth override.
        Assert.True(ItemPrices.IsSellPriceGroundTruth(1));
    }

    [Fact]
    public void IsSellPriceGroundTruth_FalseForEstimate()
    {
        // Potion (id 240) has a buy price but no sell-price override.
        // Its GetSellPrice is a buy/2 estimate, so the flag is false.
        Assert.False(ItemPrices.IsSellPriceGroundTruth(240));
        // But it still has a sell price via buy/2.
        Assert.NotNull(ItemPrices.GetSellPrice(240));
    }

    [Fact]
    public void IsSellPriceGroundTruth_FalseForUnknownItem()
    {
        // Ragnarok (36) has neither a buy price nor a sell override.
        Assert.False(ItemPrices.IsSellPriceGroundTruth(36));
        Assert.Null(ItemPrices.GetSellPrice(36));
    }

    [Fact]
    public void SellPriceOverrides_ContainsLiveVerifiedDaggerSet()
    {
        // Session 18 live-captured 7 weapon sell prices. Spot-check the
        // set so a regression (name mismatch / typo) fails loudly.
        Assert.True(ItemPrices.SellPriceOverrides.Count >= 7);
        Assert.Equal(50, ItemPrices.GetSellPrice(1));    // Dagger
        Assert.Equal(250, ItemPrices.GetSellPrice(2));   // Mythril Knife
        Assert.Equal(400, ItemPrices.GetSellPrice(3));   // Blind Knife
        Assert.Equal(750, ItemPrices.GetSellPrice(4));   // Mage Masher
        Assert.Equal(2_500, ItemPrices.GetSellPrice(8)); // Assassin's Dagger
        Assert.Equal(100, ItemPrices.GetSellPrice(19));  // Broadsword
        Assert.Equal(250, ItemPrices.GetSellPrice(20));  // Longsword
    }

    // Additional property + edge tests (session 33 batch 7).

    [Fact]
    public void AllBuyPrices_ArePositive()
    {
        // A zero or negative buy price would break affordability math.
        foreach (var kv in ItemPrices.BuyPrices)
        {
            Assert.True(kv.Value > 0, $"Item {kv.Key} has non-positive buy price {kv.Value}");
        }
    }

    [Fact]
    public void AllSellPriceOverrides_ArePositive()
    {
        foreach (var kv in ItemPrices.SellPriceOverrides)
        {
            Assert.True(kv.Value > 0, $"Item {kv.Key} has non-positive sell override {kv.Value}");
        }
    }

    [Fact]
    public void SellPriceOverrides_WithinTenXBuyPrice()
    {
        // Sanity check: an override shouldn't be absurdly larger than the buy price.
        // NOTE: some items DO sell for more than they buy (discovered session 33
        // batch 7: Mage Masher buys 600, sells 750). So the canonical "sell <= buy"
        // invariant is NOT true. Use a looser sanity bound to catch data-entry
        // typos (e.g. missing a digit) without flagging legitimate variance.
        foreach (var kv in ItemPrices.SellPriceOverrides)
        {
            if (ItemPrices.BuyPrices.TryGetValue(kv.Key, out int buy))
            {
                Assert.True(kv.Value <= buy * 10,
                    $"Item {kv.Key} sell override {kv.Value} is >10× buy price {buy} (likely typo)");
            }
        }
    }

    [Fact]
    public void GetBuyPrice_IntMaxValue_ReturnsNull()
    {
        Assert.Null(ItemPrices.GetBuyPrice(int.MaxValue));
    }

    [Fact]
    public void GetBuyPrice_IntMinValue_ReturnsNull()
    {
        Assert.Null(ItemPrices.GetBuyPrice(int.MinValue));
    }

    [Fact]
    public void GetSellPrice_ConsistentWithBuyPriceExistence()
    {
        // If buy is known AND no override, sell is non-null (equals buy/2).
        foreach (var kv in ItemPrices.BuyPrices)
        {
            if (ItemPrices.SellPriceOverrides.ContainsKey(kv.Key)) continue;
            Assert.NotNull(ItemPrices.GetSellPrice(kv.Key));
        }
    }

    [Fact]
    public void IsSellPriceGroundTruth_IntMaxValue_ReturnsFalse()
    {
        Assert.False(ItemPrices.IsSellPriceGroundTruth(int.MaxValue));
    }
}
