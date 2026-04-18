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

        // Session 38: IsPartyMenuTab — the 4 PartyMenu tabs only.
        // Does NOT include drilled-in screens (CharacterStatus etc).

        [Theory]
        [InlineData("PartyMenuUnits", true)]
        [InlineData("PartyMenuInventory", true)]
        [InlineData("PartyMenuChronicle", true)]
        [InlineData("PartyMenuOptions", true)]
        [InlineData("PartyMenu", false)]             // bare prefix — not a real state
        [InlineData("CharacterStatus", false)]       // drilled-in, not a tab
        [InlineData("EquipmentAndAbilities", false)] // drilled-in
        [InlineData("JobSelection", false)]          // drilled-in
        [InlineData("WorldMap", false)]
        [InlineData("BattleMyTurn", false)]
        public void IsPartyMenuTab_Sweep(string screenName, bool expected)
        {
            Assert.Equal(expected, ScreenNamePredicates.IsPartyMenuTab(screenName));
        }

        [Fact]
        public void IsPartyMenuTab_Null_ReturnsFalse()
        {
            Assert.False(ScreenNamePredicates.IsPartyMenuTab(null));
            Assert.False(ScreenNamePredicates.IsPartyMenuTab(""));
        }

        // IsPartyTree: PartyMenuUnits + its drilled-in descendants
        // (CharacterStatus, EquipmentAndAbilities). Used when a helper needs
        // to know "are we somewhere inside the unit-management tree?"

        [Theory]
        [InlineData("PartyMenuUnits", true)]
        [InlineData("CharacterStatus", true)]
        [InlineData("EquipmentAndAbilities", true)]
        [InlineData("JobSelection", true)]            // reached via CharacterStatus sidebar
        [InlineData("PartyMenuInventory", false)]     // different tab — not unit tree
        [InlineData("PartyMenuChronicle", false)]
        [InlineData("PartyMenuOptions", false)]
        [InlineData("WorldMap", false)]
        [InlineData("BattleMyTurn", false)]
        [InlineData("Tavern", false)]
        public void IsPartyTree_Sweep(string screenName, bool expected)
        {
            Assert.Equal(expected, ScreenNamePredicates.IsPartyTree(screenName));
        }

        [Fact]
        public void IsPartyTree_Null_ReturnsFalse()
        {
            Assert.False(ScreenNamePredicates.IsPartyTree(null));
            Assert.False(ScreenNamePredicates.IsPartyTree(""));
        }
    }
}
