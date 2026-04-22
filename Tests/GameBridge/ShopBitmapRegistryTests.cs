using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

public class ShopBitmapRegistryTests
{
    private static readonly byte[] Ch1Staves = new byte[] { 0x00, 0x06, 0x76, 0x00, 0x00, 0x00, 0x00, 0x00 };
    private static readonly byte[] Ch1Ranged = new byte[] { 0x00, 0x00, 0x00, 0x20, 0xF8, 0x01, 0x00, 0x00 };

    [Theory]
    [InlineData(7)]   // Yardrow
    [InlineData(8)]   // Gollund
    [InlineData(9)]   // Dorter
    [InlineData(10)]  // Zaland
    [InlineData(12)]  // Warjilis
    [InlineData(13)]  // Bervenia
    [InlineData(14)]  // Sal Ghidos
    public void Lookup_StavesShops_ReturnsStavesBitmap(int location)
    {
        // Session 54 screenshot-verified: all 7 staves shops
        // (Yardrow, Gollund, Dorter, Zaland, Warjilis, Bervenia,
        // Sal Ghidos) sell the same 7 staves+rods in Chapter 1.
        // Bitmap `00 06 76 00 00 00 00 00` decodes (offset +42) to
        // ids 51 (Rod), 52 (Thunder Rod), 59 (Oak), 60 (White), 62
        // (Serpent), 63 (Mage's), 64 (Golden).
        var bmp = ShopBitmapRegistry.Lookup(location, 1, ShopStockDecoder.Category.Weapons);
        Assert.Equal(Ch1Staves, bmp);
    }

    [Fact]
    public void Lookup_GougCh1Weapons_NotRegistered()
    {
        // Session 54 live-verified: Goug Ch1 displays 8 items
        // (7 crossbows + Romandan Pistol + Mythril Gun). The
        // only in-memory bitmap that matches decodes to 7 items
        // (missing Mythril Gun), so auto-mode would return
        // incorrect data. Intentionally not registered to force
        // the bridge to surface a clear "not mapped" error
        // rather than silently producing a wrong 7-item list.
        var bmp = ShopBitmapRegistry.Lookup(11, 1, ShopStockDecoder.Category.Weapons);
        Assert.Null(bmp);
    }

    [Theory]
    [InlineData(0)]  // Lesalia
    [InlineData(1)]  // Riovanes
    [InlineData(2)]  // Eagrose
    [InlineData(3)]  // Lionel
    [InlineData(4)]  // Limberry
    [InlineData(5)]  // Zeltennia
    [InlineData(6)]  // Gariland
    public void Lookup_DaggerShops_WeaponsCategory_ReturnsNull(int location)
    {
        // Dagger shops are registered under Category.Daggers, not
        // Weapons, because their id range (1-7) requires offset 1
        // and 4-byte bitmap format. Looking them up under Weapons
        // correctly returns null; the bridge action auto-falls-back
        // to Daggers when the Weapons lookup misses.
        var bmp = ShopBitmapRegistry.Lookup(location, 1, ShopStockDecoder.Category.Weapons);
        Assert.Null(bmp);
    }

