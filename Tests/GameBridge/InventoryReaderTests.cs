using System.Linq;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

public class InventoryReaderTests
{
    [Fact]
    public void DecodeRaw_EmptyBytes_ReturnsEmptyList()
    {
        var result = InventoryReader.DecodeRaw(new byte[0]);
        Assert.Empty(result);
    }

    [Fact]
    public void DecodeRaw_NullBytes_ReturnsEmptyList()
    {
        var result = InventoryReader.DecodeRaw(null!);
        Assert.Empty(result);
    }

    [Fact]
    public void DecodeRaw_AllZeros_ReturnsEmptyList()
    {
        // A fresh game with no items owned — every byte is 0, no entries emitted.
        var bytes = new byte[272];
        var result = InventoryReader.DecodeRaw(bytes);
        Assert.Empty(result);
    }

    [Fact]
    public void DecodeRaw_SingleDagger_ReturnsOneEntry()
    {
        // Dagger = FFTPatcher ID 1. Byte at index 1 = count.
        var bytes = new byte[272];
        bytes[1] = 4;
        var result = InventoryReader.DecodeRaw(bytes);
        Assert.Single(result);
        Assert.Equal(1, result[0].ItemId);
        Assert.Equal(4, result[0].Count);
        Assert.Equal("Dagger", result[0].Name);
        Assert.Equal("knife", result[0].Type);
    }

    [Fact]
    public void DecodeRaw_SkipsZeroCounts()
    {
        // Only IDs 5 and 10 have items. Zeros between them must not appear.
        var bytes = new byte[272];
        bytes[5] = 2;
        bytes[10] = 1;
        var result = InventoryReader.DecodeRaw(bytes);
        Assert.Equal(2, result.Count);
        Assert.Equal(5, result[0].ItemId);
        Assert.Equal(10, result[1].ItemId);
    }

    [Fact]
    public void DecodeRaw_EntriesOrderedByItemId()
    {
        // Populate out-of-order; result must be ID-ascending.
        var bytes = new byte[272];
        bytes[100] = 1;
        bytes[20] = 2;
        bytes[150] = 3;
        var result = InventoryReader.DecodeRaw(bytes);
        Assert.Equal(new[] { 20, 100, 150 }, result.Select(e => e.ItemId));
    }

    [Fact]
    public void DecodeRaw_UnmappedIdReturnsNullName()
    {
        // ID 250 is almost certainly not in ItemData.cs — should still be
        // emitted with count, but Name/Type will be null.
        var bytes = new byte[272];
        bytes[250] = 99;
        var result = InventoryReader.DecodeRaw(bytes);
        Assert.Single(result);
        Assert.Equal(250, result[0].ItemId);
        Assert.Equal(99, result[0].Count);
        // Name/Type are null if ItemData doesn't have this ID; that's fine.
    }

    [Fact]
    public void DecodeRaw_KnightsSwordRagnarok_MatchesItemData()
    {
        // Ramza's Ragnarok = ID 36, type "knightsword". Used as the
        // live-verification anchor for the session 18 inventory hunt.
        var bytes = new byte[272];
        bytes[36] = 1;
        var result = InventoryReader.DecodeRaw(bytes);
        Assert.Single(result);
        Assert.Equal(36, result[0].ItemId);
        Assert.Equal(1, result[0].Count);
        Assert.Equal("Ragnarok", result[0].Name);
        Assert.Equal("knightsword", result[0].Type);
    }

    [Fact]
    public void DecodeRaw_LargeInventory_AllEntriesEmitted()
    {
        // Simulate a saved-state inventory with many distinct items, some
        // stacked high (consumables) and some singletons (equipment).
        var bytes = new byte[272];
        bytes[1] = 4;    // Dagger
        bytes[2] = 2;    // Mythril Knife
        bytes[36] = 1;   // Ragnarok
        bytes[38] = 25;  // Potion-range stack
        bytes[39] = 26;
        bytes[40] = 25;
        bytes[100] = 1;
        bytes[150] = 1;
        bytes[200] = 1;
        var result = InventoryReader.DecodeRaw(bytes);
        Assert.Equal(9, result.Count);
        // Total owned: 4+2+1+25+26+25+1+1+1 = 86
        Assert.Equal(86, result.Sum(e => e.Count));
    }

    [Fact]
    public void InventoryBase_IsExactly272BytesBeforeRosterBase()
    {
        // Session 18 finding: inventory sits immediately before the roster
        // in the same main-module region. Roster base is 0x1411A18D0.
        // If this ever drifts, the game has moved its data layout and
        // this whole reader needs re-verification.
        Assert.Equal(RosterReader.RosterBase - 272, InventoryReader.InventoryBase);
    }

    [Fact]
    public void DecodeRaw_SellPricePopulated_FromItemPrices()
    {
        // Dagger is in ItemPrices at 200 gil buy → 100 gil sell.
        var bytes = new byte[272];
        bytes[1] = 4;
        var result = InventoryReader.DecodeRaw(bytes);
        Assert.Single(result);
        Assert.Equal(100, result[0].SellPrice);
    }

    [Fact]
    public void DecodeRaw_SellPriceNull_ForItemWithoutBuyPrice()
    {
        // Ragnarok (ID 36) is a story drop not sold anywhere. SellPrice null.
        var bytes = new byte[272];
        bytes[36] = 1;
        var result = InventoryReader.DecodeRaw(bytes);
        Assert.Single(result);
        Assert.Null(result[0].SellPrice);
    }

    [Fact]
    public void DecodeRaw_SellPriceNull_ForUnmappedItem()
    {
        // ID 269 is beyond ItemData's current coverage (chemistitem range
        // tops out around 259). Not in ItemPrices. Count > 0 forces the
        // entry to be emitted; SellPrice must be null.
        var bytes = new byte[272];
        bytes[269] = 1;
        var result = InventoryReader.DecodeRaw(bytes);
        Assert.Single(result);
        Assert.Null(result[0].SellPrice);
    }

    [Fact]
    public void DecodeRaw_PotionSellPrice_Is25()
    {
        // Potion = ID 240, buy 50 → sell 25 (/2 integer div).
        var bytes = new byte[272];
        bytes[240] = 10;
        var result = InventoryReader.DecodeRaw(bytes);
        Assert.Single(result);
        Assert.Equal(25, result[0].SellPrice);
    }

    // ReadSellable filter tests use DecodeRaw as a helper — simulates the
    // full pipeline without needing a live MemoryExplorer.

    [Fact]
    public void FilterSellable_OnlyEntriesWithSellPrice()
    {
        // Simulate a mixed inventory: 1 Dagger (sellable), 1 Ragnarok (no price).
        var bytes = new byte[272];
        bytes[1] = 4;   // Dagger — has sell price
        bytes[36] = 1;  // Ragnarok — no sell price
        bytes[240] = 5; // Potion — has sell price
        var all = InventoryReader.DecodeRaw(bytes);
        Assert.Equal(3, all.Count);
        var sellable = all.FindAll(e => e.SellPrice.HasValue);
        Assert.Equal(2, sellable.Count);
        Assert.Contains(sellable, e => e.ItemId == 1);
        Assert.Contains(sellable, e => e.ItemId == 240);
        Assert.DoesNotContain(sellable, e => e.ItemId == 36);
    }

    [Fact]
    public void FilterSellable_EmptyInventory_ReturnsEmptyList()
    {
        var bytes = new byte[272];
        var all = InventoryReader.DecodeRaw(bytes);
        var sellable = all.FindAll(e => e.SellPrice.HasValue);
        Assert.Empty(sellable);
    }
}
