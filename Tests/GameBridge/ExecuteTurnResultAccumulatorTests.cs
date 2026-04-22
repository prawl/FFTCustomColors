using FFTColorCustomizer.GameBridge;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// Pure accumulator for the execute_turn bundled action. The
// bundled-response returns the LAST sub-step's PostAction today, which
// loses the starting state — callers can't tell "Kenrick HP dropped
// 550 → 437" from "Kenrick HP is 437 now".
//
// Accumulator seeds with the pre-bundle snapshot, records each
// sub-step's PostAction, and exposes deltas across the whole turn:
//   HpDelta = final - initial (positive = healed, negative = hurt)
//   MaxHpChange = any buff/debuff that shifted max
//   MpDelta, position deltas, etc.
//
// Null if either endpoint wasn't captured.
public class ExecuteTurnResultAccumulatorTests
{
    [Fact]
    public void NoSeed_NoSteps_ExposesNullDeltas()
    {
        var acc = new ExecuteTurnResultAccumulator();
        Assert.Null(acc.HpDelta);
        Assert.Null(acc.FinalPostAction);
    }

    [Fact]
    public void Seed_Only_NoSteps_HpDeltaNull()
    {
        var acc = new ExecuteTurnResultAccumulator();
        acc.Seed(new PostActionState { Hp = 500, MaxHp = 500, X = 3, Y = 4 });
        // No final step recorded — no delta yet.
        Assert.Null(acc.HpDelta);
    }

    [Fact]
    public void Seed_Then_OneStep_ExposesHpDelta()
    {
        var acc = new ExecuteTurnResultAccumulator();
        acc.Seed(new PostActionState { Hp = 500, MaxHp = 500 });
        acc.RecordStep("battle_ability", new PostActionState { Hp = 437, MaxHp = 500 });
        Assert.Equal(-63, acc.HpDelta);
    }

    [Fact]
    public void Seed_Then_MultipleSteps_UsesLastPostActionForFinalHp()
    {
        var acc = new ExecuteTurnResultAccumulator();
        acc.Seed(new PostActionState { Hp = 500 });
        // battle_move: HP unchanged
        acc.RecordStep("battle_move", new PostActionState { Hp = 500 });
        // battle_ability: took reaction damage
        acc.RecordStep("battle_ability", new PostActionState { Hp = 480 });
        // battle_wait: counter-kill on approaching enemy
        acc.RecordStep("battle_wait", new PostActionState { Hp = 460 });
        Assert.Equal(-40, acc.HpDelta);
    }

    [Fact]
    public void Seed_PositiveDelta_HealedDuringTurn()
    {
        var acc = new ExecuteTurnResultAccumulator();
        acc.Seed(new PostActionState { Hp = 200, MaxHp = 500 });
        acc.RecordStep("battle_ability", new PostActionState { Hp = 340, MaxHp = 500 });
        Assert.Equal(140, acc.HpDelta);
    }

    [Fact]
    public void RecordStep_WithNullPostAction_DoesNotAdvanceFinal()
    {
        var acc = new ExecuteTurnResultAccumulator();
        acc.Seed(new PostActionState { Hp = 500 });
        acc.RecordStep("battle_move", new PostActionState { Hp = 500 });
        acc.RecordStep("battle_wait", null); // some helpers don't produce a fresh post-action
        // HpDelta falls back to the last step that DID produce one.
        Assert.Equal(0, acc.HpDelta);
    }

    [Fact]
    public void FinalPostAction_IsTheLastRecordedNonNull()
    {
        var acc = new ExecuteTurnResultAccumulator();
        acc.Seed(new PostActionState { Hp = 500 });
        var final = new PostActionState { Hp = 437, X = 7, Y = 5 };
        acc.RecordStep("battle_ability", final);
        Assert.Same(final, acc.FinalPostAction);
    }

    // --- Movement deltas ---

    [Fact]
    public void PreMove_PostMove_AreNull_WhenUnseeded()
    {
        var acc = new ExecuteTurnResultAccumulator();
        Assert.Null(acc.PreMoveX);
        Assert.Null(acc.PreMoveY);
        Assert.Null(acc.PostMoveX);
        Assert.Null(acc.PostMoveY);
    }

    [Fact]
    public void PreMove_ReflectsSeedPosition()
    {
        var acc = new ExecuteTurnResultAccumulator();
        acc.Seed(new PostActionState { X = 3, Y = 4, Hp = 500 });
        Assert.Equal(3, acc.PreMoveX);
        Assert.Equal(4, acc.PreMoveY);
    }

    [Fact]
    public void PostMove_ReflectsFinalStepPosition()
    {
        var acc = new ExecuteTurnResultAccumulator();
        acc.Seed(new PostActionState { X = 3, Y = 4 });
        acc.RecordStep("battle_move", new PostActionState { X = 7, Y = 5 });
        Assert.Equal(7, acc.PostMoveX);
        Assert.Equal(5, acc.PostMoveY);
    }

    [Fact]
    public void PostMove_CarriesThroughLaterSteps()
    {
        // Ability + wait run AFTER move but don't change position.
        var acc = new ExecuteTurnResultAccumulator();
        acc.Seed(new PostActionState { X = 3, Y = 4 });
        acc.RecordStep("battle_move", new PostActionState { X = 7, Y = 5 });
        acc.RecordStep("battle_ability", new PostActionState { X = 7, Y = 5, Hp = 430 });
        acc.RecordStep("battle_wait", new PostActionState { X = 7, Y = 5, Hp = 430 });
        Assert.Equal(7, acc.PostMoveX);
        Assert.Equal(5, acc.PostMoveY);
    }

