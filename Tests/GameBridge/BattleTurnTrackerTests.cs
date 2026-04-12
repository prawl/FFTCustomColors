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
            Assert.True(tracker.ShouldAutoScan("Battle_MyTurn"));
        }

        [Fact]
        public void ShouldScan_SecondBattleMyTurn_ReturnsFalse()
        {
            var tracker = new BattleTurnTracker();
            tracker.ShouldAutoScan("Battle_MyTurn"); // first call triggers
            tracker.MarkScanned();
            Assert.False(tracker.ShouldAutoScan("Battle_MyTurn")); // already scanned this turn
        }

        [Fact]
        public void ShouldScan_AfterEnemyTurnThenMyTurn_ReturnsTrue()
        {
            var tracker = new BattleTurnTracker();
            tracker.ShouldAutoScan("Battle_MyTurn");
            tracker.MarkScanned();

            // Enemy turn
            tracker.ShouldAutoScan("Battle_EnemiesTurn");

            // New turn
            Assert.True(tracker.ShouldAutoScan("Battle_MyTurn"));
        }

        [Fact]
        public void ShouldScan_BattleActing_ReturnsFalse()
        {
            var tracker = new BattleTurnTracker();
            Assert.False(tracker.ShouldAutoScan("Battle_Acting"));
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
            Assert.False(tracker.ShouldAutoScan("Battle_Moving"));
        }

        [Fact]
        public void ShouldScan_ResetsAfterLeavingBattle()
        {
            var tracker = new BattleTurnTracker();
            tracker.ShouldAutoScan("Battle_MyTurn");
            tracker.MarkScanned();

            // Leave battle
            tracker.ShouldAutoScan("WorldMap");

            // Re-enter battle
            Assert.True(tracker.ShouldAutoScan("Battle_MyTurn"));
        }

        [Fact]
        public void ShouldScan_BattleMyTurnWithoutMarkScanned_ReturnsTrueRepeatedly()
        {
            // If scan fails or hasn't been marked, keep returning true
            var tracker = new BattleTurnTracker();
            Assert.True(tracker.ShouldAutoScan("Battle_MyTurn"));
            Assert.True(tracker.ShouldAutoScan("Battle_MyTurn"));
        }

        [Fact]
        public void ShouldScan_MultipleTurns_ScansEachTurn()
        {
            var tracker = new BattleTurnTracker();

            // Turn 1
            Assert.True(tracker.ShouldAutoScan("Battle_MyTurn"));
            tracker.MarkScanned();
            Assert.False(tracker.ShouldAutoScan("Battle_MyTurn"));

            // Intermediate states (enemy turn)
            tracker.ShouldAutoScan("Battle_EnemiesTurn");

            // Turn 2
            Assert.True(tracker.ShouldAutoScan("Battle_MyTurn"));
            tracker.MarkScanned();
            Assert.False(tracker.ShouldAutoScan("Battle_MyTurn"));
        }

        [Theory]
        // Allowed: player has cursor control — safe to scan (C+Up cycling won't corrupt state).
        [InlineData("Battle_MyTurn", true)]
        [InlineData("Battle_Moving", true)]
        [InlineData("Battle_Attacking", true)]
        [InlineData("Battle_Casting", true)]
        [InlineData("Battle_Abilities", true)]
        [InlineData("Battle_Waiting", true)]
        [InlineData("Battle_Paused", true)]
        // Blocked: animations or other teams' turns — scanning would race with state changes.
        [InlineData("Battle_EnemiesTurn", false)]
        [InlineData("Battle_AlliesTurn", false)]
        [InlineData("Battle_Acting", false)]
        [InlineData("Battle_Mettle", false)]
        public void CanScan_CursorControlStates(string screenName, bool expected)
        {
            Assert.Equal(expected, BattleTurnTracker.CanScan(screenName));
        }

        [Fact]
        public void ShouldAutoScan_AfterEnemyTurn_ResetsForNewPlayerTurn()
        {
            var tracker = new BattleTurnTracker();
            tracker.ShouldAutoScan("Battle_MyTurn");
            tracker.MarkScanned();

            // Enemy turn resets scannedThisTurn
            tracker.ShouldAutoScan("Battle_EnemiesTurn");

            // New player turn should auto-scan
            Assert.True(tracker.ShouldAutoScan("Battle_MyTurn"));
        }
    }
}
