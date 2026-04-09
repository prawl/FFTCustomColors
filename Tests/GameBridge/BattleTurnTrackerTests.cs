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
        public void CachedScan_NotClearedByScreenTransitions()
        {
            // Screen transitions (menus, submenus) should NOT clear the cache.
            // Only explicit game actions (move, attack, ability, wait) clear it.
            var tracker = new BattleTurnTracker();
            tracker.ShouldAutoScan("Battle_MyTurn");
            var response = new FFTColorCustomizer.Utilities.CommandResponse { Id = "scan1", Status = "completed" };
            tracker.CacheScanResponse(response);
            tracker.MarkScanned();

            // All these are still the same turn
            tracker.ShouldAutoScan("Battle_Abilities");
            Assert.True(tracker.HasCachedScan);

            tracker.ShouldAutoScan("Battle_Mettle");
            Assert.True(tracker.HasCachedScan);

            tracker.ShouldAutoScan("Battle_Moving");
            Assert.True(tracker.HasCachedScan);

            tracker.ShouldAutoScan("Battle_Attacking");
            Assert.True(tracker.HasCachedScan);

            tracker.ShouldAutoScan("Battle_Acting");
            Assert.True(tracker.HasCachedScan);

            // Return to Battle_MyTurn — should NOT re-scan
            Assert.False(tracker.ShouldAutoScan("Battle_MyTurn"));
            Assert.True(tracker.HasCachedScan);
        }

        [Fact]
        public void CachedScan_ClearedByInvalidateCache()
        {
            // Game actions (battle_attack, battle_ability, battle_move) call
            // InvalidateCache to force a re-scan on next scan_move.
            var tracker = new BattleTurnTracker();
            tracker.ShouldAutoScan("Battle_MyTurn");
            var response = new FFTColorCustomizer.Utilities.CommandResponse { Id = "scan1", Status = "completed" };
            tracker.CacheScanResponse(response);
            tracker.MarkScanned();

            tracker.InvalidateCache();

            Assert.Null(tracker.CachedScanResponse);
            Assert.False(tracker.HasCachedScan);
            // But scannedThisTurn stays true — auto-scan won't re-fire
            Assert.False(tracker.ShouldAutoScan("Battle_MyTurn"));
        }

        [Fact]
        public void CachedScan_ClearedByResetForNewTurn()
        {
            // battle_wait calls ResetForNewTurn which clears everything
            var tracker = new BattleTurnTracker();
            tracker.ShouldAutoScan("Battle_MyTurn");
            var response = new FFTColorCustomizer.Utilities.CommandResponse { Id = "scan1", Status = "completed" };
            tracker.CacheScanResponse(response);
            tracker.MarkScanned();

            tracker.ResetForNewTurn();

            Assert.Null(tracker.CachedScanResponse);
            Assert.False(tracker.HasCachedScan);
            // And auto-scan should fire again on next turn
            Assert.True(tracker.ShouldAutoScan("Battle_MyTurn"));
        }

        [Theory]
        [InlineData("Battle_MyTurn", true)]
        [InlineData("Battle_EnemiesTurn", false)]
        [InlineData("Battle_AlliesTurn", false)]
        [InlineData("Battle_Acting", false)]
        [InlineData("Battle_Moving", false)]
        [InlineData("Battle_Attacking", false)]
        [InlineData("Battle_Abilities", false)]
        [InlineData("Battle_Mettle", false)]
        [InlineData("Battle_Paused", false)]
        public void CanScan_OnlyDuringMyTurn(string screenName, bool expected)
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
        [Fact]
        public void CachedScan_ClearedWhenUnitChanges_WithoutEnemyTurn()
        {
            // When user plays manually (not via battle_wait), the tracker never sees
            // Battle_EnemiesTurn. It goes Battle_MyTurn(unit1) → Battle_MyTurn(unit2).
            // The cache must invalidate when the unit HP changes (unitId is unreliable).
            var tracker = new BattleTurnTracker();
            tracker.ShouldAutoScan("Battle_MyTurn", unitId: 1, unitHp: 496);
            var response = new FFTColorCustomizer.Utilities.CommandResponse { Id = "scan1", Status = "completed" };
            tracker.CacheScanResponse(response);
            tracker.MarkScanned();

            // New unit's turn — same unitId but different HP
            Assert.True(tracker.ShouldAutoScan("Battle_MyTurn", unitId: 1, unitHp: 475));
            Assert.False(tracker.HasCachedScan);
        }

        [Fact]
        public void CachedScan_NotClearedWhenSameUnit()
        {
            // Same unit, same HP — cache should persist
            var tracker = new BattleTurnTracker();
            tracker.ShouldAutoScan("Battle_MyTurn", unitId: 1, unitHp: 496);
            var response = new FFTColorCustomizer.Utilities.CommandResponse { Id = "scan1", Status = "completed" };
            tracker.CacheScanResponse(response);
            tracker.MarkScanned();

            Assert.False(tracker.ShouldAutoScan("Battle_MyTurn", unitId: 1, unitHp: 496));
            Assert.True(tracker.HasCachedScan);
        }
    }
}