    [Fact]
    public void MoveDidNotHappen_PreEqualsPost()
    {
        // Skipped-move bundle: ability only. Position unchanged.
        var acc = new ExecuteTurnResultAccumulator();
        acc.Seed(new PostActionState { X = 3, Y = 4 });
        acc.RecordStep("battle_ability", new PostActionState { X = 3, Y = 4 });
        Assert.Equal(3, acc.PreMoveX);
        Assert.Equal(3, acc.PostMoveX);
        Assert.Equal(4, acc.PreMoveY);
        Assert.Equal(4, acc.PostMoveY);
    }

    // --- Killed-unit diff ---
    // Caller snapshots unit list before the bundle starts and again
    // after the final step. Any unit that transitioned HP>0 -> HP<=0
    // counts as a kill and shows up in KilledUnits with its name/job/team.

    [Fact]
    public void NoScanDiff_KilledUnitsEmpty()
    {
        var acc = new ExecuteTurnResultAccumulator();
        Assert.Empty(acc.KilledUnits);
    }

    [Fact]
    public void ScanDiff_HpDroppedToZero_RecordsKill()
    {
        var acc = new ExecuteTurnResultAccumulator();
        var before = new[] { new UnitSnapshot("Goblin", "Goblin", Team: 1, Hp: 120, MaxHp: 120) };
        var after = new[] { new UnitSnapshot("Goblin", "Goblin", Team: 1, Hp: 0, MaxHp: 120) };
        acc.RecordScanDiff(before, after);
        Assert.Single(acc.KilledUnits);
        Assert.Equal("Goblin", acc.KilledUnits[0].Name);
        Assert.Equal(1, acc.KilledUnits[0].Team);
    }

    [Fact]
    public void ScanDiff_HpStillPositive_NotAKill()
    {
        var acc = new ExecuteTurnResultAccumulator();
        var before = new[] { new UnitSnapshot("Goblin", "Goblin", Team: 1, Hp: 120, MaxHp: 120) };
        var after = new[] { new UnitSnapshot("Goblin", "Goblin", Team: 1, Hp: 50, MaxHp: 120) };
        acc.RecordScanDiff(before, after);
        Assert.Empty(acc.KilledUnits);
    }

    [Fact]
    public void ScanDiff_AlreadyDeadBefore_NotAKill()
    {
        // Don't credit kill when unit was already KO before the turn.
        var acc = new ExecuteTurnResultAccumulator();
        var before = new[] { new UnitSnapshot("Goblin", "Goblin", Team: 1, Hp: 0, MaxHp: 120) };
        var after = new[] { new UnitSnapshot("Goblin", "Goblin", Team: 1, Hp: 0, MaxHp: 120) };
        acc.RecordScanDiff(before, after);
        Assert.Empty(acc.KilledUnits);
    }

    [Fact]
    public void ScanDiff_UnitDisappeared_CountsAsKill()
    {
        // Crystallized / despawned units drop out of the scan. Treat as kill
        // if they were alive at the start.
        var acc = new ExecuteTurnResultAccumulator();
        var before = new[] { new UnitSnapshot("Skeleton", "Skeleton", Team: 1, Hp: 80, MaxHp: 80) };
        var after = new UnitSnapshot[0];
        acc.RecordScanDiff(before, after);
        Assert.Single(acc.KilledUnits);
        Assert.Equal("Skeleton", acc.KilledUnits[0].Name);
    }

    [Fact]
    public void ScanDiff_MultipleKills_AllRecorded()
    {
        var acc = new ExecuteTurnResultAccumulator();
        var before = new[]
        {
            new UnitSnapshot("Ramza", "Squire", Team: 0, Hp: 500, MaxHp: 500),
            new UnitSnapshot("Goblin", "Goblin", Team: 1, Hp: 120, MaxHp: 120),
            new UnitSnapshot("Skeleton", "Skeleton", Team: 1, Hp: 80, MaxHp: 80),
        };
        var after = new[]
        {
            new UnitSnapshot("Ramza", "Squire", Team: 0, Hp: 480, MaxHp: 500),
            new UnitSnapshot("Goblin", "Goblin", Team: 1, Hp: 0, MaxHp: 120),
            new UnitSnapshot("Skeleton", "Skeleton", Team: 1, Hp: 0, MaxHp: 80),
        };
        acc.RecordScanDiff(before, after);
        Assert.Equal(2, acc.KilledUnits.Count);
    }

    [Fact]
    public void ScanDiff_FriendlyKO_RecordedAsKillToo()
    {
        // Player-side KO counts for stat tracker (deaths) — accumulator
        // doesn't distinguish team here; caller decides how to label.
        var acc = new ExecuteTurnResultAccumulator();
        var before = new[] { new UnitSnapshot("Ramza", "Squire", Team: 0, Hp: 50, MaxHp: 500) };
        var after = new[] { new UnitSnapshot("Ramza", "Squire", Team: 0, Hp: 0, MaxHp: 500) };
        acc.RecordScanDiff(before, after);
        Assert.Single(acc.KilledUnits);
        Assert.Equal(0, acc.KilledUnits[0].Team);
    }
}
