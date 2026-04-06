using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class ScreenDetectionTests
    {
        // Step 1: TravelList should never be reported during battle
        [Fact]
        public void DetectScreen_InBattle_WithUiFlag_ShouldNotReturnTravelList()
        {
            // During attack animations, uiFlag flickers to 1 and battleMode flickers to 0.
            // This looks like TravelList (party=0, ui=1) but we're still in battle.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false);

            Assert.NotEqual("TravelList", result);
            Assert.StartsWith("Battle", result);
        }

        [Fact]
        public void DetectScreen_InBattle_NormalMyTurn_ShouldReturnBattleMyTurn()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false);

            Assert.Equal("Battle_MyTurn", result);
        }

        [Fact]
        public void DetectScreen_InBattle_AfterActing_ShouldReturnBattleActing()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false);

            Assert.Equal("Battle_Acting", result);
        }

        [Fact]
        public void DetectScreen_InBattle_EnemyTurn_ShouldReturnBattle()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 1, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false);

            Assert.Equal("Battle", result);
        }

        [Fact]
        public void DetectScreen_WorldMap_ShouldReturnWorldMap()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 26, slot0: 0, slot9: 0,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 5, isPartySubScreen: false);

            Assert.Equal("WorldMap", result);
        }

        [Fact]
        public void DetectScreen_TravelList_NotInBattle_ShouldReturnTravelList()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 26, slot0: 0, slot9: 0,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 5, isPartySubScreen: false);

            Assert.Equal("TravelList", result);
        }

        [Fact]
        public void DetectScreen_EncounterDialog_ShouldReturnEncounterDialog()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 26, slot0: 0, slot9: 0,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 7, isPartySubScreen: false);

            Assert.Equal("EncounterDialog", result);
        }

        [Fact]
        public void DetectScreen_GameOver_ShouldReturnGameOver()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 1, gameOverFlag: 1,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false);

            Assert.Equal("GameOver", result);
        }

        [Fact]
        public void DetectScreen_BattleMoving_ShouldReturnBattleMoving()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 2, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false);

            Assert.Equal("Battle_Moving", result);
        }

        [Fact]
        public void DetectScreen_BattleTargeting_ShouldReturnBattleTargeting()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 2, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false);

            Assert.Equal("Battle_Targeting", result);
        }

        [Fact]
        public void DetectScreen_WorldMap_WithStaleUnitSlots_ShouldReturnWorldMap()
        {
            // After leaving battle, unit slots stay populated (0xFF).
            // If rawLocation is valid and battleMode=0, we're on the world map.
            // The save logic depends on this to write last_location.txt.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 26, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 5, isPartySubScreen: false);

            Assert.Equal("WorldMap", result);
        }

        // The critical regression test: battle with flickering flags
        [Theory]
        [InlineData(0, 0)]  // battleMode=0, ui=0 → could look like WorldMap
        [InlineData(0, 1)]  // battleMode=0, ui=1 → could look like TravelList
        public void DetectScreen_InBattle_WithFlickeringFlags_ShouldStayInBattle(int battleMode, int ui)
        {
            // slot0=255, slot9=0xFFFFFFFF → units exist = we're in battle
            // Even if battleMode/ui flicker, we should stay in battle
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: ui, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: battleMode, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false);

            Assert.StartsWith("Battle", result);
        }
    }
}
