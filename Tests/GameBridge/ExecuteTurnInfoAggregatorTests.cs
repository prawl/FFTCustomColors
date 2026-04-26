using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// execute_turn bundles per-step Info from each sub-action (move /
// ability / wait). Today the inline join inside CommandWatcher
// (a) drops the [turn-interrupt] message when stepInfos has entries
// because the join happens AFTER last.Info is mutated, and
// (b) the shell-side narrator only surfaces lines starting with
// "> ", "===", or "[OUTCOME" — so even when stepInfos is preserved
// the per-step Info reaches the user as a hidden field.
//
// Aggregator handles both: prefix each step's Info with "> [action]"
// (so the narrator picks it up), append the turn-interrupt message,
// and pipe-join into one grep-friendly line.
public class ExecuteTurnInfoAggregatorTests
{
    [Fact]
    public void NoSteps_NoInterrupt_ReturnsNull()
    {
        var result = ExecuteTurnInfoAggregator.Aggregate(
            System.Array.Empty<(string, string?)>(),
            null);
        Assert.Null(result);
    }

    [Fact]
    public void EmptyInfoSteps_ReturnsNull()
    {
        var result = ExecuteTurnInfoAggregator.Aggregate(
            new[] { ("battle_move", (string?)null), ("battle_wait", (string?)"") },
            null);
        Assert.Null(result);
    }

    [Fact]
    public void SingleStep_PrefixesWithActionTag()
    {
        var result = ExecuteTurnInfoAggregator.Aggregate(
            new[] { ("battle_move", (string?)"(9,8)->(9,7) CONFIRMED") },
            null);
        Assert.Equal("> [battle_move] (9,8)->(9,7) CONFIRMED", result);
    }

    [Fact]
    public void MultipleSteps_JoinedWithPipeAndEachPrefixed()
    {
        var result = ExecuteTurnInfoAggregator.Aggregate(
            new[]
            {
                ("battle_move", (string?)"(9,8)->(9,7) CONFIRMED"),
                ("battle_ability", (string?)"Used Attack on (9,9) for 87 damage"),
            },
            null);
        Assert.Equal(
            "> [battle_move] (9,8)->(9,7) CONFIRMED | > [battle_ability] Used Attack on (9,9) for 87 damage",
            result);
    }

    [Fact]
    public void TurnInterrupt_AppendedAfterSteps_PrefixedWithGt()
    {
        var result = ExecuteTurnInfoAggregator.Aggregate(
            new[] { ("battle_move", (string?)"(9,8)->(9,7) CONFIRMED") },
            "[turn-interrupt] step 'battle_wait' landed on GameOver — battle ended");
        Assert.Equal(
            "> [battle_move] (9,8)->(9,7) CONFIRMED | > [turn-interrupt] step 'battle_wait' landed on GameOver — battle ended",
            result);
    }

    [Fact]
    public void TurnInterrupt_NoSteps_StillSurfaced()
    {
        var result = ExecuteTurnInfoAggregator.Aggregate(
            System.Array.Empty<(string, string?)>(),
            "[turn-interrupt] step 'battle_wait' landed on GameOver — battle ended");
        Assert.Equal(
            "> [turn-interrupt] step 'battle_wait' landed on GameOver — battle ended",
            result);
    }

    [Fact]
    public void StepAlreadyPrefixedWithGt_NotDoublePrefixed()
    {
        // Some sub-steps already format their own narrator lines (e.g.
        // BattleWait's enemy-turn narrator). Don't double-prefix.
        var result = ExecuteTurnInfoAggregator.Aggregate(
            new[] { ("battle_wait", (string?)"> Enemy moved (5,11)→(8,11)") },
            null);
        Assert.Equal("> Enemy moved (5,11)→(8,11)", result);
    }

    [Fact]
    public void StepWithOutcomeRecapPrefix_LeftAlone()
    {
        // OutcomeRecapRenderer emits "[OUTCOME yours] ..." which is also
        // narrator-recognized. Don't wrap it in another prefix.
        var result = ExecuteTurnInfoAggregator.Aggregate(
            new[] { ("battle_ability", (string?)"[OUTCOME yours] Time Mage HP 225→138 (-87)") },
            null);
        Assert.Equal("[OUTCOME yours] Time Mage HP 225→138 (-87)", result);
    }

    [Fact]
    public void StepWithBannerPrefix_LeftAlone()
    {
        var result = ExecuteTurnInfoAggregator.Aggregate(
            new[] { ("battle_wait", (string?)"=== TURN HANDOFF: Lloyd → Ramza ===") },
            null);
        Assert.Equal("=== TURN HANDOFF: Lloyd → Ramza ===", result);
    }

    [Fact]
    public void TurnInterrupt_AlreadyPrefixed_NotDoubled()
    {
        var result = ExecuteTurnInfoAggregator.Aggregate(
            System.Array.Empty<(string, string?)>(),
            "> [turn-interrupt] custom message");
        Assert.Equal("> [turn-interrupt] custom message", result);
    }
}
