using System.Linq;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// Pure helper: given the active unit's turn flags (moved / acted),
// classify which entries of the in-battle action menu (Move / Abilities /
// Wait / Status / Auto-battle) are effectively usable vs grayed out.
//
// Menu stays 5 entries in FFT; the display labels and "does Enter open
// the submenu" semantics change based on BattleMoved / BattleActed.
// Move entry shifts to "Reset Move" post-move (same slot, different
// effect). Abilities greys out after acting. Wait/Status/Auto-battle
// are always enabled.
//
// Used by screen renderer to surface "can't move" / "can't act"
// up-front so Claude doesn't waste keys navigating into a grayed slot.
public class BattleMenuAvailabilityTests
{
    [Fact]
    public void FreshTurn_AllItemsAvailable()
    {
        var items = BattleMenuAvailability.For(moved: 0, acted: 0).ToList();
        Assert.Equal(5, items.Count);
        foreach (var it in items)
            Assert.True(it.Available,
                $"{it.Name} should be available on a fresh turn");
    }

    [Fact]
    public void AfterMove_SlotZeroIsResetMove()
    {
        var items = BattleMenuAvailability.For(moved: 1, acted: 0).ToList();
        Assert.Equal("Reset Move", items[0].Name);
        // Reset Move is available but behaves differently — flag it.
        Assert.True(items[0].Available);
    }

    [Fact]
    public void FreshTurn_SlotZeroIsMove()
    {
        var items = BattleMenuAvailability.For(moved: 0, acted: 0).ToList();
        Assert.Equal("Move", items[0].Name);
    }

    [Fact]
    public void AfterAct_AbilitiesGrayed()
    {
        var items = BattleMenuAvailability.For(moved: 0, acted: 1).ToList();
        var abilities = items.FirstOrDefault(i => i.Name == "Abilities");
        Assert.NotNull(abilities);
        Assert.False(abilities!.Available);
    }

    [Fact]
    public void Wait_Status_AutoBattle_AlwaysAvailable()
    {
        foreach (int moved in new[] { 0, 1 })
        {
            foreach (int acted in new[] { 0, 1 })
            {
                var items = BattleMenuAvailability.For(moved, acted).ToList();
                Assert.True(items.First(i => i.Name == "Wait").Available);
                Assert.True(items.First(i => i.Name == "Status").Available);
                Assert.True(items.First(i => i.Name == "Auto-battle").Available);
            }
        }
    }

    [Fact]
    public void MenuOrder_IsStable()
    {
        var items = BattleMenuAvailability.For(moved: 0, acted: 0).ToList();
        Assert.Equal("Move", items[0].Name);
        Assert.Equal("Abilities", items[1].Name);
        Assert.Equal("Wait", items[2].Name);
        Assert.Equal("Status", items[3].Name);
        Assert.Equal("Auto-battle", items[4].Name);
    }

    [Fact]
    public void CanAct_ReturnsTrue_WhenNotActed()
    {
        Assert.True(BattleMenuAvailability.CanAct(moved: 0, acted: 0));
        Assert.True(BattleMenuAvailability.CanAct(moved: 1, acted: 0));
    }

    [Fact]
    public void CanAct_ReturnsFalse_WhenActed()
    {
        Assert.False(BattleMenuAvailability.CanAct(moved: 0, acted: 1));
        Assert.False(BattleMenuAvailability.CanAct(moved: 1, acted: 1));
    }

    [Fact]
    public void CanMove_ReturnsTrue_WhenNotMoved()
    {
        Assert.True(BattleMenuAvailability.CanMove(moved: 0, acted: 0));
        Assert.True(BattleMenuAvailability.CanMove(moved: 0, acted: 1));
    }

    [Fact]
    public void CanMove_ReturnsFalse_WhenMoved()
    {
        // Slot 0 becomes Reset Move — CanMove=false signals "don't expect
        // another fresh move out of this unit"
        Assert.False(BattleMenuAvailability.CanMove(moved: 1, acted: 0));
    }
}
