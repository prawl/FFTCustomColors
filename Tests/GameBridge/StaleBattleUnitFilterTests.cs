using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// 2026-04-26 Mandalia: scan response's `state.units[]` block contained
// 17 entries — 4 actual battle units + 13 residue from prior battles
// (Lv25 entries on Ch1-early Mandalia, multiple units stacked at (4,6),
// HP/MaxHp shapes that don't exist on this map).
//
// BattleTracker._units is populated by ScanCondensedStruct (parses
// FF FF markers in a different memory region) and only cleared on
// battle exit. When the exit detection misfires, the dict accumulates
// across battles. PollStaticArray DOES correctly filter by inBattle
// flag and gives us a reliable snapshot of current-battle MaxHp values.
//
// Filter: at emit time, only keep `_units` entries whose MaxHp appears
// in the active-slot snapshot. Two units with identical MaxHp (e.g.
// generic Goblins) are both kept — no residue collision; both are
// genuine current-battle units. The Lv25 phantom with MaxHp=263 won't
// collide with the Lv7 Mandalia enemies' MaxHp=75 / 64 / 68.
public class StaleBattleUnitFilterTests
{
    private static BattleUnitState Unit(int team, int level, int maxHp, int x = -1, int y = -1) =>
        new BattleUnitState
        {
            Team = team, Level = level, MaxHp = maxHp,
            Hp = maxHp, X = x, Y = y,
            PositionKnown = x >= 0,
        };

    [Fact]
    public void Empty_Active_DropsAll()
    {
        // No active slots in the battle array — no units survive.
        var units = new List<BattleUnitState>
        {
            Unit(team: 1, level: 7, maxHp: 75, x: 4, y: 4),
        };
        var active = new HashSet<int>();

        var result = StaleBattleUnitFilter.Filter(units, active);

        Assert.Empty(result);
    }

    [Fact]
    public void ActiveMaxHp_Match_Kept()
    {
        var units = new List<BattleUnitState>
        {
            Unit(team: 1, level: 7, maxHp: 75, x: 4, y: 4),
        };
        var active = new HashSet<int> { 75 };

        var result = StaleBattleUnitFilter.Filter(units, active);

        Assert.Single(result);
        Assert.Equal(75, result[0].MaxHp);
    }

    [Fact]
    public void StaleMaxHp_NotInActive_Dropped()
    {
        // The Mandalia residue case: Lv25 phantom unit MaxHp=263 with
        // no matching active slot.
        var units = new List<BattleUnitState>
        {
            Unit(team: 1, level: 7, maxHp: 75, x: 4, y: 4), // current
            Unit(team: 2, level: 25, maxHp: 263, x: 5, y: 1), // residue
        };
        var active = new HashSet<int> { 75, 64, 68 };

        var result = StaleBattleUnitFilter.Filter(units, active);

        Assert.Single(result);
        Assert.Equal(75, result[0].MaxHp);
    }

