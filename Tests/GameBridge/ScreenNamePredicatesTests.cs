using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for ScreenNamePredicates — small helpers that encapsulate the
    /// "is this a battle screen?" / "is this a party menu?" etc. string
    /// comparisons scattered across NavigationActions and TurnAutoScanner.
    ///
    /// Extracting these predicates (1) gives the checks a name that's easier
    /// to grep for, (2) provides a single place to extend the rule if the
    /// screen-naming convention changes (e.g. if a non-Battle-prefixed screen
    /// ever counts as a battle state), and (3) null-safes the check uniformly.
    /// </summary>
    public class ScreenNamePredicatesTests
    {
        [Theory]
        [InlineData("BattleMyTurn", true)]
        [InlineData("BattleMoving", true)]
        [InlineData("BattleAttacking", true)]
        [InlineData("BattleAbilities", true)]
        [InlineData("BattleWaiting", true)]
        [InlineData("BattleStatus", true)]
        [InlineData("BattlePaused", true)]
        [InlineData("BattleFormation", true)]
        [InlineData("BattleVictory", true)]
        [InlineData("BattleDesertion", true)]
        [InlineData("BattleSequence", true)]
        [InlineData("BattleDialogue", true)]
        [InlineData("BattleEnemiesTurn", true)]
        [InlineData("BattleAlliesTurn", true)]
        [InlineData("BattleActing", true)]
        [InlineData("BattleCasting", true)]
        [InlineData("BattleModalChoice", true)]
        [InlineData("WorldMap", false)]
        [InlineData("PartyMenuUnits", false)]
        [InlineData("EquipmentAndAbilities", false)]
        [InlineData("LocationMenu", false)]
        [InlineData("Tavern", false)]
        [InlineData("TavernRumors", false)]
        [InlineData("TitleScreen", false)]
        [InlineData("LoadGame", false)]
        [InlineData("EncounterDialog", false)]
        [InlineData("Cutscene", false)]
        [InlineData("GameOver", false)]
        public void IsBattleState_Sweep(string screenName, bool expected)
        {
            Assert.Equal(expected, ScreenNamePredicates.IsBattleState(screenName));
        }

        [Fact]
        public void IsBattleState_Null_ReturnsFalse()
        {
            Assert.False(ScreenNamePredicates.IsBattleState(null));
        }

        [Fact]
        public void IsBattleState_Empty_ReturnsFalse()
        {
            Assert.False(ScreenNamePredicates.IsBattleState(""));
        }

        [Fact]
        public void IsBattleState_NonBattlePrefix_ReturnsFalse()
        {
            // Any screen name that doesn't start with "Battle" should be false.
            Assert.False(ScreenNamePredicates.IsBattleState("Unknown"));
            Assert.False(ScreenNamePredicates.IsBattleState("CharacterStatus"));
            Assert.False(ScreenNamePredicates.IsBattleState("JobSelection"));
        }
    }
}
