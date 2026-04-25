using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    // Pure classifier for battle-lifecycle events. Given the previous and
    // current screen names, decide what (if anything) to tell the stat
    // tracker. Keeps BattleStatTracker decoupled from screen-detection
    // internals and makes the transition rules unit-testable.
    public class BattleLifecycleClassifierTests
    {
        [Fact]
        public void NullPrevious_AndBattleMyTurn_StartsBattle()
        {
            // Fresh process lands in an already-started battle (e.g. reload).
            var result = BattleLifecycleClassifier.Classify(null, "BattleMyTurn");
            Assert.Equal(BattleLifecycleEvent.StartBattle, result);
        }

        [Theory]
        [InlineData("WorldMap")]
        [InlineData("EncounterDialog")]
        [InlineData("BattleFormation")]
        [InlineData("TravelList")]
        [InlineData("Cutscene")]
        public void NonBattleToBattleMyTurn_StartsBattle(string prev)
        {
            var result = BattleLifecycleClassifier.Classify(prev, "BattleMyTurn");
            Assert.Equal(BattleLifecycleEvent.StartBattle, result);
        }

        [Theory]
        [InlineData("BattleMoving")]
        [InlineData("BattleAttacking")]
        [InlineData("BattleCasting")]
        [InlineData("BattleAbilities")]
        [InlineData("BattleActing")]
        [InlineData("BattleWaiting")]
        [InlineData("BattleEnemiesTurn")]
        [InlineData("BattleAlliesTurn")]
        [InlineData("BattlePaused")]
        [InlineData("BattleDialogue")]
        public void InsideBattle_ToBattleMyTurn_DoesNotRestart(string prev)
        {
            var result = BattleLifecycleClassifier.Classify(prev, "BattleMyTurn");
            Assert.Equal(BattleLifecycleEvent.None, result);
        }

        [Theory]
        [InlineData("BattleVictory")]
        [InlineData("BattleDefeat")]
        [InlineData("GameOver")]
        public void TerminalState_ToBattleMyTurn_DoesNotRestart(string prev)
        {
            // Live-observed at Siedge Weald 2026-04-25: the screen detector
            // flicker-touched BattleVictory mid-attack while the battle was
            // ongoing. Without this guard the lifecycle classifier saw it as
            // a brand-new StartBattle on the rebound, which calls into the
            // CommandWatcher StartBattle handler — that resets the per-turn
            // _actedThisTurn / _movedThisTurn flags, making the bridge forget
            // the player has already used their action. A real victory ends
            // the battle for good and we never see BattleMyTurn again, so
            // this transition can only ever be a flicker.
            var result = BattleLifecycleClassifier.Classify(prev, "BattleMyTurn");
            Assert.Equal(BattleLifecycleEvent.None, result);
        }

        [Fact]
        public void BattleVictory_EndsBattleAsWin()
        {
            var result = BattleLifecycleClassifier.Classify("BattleMyTurn", "BattleVictory");
            Assert.Equal(BattleLifecycleEvent.EndBattleVictory, result);
        }

        [Fact]
        public void BattleDesertion_EndsBattleAsFlee()
        {
            var result = BattleLifecycleClassifier.Classify("BattleMyTurn", "BattleDesertion");
            Assert.Equal(BattleLifecycleEvent.EndBattleDefeat, result);
        }

        [Fact]
        public void VictoryToDesertion_DoesNotReEndBattle()
        {
            // S58 bug: we killed all enemies (BattleVictory fired, EndBattle
            // rolled up stats as Win), then a unit crystallized and the game
            // transitioned to BattleDesertion. The classifier was re-firing
            // EndBattleDefeat, stomping the previous victory with a loss.
            // Post-Victory Desertion is just the game's "unit crystallized"
            // notification — battle already ended.
            var result = BattleLifecycleClassifier.Classify("BattleVictory", "BattleDesertion");
            Assert.Equal(BattleLifecycleEvent.None, result);
        }

        [Fact]
        public void VictoryToGameOver_DoesNotReEndBattle()
        {
            // Same defensive principle: once Victory fires, don't re-end.
            var result = BattleLifecycleClassifier.Classify("BattleVictory", "GameOver");
            Assert.Equal(BattleLifecycleEvent.None, result);
        }

        [Fact]
        public void GameOver_EndsBattleAsDefeat()
        {
            var result = BattleLifecycleClassifier.Classify("BattleMyTurn", "GameOver");
            Assert.Equal(BattleLifecycleEvent.EndBattleDefeat, result);
        }

        [Fact]
        public void AlreadyOnVictory_NoDoubleEnd()
        {
            // Victory→Victory (repeated polls) must not re-fire EndBattle.
            var result = BattleLifecycleClassifier.Classify("BattleVictory", "BattleVictory");
            Assert.Equal(BattleLifecycleEvent.None, result);
        }

        [Fact]
        public void AlreadyOnDesertion_NoDoubleEnd()
        {
            var result = BattleLifecycleClassifier.Classify("BattleDesertion", "BattleDesertion");
            Assert.Equal(BattleLifecycleEvent.None, result);
        }

        [Fact]
        public void AlreadyOnGameOver_NoDoubleEnd()
        {
            var result = BattleLifecycleClassifier.Classify("GameOver", "GameOver");
            Assert.Equal(BattleLifecycleEvent.None, result);
        }

        [Theory]
        [InlineData("WorldMap", "PartyMenuUnits")]
        [InlineData("PartyMenuUnits", "EquipmentAndAbilities")]
        [InlineData("BattleMyTurn", "BattleMoving")]
        [InlineData("BattleAttacking", "BattleMyTurn")]
        public void NonLifecycleTransitions_ReturnNone(string prev, string curr)
        {
            var result = BattleLifecycleClassifier.Classify(prev, curr);
            Assert.Equal(BattleLifecycleEvent.None, result);
        }

        [Fact]
        public void Null_Null_ReturnsNone()
        {
            var result = BattleLifecycleClassifier.Classify(null, null);
            Assert.Equal(BattleLifecycleEvent.None, result);
        }
    }
}