    [Theory]
    [InlineData(0)]  // Lesalia
    [InlineData(1)]  // Riovanes
    [InlineData(2)]  // Eagrose
    [InlineData(3)]  // Lionel
    [InlineData(4)]  // Limberry
    [InlineData(5)]  // Zeltennia
    [InlineData(6)]  // Gariland
    public void Lookup_DaggerShops_DaggersCategory_ReturnsDaggerBitmap(int location)
    {
        // Session 54 live-verified: all 7 dagger shops share bitmap
        // `7F 00 00 00 ...` (bits 0-6) which decodes (offset +1)
        // to ids 1-7 (Dagger, Mythril Knife, Blind Knife, Mage
        // Masher, Platinum Dagger, Main Gauche, Orichalcum Dirk).
        var expected = new byte[] { 0x7F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var bmp = ShopBitmapRegistry.Lookup(location, 1, ShopStockDecoder.Category.Daggers);
        Assert.Equal(expected, bmp);
    }

    [Fact]
    public void Lookup_DorterCh1Shields_ReturnsIdArray()
    {
        // Session 54 live-verified: Dorter Ch1 Shields tab shows 7
        // shields (ids 128-134). The record uses u8 id-array format
        // (not bitmap) — each byte is a direct FFTPatcher id, 0
        // terminates the list. Padded to 8 bytes.
        var expected = new byte[] { 0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x00 };
        var rec = ShopBitmapRegistry.Lookup(9, 1, ShopStockDecoder.Category.Shields);
        Assert.Equal(expected, rec);
    }

    [Theory]
    [InlineData(0)]   // Lesalia
    [InlineData(1)]   // Riovanes
    [InlineData(2)]   // Eagrose
    [InlineData(3)]   // Lionel
    [InlineData(4)]   // Limberry
    [InlineData(5)]   // Zeltennia
    [InlineData(6)]   // Gariland
    [InlineData(7)]   // Yardrow ✓verified
    [InlineData(8)]   // Gollund
    [InlineData(9)]   // Dorter ✓verified
    [InlineData(10)]  // Zaland
    [InlineData(11)]  // Goug
    [InlineData(12)]  // Warjilis
    [InlineData(13)]  // Bervenia
    [InlineData(14)]  // Sal Ghidos
    public void Lookup_HelmShops_ReturnsIdArray(int location)
    {
        // Session 54 live-verified: Dorter and Yardrow Ch1 Helms
        // tabs both sell 7 identical hats (ids 157-163). Other
        // settlements registered on the assumption they share the
        // same Ch1 helm stock; decoder will return "record not
        // found" if a shop lacks helms (no wrong data risk).
        // Uses u8 id-array format (NOT bitmap).
        var expected = new byte[] { 0x9D, 0x9E, 0x9F, 0xA0, 0xA1, 0xA2, 0xA3, 0x00 };
        var rec = ShopBitmapRegistry.Lookup(location, 1, ShopStockDecoder.Category.Helms);
        Assert.Equal(expected, rec);
    }

    [Fact]
    public void Lookup_DorterCh1Body_ReturnsBitmap4()
    {
        // Session 54 live-verified: Dorter/Yardrow Ch1 Body tab
        // sells 7 clothing items (ids 186-192: Clothing through
        // Wizard Clothing). Uses Bitmap4 format at offset 186.
        var expected = new byte[] { 0x7F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var rec = ShopBitmapRegistry.Lookup(9, 1, ShopStockDecoder.Category.Body);
        Assert.Equal(expected, rec);
    }

    [Fact]
    public void Lookup_DorterCh1Accessories_ReturnsBitmap4()
    {
        // Session 54 live-verified: Yardrow + Dorter Ch1 Accessories
        // tab sells 7 shoes (ids 208-214: Battle/Spiked/Germinas/
        // Rubber/Winged Boots + Hermes + Red Shoes). Uses Bitmap4
        // format at offset 208.
        var expected = new byte[] { 0x7F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var rec = ShopBitmapRegistry.Lookup(9, 1, ShopStockDecoder.Category.Accessories);
        Assert.Equal(expected, rec);
    }

    [Fact]
    public void Lookup_DorterCh1Consumables_ReturnsBitmap8()
    {
        // Session 54 live-verified: 6 consumables (ids 240-244, 246).
        // Uses Bitmap8 at offset 240.
        var expected = new byte[] { 0x5F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var rec = ShopBitmapRegistry.Lookup(9, 1, ShopStockDecoder.Category.Consumables);
        Assert.Equal(expected, rec);
    }

    [Fact]
    public void HasMapping_DorterCh1Weapons_True()
    {
        Assert.True(ShopBitmapRegistry.HasMapping(9, 1, ShopStockDecoder.Category.Weapons));
    }

    [Fact]
    public void HasMapping_GougCh1Weapons_False()
    {
        // See Lookup_GougCh1Weapons_NotRegistered — the 8-item
        // stock doesn't decode cleanly from any known bitmap.
        Assert.False(ShopBitmapRegistry.HasMapping(11, 1, ShopStockDecoder.Category.Weapons));
    }

    [Fact]
    public void HasMapping_LesaliaCh1Weapons_False()
    {
        Assert.False(ShopBitmapRegistry.HasMapping(0, 1, ShopStockDecoder.Category.Weapons));
    }

    [Fact]
    public void RegisteredCategoriesFor_DorterCh1_HasAllSixVerifiedCategories()
    {
        // Dorter (9) is the most-verified shop — staves weapons,
        // shields, helms, body, accessories, consumables are all
        // registered. No daggers (dagger shops are a different set).
        var cats = ShopBitmapRegistry.RegisteredCategoriesFor(9, 1);
        Assert.Contains(ShopStockDecoder.Category.Weapons, cats);
        Assert.DoesNotContain(ShopStockDecoder.Category.Daggers, cats);
        Assert.Contains(ShopStockDecoder.Category.Shields, cats);
        Assert.Contains(ShopStockDecoder.Category.Helms, cats);
        Assert.Contains(ShopStockDecoder.Category.Body, cats);
        Assert.Contains(ShopStockDecoder.Category.Accessories, cats);
        Assert.Contains(ShopStockDecoder.Category.Consumables, cats);
    }

    [Fact]
    public void RegisteredCategoriesFor_CanonicalTabOrder()
    {
        // Order has to match the in-game Outfitter tab sequence so
        // screen.stockItems is predictable for callers iterating it.
        // Weapons → Daggers → Shields → Helms → Body → Accessories →
        // Consumables. Dorter has all except Daggers.
        var cats = ShopBitmapRegistry.RegisteredCategoriesFor(9, 1);
        var expected = new[]
        {
            ShopStockDecoder.Category.Weapons,
            // No Daggers at Dorter
            ShopStockDecoder.Category.Shields,
            ShopStockDecoder.Category.Helms,
            ShopStockDecoder.Category.Body,
            ShopStockDecoder.Category.Accessories,
            ShopStockDecoder.Category.Consumables,
        };
        Assert.Equal(expected, cats);
    }

    [Fact]
    public void RegisteredCategoriesFor_GarilandCh1_HasDaggersNotWeapons()
    {
        // Dagger shop — registered as Daggers, not Weapons.
        var cats = ShopBitmapRegistry.RegisteredCategoriesFor(6, 1);
        Assert.Contains(ShopStockDecoder.Category.Daggers, cats);
        Assert.DoesNotContain(ShopStockDecoder.Category.Weapons, cats);
    }

    [Fact]
    public void RegisteredCategoriesFor_GougCh1_ExcludesWeaponsUntilMythrilGunCracked()
    {
        // Goug's weapons tab is NOT registered (8-item bitmap missing
        // Mythril Gun). Other categories are registered by analogy;
        // we need the enumeration to reflect the honest state.
        var cats = ShopBitmapRegistry.RegisteredCategoriesFor(11, 1);
        Assert.DoesNotContain(ShopStockDecoder.Category.Weapons, cats);
        Assert.Contains(ShopStockDecoder.Category.Helms, cats);
    }

    [Fact]
    public void RegisteredCategoriesFor_UnknownLocation_ReturnsEmpty()
    {
        // Out-of-range location (no registered entries) returns an
        // empty list, not null. Keeps callers from having to null-
        // check before iterating.
        var cats = ShopBitmapRegistry.RegisteredCategoriesFor(99, 1);
        Assert.Empty(cats);
    }

    [Fact]
    public void RegisteredCategoriesFor_Chapter2AtDorter_ReturnsEmpty()
    {
        // Ch2+ shop stocks aren't registered yet (chapter byte hunt
        // is deferred TODO). Enumeration must gracefully return an
        // empty list so the screen-assembly code doesn't crash on
        // future chapters — it just won't show stock items.
        var cats = ShopBitmapRegistry.RegisteredCategoriesFor(9, 2);
        Assert.Empty(cats);
    }
}
