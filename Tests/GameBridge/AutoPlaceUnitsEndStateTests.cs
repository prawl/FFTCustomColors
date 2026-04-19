using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// Pins the "acceptable screen states to exit auto_place_units poll loop".
// auto_place_units sends Enter*8 + Space + Enter then polls for the battle
// to actually start. The game can land on BattleDialogue or Cutscene FIRST
// (pre-battle story text) before the player's turn — these must end the
// poll, otherwise the helper hangs the full 30s until its fallback path.
// Story battles like Dorter/Orbonne always open with BattleDialogue.
public class AutoPlaceUnitsEndStateTests
{
    [Theory]
    [InlineData("BattleMyTurn")]
    [InlineData("BattleAlliesTurn")]
    [InlineData("BattleEnemiesTurn")]
    [InlineData("BattleActing")]
    [InlineData("BattleDialogue")]
    [InlineData("Cutscene")]
    public void IsBattleStartedState_ReturnsTrue_ForBattleAndPreBattleNarrativeStates(string name)
    {
        Assert.True(AutoPlaceUnitsEndState.IsBattleStartedState(name));
    }

    [Theory]
    [InlineData("BattleFormation")]
    [InlineData("WorldMap")]
    [InlineData("TitleScreen")]
    [InlineData("LocationMenu")]
    [InlineData(null)]
    [InlineData("")]
    public void IsBattleStartedState_ReturnsFalse_ForNonBattleStates(string? name)
    {
        Assert.False(AutoPlaceUnitsEndState.IsBattleStartedState(name));
    }
}
