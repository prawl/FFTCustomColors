using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// Pure classifier for screen states encountered mid-turn during
// execute_turn / battle_move / battle_ability / battle_wait execution.
// Distinguishes "keep going / retry" from "bail with context" cases:
//
//   Normal       — BattleMyTurn / BattleMoving / BattleActing etc.
//                  Step succeeded; continue.
//   DialogueInterrupt — BattleDialogue / Cutscene mid-turn. Story event
//                  fired mid-sequence; caller should advance dialogue
//                  before retrying.
//   BattleEnded  — Victory / Defeat / GameOver / Desertion. Turn no
//                  longer applies; accumulator returns gracefully.
//   OutOfBattle  — WorldMap / TitleScreen / PartyMenu etc. Battle ended
//                  abruptly (flee, game crash recover). Bail hard.
//   Unknown      — Screen not recognized. Likely a detection drift;
//                  caller returns fail-loud error.
public class TurnInterruptionClassifierTests
{
    [Theory]
    [InlineData("BattleMyTurn")]
    [InlineData("BattleMoving")]
    [InlineData("BattleAttacking")]
    [InlineData("BattleCasting")]
    [InlineData("BattleAbilities")]
    [InlineData("BattleActing")]
    [InlineData("BattleWaiting")]
    [InlineData("BattleEnemiesTurn")]
    [InlineData("BattleAlliesTurn")]
    [InlineData("BattlePaused")]
    public void BattleStates_ClassifiedAsNormal(string screen)
    {
        Assert.Equal(TurnInterruption.Normal, TurnInterruptionClassifier.Classify(screen));
    }

    [Theory]
    [InlineData("BattleDialogue")]
    [InlineData("Cutscene")]
    public void DialogueScreens_ClassifiedAsDialogueInterrupt(string screen)
    {
        Assert.Equal(TurnInterruption.DialogueInterrupt, TurnInterruptionClassifier.Classify(screen));
    }

    [Theory]
    [InlineData("BattleVictory")]
    [InlineData("BattleDesertion")]
    [InlineData("GameOver")]
    public void EndStates_ClassifiedAsBattleEnded(string screen)
    {
        Assert.Equal(TurnInterruption.BattleEnded, TurnInterruptionClassifier.Classify(screen));
    }

    [Theory]
    [InlineData("WorldMap")]
    [InlineData("TitleScreen")]
    [InlineData("PartyMenuUnits")]
    [InlineData("TravelList")]
    [InlineData("LocationMenu")]
    public void OutOfBattleScreens_ClassifiedAsOutOfBattle(string screen)
    {
        Assert.Equal(TurnInterruption.OutOfBattle, TurnInterruptionClassifier.Classify(screen));
    }

    [Fact]
    public void NullOrEmpty_ClassifiedAsUnknown()
    {
        Assert.Equal(TurnInterruption.Unknown, TurnInterruptionClassifier.Classify(null));
        Assert.Equal(TurnInterruption.Unknown, TurnInterruptionClassifier.Classify(""));
    }

    [Fact]
    public void UnrecognizedScreen_ClassifiedAsUnknown()
    {
        Assert.Equal(TurnInterruption.Unknown, TurnInterruptionClassifier.Classify("SomeFutureScreen"));
    }

    [Fact]
    public void ShouldAbort_ReturnsTrue_ForBattleEnded()
    {
        Assert.True(TurnInterruptionClassifier.ShouldAbortTurn(TurnInterruption.BattleEnded));
    }

    [Fact]
    public void ShouldAbort_ReturnsTrue_ForOutOfBattle()
    {
        Assert.True(TurnInterruptionClassifier.ShouldAbortTurn(TurnInterruption.OutOfBattle));
    }

    [Fact]
    public void ShouldAbort_ReturnsFalse_ForNormal()
    {
        Assert.False(TurnInterruptionClassifier.ShouldAbortTurn(TurnInterruption.Normal));
    }

    [Fact]
    public void ShouldAbort_ReturnsFalse_ForDialogue()
    {
        // Dialogue is recoverable (caller advances then retries) — not a hard abort.
        Assert.False(TurnInterruptionClassifier.ShouldAbortTurn(TurnInterruption.DialogueInterrupt));
    }
}
