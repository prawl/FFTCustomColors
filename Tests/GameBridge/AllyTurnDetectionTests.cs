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

            Assert.Equal("BattleAlliesTurn", result);
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

            Assert.Equal("BattleMyTurn", result);
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

            Assert.Equal("BattleEnemiesTurn", result);
        }

        // The BattleAttacking/BattleMoving/BattleWaiting rules fire on battleMode
        // values (1, 2, 4, 5) that also appear DURING enemy turns — e.g. when an
        // enemy is pathing or targeting. Those rules must be gated on
        // battleTeam==0 (player's turn) so they don't preempt BattleEnemiesTurn.
        // Live-verified 2026-04-19 at Zeklaus event 40: screen reported
        // BattleMoving during enemies' turn.

        [Fact]
        public void DetectScreen_EnemyTurn_BattleMode2_DoesNotFalseTriggerBattleMoving()
        {
            // Enemy pathing: team=1, mode=2 (move mode). Must be BattleEnemiesTurn,
            // not BattleMoving (which means "player selecting a move tile").
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 2, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 1, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false);

            Assert.Equal("BattleEnemiesTurn", result);
        }

        [Fact]
        public void DetectScreen_EnemyTurn_BattleMode4_DoesNotFalseTriggerBattleAttacking()
        {
            // Enemy targeting: team=1, mode=4. Must be BattleEnemiesTurn,
            // not BattleAttacking (which means "player has selected an ability target").
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 4, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 1, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false);

            Assert.Equal("BattleEnemiesTurn", result);
        }

        [Fact]
        public void DetectScreen_AllyTurn_BattleMode2_DoesNotFalseTriggerBattleMoving()
        {
            // NPC ally pathing: team=2, mode=2. Must be BattleAlliesTurn,
            // not BattleMoving.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 2, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 2, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false);

            Assert.Equal("BattleAlliesTurn", result);
        }

        [Fact]
        public void DetectScreen_PostBattleStaleTeam1_DoesNotFalseTriggerBattleEnemiesTurn()
        {
            // Live-captured 2026-04-25 after Siedge Weald battle ended:
            // game returned to world map (screenshot confirmed) but
            // battleTeam=1 stale-persisted from the prior enemy turn.
            // The turn-owner rule fired BattleEnemiesTurn because it
            // had no battleMode guard. battleMode=0 means out-of-battle;
            // turn-owner rules should require battleMode != 0 (during
            // enemy turns battleMode is 1/2/3/4/5 — the comment at the
            // rule site already documents these as the in-battle range).
            // Captured fingerprint:
            //   battleMode=0, paused=0, gameOverFlag=0, battleTeam=1,
            //   battleActed=255, battleMoved=255, encA=8, encB=8,
            //   eventId=65535, submenuFlag=0, rawLocation=255
            // Detection should fall through to a non-battle classifier.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 1, battleActed: 255, battleMoved: 255,
                encA: 8, encB: 8, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0);
            Assert.NotEqual("BattleEnemiesTurn", result);
        }
    }
}