    [Fact]
    public void DuplicateMaxHp_BothKept()
    {
        // Two generic Goblins both MaxHp=75 — both are current-battle,
        // both survive. The active-set is HashSet<int> so membership
        // is "any unit with this MaxHp is alive somewhere", not
        // count-matched.
        var units = new List<BattleUnitState>
        {
            Unit(team: 1, level: 7, maxHp: 75, x: 4, y: 4),
            Unit(team: 1, level: 7, maxHp: 75, x: 5, y: 5),
        };
        var active = new HashSet<int> { 75 };

        var result = StaleBattleUnitFilter.Filter(units, active);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void PositionUnknown_StillFiltered()
    {
        // The Mandalia bug also showed positionKnown=false residue
        // (`team:2 level:25 (-1,-1) hp:134/203 positionKnown:false`).
        // These pre-position-discovery entries also need filtering.
        var units = new List<BattleUnitState>
        {
            Unit(team: 2, level: 25, maxHp: 203, x: -1, y: -1),
        };
        var active = new HashSet<int> { 75, 64 };

        var result = StaleBattleUnitFilter.Filter(units, active);

        Assert.Empty(result);
    }

    [Fact]
    public void ActiveUnit_AlwaysKept_EvenIfMaxHpMissing()
    {
        // Defensive: the active unit's slot may not be in the static
        // array snapshot during certain transient states (e.g. just
        // moved, mid-animation). Keep it regardless to avoid losing
        // the player's own data.
        var ramza = Unit(team: 0, level: 8, maxHp: 393, x: 4, y: 4);
        ramza.IsActive = true;
        var units = new List<BattleUnitState> { ramza };
        var active = new HashSet<int>(); // empty — no slot matches

        var result = StaleBattleUnitFilter.Filter(units, active);

        Assert.Single(result);
        Assert.True(result[0].IsActive);
    }

    [Fact]
    public void EmptyInput_ReturnsEmpty()
    {
        var result = StaleBattleUnitFilter.Filter(
            new List<BattleUnitState>(),
            new HashSet<int> { 75 });

        Assert.Empty(result);
    }

    [Fact]
    public void NullActiveSet_NoOp_ReturnsAllInput()
    {
        // Defensive: if the caller can't build an active set (e.g.
        // PollStaticArray hasn't run yet on first frame), don't
        // strip everything — pass through. Filter is best-effort.
        var units = new List<BattleUnitState>
        {
            Unit(team: 1, level: 7, maxHp: 75, x: 4, y: 4),
            Unit(team: 2, level: 25, maxHp: 263, x: 5, y: 1),
        };

        var result = StaleBattleUnitFilter.Filter(units, activeMaxHps: null);

        Assert.Equal(2, result.Count);
    }

    // 2026-04-26 PM playtest: phantom unit at (0,0) with HP=8192/288
    // Lv32 persisted across every scan. hp > maxHp is physically
    // impossible — a unit can't have current HP higher than max.
    // 8192/288 = 28× over-cap; clearly garbage data from somewhere
    // (uninitialized slot, byte-misalignment, prior-battle residue).
    // Reject regardless of MaxHp membership.
    [Fact]
    public void HpExceedsMaxHp_Dropped_EvenIfMaxHpInActiveSet()
    {
        var phantom = Unit(team: 1, level: 32, maxHp: 288, x: 0, y: 0);
        phantom.Hp = 8192; // physically impossible
        var units = new List<BattleUnitState> { phantom };
        var active = new HashSet<int> { 288 }; // MaxHp matches active set

        var result = StaleBattleUnitFilter.Filter(units, active);

        Assert.Empty(result);
    }

    [Fact]
    public void HpExceedsMaxHp_DroppedEvenForActiveUnit()
    {
        // Even the active unit doesn't get a pass on this — if the
        // scan returned hp > maxHp for the player, the data is
        // corrupt and we'd rather drop it than render garbage.
        var ramza = Unit(team: 0, level: 8, maxHp: 393, x: 4, y: 4);
        ramza.Hp = 65535;
        ramza.IsActive = true;
        var units = new List<BattleUnitState> { ramza };

        var result = StaleBattleUnitFilter.Filter(units, new HashSet<int>());

        Assert.Empty(result);
    }

    [Fact]
    public void HpEqualsMaxHp_Kept()
    {
        // Full HP is fine. Boundary case: hp == maxHp is not over-cap.
        var unit = Unit(team: 1, level: 7, maxHp: 75, x: 4, y: 4);
        unit.Hp = 75;
        var units = new List<BattleUnitState> { unit };

        var result = StaleBattleUnitFilter.Filter(units, new HashSet<int> { 75 });

        Assert.Single(result);
    }

    [Fact]
    public void HpZero_Kept()
    {
        // Dead units are fine — hp=0 ≤ maxHp.
        var unit = Unit(team: 1, level: 7, maxHp: 75, x: 4, y: 4);
        unit.Hp = 0;
        var units = new List<BattleUnitState> { unit };

        var result = StaleBattleUnitFilter.Filter(units, new HashSet<int> { 75 });

        Assert.Single(result);
    }
}
