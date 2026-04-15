using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class BattleTurnTrackerTests
    {
        [Fact]
        public void ShouldScan_FirstBattleMyTurn_ReturnsTrue()
        {
            var tracker = new BattleTurnTracker();
            Assert.True(tracker.ShouldAutoScan("BattleMyTurn"));
        }

        [Fact]
        public void ShouldScan_SecondBattleMyTurn_ReturnsFalse()
        {
            var tracker = new BattleTurnTracker();
            tracker.ShouldAutoScan("BattleMyTurn"); // first call triggers
            tracker.MarkScanned();
            Assert.False(tracker.ShouldAutoScan("BattleMyTurn")); // already scanned this turn
        }

        [Fact]
        public void ShouldScan_AfterEnemyTurnThenMyTurn_ReturnsTrue()
        {
            var tracker = new BattleTurnTracker();
            tracker.ShouldAutoScan("BattleMyTurn");
            tracker.MarkScanned();

            // Enemy turn
            tracker.ShouldAutoScan("BattleEnemiesTurn");

            // New turn
            Assert.True(tracker.ShouldAutoScan("BattleMyTurn"));
        }

        [Fact]
        public void ShouldScan_BattleActing_ReturnsFalse()
        {
            var tracker = new BattleTurnTracker();
            Assert.False(tracker.ShouldAutoScan("BattleActing"));
        }

        [Fact]
        public void ShouldScan_WorldMap_ReturnsFalse()
        {
            var tracker = new BattleTurnTracker();
            Assert.False(tracker.ShouldAutoScan("WorldMap"));
        }

        [Fact]
        public void ShouldScan_BattleMoving_ReturnsFalse()
        {
            var tracker = new BattleTurnTracker();
            Assert.False(tracker.ShouldAutoScan("BattleMoving"));
        }

        [Fact]
        public void ShouldScan_ResetsAfterLeavingBattle()
        {
            var tracker = new BattleTurnTracker();
            tracker.ShouldAutoScan("BattleMyTurn");
            tracker.MarkScanned();

            // Leave battle
            tracker.ShouldAutoScan("WorldMap");

            // Re-enter battle
            Assert.True(tracker.ShouldAutoScan("BattleMyTurn"));
        }

        [Fact]
        public void ShouldScan_BattleMyTurnWithoutMarkScanned_ReturnsTrueRepeatedly()
        {
            // If scan fails or hasn't been marked, keep returning true
            var tracker = new BattleTurnTracker();
            Assert.True(tracker.ShouldAutoScan("BattleMyTurn"));
            Assert.True(tracker.ShouldAutoScan("BattleMyTurn"));
        }

        [Fact]
        public void ShouldScan_MultipleTurns_ScansEachTurn()
        {
            var tracker = new BattleTurnTracker();

            // Turn 1
            Assert.True(tracker.ShouldAutoScan("BattleMyTurn"));
            tracker.MarkScanned();
            Assert.False(tracker.ShouldAutoScan("BattleMyTurn"));

            // Intermediate states (enemy turn)
            tracker.ShouldAutoScan("BattleEnemiesTurn");

            // Turn 2
            Assert.True(tracker.ShouldAutoScan("BattleMyTurn"));
            tracker.MarkScanned();
            Assert.False(tracker.ShouldAutoScan("BattleMyTurn"));
        }

        [Theory]
        // Allowed: player has cursor control — safe to scan (C+Up cycling won't corrupt state).
        [InlineData("BattleMyTurn", true)]
        [InlineData("BattleMoving", true)]
        [InlineData("BattleAttacking", true)]
        [InlineData("BattleCasting", true)]
        [InlineData("BattleAbilities", true)]
        [InlineData("BattleWaiting", true)]
        [InlineData("BattlePaused", true)]
        // Blocked: animations or other teams' turns — scanning would race with state changes.
        [InlineData("BattleEnemiesTurn", false)]
        [InlineData("BattleAlliesTurn", false)]
        [InlineData("BattleActing", false)]
        [InlineData("BattleMettle", false)]
        public void CanScan_CursorControlStates(string screenName, bool expected)
        {
            Assert.Equal(expected, BattleTurnTracker.CanScan(screenName));
        }

        [Fact]
        public void ShouldAutoScan_AfterEnemyTurn_ResetsForNewPlayerTurn()
        {
            var tracker = new BattleTurnTracker();
            tracker.ShouldAutoScan("BattleMyTurn");
            tracker.MarkScanned();

            // Enemy turn resets scannedThisTurn
            tracker.ShouldAutoScan("BattleEnemiesTurn");

            // New player turn should auto-scan
            Assert.True(tracker.ShouldAutoScan("BattleMyTurn"));
        }
    }
}
