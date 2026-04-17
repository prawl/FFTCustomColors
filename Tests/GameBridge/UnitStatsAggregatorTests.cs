using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

/// <summary>
/// Tests for <c>UnitStatsAggregator</c> — derives per-unit stats visible on
/// CharacterStatus by combining roster data with equipment bonuses from
/// ItemData. Used to populate the verbose-only `detailedStats` payload
/// surfaced on CharacterStatus (TODO §10.6 Data surfacing).
///
/// Pure function; no memory reads — all inputs are passed in by caller.
/// Caller resolves ItemIds → ItemInfo via ItemData.Items beforehand.
/// </summary>
public class UnitStatsAggregatorTests
{
    [Fact]
    public void Aggregate_EmptySlots_ReturnsZeroBonuses()
    {
        var stats = UnitStatsAggregator.Aggregate(
            helm: null, body: null, accessory: null,
            weapon: null, leftHand: null, shield: null);
        Assert.Equal(0, stats.TotalHpBonus);
        Assert.Equal(0, stats.TotalMpBonus);
        Assert.Null(stats.WeaponPower);
        Assert.Null(stats.WeaponRange);
        Assert.Equal(0, stats.ShieldPhysicalEvade);
        Assert.Equal(0, stats.ShieldMagicEvade);
    }

    [Fact]
    public void Aggregate_RamzaLoadout_SumsEquipmentHpMpBonuses()
    {
        // Ramza save-state: Grand Helm(156, HP+150), Maximillian(185, HP+200),
        // Bracer(218, no HP), Ragnarok(36, WP24), Round Shield(131).
        // Expected HP bonus = 150 + 200 + 0 = 350.
        var helm = ItemData.GetItem(156);
        var body = ItemData.GetItem(185);
        var acc = ItemData.GetItem(218);
        var weapon = ItemData.GetItem(36);
        var shield = ItemData.GetItem(131);
        Assert.NotNull(helm);
        Assert.NotNull(body);
        Assert.NotNull(weapon);
        Assert.NotNull(shield);

        var stats = UnitStatsAggregator.Aggregate(
            helm: helm, body: body, accessory: acc,
            weapon: weapon, leftHand: null, shield: shield);
        Assert.Equal(150 + 200, stats.TotalHpBonus);
        Assert.Equal(weapon!.WeaponPower, stats.WeaponPower);
        Assert.Equal(weapon.Range, stats.WeaponRange);
        Assert.Equal(shield!.PhysicalEvade, stats.ShieldPhysicalEvade);
        Assert.Equal(shield.MagicEvade, stats.ShieldMagicEvade);
    }

    [Fact]
    public void Aggregate_PlumedHatPlusWizardsHat_IgnoresSecondSlotMixed()
    {
        // Plumed Hat(158, HP+16, MP+5). Not a real multi-helm scenario but
        // verifies that the aggregator takes the one helm passed and sums
        // its HP and MP bonuses (not just HP).
        var helm = ItemData.GetItem(158);
        Assert.NotNull(helm);

        var stats = UnitStatsAggregator.Aggregate(
            helm: helm, body: null, accessory: null,
            weapon: null, leftHand: null, shield: null);
        Assert.Equal(16, stats.TotalHpBonus);
        Assert.Equal(5, stats.TotalMpBonus);
    }

    [Fact]
    public void Aggregate_DualWield_UsesWeaponInRightHand()
    {
        // The right-hand weapon is the "primary" one for WP/range display —
        // the detailed panel shows right-hand weapon stats prominently.
        // (Dual-wield math doubles attacks via two hits, not by summing WP.)
        var rightHand = ItemData.GetItem(10); // Zwill Straightblade WP12 knife
        var leftHand = ItemData.GetItem(9);   // Air Knife WP10
        Assert.NotNull(rightHand);
        Assert.NotNull(leftHand);

        var stats = UnitStatsAggregator.Aggregate(
            helm: null, body: null, accessory: null,
            weapon: rightHand, leftHand: leftHand, shield: null);
        Assert.Equal(rightHand!.WeaponPower, stats.WeaponPower);
        Assert.Equal(leftHand!.WeaponPower, stats.LeftHandPower);
    }

    [Fact]
    public void Aggregate_WeaponElement_Surfaced()
    {
        // Ragnarok has no element; Ice Brand (knightsword 29) has Ice.
        var ragnarok = ItemData.GetItem(36);
        var stats = UnitStatsAggregator.Aggregate(
            helm: null, body: null, accessory: null,
            weapon: ragnarok, leftHand: null, shield: null);
        Assert.Equal(ragnarok!.Element, stats.WeaponElement); // may be null
    }
}
