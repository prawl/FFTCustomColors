using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

public class ShopStockResolverTests
{
    // CountBits pins the expected-count contract used by
    // LocateBitmapRecord. If the bit-counting logic ever drifts, the
    // memory locator starts searching for the wrong record pattern
    // and the whole shop-stock decode path silently returns empty.
    // These tests catch that regression without a live game.

    [Fact]
    public void CountBits_DorterCh1Staves_Returns7()
    {
        // `00 06 76 00 00 00 00 00` — staves bitmap. Should count to
        // 7 (the in-game display at all 7 staves shops).
        var bmp = new byte[] { 0x00, 0x06, 0x76, 0x00, 0x00, 0x00, 0x00, 0x00 };
        Assert.Equal(7, ShopStockResolver.CountBits(bmp));
    }

    [Fact]
    public void CountBits_DorterCh1Consumables_Returns6()
    {
        // `5F` = 0b01011111 — 6 bits set. Consumables Ch1 Potion
        // through Antidote are 6 items, matching the in-game
        // display.
        var bmp = new byte[] { 0x5F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        Assert.Equal(6, ShopStockResolver.CountBits(bmp));
    }

    [Fact]
    public void CountBits_EmptyBitmap_ReturnsZero()
    {
        Assert.Equal(0, ShopStockResolver.CountBits(new byte[8]));
    }

    [Fact]
    public void CountBits_NullBitmap_ReturnsZero()
    {
        // Graceful handling — resolver calls this during an auto-mode
        // iteration that may have null lookups in future chapter
        // tuples. Crashing here would take down the whole screen
        // response.
        Assert.Equal(0, ShopStockResolver.CountBits(null!));
    }

    [Fact]
    public void CountBits_AllBitsSet_Returns64()
    {
        var bmp = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
        Assert.Equal(64, ShopStockResolver.CountBits(bmp));
    }

    [Fact]
    public void CountIdArrayIds_DorterShields_Returns7()
    {
        // `80 81 82 83 84 85 86 00` — 7 ids, 0 terminator.
        var rec = new byte[] { 0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x00 };
        Assert.Equal(7, ShopStockResolver.CountIdArrayIds(rec));
    }

    [Fact]
    public void CountIdArrayIds_AllIds_Returns8()
    {
        // No terminator — all 8 bytes are valid ids.
        var rec = new byte[] { 0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87 };
        Assert.Equal(8, ShopStockResolver.CountIdArrayIds(rec));
    }

    [Fact]
    public void CountIdArrayIds_EarlyTerminator_StopsAtZero()
    {
        // Session 54 observed: id arrays can be shorter than 8
        // (e.g. 3-item shops). Bytes after the 0 are padding/stale
        // and must not be counted.
        var rec = new byte[] { 0x80, 0x81, 0x82, 0x00, 0xFF, 0xFF, 0xFF, 0xFF };
        Assert.Equal(3, ShopStockResolver.CountIdArrayIds(rec));
    }

    [Fact]
    public void CountIdArrayIds_NullArray_ReturnsZero()
    {
        Assert.Equal(0, ShopStockResolver.CountIdArrayIds(null!));
    }

    [Fact]
    public void CountIdArrayIds_EmptyArray_ReturnsZero()
    {
        Assert.Equal(0, ShopStockResolver.CountIdArrayIds(new byte[0]));
    }
}
