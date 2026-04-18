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

        // IsPartyTree: PartyMenuUnits + its roster-view descendants
        // (CharacterStatus, EquipmentAndAbilities). Scoped to match the
        // roster-populating block in CommandWatcher — JobSelection is NOT
        // included because its cursor is per-job-grid, not per-unit-in-roster.

        [Theory]
        [InlineData("PartyMenuUnits", true)]
        [InlineData("CharacterStatus", true)]
        [InlineData("EquipmentAndAbilities", true)]
        [InlineData("JobSelection", false)]           // different cursor semantics
        [InlineData("PartyMenuInventory", false)]     // different tab
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

        // Session 42: IsShopState — the shop-adjacent screens where the
        // Outfitter / Tavern / Warriors Guild / Poachers Den flow lives.
        // Used by gil-display logic and shop navigation helpers.

        [Theory]
        [InlineData("LocationMenu", true)]
        [InlineData("Outfitter", true)]             // SettlementMenu inside Outfitter
        [InlineData("OutfitterBuy", true)]
        [InlineData("OutfitterSell", true)]
        [InlineData("OutfitterFitting", true)]
        [InlineData("Tavern", true)]
        [InlineData("TavernRumors", true)]
        [InlineData("TavernErrands", true)]
        [InlineData("WorldMap", false)]
        [InlineData("BattleMyTurn", false)]
        [InlineData("PartyMenuUnits", false)]
        [InlineData("CharacterStatus", false)]
        [InlineData("Cutscene", false)]
        public void IsShopState_Sweep(string screenName, bool expected)
        {
            Assert.Equal(expected, ScreenNamePredicates.IsShopState(screenName));
        }

        [Fact]
        public void IsShopState_Null_ReturnsFalse()
        {
            Assert.False(ScreenNamePredicates.IsShopState(null));
            Assert.False(ScreenNamePredicates.IsShopState(""));
        }

        [Fact]
        public void IsShopState_And_IsBattleState_AreDisjoint()
        {
            // No screen is simultaneously a shop and a battle state.
            foreach (var name in new[] {
                "LocationMenu", "Outfitter", "OutfitterBuy", "OutfitterSell",
                "OutfitterFitting", "Tavern", "TavernRumors", "TavernErrands",
                "BattleMyTurn", "BattleMoving", "BattleFormation", "BattleVictory",
                "WorldMap", "PartyMenuUnits",
            })
            {
                bool shop = ScreenNamePredicates.IsShopState(name);
                bool battle = ScreenNamePredicates.IsBattleState(name);
                Assert.False(shop && battle, $"{name} classified as both shop and battle");
            }
        }
    }
}
