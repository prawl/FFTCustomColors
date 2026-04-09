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
            tracker.ShouldAutoScan("Battle");

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

            // Intermediate states (acting, enemy turn)
            tracker.ShouldAutoScan("Battle_Acting");
            tracker.ShouldAutoScan("Battle");

            // Turn 2
            Assert.True(tracker.ShouldAutoScan("Battle_MyTurn"));
            tracker.MarkScanned();
            Assert.False(tracker.ShouldAutoScan("Battle_MyTurn"));
        }

        // === Scan caching tests ===

        [Fact]
        public void CachedScan_NullBeforeFirstScan()
        {
            var tracker = new BattleTurnTracker();
            Assert.Null(tracker.CachedScanResponse);
        }

        [Fact]
        public void CachedScan_StoredAfterCacheScan()
        {
            var tracker = new BattleTurnTracker();
            var response = new FFTColorCustomizer.Utilities.CommandResponse { Id = "test", Status = "completed" };
            tracker.CacheScanResponse(response);

            Assert.NotNull(tracker.CachedScanResponse);
            Assert.Equal("test", tracker.CachedScanResponse!.Id);
        }

        [Fact]
        public void CachedScan_ReturnedOnSecondScan()
        {
            var tracker = new BattleTurnTracker();
            var response = new FFTColorCustomizer.Utilities.CommandResponse { Id = "scan1", Status = "completed" };
            tracker.CacheScanResponse(response);
            tracker.MarkScanned();

            // Second scan should return cached
            Assert.True(tracker.HasCachedScan);
            Assert.Equal("scan1", tracker.CachedScanResponse!.Id);
        }

        [Fact]
        public void CachedScan_ClearedOnResetForNewTurn()
        {
            var tracker = new BattleTurnTracker();
            var response = new FFTColorCustomizer.Utilities.CommandResponse { Id = "scan1", Status = "completed" };
            tracker.CacheScanResponse(response);
            tracker.MarkScanned();

            tracker.ResetForNewTurn();

            Assert.Null(tracker.CachedScanResponse);
            Assert.False(tracker.HasCachedScan);
        }

        [Fact]
        public void CachedScan_ClearedOnTurnTransition()
        {
            var tracker = new BattleTurnTracker();
            tracker.ShouldAutoScan("Battle_MyTurn");
            var response = new FFTColorCustomizer.Utilities.CommandResponse { Id = "scan1", Status = "completed" };
            tracker.CacheScanResponse(response);
            tracker.MarkScanned();

            // Enemy turn clears cache
            tracker.ShouldAutoScan("Battle");

            Assert.Null(tracker.CachedScanResponse);
        }
    }
}
