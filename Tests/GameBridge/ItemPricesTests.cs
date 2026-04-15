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
    public void GetSellPrice_IsHalfBuyPrice()
    {
        // Dagger: buy 200 → sell 100.
        Assert.Equal(100, ItemPrices.GetSellPrice(1));
        // Potion: buy 50 → sell 25.
        Assert.Equal(25, ItemPrices.GetSellPrice(240));
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
}
