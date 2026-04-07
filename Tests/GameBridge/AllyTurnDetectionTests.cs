using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class AllyTurnDetectionTests
    {
        [Fact]
        public void DetectScreen_AllyTurn_Team2NotActedNotMoved()
        {
            // Team 2 = NPC ally (Agrias, Gaffgarion), hasn't acted or moved yet
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 2, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false);

            Assert.Equal("Battle_AlliesTurn", result);
        }

        [Fact]
        public void DetectScreen_MyTurn_Team0NotActedNotMoved()
        {
            // Team 0 = player-controlled, should still be Battle_MyTurn
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false);

            Assert.Equal("Battle_MyTurn", result);
        }

        [Fact]
        public void DetectScreen_EnemyTurn_Team1NotActedNotMoved()
        {
            // Team 1 = enemy turn
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 1, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false);

            Assert.Equal("Battle_EnemiesTurn", result);
        }
    }
}
