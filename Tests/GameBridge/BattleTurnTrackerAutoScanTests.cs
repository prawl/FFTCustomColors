using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class BattleTurnTrackerAutoScanTests
    {
        [Fact]
        public void ShouldAutoScan_PlayerTurn_ReturnsTrue()
        {
            var tracker = new BattleTurnTracker();
            Assert.True(tracker.ShouldAutoScan("BattleMyTurn", team: 0));
        }

        [Fact]
        public void ShouldAutoScan_AllyTurn_ReturnsFalse()
        {
            var tracker = new BattleTurnTracker();
            Assert.False(tracker.ShouldAutoScan("BattleMyTurn", team: 2));
        }

        [Fact]
        public void ShouldAutoScan_EnemyTurn_ReturnsFalse()
        {
            var tracker = new BattleTurnTracker();
            Assert.False(tracker.ShouldAutoScan("BattleMyTurn", team: 1));
        }

        [Fact]
        public void ShouldAutoScan_AfterMarkScanned_ReturnsFalse()
        {
            var tracker = new BattleTurnTracker();
            Assert.True(tracker.ShouldAutoScan("BattleMyTurn", team: 0));
            tracker.MarkScanned();
            Assert.False(tracker.ShouldAutoScan("BattleMyTurn", team: 0));
        }

        [Fact]
        public void ShouldAutoScan_NewTurnAfterEnemyPhase_ReturnsTrue()
        {
            var tracker = new BattleTurnTracker();
            tracker.ShouldAutoScan("BattleMyTurn", team: 0);
            tracker.MarkScanned();

            // Enemy phase
            tracker.ShouldAutoScan("BattleEnemiesTurn", team: 1);

            // New player turn
            Assert.True(tracker.ShouldAutoScan("BattleMyTurn", team: 0));
        }

        [Fact]
        public void ShouldAutoScan_AllyTurnDoesNotResetScanFlag()
        {
            var tracker = new BattleTurnTracker();
            // Player turn, scan
            tracker.ShouldAutoScan("BattleMyTurn", team: 0);
            tracker.MarkScanned();

            // Ally turn (team 2) on Battle_MyTurn — should NOT reset scan flag
            tracker.ShouldAutoScan("BattleMyTurn", team: 2);

            // Still same player turn cycle — should not re-scan
            // Only resets after leaving MyTurn state entirely
            Assert.False(tracker.ShouldAutoScan("BattleMyTurn", team: 0));
        }

        [Fact]
        public void ShouldAutoScan_BackwardsCompatible_NoTeamParam()
        {
            // Existing tests use the 1-param overload — make sure it still works
            var tracker = new BattleTurnTracker();
            Assert.True(tracker.ShouldAutoScan("BattleMyTurn"));
        }

        [Fact]
        public void ShouldAutoScan_BattleWaitSkipsTurnPhases_StillScansNextTurn()
        {
            var tracker = new BattleTurnTracker();

            // Turn 1: auto-scan fires
            Assert.True(tracker.ShouldAutoScan("BattleMyTurn", team: 0));
            tracker.MarkScanned();
            Assert.False(tracker.ShouldAutoScan("BattleMyTurn", team: 0));

            // battle_wait skips enemy/ally phases — must call ResetForNewTurn
            tracker.ResetForNewTurn();

            // Turn 2: auto-scan should fire again
            Assert.True(tracker.ShouldAutoScan("BattleMyTurn", team: 0));
        }

        [Fact]
        public void ShouldAutoScan_FirstScreenCheckOnBattleMyTurn_ReturnsTrue()
        {
            // BUG: On the very first turn, the "screen" no-op command should trigger
            // auto-scan. The tracker starts fresh — ShouldAutoScan should return true
            // on the first Battle_MyTurn seen, even without any prior state transitions.
            var tracker = new BattleTurnTracker();

            // First ever check — like calling "screen" after boot lands on Battle_MyTurn
            Assert.True(tracker.ShouldAutoScan("BattleMyTurn", team: 0));
        }

        [Fact]
        public void ShouldAutoScan_FirstTurnTimesOut_SecondScreenStillGetsData()
        {
            // BUG scenario: boot's "enter" triggers auto-scan on first turn,
            // but fft.sh times out (5s) before scan completes. Client sends "screen"
            // next — the tracker already marked it scanned, so no data.
            //
            // The scan should fire on the FIRST command that reaches the check,
            // and if MarkScanned was called, subsequent checks return false.
            // This is correct behavior — the fix is on the client timeout side,
            // not the tracker. This test documents the expected behavior.
            var tracker = new BattleTurnTracker();

            // First command hits Battle_MyTurn — scan triggers
            Assert.True(tracker.ShouldAutoScan("BattleMyTurn", team: 0));
            tracker.MarkScanned();

            // Second command (screen) — already scanned, returns false
            Assert.False(tracker.ShouldAutoScan("BattleMyTurn", team: 0));
        }
    }
}
