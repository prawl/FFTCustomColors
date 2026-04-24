using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pin the stale-byte override rule that flips a BattleMoving detection
    /// back to BattleWaiting when the caller just sent a battle_wait Enter
    /// press. battleMode and menuCursor both take time to catch up after the
    /// Wait confirm; polling during that window mislabels the facing screen
    /// as Move. Only overrides within a short window — otherwise a genuine
    /// BattleMoving state would be silently converted.
    /// </summary>
    public class StaleBattleMovingClassifierTests
    {
        [Fact]
        public void RecentWaitEnter_WithBattleMovingDetection_OverridesToWaiting()
        {
            var result = StaleBattleMovingClassifier.ShouldOverrideToBattleWaiting(
                detectedName: "BattleMoving",
                battleMode: 2,
                msSinceLastWaitEnter: 100);
            Assert.True(result);
        }

        [Fact]
        public void OldWaitEnter_BeyondWindow_DoesNotOverride()
        {
            var result = StaleBattleMovingClassifier.ShouldOverrideToBattleWaiting(
                detectedName: "BattleMoving",
                battleMode: 2,
                msSinceLastWaitEnter: 1500);
            Assert.False(result);
        }

        [Fact]
        public void NoWaitEnterEver_SentinelMinusOne_DoesNotOverride()
        {
            var result = StaleBattleMovingClassifier.ShouldOverrideToBattleWaiting(
                detectedName: "BattleMoving",
                battleMode: 2,
                msSinceLastWaitEnter: -1);
            Assert.False(result);
        }

        [Fact]
        public void NonBattleMovingDetection_NoOverride()
        {
            var result = StaleBattleMovingClassifier.ShouldOverrideToBattleWaiting(
                detectedName: "BattleMyTurn",
                battleMode: 2,
                msSinceLastWaitEnter: 100);
            Assert.False(result);
        }

        [Fact]
        public void WrongBattleMode_NoOverride()
        {
            var result = StaleBattleMovingClassifier.ShouldOverrideToBattleWaiting(
                detectedName: "BattleMoving",
                battleMode: 3,
                msSinceLastWaitEnter: 100);
            Assert.False(result);
        }

        [Fact]
        public void NullDetectedName_NoOverride()
        {
            var result = StaleBattleMovingClassifier.ShouldOverrideToBattleWaiting(
                detectedName: null,
                battleMode: 2,
                msSinceLastWaitEnter: 100);
            Assert.False(result);
        }

        [Fact]
        public void CustomWindow_Respected()
        {
            var result200 = StaleBattleMovingClassifier.ShouldOverrideToBattleWaiting(
                detectedName: "BattleMoving",
                battleMode: 2,
                msSinceLastWaitEnter: 300,
                overrideWindowMs: 200);
            Assert.False(result200);

            var result1000 = StaleBattleMovingClassifier.ShouldOverrideToBattleWaiting(
                detectedName: "BattleMoving",
                battleMode: 2,
                msSinceLastWaitEnter: 300,
                overrideWindowMs: 1000);
            Assert.True(result1000);
        }

        [Fact]
        public void BoundaryZero_IsWithinWindow()
        {
            var result = StaleBattleMovingClassifier.ShouldOverrideToBattleWaiting(
                detectedName: "BattleMoving",
                battleMode: 2,
                msSinceLastWaitEnter: 0);
            Assert.True(result);
        }

        [Fact]
        public void BoundaryWindowEdge_IsOutsideWindow()
        {
            var result = StaleBattleMovingClassifier.ShouldOverrideToBattleWaiting(
                detectedName: "BattleMoving",
                battleMode: 2,
                msSinceLastWaitEnter: 500,
                overrideWindowMs: 500);
            Assert.False(result);
        }
    }
}
