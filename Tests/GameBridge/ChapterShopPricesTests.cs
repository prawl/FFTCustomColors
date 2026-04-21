using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

public class ChapterShopPricesTests
{
    [Theory]
    [InlineData(7)]   // Yardrow
    [InlineData(8)]   // Gollund
    [InlineData(9)]   // Dorter
    [InlineData(13)]  // Bervenia
    [InlineData(14)]  // Sal Ghidos
    public void Lookup_Ch1DiscountShops_WhiteStaffReturns400(int location)
    {
        // Session 54 screenshot-verified: Dorter, Yardrow, Gollund,
        // Bervenia, and Sal Ghidos all sell White Staff at the Ch1
        // discount price of 400 (vs end-game 800). Zaland and
        // Warjilis sell it at full price in Ch1, so they get no
        // override.
        var p = ChapterShopPrices.Lookup(location, 1, 60);
        Assert.Equal(400, p);
    }

    [Theory]
    [InlineData(7)]   // Yardrow
    [InlineData(8)]   // Gollund
    [InlineData(9)]   // Dorter
    [InlineData(13)]  // Bervenia
    [InlineData(14)]  // Sal Ghidos
    public void Lookup_Ch1DiscountShops_SerpentStaffReturns1200(int location)
    {
        // Session 54: Serpent Staff at Ch1 discount = 1200
        // (vs end-game 2200).
        var p = ChapterShopPrices.Lookup(location, 1, 62);
        Assert.Equal(1200, p);
    }

    [Theory]
    [InlineData(10)]  // Zaland
    [InlineData(12)]  // Warjilis
    public void Lookup_Ch1FullPriceShops_WhiteStaffReturnsNull(int location)
    {
        // Zaland and Warjilis sell White Staff at full 800 gil even
        // in Ch1 — same as end-game fallback, so intentionally no
        // override. Null here forces the caller into the ItemPrices
        // fallback path.
        var p = ChapterShopPrices.Lookup(location, 1, 60);
        Assert.Null(p);
    }

    [Fact]
    public void Lookup_DorterCh1OakStaff_ReturnsNull()
    {
        // Oak Staff (id 59) at Dorter Ch1 costs 120 — which matches
        // the end-game fallback price. We intentionally DON'T
        // override when chapter-specific = end-game so the override
        // table stays minimal.
        var p = ChapterShopPrices.Lookup(9, 1, 59);
        Assert.Null(p);
    }

    [Fact]
    public void GetBuyPrice_DorterCh1WhiteStaff_Returns400()
    {
        var p = ChapterShopPrices.GetBuyPrice(9, 1, 60);
        Assert.Equal(400, p);
    }

    [Fact]
    public void GetBuyPrice_ZalandCh1WhiteStaff_FallsBackTo800()
    {
        // Zaland has no override → fallback to ItemPrices end-game = 800.
        var p = ChapterShopPrices.GetBuyPrice(10, 1, 60);
        Assert.Equal(800, p);
    }

    [Fact]
    public void GetBuyPrice_DorterCh1OakStaff_FallsBackToItemPrices()
    {
        // No Dorter Ch1 override for Oak Staff (id 59); falls back
        // to ItemPrices.GetBuyPrice which holds 120.
        var p = ChapterShopPrices.GetBuyPrice(9, 1, 59);
        Assert.Equal(120, p);
    }

    [Fact]
    public void GetBuyPrice_UnmappedShop_FallsBackToItemPrices()
    {
        var p = ChapterShopPrices.GetBuyPrice(99, 9, 59);
        Assert.Equal(120, p);
    }

    [Theory]
    [InlineData(0, 1, 100)]   // Lesalia, Dagger
    [InlineData(1, 1, 100)]   // Riovanes, Dagger
    [InlineData(2, 1, 100)]   // Eagrose, Dagger
    [InlineData(3, 1, 100)]   // Lionel, Dagger
    [InlineData(4, 1, 100)]   // Limberry, Dagger
    [InlineData(5, 1, 100)]   // Zeltennia, Dagger
    [InlineData(6, 1, 100)]   // Gariland, Dagger
    [InlineData(0, 5, 1500)]  // Lesalia, Platinum Dagger
    [InlineData(6, 6, 2500)]  // Gariland, Main Gauche
    [InlineData(3, 7, 4000)]  // Lionel, Orichalcum Dirk
    public void Lookup_DaggerShops_Ch1Prices(int location, int itemId, int expectedPrice)
    {
        // Session 54 tour: all 7 dagger shops share identical Ch1
        // pricing (Dagger 100 / Mythril 200 / Blind 300 / Mage
        // Masher 700 / Platinum 1500 / Main Gauche 2500 / Orichalcum
        // 4000). Populated via static ctor loop in ChapterShopPrices.
        var p = ChapterShopPrices.Lookup(location, 1, itemId);
        Assert.Equal(expectedPrice, p);
    }

    [Fact]
    public void Lookup_DorterCh1BronzeShield_Returns1000()
    {
        // Session 54 screenshot: Bronze Shield (id 130) at Dorter
        // Ch1 = 1000 gil (vs end-game 1200). Only Ch1 shield with
        // a differing price; other 6 shields match ItemPrices
        // fallback so no overrides.
        var p = ChapterShopPrices.Lookup(9, 1, 130);
        Assert.Equal(1000, p);
    }

    [Fact]
    public void GetBuyPrice_DorterCh1Escutcheon_FallsBack400()
    {
        // Escutcheon (id 128) at Dorter Ch1 = 400, same as end-game
        // fallback. No override needed; falls through to ItemPrices.
        var p = ChapterShopPrices.GetBuyPrice(9, 1, 128);
        Assert.Equal(400, p);
    }
}
