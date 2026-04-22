using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

public class ShopStockDecoderTests
{
    // Dorter Ch1 weapons — session 53 confirmed bitmap `00 06 76 00 00 00 00 00`
    // decodes via offset +42 to item ids 51 (Rod), 52 (Thunder Rod), 59 (Oak
    // Staff), 60 (White Staff), 62 (Serpent Staff), 63 (Mage's Staff), 64
    // (Golden Staff). Pin the full decode here so any future refactor of
    // the offset formula has to stay byte-for-byte compatible.
    [Fact]
    public void DecodeBitmap_DorterCh1Weapons_ReturnsSevenStaves()
    {
        var bitmap = new byte[] { 0x00, 0x06, 0x76, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var ids = ShopStockDecoder.DecodeBitmap(bitmap, 42);
        Assert.Equal(new[] { 51, 52, 59, 60, 62, 63, 64 }, ids);
    }

    [Fact]
    public void DecodeBitmap_DorterCh1Shields_ReturnsSevenShields()
    {
        // Session 54: shield record at heap 0xB0A9920 had bitmap
        // `7F 00 00 00 00 00 00 00` which decodes to IDs 128-134
        // (Escutcheon, Buckler, Bronze Shield, Round Shield, Mythril
        // Shield, Golden Shield, Ice Shield) given offset=128. Verified
        // live against the in-game display at Dorter Outfitter Buy /
        // Shields tab.
        var bitmap = new byte[] { 0x7F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var ids = ShopStockDecoder.DecodeBitmap(bitmap, 128);
        Assert.Equal(new[] { 128, 129, 130, 131, 132, 133, 134 }, ids);
    }

    [Fact]
    public void DecodeBitmap_EmptyBitmap_ReturnsEmptyList()
    {
        var ids = ShopStockDecoder.DecodeBitmap(new byte[8], 42);
        Assert.Empty(ids);
    }

    [Fact]
    public void DecodeBitmap_NullBitmap_ReturnsEmptyList()
    {
        var ids = ShopStockDecoder.DecodeBitmap(null!, 42);
        Assert.Empty(ids);
    }

    [Fact]
    public void DecodeBitmap_AllBitsSet_ReturnsAll64Ids()
    {
        var bitmap = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
        var ids = ShopStockDecoder.DecodeBitmap(bitmap, 0);
        Assert.Equal(64, ids.Count);
        Assert.Equal(0, ids[0]);
        Assert.Equal(63, ids[^1]);
    }

    [Fact]
    public void DecodeBitmap_Bitmap4Format_CountBytesLeakIntoOffset208()
    {
        // Pins the buggy (no-format-awareness) behavior that
        // DecodeStockAt now guards against. For a Bitmap4 record
        // read as 8 raw bytes, bytes 4-7 store the record count
        // (not bitmap bits). Decoding all 8 bytes at Accessories
        // offset 208 incorrectly reads the count byte as 3 phantom
        // items (240, 241, 242 = Potion/Hi-Potion/X-Potion).
        //
        // Session 55 live-verified at Gariland Outfitter Buy: the
        // resolver call returned 10 Accessories instead of 7. Fix
        // is in DecodeStockAt — zeroes the high 4 bytes on Bitmap4.
        // This test asserts the *raw* decode still produces the
        // phantom result so future refactors of the low-level
        // decoder can't accidentally silently fix the issue in a
        // different place.
        var raw = new byte[] { 0x7F, 0x00, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00 };
        var ids = ShopStockDecoder.DecodeBitmap(raw, 208);
        Assert.Contains(240, ids); // phantom from count byte
        Assert.Contains(241, ids);
        Assert.Contains(242, ids);
    }

    [Fact]
    public void ValidateAgainstExpected_MatchingCount_ReturnsInput()
    {
        // Pure helper called at the end of DecodeStockAt. When the
        // decoded list size matches the registry-expected count, it
        // passes through unchanged.
        var stock = new List<ShopStockItem>
        {
            new() { Id = 240, Name = "Potion", Type = "chemistitem", BuyPrice = 50 },
            new() { Id = 241, Name = "Hi-Potion", Type = "chemistitem", BuyPrice = 200 },
        };
        var result = ShopStockDecoder.ValidateAgainstExpected(stock, expectedCount: 2);
        Assert.Equal(2, result.Count);
        Assert.Same(stock, result);
    }

    [Fact]
    public void ValidateAgainstExpected_TooManyItems_ReturnsEmpty()
    {
        // Lesalia/Warjilis live-observed false positive: locate
        // returned a memory region that decoded to 8 items when
        // the registry expected 6. The 2 extras (Vesper, Sagittarius
        // Bow) are bogus — count mismatch must reject the whole
        // list rather than surface partial wrong data.
        var stock = new List<ShopStockItem>
        {
            new() { Id = 240, Name = "Potion", BuyPrice = 50 },
            new() { Id = 241, Name = "Hi-Potion", BuyPrice = 200 },
            new() { Id = 242, Name = "X-Potion", BuyPrice = 700 },
            new() { Id = 243, Name = "Ether", BuyPrice = 200 },
            new() { Id = 244, Name = "Hi-Ether", BuyPrice = 600 },
            new() { Id = 246, Name = "Antidote", BuyPrice = 50 },
            new() { Id = 273, Name = "Vesper", BuyPrice = null },
            new() { Id = 274, Name = "Sagittarius Bow", BuyPrice = null },
        };
        var result = ShopStockDecoder.ValidateAgainstExpected(stock, expectedCount: 6);
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateAgainstExpected_TooFewItems_ReturnsEmpty()
    {
        // Defensive: a partial decode (e.g. some IDs missing from
        // ItemData lookup) shouldn't leak through either. Symmetric
        // rejection on either side of the expected count.
        var stock = new List<ShopStockItem>
        {
            new() { Id = 240, Name = "Potion", BuyPrice = 50 },
        };
        var result = ShopStockDecoder.ValidateAgainstExpected(stock, expectedCount: 6);
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateAgainstExpected_NegativeExpected_PassesThrough()
    {
        // Backwards-compat: callers that don't have a registry
        // expected count (e.g. one-off pattern-arg manual mode in
        // shop_stock) pass -1 and get the un-validated decode.
        var stock = new List<ShopStockItem>
        {
            new() { Id = 240, Name = "Potion", BuyPrice = 50 },
        };
        var result = ShopStockDecoder.ValidateAgainstExpected(stock, expectedCount: -1);
        Assert.Single(result);
    }

    [Fact]
    public void OffsetForCategory_Weapons_Is42()
    {
        // FFTPatcher canonical offsets: weapons 1-41 are story items /
        // unique equipment; shop-stock weapons begin at id 42 (Dagger).
        // The bitmap encoding uses id-42 as bit-0 anchor.
        Assert.Equal(42, ShopStockDecoder.OffsetForCategory(ShopStockDecoder.Category.Weapons));
    }

    [Fact]
    public void OffsetForCategory_Shields_Is128()
    {
        Assert.Equal(128, ShopStockDecoder.OffsetForCategory(ShopStockDecoder.Category.Shields));
    }

    [Fact]
    public void OffsetForCategory_Daggers_Is1()
    {
        // Daggers occupy ids 1-7 in FFTPatcher layout. Session 54
        // tour revealed they use a separate 4-byte bitmap record at
        // `0x3CE95C0` with offset 1 — not the offset-42 scheme used
        // for higher-id weapons.
        Assert.Equal(1, ShopStockDecoder.OffsetForCategory(ShopStockDecoder.Category.Daggers));
    }

    [Fact]
    public void DecodeBitmap_GarilandCh1Daggers_ReturnsSevenDaggers()
    {
        // Bitmap `7F 00 00 00 00 00 00 00` + offset 1 decodes to
        // ids 1-7: Dagger, Mythril Knife, Blind Knife, Mage Masher,
        // Platinum Dagger, Main Gauche, Orichalcum Dirk.
        var bitmap = new byte[] { 0x7F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var ids = ShopStockDecoder.DecodeBitmap(bitmap, 1);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7 }, ids);
    }

    [Fact]
    public void DecodeIdArray_DorterCh1Shields_ReturnsSevenShields()
    {
        // Session 54 live-verified: Dorter Ch1 Shields tab uses u8
        // id-array format — bytes 0x80-0x86 directly, 0 terminator.
        var idArray = new byte[] { 0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x00 };
        var ids = ShopStockDecoder.DecodeIdArray(idArray);
        Assert.Equal(new[] { 128, 129, 130, 131, 132, 133, 134 }, ids);
    }

    [Fact]
    public void DecodeIdArray_FullEightItems_ReturnsEight()
    {
        // When all 8 slots are populated (no terminator), all 8
        // bytes decode as ids.
        var idArray = new byte[] { 0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87 };
        var ids = ShopStockDecoder.DecodeIdArray(idArray);
        Assert.Equal(new[] { 128, 129, 130, 131, 132, 133, 134, 135 }, ids);
    }

    [Fact]
    public void DecodeIdArray_LeadingZero_ReturnsEmpty()
    {
        // A record starting with 0 is interpreted as "no items".
        var idArray = new byte[] { 0x00, 0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86 };
        var ids = ShopStockDecoder.DecodeIdArray(idArray);
        Assert.Empty(ids);
    }

    [Fact]
    public void DecodeIdArray_NullInput_ReturnsEmpty()
    {
        var ids = ShopStockDecoder.DecodeIdArray(null!);
        Assert.Empty(ids);
    }

    [Fact]
    public void FormatForCategory_WeaponsIsBitmap8()
    {
        Assert.Equal(ShopStockDecoder.RecordFormat.Bitmap8,
            ShopStockDecoder.FormatForCategory(ShopStockDecoder.Category.Weapons));
    }

    [Fact]
    public void FormatForCategory_DaggersIsBitmap4()
    {
        Assert.Equal(ShopStockDecoder.RecordFormat.Bitmap4,
            ShopStockDecoder.FormatForCategory(ShopStockDecoder.Category.Daggers));
    }

    [Fact]
    public void FormatForCategory_ShieldsIsIdArray()
    {
        Assert.Equal(ShopStockDecoder.RecordFormat.IdArray,
            ShopStockDecoder.FormatForCategory(ShopStockDecoder.Category.Shields));
    }

    [Fact]
    public void FormatForCategory_BodyIsBitmap4()
    {
        // Session 54: Body uses the same Bitmap4 format as daggers
        // (same shared record at 0x3E4FFC0 in memory), with offset
        // 186 so the 7 bits decode to ids 186-192 (clothing).
        Assert.Equal(ShopStockDecoder.RecordFormat.Bitmap4,
            ShopStockDecoder.FormatForCategory(ShopStockDecoder.Category.Body));
    }

    [Fact]
    public void FormatForCategory_AccessoriesIsBitmap4()
    {
        // Session 54: Accessories shares Bitmap4 format + record
        // with daggers/body, differentiated by offset 208.
        Assert.Equal(ShopStockDecoder.RecordFormat.Bitmap4,
            ShopStockDecoder.FormatForCategory(ShopStockDecoder.Category.Accessories));
    }

    [Fact]
    public void FormatForCategory_ConsumablesIsBitmap8()
    {
        // Session 54: Consumables uses Bitmap8 at offset 240.
        // Dorter/Yardrow ch1 bitmap `5F 00 00 00 00 00 00 00`
        // decodes to 6 items (Potion, Hi-Potion, X-Potion, Ether,
        // Hi-Ether, Antidote). 245 Elixir is skipped.
        Assert.Equal(ShopStockDecoder.RecordFormat.Bitmap8,
            ShopStockDecoder.FormatForCategory(ShopStockDecoder.Category.Consumables));
    }

    [Fact]
    public void DecodeBitmap_DorterCh1Body_ReturnsSevenClothing()
    {
        var bitmap = new byte[] { 0x7F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var ids = ShopStockDecoder.DecodeBitmap(bitmap, 186);
        Assert.Equal(new[] { 186, 187, 188, 189, 190, 191, 192 }, ids);
    }

    [Fact]
    public void DecodeBitmap_DorterCh1Accessories_ReturnsSevenShoes()
    {
        var bitmap = new byte[] { 0x7F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var ids = ShopStockDecoder.DecodeBitmap(bitmap, 208);
        Assert.Equal(new[] { 208, 209, 210, 211, 212, 213, 214 }, ids);
    }

    [Fact]
    public void DecodeBitmap_DorterCh1Consumables_ReturnsSixPotions()
    {
        // Bitmap `5F` = bits 0-4 and 6 (skip bit 5 = Elixir 245).
        var bitmap = new byte[] { 0x5F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var ids = ShopStockDecoder.DecodeBitmap(bitmap, 240);
        Assert.Equal(new[] { 240, 241, 242, 243, 244, 246 }, ids);
    }
}
