using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

public class SkillsetItemLookupTests
{
    private static byte[] EmptyInventory() => new byte[272];

    private static byte[] InventoryWith(params (int id, int count)[] entries)
    {
        var inv = EmptyInventory();
        foreach (var (id, count) in entries)
            inv[id] = (byte)count;
        return inv;
    }

    // ---- Items (Chemist) ----

    [Fact]
    public void Items_Potion_ReturnsInventoryCount()
    {
        var inv = InventoryWith((240, 3));
        var count = SkillsetItemLookup.TryGetHeldCount("Items", "Potion", inv);
        Assert.Equal(3, count);
    }

    [Fact]
    public void Items_PhoenixDown_Returns98()
    {
        var inv = InventoryWith((253, 98));
        var count = SkillsetItemLookup.TryGetHeldCount("Items", "Phoenix Down", inv);
        Assert.Equal(98, count);
    }

    [Fact]
    public void Items_EmptyInventory_ReturnsZero()
    {
        var inv = EmptyInventory();
        Assert.Equal(0, SkillsetItemLookup.TryGetHeldCount("Items", "Potion", inv));
    }

    [Fact]
    public void Items_UnknownAbilityName_ReturnsNull()
    {
        var inv = EmptyInventory();
        Assert.Null(SkillsetItemLookup.TryGetHeldCount("Items", "Fire", inv));
    }

    [Fact]
    public void Items_EyeDrop_AndEyeDropsPlural_BothWork()
    {
        // ActionAbilityLookup uses "Eye Drop" (singular); ItemData uses
        // "Eye Drops" (plural). SkillsetItemLookup accepts both so a
        // spelling drift between the two tables doesn't silently return null.
        var inv = InventoryWith((247, 5));
        Assert.Equal(5, SkillsetItemLookup.TryGetHeldCount("Items", "Eye Drop", inv));
        Assert.Equal(5, SkillsetItemLookup.TryGetHeldCount("Items", "Eye Drops", inv));
    }

    // ---- Iaido (Samurai) ----

    [Fact]
    public void Iaido_Ashura_ReturnsKatanaCount()
    {
        var inv = InventoryWith((38, 25));
        Assert.Equal(25, SkillsetItemLookup.TryGetHeldCount("Iaido", "Ashura", inv));
    }

    [Fact]
    public void Iaido_MissingKatana_ReturnsZero_Unusable()
    {
        var inv = EmptyInventory();
        Assert.Equal(0, SkillsetItemLookup.TryGetHeldCount("Iaido", "Chirijiraden", inv));
    }

    // ---- Throw (Ninja) ----

    [Fact]
    public void Throw_Knife_SumsAllKnivesInInventory()
    {
        // Dagger(1)=4, Mythril Knife(2)=2, Mage Masher(4)=1 → sum 7 knives
        var inv = InventoryWith((1, 4), (2, 2), (4, 1));
        var count = SkillsetItemLookup.TryGetHeldCount("Throw", "Knife", inv);
        Assert.Equal(7, count);
    }

    [Fact]
    public void Throw_Sword_SumsOnlySwords_NotOtherTypes()
    {
        // 1 Dagger (knife), 4 Longsword (sword) → knife throw=1, sword throw=4
        var inv = InventoryWith((1, 1), (20, 4));
        Assert.Equal(4, SkillsetItemLookup.TryGetHeldCount("Throw", "Sword", inv));
        Assert.Equal(1, SkillsetItemLookup.TryGetHeldCount("Throw", "Knife", inv));
    }

    [Fact]
    public void Throw_NoMatchingType_ReturnsZero()
    {
        // Only Ragnarok (knightsword, id 36) — no ability name "Knightsword"
        // because FFT's Throw doesn't include knightswords. Regular Sword
        // throw would look for type=sword, not matching Ragnarok.
        var inv = InventoryWith((36, 1));
        Assert.Equal(0, SkillsetItemLookup.TryGetHeldCount("Throw", "Sword", inv));
    }

    // ---- Non-inventory skillsets return null (don't show heldCount) ----

    [Fact]
    public void NonInventorySkillset_ReturnsNull()
    {
        var inv = InventoryWith((240, 5));
        Assert.Null(SkillsetItemLookup.TryGetHeldCount("White Magicks", "Cure", inv));
        Assert.Null(SkillsetItemLookup.TryGetHeldCount("Arts of War", "Rend Helm", inv));
        Assert.Null(SkillsetItemLookup.TryGetHeldCount("Monk", "Focus", inv));
    }

    [Fact]
    public void NullInventory_ReturnsNull()
    {
        Assert.Null(SkillsetItemLookup.TryGetHeldCount("Items", "Potion", null!));
    }

    [Fact]
    public void GetThrowTypeCount_PureFunction_EmptyInventory()
    {
        Assert.Equal(0, SkillsetItemLookup.GetThrowTypeCount(EmptyInventory(), "knife"));
    }
}
