using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// Pure planner for the direct-cursor-write proto. No memory I/O —
// just validates the request and emits a Plan that the bridge action
// dispatches to GameMemoryScanner.WriteByte.
public class DirectCursorPlannerTests
{
    [Fact]
    public void UnknownCursor_Rejects()
    {
        var p = DirectCursorPlanner.PlanWrite("nope", 0, null, null);
        Assert.Equal(DirectCursorPlanner.PlanKind.Reject, p.Kind);
        Assert.Contains("Unknown cursor", p.Reason);
    }

    [Fact]
    public void IndexOutOfRange_Rejects()
    {
        // battle_menu has 5 slots (0..4). 5 is out of range.
        var p = DirectCursorPlanner.PlanWrite("battle_menu", 5, null, "BattleMyTurn");
        Assert.Equal(DirectCursorPlanner.PlanKind.Reject, p.Kind);
        Assert.Contains("out of range", p.Reason);
    }

    [Fact]
    public void NegativeIndex_Rejects()
    {
        var p = DirectCursorPlanner.PlanWrite("battle_menu", -1, null, "BattleMyTurn");
        Assert.Equal(DirectCursorPlanner.PlanKind.Reject, p.Kind);
    }

    [Fact]
    public void WrongScreen_Rejects()
    {
        // battle_menu requires BattleMyTurn — refuse on WorldMap to
        // prevent writing to that address when its meaning is undefined.
        var p = DirectCursorPlanner.PlanWrite("battle_menu", 2, null, "WorldMap");
        Assert.Equal(DirectCursorPlanner.PlanKind.Reject, p.Kind);
        Assert.Contains("requires screen", p.Reason);
    }

    [Fact]
    public void NullScreen_PassesScreenGuard()
    {
        // Caller couldn't read the screen — let the write proceed.
        // This is the "best-effort" stance; the live verify-after-write
        // step catches misfires.
        var p = DirectCursorPlanner.PlanWrite("battle_menu", 2, null, currentScreen: null);
        Assert.Equal(DirectCursorPlanner.PlanKind.Write, p.Kind);
    }

    [Fact]
    public void ValidRequest_EmitsWritePlan()
    {
        var p = DirectCursorPlanner.PlanWrite("battle_menu", 3, currentValue: 0, "BattleMyTurn");
        Assert.Equal(DirectCursorPlanner.PlanKind.Write, p.Kind);
        Assert.Equal(0x1407FC620, p.Address);
        Assert.Equal((byte)3, p.Value);
    }

    [Fact]
    public void AlreadyAtTarget_Skips()
    {
        // Idempotent: don't write if cursor already holds the target.
        var p = DirectCursorPlanner.PlanWrite("battle_menu", 2, currentValue: 2, "BattleMyTurn");
        Assert.Equal(DirectCursorPlanner.PlanKind.Skip, p.Kind);
    }

    [Fact]
    public void BoundaryIndices_Accepted()
    {
        var min = DirectCursorPlanner.PlanWrite("battle_menu", 0, currentValue: 4, "BattleMyTurn");
        Assert.Equal(DirectCursorPlanner.PlanKind.Write, min.Kind);
        Assert.Equal((byte)0, min.Value);

        var max = DirectCursorPlanner.PlanWrite("battle_menu", 4, currentValue: 0, "BattleMyTurn");
        Assert.Equal(DirectCursorPlanner.PlanKind.Write, max.Kind);
        Assert.Equal((byte)4, max.Value);
    }
}
